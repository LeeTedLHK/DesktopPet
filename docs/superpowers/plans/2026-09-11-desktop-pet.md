# DesktopPet P0 Implementation Plan

> **For agentic workers:** Use superpowers:subagent-driven-development for the isolated core task and a final independent review. The user requests direct execution and no approval interruptions.

**Goal:** 交付可以运行的 DesktopPet 0.1.0 Windows 桌宠及自包含发布。

**Architecture:** WPF View + Controller；独立 Core 保存纯逻辑及持久化；UI 服务负责工作区、托盘、动画和退出。生产代码不依赖第三方包。

**Tech Stack:** C#、.NET 10、WPF、WinForms NotifyIcon、xUnit。

**Spec:** ../specs/2026-09-11-desktop-pet-design.md

## Global Constraints

- .NET 10 / Windows 10、11 / x64 / 0.1.0；原始照片不改动。
- 无联网功能、无第三方 UI 框架、无数据库、无游戏养成扩展。
- 基准宽度 160 DIP；缩放 0.8–2；随机等待正常 5–15 秒；步距 50–200 DIP。
- 设置和日志写 LocalAppData/DesktopPet；隐藏不退出，暂停仍可拖动点击。
- PublishTrimmed=false，自包含 win-x64 发布到 artifacts/publish/win-x64。

## Tasks

- [x] Core：模型、状态机、几何限制、行为范围、配置与日志；先写核心测试，再实现和验证。52 项通过；见 core-report.md。
- [x] WPF：PetWindow、SettingsWindow、PetController、BehaviorManager、ScreenBoundsService、TrayIconService、App 生命周期；导入原图为资源。
- [x] 工程：Solution、可复用本地 SDK 引导、build/publish 脚本、中文 README。
- [x] 验证：restore/build/test、独立代码审查、Release publish、资源哈希、真实进程启动和单实例；GUI 工具不能枚举此窗口，交互部分明确保留人工验收，见 ../../verification.md。

## Verification commands

```powershell
. ./scripts/use-dotnet.ps1
dotnet restore DesktopPet.sln
dotnet build DesktopPet.sln -c Release --no-restore
dotnet test DesktopPet.sln -c Release --no-build
./scripts/publish.ps1
```
