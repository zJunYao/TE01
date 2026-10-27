# Unity CLI 本地发现与命令兼容修复

- 状态：complete（CLI 入口修复完成，Editor 连接仍阻塞）

## 目标与验收

- 不再仅依赖缺失的 `Tools/unity.exe`；支持复用已安装 CLI。
- 保留旧命令协议，兼容本机 `0.1.0-beta.5` 的 `request` / `pipeline list`。
- 拒绝错误工程、多实例和不可达服务；不自动安装或启动服务。

## 范围与授权

- 用户确认：“按此方案修复”。允许修改项目工作流、测试及相关说明。
- 不下载、不安装、不升级、不复制二进制，不修改包依赖、Unity 资源和项目设置。
- 现有 BattleMainUI、ProjectSettings、文档和角色素材修改全部保留。

## 实施与证据

- 本机 CLI 版本：0.1.0-beta.5；帮助列出 request 和 pipeline，无旧版 command/status。
- 只读 pipeline list 检测到当前工程已打开，但 pipelineServer 为 null。
- 实现：`core.py` 支持显式路径、项目副本、PATH、本地安装目录；`unity.py` 按帮助适配两套协议，保留工程隔离和就绪检查；`doctor` 显示路径及协议。
- 回归：`python -m unittest discover -s .codex/tests -v`，19 项全部通过，包含新版命令发现、旧版协议、错误工程、多实例、服务断开和状态轮询。
- `doctor`：environment 通过，实际 Unity 2022.3.62f1 / Pipeline 0.3.1-exp.1 / CLI 0.1.0-beta.5 / .NET 9.0.101；editor-connection 阻塞。
- `unity list`：不再报告 CLI 缺失，明确报告当前工程 Pipeline 服务未连接。
- 报告：`.codex/runs/20261027T025811Z-ad8d7a49/report.md`、`.codex/runs/20261027T025811Z-d4266819/report.md`。
- `verify --profile docs`：工作流单测通过，整体失败于原有 Luban SHARDED_TABLES.md 断链与 README 缺少工作流入口；不扩大本次范围修复。报告 `.codex/runs/20261027T025913Z-2b834e9f/report.md`。
- `git diff --check` 无空白错误；未改 C#，未执行 .NET 构建或 Unity 资源写入。

## 剩余风险

- Pipeline 尚未连接，真实命令发现及 Editor 资源验证可能继续阻塞。
- 需要在当前工程编辑器检查编译错误，并从 Pipeline 菜单启动服务，再重跑 doctor 和 unity list；未操作其他已打开工程。
- 新版成功连接分支目前通过模拟响应验证，真实服务连接后仍需复验。

## 后续连接排查：2026-10-27

- 用户已启动服务；Unity 日志确认 `Pipeline Server started on port 7800`，监听进程为当前工程的 Unity 编辑器。
- 只检查了服务描述文件存在性、大小和修改时间，未读取或输出其内容。
- 检查本机 CLI 二进制中附带的 JavaScript：`Oc` 在工程根目录查找 `.unity-pipeline-port`，而当前 Pipeline 使用 `Library/Pipeline/.unity-pipeline-port`；CLI 读取 `authToken`，当前包发布 `evalToken`。此 CLI 与当前包协议不匹配。
- 显式地址失败也有源码依据：CLI 用 `URL.host`（包含端口）与纯主机白名单比较；无端口又得到 NaN。不能将其作为可用绕过方案。
- 已将工作流错误提示改为“CLI 未发现服务”，不再断言服务未启动。
- 不修改或复制鉴权描述、不关闭认证、不篡改 CLI 二进制；需要用户明确授权下载验证兼容 CLI，或者批准其他连接实现方案。不能宣称端到端修复完成。

## 官方 CLI 临时验证结果

- 用户明确允许下载官方 CLI 到临时目录进行兼容验证；未授权替换系统 CLI 或永久安装。
- 官方通道清单仍返回 0.1.0-beta.3，该版本无 Pipeline 命令，排除。
- 使用官方版本清单 `https://public-cdn.cloud.unity3d.com/hub/prod/cli/1.0.0-beta.3/latest.json`，下载 `unity-windows-x64.exe` 至批准的临时目录。
- SHA-256 与官方清单一致：`ff9ef81ade1063041d25e2c549cc7ed14e96d446f4204400bf101b389f7b8502`；运行输出版本为 1.0.0-beta.3。
- `status --project-path` 返回当前工程唯一实例，Unity 2022.3.62f1、状态 ready；无需修改包或认证。
- 仅在验证子进程内设置 `UNITY_CLI_PATH` 后，工作流 `doctor` 和 `unity list` 均通过。报告 `.codex/runs/20261027T031639Z-d15df71c/report.md`、`.codex/runs/20261027T031641Z-4b989d52/report.md`。
- 尚未永久切换：默认 PATH 仍为不兼容的 0.1.0-beta.5。需要确认项目专用副本落地或后续自行显式指定已验证 CLI。
- 本次只读连接与命令发现通过，不代表资源导入设置、测试或构建通过。

## 项目专用副本落地

- 用户授权：“将已验证的 CLI 复制到这个项目目录”。
- 已新增 `UnityProject/Tools/unity.exe`，未覆盖既有文件，SHA-256 与官方清单一致，文件大小 106366048 字节（超过 GitHub 普通 Git 文件 100 MiB 限制）。未执行 git add、提交、推送，也未擅自配置忽略或 LFS。
- 不设置临时 `UNITY_CLI_PATH`，直接运行工作流 `doctor`、`unity list` 均通过；报告 `.codex/runs/20261027T034511Z-e606e5ce/report.md`、`.codex/runs/20261027T034520Z-40981d1d/report.md`。
- 项目现在自动优先使用该副本；未修改全局 PATH 或用户级 CLI，未改 Unity 资源与设置。
