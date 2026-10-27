"""CLI 发现、命令兼容及工程隔离回归；不连接真实编辑器。"""

import json
import os
from pathlib import Path
import sys
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
from workflow_lib.core import WorkflowError, resolve_unity_cli
from workflow_lib.unity import Unity


class 假运行:
    def __init__(self, project, responses):
        self.context = SimpleNamespace(project=project, cli=Path(__file__))
        self.responses = iter(responses)
        self.calls = []

    def execute(self, args, **kwargs):
        self.calls.append(args)
        value = next(self.responses)
        return SimpleNamespace(returncode=0, stdout=value if isinstance(value, str) else json.dumps(value), stderr="")


新版帮助 = "Commands:\n  request|req [options]\n  pipeline|pipe\n"
旧版帮助 = "Commands:\n  command [options]\n  status [options]\n"


class 路径发现测试(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.env = patch.dict(os.environ, {}, clear=True)
        self.env.start()
        self.addCleanup(self.env.stop)

    def test_显式配置优先且错误时不回退(self):
        target = self.root / '不存在.exe'
        with patch.dict(os.environ, {"UNITY_CLI_PATH": str(target)}), patch('shutil.which') as which:
            self.assertEqual(resolve_unity_cli(self.root), target)
            which.assert_not_called()

    def test_项目工具优先(self):
        target = self.root / 'Tools/unity.exe'
        target.parent.mkdir()
        target.touch()
        with patch('shutil.which') as which:
            self.assertEqual(resolve_unity_cli(self.root), target)
            which.assert_not_called()

    def test_路径环境已安装工具(self):
        target = self.root / 'unity.exe'
        with patch('shutil.which', return_value=str(target)):
            self.assertEqual(resolve_unity_cli(self.root), target)

    def test_本地安装目录回退(self):
        target = self.root / 'Unity/bin/unity.exe'
        target.parent.mkdir(parents=True)
        target.touch()
        with patch.dict(os.environ, {"LOCALAPPDATA": str(self.root)}), patch('shutil.which', return_value=None):
            self.assertEqual(resolve_unity_cli(self.root), target)

    def test_全部缺失返回项目路径以便诊断(self):
        with patch('shutil.which', return_value=None):
            self.assertEqual(resolve_unity_cli(self.root), self.root / 'Tools/unity.exe')


class 协议兼容测试(unittest.TestCase):
    project = Path(__file__).resolve().parents[2]

    def 运行(self, *responses):
        run = 假运行(self.project, responses)
        return run, Unity(run)

    def 实例(self, **overrides):
        return dict(projectPath=str(self.project), isRunning=True, pipelineServer={"port": 7800}, **overrides)

    def test_新版命令转换与帮助缓存(self):
        run, unity = self.运行(新版帮助, {}, {})
        unity.raw(['command', '--project-path', str(self.project), 'find_assets'])
        unity.raw(['command', '--project-path', str(self.project)])
        self.assertEqual(len(run.calls), 3)
        self.assertEqual(run.calls[1][1], 'request')
        self.assertNotIn('--no-log-proxy', run.calls[1])
        self.assertIn('--quiet', run.calls[1])

    def test_旧版命令保持(self):
        run, unity = self.运行(旧版帮助, {})
        unity.raw(['command', 'editor_status'])
        self.assertEqual(run.calls[1][1], 'command')
        self.assertIn('--no-log-proxy', run.calls[1])

    def test_拒绝未知协议(self):
        _, unity = self.运行('Commands:\n  editor\n  build\n')
        with self.assertRaises(WorkflowError):
            unity.detect_cli()

    def test_同工程就绪且忽略其他工程(self):
        other = {"projectPath": str(self.project.parent / '其他工程'), "isRunning": True}
        run, unity = self.运行(新版帮助, {"success": True, "data": {"instances": [other, self.实例()]}},
                              {"success": True, "data": {"projectPath": str(self.project), "status": "ready"}})
        self.assertEqual(unity.ready()['status'], 'ready')
        self.assertIn(str(self.project), run.calls[-1])

    def test_当前工程重复实例拒绝(self):
        run, unity = self.运行(新版帮助, {"instances": [self.实例(), self.实例()]})
        with self.assertRaises(WorkflowError):
            unity.ready()
        self.assertEqual(len(run.calls), 2)

    def test_服务未启动阻塞不发送命令(self):
        instance = self.实例()
        instance['pipelineServer'] = None
        run, unity = self.运行(新版帮助, {"instances": [instance]})
        with self.assertRaises(WorkflowError) as result:
            unity.ready()
        self.assertEqual(result.exception.status, 'blocked')
        self.assertEqual(len(run.calls), 2)

    def test_状态返回错误工程拒绝(self):
        _, unity = self.运行(新版帮助, {"instances": [self.实例()]},
                            {"projectPath": str(self.project.parent), "status": "ready"})
        with self.assertRaises(WorkflowError):
            unity.ready()

    def test_编译中阻塞但允许状态轮询(self):
        responses = (新版帮助, {"instances": [self.实例()]},
                     {"projectPath": str(self.project), "status": "compiling"})
        _, unity = self.运行(*responses)
        with self.assertRaises(WorkflowError):
            unity.ready()
        _, unity = self.运行(*responses)
        self.assertEqual(unity.ready(require_ready=False)['status'], 'compiling')

    def test_旧版实例验证不变(self):
        _, unity = self.运行(旧版帮助, {"instances": [{"projectPath": str(self.project), "state": "ready"}]})
        self.assertEqual(unity.ready()['state'], 'ready')

    def test_业务失败不会当成成功(self):
        _, unity = self.运行(新版帮助, {"success": False, "errors": [{"message": "连接失败"}]})
        with self.assertRaises(WorkflowError):
            unity.ready()

    def test_没有当前工程不转用其他工程(self):
        other = self.实例()
        other['projectPath'] = str(self.project.parent)
        run, unity = self.运行(新版帮助, {"instances": [other]})
        with self.assertRaises(WorkflowError):
            unity.ready()
        self.assertEqual(len(run.calls), 2)

    def test_未知列表结构拒绝(self):
        _, unity = self.运行(新版帮助, {"unexpected": []})
        with self.assertRaises(WorkflowError):
            unity.ready()

    def test_新版命令发现与查询(self):
        run, unity = self.运行(新版帮助, {"instances": [self.实例()]},
                              {"projectPath": str(self.project), "status": "ready"},
                              {"commands": [{"name": "editor_status"}]},
                              {"projectPath": str(self.project), "status": "ready"})
        self.assertEqual(unity.discover(), ['editor_status'])
        self.assertEqual(unity.call('editor_status')['status'], 'ready')
        self.assertTrue(all(call[1] == 'request' for call in run.calls[2:]))

    def test_连接错误标记为阻塞(self):
        run, unity = self.运行(新版帮助)
        unity.detect_cli()
        with patch.object(run, 'execute', return_value=SimpleNamespace(
                returncode=1, stdout='', stderr='No Pipeline instance found for project')):
            with self.assertRaises(WorkflowError) as result:
                unity.raw(['command', '--project-path', str(self.project)])
        self.assertEqual(result.exception.status, 'blocked')


class 资源导入策略测试(unittest.TestCase):
    def setUp(self):
        self.unity = Unity(SimpleNamespace(context=SimpleNamespace()))

    def test_导入预览绑定源文件(self):
        参数 = {'source': '本地素材.png', 'path': 'Assets/怪物.png'}
        with patch.object(self.unity, 'ready'), patch.object(self.unity, 'call', return_value={}), \
                patch('workflow_lib.unity.make_plan') as 生成计划:
            self.unity.preview('import_asset', 参数, inputs=('附加输入',))
        self.assertEqual(生成计划.call_args.kwargs['inputs'], ('附加输入', '本地素材.png'))

    def test_未知导入设置阻止生成计划(self):
        with patch.object(self.unity, 'ready'), \
                patch.object(self.unity, 'call', return_value={'unknown': ['不存在的属性']}), \
                patch('workflow_lib.unity.make_plan') as 生成计划:
            with self.assertRaises(WorkflowError):
                self.unity.preview('set_import_settings', {'asset': 'Assets/怪物.png', 'settings': {}})
        生成计划.assert_not_called()

    def test_实际写入仅向支持命令发送确认参数(self):
        for 命令, 参数, 需要确认 in (
                ('import_asset', {'source': '源.png', 'path': 'Assets/怪物.png'}, True),
                ('set_import_settings', {'asset': 'Assets/怪物.png', 'settings': {}}, False)):
            with self.subTest(命令=命令):
                self.unity.capabilities = {命令}
                计划 = {'kind': 'unity', 'command': 命令, 'parameters': 参数, 'preview': {}}
                with patch.object(self.unity, 'ready'), patch.object(self.unity, 'discover'), \
                        patch.object(self.unity, 'call', return_value={}) as 调用, \
                        patch('workflow_lib.unity.consume_approval') as 消费授权:
                    self.unity.apply(计划, '批准.json')
                消费授权.assert_called_once()
                self.assertEqual(调用.call_args.kwargs, {'mutating': True})
                实际参数 = 调用.call_args.args[1]
                self.assertIs(实际参数['dry_run'], False)
                self.assertEqual('confirm' in 实际参数, 需要确认)
                if 需要确认:
                    self.assertIs(实际参数['confirm'], True)


if __name__ == '__main__':
    unittest.main()
