import base64
import gzip
import hashlib
from pathlib import Path
import subprocess
import sys
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))
from tv import check_identity, decode_pull, observed_firmware, parse_firmware, private_path, PROFILE


class AccessTests(unittest.TestCase):
    def test_target_identity_gate(self):
        check_identity(PROFILE["model"], PROFILE["firmware"])
        with self.assertRaises(ValueError):
            check_identity("ANOTHER_MODEL", PROFILE["firmware"])
        with self.assertRaises(ValueError):
            check_identity(PROFILE["model"], "ANOTHER_FIRMWARE")
        with self.assertRaises(ValueError):
            check_identity(PROFILE["model"], None)

    def test_firmware_is_read_from_product_info(self):
        prd_info = ("[Version]\nModel=T-EXAMPLE-0000;\nVersion=T-EXAMPLE-0000-1000.0;\n"
                    "Build=EXAMPLE-RELEASE;\n\n[Build]\nDate=2026-01-01;\n")
        self.assertEqual(parse_firmware(prd_info), "T-EXAMPLE-0000-1000.0")
        self.assertIsNone(parse_firmware("[Version]\nModel=T-EXAMPLE-0000;\n"))
        # A TV without the file must yield "unknown", not a failed SDB command.
        with patch("tv._capture", return_value="") as capture:
            self.assertIsNone(observed_firmware(object()))
        command = capture.call_args.args[1].replace("/etc/prd_info.ini", "/nonexistent/prd_info.ini")
        result = subprocess.run(["sh", "-c", command], capture_output=True)
        self.assertEqual((result.returncode, result.stdout), (0, b""))

    def test_transfer_integrity(self):
        payload = b"synthetic contents only" * 100
        digest = hashlib.sha256(payload).hexdigest()
        body = base64.encodebytes(gzip.compress(payload)).decode()
        self.assertEqual(decode_pull(digest + "\n" + body), payload)
        with self.assertRaises(ValueError):
            decode_pull("0" * 64 + "\n" + body)
        with self.assertRaises(Exception):
            decode_pull(digest + "\n" + body[:-12])

    def test_private_outputs_cannot_escape_checkout_private_directory(self):
        with self.assertRaises(ValueError):
            private_path(ROOT / "public-backup.json")
        with self.assertRaises(ValueError):
            private_path(ROOT / ".local/../public-backup.json")

    def test_runtime_fallback_is_narrow(self):
        from samsung_tv_root.archive_root import _check_target, ArchiveRootError
        from unittest.mock import Mock
        client = Mock()
        capabilities = ("/usr/bin/dotnet cap_setgid,cap_sys_admin=ep\n"
                        "/usr/sbin/sdbd-tarlauncher cap_setgid,cap_setuid=ep")
        with patch("samsung_tv_root.archive_root._capture", side_effect=[
            capabilities, ArchiveRootError("Unknown option --list-runtimes"),
            "Microsoft.NETCore.App 6.0.9 [/usr/share/runtime/6.0.9]",
            "--to-command=COMMAND", "synthetic hash",
        ]) as capture:
            self.assertEqual(_check_target(client).runtime, "6.0")
            self.assertIn("System.Private.CoreLib.dll", capture.call_args_list[2].args[1])
        with patch("samsung_tv_root.archive_root._capture", side_effect=[
            capabilities, ArchiveRootError("permission denied"),
        ]):
            with self.assertRaises(ArchiveRootError):
                _check_target(client)


if __name__ == "__main__":
    unittest.main()
