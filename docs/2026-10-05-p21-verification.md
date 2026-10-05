# P21 行走迈步、后腿支撑和循环修复

版本 0.9.12。用户要求修复前后脚迈步不自然的问题，继续保留原来小狗的外形；`小狗行走.mp4` 只用于参考行为。

重新查看视频旋转后的 45–68 帧，并以 90–113 帧核对另一个周期。近侧前脚在视频 47–55 帧仍支撑，随后抬起、回收、伸出；近侧后脚在 55 帧开始抬起，随后向前回收落地。远侧脚部分被遮挡，精确落地时刻和关节角度不能从视频认证。示意图是对原角色比例的人工适配，不能称为动作捕捉。

替换左右行走各 8 张透明素材，仍以 15fps 循环。修正前脚 2→3 反向跳回、右行抬错后腿、左行后脚落地点偏后和 6→7→0 的支撑跳变。左行按实际外侧粗后腿与内侧细后腿的连接关系设计，避免把右行的根部位置简单镜像过去。

使用内置 ImageGen 编辑完整单帧；连续姿势的头脸、耳毛、吐舌、项圈铃铛、紧凑体型和卷尾保持原角色特征。整图图集候选出现重复支撑姿势，未采用。打包仅对完整帧统一缩放和平移，注册头顶、头部横向位置和支撑地面，保留生成透明通道；没有分段拼腿、肢体变形或脚本抠图。支撑脚底约 y=228，远侧支撑约 y=222。

细微头宽、毛发轮廓和身体比例变化仍可见。左行 6→7 的后脚支撑移动比示意轨迹快，7→0 接近同一落点；本次没有宣称逐像素保留上半身或精确还原全部关节。

## 素材与复核

- [左右运行素材](../artifacts/imagegen/p21/packaged/)、[原 P20 素材备份](../artifacts/imagegen/p21/previous-runtime/)
- [左行检查图](../artifacts/imagegen/p21/WalkLeft-contact.png)、[右行检查图](../artifacts/imagegen/p21/WalkRight-contact.png)、[交互式前后预览](../artifacts/imagegen/p21/preview.html)
- [视频与四脚路径记录](../artifacts/imagegen/p21/reference/four-paw-motion.json)、[缩放、透明边界与帧哈希](../artifacts/imagegen/p21/validation.json)
- [初始单帧提示词](assets/2026-10-05-p21-single-pose-prompts.json)、[左行与图集候选提示词](assets/2026-10-05-p21-refinement-prompts.json)、[关键姿势精修提示词](assets/2026-10-05-p21-key-repair-prompts.json)、[末帧精修](assets/2026-10-05-p21-loop-repair-prompt.json)
- [独立视觉复核](../artifacts/verification/p21-gait-review.md)、[前脚支撑轨迹检查](../artifacts/verification/p21/fore-support-qa.json)

独立复核确认前脚明显反向跳回和主要后腿支撑错误已处理，最终左行末帧未发现新的重要阻碍。数字检查仅验证前脚支撑的方向、高度和边界，不能代替后腿归属与运动连续性的视觉检查。

## 验证和发布

完整 Windows 集成检查 466 项、皮肤检查 42/42 通过。实际 WPF 定时播放另有 44 项检查通过，左右分别捕获 19 个换帧样本，覆盖全部 8 个姿势和至少两次循环。核对了实际渲染中的脚部、方向和循环闭合。应用没有增加镜像或合成身体上下晃动。

- [Windows 检查日志](../artifacts/verification/p21/windows-final.log)、[皮肤检查日志](../artifacts/verification/p21/skins-final.log)
- [实际播放日志](../artifacts/verification/p21/native-final.log)、[实际左行渲染](../artifacts/verification/p21/native-final/WalkLeft-contact.png)、[实际右行渲染](../artifacts/verification/p21/native-final/WalkRight-contact.png)
- [源码范围核对](../artifacts/verification/p21/source-scope.json)、[发布启动验证](../artifacts/verification/p21/release-final.log)、[用户数据保留](../artifacts/verification/p21/user-preservation.json)
- [发布元数据](../artifacts/verification/p21/release-metadata.json)

初次命令误把离线源选项传给测试程序，皮肤检查没有执行；随后直接运行已编译测试程序，42 项完整通过。初次 Windows 检查遇到旧实例占用快捷键，退出旧实例后重新执行完整套件并通过。未修改测试预期或跳过冲突检查。

旧实例没有响应正常退出信号，等待一次成功自动保存并复制最新快照后结束进程，再启动新版。修复过程中未覆盖或回滚真实用户文件。原有 62 条日记全部保留；非位置设置、好感度、累计计数与已访问日期未丢失。新版自然回窝新增一条日记，最近回窝时间、饱腹和精力随正常行为与计时更新。

源码只替换 16 张行走 PNG，其余 232 个源码文件（含参考照片、视频和其他动作）保持原哈希；应用行为逻辑和测试源码未改。版本号及发布脚本默认目录更新到 P21。P20 压缩包哈希保持不变。

P21 EXE 版本 0.9.12.0，已启动，PID 11780；175 个发布素材哈希匹配源码，单实例检查通过，四个关键运行模块来自随包 .NET 10.0.12。ZIP 含 668 项、88,833,129 字节：

- [运行程序](../artifacts/publish/p21-win-x64/DesktopPet.exe)
- [发布包](../artifacts/DesktopPet-P21-win-x64.zip)

SHA256：`B5630FBF0D9747C590A8B24DD033E97FC5D1A60102E7A8B6B2AD3D22821E15A6`。
