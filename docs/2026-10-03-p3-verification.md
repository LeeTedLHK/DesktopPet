# DesktopPet P3 验收记录

日期：2026-10-03，Windows x64，.NET SDK 10.0.401。版本 0.3.0。

## 交付

- EXE：`artifacts/publish/p3-win-x64/DesktopPet.exe`
- ZIP：`artifacts/DesktopPet-P3-win-x64.zip`，109,192,719 字节，1155 条目。
- 包含官方 .NET Core / Windows Desktop 10.0.12 私有运行时，保留整个目录即可运行。
- P1/P2 发布目录和 ZIP 保留。源码变更前快照：`artifacts/verification/p3-source-before.zip`。
- 新版实际启动 PID 20136，Responding=true。加载的 hostfxr、hostpolicy、coreclr、wpfgfx_cor3 全来自包内 runtime；第二次启动正常退出 0。见 `artifacts/verification/p3-win-x64-startup.json`。

## 功能验收

| 用户要求 | 实现与证据 |
|---|---|
| 4 轻量养成 | 命名、独立 profile.json、亲密/饱腹/精力、互动奖励节流、睡眠恢复、可关闭、隐藏/离线不扣值；低精力影响休息倾向 |
| 5 提醒与番茄钟 | 三类提醒独立开关/间隔；专注和休息阶段、暂停/继续/停止、实时剩余时间、菜单入口；免打扰/忙碌延后、合并、10 分钟过期、2 分钟间隔 |
| 6 性格与活动区域 | 默认/活泼/安静/黏人；主屏全区/底部/右下角/百分比自定义；拖动、自动移动、恢复、缩放统一边界 |
| 原功能兼容 | 原互动/动画/快捷键/穿透/显隐/夜间作息测试继续通过；旧配置补充默认值，健康提醒默认关闭 |

## 实际运行的检查

1. `./scripts/test-offline.ps1 -WindowsIntegration`：**94 Core + 53 Windows 集成检查通过**，共 147。完整输出 `artifacts/verification/p3-final-checks.log`，退出 0。
2. `dotnet build src/DesktopPet/DesktopPet.csproj -c Release --no-restore`：0 警告、0 错误，`artifacts/verification/p3-build.log`。
3. `./scripts/publish.ps1`：新目录与 ZIP 成功，`artifacts/verification/p3-publish.log`。
4. `./scripts/verify-release.ps1 -StartCheck`：662 张图片源/发布 SHA256 一致，无旧图片残留；ZIP 内容、私有运行时和真实 EXE 启动通过，`artifacts/verification/p3-release-checks.log`。
5. 另比对发布 DesktopPet.dll 与最终源码构建 DLL 哈希，一致。所有 scripts/*.ps1 通过 PowerShell AST 解析检查。

Core 检查覆盖保存/重新加载、损坏数据备份、奖励防刷与时间校正、关闭养成、在线作息、提醒截止/合并/过期/关闭/排序、番茄阶段/暂停/时钟倒退/长时间挂起、性格采样和负坐标/缩放范围。

Windows 检查创建实际 WPF 窗口，验证原生窗口标志、快捷键注册/冲突及 WM_HOTKEY 消息、状态与按钮事件、养成接入、隐藏/全屏/互动/托盘菜单的提醒门控、阶段流转、关闭计时器。设置页及气泡由 WPF RenderTargetBitmap 渲染并人工查看：`artifacts/verification/p3-windows/companion-top.png`、`companion-bottom.png`、`settings-top.png`、`settings-bottom.png`。

## 审查与修复

独立只读审查发现一个 Important、两个 Minor，已全部修复并补回归检查：

- 系统时间纠正后的未来 LastBondUtc 不再长期挡住亲密度奖励；加载时清理，运行期间倒退时重置节流基准。
- 宠物“状态与专注”菜单通过独立回调打开正确选项卡。
- 系统托盘菜单的 Opened/Closed 状态接入提醒门控。

原有 1.9 秒动画测试出现一次时序不稳定；诊断确认六帧 1.5 秒动画正常完成。测试改为最多 30 次 100ms Dispatcher 轮询，验证有界完成（包含已支持的兜底）。一次新提醒检查使用了被关闭的健康类别，配置刷新正确清除它；将场景改为实际番茄通知后通过。两项均在执行记录中保留真实原因。

## 验证边界

- 原 xUnit 命令 `dotnet test DesktopPet.sln -c Release --no-restore` 仍失败：NU1301，api.nuget.org:443 套接字访问权限错误。`artifacts/verification/p3-xunit.log`。147 项数字仅统计实际通过的离线/Windows 检查，不包含 xUnit。
- 提醒的长间隔通过明确传入时间测试，没有声称逐个等待 15–120 分钟实测。
- 全屏通过边界算法和控制器门控测试；真实游戏/浏览器全屏、实际物理组合键未做逐项人工操作测试。
- 界面图为应用自身 WPF 渲染，不是 OS 屏幕录制。实际发布 EXE 的进程及私有运行时独立验证。
- 多屏、开机自启、皮肤管理、AI 聊天、安装器和代码签名仍不在本轮范围。番茄会话不跨重启恢复。
- P3 使用现有比熊动作，没有生成新图片。旧 P2 诊断临时目录保持保留，本轮未再次清理它们，正式包不包含它们。
