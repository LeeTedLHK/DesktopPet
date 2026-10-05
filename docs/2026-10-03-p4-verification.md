# DesktopPet 0.4.0 验收记录

日期：2026-10-03，Windows x64，.NET SDK 10.0.401。本轮完成用户选择的 1、2、3、4，再完成 6、7、9。没有 Git 仓库，修改前源码保存在 `artifacts/verification/p4-source-before.zip`；旧 P3 发布包保留。

## 完成功能

| 编号 | 功能 | 实际入口与行为 |
|---|---|---|
| 1 | 多屏 | 设置选择显示器、允许/限制跨屏拖动，记忆物理坐标；明确选定的屏幕断开后回主屏。活动范围、气泡、小窝和全屏检测跟随所选屏幕。 |
| 2 | 皮肤管理 | 默认比熊、文件夹/ZIP 导入、预览及缺失动作提示；独立副本保存到 LocalAppData。导入防路径越界/链接/资源超限，切换前完整解码；坏预览/坏皮肤保留可用状态。 |
| 3 | 启动与找回 | 开机自启默认关闭，当前用户 Run 项写入/移除；重复启动向旧实例发找回信号，初始化前接收信号并保留待显示请求。右键/托盘/设置可重置位置和恢复鼠标互动。 |
| 4 | 精准点击 | 缓存动画帧 alpha，按 WPF 实际显示及变换映射；透明点返回 HTTRANSPARENT，同时原生窗口穿透随鼠标位置更新。支持捕获拖动和整个窗口手动穿透。 |
| 6 | 小球/飞盘 | WPF 绘制玩具，弹跳/旋转，宠物追逐并开心回应；自动行为暂停时仍可玩。互动、拖动、设置/显示变化、菜单、隐藏/全屏可取消。 |
| 7 | 小窝 | 三种主题、软垫与食盆、相对位置。回窝行走并睡眠；夜间作息开启时使用小窝，摸头叫醒。道具适配范围/屏幕/大小/置顶及显隐。 |
| 9 | 日记与成就 | 本地日期陪伴记录，摸头/喂食/玩耍/回窝/专注计数，最近 200 条日记；六个成就、进度和解锁台词。开关独立于养成；旧数值保留。 |

P4 复用现有比熊动画，新增道具用 WPF 原生图形绘制，没有重复生成同一姿势的图片。

## 检查结果

全部 **313 项通过**：

| 检查 | 最终数量 | 证据 |
|---|---:|---|
| Core 离线行为 | 113/113 | `artifacts/verification/p4-final-core.log` |
| 皮肤独立检查 | 28/28 | `artifacts/verification/p4-all-checks.log` 的 Skin 段及 `p4-skins-green.log` |
| 日记独立检查 | 83/83 | `artifacts/verification/p4-all-checks.log` 的 Journal 段及 `p4-journal-green.log` |
| 实际 WPF 集成 | 89/89 | `artifacts/verification/p4-final-windows.log` |

`p4-all-checks.log` 中较早的 Core 112 / WPF 81 已被最后两份日志替代，不能把该旧日志单独称作 313 项。

WPF 检查使用独立临时 settings/profile 和自有窗口，覆盖原有养成/提醒/专注/作息、实际显示器枚举、原生物理坐标、透明点命中、HKCU 测试项、命名事件、隐藏与全屏、完整解码回滚、玩具真实计时追到开心、回窝睡眠、日记从文件重读及计时器清理。截图位于 `artifacts/verification/p4-windows/`，包括新设置页、日记底部、小球与小窝。

执行：`./scripts/test-offline.ps1 -WindowsIntegration`；最后 Core/WPF 改动后分别重跑 `dotnet run --project tests/DesktopPet.OfflineChecks -c Release --no-restore` 与 `dotnet run --project tests/DesktopPet.WindowsChecks -c Release --no-restore -- artifacts/verification/p4-windows`。两项独立模块在后续修复中未改动。

原 xUnit NuGet 还原仍受当前环境 `api.nuget.org:443` 连接限制，本轮使用无外部 NuGet 包的检查项目；不声称 xUnit 全套已运行。

## 审查与回归

独立只读审查见 `artifacts/verification/p4-review.md`，无未解决的 Critical/Important。修复并验证：追逐被设置/显示变化取消后恢复自动行为；跨屏时同步移动小窝且保留捕获；PNG 横纵 DPI 不同的命中映射；明确屏幕断开回主屏；坏预览不退出；初始化前注册找回事件。

另修复右键菜单开合早于下次玩具 tick 时不恢复自动行为的竞态。检查把下一次 world tick 延后，让真实 Popup 事件先完成调度，确认取消与恢复不依赖该 tick。

异轴 DPI 回归使用 WPF 编码器现场生成的合法全不透明 PNG，旧算法失败证据为 `p4-anisotropic-valid-red.log`；之前文本样本/诊断尝试不作为有效 red 证明。

真实迁移检查还发现旧代码把保存操作放在可空事件调用的参数里：没有保存通知监听器时，参数不会求值，文件也不会写入。设置和养成保存现已先执行，再通知监听器。两项检查在无监听器、尚未 Dispose 时直接读回文件，旧代码失败证据为 `p4-save-listener-red.log`、`p4-profile-listener-red.log`；最终 WPF 89/89 通过。独立审查确认此修复。

## 发布与迁移

最终入口：`artifacts/publish/p4-win-x64/DesktopPet.exe`。分发包：`artifacts/DesktopPet-P4-win-x64.zip`，保留整目录运行。旧 P3 和修改前快照仍在；重打包时也保留上一候选目录。

最终发布核验：目录重建后再次运行 `./scripts/verify-release.ps1 -StartCheck`，结果与精确 ZIP 元数据见 `artifacts/verification/p4-release-check.log`、`p4-win-x64-startup.json` 和 `p4-release-metadata.json`。

真实用户 settings/profile 的发布前副本为 `p4-user-before-settings.json`、`p4-user-before-profile.json`。集成检查未使用这份真实数据；最终启动沿用并迁移旧配置/养成数据。`p4-user-migration.json` 确认名字、亲密度及上次亲密互动时间保留，饱腹/精力变化在实际运行时间允许范围内；日记、新的显示器与物理坐标字段已写入，开机自启仍为关闭。最终 P4 实例保持运行。

## 验证边界

- 当前只有一块主屏，150% 缩放。负坐标、不同 DPI、断屏选择在纯模型中检查；捕获期间的小窝原生移动使用合成 monitor fixture。尚未实际双屏拖动、混合缩放跨屏或物理拔插。
- 已验证原生命中结果和窗口样式，未实际向其他应用点击穿透或测试快速移动后立即点击的 20ms 更新间隔。
- 已验证开机启动注册表写入/移除，未实际重启或重新登录 Windows。游戏/视频全屏使用显隐门控检查，没有启动实际游戏。
- 发布包未签名；没有加入安装器或 AI 聊天。
- 两份旧中断皮肤测试目录因自动审批 `blocked by policy` 保留；最后一次检查正常清理自己的数据。详见 `p4-skins-report.md`。

## 额度与重置卡

通过本机官方 Codex app-server 的 `account/rateLimits/read` 查询，协议参考 [官方说明](https://learn.chatgpt.com/docs/app-server)。只读取额度/重置卡元数据，不读取认证文件；辅助脚本只会在“已耗尽”及精确到期卡仍可用时请求兑换。

开始时周额度已用 99%，尚未耗尽，4 张可用卡中查到 1 张符合 10 月 4 日到期。随后一个模块收到使用上限错误；再次查询时主窗口变为 1%、周窗口 0%，卡剩 3 张，10 月 4 日到期卡已从可用列表移除，因此继续工作。该次辅助查询没有发送兑换请求，未获得谁兑换的记录，不把它记作工具已兑换。

最终查询 `p4-quota-final.json`：主窗口已用 47%、周窗口 7%，未耗尽，剩 3 张可用重置卡，无符合 10 月 4 日到期的可用卡；无需再兑换其他卡。
