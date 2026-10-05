# P13 / 0.9.4 验收记录

验收日期：2026-10-05（北京时间）。已发布并启动 `artifacts/publish/p13-win-x64/DesktopPet.exe`，启动检查及数据校验时 PID 7212，进程响应正常。

## 本轮完成

- 根据 `pet.jpg` 与 `比熊犬三视图参考图.png` 重新生成六帧喂食动作：低头、吃食、咀嚼、抬头、舔嘴。食盆水平位置固定，咀嚼时不会跟着嘴部晃动。
- 球与飞盘按小狗实际显示宽度缩放；碰撞、追逐、嘴部叼取和落地使用实例尺寸，矢量画面也同步缩放。
- “玩具与日记”增加小球、飞盘、食盆三个独立大小滑块，范围60%–150%。默认100%保留现有小巧食盆；加载和修改其他设置时保留已保存的精确自定义值。
- “外观与互动”增加默认关闭的省电模式：待机最多4fps、睡眠最多2fps；原速度更慢时保持更慢。互动、行走、奔跑、玩法运动和专注/提醒计时保持原来的速度；隐藏、禁用动画、皮肤替换及退出沿用原有计时器生命周期。
- 修复DIP尺寸换算中的浮点取整边界：例如29/1.75再乘1.75可能略大于29，避免原生窗口因此多出一像素。

## 实际执行的检查

所有共享 .NET 构建和检查由主代理执行。下列最终命令均退出0，相关日志保存在 `artifacts/verification/`。

| 检查 | 结果 | 日志 |
|---|---:|---|
| `dotnet run --project tests/DesktopPet.OfflineChecks -c Release --no-restore` | 428通过 | `p13-offline.log` |
| `dotnet run --project tests/DesktopPet.SkinChecks -c Release --no-restore` | 42/42通过 | `p13-skins.log` |
| `dotnet run --project tests/DesktopPet.JournalChecks -c Release --no-restore` | 83/83通过 | `p13-journal.log` |
| `dotnet run --project tests/DesktopPet.WindowsChecks -c Release --no-restore -- artifacts/verification/p13-focused --p13-only` | 41通过 | `p13-windows-green.log` |
| `dotnet run --project tests/DesktopPet.WindowsChecks -c Release --no-restore -- artifacts/verification/p13-windows` | 356通过 | `p13-windows-final.log` |
| `scripts/publish.ps1` | 发布成功 | `p13-publish.log` |
| `scripts/verify-release.ps1 -StartCheck` | 素材、ZIP、运行时、启动及单实例通过 | `p13-release-check.log` |
| `artifacts/verification/p13-delivery-check.ps1` | 数据保留、旧发布包哈希及版本通过 | `p13-delivery.log` |

P13 Core 121项专项包含在428项离线检查中；41项Windows专项包含在356项完整检查中，不重复相加。Core和最初的原生检查先在旧实现上观察到语义失败，分别记录于 `p13-core-red.log`、`p13-windows-red.log`。

DPI边界回归先将实际原生尺寸函数恢复为原来的向上取整，观察175%边界失败且真实显示器窗口尺寸检查通过（`p13-dpi-red.log`），再恢复修复并通过专项及完整回归。当前显示器为150%；175%的验证针对实际尺寸函数和算术边界，没有声称在175%显示器上运行过。

完整追逐检查覆盖两种玩具、小狗80%/200%大小、左右两个方向，确认实际窗口中的叼取、落地、边界与完成状态。为了稳定验证动作流程，专项直接推进游戏时间；实时计时和既有玩法由完整回归继续覆盖。Core另覆盖小范围及负坐标范围。

独立审查：[p13-review.md](../artifacts/verification/p13-review.md)，无未解决的Critical或Important问题。审查发现的一像素边界问题已经修复。

## 素材与画面

使用内置 `image_gen`，以两张原始参考图生成透明素材；无需外部API配置。完整提示词和生成方式见[生成记录](assets/2026-10-05-p13-feeding-prompt.txt)。原始生成图保留在 `artifacts/imagegen/p13/feeding-atlas.png`。

`scripts/prepare-p13-feeding.py` 仅整体等比缩放图集、切分等大格子、平移完整帧；未重画、改色或重新处理生成透明度。交付六张256×256 RGBA图片，脚底统一在y228，附透明边缘、边界及地面位置检查记录 `artifacts/imagegen/p13/validation.json`。

已查看六帧联系图和实际小狗/食盆合成图，确认低头吃食、抬头舔嘴和食盆位置。预览：

- [六帧素材图](../artifacts/imagegen/p13/feeding-contact.png)
- [实际喂食动画](../artifacts/verification/p13-windows/p13-feeding-scene.gif)
- [大小设置页](../artifacts/verification/p13-windows/p13-settings-props.png)
- [省电设置页](../artifacts/verification/p13-windows/p13-settings-power.png)

`p13-source-diff.json` 对照改动前ZIP确认原文件无缺失；两张参考图和另外168个素材文件均未修改。仅六帧Feeding图片和对应嘴部定位元数据更新；其他动作的定位数组也保持一致。

## 发布与实际用户数据

ZIP：`artifacts/DesktopPet-P13-win-x64.zip`，88,746,933字节、668项；内置.NET Core/Windows Desktop 10.0.12。发布目录中175个素材文件与源码哈希一致，无过期图片；发布程序集与Release构建匹配。

SHA256：`9E95E81508ED3256D97EBE5313F9B78383BE61692A6F20D6EBAD04A64D058780`。

启动后 `hostfxr.dll`、`hostpolicy.dll`、`coreclr.dll`、`wpfgfx_cor3.dll` 均从发布包的私有运行时加载；第二次启动通知已有实例显示，并正常退出0。P6–P12所有旧ZIP哈希保持不变。

停止旧版之前保存实际设置和养成数据快照；所有测试使用临时数据目录，没有将测试数据写回用户目录。首个启动快照的48条日记原样保留；随后实际回窝事件新增一条，当前49条中的原48条逐项一致。新增事件发生在启动后15秒内，校验仅允许原有自动行为/夜间作息/小窝设置产生这条Home事件及对应计数、动作时间变化。

原名字“团团”、黏人性格、亲密度36、最后亲密奖励时间、喂食9次、摸头4次、玩耍17次、专注完成1次均保留。原Home计数10按实际回窝变为11。饱腹/精力的变化经过原有每小时照料速率核验；没有重置或人工覆盖实际数据。

旧设置（除正常位置保存字段）保持一致，新设置为小球1、飞盘1、食盆1、省电false。详细证据：`p13-user-migration.json`、`p13-current-journal-preservation.json`、`p13-release-metadata.json`。

## 验证范围

新增原生流程使用默认方形皮肤和完整活动范围；小范围/负坐标由Core验证。自定义纵向皮肤的缩放及地面上限按源码检查，未新增原生纵向皮肤流程。没有独立测量功耗，省电选项验证的是待机/睡眠帧率降低。其余审查覆盖限制记录在独立审查文档中。
