# Windows EXE 交付与验收记录

日期：2026-10-02。环境：Windows 10 x64，.NET SDK 10.0.401。

## 项目扫描

- 检查了 30 个应用与核心源文件、11 个测试项目文件、4 个 PowerShell 脚本，以及解决方案、项目配置和现有文档结构。
- 启动入口为 `src/DesktopPet/App.xaml.cs`；应用为 WPF Windows GUI，使用 WinForms 系统托盘。
- 核心逻辑包含状态机、随机行为、动画资源加载、配置保存、日志和屏幕边界计算。
- 资源包含 Idle 96、Walk 96、WalkLeft 96、WalkRight 96、Happy 120、Sleep 120 张 PNG，以及 `pet.jpg`，共 625 个文件。
- 动画路径基于 `AppContext.BaseDirectory`，无需从 EXE 所在目录启动。

## 构建问题及处理

- Windows PowerShell 5.1 按系统编码读取无 BOM 的 UTF-8 脚本，导致中文字符串被误读并出现语法错误。
- 为 `scripts/use-dotnet.ps1`、`scripts/publish.ps1`、`scripts/bootstrap-dotnet.ps1` 添加 UTF-8 BOM，保留原脚本内容。修正后均通过 PowerShell 语法解析检查。
- `scripts/build.ps1` 无法完成测试项目还原，报 `NU1301`：当前受限环境无法连接 `api.nuget.org:443`，本地也没有 xUnit 和 Microsoft.NET.Test.Sdk 包。**本次没有运行自动测试，不沿用旧验收记录中的测试通过数。**
- 标准自包含发布同样因运行时 NuGet 包无法下载而失败。

## 实际发布方式

Release 发布成功，退出码 0。程序使用旁置私有运行时，未裁剪、未打包成单文件。发布命令为：

```powershell
New-Item -ItemType Directory -Path .tools/offline-feed -Force
. ./scripts/use-dotnet.ps1
dotnet publish src/DesktopPet/DesktopPet.csproj -c Release -r win-x64 --self-contained false -p:AppHostDotNetSearch=AppRelative -p:AppHostRelativeDotNet=runtime --source .tools/offline-feed -o artifacts/publish/win-x64
```

随后从本机 `C:/Program Files/dotnet/` 复制以下官方运行时文件到发布目录的 `runtime/`，保留原相对目录结构：

- `host/fxr/10.0.12/`
- `shared/Microsoft.NETCore.App/10.0.12/`
- `shared/Microsoft.WindowsDesktop.App/10.0.12/`
- `dotnet.exe`、`LICENSE.txt`、`ThirdPartyNotices.txt`

EXE 配置为只搜索 `runtime/`，不依赖系统运行时或环境变量。[微软发布说明](https://learn.microsoft.com/en-us/dotnet/core/deploying/)说明了 `AppHostDotNetSearch` 和 `AppHostRelativeDotNet` 的用途。

## 本次验证

- 所有 625 个发布素材与源码素材逐个比较 SHA256，全部一致。
- 私有 `runtime/dotnet.exe --list-runtimes` 正确列出 .NET Core 和 Windows Desktop 10.0.12。
- 实际启动发布 EXE，观察到进程运行 227 秒，`Responding=True`。
- 检查已加载模块，`hostfxr.dll`、`hostpolicy.dll`、`coreclr.dll` 和 `wpfgfx_cor3.dll` 均来自发布目录中的 `runtime/`。
- 确认首实例持有单实例 Mutex；不启用详细宿主日志时，第二次启动正常退出，退出码 0。
- 首轮重复启动在开启详细宿主日志时超过 5 秒检查期限；关闭该日志后复测通过。宿主日志仅用于诊断，没有随程序启用。
- 本次未进行点击、拖动、菜单及动画视觉效果的完整 GUI 验收；进程存活检查不代表完整交互验收。

原始证据位于 `artifacts/verification/`：`build-test.log`、`publish.log`、`publish-local-runtime.log`、`host-trace.log`、`startup.json`。

## 启动与分发

双击 `artifacts/publish/win-x64/DesktopPet.exe`。

分发 `artifacts/DesktopPet-win-x64.zip`，解压后保留整个 `win-x64/` 文件夹。不能单独复制 EXE；`Assets/`、`runtime/`、应用 DLL 和配置描述文件均需保留。退出使用宠物右键菜单或系统托盘的“退出”。
