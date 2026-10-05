# P20：保留原小狗外形，只参考视频步态

用户明确：`小狗行走.mp4` 用于参考行走的行为模式，不能把原来小狗的样子改成视频里的狗。

P19的生成流程直接输入了视频狗的画面，导致头部变小、改为侧脸、体型变长、表情和尾巴变化。P20改为编辑原P16宠物素材，保留圆头、蓬松长耳毛、略朝向屏幕的侧脸角度、吐舌表情、紧凑身体、蓬松卷尾、红花项圈和金色铃铛。素材在两个方向分别以原外形校准大小。

视频只用于观察步序、后腿向后支撑/蹬地/弯曲回收/落脚及时间节奏。把这些行为转换为抽象的关节示意图；ImageGen收到原宠物素材与关节示意图，未收到视频中小狗的图片。示意图为人工概括，不是动作捕捉的精确测量。

两个方向各8帧、15fps，默认周期约0.533秒；播放逻辑与P19保持一致。本轮不修改控制器、玩法、数据逻辑、动作定义或测试预期，其他动作素材保持不变。

## 外形与素材

使用内置ImageGen编辑原角色的腿部。打包仅裁切完整单元，以每个方向固定比例缩放及整帧平移对齐；保留生成透明通道，不拼接腿部、不制作抠图掩码、不用脚本变形肢体。

- [左右提示词、素材路径和输入角色](assets/2026-10-05-p20-appearance-walk-prompts.json)
- [右行末帧的后脚及透明背景修正记录](assets/2026-10-05-p20-rear-refinement-prompt.json)
- [步态示意记录](../artifacts/imagegen/p20/motion-only-guide.json)
- [左向原图](../artifacts/imagegen/p20/WalkLeft-selected-atlas.png)、[右向原图](../artifacts/imagegen/p20/WalkRight-selected-atlas.png)
- [左向素材目录](../src/DesktopPet/Assets/Pet/WalkLeft/)、[右向素材目录](../src/DesktopPet/Assets/Pet/WalkRight/)
- [左行预览](../artifacts/imagegen/p20/WalkLeft-preview-160.gif)、[右行预览](../artifacts/imagegen/p20/WalkRight-preview-160.gif)
- [原外形、上一版与修正后的并排对照](../artifacts/imagegen/p20/appearance-comparison.png)
- [透明边界、脚底、尺寸和裁切记录](../artifacts/imagegen/p20/validation.json)
- [独立外形及步态复核](../artifacts/verification/p20-appearance-review.md)

右行末帧采用另一次ImageGen结果中的完整单元，让近侧后脚在胯下支撑，远侧后脚保持可辨识的回收姿势；其余7帧均保留最初选用结果。过度抬脚及带灰色透明残影的候选被拒绝，未将其裁切到运行素材中。透明背景由ImageGen重新提取，无脚本抠图处理。

每帧256×256，实心脚底统一到y=228。头脸、耳毛、表情和尾巴按原素材保持一致；局部毛发仍可能有生成带来的细微变化，不能称为上半身逐像素不变。

P19行走素材保存在 `artifacts/imagegen/p20/previous-runtime/`。原始两张参考图、行走视频、其他动作素材和旧发布包不改动。

## 验证及发布记录

视觉复核以原角色外形为首要验收条件，未把数值检查通过等同于外形通过。素材透明边缘和脚底范围检查通过，实际WPF加载及方向换帧检查有独立记录。

- [最终素材的Windows完整集成检查](../artifacts/verification/p20-windows-final.log)
- [独立皮肤检查](../artifacts/verification/p20-skins.log)
- [源码范围检查](../artifacts/verification/p20-source-diff.json)
- [发布元数据](../artifacts/verification/p20-release-metadata.json)
- [真实用户数据保留](../artifacts/verification/p20-user-migration.json)
- [当前日记连续性](../artifacts/verification/p20-current-journal-preservation.json)
- [最终进程状态](../artifacts/verification/p20-final-state.json)

真实设置和养成文件仅被读取、复制快照，不覆盖、不回滚、不注入测试数据。Windows及皮肤测试使用临时目录。程序自然计时产生的状态更新与用户互动分别核验。

最终Windows集成检查466项通过，独立皮肤检查42/42通过，共508项不同用例。右行末帧精修后重新执行完整Windows套件；首次执行的同一466项未重复累计。源码范围核验确认16张行走帧替换，应用行为逻辑与测试源码均未修改，其他163个源码宠物相关文件（含原始参考图和视频）保持原哈希。

独立视觉复核最终通过：原角色外形保持一致，右行6→7→0后脚支撑可辨识，脚底、透明边缘和深浅背景叠加均无可见裁切或灰色残影。仍有细微毛发色调及支撑节奏差异；不宣称上半身逐像素完全不变或精确还原真实小狗的所有关节。

P20版本0.9.11.0已启动，单一进程PID10256响应正常，四个关键运行时模块均来自随包的.NET10.0.12私有运行时；重复启动退出0并唤醒当前实例。175个发布素材均匹配源码，159个无关运行素材与P19哈希一致；P6–P19旧压缩包哈希不变。

团团的非位置设置、Clingy性格、好感43、62条日记、摸头7、喂食10、玩耍23、小窝13、专注完成1和最近亲密互动时间均保留。启动冻结快照的饱腹与精力值也未改变，实时连续性检查确认原有记录和计数完整。

发布位置为 `artifacts/publish/p20-win-x64/`；[压缩包](../artifacts/DesktopPet-P20-win-x64.zip)为88,739,720字节、668项。原始照片和行走视频仍保留在源码目录作为开发参考。

SHA256：`AEE5FDE8D6174F20869AC78AA9E528894F87C32F7D9AA511313EF75B387B86DE`。
