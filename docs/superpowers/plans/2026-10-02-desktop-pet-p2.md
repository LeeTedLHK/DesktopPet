# DesktopPet P2 Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans to implement the authorized work inline; one fresh reviewer after implementation.

**Goal:** 实现不打扰模式、丰富互动与自然行为，并交付匹配参考比熊的素材和可运行 EXE。
**Architecture:** Core 保存纯状态与策略；WPF 服务封装系统交互，Controller 协调可见性与手动操作，BehaviorManager 管理自然动作计时。保留离线运行与旧素材回退。
**Tech Stack:** C# / .NET 10 / WPF / Win32；内置 ImageGen；PowerShell。
**Spec:** docs/superpowers/specs/2026-10-02-desktop-pet-p2-design.md

## Global Constraints

- 现有目录无 Git，保留 artifacts/backups 中源码备份，使用文件验收记录。
- 应用不联网；生成图片保留两张原图及现有动作。
- Ctrl+Alt+P 显隐，Ctrl+Alt+M 穿透；默认全屏隐藏、新自然行为和夜间睡眠开启，穿透关闭。

## Review Focus

- 全屏开始/结束期间用户手动隐藏，不能误恢复。
- 禁用动画、缺少动作、重复互动、暂停、拖动不能卡状态。
- 夜间跨午夜与修改睡眠配置必须可唤醒。
- 快捷键冲突、隐藏启动、穿透时仍可从托盘恢复控制。
- 退出时所有窗口、原生钩子、快捷键与计时器释放。

### Task 1: Core 与离线功能检查

Files: Models/AppSettings.cs, Models/PetState.cs, Controllers/PetStateMachine.cs, Animations/AnimationDefinition.cs, Animations/AnimationKind.cs, Services/PetVisibility.cs, Behaviors/CompanionPolicy.cs, tests/DesktopPet.OfflineChecks/Program.cs。
Interfaces: PetVisibility.ShouldShow/RequestedVisible/FullscreenSuppressed；CompanionPolicy.IsSleepTime(int,int,int)、IsFullscreen(WorkArea,WorkArea)、LookDirection(PetPoint,PetPoint)；TryBeginInteraction、TryBeginNaturalAction、CompleteTemporaryAction、CancelNaturalAction。

- [x] 添加包含上述失败模式的无依赖检查；运行并确认新接口缺失。
- [x] 实现策略、新字段、状态/动作映射；运行检查确认全部通过。
- [x] 更新现有 xUnit 期望与新增动作测试。

### Task 2: WPF 功能

Files: Services/DesktopEnvironmentService.cs, GlobalHotkeyService.cs, NativeWindowService.cs, SpeechBubbleService.cs, Controllers/PetController.cs, Behaviors/BehaviorManager.cs, Views/SettingsWindow.xaml(.cs), Views/PetWindow.xaml(.cs), ApplicationService.cs, TrayIconService.cs。
Interfaces: Core Task 1；PetController.SetFullscreenSuppressed、ToggleVisibility、ToggleClickThrough、PetHead、Feed；服务 Dispose；SettingsWindow.SetRuntimeStatus。

- [x] 接入全屏检测与快捷键、穿透；保持用户显示意图。
- [x] 接入摸头/喂食/拖动、气泡、临时动作完成和自然计时。
- [x] 更新设置和托盘入口；编译 Release，进行 Windows 功能检查。

### Task 3: 素材、发布与验收

Files: Assets/Pet 新动作目录、artifacts/imagegen、scripts/test-offline.ps1、scripts/publish.ps1、README.md、docs/2026-10-02-p2-verification.md。
Interfaces: 动作目录名与 Task 1 的 AnimationKind 一致，PNG 帧自然排序。

- [x] 用内置 ImageGen 生成六组精灵图，切分成七组动作；检查透明通道、尺寸、边界并看图。
- [x] 更新发布脚本支持旁置官方本地运行时，并生成 ZIP。
- [x] 完整运行 Core 检查、编译、素材校验和 GUI 操作检查；新上下文审查并修复重要问题；记录证据。
