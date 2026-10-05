# P10 喂食食盆修复

用户指出“小狗喂食没有狗盆”。检查确认Feed只启动Feeding动作，食盆仅出现在小窝装饰中；HomeEnabled关闭时没有独立喂食道具。沿用用户此前直接处理、不等待确认的授权，本轮为现有喂食流程的局部修复，版本0.9.1。

## 行为

喂食开始时，在小狗前爪前方显示带狗粮的浅绿色陶瓷食盆，奶油色盆沿、柔和阴影和爪印。食盆随宠物缩放、屏幕与活动范围调整，并遵循置顶偏好；作为宠物的附属窗口保持前景位置，同时透过鼠标操作、不抢焦点。

食盆按实际Feeding状态显示，关闭小窝或玩具仍可喂食。完成喂食、摸头/拖动打断、隐藏、全屏自动隐藏、关闭动画结束当前互动或退出时都会收起。禁用动画时，仍显示食盆，沿用静态动作看门狗结束互动。原有8秒喂食冷却与养成/日记奖励逻辑保持。

实现复用原生WPF道具绘制及屏幕定位，未修改狗的PNG、两张参考图片、嘴部定位JSON或Core数据模型。Feeding没有自定义嘴部定位时使用已观察默认喂食图片左侧头部位置；自定义有效定位优先。

## 专项检查

- `p10-feeding-red.log`复现缺少食盆、默认喂食错误使用追逐嘴部回退位置，以及静态喂食和边缘区域均缺食盆。
- `p10-feeding-green.log`记录实现中首次关联Owner过早的WPF初始化异常，原始失败日志保留。关联改为宠物显示、开始喂食后执行。
- `p10-feeding-green-after-owner.log`退出0，20项喂食专项通过；包括实际WS_EX_TRANSPARENT/NOACTIVATE、Owner、缩放/置顶、冷却、结束/中断、静态/动态动作、边缘区域和喂食中退出的窗口清理。

实际查看自有WPF小狗与食盆的合成画面：[普通大小](../artifacts/verification/p10-windows/p10-feeding-scene.png)、[放大](../artifacts/verification/p10-windows/p10-feeding-large-scene.png)。未截取桌面或其他应用窗口。

## 保留与交付验证

变更前源码快照`artifacts/verification/p10-source-clean-before.zip`，14,954,238字节；真实数据初始快照`p10-user-before-settings.json`及`p10-user-before-profile.json`。停止旧版P9进程19376前另存最新`p10-user-before-restart-settings.json`及`p10-user-before-restart-profile.json`，最终数据比较使用这组最新快照。

- `p10-windows-final.log`退出0，终端明确记录288项完整Windows集成检查通过，包含此前268项和本轮20项。未重新执行本轮未修改的Core、皮肤及日记独立检查项目。
- 独立只读审查见`artifacts/verification/p10-review.md`，无Critical、Important或Minor问题；审查者未运行构建或测试，结果来自协调者执行的日志。
- `p10-publish.log`退出0，发布0.9.1.0及独立P10目录。`p10-release-check.log`核对175个素材与源码哈希一致，无多余图片；ZIP包含668项。启动进程16944响应正常，加载旁置.NET Core/Desktop 10.0.12的四个预期运行时模块，重复启动找回原实例并退出0。
- `p10-delivery-check.log`退出0。发布DLL与Release构建一致，P6/P7/P8/P9压缩包SHA256保持；175个P7素材逐一保留，没有修改狗的图片。
- `p10-user-migration.json`记录新版启动后的即时比较。除窗口坐标允许由定位更新外，所有设置相同；团团、Clingy、亲密度32及最近亲密奖励时间保留，饱腹与精力差值均0。完整日记序列相同：摸头4、喂食7、玩耍17、回窝3、完成专注1，37条日记。比较后的真实文件副本保存在`p10-user-after-settings.json`及`p10-user-after-profile.json`。

交付文件`artifacts/DesktopPet-P10-win-x64.zip`，88,666,330字节，SHA256：`82DC974758DD74BAAF32F3D123718DF8198FD0F7905AE6D4DA7C6F457C186F6A`。启动入口`artifacts/publish/p10-win-x64/DesktopPet.exe`，已保留运行。

本轮验证使用自有WPF窗口及其合成图，没有自动操作Explorer或系统对话框。缩放与小活动范围已验证；没有新增混合DPI或物理多显示器实机验证。自定义定位优先和缺动作回退经过源码审查，专项使用默认素材。本轮新增的是现有喂食的道具，未新生成小狗动作或修改养成规则。
