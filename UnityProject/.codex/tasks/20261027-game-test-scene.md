# Game 弹球消灭测试场景

- ID: 20261027-game-test-scene
- 状态: 已完成实现与相关自动验证，保留人工设备验收及既有文档检查问题
- 负责人: 当前会话

## 目标与验收

- [x] 新建 `Assets/AssetRaw/Scenes/Game.unity`，作为独立测试场景。
- [x] 使用现有木盘底图、包子主角和紫色霉菌怪物，场景中直接可见。
- [x] 鼠标或触摸拖动瞄准，松开发射；包子限制在木盘内部并反弹。
- [x] 命中一个怪物后将其消灭，显示结果并可重置重玩。
- [x] 玩法源码放在 `Assets/GameScripts/HotFix/GameLogic/Test/`，归属现有 GameLogic 热更程序集。
- [x] 取得完整解决方案编译、Unity 编译和相关行为验证证据；无法执行的检查明确记录阻塞。

## 范围与决策

- 场景：`Assets/AssetRaw/Scenes/Game.unity`，遵循现有可加载场景资源目录。
- 测试逻辑：`Assets/GameScripts/HotFix/GameLogic/Test/`。
- 必要的编辑器搭建、校验和自动化测试工具分别隔离到编辑器和测试程序集，不进入正式玩法入口。
- 允许为本次窄范围场景搭建命令扩充项目 CLI 审核目录和对应回归测试，不使用任意代码执行命令。
- 底图：`Assets/AssetRaw/UIRaw/Raw/Battle/BG_Battle_KitchenWarmWood.png`。
- 主角：`Assets/AssetRaw/Actor/Food/Bun/Sprites/Food_Bun_Idle.png`。
- 怪物：`Assets/AssetRaw/Actor/Enemy/Mold/Sprites/Enemy_Mold_Idle.png`。
- 三张图均已读取，与用户给出的三张参考图一致；复用现有 Sprite，不生成新图片、不改导入参数。
- 场景静态引用图片，由 Unity 管理引用生命周期；不额外创建资源模块，不在独立测试场景中绕过 GameModule 访问框架。
- 不修改 `main`、`GameApp`、`BattleMainUI`、YooAsset 收集规则、包依赖、项目设置或构建场景列表。
- 不执行真实构建、提交、推送、发布，不覆盖现有场景。

## 授权

- 原始请求：新建 Game 场景，搭建底图、包子和一个怪物，实现最简单弹球消灭，测试源码放 Test，遵循框架规范。
- 补充输入：用户发出的木盘、包子和紫色怪物图片。
- 用户在本会话选择“确认，按此搭建（推荐）”，批准新增 Game 场景、对应对象、测试逻辑及独立运行验证，不保存其他脏场景。
- 已按确认范围执行 preview / approve / apply，未覆盖现有场景；预览图仅新增到 `Temp/GameTestScene-portrait.png`，不保存场景或正式资源。
- 高风险边界：若需要覆盖、删除、改变设置或包依赖，另行确认。

## 实施与证据

- [x] 初始 `git status --short` 为空，无已有用户修改。
- [x] 已读取仓库和 Unity 约束、框架与 Unity CLI 技能。
- [x] doctor 通过：`.codex/runs/20261027T113852Z-5b2b672f/report.md`。
- [x] Unity 命令发现通过：`.codex/runs/20261027T113902Z-910b9067/report.md`。
- [x] 当前仅打开 `Assets/Scenes/main.unity`，无未保存修改：`.codex/runs/20261027T113911Z-63b5c14b/report.md`。
- [x] 场景写入预览、用户确认、审批与执行。
- [x] 玩法实现和相关回归测试。
- [x] .NET / Unity 编译及场景行为验证。
- [x] 相机预览视觉检查与运行说明；真实 GameView、触屏手感不据此声明通过。
- 新建场景预览：`.codex/runs/20261027T115031Z-1d4a1f77/preview.json`。
- 审批：`.codex/runs/20261027T115111Z-1980f9f4/approval.json`。
- 创建与初次引用校验：`.codex/runs/20261027T115120Z-37050f95/report.md`，场景保存成功，丢失脚本数为 0。
- 最终 Unity 验证：`.codex/runs/20261027T115928Z-8406bf97/report.md`，编译错误数为 0，EditMode 10/10、PlayMode 2/2，无跳过。通用 asset-validation 阶段未运行，以独立的场景专用校验结果为证据。
- 完整解决方案编译：`.codex/runs/20261027T115848Z-99d7c6fa/report.md`，solution-build 通过；同次工作流单元测试 28/28 通过。
- 最终独立场景引用校验：`.codex/runs/20261027T115917Z-47247d17/report.md`，丢失脚本数 0，三张 Sprite 路径正确，底图尺寸 9.41 × 16.72，场景无未保存修改。
- 相机预览：`.codex/runs/20261027T115545Z-f04fc744/report.md`，540 × 960，图片位于 `Temp/GameTestScene-portrait.png`；已读取并检查木盘、包子和怪物的位置与渲染。
- 实施中修正测试程序集重复引用问题；最初编译超时和域重载暂时断连未记为通过，后续终态和重新验证均已取证。
- 修正校验返回值中直接序列化 Vector3 引发的自引用错误，改为宽高数值 DTO；修正后的场景专用校验已通过。
- `git diff --check` 通过，无空白错误；工具提示已有文本将按 Git 配置转换行尾，未额外改动行尾设置。

## 剩余风险

- 独立测试场景不接入正式启动流程；正式入口集成不在本次范围。
- CLI 只增加固定场景搭建、固定 Temp 截图写入和只读校验命令，并增加权限回归；未放开 eval、menu、任意场景覆盖或 SaveAll。
- `verify --profile code` 整体未通过：既有 Luban 文档链接指向不存在的 `Configs/GameConfig/CustomTemplate/SHARDED_TABLES.md`，且 README 工作流入口检查失败。本次未改这两处；不能将整体 code profile 宣称为通过。
- Unity 打开场景后自动修改了 `UserSettings/EditorUserSettings.asset` 的最近场景记录及版本控制本地扫描字段。未手工编辑或恢复该序列化文件，交付时说明；未修改 ProjectSettings、包依赖或构建列表。
- 自动行为测试从测试入口发射，未模拟真实鼠标拖拽或触摸；需人工检查鼠标手感和实际设备触屏，测试字体使用系统字体，非 Windows 平台中文字形也需检查。
- 即时调试面板是本次独立原型的临时显示方式，不属于正式 UIWindow；后续正式接入应替换为框架 UI，不扩大本次范围。
