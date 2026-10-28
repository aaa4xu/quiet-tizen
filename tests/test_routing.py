import re
import sqlite3
import tempfile
from pathlib import Path
import unittest
from contextlib import closing

ROOT = Path(__file__).resolve().parents[1]


class RoutingTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.directory = Path(self.temp.name)
        self.conn = sqlite3.connect(self.directory / "live.db")
        self.conn.executescript("""
          CREATE TABLE key_filter(appid TEXT, key TEXT, on_selector TEXT, target_selector TEXT, args TEXT, other TEXT);
          INSERT INTO key_filter VALUES
            ('com.samsung.tv.quicklaunch','XF86More','','package:com.samsung.tv.quicklaunch','original-more','keep'),
            ('com.samsung.tv.quicklaunch','XF86More','~alive-package:org.tizen.function-osd','package:org.tizen.function-osd','prewarm','keep'),
            ('org.tizen.menu','XF86SysMenu',NULL,'package:org.tizen.menu','original-menu','keep'),
            ('unrelated.app','Other','','package:unrelated.app','unrelated','keep');
        """)
        with closing(sqlite3.connect(self.directory / ".app-context.db")) as snapshot:
            self.conn.backup(snapshot)

    def tearDown(self):
        self.conn.close()
        self.temp.cleanup()

    def sql(self, recipe):
        script = (ROOT / "recipes" / recipe).read_text(encoding="utf-8")
        sql = re.search(r"<<SQL\n(.*?)\nSQL", script, re.S).group(1)
        container = "com.samsung.tv.adplayer"
        values = {"$D": str(self.directory), "$container": container, "$target": "package:" + container}
        return re.sub(r"\$(D|container|target)\b", lambda m: values[m.group(0)], sql)

    def test_round_trip_preserves_new_metadata(self):
        self.conn.executescript(self.sql("install-settings-bridge.sh"))
        self.conn.execute("DETACH before")
        self.conn.execute("UPDATE key_filter SET other='updated'")
        self.conn.commit()
        self.conn.executescript(self.sql("restore-settings-bridge.sh"))
        self.assertEqual(self.conn.execute("SELECT target_selector,args,other FROM key_filter "
                         "WHERE appid='org.tizen.menu'").fetchone(),
                         ("package:org.tizen.menu", "original-menu", "updated"))
        self.assertNotIn("$", self.sql("install-settings-bridge.sh") + self.sql("restore-settings-bridge.sh"))
        self.assertEqual(self.conn.execute("SELECT args FROM key_filter "
                         "WHERE appid='unrelated.app'").fetchone(), ("unrelated",))

    def test_conditional_rows_are_never_redirected(self):
        conditional = "SELECT target_selector,args FROM key_filter WHERE on_selector<>''"
        before = self.conn.execute(conditional).fetchall()
        self.conn.executescript(self.sql("install-settings-bridge.sh"))
        self.conn.execute("DETACH before")
        self.assertEqual(self.conn.execute(conditional).fetchall(), before)
        self.assertEqual(self.conn.execute("SELECT count(*) FROM key_filter WHERE target_selector="
                         "'package:com.samsung.tv.adplayer'").fetchone(), (2,))
        self.conn.executescript(self.sql("restore-settings-bridge.sh"))
        self.assertEqual(self.conn.execute(conditional).fetchall(), before)

    def test_changed_rule_refuses_apply(self):
        self.conn.execute("UPDATE key_filter SET args='vendor-update' WHERE appid='org.tizen.menu'")
        self.conn.commit()
        with self.assertRaises(sqlite3.IntegrityError):
            self.conn.executescript(self.sql("install-settings-bridge.sh"))
        self.conn.rollback()
        self.assertEqual(self.conn.execute("SELECT args FROM key_filter "
                         "WHERE appid='com.samsung.tv.quicklaunch' AND on_selector=''").fetchone(), ("original-more",))

    def test_changed_rule_refuses_restore(self):
        self.conn.executescript(self.sql("install-settings-bridge.sh"))
        self.conn.execute("DETACH before")
        self.conn.execute("UPDATE key_filter SET args='new vendor route' WHERE appid='org.tizen.menu'")
        self.conn.commit()
        with self.assertRaises(sqlite3.IntegrityError):
            self.conn.executescript(self.sql("restore-settings-bridge.sh"))
        self.conn.rollback()


if __name__ == "__main__":
    unittest.main()
