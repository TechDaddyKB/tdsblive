"""Exercise the real host's isolated desktop lifecycle without a GUI or live integrations.

This is control-channel evidence, not native tray or OBS qualification.
Session capabilities are kept in memory and pipes and are never printed.
"""

import argparse
import concurrent.futures
import contextlib
import http.cookiejar
import json
import queue
import secrets
import socket
import sqlite3
import subprocess
import tempfile
import threading
import time
import urllib.request
from pathlib import Path

SETUP_ROUTE = "/api/setup"


def checked_file(value, names):
    """Only run the expected host/SDK files, never a shell, script or CLI flag."""
    if not value or any(ord(character) < 32 for character in value):
        raise ValueError("An explicit application file path is required")
    candidate = Path(value).resolve(strict=True)
    if not candidate.is_file() or candidate.name.lower() not in names:
        raise ValueError("The application file does not have an expected name")
    return str(candidate)


def checked_command(command):
    if len(command) == 1:
        return [checked_file(command[0], {"tdsblive.exe"})]
    if len(command) == 2:
        return [checked_file(command[0], {"dotnet", "dotnet.exe"}),
                checked_file(command[1], {"extensionsuite.host.dll"})]
    raise ValueError("Only a packaged host or the pinned SDK plus host assembly is allowed")


def free_port():
    with socket.socket() as listener:
        listener.bind(("127.0.0.1", 0))
        return listener.getsockname()[1]


def request(bootstrap, command, timeout=10):
    with socket.create_connection(("127.0.0.1", bootstrap["Port"]), timeout=timeout) as client:
        wire = json.dumps({"SessionToken": bootstrap["SessionToken"], "Command": command}).encode() + b"\n"
        client.sendall(wire)
        with client.makefile("rb") as stream:
            reply = stream.readline(4097)
        if not reply or len(reply) > 4096:
            raise RuntimeError("Desktop control did not provide a bounded reply")
        return json.loads(reply)


def start_host(command, profile):
    command = checked_command(command)
    process = subprocess.Popen(command + ["--TDSBLive:DataDirectory", str(profile),
                                          "--TDSBLive:DesktopMode=external", "--TDSBLive:OpenEditor=false"],
                               stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
    token = secrets.token_hex(32).upper()
    process.stdin.write(json.dumps({"Port": 0, "SessionToken": token}) + "\n")
    process.stdin.close()
    lines = queue.Queue()
    captured = []

    def drain(stream):
        for line in stream:
            captured.append(line)
            lines.put(line)

    for stream in (process.stdout, process.stderr):
        threading.Thread(target=drain, args=(stream,), daemon=True).start()
    try:
        bootstrap = ready(process, lines, token)
        return process, bootstrap, captured
    except BaseException:
        process.terminate()
        process.wait(timeout=15)
        raise


def ready(process, lines, token):
    deadline = time.monotonic() + 30
    while time.monotonic() < deadline:
        if process.poll() is not None:
            raise RuntimeError("Owned host stopped before desktop readiness")
        try:
            line = lines.get(timeout=0.1)
        except queue.Empty:
            continue
        if line.startswith("TDSBLIVE-DESKTOP "):
            bootstrap = {"Port": int(line.split()[1]), "SessionToken": token}
            return running(bootstrap, deadline)
    raise RuntimeError("Owned host readiness timed out")


def running(bootstrap, deadline):
    while time.monotonic() < deadline:
        if request(bootstrap, "status")["State"] == "running":
            return bootstrap
        time.sleep(0.05)
    raise RuntimeError("Owned host readiness timed out")


class BrowserClient:
    """Same loopback cookie, Origin and CSRF requirements as the real editor."""

    def __init__(self, port):
        self.origin = f"http://127.0.0.1:{port}"
        self.opener = urllib.request.build_opener(urllib.request.ProxyHandler({}),
                                                  urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
        self.csrf = self.send("/api/auth/csrf")["requestToken"]

    def send(self, route, body=None, method="GET", raw=False):
        headers = {"Origin": self.origin}
        if method != "GET":
            headers["X-TDSBLive-CSRF"] = self.csrf
            headers["Content-Type"] = "application/zip" if raw else "application/json"
        data = None
        if body is not None:
            data = body if raw else json.dumps(body).encode()
        if method != "GET" and data is None:
            data = b""
        message = urllib.request.Request(self.origin + route, data=data, headers=headers, method=method)
        with self.opener.open(message, timeout=30) as response:
            content = response.read()
            return content if response.headers.get_content_type() == "application/zip" else json.loads(content)


def stop_with_browser(command, profile, port, operation):
    process, bootstrap, captured = start_host(command, profile)
    try:
        browser = BrowserClient(port)
        if operation == "restore":
            progress = browser.send(SETUP_ROUTE)
            browser.send(SETUP_ROUTE, {"step": 1, "reviewed": False, "version": progress["version"]}, "PUT")
            backup = browser.send("/api/recovery/backup", method="POST")
            progress = browser.send(SETUP_ROUTE)
            browser.send(SETUP_ROUTE, {"step": 3, "reviewed": False, "version": progress["version"]}, "PUT")
            checked = browser.send("/api/recovery/validate", backup, "POST", raw=True)
        with concurrent.futures.ThreadPoolExecutor(max_workers=1) as executor:
            outcome = executor.submit(request, bootstrap, "wait", 30)
            if operation == "restore":
                browser.send("/api/recovery/restore", {"id": checked["id"], "confirm": True}, "POST")
            else:
                browser.send("/api/application/restart", method="POST")
            assert outcome.result(timeout=30)["State"] == "restart-ready"
        assert process.wait(timeout=15) == 0
        assert all(bootstrap["SessionToken"] not in line for line in captured)
    finally:
        if process.poll() is None:
            process.terminate()
            process.wait(timeout=15)


def qualify(command):
    command = checked_command(command)
    with tempfile.TemporaryDirectory(prefix="tdsblive-desktop-control-") as temporary:
        profile = Path(temporary) / "profile with spaces"
        profile.mkdir()
        port = free_port()
        config = {"server": {"host": "127.0.0.1", "port": port}, "displayName": "Owned tray qualification"}
        (profile / "configuration.json").write_text(json.dumps(config), encoding="utf-8")
        process, bootstrap, captured = start_host(command, profile)
        try:
            assert request(bootstrap, "status")["EditorUrl"] == f"http://127.0.0.1:{port}/editor"
            duplicate = subprocess.run(command + ["--TDSBLive:DataDirectory", str(profile), "--TDSBLive:DesktopMode=external"],
                                       capture_output=True, text=True, timeout=15)
            assert duplicate.returncode == 0
            deadline = time.monotonic() + 5
            while request(bootstrap, "status")["OpenRequests"] != 1 and time.monotonic() < deadline:
                time.sleep(0.05)
            assert request(bootstrap, "status")["OpenRequests"] == 1
            with concurrent.futures.ThreadPoolExecutor(max_workers=1) as executor:
                outcome = executor.submit(request, bootstrap, "wait", 30)
                assert request(bootstrap, "restart")["Accepted"]
                assert outcome.result(timeout=30)["State"] == "restart-ready"
            assert process.wait(timeout=15) == 0
            assert all(bootstrap["SessionToken"] not in line for line in captured)
        finally:
            if process.poll() is None:
                process.terminate()
                process.wait(timeout=15)

        # Browser-triggered restart/restore obey the same external ownership.
        stop_with_browser(command, profile, port, "restart")
        stop_with_browser(command, profile, port, "restore")

        # Relaunch is explicitly owned by the external companion, never host death.
        process, bootstrap, captured = start_host(command, profile)
        try:
            assert BrowserClient(port).send(SETUP_ROUTE)["step"] == 1
            with concurrent.futures.ThreadPoolExecutor(max_workers=1) as executor:
                outcome = executor.submit(request, bootstrap, "wait", 30)
                assert request(bootstrap, "quit")["Accepted"]
                assert outcome.result(timeout=30)["State"] == "quit"
            assert process.wait(timeout=15) == 0
            assert all(bootstrap["SessionToken"] not in line for line in captured)
        finally:
            if process.poll() is None:
                process.terminate()
                process.wait(timeout=15)
        # sqlite3's own context manager commits/rolls back but does not close.
        # Release this owned inspection handle before Windows removes the profile.
        with contextlib.closing(sqlite3.connect(profile / "tdsblive.db")) as database:
            assert database.execute("PRAGMA integrity_check").fetchone()[0] == "ok"
        assert json.loads((profile / "configuration.json").read_text(encoding="utf-8"))["displayName"] == config["displayName"]
        assert not (profile / "tdsblive.db-wal").exists() or (profile / "tdsblive.db-wal").stat().st_size == 0
    print("PASS: actual owned host readiness, duplicate launch, tray/browser restart, browser restore, quit, retained profile and SQLite integrity")
    print("Native tray, Windows/Linux desktop and OBS evidence remain separate requirements.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    entrypoint = parser.add_mutually_exclusive_group(required=True)
    entrypoint.add_argument("--dotnet")
    entrypoint.add_argument("--executable")
    parser.add_argument("--assembly")
    options = parser.parse_args()
    if options.dotnet and not options.assembly:
        parser.error("--dotnet requires --assembly")
    qualify([options.dotnet, str(Path(options.assembly).resolve())] if options.dotnet else [str(Path(options.executable).resolve())])
