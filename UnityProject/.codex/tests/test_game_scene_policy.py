"""本次固定场景搭建命令的权限契约回归。"""

from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
from workflow_lib.unity import MUTATIONS, READ_ONLY, Unity
from workflow_lib.core import WorkflowError


class 场景命令审核测试(unittest.TestCase):
    def test_写入命令必须预览并确认(self):
        policy = MUTATIONS["tengine_create_game_test_scene"]
        self.assertTrue(policy["dry_run"])
        self.assertTrue(policy["confirm"])
        self.assertNotIn("tengine_create_game_test_scene", READ_ONLY)

    def test_校验命令只有只读权限(self):
        self.assertIn("tengine_validate_game_test_scene", READ_ONLY)
        self.assertNotIn("tengine_validate_game_test_scene", MUTATIONS)

    def test_截图写入也必须预览并确认(self):
        policy = MUTATIONS["tengine_capture_game_test_scene"]
        self.assertTrue(policy["dry_run"])
        self.assertTrue(policy["confirm"])
        self.assertNotIn("tengine_capture_game_test_scene", READ_ONLY)
        self.assertNotIn("capture_game_view", READ_ONLY)

    def test_不能通过只读入口执行搭建(self):
        unity = Unity(type("运行", (), {"context": None})())
        with self.assertRaises(WorkflowError):
            unity.query("tengine_create_game_test_scene", {})

    def test_没有放开任意执行或场景覆盖命令(self):
        for command in ("eval", "eval_file", "menu", "create_scene", "save_all"):
            self.assertNotIn(command, MUTATIONS)


if __name__ == "__main__":
    unittest.main()
