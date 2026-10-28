# Game 弹球消灭测试

## 运行

1. 打开 `Assets/AssetRaw/Scenes/Game.unity`。
2. 点击 Unity 自带播放按钮，直接运行当前场景。不要点击项目工具栏的 `Launcher`，该按钮会启动 `main`。
3. 按住包子向发射方向的反方向拖动，出现瞄准线后松开；支持鼠标或单指触摸。
4. 包子在木盘内反弹，命中紫色怪物即消灭并停止。未命中则在 8 秒后回到起点，可以再次发射。
5. 点击下方“重置 / R”或按键盘 `R` 恢复整局。

## 代码边界

- `GameTestSimulation.cs`：连续圆形碰撞、墙面反射、消灭、超时和重置模型，不修改全局物理参数。
- `GameTestSceneController.cs`：当前场景的测试输入、表现和即时调试面板，不接入正式 `GameApp`。
- 当前目录继承上层 `GameLogic.asmdef`，业务代码仍位于热更边界内。
- `Tests/Editor` 与 `Tests/PlayMode` 使用独立测试程序集，不属于正式业务启动入口。
- 编辑器搭建工具位于 `Assets/Editor/Test/GameTestSceneCommands.cs`，只通过 Unity API 新建场景和序列化引用，不手写场景 YAML、GUID 或 `.meta`。
- 三张 Sprite 为静态场景引用，未创建异步资源句柄，不需要额外调用资源卸载。
- 即时调试面板仅用于本次原型，不是正式 UI；后续接入正式玩法时使用项目 `UIWindow`、`AddUIEvent` 和 `GameModule`，不要将测试面板扩展成另一套 UI 框架。

## 验证

从仓库根执行：

```powershell
python UnityProject/.codex/scripts/workflow.py verify --profile code
python UnityProject/.codex/scripts/workflow.py verify --profile unity --test-filter Game.Test --timeout 180
python UnityProject/.codex/scripts/workflow.py unity query tengine_validate_game_test_scene
```

第二条命令分别发现并运行当前测试程序集的编辑模式与运行模式用例，不运行全部第三方测试。第三条要求 Game 场景已经打开，只读检查场景引用、渲染配置和丢失脚本。

测试覆盖直接命中、反弹命中、高速防穿透、角点双轴反射、不同时间步长一致性、超时归位、重置及实际逐帧角色表现。鼠标手感、触屏和真实设备显示仍需人工检查；相机 RenderTexture 预览不等于真实 GameView 或设备截图。
