# DesktopPet 0.5.0 验收记录

2026-10-03，Windows x64，.NET SDK10.0.401。按用户要求重做小球、飞盘和左右行走，并添加现有互动流程中的三个新玩法。修改前源码副本：`artifacts/verification/p5-source-clean-before.zip`，P4发布保留。

## 交付变化

- 小球：珊瑚色橡胶、奶油拼片、曲线接缝与细纹；飞盘：青绿色软胶、厚边、凹槽与爪印。`ToyVisualFactory`以32DIP原生矢量绘制，保持原有运动尺寸。原尺寸、放大及96/144/192DPI渲染见`artifacts/verification/p5-toys/`，根代理已查看预览。
- 左右步态：内置ImageGen参考`pet.jpg`和`比熊犬三视图参考图.png`，分别生成8帧循环。每帧256×256 RGBA，保留生成的透明毛边，统一每个方向的整体缩放，并通过整帧平移对齐接触脚底y=228。头部仍有左5px/右3px自然起伏和轻微毛发变化。两方向原96帧均备份；新运行目录仅含各8帧，移除额外WPF上下晃动。
- 跟随鼠标散步：30秒单次会话；当前显示器与活动范围内移动，靠近后停下来等待，保持鼠标与宠物轮廓之间的距离。单调计时控制超时。
- 三回合接球挑战：真实运动模拟与追球，三次接住后结束，回合之间1.2秒休息；显示进度，提供结束入口。
- 宠物拍立得：保存640×760照片卡与原尺寸透明头像，名字/日期绘入照片卡。读取当前宠物位图，不截取桌面；唯一文件名和CreateNew保留旧照片。默认保存到系统图片目录/DesktopPet，可从设置打开。

新玩法接入宠物右键、托盘和“玩具与日记”页。自动行为暂停时仍可主动玩。菜单、隐藏、全屏、拖动、设置/屏幕变化和退出会取消移动及待抛球计时。

## 证据

完整检查：Core113、Skin28、Journal83、实际WPF110，共334。`p5-all-checks.log`记录完整离线脚本；`p5-final-windows.log`记录最终WPF检查。xUnit原项目本轮未运行，以上是无需外部NuGet包的检查项目。

新增21项WPF检查覆盖：暂停时散步、隐藏和全屏取消、轮廓外停留、近距离等待、活动边界、单调超时、关闭玩具、三次实际模拟接球且不继续第四回合、菜单/托盘取消、关闭动画时回合间取消恢复状态、PNG解码/透明度、重拍保留旧文件、名字不作为路径、实际新8帧加载、设置开始/结束入口。

有效失败→通过记录：`p5-play-red.log`→`p5-play-green-initial.log`；`p5-menu-red.log`→`p5-menu-green.log`；`p5-cancel-red.log`→`p5-cancel-green.log`。后两项分别修复菜单关闭事件前无法开启挑战，以及关闭动画时中断回合间开心表情会卡住状态的问题。

独立只读源码审查：`artifacts/verification/p5-review.md`，无Critical/Important或需发布前处理的Minor。该审查没有代替实际测试。

视觉证据：左右contact图、8帧GIF和alpha/脚底验证在`artifacts/imagegen/p5/`，球/飞盘原尺寸预览在`p5-toys/`。新界面、照片卡和透明头像在`artifacts/verification/p5-windows/`。生成提示词和来源：`docs/assets/2026-10-03-p5-imagegen-prompts.json`；拆分脚本：`scripts/prepare-p5-assets.py`。

## 发布

入口：`artifacts/publish/p5-win-x64/DesktopPet.exe`；完整ZIP：`artifacts/DesktopPet-P5-win-x64.zip`。需保留整个目录的Assets、runtime和DLL。

发布核验：`p5-release-check.log`、`p5-win-x64-startup.json`与`p5-release-metadata.json`。检查源码/发布素材哈希、旧方向帧没有混入、旁置官方运行时、ZIP、实际EXE启动和重复启动找回。用户设置/养成状态的发布前副本为`p5-user-before-settings.json`、`p5-user-before-profile.json`；迁移结果写入`p5-user-migration.json`。

最终包0.5.0.0，ZIP为100,011,940字节。发布素材与源码一致，两个方向均仅8帧，实际EXE加载旁置.NET10.0.12并保持运行。原配置、亲密度及互动时间、完整日记均保留；饱腹/精力变化未超出运行时间范围。334项检查通过。

## 边界

当前一块150%显示器，真实混合DPI多屏、物理拔插及Windows重新登录自启仍未实测。步态已检查逐帧图、透明度、脚底和循环接缝；生成式逐帧素材仍有轻微外观变化。照片卡放大现有动画帧，源帧清晰度限制仍存在。新增玩法无需联网或安装额外运行依赖。
