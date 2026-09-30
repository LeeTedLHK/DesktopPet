# DesktopPet 0.1.0 · P1 动画版

一个原生 Windows WPF 桌面宠物，在 P0 的稳定显示、拖动、自动行为、托盘与设置基础上，增加 Idle / Walk / Happy / Sleep PNG 序列帧动画。应用运行时完全离线，没有 AI、联网、养成系统或数据库。

## 当前功能

- 透明、无边框、不显示任务栏按钮的宠物窗口，默认始终置顶。
- 宠物等比显示，默认宽度 160 WPF DIP；窗口额外保留 4 DIP 动画余量。
- Idle / Walk 循环播放，Walk 左行自动水平翻转；点击播放 Happy 单次动画，自动行为可短暂进入 Sleep。
- 左键单击产生约 220ms 的轻微缩放；按住移动超过 5 DIP 后拖动，拖动不会触发点击。
- 无序列帧资源时自动回退为默认图片，并保留 P0 的呼吸/点击反馈和实际位移。
- 统一状态机：Idle / WalkingLeft / WalkingRight / Dragging / Paused / Happy / Sleeping。
- 拖动期间取消自动移动；松开后至少等待 3 秒，再恢复随机决策。
- 右键菜单：暂停/恢复、始终置顶、设置、隐藏、退出。
- 系统托盘：显示、隐藏、暂停/恢复、设置、退出；双击托盘图标显示宠物。
- 设置即时生效，500ms 防抖保存；拖动结束、自动移动结束及退出时保存位置。
- 主显示器工作区限制、无效位置恢复、损坏配置备份、单实例、基础日志、统一退出清理。

## 环境

- Windows 10 / Windows 11，x64。
- 开发需要 .NET 10 SDK；可以使用支持 .NET 10 的 Visual Studio 或 dotnet CLI。
- 自包含发布目录自带 .NET Desktop Runtime，运行发布 EXE 不要求预先安装 .NET。
- SDK 下载与 NuGet restore 需要网络；应用自身不发起网络请求。

微软官方参考：[.NET 10 下载](https://dotnet.microsoft.com/download/dotnet/10.0)、[Windows 安装说明](https://learn.microsoft.com/dotnet/core/install/windows)。

## 开发、编译和测试

在仓库根目录执行：

```powershell
dotnet restore
dotnet build
dotnet test
dotnet run --project src/DesktopPet/DesktopPet.csproj
```

当前任务环境没有系统 SDK，已在 `.tools/dotnet` 准备了工作区 SDK。使用前在当前 PowerShell 会话执行：

```powershell
. ./scripts/use-dotnet.ps1
```

该脚本只影响当前会话，设置本地 SDK 和缓存路径，不修改系统 PATH。如果在新电脑上也需要局部 SDK，可以先执行 `./scripts/bootstrap-dotnet.ps1`，下载微软官方 .NET 10 SDK ZIP 并验证 SHA512。

完整 Release 构建和测试：

```powershell
./scripts/build.ps1
```

任一 restore、build、test 命令失败时，脚本返回非零退出码。核心测试在 `tests/DesktopPet.Tests`，覆盖自然排序、动画资源回退、设置默认值/序列化/配置恢复、屏幕位置限制、状态切换冲突和随机行为范围，不做 WPF UI 自动化。

## 发布和运行

```powershell
./scripts/publish.ps1
```

等效命令：

```powershell
dotnet publish src/DesktopPet/DesktopPet.csproj -c Release -r win-x64 --self-contained true -p:PublishTrimmed=false -o artifacts/publish/win-x64
```

运行：`artifacts/publish/win-x64/DesktopPet.exe`。

请保留整个 `win-x64` 目录一起分发，不能只复制 EXE。未开启裁剪或单文件打包，以确保 WPF 资源与桌面运行时正常工作。普通 `dotnet publish` 也可生成各项目的默认发布输出；交付目录使用上面的脚本。

第一次启动显示在主屏右下角，避开任务栏。隐藏后程序继续运行，系统托盘图标可能位于 Windows 的“显示隐藏的图标”区域。关闭设置窗口不退出；Alt+F4 关闭宠物窗口会隐藏宠物。真正退出请使用宠物右键或托盘的“退出”。第二次启动会直接正常退出，不唤醒第一个实例。

## P1 动画资源和替换

资源源码目录为 `src/DesktopPet/Assets/Pet/`，build/publish 后会复制到 EXE 旁的 `Assets/Pet/`。当前目录结构如下：

```text
Assets/Pet/
  pet.jpg                 # 默认静态回退图；也支持 pet.png
  Idle/idle_000.png ...   # 待机循环，建议 4–6 FPS
  Walk/walk_000.png ...   # 通用行走回退，建议 8–12 FPS
  WalkLeft/walk_left_000.png ...   # 左行循环，建议 8–12 FPS
  WalkRight/walk_right_000.png ... # 右行循环，建议 8–12 FPS
  Happy/happy_000.png ... # 点击单次播放，建议 8–10 FPS
  Sleep/sleep_000.png ... # 睡眠循环，建议 3–5 FPS
```

替换素材时只需替换动作目录中的 PNG 并重新 build/publish。左右行走应分别放入 `WalkLeft` 和 `WalkRight`，程序会直接使用对应方向帧，不再对它们做水平镜像；旧的 `Walk` 目录保留为通用回退。文件名建议使用统一前缀加自然数字序号，例如 `walk_left_1.png`、`walk_left_2.png`、`walk_left_10.png`；程序会按数字而不是字符串排序。推荐所有帧使用相同的透明画布和尺寸（约 160–256 像素方形），使用透明 PNG，避免帧间主体位置跳动。每个动作建议 4–12 帧；实际帧数不限。

资源回退规则：`WalkLeft` / `WalkRight` 缺失时先回退到通用 `Walk`，再回退到 Idle；其他动作目录不存在、为空或没有可解码 PNG 时，直接回退到 Idle 帧。Idle 也不可用时回退到 `pet.png`，再回退到 `pet.jpg`。只有一张图片时按单帧动画使用。资源错误会记日志，不会导致程序崩溃。图片只在首次需要时加载并冻结，后续帧使用缓存，不会每帧读磁盘。

P1 默认启用序列帧动画。设置中关闭“启用序列帧动画”后，程序保留静态图、点击反馈、拖动、托盘和自动移动；动画速度提供 0.75x、1x、1.5x 三档。

原始 `pet.jpg` 是 3024 × 3024、不含透明通道的照片，SHA256 为 `BEE6D30248E3F4390371B728605B9410F81CED36695E7082F85CE38D8CCF24B4`。透明窗口不会自动去除照片背景。

## 设置、位置和日志

配置：`%LOCALAPPDATA%\DesktopPet\settings.json`。

日志：`%LOCALAPPDATA%\DesktopPet\logs\`，按天生成文本文件，记录启动、退出、配置读写失败和异常。日志写入失败不会导致程序崩溃。

设置包括：

| 字段 | 默认值 / 含义 |
|---|---|
| Topmost | true，始终置顶 |
| AutoBehaviorEnabled | true，自动行为 |
| ShowPetOnStartup | true，启动应用时显示（不是开机自启） |
| PetScale | 1.0，可设 0.8–2.0 |
| EnableAnimations | true，启用 PNG 序列帧动画 |
| AnimationSpeedMultiplier | 1.0，可设 0.25–3.0；设置界面提供 0.75/1/1.5 |
| MoveSpeed | Normal：45 DIP/s；Slow：25；Fast：75 |
| BehaviorFrequency | Normal：5–15 秒；Low：10–25；High：3–8 |
| LastPetX / LastPetY | 上次窗口位置，WPF DIP；首次为空 |

每次随机决定待机/左移/右移，偶尔进入 5–12 秒 Sleep，计划移动距离 50–200 DIP；接近边缘时距离会被工作区截短。暂停仍允许点击和拖动；隐藏后停止无意义的动画和计时器。

配置不存在则使用默认设置；JSON 损坏时尽力备份为 `settings.corrupt.<时间>.json` 并恢复默认。非法尺寸和枚举会规范化，无效位置会回到可见区域。配置先写同目录临时文件，再原子替换正式文件。不要在应用运行时手动改配置，否则应用保存时可能覆盖手工改动。

## 项目结构

```text
DesktopPet.sln
src/
  DesktopPet/          WPF 窗口、控制器、行为、托盘和生命周期
  DesktopPet.Core/     纯逻辑模型、动画定义/加载、状态机、配置、日志和几何计算
  DesktopPet/Animations/ WPF 序列帧缓存和 DispatcherTimer 播放器
  DesktopPet/Assets/Pet/ Idle、Walk、Happy、Sleep 和默认图片
tests/DesktopPet.Tests/
scripts/              SDK 引导、构建、发布
docs/                 设计、执行记录和验收记录
artifacts/publish/win-x64/
```

## 已知限制

- 默认回退图是无透明通道的 JPG，因此使用默认图时会保留原始照片背景；替换为透明 PNG 可得到更自然的桌宠效果。
- P0 将宠物限制在主显示器工作区；不支持跨显示器活动。采用系统 DPI awareness，混合 DPI 多屏效果不属于本次保证范围。
- 不做透明像素级命中测试，图片显示矩形内可拖动和点击。
- 托盘使用 Windows 系统应用图标；动画帧需要由用户自行提供或替换，当前不包含素材编辑器。
- 发布文件尚未进行代码签名，也没有安装器；交付为可运行目录。
- GUI 实测结果见 `docs/verification.md`。
