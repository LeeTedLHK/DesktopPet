# DesktopPet P2 验收记录

工作开始于 2026-10-02，完成于 2026-10-03。Windows x64 / .NET SDK 10.0.401，版本 0.2.0。

## 实现范围

1. 不打扰：前台全屏检测、自动隐藏/恢复、手动隐藏优先、Ctrl+Alt+P 显隐、Ctrl+Alt+M 穿透、快捷键开关及冲突提示、托盘控制入口。
2. 互动：头部摸头、身体开心、右键/托盘喂食、8 秒冷却、拖动悬空动作与放下回应、3 秒气泡。暂停和关闭动画仍可互动。
3. 自然行为：伸懒腰、哈欠、鼠标靠近时左右观察、12 秒观察间隔、默认 22:00–07:00 夜间睡眠、手动唤醒及作息设置。

## 验证结果

- Release WPF 构建成功，0 warning、0 error。证据：`artifacts/verification/p2-build.log`。
- `scripts/test-offline.ps1 -WindowsIntegration` 成功，**55 项 Core 检查和 35 项 Windows 集成检查通过**。证据：`artifacts/verification/p2-final-checks.log`。
- 原 xUnit 基线仍因缺少 NuGet 包和 `NU1301` 无法执行。未把旧 P1 的通过数量算入本次，也未将无依赖检查称为 xUnit 通过。证据：`p2-baseline.log`。
- 图像生成使用内置 ImageGen，参考 `pet.jpg` 与 `比熊犬三视图参考图.png`；生成六张精灵图，拆成七组动作、**36 张 256×256 RGBA PNG**。逐张检查透明边缘、可见主体与尺寸；检查深色背景预览，无背景矩形或主体越格。证据：`artifacts/imagegen/p2/validation.json`、`contact-sheet.jpg`；[完整提示词](assets/2026-10-02-p2-imagegen-prompts.json)。
- 实际 WPF 设置页顶部/底部、宠物与气泡渲染已查看，内容未被遮挡且可滚动。证据：`artifacts/verification/p2-windows/`。
- 分阶段发布成功；人为加入的旧帧 `stale_check_999.png` 在新发布中被移除。发布目录和源码 **662 个图片文件逐个 SHA256 一致**，没有多余图片。
- ZIP 结构验证成功，共 **1155 个条目**，包括 EXE、私有运行时及新增动作，不含旧帧或构建暂存目录。
- 最终 `artifacts/publish/p2-win-x64/DesktopPet.exe` 实际启动，PID 16448，`Responding=True`。hostfxr、hostpolicy、coreclr、wpfgfx 均来自自身 `runtime/`；第二次启动退出码 0。证据：`artifacts/verification/p2-release-checks.log`、`p2-startup.json`。

## 独立审查与回归

采用独立只读审查，发现的行为问题均有失败重现与修复后通过记录：

- 气泡 Owner 不能在宠物首次显示前设置：改为首次气泡显示时设置。
- 静态互动被按下操作打断后丢失鼠标捕获，状态可能不结束：清理互动前先完成原状态。
- 慢速单次动作被固定计时提前结束：按实际帧数、FPS 与倍率计算保护时长；播放中改变速度也重新计算。
- 拖动期间隐藏宠物会重新打开落地气泡：结束拖动后再隐藏气泡。
- 鼠标观察错误依赖伸懒腰/哈欠开关：分别控制。
- 冷却期间未采样指针，导致指针停下后延迟观察：持续更新指针样本，再判断动作资格。
- 发布覆盖目录会保留已删除素材：使用全新暂存目录、同级改名替换、失败恢复及最多约 6 秒的有限重试。

回归证据：`p2-windows-red.log`、`p2-slow-animation-red.log`、`p2-review-red.log`、`p2-cursor-sampling-red.log`、`p2-publish-stale-red.log` 与最终通过日志。

## 验证边界

Windows 检查使用实际 WPF 窗口、原生穿透标志、快捷键注册/冲突及 WM_HOTKEY 消息；全屏恢复测试通过控制器触发并覆盖矩形策略。没有逐一操作实际游戏或浏览器全屏，也没有通过真实键盘逐键注入验证快捷键。当前会话缺少 Computer Use 所需的 node_repl 执行入口，因此没有宣称完成全面人工式 GUI 验收。

应用活动范围仍为主屏。代码签名、安装器、多屏、开机自启、皮肤管理和 AI 聊天属于后续功能。

## 交付

- EXE：`artifacts/publish/p2-win-x64/DesktopPet.exe`。
- 分发包：`artifacts/DesktopPet-P2-win-x64.zip`，解压后保留整个目录。
- 原始生成图：`artifacts/imagegen/p2/`。
- 运行素材：`src/DesktopPet/Assets/Pet/{Petting,Feeding,Stretching,Yawning,Dragging,LookLeft,LookRight}/`。
- 源码修改前备份：`artifacts/verification/p2-source-before.zip`。
- 失败发布的暂存目录保留作诊断；它们不在正式 ZIP 中。
