"""Skip markers for tests that run TV-side shell code on the host."""
import os
import unittest

# The TV runs these commands and recipes with its own POSIX sh and tools. A POSIX
# host reproduces that closely; Windows only emulates it, so the POSIX job covers them.
runs_on_tv = unittest.skipIf(os.name == "nt", "runs TV-side shell code; covered by the POSIX job")
