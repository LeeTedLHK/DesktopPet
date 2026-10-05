# P18 / 0.9.9 左向行走回退修复

日期：2026-10-05（北京时间）。用户反馈上一版左向行走比修改前更奇怪，尤其后腿动作不自然。

## 原因与处理

对照P16与P17逐帧素材，P17左向后腿有多次向后高抬、勾起及落下的姿势，帧间相位变化不连续。原有检查确认了帧数、透明度与播放，但不能证明犬类动作自然；上一轮的视觉验收不足。

本轮以原照片、三视图和P16左向素材为参考，使用内置ImageGen生成了一份小步幅候选。该候选的透明间隔出现残留，连续后腿相位仍不理想，因此未装入应用。提示词、引用与弃用理由见[候选记录](assets/2026-10-05-p18-left-walk-review.json)。弃用图位于 `artifacts/imagegen/p18/WalkLeft-rejected-atlas.png`。

实际交付撤回P17的左向视觉退步，恢复P16原来的八张左向帧及10fps循环（默认0.8秒）。恢复后的源文件、构建输出与发布文件均逐字节匹配P16。008–011四张额外帧从当前素材与派生输出移入 `artifacts/verification/p18-quarantined-frames/`，保留备查；完整P17素材与发布包也保留。

右向继续使用P17的12帧12fps；奔跑、起步、刹停、转身、叼回、嘴部定位及其他动作不变。P17参考小球、软窝和默认约1.6秒开心动作，以及P16默认约3秒的伸懒腰/打哈欠保留。

这是恢复修改前步态的回退修复。旧素材仍存在前腿个别相邻姿势近似、部分中间相位变化不均等限制，不将此次交付描述为重新完成了自然犬类步态。

## 素材与验证

最终左向素材：`src/DesktopPet/Assets/Pet/WalkLeft/`。原版来源：`artifacts/imagegen/p17/previous-runtime/WalkLeft/`，并与P16发布目录独立比对。当前共29个动作目录、176张动作帧，运行包179份图片/定位数据。源目录另外保留开发参考小球照片。

[左向160像素预览](../artifacts/imagegen/p18/WalkLeft-preview-160.gif)，[恢复后逐帧图](../artifacts/imagegen/p18/WalkLeft-restored-contact.png)。预览按默认10fps播放，采用恢复帧，没有插值、腿部变形或镜像。

源快照比对只允许八张左向PNG替换及四张额外左向PNG移出；172份其他源图片/定位文件保持原字节。P17发布对照只允许同样变化，171份其他运行素材保持原字节。原始三张参考图保留。

| 检查 | 结果 | 证据（artifacts/verification/） |
|---|---:|---|
| 旧实现的左向帧数/节奏预期 | 实际失败 | p18-walking-core-red.log、p18-walking-native-red.log |
| 修复后原生视觉/播放专项 | 21通过 | p18-walking-native-green.log |
| 完整OfflineChecks | 587通过 | p18-offline.log |
| 完整WindowsChecks | 466通过，包含专项 | p18-windows.log |
| 脚本解析 | 通过 | p18-scripts-check.json |
| 发布、素材/ZIP/运行时/启动/第二实例 | 退出0 | p18-publish.log、p18-release-check.log |
| 数据保留、旧ZIP、程序集与版本 | 退出0 | p18-delivery.log |
| 源快照及恢复帧比对 | 通过 | p18-source-diff.json |

本轮完整回归1053项。实际WPF检查加载八张左向帧，在起步过渡后进入左向循环并推进帧，未添加合成上下晃动；同时检查右向、奔跑、转身、追逐/叼回、菜单暂停、皮肤完整导出与重新导入176帧、隐藏和退出。测试使用临时设置与数据。独立只读复核见 `p18-review.md`，明确区分播放正确与视觉自然。

独立SkinChecks、JournalChecks及xUnit未重复运行：此轮完整原生回归覆盖实际默认皮肤导出/导入，日记及皮肤服务实现没有变化，先前P17专项记录保留。xUnit本地依赖包未恢复，不计入本轮通过数量。

交付检查脚本的初版替换ZIP项数时误改了P7预期哈希中的相同数字片段，产生误报。实际P7文件哈希没有变化；修正为仅替换项数条件后，P6–P17旧包哈希检查全部通过。初版日志保留于 `p18-delivery-initial-check.log`。

## 发布与数据保留

发布包：`artifacts/DesktopPet-P18-win-x64.zip`，89,036,589字节、672项，EXE版本0.9.9.0，附带.NET Core/Windows Desktop10.0.12。

SHA256：`638E8E132ED5DF343C29CB28821C761EA105B6E0B709D6D64B4D55CB0ACF75AA`。

启动PID24488响应正常，运行时模块来自P18目录的私有runtime。第二实例退出0并通知原实例显示。发布程序集与Release构建哈希相同，P6–P17旧压缩包保持原哈希。

停止P17前重新保存当时最新的真实设置与养成数据；启动P18后冻结快照比较，避免使用先前过时的59条记录基准。团团的黏人性格、亲密度42、61条日记及整个Journal对象保留；摸头6、喂食10、玩耍23、回窝13、完成专注1次保留。除正常位置字段外设置一致，最后亲密奖励时间、饱腹与精力在启动快照中一致，无额外夜间回窝记录。没有用测试数据覆盖用户文件或人为改数值。

最新连续性与进程核对见 `p18-current-journal-preservation.json`、`p18-final-state.json`；其他证据为 `p18-source-clean-before.zip`、`p18-user-before-restart-*.json`、`p18-user-migration.json`、`p18-release-metadata.json`。当前运行目录：`artifacts/publish/p18-win-x64/`。
