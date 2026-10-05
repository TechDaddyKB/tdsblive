#!/usr/bin/env python3
"""Exercise the packaged native first-run window on an owned X11 display."""
import argparse
import json
import os
from pathlib import Path
import select
import shutil
import subprocess
import tempfile
import time

from build_linux_package import ROOT, scan, validate_bundle
from rumble_evidence import safe_read


def owned_path(path):
    resolved = path.resolve()
    if not resolved.is_relative_to(ROOT / 'release'):
        raise ValueError('Package qualification paths must be inside the ignored release directory')
    return resolved


def command(arguments, env):
    return subprocess.run(arguments, env=env, capture_output=True, text=True, check=True, timeout=15).stdout


def startup_line(process):
    if not select.select([process.stdout], [], [], 10)[0]:
        raise ValueError('Owned display or session bus startup timed out')
    return process.stdout.readline().strip()


def find_window(app, env):
    for _ in range(100):
        if app.poll() is not None:
            raise ValueError('Native app exited before showing setup')
        result = subprocess.run(['xdotool', 'search', '--all', '--onlyvisible', '--pid', str(app.pid),
                                 '--name', '^Set up TDSBLive on Linux$'],
                                env=env, capture_output=True, text=True, timeout=5)
        if result.returncode == 0 and result.stdout.strip():
            return result.stdout.strip().splitlines()[0]
        time.sleep(.1)
    raise ValueError('Native first-run setup window did not appear')


def capture(window, env, target):
    bounds = dict(line.split('=', 1) for line in
                  command(['xdotool', 'getwindowgeometry', '--shell', window], env).splitlines() if '=' in line)
    command(['ffmpeg', '-hide_banner', '-loglevel', 'error', '-y', '-f', 'x11grab', '-video_size',
             bounds['WIDTH'] + 'x' + bounds['HEIGHT'], '-i',
             env['DISPLAY'] + '+' + bounds['X'] + ',' + bounds['Y'],
             '-frames:v', '1', '-update', '1', '-threads', '1', str(target)], env)


def qualify(application):
    application = owned_path(application)
    evidence = ROOT / 'release' / 'linux-native-evidence'
    # This fixed output location is independent of the caller's package path.
    # Reject aliases instead of resolving a symlink to a different destination.
    if evidence.resolve() != evidence:
        raise ValueError('The qualification evidence directory must not use links')
    if evidence.exists():
        raise ValueError('Use a fresh qualification evidence directory')
    scan(application)
    validate_bundle(application)
    marker = json.loads(safe_read(application / 'TDSBLive.package.json'))
    commit = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip()
    if marker.get('target') != 'linux-x64-wine' or marker.get('sourceCommit') != commit:
        raise ValueError('Qualify the Linux package from this exact source commit')
    for name in ('Xvfb', 'xdotool', 'ffmpeg', 'dbus-daemon'):
        if shutil.which(name) is None:
            raise ValueError('Missing qualification prerequisite: ' + name)
    evidence.mkdir(mode=0o700, parents=True)
    with tempfile.TemporaryDirectory(prefix='tdsblive-linux-package-') as temporary:
        root = Path(temporary)
        runtime = root / 'runtime'
        runtime.mkdir(mode=0o700)
        app = display = bus = None
        try:
            display = subprocess.Popen(['Xvfb', '-displayfd', '1', '-screen', '0', '1280x900x24',
                                        '-nolisten', 'tcp'], stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, text=True)
            number = startup_line(display)
            if not number.isdecimal():
                raise ValueError('Owned X11 display could not start')
            bus = subprocess.Popen(['dbus-daemon', '--session', '--nofork', '--print-address=1'],
                                   stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, text=True)
            address = startup_line(bus)
            if not address.startswith('unix:'):
                raise ValueError('Owned session bus could not start')
            env = dict(os.environ, DISPLAY=':' + number, DBUS_SESSION_BUS_ADDRESS=address,
                       XDG_RUNTIME_DIR=str(runtime), XDG_CONFIG_HOME=str(root / 'choices'),
                       XDG_DATA_HOME=str(root / 'applications'), WINEPREFIX=str(root / 'unstarted-prefix'))
            env.pop('XDO_DEBUG', None)
            with (evidence / 'native-app.log').open('x') as log:
                app = subprocess.Popen([str(application / 'TDSBLive')], cwd=application, env=env,
                                       stdin=subprocess.DEVNULL, stdout=log, stderr=log)
                window = find_window(app, env)
                time.sleep(.5)
                capture(window, env, evidence / 'setup-window.png')
                command(['xdotool', 'windowsize', '--sync', window, '320', '360'], env)
                time.sleep(.3)
                capture(window, env, evidence / 'setup-window-narrow.png')
                command(['xdotool', 'windowfocus', '--sync', window], env)
                command(['xdotool', 'key', '--window', window, 'Escape'], env)
                if app.wait(timeout=10) != 0:
                    raise ValueError('Cancel did not exit the native companion successfully')
            if (root / 'unstarted-prefix').exists() or (root / 'choices/tdsblive/launcher.json').exists():
                raise ValueError('Cancel unexpectedly created a prefix or saved launcher choices')
            result = {'sourceCommit': commit, 'version': marker['version'], 'runtime': 'native self-contained linux-x64',
                      'checks': ['actual first-run window', '320x360 resize', 'Escape cancel with exit 0',
                                 'no created Wine prefix', 'no saved launcher choices'],
                      'limitations': ['Xvfb with private session bus; no desktop tray host',
                                      'No Wine/UMU backend started; desktop/runner lifecycle remains separate']}
            with (evidence / 'qualification.json').open('x', encoding='utf-8') as output:
                json.dump(result, output, indent=2)
                output.write('\n')
            scan(evidence)
            print('Packaged native first-run rendering, resize and isolated cancellation passed.')
        finally:
            for process in (app, bus, display):
                if process is not None and process.poll() is None:
                    process.terminate()
                    process.wait(timeout=10)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--application-directory', type=Path, required=True)
    arguments = parser.parse_args()
    qualify(arguments.application_directory)
