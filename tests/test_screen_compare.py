from contextlib import redirect_stdout
import io
from pathlib import Path
import re
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))
from screen_compare import FRAMES, SHOTS, compare, main


def write_run(directory, png=b"frame", build_ms="40", selected="0"):
    for frame in FRAMES:
        (directory / f"{frame}.png").write_bytes(png + frame.encode())
        (directory / f"{frame}.state").write_text(
            f"2026-01-01T00:00:00.0000000Z\npage=0\nselected={selected}\nbuild_ms={build_ms}\nprocess_id=7\n",
            encoding="utf-8")


class ScreenCompareTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.reference, self.current = Path(temporary.name, "reference"), Path(temporary.name, "current")
        self.reference.mkdir()
        self.current.mkdir()
        write_run(self.reference)

    def test_identical_frames_match_whatever_the_timing(self):
        write_run(self.current, build_ms="55")
        lines, matched = compare(self.reference, self.current, pixels=None)
        self.assertTrue(matched)
        self.assertEqual(len(lines), len(FRAMES))
        self.assertIn("build_ms 40 -> 55", lines[0])

    def test_one_changed_frame_fails_and_names_the_frame(self):
        write_run(self.current)
        (self.current / "apps-light.png").write_bytes(b"other")
        lines, matched = compare(self.reference, self.current, pixels=lambda a, b: "3 px")
        self.assertFalse(matched)
        self.assertEqual([line.split()[0] for line in lines if "DIFFERENT: 3 px" in line], ["apps-light"])

    def test_missing_frames_and_state_count_as_differences(self):
        write_run(self.current)
        (self.current / "home-dark.png").unlink()
        (self.current / "home-dark.state").unlink()
        lines, matched = compare(self.reference, self.current, pixels=None)
        self.assertFalse(matched)
        self.assertIn("build_ms 40 -> -", lines[0])
        self.assertIn("missing in current", lines[0])

    def test_state_changes_are_listed_without_volatile_keys(self):
        write_run(self.current, build_ms="90", selected="2")
        lines, matched = compare(self.reference, self.current, pixels=None)
        self.assertTrue(matched)
        changes = [line.strip() for line in lines if "state " in line]
        self.assertEqual(changes, ["state selected: 0 -> 2"] * len(FRAMES))

    def test_exit_code(self):
        write_run(self.current)
        with redirect_stdout(io.StringIO()):
            self.assertEqual(main([str(self.reference), str(self.current)]), 0)
            # Not a PNG: the report says so instead of stopping.
            (self.current / "settings-dark.png").write_bytes(b"other")
            self.assertEqual(main([str(self.reference), str(self.current)]), 1)

    def test_recipe_takes_every_compared_shot(self):
        recipe = (ROOT / "recipes/readme-screens.sh").read_text(encoding="utf-8")
        self.assertEqual(tuple(re.search(r"^for shot in (.+); do$", recipe, re.M).group(1).split()), SHOTS)
        self.assertIn("quiet_page home quiet_focus toolbar", recipe)


if __name__ == "__main__":
    unittest.main()
