import re
import sqlite3
import subprocess
import tempfile
from pathlib import Path
import unittest
from contextlib import closing
from tv_side import runs_on_tv

ROOT = Path(__file__).resolve().parents[1]
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


if __name__ == "__main__":
    unittest.main()
