# 关卡载入预制体

## 目标与授权

- 用户确认创建 `Assets/AssetRaw/UI/LevelLoadingUI.prefab`：烘焙料理台背景、橙色进度条、默认零进度、无字体、无加载业务代码。
- 新增两张基础图片至 `Assets/AssetRaw/UIRaw/Loading/`，拒绝覆盖；背景使用已导入资源。
- 不修改场景、已有界面、默认字体、项目设置或资源收集配置。

## 决策

- 通用 HTML 烘焙器不支持所需的 EnvelopeParent 背景、不可交互 Slider 和填充内高光组合；使用固定目标的 Editor 命令在 PreviewScene 创建，通过 CLI 预览、批准、执行。
- 图片由 AssetDatabase 导入，九宫格通过 TextureImporter 设置；不手写序列化资产。
- 1080×1920 设计，进度条位于距底约 10.5% 的位置，宽度 70%。

## 验证

- Unity 已生成预制体，GUID 为 `0986ebe35342a9f44ab50e293f77d65a`。生成报告：`.codex/runs/20261027T100526Z-7a007a43/report.json`。
- 修正本次生成的高光：使用比例锚点，避免低进度时固定边距造成负宽度。修正已保存，随后只读回读验证通过；没有重放写入。初次修正后的验证误将 UGUI 包内 Slider 脚本判断成自定义脚本，修复验证器后只读复验。
- 最终验证报告：`.codex/runs/20261027T101125Z-195ef98b/report.json`，无缺失引用、自定义运行脚本或字体依赖；0%、1%、50%、100% 填充宽度依次 0、7.44、372、744；默认保存值仍为 0。
- 1080×1920、1080×2400、1536×2048 背景等比铺满几何检查通过。此为布局检查，不是设备截图或真实框架视觉验收。
- 最终 Unity 编译通过，控制台查询返回零条日志：`.codex/runs/20261027T101101Z-0d09c30d/report.json`。此前首次编译后的控制台回读遇到域重载连接中断，未当作通过；后续重连及编译通过。
- `dotnet build UnityProject.sln --no-restore --verbosity quiet`：成功，0 警告、0 错误；生成的 Editor 工程已包含新工具源码。
- 现有 Python 回归 22 项通过；未新增或运行专门的 Unity Test Runner 测试集，资源验证由项目 CLI 的限定命令实际执行。
- CLI 报告保存期间两次遭遇 Windows 文件替换拒绝访问；只读操作重新执行后取得成功报告，未修改公共日志实现。
- 真机、安全区及实际框架 Canvas 效果须另行验收；本次不接入关卡流程。

## 临时工具清理

- 用户明确授权“可以清理这部分临时工具”。
- 通过 Unity AssetDatabase 删除 `Assets/Editor/LevelLoadingPrefabBuilder.cs` 及其元数据，移除 Python 工作流中生成、收尾、校验三个专用命令入口。
- 删除报告：`.codex/runs/20261027T105210Z-04508201/report.json`。首次请求在预览阶段因参数名不匹配被拒绝，没有发生写入；核实目标仍在后改正参数重新预览。
- 清理前后校验预制体及其 meta、两张进度条图片及 meta、Loading 背景及 meta 的内容哈希，全部保持一致。
- 保留此任务记录和历史验证报告，成品不依赖临时 Editor 工具运行。
