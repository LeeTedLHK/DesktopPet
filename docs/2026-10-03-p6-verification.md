# P6 参考外形行走与追逐奔跑验收

## 请求与实现

用户指出上一版行走不像参考小狗，并要求追球和追飞盘时使用奔跑动画。本次以 `src/DesktopPet/Assets/Pet/pet.jpg` 为外形主参考，三视图辅助身体结构；先制作外形基准，再制作四组动作。保留照片的大头、长耳毛、卷毛、暖色口鼻、黑眼睛/鼻子、红项圈与黄色铃铛。

`RunningLeft/RunningRight` 与 `RunLeft/RunRight` 追加到枚举末尾，保留旧值。奔跑12fps循环，仅用于球/飞盘追逐和三回合接球挑战，移动速度提高；自动散步、回窝和跟随鼠标使用原来的行走逻辑。停止、隐藏、全屏、摸头、拖动、设置变化和菜单打开沿用统一取消路径。

旧皮肤不要求重新导入。缺少奔跑时优先使用同方向行走，再依次使用通用行走、待机、默认图片。新增 RunLeft/RunRight 文件夹和 ZIP 都可导入。

## 素材方法

素材通过内置 ImageGen 参考原图生成。原始生成图、弃用候选、合成预览与循环GIF均保留在 `artifacts/imagegen/p6/`；提示词和来源记录在 `docs/assets/2026-10-03-p6-imagegen-prompts.json`。Pillow 仅拆分整格、整帧缩放/平移及生成检查预览，不合成姿态、不变形身体部位、不重建透明蒙版。

外形基准及已选行走帧经人工对照照片检查。边缘过紧或脚掌跨格的候选弃用。奔跑保留蹬地、伸展、腾空、收腿和落地的不同姿態及腾空高度；移除运行时额外呼吸和上下晃动变换。AI逐帧生成仍可能有轻微毛发变化。

## 检查记录

- 核心离线检查194项通过，包含81项新增运行状态、映射、枚举兼容、循环速度及各级旧皮肤回退检查。
- 皮肤检查29项通过，新增方向奔跑文件夹/ZIP和旧皮肤对应方向回退。
- 日记检查83项通过。
- Windows逻辑专项25项通过，验证双向球/飞盘奔跑、物理tick保持状态、方向反转、垂直目标朝向稳定、停止/拖动/全屏/摸头取消、挑战奔跑以及回窝/跟随保持行走。
- 初始 Windows RED：球追逐仍处于 WalkingRight；改用独立运行状态后通过。
- 完整离线初次运行在测试临时目录删除 `notes.txt` 时出现短暂拒绝访问；重复执行有成功和失败，延后独占读写成功。将P6测试目录放到自有输出内并仅对清理进行有限重试后，完整306项离线检查通过。持续清理失败仍使检查失败，未修改产品逻辑。
- 独立审查目前无 Critical/Important 问题，见 `artifacts/verification/p6-review.md`。

实际素材播放与完整Windows回归通过151项检查，包含41项P6专项；总计194+29+83+151=457项通过。证据：`artifacts/verification/p6-offline-checks.log`、`p6-final-windows.log`；`p6-all-checks.log`合并这两份成功记录。真实WPF渲染在 `artifacts/verification/p6-windows/`，包括双向小球/飞盘奔跑。

完整窗口回归曾发现左向抛掷起点把宠物中心当成玩具左上角，导致最初物理tick可能朝反方向转身。新增玩具中心对齐检查先失败（`p6-spawn-red.log`），将初始X减去玩具半径后，41项专项与151项完整Windows检查通过。玩具Y、速度、弹跳和捕获条件保持原值。

首次完整快捷键检查被旧版P5 PID6792占用。核实其路径为本项目P5发布EXE，备份当前已保存的数据后停止该实例，最终检查验证了快捷键注册、切换、注销与冲突提示。旧失败记录均保留；旧进程处理记录为 `p6-old-process-stop.json`。

## 保留与数据

修改前源码快照：`artifacts/verification/p6-source-clean-before.zip`。用户设置与养成数据修改前快照：`p6-user-before-settings.json`、`p6-user-before-profile.json`；旧实例停止前最新持久化数据快照：`p6-user-before-restart-settings.json`、`p6-user-before-restart-profile.json`。用户日记计数为摸头2、喂食2、玩耍5、回窝0、专注完成1，亲密度25。验收使用临时控制器，不增加真实用户互动计数。

最新持久化数据与启动后数据核对通过：设置、亲密度25、上次亲密互动时间、完整日记和计数均保留，饱食度和精力未重置。记录：`artifacts/verification/p6-user-migration.json`。

## 发布

版本0.6.0，EXE：`artifacts/publish/p6-win-x64/DesktopPet.exe`；ZIP：`artifacts/DesktopPet-P6-win-x64.zip`。P5发布及更早发布均保留，P5压缩包SHA256与原值一致。

- 502个发布素材逐一匹配源文件SHA256，无旧帧混入；ZIP共995项，四组动作各8帧。
- 私有.NET Core和Windows Desktop运行库均为10.0.12，启动加载的hostfxr/hostpolicy/coreclr/wpfgfx均来自发布目录。
- 发布DLL与Release编译产物一致，EXE文件版本0.6.0.0。
- ZIP大小100706642字节，SHA256：`DEC6091483D8B4AECFB8E26F014ED92132DBAF2C906F6314AD0260FFA18858F5`。
- 最终EXE已运行，启动验收PID19228、Responding=true；重复启动退出0并向现有实例发送找回信号。

发布和启动记录：`artifacts/verification/p6-release-check.log`、`p6-release-metadata.json`、`p6-win-x64-startup.json`。独立完整复核见 `p6-review.md`。
