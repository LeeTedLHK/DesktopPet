# P19：根据行走视频重做步态

用户参考：`src/DesktopPet/Assets/Pet/小狗行走.mp4`。本轮改动范围为左右行走素材、对应播放节奏和发布验证；原视频及照片保留。

## 行走变化

旧素材中有连续重复的伸腿姿势，后腿缺少弯曲收回过程。P19以视频连续姿势引导重绘，补齐后腿支撑、向后蹬地、弯曲收回、向前落脚；前腿交替支撑和摆动。保持低抬脚、较短步幅和较平稳的背部，保留白色卷毛、红花项圈与金色铃铛。

视频为30fps，原始画面物理侧转，开发参考按顺时针90度校正。选择第47、49、51、53、55、57、59、61帧，视觉上第47与63帧接近重复步态，相隔16/30秒。两个方向各8帧，以15fps循环，默认周期约0.533秒。未重复末帧。

视频姿势与生成素材存在差异，属于视频引导重绘，不是逐像素提取或精确动作捕捉。独立视觉复核确认，后腿回收比P18有实质改善，首尾腿部状态兼容。循环尾部仍有轻微的尾巴形状、头部角度和身体比例变化；这部分不能仅靠整帧对齐消除，也不把检查数量当作步态自然度的证明。

GIF预览：[向左](../artifacts/imagegen/p19/WalkLeft-preview-160.gif)、[向右](../artifacts/imagegen/p19/WalkRight-preview-160.gif)。GIF时间以10ms为单位，循环约530ms；程序按15fps播放。

## 素材与生成记录

使用内置ImageGen。先从视频提取开发参考画面，再以连续姿势为依据生成透明素材并补齐项圈、铃铛。左向对应图由ImageGen处理；该结果并非像素精确镜像，打包时只调整完整单元的编号以恢复时间顺序。

- [视频姿势选择记录](../artifacts/imagegen/p19/reference/motion-selection.json)
- [视频八阶段参考](../artifacts/imagegen/p19/reference/right-eight-phase-guide.png)
- [所有提示词、模式及选用路径](assets/2026-10-05-p19-video-walk-prompts.json)
- [选用左向原图](../artifacts/imagegen/p19/WalkLeft-selected-atlas.png)、[选用右向原图](../artifacts/imagegen/p19/WalkRight-selected-atlas.png)
- [左向帧目录](../src/DesktopPet/Assets/Pet/WalkLeft/)、[右向帧目录](../src/DesktopPet/Assets/Pet/WalkRight/)
- [完整透明与打包校验](../artifacts/imagegen/p19/validation.json)
- [独立视觉复核](../artifacts/verification/p19-gait-review/review.md)

打包仅裁切完整生成单元，以统一比例缩放和整帧平移对齐。保留生成透明通道，不进行肢体变形、拼腿、抠图掩码或脚本镜像。所有帧为256×256，实心脚底统一到y=228。边缘透明，无裁切脚掌、头部和尾巴。

旧行走帧保存在 `artifacts/imagegen/p19/previous-runtime/`，多余旧帧从源码及构建输出可逆地移入 `artifacts/verification/p19-quarantined-frames/`。旧发布包不修改。

## 验证

- 播放节奏回归先改预期到15fps，旧实现按预期失败：[RED记录](../artifacts/verification/p19-walking-core-red.log)。随后更新实现，完整离线检查通过。
- [离线检查](../artifacts/verification/p19-offline.log)：587项通过。
- [Windows集成检查](../artifacts/verification/p19-windows.log)：466项通过，实际WPF方向步态加载、起步过渡后换帧、关闭合成上下摇晃、投球/飞盘、叼回、庆祝、喂食、小窝、设置、计时与皮肤导入导出均通过原有检查。
- [独立皮肤检查](../artifacts/verification/p19-skins.log)：42/42通过。
- 合计1095项本轮执行的检查；未计入无法离线恢复的xUnit测试项目，也未重复计入Windows套件中的专题检查。
- [源码差异记录](../artifacts/verification/p19-source-diff.json)：16张行走帧替换，4张旧右向多余帧移出；其余宠物素材、原始照片和视频保持不变。

当前29个动作目录，共172张动作PNG。奔跑、叼回、起步、刹停、转身等动作保留原素材与节奏；通用回退行走仍为10fps。导出默认皮肤包含新的左右步态。

## 发布与数据保留

发布结果和启动核验写入 [发布元数据](../artifacts/verification/p19-release-metadata.json)，用户数据核验写入 [迁移记录](../artifacts/verification/p19-user-migration.json) 和 [当前日记连续性记录](../artifacts/verification/p19-current-journal-preservation.json)。最终启动结果写入 [最终状态](../artifacts/verification/p19-final-state.json)。

测试使用独立临时数据目录。真实设置和养成文件仅读取、复制快照，不覆盖、注入或回滚。更新前快照为团团、好感43、62条日记；启动后依据冻结快照核验原有记录、设置与计数，区分应用正常计时变化和真实用户互动。

发布目录：`artifacts/publish/p19-win-x64/`；版本0.9.10.0，携带本机官方.NET10.0.12 Core/Desktop私有运行时。发布包包含175个运行素材文件；球照片和行走视频只留在开发目录。

发布与启动验证通过，实际运行P19单一实例PID23724，进程响应正常；重复启动退出0并唤醒既有实例。四个关键运行时模块均来自发布目录内的 `runtime/`。159个无关运行素材与P18逐文件哈希一致，16张行走帧均匹配选用打包素材，4张旧右向多余帧不再发布；P6–P18压缩包哈希保持不变。

启动冻结快照保留团团、Clingy、好感43和62条原有日记；摸头7、喂食10、玩耍23、小窝13、专注完成1等计数与更新前一致，所有非位置设置不变。

压缩包：[DesktopPet-P19-win-x64.zip](../artifacts/DesktopPet-P19-win-x64.zip)，88,835,908字节，668项。

SHA256：`B9EBD9E7AD8F01D22E8A2D24BF9C924FB122EF7432C740BE2C7EF44012A6827D`。
