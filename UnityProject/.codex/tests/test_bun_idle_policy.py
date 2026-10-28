"""包子资源生成与修复操作的权限边界。"""
from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
from workflow_lib.unity import MUTATIONS, READ_ONLY


class 包子权限测试(unittest.TestCase):
    def test_写入必须预览确认(self):
        for name in ("tengine_build_bun_idle", "tengine_fix_bun_idle_duration", "tengine_build_bun_motion", "tengine_fix_bun_motion_states", "tengine_build_bun_death", "tengine_fix_bun_death_state"):
            self.assertTrue(MUTATIONS[name]["dry_run"])
            self.assertTrue(MUTATIONS[name]["confirm"])
            self.assertNotIn(name, READ_ONLY)

    def test_验证只有只读权限(self):
        self.assertIn("tengine_validate_bun_idle", READ_ONLY)
        self.assertNotIn("tengine_validate_bun_idle", MUTATIONS)
        self.assertIn("tengine_validate_bun_motion", READ_ONLY)
        self.assertNotIn("tengine_validate_bun_motion", MUTATIONS)
        self.assertIn("tengine_validate_bun_death", READ_ONLY)
        self.assertNotIn("tengine_validate_bun_death", MUTATIONS)
