from datetime import datetime, timezone
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
from contextlib import closing
from tv_side import runs_on_tv

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))
from update import log_line, pack, update_name

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
    """update-launcher.sh on a synthetic container, with Samsung's tools stubbed out."""

    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        root = Path(self.temp.name)
        self.backup = root / "backups/home-01"
        self.app = root / f"apps/{CONTAINER}"
        self.stage = root / "stage"
        stubs = root / "stubs"
        for directory in (self.backup, self.app / "bin", self.app / "res/quiet-home", self.stage, stubs):
            directory.mkdir(parents=True)
        for tool in ("app_launcher", "chown", "chsmack"):
            (stubs / tool).write_text("#!/bin/sh\nexit 0\n")
            (stubs / tool).chmod(0o755)
        (self.backup / "launcher-container.txt").write_text(f"{CONTAINER} Host.dll\n")
        (self.backup / "original-metadata.txt").write_text("644 201:0 10\n")
        self.dll = self.app / "bin/Host.dll"
        self.dll.write_text("build-1")
        (self.app / "res/quiet-home/apps.json").write_text("catalog-1")
        script = (ROOT / "recipes/update-launcher.sh").read_text(encoding="utf-8")
        script = script.replace('test "$(id -u)" = 0\n', "")
        for path, local in (("/opt/usr/share/quiet-tizen-backups", root / "backups"),
                            ("/opt/usr/apps", root / "apps"), ("/home/owner/share/tmp/sdk_tools/quiet-tizen", self.stage)):
            script = script.replace(path, str(local))
        self.script = script
        self.environment = {"PATH": f"{stubs}:{os.environ['PATH']}:/sbin"}
        digest = hashlib.sha256(b"build-1").hexdigest()  # what install-launcher.sh recorded
        (self.backup / "installed-home.sha256").write_text(f"{digest}  {self.dll}\n")

    def tearDown(self):
        self.temp.cleanup()

    def run_recipe(self, *args):
        self.last = subprocess.run(["sh", "-c", self.script, "sh", *args], capture_output=True, text=True,
                                   env=self.environment)
        return self.last.returncode == 0

    def stage_build(self, build, dll=None):
        source = Path(self.temp.name) / build
        (source / "quiet-home").mkdir(parents=True)
        (source / "Host.dll").write_text(dll or build)
        (source / "quiet-home/apps.json").write_text(f"catalog-{build}")
        pack(source, self.stage / "package.tar.gz")

    def installed(self):
        return self.dll.read_text(), (self.app / "res/quiet-home/apps.json").read_text()

    def apply(self, *builds):
        for build in builds:
            self.stage_build(build)
            self.assertTrue(self.run_recipe("apply", "home-01", build), self.last.stderr)

    def snapshot_files(self):
        # Everything restore-launcher.sh relies on lies outside updates/.
        return {path.relative_to(self.backup): path.read_bytes() for path in self.backup.rglob("*")
                if path.is_file() and "updates" not in path.relative_to(self.backup).parts}

    @runs_on_tv
    def test_updates_roll_back_newest_first_to_the_installed_build(self):
        self.stage_build("build-2")
        self.assertTrue(self.run_recipe("apply", "home-01", "u2"), self.last.stderr)
        self.assertEqual(self.installed(), ("build-2", "catalog-build-2"))
        self.assertFalse(self.run_recipe("apply", "home-01", "u2"))  # each update name is used once
        self.stage_build("build-3")
        self.assertTrue(self.run_recipe("apply", "home-01", "u3"), self.last.stderr)
        self.assertFalse(self.run_recipe("rollback", "home-01", "u2"))  # u3 sits on top of it
        self.assertEqual(self.installed(), ("build-3", "catalog-build-3"))
        self.assertTrue(self.run_recipe("rollback", "home-01", "u3"), self.last.stderr)
        self.assertTrue(self.run_recipe("rollback", "home-01", "u2"), self.last.stderr)
        self.assertEqual(self.installed(), ("build-1", "catalog-1"))
        self.assertFalse(self.run_recipe("rollback", "home-01", "u2"))
        self.assertIn(hashlib.sha256(b"build-1").hexdigest(), (self.backup / "installed-home.sha256").read_text())

    @runs_on_tv
    def test_a_replaced_launcher_or_another_profile_refuses_apply(self):
        self.stage_build("build-2")
        self.environment["QUIET_LAUNCHER_APP"], self.environment["QUIET_LAUNCHER_DLL"] = "other.host", "Host.dll"
        self.assertFalse(self.run_recipe("apply", "home-01", "u2"))
        del self.environment["QUIET_LAUNCHER_APP"], self.environment["QUIET_LAUNCHER_DLL"]
        self.dll.write_text("vendor update")
        self.assertFalse(self.run_recipe("apply", "home-01", "u2"))
        self.assertIn("not the Quiet Home build", self.last.stderr)
        self.assertFalse((self.backup / "updates").exists())
        self.assertEqual((self.app / "res/quiet-home/apps.json").read_text(), "catalog-1")

    @runs_on_tv
    def test_an_update_with_the_same_dll_is_rolled_back_only_from_the_top(self):
        self.apply("build-2")
        self.stage_build("build-2b", dll="build-2")  # only the catalog changed
        self.assertTrue(self.run_recipe("apply", "home-01", "build-2b"), self.last.stderr)
        self.assertFalse(self.run_recipe("rollback", "home-01", "build-2"))
        self.assertTrue(self.run_recipe("rollback", "home-01", "build-2b"), self.last.stderr)
        self.assertEqual(self.installed(), ("build-2", "catalog-build-2"))
        self.assertTrue(self.run_recipe("rollback", "home-01", "build-2"), self.last.stderr)
        self.assertEqual(self.installed(), ("build-1", "catalog-1"))
        self.assertEqual((self.backup / "installed-update").exists(), False)

    @runs_on_tv
    def test_prune_keeps_the_newest_updates_on_the_chain(self):
        self.assertFalse(self.run_recipe("prune", "home-01", "2"))  # only right after an apply
        self.apply("u2", "u3")
        self.assertTrue(self.run_recipe("rollback", "home-01", "u3"), self.last.stderr)
        self.apply("u4", "u5")
        before = self.snapshot_files()
        self.assertFalse(self.run_recipe("prune", "home-01", "0"))
        self.assertTrue(self.run_recipe("prune", "home-01", "2"), self.last.stderr)
        self.assertEqual(sorted(path.name for path in (self.backup / "updates").iterdir()), ["u4", "u5"])
        self.assertEqual(self.snapshot_files(), before)
        self.assertEqual(self.installed(), ("u5", "catalog-u5"))
        self.assertTrue(self.run_recipe("rollback", "home-01", "u5"), self.last.stderr)
        self.assertTrue(self.run_recipe("rollback", "home-01", "u4"), self.last.stderr)
        self.assertEqual(self.installed(), ("u2", "catalog-u2"))  # the oldest kept update's own backup
        self.assertFalse(self.run_recipe("rollback", "home-01", "u2"))
        self.assertIn("No update u2", self.last.stderr)

    @runs_on_tv
    def test_prune_follows_older_updates_by_digest(self):
        self.apply("u2", "u3")
        for path in [self.backup / "installed-update", *(self.backup / "updates").glob("*/previous-update")]:
            path.unlink()  # as left by update-launcher.sh before these records
        self.apply("u4")
        self.assertTrue(self.run_recipe("prune", "home-01", "2"), self.last.stderr)
        self.assertEqual(sorted(path.name for path in (self.backup / "updates").iterdir()), ["u3", "u4"])
        self.assertTrue(self.run_recipe("rollback", "home-01", "u4"), self.last.stderr)
        self.assertTrue(self.run_recipe("rollback", "home-01", "u3"), self.last.stderr)
        self.assertEqual(self.installed(), ("u2", "catalog-u2"))


class UpdateToolTests(unittest.TestCase):
    """The parts of tools/update.py that run on the computer."""

    def test_names_suit_update_launcher_and_sort_by_time(self):
        early, late = (update_name(datetime(2026, 10, 28, hour, 5, 9)) for hour in (9, 17))
        self.assertEqual(early, "dev-20261028-090509")
        self.assertRegex(late, r"^[A-Za-z0-9_-]+$")
        self.assertLess(early, late)

    def test_package_is_owned_by_root_and_rooted_at_dot(self):
        with tempfile.TemporaryDirectory() as temp:
            source = Path(temp) / "package"
            (source / "quiet-home").mkdir(parents=True)
            (source / "Host.dll").write_text("build")
            (source / "quiet-home/apps.json").write_text("[]")
            pack(source, Path(temp) / "package.tar.gz")
            with tarfile.open(Path(temp) / "package.tar.gz") as package:
                members = package.getmembers()
        self.assertEqual(sorted(m.name for m in members), [".", "./Host.dll", "./quiet-home", "./quiet-home/apps.json"])
        self.assertEqual({(m.uid, m.gid, m.uname, m.gname) for m in members}, {(0, 0, "", "")})

    def test_log_line(self):
        line = log_line(datetime(2026, 10, 28, 9, 5, 9, tzinfo=timezone.utc), "home-01", "dev-1", "abc", "applied")
        self.assertEqual(line, "2026-10-28T09:05:09+00:00\thome-01\tdev-1\tabc\tapplied\n")


if __name__ == "__main__":
    unittest.main()
