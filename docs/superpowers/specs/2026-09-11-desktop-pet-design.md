# DesktopPet P0 设计

根据用户完整附件直接实施，用户明确要求自行决策、不等待方案确认。

- C# / WPF / .NET 10，Windows 10/11 x64，版本 0.1.0。
- 原始 JPG 按字节复制为 Assets/Pet/pet.jpg，Resource 打包，不重绘、不裁切。原照片背景保留；支持日后替换透明 PNG。
- PetWindow 无边框、透明、不进任务栏、默认置顶；160 DIP 基准宽度，比例保持，4 DIP 动画余量。
- 控制器协调手动拖动（5 DIP 阈值）、点击反馈、菜单、设置和生命周期；拖动松手后至少等待 3 秒。
- PetStateMachine 管理 Idle、WalkingLeft、WalkingRight、Dragging、Paused；暂停偏好独立于临时拖动和隐藏。
- BehaviorManager 使用低频决策 DispatcherTimer 和仅走动期间 30Hz 位置计时器；呼吸/点击使用 WPF 动画。隐藏时停止动画和行为。
- 主显示器 SystemParameters.WorkArea，以 WPF DIP 为坐标，系统 DPI aware；限制在工作区内。显示器变化时重新校正。
- 原生 WPF 设置窗口即时应用。JSON 原子替换保存到 LocalAppData/DesktopPet，坏配置备份、字段校验、线程安全尽力日志。
- NotifyIcon 托盘维持隐藏状态运行。显式 Shutdown、单实例 Mutex、统一退出和异常清理。
- 小型纯逻辑 Core 库供应用和 xUnit 测试共用，不引入生产 NuGet 依赖。
- 不开发联网、AI、养成、额外物理和第三方 UI。

验证：真实 restore/build/test/publish；检查资源哈希；启动发布 EXE、单实例、运行日志；GUI 工具可用则进行交互验证，否则明确限制。
