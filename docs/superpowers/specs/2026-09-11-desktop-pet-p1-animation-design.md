# DesktopPet P1 动画升级设计

## 目标

在保留 P0 窗口、拖动、点击、托盘、设置、位置持久化、单实例和自动移动能力的前提下，把静态宠物图片升级为可缓存的 PNG 序列帧动画，支持 Idle、Walk、Happy 和 Sleep。

## 现状与范围

- WPF `PetWindow` 当前通过 pack URI 加载 `Assets/Pet/pet.jpg`，并保留呼吸、行走上下浮动和点击缩放动画。
- `BehaviorManager` 使用 DispatcherTimer 做低频行为决策和 30Hz 位移更新。
- Core 状态机当前包含 `Idle`、`WalkingLeft`、`WalkingRight`、`Dragging`、`Paused`。
- 仓库已经包含 `Assets/Pet/Idle`、`Walk`、`Happy`、`Sleep` PNG 帧；项目仍需将这些资源纳入构建和发布。
- 本次不引入网络、数据库、第三方动画库、多宠物或新的 UI 技术栈。

## 设计

### Core 动画模型

在 `DesktopPet.Core/Animations` 增加可测试的纯逻辑类型：

- `AnimationKind`：Idle、Walk、WalkLeft、WalkRight、Happy、Sleep；Walk 是通用回退，左右动作使用独立方向帧。
- `AnimationPlayback`：Loop 或 Once。
- `AnimationFrame`：资源标识和帧序号。
- `AnimationClip`：动作类型、帧列表、帧间隔、播放模式。
- `AnimationDefinition`：动作到目录/默认帧率/播放模式的定义和状态映射。
- `NaturalSort`：按文件名中的数字片段比较，保证 `walk_2` 排在 `walk_10` 前。
- `AnimationLoader`：扫描目录、过滤 PNG、自然排序、生成定义所需的帧路径，并应用回退链；不解码 WPF 图片。

回退链固定为：`Idle → pet.jpg`，`WalkLeft/WalkRight → Walk → Idle → pet.jpg`，`Happy → Idle → pet.jpg`，`Sleep → Idle → pet.jpg`。不存在、为空、只有坏文件或扫描异常时返回可播放的单帧默认 clip，并通过注入的日志回调记录原因。

### WPF 动画播放

在 WPF 项目中增加 `Animations/AnimationManager`。它在初始化/切换时加载并冻结 `BitmapImage`，以资源路径为键缓存图片，不在每次 tick 从磁盘读取。使用一个 `DispatcherTimer` 按当前 clip 的帧间隔推进，切换同一 clip 时不重置计时器，Once clip 播放完触发 `PlaybackCompleted`。

`PetWindow` 继续拥有 Image、窗口尺寸和 P0 Transform；`AnimationManager` 只通过 `Action<ImageSource>` 帧回调更新 `PetImage.Source`，不会接管输入、窗口位置或菜单。动画关闭、隐藏、拖动时停止计时器并显示当前/默认静态帧。

### 状态与行为

- 增加 `Happy`、`Sleeping`。
- `Idle` → Idle loop。
- `WalkingLeft` → WalkLeft loop；`WalkingRight` → WalkRight loop。方向帧已经按方向绘制，不再依赖水平镜像；通用 Walk 只作为回退。
- `Happy` → Happy once；完成后回到 Idle。
- `Sleeping` → Sleep loop；睡眠定时结束后回到 Idle。
- `Dragging` → 停止帧推进，显示稳定帧。
- `Paused` → 停止自动行为，显示 Idle 或稳定帧。

点击仍然通过现有 5 DIP 拖动阈值判定；确认是 click 后进入 Happy，并保留 P0 缩放反馈作为资源回退/补充。拖动和右键菜单不改变现有优先级。

`BehaviorManager` 只在 Idle 且自动行为启用时决策，新增低概率 Sleep 分支和睡眠停留 DispatcherTimer；暂停、隐藏、拖动会停止决策/位移/睡眠计时器。若 Sleep 没有真实资源，状态仍可安全回退为 Idle clip，或直接不选择 Sleeping。

### 设置与资源

`AppSettings` 增加：

- `EnableAnimations = true`。
- `AnimationSpeedMultiplier = 1.0`，规范化到 `0.25–3.0`，旧 JSON 缺字段时使用默认值。

设置窗口提供动画开关和速度倍率的轻量控件；关闭动画只禁用序列帧计时器，保留静态图片、P0 移动、拖动、点击和托盘能力。

资源目录为 `src/DesktopPet/Assets/Pet/{Idle,Walk,WalkLeft,WalkRight,Happy,Sleep}`，继续保留 `pet.jpg` 作为默认回退图。所有 PNG 复制到 publish 输出可访问的资源中。

### 异常与生命周期

资源目录不存在、夹杂非 PNG、文件名异常、图片损坏、JSON 缺字段或损坏均不能使程序退出。Core 解析跳过异常条目；WPF 解码失败跳过该帧并回退；设置服务继续使用现有损坏备份和默认值逻辑。所有 DispatcherTimer 和事件在窗口/控制器 Dispose 时解除。

## 测试

Core 测试覆盖：

- 自然数字排序和非数字文件名稳定排序。
- 动画定义的默认 FPS、loop/once 和状态映射。
- 缺失、空目录和缺失 Happy/Sleep 时的回退链。
- `Happy`、`Sleeping` 的状态转换和完成回 Idle；拖动/暂停冲突保护。
- `AppSettings` 新字段默认值、边界规范化和旧 JSON 反序列化兼容。

不增加 WPF 自动化测试；WPF 播放器通过 build、publish、资源检查和真实启动验证。

## 验证标准

执行 `. ./scripts/use-dotnet.ps1` 后运行 `dotnet restore DesktopPet.sln`、`dotnet build DesktopPet.sln -c Release`、`dotnet test DesktopPet.sln -c Release` 和 `./scripts/publish.ps1`。记录每个命令的退出码和测试数量；启动最终 publish EXE，检查进程存活、单实例、日志和资源加载。无法用 GUI 工具操作无任务栏窗口的部分必须在交付报告中明确标注，不将进程存活等同于完整 GUI 交互验收。
