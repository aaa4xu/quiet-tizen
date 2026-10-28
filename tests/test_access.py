import base64
import gzip
import hashlib
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import Mock, patch

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))
import tv
from tv import check_identity, decode_pull, launcher_environment, framework_pull_plan, literal_command, observed_firmware, parse_firmware, private_path, script_command, PROFILES
from tv_side import runs_on_tv


class AccessTests(unittest.TestCase):
    def test_target_identity_gate(self):
        for profile in PROFILES:
            with self.subTest(model=profile["model"]):
                check_identity(profile["model"], profile["firmware"])
                with self.assertRaises(ValueError):
                    check_identity("ANOTHER_MODEL", profile["firmware"])
                with self.assertRaises(ValueError):
                    check_identity(profile["model"], "ANOTHER_FIRMWARE")
                with self.assertRaises(ValueError):
                    check_identity(profile["model"], None)

    def test_one_models_firmware_never_unlocks_another(self):
        builds = {(profile["model"], profile["firmware"]) for profile in PROFILES}
        crossed = [(model, firmware) for model, _ in builds for other, firmware in builds
                   if other != model and (model, firmware) not in builds]
        self.assertTrue(crossed, "needs profiles for at least two models")
        for model, firmware in crossed:
            with self.subTest(model=model, firmware=firmware):
                with self.assertRaises(ValueError):
                    check_identity(model, firmware)

    def test_each_model_firmware_pair_has_one_profile(self):
        pairs = [(profile["model"], profile["firmware"]) for profile in PROFILES]
        self.assertEqual(len(pairs), len(set(pairs)))

    @runs_on_tv
    def test_script_reaches_shell_decoded_with_arguments(self):
        script = b'#!/bin/sh\nprintf "%s|" "$@"\n'
        command = script_command(script, ["home-01", "two words", "it's"])
        result = subprocess.run(["sh", "-c", command], check=True, capture_output=True)
        self.assertEqual(result.stdout, b"home-01|two words|it's|")

    @runs_on_tv
    def test_script_gets_the_profile_launcher(self):
        script = b'#!/bin/sh\nprintf "%s|%s|%s" "$QUIET_LAUNCHER_APP" "$QUIET_LAUNCHER_DLL" "$1"\n'
        for profile in PROFILES:
            environment = launcher_environment(profile)
            command = script_command(script, ["home-01"], environment)
            result = subprocess.run(["sh", "-c", command], check=True, capture_output=True, text=True)
            launcher = profile["launcher"]
            self.assertEqual(result.stdout, f"{launcher['app_id']}|{launcher['dll']}|home-01")

    def test_launcher_values_cannot_reach_the_shell_unchecked(self):
        with self.assertRaises(ValueError):
            launcher_environment({"launcher": {"app_id": "a;reboot", "dll": "A.dll"}})

    @runs_on_tv
    def test_command_reaches_shell_with_its_own_quoting(self):
        text = ("printf '%s\\n' hello\n"
                "printf 'a\\tb\\n'\n"
                "echo \"double=$HOME\"\n"
                "echo 'single=$HOME'\n"
                "echo \"escaped=\\$HOME\"\n")
        command = literal_command(text)
        # The agent doubles every backslash before sh sees the text; the wrapper has none.
        self.assertNotIn("\\", command)
        expected = subprocess.run(["sh", "-c", text], check=True, capture_output=True)
        actual = subprocess.run(["sh", "-c", command], check=True, capture_output=True)
        self.assertEqual(actual.stdout, expected.stdout)

    @runs_on_tv
    def test_command_wrapper_preserves_exit_status_and_stderr(self):
        command = literal_command("printf 'out\\n'; printf 'err\\n' >&2; exit 7")
        result = subprocess.run(["sh", "-c", command], capture_output=True)
        self.assertEqual(result.returncode, 7)
        self.assertEqual(result.stdout, b"out\n")
        self.assertEqual(result.stderr, b"err\n")

    def test_oversized_command_is_refused_before_the_root_session(self):
        profile = PROFILES[0]
        with patch("tv.SdbClient"), patch("tv.find_sdb"), \
                patch("tv.observed_model", return_value=profile["model"]), \
                patch("tv.observed_firmware", return_value=profile["firmware"]), \
                patch("tv.probe") as probe, \
                patch("sys.argv", ["tv.py", "--host", "192.0.2.50", "exec",
                                   "--command", "x" * 60000]), \
                patch("sys.stdout"):
            with self.assertRaisesRegex(ValueError, "too large"):
                tv.main()
        probe.assert_not_called()

    def test_crlf_script_is_refused_before_the_root_session(self):
        profile = PROFILES[0]
        with tempfile.TemporaryDirectory() as directory:
            script = Path(directory) / "windows.sh"
            script.write_bytes(b"#!/bin/sh\nset -eu\r\nid\r\n")
            with patch("tv.SdbClient"), patch("tv.find_sdb"), \
                    patch("tv.observed_model", return_value=profile["model"]), \
                    patch("tv.observed_firmware", return_value=profile["firmware"]), \
                    patch("tv.probe") as probe, \
                    patch("sys.argv", ["tv.py", "--host", "192.0.2.50", "exec", "--script", str(script)]), \
                    patch("sys.stdout"):
                with self.assertRaisesRegex(ValueError, "CRLF.*line 2"):
                    tv.main()
        probe.assert_not_called()

    def test_unsupported_model_is_refused_before_any_tv_command(self):
        with patch("tv.SdbClient"), patch("tv.find_sdb"), \
                patch("tv.observed_model", return_value="ANOTHER_MODEL"), \
                patch("tv.observed_firmware") as firmware, \
                patch("sys.argv", ["tv.py", "--host", "192.0.2.50", "exec", "--command", "true"]), \
                patch("sys.stdout"):
            with self.assertRaisesRegex(ValueError, "Unsupported model"):
                tv.main()
        firmware.assert_not_called()

    def test_firmware_is_read_from_product_info(self):
        prd_info = ("[Version]\nModel=T-EXAMPLE-0000;\nVersion=T-EXAMPLE-0000-1000.0;\n"
                    "Build=EXAMPLE-RELEASE;\n\n[Build]\nDate=2026-01-01;\n")
        self.assertEqual(parse_firmware(prd_info), "T-EXAMPLE-0000-1000.0")
        self.assertIsNone(parse_firmware("[Version]\nModel=T-EXAMPLE-0000;\n"))
        client = Mock()
        client.capture.return_value.output = "\n[exit:0]\n"
        self.assertIsNone(observed_firmware(client, "192.0.2.10"))
        self.assertEqual(client.capture.call_args.kwargs["callback_host"], "192.0.2.10")

    @runs_on_tv
    def test_firmware_read_succeeds_without_product_info(self):
        # A TV without the file must yield "unknown", not a failed SDB command.
        client = Mock()
        client.capture.return_value.output = "\n[exit:0]\n"
        observed_firmware(client, "192.0.2.10")
        command = client.capture.call_args.args[0].replace("/etc/prd_info.ini", "/nonexistent/prd_info.ini")
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

    def test_framework_pull_plans_missing_private_copies_and_never_replaces_one(self):
        (ROOT / ".local").mkdir(exist_ok=True)
        with tempfile.TemporaryDirectory(dir=ROOT / ".local") as directory:
            plan = framework_pull_plan("/usr/share/dotnet.tizen/framework", directory)
            self.assertEqual([remote for remote, _ in plan],
                             [f"/usr/share/dotnet.tizen/framework/{name}" for name in tv.FRAMEWORK])
            self.assertIn("Tizen.TV.Broadcast.TVService.dll", tv.FRAMEWORK)
            self.assertEqual({local.parent for _, local in plan}, {Path(directory).resolve()})
            plan[2][1].write_bytes(b"earlier copy")
            rest = framework_pull_plan("/usr/share/dotnet.tizen/framework", directory)
            self.assertNotIn("Tizen.System.dll", [local.name for _, local in rest])
            self.assertEqual(len(rest), len(tv.FRAMEWORK) - 1)
            for _, local in rest:
                local.write_bytes(b"earlier copy")
            with self.assertRaisesRegex(ValueError, "already"):
                framework_pull_plan("/usr/share/dotnet.tizen/framework", directory)
        for source in ("usr/share/dotnet.tizen/framework", "/usr/share\n/framework"):
            with self.assertRaises(ValueError):
                framework_pull_plan(source, ROOT / ".local/tv-framework")
        with self.assertRaises(ValueError):
            framework_pull_plan("/usr/share/dotnet.tizen/framework", ROOT / "tv-framework")

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
