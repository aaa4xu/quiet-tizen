"""Exercise SQLite lifetimes on the host."""
import sqlite3
import sys
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))
import plan_cleanup


class WindowsPortabilityTests(unittest.TestCase):
    def test_snapshot_connection_closed_on_success_and_error(self):
        with tempfile.TemporaryDirectory() as directory:
            database = Path(directory) / "snapshot.db"
            with sqlite3.connect(database) as writer:
                writer.execute("CREATE TABLE package_app_info (app_id TEXT, "
                               "app_disable TEXT, app_onboot TEXT, app_autorestart TEXT)")
                writer.execute("INSERT INTO package_app_info VALUES ('example.app', 'false', 'true', 'false')")
            writer.close()
            connect = sqlite3.connect
            for app_id in ("example.app", "missing.app"):
                connections = []
                def tracked_connect(*args, **kwargs):
                    connection = connect(*args, **kwargs)
                    connections.append(connection)
                    return connection
                with patch.object(plan_cleanup.sqlite3, "connect", side_effect=tracked_connect):
                    if app_id == "missing.app":
                        with self.assertRaises(ValueError):
                            plan_cleanup.read_rows(database, [app_id])
                    else:
                        self.assertEqual(len(plan_cleanup.read_rows(database, [app_id])), 1)
                self.assertEqual(len(connections), 1)
                with self.assertRaises(sqlite3.ProgrammingError):
                    connections[0].execute("SELECT 1")


if __name__ == "__main__":
    unittest.main()
