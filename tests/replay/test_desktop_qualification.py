"""Check executable admission without executing any test application."""

import os
import sys
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from tools.qualify_desktop_control import OwnedWine, checked_command


class DesktopQualificationTests(unittest.TestCase):
    def test_only_named_host_and_sdk_files_are_admitted_as_individual_arguments(self):
        with tempfile.TemporaryDirectory(prefix="tdsblive-command-") as directory:
            root = Path(directory) / "path with spaces"
            root.mkdir()
            for name in ("dotnet", "dotnet.exe", "TDSBLive.exe", "ExtensionSuite.Host.dll"):
                (root / name).touch()
            self.assertEqual([str((root / "TDSBLive.exe").resolve())], checked_command([str(root / "TDSBLive.exe")]))
            for sdk in ("dotnet", "dotnet.exe"):
                expected = [str((root / sdk).resolve()), str((root / "ExtensionSuite.Host.dll").resolve())]
                self.assertEqual(expected, checked_command(expected))

    def test_shells_scripts_flags_extra_arguments_and_missing_files_are_rejected(self):
        with tempfile.TemporaryDirectory(prefix="tdsblive-command-") as directory:
            root = Path(directory)
            for name in ("shell.exe", "host.bat", "dotnet", "ExtensionSuite.Host.dll", "foreign.dll"):
                (root / name).touch()
            invalid = [[], ["--quit"], [str(root / "shell.exe")], [str(root / "host.bat")],
                       [str(root / "dotnet"), str(root / "foreign.dll")],
                       [str(root / "dotnet"), str(root / "ExtensionSuite.Host.dll"), "--extra"],
                       [str(root / "TDSBLive.exe")], [str(root / "TDSBLive.exe") + "\n"],
                       [str(root / "dotnet"), str(root)]]
            for command in invalid:
                with self.subTest(command=command), self.assertRaises((ValueError, FileNotFoundError)):
                    checked_command(command)

    @unittest.skipUnless(sys.platform == "linux", "Owned Wine prefixes require Linux drive symlinks")
    def test_wine_uses_owned_prefix_and_individual_arguments_without_changing_global_environment(self):
        with tempfile.TemporaryDirectory(prefix="tdsblive-command-") as directory:
            root = Path(directory) / "path with spaces"
            root.mkdir()
            for name in ("wine", "wineserver", "TDSBLive.exe", "dotnet", "ExtensionSuite.Host.dll"):
                (root / name).touch()
            profile = root / "owned profile with spaces"
            profile.mkdir()
            with patch.dict(os.environ, {"WINEPREFIX": "/existing user prefix", "WINELOADER": "/other runner",
                                         "WINEDLLOVERRIDES": "user-override=n"}):
                owned = OwnedWine(str(root / "wine"), root)
                command, data_directory = owned.launch([str(root / "TDSBLive.exe")], profile)
                self.assertEqual([str(root / "wine"), str(root / "TDSBLive.exe")], command)
                self.assertEqual("Z:" + str(profile).replace("/", "\\"), data_directory)
                self.assertEqual(str(root / "owned wine prefix"), owned.environment["WINEPREFIX"])
                self.assertEqual(str(root / "wineserver"), owned.environment["WINESERVER"])
                self.assertEqual("mscoree=b;mshtml=", owned.environment["WINEDLLOVERRIDES"])
                self.assertNotIn("WINELOADER", owned.environment)
                self.assertEqual("/existing user prefix", os.environ["WINEPREFIX"])
                self.assertEqual("user-override=n", os.environ["WINEDLLOVERRIDES"])
                self.assertFalse((owned.prefix / "dosdevices").exists(), "Leave fresh drive initialization to Wine")
                with self.assertRaises(ValueError):
                    owned.launch([str(root / "dotnet"), str(root / "ExtensionSuite.Host.dll")], profile)
                with self.assertRaises(ValueError):
                    owned.launch([str(root / "TDSBLive.exe")], root)
                with self.assertRaises(FileExistsError):
                    OwnedWine(str(root / "wine"), root)

    @unittest.skipUnless(sys.platform == "linux", "Owned Wine prefixes require Linux drive symlinks")
    def test_wine_cleanup_waits_only_for_the_owned_prefix_and_never_kills_a_server(self):
        with tempfile.TemporaryDirectory(prefix="tdsblive-command-") as directory:
            root = Path(directory)
            for name in ("wine", "wineserver"):
                (root / name).touch()
            owned = OwnedWine(str(root / "wine"), root)
            with patch("tools.qualify_desktop_control.subprocess.run") as run:
                owned.wait()
            self.assertEqual([str(root / "wineserver"), "--wait"], run.call_args.args[0])
            self.assertEqual(str(owned.prefix), run.call_args.kwargs["env"]["WINEPREFIX"])
            self.assertTrue(run.call_args.kwargs["check"])

    def test_wine_fixture_rejects_non_linux_platform(self):
        with patch("tools.qualify_desktop_control.sys.platform", "win32"), self.assertRaises(ValueError):
            OwnedWine("wine", ".")

    @unittest.skipUnless(sys.platform == "linux", "Owned Wine prefixes require Linux drive symlinks")
    def test_wine_initialization_is_owned_and_preserves_backend_loader(self):
        with tempfile.TemporaryDirectory(prefix="tdsblive-command-") as directory:
            root = Path(directory)
            for name in ("wine", "wineserver"):
                (root / name).touch()
            owned = OwnedWine(str(root / "wine"), root)
            with patch("tools.qualify_desktop_control.subprocess.run") as run:
                with self.assertRaises(RuntimeError):
                    owned.initialize()
                self.assertFalse(owned.initialized)
                self.assertEqual([str(root / "wine"), "wineboot.exe", "--init"], run.call_args.args[0])
                self.assertEqual(str(owned.prefix), run.call_args.kwargs["env"]["WINEPREFIX"])
                self.assertEqual("mscoree,mshtml=", run.call_args.kwargs["env"]["WINEDLLOVERRIDES"])
                self.assertEqual("mscoree=b;mshtml=", owned.environment["WINEDLLOVERRIDES"])
                (owned.prefix / "drive_c").mkdir()
                (owned.prefix / "dosdevices").mkdir()
                (owned.prefix / "dosdevices" / "z:").symlink_to("/", target_is_directory=True)
                owned.initialize()
                self.assertTrue(owned.initialized)
                count = run.call_count
                owned.initialize()
                self.assertEqual(count, run.call_count)


if __name__ == "__main__":
    unittest.main()
