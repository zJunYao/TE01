# 新增怪物素材导入

## 目标与授权

- 用户要求导入本地 `素材/` 新增怪物；展示五张目标路径和导入设置后，用户明确回复“导入”。
- 不覆盖既有资源，不缩放或裁切，不创建预制体，不修改场景、项目设置或包依赖。
- 原图保留本地，沿用 `/素材/` Git 忽略规则。

## 完成范围

- [x] 三兄弟 HP1、HP2、HP3 导入至 `Assets/AssetRaw/Actor/Enemy/Brothers/Sprites/`。
- [x] 泡泡怪导入至 `Assets/AssetRaw/Actor/Enemy/Bubble/Sprites/Enemy_Bubble_Idle.png`。
- [x] 罐头怪导入至 `Assets/AssetRaw/Actor/Enemy/Can/Sprites/Enemy_Can_Idle.png`。
- [x] Unity 自动生成元数据，设置 Single Sprite、最大尺寸 256、关闭 Mipmap 和读写、Bilinear、Clamp。
- [x] 工作流补充两个已核实契约的写入命令，导入绑定源文件校验值，未知设置阻止生成计划，仅对实际支持的命令发送 confirm。

## 验证证据

- Unity 导入及设置回读报告：`.codex/runs/20261027T035358Z-df4e6fca/report.json`，12 步通过。
- 首批执行达到外部时限；确认进程退出、核实前两张已完成后，仅续跑剩余三张，没有重放或覆盖前两张。
- 五张源图与目标图 SHA-256 一致，`.meta` 均存在，报告中的五份导入设置全部通过断言。首次断言命令受 PowerShell 管道中文编码影响而失败，改为 Unicode 转义路径后通过。
- `python -m unittest discover -s .codex/tests`：22 项通过，包含新增的源文件绑定、未知字段拒绝、按命令决定 confirm 参数三项回归。
- `dotnet build UnityProject.sln --no-restore --verbosity quiet`：成功，0 错误、5 警告，涉及 UniTask 消息签名、未赋值字段及 nullable 上下文；本次未改 C#。
- 查询最近 10 条控制台日志未返回 Error，但不代表完整控制台无错。

## 验证边界

- 未进行场景视觉、状态切换、真机或真实构建验收；未实现血量、技能及运行时加载逻辑。
- 用户既有 Prefab、项目设置及其他未提交修改保持不动；未暂存、提交或推送。
