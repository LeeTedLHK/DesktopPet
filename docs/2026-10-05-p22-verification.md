# P22 / 0.9.13：行走头部比例与动作衔接

用户反馈行走时头部突然变大。本次修正左右行走及起步、停步、转身的素材比例，保留原比熊身份与 P21 已校正的迈腿顺序。

## 根因与修正

WPF 实际播放证实图像尺寸及变换在换帧时保持固定，没有单独的头部缩放。问题来自旧起步/停步素材的头比新行走帧小，以及循环帧的细微差异：头部上方轮廓宽度左侧 90–105px、右侧 90–103px。早期使用较深测量带会把躯干误计入头宽，已弃用该指标；最终测量带止于头顶下方45px，转身使用与尾巴分离的头顶连通区域。

通过内置 imagegen 局部修改头部，要求保留每帧的身体、前后脚姿态、尾巴与角色身份；随后仅对完整帧统一缩放和平移，校准头部比例与支撑地面。未使用分段抠头、肢体变形或运行时头部覆盖层。生成过程仍产生少量毛边、脚趾和落点细节变化，不能描述为腿部像素完全不变。

最终40张256×256 RGBA素材的上部头宽为100–103px；左右行走循环分别为101–103px与102px。支撑前脚0–4仍沿朝向单向后移，脚底在226–230px范围。左右行走继续各8帧、15fps，播放控制与应用行为代码未改。

转身原候选虽大小接近，侧脸接入三分之四脸仍会跳变。最终采用完整行走帧作为转身起止端点，保留新生成的中间转角：`TurnLeft0=WalkRight7`、`TurnLeft3=WalkLeft0`、`TurnRight0=WalkLeft7`、`TurnRight3=WalkRight0`。四对PNG哈希一致；转身接入行走时不再切换头部造型。

## 素材与复核记录

- [最终项目素材](../src/DesktopPet/Assets/Pet/)：[选用清单及哈希](../artifacts/imagegen/p22/selected-assets.json)，仅40张Walk/Start/Stop/Turn PNG变化，其余208个源文件保持原哈希，照片、视频及其他动作保留。
- 内置imagegen提示词：[行走与起停头部修正](assets/2026-10-05-p22-head-prompts.json)、[转身头部修正](assets/2026-10-05-p22-turn-head-prompt.json)。[原素材备份](../artifacts/imagegen/p22/previous-runtime/)与未选用的生成候选保留。
- [左侧完整流程对照](../artifacts/imagegen/p22/Left-lifecycle-candidate.png)、[右侧完整流程对照](../artifacts/imagegen/p22/Right-lifecycle-candidate.png)、[左转身接口](../artifacts/imagegen/p22/Left-turn-seam-candidate.png)、[右转身接口](../artifacts/imagegen/p22/Right-turn-seam-candidate.png)。
- [独立最终视觉复核](../artifacts/verification/p22-head-review.md)：未见重要步态回退，前脚支撑与近后腿抬起、回收、前落、支撑顺序可追踪；转身接口头脸突变已移除。

## 验证与发布

头部轮廓回归在旧素材上失败，在最终源素材上通过；检查覆盖起步、行走、转身和停步，动作接口差值不超过3px。[最终回归结果](../artifacts/verification/p22/head-regression-final.json)、[旧素材失败记录](../artifacts/imagegen/p22/regression-before.json)。

真实WPF定时播放有324项尺寸、变换及覆盖检查通过，79个换帧样本覆盖所有左右起步、行走、转身、停步姿态。捕获程序没有控制器、设置或成长服务，不会写真实存档；所用175个素材与源码哈希一致。[播放日志](../artifacts/verification/p22/native-final.log)、[左侧实际渲染](../artifacts/verification/p22/native-final/Left-native-contact.png)、[右侧实际渲染](../artifacts/verification/p22/native-final/Right-native-contact.png)。

完整[Windows集成检查](../artifacts/verification/p22/windows-final.log)466项、[皮肤检查](../artifacts/verification/p22/skins-final.log)42/42通过。生成新版发布包，175个发布素材与源码一致，ZIP含668项，携带.NET 10.0.12私有运行时。启动及重复实例唤回检查通过，新版运行进程为3492。[发布检查](../artifacts/verification/p22/release-final.log)、[发布元数据](../artifacts/verification/p22/release-metadata.json)。

旧实例在一次成功自动保存后结束，未改写或恢复真实用户文件。原74条成长日记全部保留；新版因夜间自然回窝新增一条，合计75条。非位置设置、好感、累计计数及已访问日期保留，最近动作时间未倒退。[存档核对](../artifacts/verification/p22/user-preservation.json)、[重启记录](../artifacts/verification/p22/restart-save.json)。P21发布包哈希保持不变。

启动：[DesktopPet.exe](../artifacts/publish/p22-win-x64/DesktopPet.exe)。分发：[DesktopPet-P22-win-x64.zip](../artifacts/DesktopPet-P22-win-x64.zip)，88,953,820字节，SHA-256为`F01B6BC1DF6F9C9FD8FA1A69567A8862A656FEFA390C79FC932E430A912F638A`。

仍有轻微起步压身、转身约150ms同姿态持帧与四帧转角较粗的限制；Left6→7较快的后脚后移属于P21已有残余。毛发与远侧遮挡关节也有生成变化；本次修复头部突变，不代表所有动作已达到精细连续插值。
