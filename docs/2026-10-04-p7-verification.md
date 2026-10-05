# P7 统一动作与完整叼回玩法验收

## 本轮实现

承接已授权的四项优化：统一参考小狗的基础动作，增加起步/刹停/转身，完善地面追逐与跳接/捡取，并让玩具完整叼回投掷原位。保留上一版左右行走、左右奔跑、跟随、小窝、挑战、皮肤、日记与养成。

确定性的 `FetchSequence` 顺序推进追逐、跳跃或捡取、叼回、放下、摇尾和完成。小球落地后低头捡起；高度和距离合适的飞盘尝试跳接，未接到则落地后捡取。狗沿同一地面追逐，玩具随飞行轨迹运动，返回使用投掷起点的X和活动区域地面。每帧嘴部定位绑定玩具，放下时玩具落到地面并短暂保留。

仅完成放下和庆祝后才奖励玩耍、记录日记或计入三回合挑战。隐藏、全屏、拖动、摸头、菜单、设置变化和主动停止使用现有取消路径，取消后不补发完成事件。动作过渡由动画管理器内部消费，不触发互动完成事件；新请求替换旧目标，关闭动画时保持静态帧。缺少过渡的旧皮肤直接进入目标动作，缺少新姿态使用同方向行走回退。

捡取、放下和庆祝时长随动画速度变化。跳跃物理腾空限制为0.28–0.7秒，避免慢速设置让接盘持续悬空；完整姿态按阶段进度推进。此上限是有意保留的速度例外。

## 素材与预览

内置ImageGen参考 `Assets/Pet/pet.jpg`、三视图和P6外形基准生成，保留照片的蓬松大头、短口鼻、红色花纹项圈与金色铃铛。总计20次生成：10份基础图集、9份运动图集和1份重做的转身图集。

- 60张新基础帧：Idle/Happy/Sleep各8；Petting/Feeding/Stretching/Yawning/Dragging各6；LookLeft/LookRight各3。
- 72张新运动帧：左右起步/刹停/转身/捡取/放下各4；左右跳接/叼回各8。
- 保留P6左右行走/奔跑共32帧；通用Walk复制右向行走8帧。当前共29个动作目录、172张动作PNG，加两张原始参考图和嘴部JSON为175个发布素材。

生成图与提示词记录分别在 `artifacts/imagegen/p7/base/`、`motion/` 和 `docs/assets/2026-10-04-p7-*-prompts.json`。Pillow仅切完整单元、整帧统一缩放/平移和制作预览，不变形身体、不镜像方向、不修补透明蒙版。第一版转身的规则网格会切到毛发，弃用并重新生成；第二版沿不等宽透明间隔切出完整单元，全部八帧使用相同像素缩放比例。GIF预览速度已对齐运行时默认速度，重新打包后的132个PNG与已安装源素材SHA256逐一一致。

原始透明度保留；深色背景、全尺寸/160px动作联系表和GIF经检查。WPF检查另外渲染了左右方向的球/飞盘叼回合成画面，检查嘴边玩具位置。只渲染测试自己创建的宠物与玩具视觉，不截图用户桌面。

预览：

- [基础动作联系表](../artifacts/imagegen/p7/base/all-actions-contact-160.png)
- [运动动作联系表](../artifacts/imagegen/p7/motion/all-actions-contact-160.png)
- [叼球画面](../artifacts/verification/p7-windows/Ball-Carry-Scene-Right.png)
- [叼飞盘画面](../artifacts/verification/p7-windows/Frisbee-Carry-Scene-Left.png)

## 检查与复核

首次完整运行 `./scripts/test-offline.ps1 -WindowsIntegration`：核心289、皮肤29、日记83、Windows211，合计612项通过。日志：`artifacts/verification/p7-all-checks-first.log`。最后的嘴部元数据修复仅改变WPF动画加载，重新运行全部Windows检查后213项通过；最终覆盖总数为 **289 + 29 + 83 + 213 = 614项**。未把重复专项运行叠加计数。

核心检查包括枚举兼容、动作回退和过渡选择，及球/飞盘完整流程、各阶段取消、不同速度/大小、负屏幕坐标和窄区域。原生Windows检查包含双向完整叼回、过渡队列中断、关闭动画、嘴部逐帧绑定、错误元数据、旧皮肤、三回合挑战及原有功能回归。

保留失败和成功证据：

- `p7-animation-red.log`、`p7-fetch-red.log`、`p7-wpf-red.log`：新接口或新行为加入前的失败。
- `p7-static-pose-red.log`：关闭动画后手动玩法进度仍换帧；修复后静态帧检查通过。
- `p7-anchor-schema-red.log`：错误嘴部条目使后续有效动作未加载；增加类型与长度校验，单个动作独立回退，两种错误类型回归通过。
- `p7-final-windows.log`：最终213项Windows检查通过；渲染输出为 `p7-windows/`。

首次完整运行仍使用旧的 `p6-windows/` 默认输出目录；最终运行已改为 `p7-windows/`，不将旧失败日志重命名为成功。停止过渡检查曾因固定等待时间在重负载下误判，改为有上限地等待目标动作，仍检查计时器停止。

独立只读复核未发现Critical/Important问题，记录：`artifacts/verification/p7-review.md`。两项Minor已处理：跳跃时长上限在此说明；错误嘴部条目的解析问题已修复并通过回归。复核者未运行构建，其判断和主代理实际检查证据分开记录。未穷举每个玩法阶段的全部原生中断组合，取消路径另外进行了静态复核。

## 数据与历史保留

无Git仓库，修改前源码快照：`artifacts/verification/p7-source-clean-before.zip`。替换的基础帧保存在 `artifacts/imagegen/p7/previous-runtime/`，原参考图与P6左右步态保留。旧P6程序和ZIP保留。

验收均使用临时设置/养成目录。启动前保存真实用户的最新 `settings.json`、`profile.json` 至 `p7-user-before-restart-*.json`，核实路径后停止旧P6进程，记录为 `p7-old-process-stop.json`。停止前数据为：名字团团、黏人性格，亲密度26，摸头2、喂食3、玩耍7、回窝0、专注1。

启动后核对通过：用户配置（移动位置单独处理）、名字、性格、亲密度26、上次亲密互动时间、完整日记和所有计数保留。核对时饱腹与精力差值均为0，没有重置或补扣离线时间。证据：`artifacts/verification/p7-user-migration.json` 与 `p7-user-after-*.json`。

## 发布与启动

0.7.0独立发布目录：`artifacts/publish/p7-win-x64/DesktopPet.exe`；分发包：`artifacts/DesktopPet-P7-win-x64.zip`。`./scripts/publish.ps1` 退出0；`./scripts/verify-release.ps1 -StartCheck` 退出0。

- 175个发布素材逐一匹配源文件SHA256，无旧帧混入。ZIP共668项，包含完整动作、嘴部定位、DLL和运行库。
- 私有.NET Core与Windows Desktop运行库均为10.0.12，hostfxr/hostpolicy/coreclr/wpfgfx均实际从该发布目录加载。
- 发布的DesktopPet及Core DLL与Release构建产物逐一匹配；EXE文件版本0.7.0.0。
- ZIP大小88,656,033字节，SHA256：`AD57B0052676B45C502439ABA90DB4D394A830F80FA4C192CACB2B6CC70F5567`。
- 新版启动验收PID2632、Responding=true；重复启动退出0，并向已有实例发送找回信号。验收后保留新版运行。
- P6目录和ZIP均保留；旧ZIP SHA256仍为 `DEC6091483D8B4AECFB8E26F014ED92132DBAF2C906F6314AD0260FFA18858F5`。

发布日志与机器可读记录：`p7-publish.log`、`p7-release-check.log`、`p7-release-metadata.json`、`p7-win-x64-startup.json`，均位于 `artifacts/verification/`。

## 验证边界

当前机器只有一块150%缩放显示器。负坐标和窄区域有模型检查，混合DPI双屏拖动、真实游戏全屏及Windows重启仍未实机验收。AI逐帧素材可能有轻微毛发变化；发布包未签名，未新增安装器或AI聊天。
