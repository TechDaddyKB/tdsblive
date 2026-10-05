"""Check executable admission without executing any test application."""

import tempfile
import unittest
from pathlib import Path

from tools.qualify_desktop_control import checked_command


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


if __name__ == "__main__":
    unittest.main()
