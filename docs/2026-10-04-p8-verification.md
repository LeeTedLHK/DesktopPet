# P8 设置、皮肤与数据备份验收

## 请求与本轮范围

用户要求检查需要完善的地方，列出后直接处理。本轮列出并实现五项：自定义动画速度保留；完整皮肤导入/导出；玩法入口状态同步；设置/养成/日记备份和数据目录入口；操作帮助与自动版本显示。沿用P7已选素材及完整叼回流程，独立发布0.8.0。

## 行为与实现

设置界面原先把任何保存的合法速度映射到0.75/1/1.5三档，修改其他选项会覆盖原值。现在使用0.25–3.0的数值选项，并将任意合法既存值加入选项；0.63、1.125等非预设值也准确保留。界面输入不修改原设置快照。

SkinCatalog允许根部可选mouth-anchors.json，保持其原始字节，上限128KiB，区别于64KiB的skin.json名字元数据。定位文件不消耗5000帧和64Mi-pixels预算；其具体动作内容仍由动画管理器逐项校验并回退。支持单层包装ZIP，包括包装目录恰好叫Idle或skin.json的情况。

“导出当前皮肤ZIP”只打包已知动作PNG、皮肤名字和可选定位文件，省略默认照片/参考图。导出执行同样的帧、PNG和体积检查，通过临时文件和同目录原子替换写入。锁定或无效目标不破坏已有文件；拒绝源目录内的目标、非ZIP后缀和链接路径，普通Windows扩展驱动器/UNC前缀先归一化再检查。

设置与托盘的小球、飞盘和挑战入口随玩具开关同步，小窝入口随小窝开关同步。隐藏或全屏临时隐藏时，托盘不可启动需要宠物显示的这些动作。原有取消、快捷键、互动和养成逻辑不变。

新增“数据与帮助”设置页：“备份设置与日记”从当前内存快照生成三项ZIP（settings.json、profile.json、backup.json），Profile包含养成、完整日记、累计次数和互动时间。备份不改写使用中的配置，写入失败保留旧备份。照片、日志、导入皮肤和正在计时的专注会话不包含在此备份，界面明确说明；皮肤由独立导出保存。此轮不提供自动恢复覆盖数据。

帮助说明快捷键、找回宠物、点击/拖动、叼回、散步、暂停、隐藏和退出；全屏取消玩法准确限定为开启全屏自动隐藏时。版本从程序集读取。帮助可纵向滚动，长文本换行，底部保存状态与完成按钮分列。

## 已执行的专项证据

- `p8-data-red.log`：新备份接口加入前编译失败；`p8-data-green.log`：299项Core检查通过，包括10项新增备份检查。
- `p8-settings-red.log`：真实复现0.25→0.75、0.63→0.75、1.125→1、2.75/3→1.5；同时复现关闭玩具后挑战仍可用。
- `p8-tray-red.log`：关闭玩具后托盘小球/飞盘仍启用。
- `p8-skins-red.log`：旧29项通过，新定位导入和导出检查失败。
- `p8-skin-path-red.log`：扩展路径源碰撞未提前拒绝；`p8-skin-suffix-red.log`：非ZIP目标被错误覆写。修复后 `p8-skin-path-green.log` 3/3通过。
- `p8-focused-windows-green.log`：35项P8原生检查通过，包含24项设置、6项托盘和5项真实皮肤往返。

真实皮肤往返实际导出默认172帧、保留定位JSON、重新导入、WPF完整解码，并检查CarryRight第0帧使用对应嘴部锚点；之后恢复默认皮肤。只操作检查自己的目录和窗口。

检查中遇到的两项旧预期问题也已修正：P4原生检查写死四个设置页，改为核对原有页标题；锁定ZIP的Windows替换可能抛IOException或UnauthorizedAccessException，故障检查接受两者，同时仍核对目标字节与临时文件。锁定夹具使用读共享，允许扫描器读取，同时禁止替换。失败日志均保留，未重命名成成功记录。

帮助页额外对测试自己的内容施加1.5倍布局缩放，在当前150%显示缩放下渲染。Root及UI代理均查看了顶部/底部PNG：页签分两行，按钮与长状态文字可读，末尾版本和退出说明可到达，底部无重叠。预览：[顶部](../artifacts/verification/p8-windows/p8-help-top-150pct.png)、[底部](../artifacts/verification/p8-windows/p8-help-bottom-150pct.png)。

## 保留与验证边界

无Git仓库，源码快照为 `artifacts/verification/p8-source-clean-before.zip`。先保存真实数据初始快照，随后再次保存最新持久化settings/profile，再核实并停止P7 PID2632以避免原生快捷键冲突。最新数据快照为 `p8-user-before-restart-*.json`：团团、黏人，亲密度27，摸头2、喂食3、玩耍9、回窝0、专注1。验收不用真实用户配置。

所有P7动作素材和参考图保持原样；旧发布目录和ZIP保留。新功能不依赖网络和新NuGet包。

系统文件选择器和Explorer入口没有自动点击；实际ZIP内容/故障及WPF事件接线已检查，ApplicationService接线另外静态复核。扩展驱动器路径已实测，UNC前缀转换没有可用网络共享实测。当前仅一块150%显示器，混合DPI多屏、真实游戏全屏和Windows重启仍不在本轮实机验收范围。

## 最终回归与独立复核

最终源码执行 `./scripts/test-offline.ps1 -WindowsIntegration`，退出码0。[最终日志](../artifacts/verification/p8-all-checks-final.log)确认全部672项通过：Core 299、皮肤42、日记83、原生Windows248。此前35项P8专项及3项路径专项包含在最终总数中，不重复计数。

[独立只读复核](../artifacts/verification/p8-review.md)无遗留Critical、Important或Minor问题。该复核结束后，root完成上述最终全量回归及以下发布、用户数据验证；发布结果不归为复核代理执行。

## 发布与实际启动

`./scripts/publish.ps1`和`./scripts/verify-release.ps1 -StartCheck`均退出0，证据为[打包日志](../artifacts/verification/p8-publish.log)、[发布检查](../artifacts/verification/p8-release-check.log)及[最终交付检查](../artifacts/verification/p8-delivery-check.log)。

- 程序：`artifacts/publish/p8-win-x64/DesktopPet.exe`，文件版本0.8.0.0；发布的DesktopPet.dll和DesktopPet.Core.dll与Release构建哈希一致。
- 完整包：`artifacts/DesktopPet-P8-win-x64.zip`，668条目，88,662,529字节。
- ZIP SHA256：`B0CE9476771B0C9761750F77D015C433E8FEAB413C5F115D33F05F8E4BBA8989`。
- 发布175个素材文件均匹配当前源码及P7发布文件哈希，包含172个动作PNG、两张参考图片和嘴部定位JSON；无旧帧残留。
- 加载包内私有.NET和WPF 10.0.12；启动后响应正常。第二次启动找回已有宠物并以0退出；验收时P8 PID10124保持运行。
- P6和P7旧ZIP哈希均与原记录一致，发布目录保留。

机器可读记录：[启动](../artifacts/verification/p8-win-x64-startup.json)、[发布元数据](../artifacts/verification/p8-release-metadata.json)。

## 真实用户数据保留

P8实际启动后，读取真实settings/profile并与最新`p8-user-before-restart-*.json`比较。除允许宠物移动的LastPetX/LastPetY外，所有配置字段一致；团团、Clingy性格、亲密度27和LastBondUtc保留。整个Journal序列化比较一致，19条条目、到访日、成就、动作冷却时间及摸头2/喂食3/玩耍9/回窝0/专注1均保留。

饱腹和体力与停机快照的绝对差分别为0.0450923和0.0901845，属于启动后正常计时变化。验收未覆盖或恢复真实用户文件，启动后的实际JSON另存为`p8-user-after-settings.json`及`p8-user-after-profile.json`。证据：[数据保留报告](../artifacts/verification/p8-user-migration.json)。
