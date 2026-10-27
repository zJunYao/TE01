# 包子待机角色导入

## 授权与范围

- 用户要求参考 04B 中的小包子生成角色图，并确认通过项目 CLI 新增 Sprite；明确纠正不能使用 1024 角色规格。
- 按现有怪物导入记录使用 256×256，保留高清原稿于仓库外部素材目录约定位置；新增到已约定的 `Assets/AssetRaw/Actor/Food/Bun/BunIdle.png`。
- 不替换角色，不创建 Prefab，不修改场景、项目设置和既有用户改动。

## 完成与验证

- [x] 当前工程 Editor 与 Pipeline 连接检查通过：`.codex/runs/20261027T111929Z-1e82136c/report.md`。
- [x] 新增导入通过预览、授权、执行：`.codex/runs/20261027T112139Z-24021ac5/report.md`。
- [x] 单张 Sprite、最大尺寸 256、PPU 100、透明度、Bilinear、Clamp、禁用 Mipmap 和读写；设置回读：`.codex/runs/20261027T112313Z-cbc907d2/report.md`。
- [x] 图片实际为 256×256 RGBA，包含透明像素和不透明像素，源文件与导入文件 SHA-256 一致，Unity 已生成 `.meta`。
- [x] `dotnet build UnityProject.sln --no-restore --verbosity quiet`：0 警告、0 错误。
- [x] 控制台查询返回 2 条记录，无 Error，有一条非自动模式弹窗风险 Warning：`.codex/runs/20261027T112343Z-6f11a87f/report.md`。

## 验证边界

## 路径修正

- 用户确认遵循现有食物目录命名，已通过 Unity 资源数据库移动至 `Assets/AssetRaw/Actor/Food/Bun/Sprites/Food_Bun_Idle.png`，原导入路径为历史记录。
- GUID 保持 `4a9f63495d96c1c4e9116db32a251c05`；移动报告：`.codex/runs/20261027T113035Z-24e21b29/report.md`。
- 工作流补充已核实契约的目录创建、资源移动操作；目录创建不支持 dry_run，因此使用静态预览，实际调用不发送 dry_run 或 confirm。

## 验证边界（延续）

- 未进行场景或真机显示验收，不保证世界空间大小；实际显示大小还取决于 PPU、Transform 和摄像机。
- 未执行玩法测试、完整 Unity 验证集或真实构建；本次仅新增静态图片资源，无 C# 修改。
