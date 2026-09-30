# DesktopPet P1 验收记录

日期：2026-09-13。系统：Windows x64；使用仓库 `.tools/dotnet` SDK 10.0.401。

## 构建与自动测试

- `dotnet restore DesktopPet.sln`：成功，退出码 0。
- `dotnet build DesktopPet.sln -c Release --no-restore`：成功，0 warning、0 error。
- `dotnet test DesktopPet.sln -c Release --no-build`：83 通过，0 失败，0 跳过。
- `scripts/publish.ps1`：成功，退出码 0，生成自包含 `win-x64` 发布目录。
- 发布时 restore 曾出现 `NU1900`：环境无法访问 `https://api.nuget.org/v3/index.json` 获取漏洞数据；依赖使用本地缓存，publish 仍成功完成。该网络警告不属于编译或测试失败。

## P1 资源与源码

- 新增 Core 动画模型、自然数字排序、动作目录扫描和回退链。
- 新增 WPF `AnimationManager`：冻结 BitmapImage、按路径缓存、单 DispatcherTimer、Loop/Once 和完成事件。
- 新增 `Happy`、`Sleeping` 状态；接入点击 Happy、移动 Walk、Idle、Sleep 和拖动/暂停稳定帧。
- `WalkingLeft` / `WalkingRight` 已改为分别使用 `WalkLeft` / `WalkRight` 方向帧，旧 `Walk` 保留为通用回退。
- Release build 输出资源检查：Idle 96 帧、Walk 96 帧、WalkLeft 96 帧、WalkRight 96 帧、Happy 120 帧、Sleep 120 帧，另有 `pet.jpg` 默认回退图。
- 资源加载不依赖当前工作目录；publish 目录包含 `Assets/Pet` 全部动作目录。
- 没有加入 Thread.Sleep、while(true)、网络、数据库或第三方动画依赖。

## Windows 运行验证

- 实际启动最终发布版 `artifacts/publish/win-x64/DesktopPet.exe`。
- 首实例 PID 12240，路径为最终 publish EXE，`Responding=True`；设置为 `ShowPetOnStartup=true`。
- 第二次启动在 5 秒内退出，退出码 0，验证单实例互斥仍生效。
- 进程的 `MainWindowHandle=0`；该窗口配置为不显示任务栏/Alt+Tab，当前 CUA GUI 工具初始化后返回空应用列表及浏览器服务错误，因此未能通过 GUI 工具执行点击、拖动、菜单和截图操作。
- 因此不能把进程存活等同于完整 GUI 交互验收；视觉观感、托盘菜单和实际帧切换仍建议在本机桌面人工点验。

## 尚需人工 GUI 验收

- Idle/Walk/WalkLeft/WalkRight/Happy/Sleep 帧的实际视觉效果、方向表现和帧间尺寸稳定性。
- 单击与拖动阈值、拖动期间动画冻结、右键菜单。
- 暂停/恢复、隐藏/托盘显示、设置窗口中的动画开关和速度。
- Sleep 的实际随机触发和结束返回 Idle。

## 已知限制

- 默认 `pet.jpg` 不含透明通道，使用默认图时会保留照片背景；透明 PNG 需由用户替换素材。
- 移动仍限制在主显示器工作区，不支持多显示器复杂 DPI 和像素级命中测试。
- 动画资源随 build/publish 复制到 EXE 旁；替换素材后需要重新构建/发布。
- 无安装器和代码签名；托盘使用系统应用图标。
