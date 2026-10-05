# P9 交互状态与保存恢复验收

用户要求继续检查和完善，并沿用上一轮“列出后直接处理、不等待确认”的授权。本轮针对现有功能完成四项修正，独立发布0.9.0；不增加素材、依赖或数据恢复覆盖流程。

## 检查发现与处理

| 问题 | 根因 | 本轮行为 |
| --- | --- | --- |
| 隐藏时设置页玩法按钮仍可用，点击无反应 | 设置页只按玩具/小窝开关设置按钮；ApplicationService仅同步托盘可见性 | 投球、飞盘、接球挑战、回窝及散步按实际显示状态禁用；解释隐藏或全屏原因；手动隐藏时可从设置页显示宠物；全屏期间等待退出全屏 |
| 保存错误提示不恢复，养成失败等一分钟才重试 | ProfileSaved只向界面发送失败；SaveNow先发送设置成功再写Profile；保存间隔不区分成功/失败 | 两文件保存完后发送一次合并结果；养成恢复会清除失败，但不能掩盖设置失败；设置页重开使用最近保存结果；养成失败每10秒重试，成功后恢复一分钟间隔 |
| 较长名字截断emoji或组合字符 | 名字使用UTF-16的20个编码单元截取，TextBox也按20个编码单元限制 | 使用20个显示字符（Unicode字素簇）限制，保留组合emoji、旗帜及重音；失去焦点后显示最终保存的名字 |
| 错误提示要求查看日志却缺少直接入口 | 帮助页仅有数据目录按钮 | “数据与帮助”增加打开日志目录，保存失败提示指向此页 |

隐藏状态不阻止设置修改、备份、导出皮肤或专注计时。显示按钮不重置位置及鼠标穿透偏好；全屏自动隐藏仍遵守现有设置。皮肤与写真功能沿用上一版行为。

## 实际专项检查

- `p9-name-focused-red.log`：旧截取方式导致六个边界/JSON检查失败。`p9-name-green.log`：8项通过，包括普通emoji、组合emoji、旗帜、组合重音、JSON保存读取及原有空名处理。
- `p9-windows-red.log`：旧代码复现隐藏动作可用、缺少原因/显示入口、提前发送保存成功、恢复后提示不更新，以及失败后未及时重试。
- `p9-ui-name-red.log`：保存逻辑修正后，界面隐藏状态和名字编辑结束仍复现失败。
- `p9-save-green.log`：11项保存专项通过。
- `p9-windows-green.log`：20项原生专项通过，实际调用ApplicationService状态同步并检查五个按钮；真实锁定临时目录的profile/settings文件验证合并结果和原文件保留；按9/11/59/72秒时刻验证重试及恢复。

检查只操作自有WPF窗口、临时目录和验收目录，不修改真实用户配置。没有自动打开真实Explorer窗口；日志入口按事件、处理器和异常路径静态复核。

首次尝试全量Core时，`p9-name-red.log`在P8备份的正常ZIP覆盖步骤遇到UnauthorizedAccessException，尚未执行名字检查；这份日志保留，不能作为名字RED证据。P8锁定备份检查原本已接受IOException及UnauthorizedAccessException，没有更改该测试或产品代码。最终全量验收须重新覆盖该行为。

## 保留与最终验收

项目无Git仓库。变更前完整源码快照：`artifacts/verification/p9-source-clean-before.zip`；初始真实用户数据：`p9-user-before-settings.json`及`p9-user-before-profile.json`。沿用P8的175个素材文件并保留旧版发布目录和ZIP。

完整回归`p9-all-checks-final.log`通过Core307、皮肤42、日记83；Windows部分在旧P6飞盘奔跑动画检查失败，不能作为全量通过证据。后续补充实际动画状态诊断并完整复跑Windows项目，最终结果见下。

[独立只读复核](../artifacts/verification/p9-review.md)没有Critical或Important发现；一项Minor提示歧义已修正：全屏提示不再承诺退出全屏自动开始玩耍，以兼容手动隐藏的既有意愿。发布和数据验证记录如下。

## 最终回归结果

完整Windows项目在最终提示文字修正后重新执行，`p9-windows-final-diagnostic.log`退出0，268项通过。原P6奔跑检查保留所有断言，仅补充失败时的实际动作、帧数、帧率及状态信息；四组小球/飞盘左右追逐均确认RunLeft/RunRight、8帧、12fps，动画继续切帧。此前一次飞盘失败在这次完整复跑未再复现，其原因尚未确定，失败日志仍保留。

本轮最终通过的完整项目合计700项：Core307、皮肤42、日记83、Windows268。Core/皮肤/日记代码在其成功执行后未再改变，因此界面提示修正后只重跑全部Windows检查。机器可读汇总为[p9-final-regression.json](../artifacts/verification/p9-final-regression.json)，明确区分Core/皮肤/日记日志及最终Windows日志；不将包含早先Windows失败的`p9-all-checks-final.log`描述为整条命令成功。

最终0.9.0的WPF放大渲染已实际查看：[全屏玩法页](../artifacts/verification/p9-windows/p9-fullscreen-play-150pct.png)、[数据与帮助页](../artifacts/verification/p9-windows/p9-data-help-150pct.png)。当前150%显示缩放下再对内容施加1.5倍布局缩放，提示换行、按钮及日志入口清晰，底部无重叠；内容可滚动。

独立复核已复查并将Minor标记为解决，没有遗留Critical、Important或Minor发现。完整回归、打包和实际数据比较由root执行。

## 发布与实际启动

发布脚本和`verify-release.ps1 -StartCheck`均退出0，[交付检查](../artifacts/verification/p9-delivery-check.log)也退出0。

- 程序：`artifacts/publish/p9-win-x64/DesktopPet.exe`，文件版本0.9.0.0；发布的两个程序集与当前Release构建哈希一致。
- 完整包：`artifacts/DesktopPet-P9-win-x64.zip`，668条目，88,663,709字节。
- SHA256：`E68DA9DEAAFBD08F4C3D01F520BDFD4D66631106C5BE5EB506A2C40C615CDE42`。
- 175个发布素材文件匹配当前源码及原P7文件；无旧帧残留。P6/P7/P8旧ZIP的哈希分别匹配原记录，旧发布目录保留。
- 加载包内私有.NET/WPF 10.0.12；第二次启动找回已有宠物并以0退出。验收时PID19376响应正常，保留P9运行。

证据：[打包](../artifacts/verification/p9-publish.log)、[发布检查](../artifacts/verification/p9-release-check.log)、[启动报告](../artifacts/verification/p9-win-x64-startup.json)、[发布元数据](../artifacts/verification/p9-release-metadata.json)。

## 真实用户数据保留

原P8 PID10124的实际程序路径先经核实，最新持久化settings/profile另存为`p9-user-before-restart-*.json`后才停止进程，为原生快捷键检查留出环境；记录为`p9-old-process-stop.json`。

P9实际启动后，与该最新快照比较：除允许宠物自然移动的LastPetX/LastPetY外，配置字段完全一致。团团、Clingy性格、亲密度30、LastBondUtc保留；整个Journal序列化比较一致，包括36条日记、到访日、成就、动作冷却和摸头4/喂食6/玩耍17/回窝3/专注1。饱腹和体力差值均为0。

验收没有恢复、覆盖或手动修改真实用户配置；实际启动后的JSON另存为`p9-user-after-settings.json`和`p9-user-after-profile.json`。详见[数据保留报告](../artifacts/verification/p9-user-migration.json)。

## 验证边界

文件选择器和Explorer没有自动点击；其事件、实际处理器及错误状态按源码复核。当前实机仅一块150%显示器，未增加混合DPI多屏、真实游戏全屏或Windows重启实测。此前正常备份覆盖和旧P6飞盘检查各有一次未再次复现的失败，日志保留；本轮未凭猜测更改相应产品代码或放宽断言。
