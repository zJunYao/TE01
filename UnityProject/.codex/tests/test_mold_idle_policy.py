"""霉菌素材导入、迁移与删除的权限契约。"""
from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
from workflow_lib.unity import MUTATIONS, READ_ONLY


class 霉菌权限测试(unittest.TestCase):
    def test_写入必须预览确认(self):
        for name in ("tengine_build_mold_idle", "tengine_fix_mold_idle_state", "tengine_build_mold_dissolve", "tengine_build_mold_burst", "tengine_extract_mold_effect", "tengine_replace_mold_smoke", "tengine_configure_mold_native_burst"):
            policy = MUTATIONS[name]
            self.assertTrue(policy["dry_run"])
            self.assertTrue(policy["confirm"])
            self.assertNotIn(name, READ_ONLY)

    def test_验证仅只读(self):
        self.assertIn("tengine_validate_mold_idle", READ_ONLY)
        self.assertNotIn("tengine_validate_mold_idle", MUTATIONS)
        self.assertIn("tengine_validate_mold_dissolve", READ_ONLY)
        self.assertNotIn("tengine_validate_mold_dissolve", MUTATIONS)
        self.assertIn("tengine_validate_mold_effect", READ_ONLY)
        self.assertNotIn("tengine_validate_mold_effect", MUTATIONS)
        self.assertIn("tengine_validate_mold_native_preview", READ_ONLY)
        self.assertNotIn("tengine_validate_mold_native_preview", MUTATIONS)
