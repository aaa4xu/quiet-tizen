import hashlib
import os
import re
import sqlite3
import subprocess
import sys
import tarfile
import tempfile
from pathlib import Path
import unittest
from contextlib import closing, redirect_stdout
from io import StringIO
from unittest.mock import patch
from tv_side import runs_on_tv

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))
import update
from update import check_package, pack

CONTAINER = "example.quiet-home-host"


class LauncherRegistryTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.directory = Path(self.temp.name)
        self.conn = sqlite3.connect(self.directory / "live.db")
        self.conn.executescript(f"""
          CREATE TABLE package_app_info(app_id TEXT, app_disable TEXT, app_onboot TEXT,
                                        app_autorestart TEXT, other TEXT);
          INSERT INTO package_app_info VALUES
            ('{CONTAINER}','true','true','true','keep'),
            ('unrelated.app','true','true','true','keep');
        """)
        with closing(sqlite3.connect(self.directory / ".pkgmgr_parser.db")) as snapshot:
            self.conn.backup(snapshot)

    def tearDown(self):
        self.conn.close()
        self.temp.cleanup()

    def sql(self, recipe):
        script = (ROOT / "recipes" / recipe).read_text(encoding="utf-8")
        sql = re.search(r"<<SQL\n(.*?)\nSQL", script, re.S).group(1)
        sql = sql.replace("$D", str(self.directory)).replace("$container", CONTAINER)
        self.assertNotIn("$", sql)  # every shell variable in the SQL is accounted for
        return sql

    def rows(self):
        return self.conn.execute("SELECT * FROM package_app_info ORDER BY app_id").fetchall()

    def test_install_enables_only_the_container_and_restore_returns_its_flags(self):
        self.conn.executescript(self.sql("install-launcher.sh"))
        self.assertEqual(self.rows(), [(CONTAINER, "false", "false", "false", "keep"),
                                       ("unrelated.app", "true", "true", "true", "keep")])
        self.conn.execute("UPDATE package_app_info SET other='updated'")
        self.conn.commit()
        self.conn.executescript(self.sql("restore-launcher.sh"))
        self.assertEqual(self.rows(), [(CONTAINER, "true", "true", "true", "updated"),
                                       ("unrelated.app", "true", "true", "true", "updated")])

    def test_missing_container_refuses_install(self):
        self.conn.execute(f"DELETE FROM package_app_info WHERE app_id='{CONTAINER}'")
        self.conn.commit()
        with self.assertRaises(sqlite3.IntegrityError):
            self.conn.executescript(self.sql("install-launcher.sh"))
        self.conn.rollback()
        self.assertEqual(self.rows(), [("unrelated.app", "true", "true", "true", "keep")])

    def preflight_passes(self, recipe, first_change):
        script = (ROOT / "recipes" / recipe).read_text(encoding="utf-8")
        block = re.search(r"^for db in .*?^done$", script, re.S | re.M)
        self.assertLess(block.start(), script.index(first_change))  # nothing is changed before it
        check = block.group(0).replace("/opt/dbspace/.pkgmgr_parser.db", str(self.directory / "live.db"))
        check = check.replace("$container", CONTAINER)
        return subprocess.run(["sh", "-euc", check], env={"D": str(self.directory), "PATH": "/usr/bin:/bin"},
                              capture_output=True).returncode == 0

    @runs_on_tv
    def test_container_row_is_checked_before_any_change(self):
        for recipe, first_change in [("install-launcher.sh", 'mkdir "$S/unpacked"'),
                                     ("restore-launcher.sh", '"$D/Probe.dll" set-home')]:
            self.assertTrue(self.preflight_passes(recipe, first_change))
            self.conn.execute(f"INSERT INTO package_app_info (app_id) VALUES ('{CONTAINER}')")
            self.conn.commit()
            self.assertFalse(self.preflight_passes(recipe, first_change))  # duplicate row
            self.conn.execute(f"DELETE FROM package_app_info WHERE app_id='{CONTAINER}'")
            self.conn.commit()
            self.assertFalse(self.preflight_passes(recipe, first_change))  # missing row
            self.conn.execute(f"INSERT INTO package_app_info VALUES ('{CONTAINER}','true','true','true','keep')")
            self.conn.commit()


class ContainerBackupTests(unittest.TestCase):
    def recipe(self, name):
        return (ROOT / "recipes" / name).read_text(encoding="utf-8")

    def test_snapshot_records_what_install_checks(self):
        snapshot = self.recipe("snapshot.sh")
        for record in ('> "$D/original-assembly.sha256"', '> "$D/original-metadata.txt"',
                       'tar -czf "$original.tar.gz"'):
            self.assertIn(record, snapshot)
        self.assertIn("stat -c '%a ", snapshot)  # the mode comes first

    def test_install_checks_the_backup_before_any_change_and_keeps_its_mode(self):
        install = self.recipe("install-launcher.sh")
        first_change = install.index('mkdir "$S/unpacked"')
        for check in ('sha256sum -c "$D/original-assembly.sha256"', 'test -s "$original.tar.gz"',
                      'test ! -e "$D/installed-home.sha256"',
                      'mode=$(cut -d \' \' -f 1 "$D/original-metadata.txt")'):
            self.assertLess(install.index(check), first_change)
        self.assertNotIn('cp -a "$A"', install)  # the snapshot owns the backup
        self.assertIn('chmod "$mode" "$A/bin/$dll"', install)
        self.assertNotRegex(install, r'chmod [0-7]+ "\$A/bin/\$dll"')

    def test_restore_checks_the_backup_digest_before_any_change(self):
        restore = self.recipe("restore-launcher.sh")
        check = restore.index('sha256sum -c "$D/original-assembly.sha256"')
        self.assertLess(check, restore.index('"$D/Probe.dll" set-home'))

class RecordedContainerTests(unittest.TestCase):
    """install-launcher.sh and restore-launcher.sh take the container the snapshot backed up."""

    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.directory = Path(self.temp.name)

    def tearDown(self):
        self.temp.cleanup()

    def header(self, recipe, args, environment=None, recorded=None):
        if recorded is not None:
            (self.directory / "launcher-container.txt").write_text(recorded + "\n")
        script = (ROOT / "recipes" / recipe).read_text(encoding="utf-8")
        script = script[:script.index("\nA=/opt/usr/apps/")]
        script = script.replace('test "$(id -u)" = 0\n', "")
        script = script.replace("D=/opt/usr/share/quiet-tizen-backups/$name", f"D={self.directory}")
        script += '\nprintf "%s %s" "$container" "$dll"\n'
        result = subprocess.run(["sh", "-c", script, "sh", *args], capture_output=True, text=True,
                                env={"PATH": "/usr/bin:/bin", **(environment or {})})
        return result.stdout if result.returncode == 0 else None

    @runs_on_tv
    def test_install_uses_the_recorded_container_of_the_same_profile(self):
        profile = {"QUIET_LAUNCHER_APP": CONTAINER, "QUIET_LAUNCHER_DLL": "Host.dll"}
        self.assertIsNone(self.header("install-launcher.sh", ["home-01"], profile))  # no container backup
        self.assertEqual(self.header("install-launcher.sh", ["home-01"], profile, f"{CONTAINER} Host.dll"),
                         f"{CONTAINER} Host.dll")
        other = {"QUIET_LAUNCHER_APP": "other.host", "QUIET_LAUNCHER_DLL": "Host.dll"}
        self.assertIsNone(self.header("install-launcher.sh", ["home-01"], other))

    @runs_on_tv
    def test_restore_prefers_the_recorded_container(self):
        ids = ["home-01", "op.app", "home.app", "startup.app"]
        self.assertEqual(self.header("restore-launcher.sh", ids),
                         "com.samsung.tv.bixby-promotion BixbyPromotion.dll")  # older snapshot
        self.assertEqual(self.header("restore-launcher.sh", [*ids, CONTAINER, "Host.dll"]), f"{CONTAINER} Host.dll")
        self.assertEqual(self.header("restore-launcher.sh", ids, recorded=f"{CONTAINER} Host.dll"),
                         f"{CONTAINER} Host.dll")
        self.assertIsNone(self.header("restore-launcher.sh", [*ids, "other.host", "Host.dll"]))


class UpdateTests(unittest.TestCase):
    """Install and recover on a synthetic TV filesystem, with Samsung tools stubbed out."""

    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        root = Path(self.temp.name)
        self.backup = root / "backups/home-01"
        self.app = root / f"apps/{CONTAINER}"
        self.stage = root / "stage"
        self.pending = self.backup / "update-pending"
        stubs = root / "stubs"
        for directory in (self.backup, self.app / "bin", self.app / "res/quiet-home", self.stage, stubs):
            directory.mkdir(parents=True)
        for tool in ("app_launcher", "chown", "chsmack"):
            (stubs / tool).write_text("#!/bin/sh\nexit 0\n")
            (stubs / tool).chmod(0o755)
        (stubs / "stat").write_text("#!/bin/sh\nprintf '201:0\\n'\n")
        (stubs / "stat").chmod(0o755)
        (self.backup / "launcher-container.txt").write_text(f"{CONTAINER} Host.dll\n")
        (self.backup / "original-metadata.txt").write_text("644 201:0 10\n")
        self.original = self.backup / "samsung-original.dll"
        self.original.write_text("original Samsung binary")
        self.dll = self.app / "bin/Host.dll"
        self.dll.write_text("build-1")
        (self.app / "res/quiet-home/apps.json").write_text("catalog-build-1")
        self.script = (ROOT / "recipes/update-launcher.sh").read_text(encoding="utf-8")
        self.script = self.script.replace('test "$(id -u)" = 0\n', "")
        for path, local in (("/opt/usr/share/quiet-tizen-backups", root / "backups"),
                            ("/opt/usr/apps", root / "apps"), ("/home/owner/share/tmp/sdk_tools/quiet-tizen", self.stage)):
            self.script = self.script.replace(path, str(local))
        self.environment = {"PATH": f"{stubs}:{os.environ['PATH']}:/sbin"}
        digest = hashlib.sha256(b"build-1").hexdigest()
        (self.backup / "installed-home.sha256").write_text(f"{digest}  {self.dll}\n")

    def tearDown(self):
        self.temp.cleanup()

    def run_recipe(self, action, script=None):
        self.last = subprocess.run(["sh", "-c", script or self.script, "sh", action, "home-01"],
                                   capture_output=True, text=True, env=self.environment)
        return self.last.returncode == 0

    def stage_build(self, build, dll=None):
        source = Path(self.temp.name) / build
        (source / "quiet-home").mkdir(parents=True, exist_ok=True)
        (source / "Host.dll").write_text(dll or build)
        (source / "quiet-home/apps.json").write_text(f"catalog-{build}")
        pack(source, self.stage / "package.tar.gz")

    def installed(self):
        return self.dll.read_text(), (self.app / "res/quiet-home/apps.json").read_text()

    def assert_consistent(self, build, dll=None):
        self.assertEqual(self.installed(), (dll or build, f"catalog-{build}"))
        recorded = (self.backup / "installed-home.sha256").read_text().split()[0]
        self.assertEqual(recorded, hashlib.sha256(self.dll.read_bytes()).hexdigest())
        self.assertEqual(self.original.read_text(), "original Samsung binary")
        self.assertFalse(self.pending.exists())

    def interrupted_script(self, step, signal='kill -KILL $$'):
        before, separator, after = self.script.rpartition(step + "\n")
        self.assertTrue(separator, step)
        return before + separator + signal + "\n" + after

    @runs_on_tv
    def test_install_saved_package_without_creating_version_history(self):
        for build in ("build-2", "build-3", "build-1"):
            self.stage_build(build)
            self.assertTrue(self.run_recipe("apply"), self.last.stderr)
            self.assert_consistent(build)
            self.assertFalse((self.stage / "package.tar.gz").exists())
        self.assertFalse((self.backup / "updates").exists())
        self.assertFalse((self.backup / "installed-update").exists())

    @runs_on_tv
    def test_profile_and_vendor_drift_refuse_before_writing(self):
        self.stage_build("build-2")
        self.environment.update(QUIET_LAUNCHER_APP="other.host", QUIET_LAUNCHER_DLL="Host.dll")
        self.assertFalse(self.run_recipe("apply"))
        del self.environment["QUIET_LAUNCHER_APP"], self.environment["QUIET_LAUNCHER_DLL"]
        self.dll.write_text("vendor update")
        self.assertFalse(self.run_recipe("apply"))
        self.assertEqual(self.installed(), ("vendor update", "catalog-build-1"))
        self.assertFalse(self.pending.exists())

    @runs_on_tv
    def test_vendor_change_during_preparation_is_preserved(self):
        self.stage_build("build-2")
        script = self.interrupted_script('cp "$D/installed-home.sha256" "$T/old.sha256"',
                                         'printf "vendor update" > "$A/bin/$dll"')
        self.assertFalse(self.run_recipe("apply", script))
        self.assertEqual(self.installed(), ("vendor update", "catalog-build-1"))
        self.assertEqual(self.original.read_text(), "original Samsung binary")

    @runs_on_tv
    def test_resource_only_update(self):
        self.stage_build("new-catalog", dll="build-1")
        self.assertTrue(self.run_recipe("apply"), self.last.stderr)
        self.assert_consistent("new-catalog", dll="build-1")

    @runs_on_tv
    def test_recover_after_process_loss_at_each_replacement_step(self):
        steps = [
            'cp "$D/installed-home.sha256" "$T/old.sha256"',
            'touch "$T/ready"',
            'mv "$A/res/quiet-home" "$T/old-resources"',
            'mv "$T/package/quiet-home" "$A/res/quiet-home"',
            'mv "$T/package/$dll" "$A/bin/$dll"',
            'cp "$T/new.sha256" "$T/committed.sha256"',
            'touch "$T/commit"',
            'mv "$T/committed.sha256" "$D/installed-home.sha256"',
            'rm -f "$T/ready"',
        ]
        for same_dll in (False, True):
            dll = "build-1" if same_dll else "build-2"
            self.stage_build("build-2", dll=dll)
            for step in steps:
                with self.subTest(step=step, same_dll=same_dll):
                    self.assertFalse(self.run_recipe("apply", self.interrupted_script(step)))
                    self.assertTrue(self.pending.exists())
                    self.assertTrue(self.run_recipe("recover"), self.last.stderr)
                    committed = step in steps[-3:]
                    self.assert_consistent("build-2" if committed else "build-1", dll=dll if committed else "build-1")
                    if committed:
                        self.stage_build("build-1")
                        self.assertTrue(self.run_recipe("apply"), self.last.stderr)
                        self.stage_build("build-2", dll=dll)

    @runs_on_tv
    def test_regular_failure_restores_previous_files_automatically(self):
        self.stage_build("build-2")
        script = self.interrupted_script('mv "$T/package/$dll" "$A/bin/$dll"', 'exit 42')
        self.assertFalse(self.run_recipe("apply", script))
        self.assert_consistent("build-1")

    @runs_on_tv
    def test_apply_recovers_an_interrupted_update_before_retrying(self):
        self.stage_build("build-2")
        script = self.interrupted_script('mv "$T/package/$dll" "$A/bin/$dll"')
        self.assertFalse(self.run_recipe("apply", script))
        self.assertTrue(self.run_recipe("apply"), self.last.stderr)
        self.assert_consistent("build-2")

    @runs_on_tv
    def test_recovery_preserves_external_changes_and_its_own_copy(self):
        self.stage_build("build-2")
        script = self.interrupted_script('mv "$T/package/$dll" "$A/bin/$dll"')
        self.assertFalse(self.run_recipe("apply", script))
        catalog = self.app / "res/quiet-home/apps.json"
        catalog.write_text("external change")
        self.assertFalse(self.run_recipe("recover"))
        self.assertEqual(catalog.read_text(), "external change")
        self.assertEqual((self.pending / "old.dll").read_text(), "build-1")
        catalog.write_text("catalog-build-2")
        (self.pending / "old.dll").write_text("damaged copy")
        self.assertFalse(self.run_recipe("recover"))
        self.assertEqual(self.dll.read_text(), "build-2")

    @runs_on_tv
    def test_recovery_can_itself_be_interrupted_and_repeated(self):
        self.stage_build("build-2")
        self.assertFalse(self.run_recipe("apply", self.interrupted_script('mv "$T/package/$dll" "$A/bin/$dll"')))
        interrupted = self.interrupted_script('        mv "$T/old-resources" "$A/res/quiet-home" || return 1')
        self.assertFalse(self.run_recipe("recover", interrupted))
        self.assertTrue(self.run_recipe("recover"), self.last.stderr)
        self.assert_consistent("build-1")


class UpdateToolTests(unittest.TestCase):
    """The package and command orchestration on the computer, without a TV."""

    def test_package_is_owned_by_root_and_rooted_at_dot(self):
        with tempfile.TemporaryDirectory() as temp:
            source = Path(temp) / "package"
            (source / "quiet-home").mkdir(parents=True)
            (source / "Host.dll").write_text("build")
            (source / "quiet-home/apps.json").write_text("[]")
            archive = Path(temp) / "package.tar.gz"
            pack(source, archive)
            check_package(archive, "Host.dll")
            with tarfile.open(archive) as package:
                members = package.getmembers()
        self.assertEqual(sorted(m.name for m in members), [".", "./Host.dll", "./quiet-home", "./quiet-home/apps.json"])
        self.assertEqual({(m.uid, m.gid, m.uname, m.gname) for m in members}, {(0, 0, "", "")})

    def test_incomplete_or_unsafe_package_is_refused(self):
        with tempfile.TemporaryDirectory() as temp:
            archive = Path(temp) / "bad.tar.gz"
            for name, kind in [("../escape", tarfile.REGTYPE), ("/escape", tarfile.REGTYPE),
                               ("link", tarfile.SYMTYPE), ("link", tarfile.LNKTYPE), ("Host.dll", tarfile.DIRTYPE)]:
                with self.subTest(name=name, kind=kind):
                    with tarfile.open(archive, "w:gz") as package:
                        member = tarfile.TarInfo(name)
                        member.type = kind
                        package.addfile(member)
                    with self.assertRaises(ValueError):
                        check_package(archive, "Host.dll")

    def test_saved_package_skips_build_and_history_and_forwards_target(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            source = root / "package"
            (source / "quiet-home").mkdir(parents=True)
            (source / "Host.dll").write_text("build")
            (source / "quiet-home/apps.json").write_text("[]")
            archive = root / "working.tar.gz"
            pack(source, archive)
            profile = root / "profile.json"
            profile.write_text('{"launcher":{"dll":"Host.dll"}}')
            args = ["update.py", "--host", "192.0.2.50", "--profile", str(profile), "--snapshot", "home-01",
                    "--package", str(archive), "--sdb", "example-sdb", "--firmware", "observed-version"]
            with patch.object(sys, "argv", args), patch.object(update.subprocess, "run") as run, redirect_stdout(StringIO()):
                update.main()
            commands = [call.args[0] for call in run.call_args_list]
            self.assertEqual(len(commands), 2)
            self.assertIn(str(archive), commands[0])
            self.assertEqual(commands[1][-4:], ["--arg", "apply", "--arg", "home-01"])
            for command in commands:
                self.assertIn("192.0.2.50", command)
                self.assertIn("example-sdb", command)
                self.assertIn("observed-version", command)

    def test_build_install_and_optional_compare_use_one_output_directory(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            source = root / ".local/package"
            (source / "quiet-home").mkdir(parents=True)
            (source / "Host.dll").write_text("build")
            (source / "quiet-home/apps.json").write_text("[]")
            profile = root / "profile.json"
            profile.write_text('{"launcher":{"dll":"Host.dll"}}')
            archive = root / ".local/package.tar.gz"
            args = ["update.py", "--host", "192.0.2.50", "--profile", str(profile), "--snapshot", "home-01",
                    "--compare", str(root / "reference")]
            with patch.object(sys, "argv", args), patch.object(update, "ROOT", root), \
                    patch.object(update, "PACKAGE", source), patch.object(update, "ARCHIVE", archive), \
                    patch.object(update.subprocess, "run") as run, redirect_stdout(StringIO()):
                update.main()
            check_package(archive, "Host.dll")
            commands = [call.args[0] for call in run.call_args_list]
            self.assertEqual(len(commands), 4)
            self.assertIn("launcher", commands[0])
            self.assertIn("push", commands[1])
            self.assertEqual(commands[2][-4:], ["--arg", "apply", "--arg", "home-01"])
            self.assertIn(str(root / ".local/screens/update"), commands[3])
            self.assertFalse((root / ".local/baseline").exists())


if __name__ == "__main__":
    unittest.main()
