# P5 步行动画

WalkLeft 和 WalkRight 分别使用内置 ImageGen，根据本目录的 `pet.jpg` 与 `比熊犬三视图参考图.png` 生成。两个方向分别生成，不使用镜像或程序合成动作。

每个方向为 8 帧、256 × 256 RGBA。精灵图按 4 列 × 2 行读取，逐行排序。Pillow 仅按等格切分并统一缩小完整画格，保留 ImageGen 生成的 alpha；没有背景抠图、姿势变形或补间帧。预览图和 GIF 的底色只用于检查，运行 PNG 保持透明。

原始生成图、接触表、循环 GIF 和逐帧 alpha 检查记录位于 `artifacts/imagegen/p5`。提示词与生成来源见 `docs/assets/2026-10-03-p5-imagegen-prompts.json`。可用 `scripts/prepare-p5-assets.py` 重建暂存帧；`--install` 在旧运行目录归档后安装，并拒绝混合旧、新帧。

替换前的两个方向各 96 帧完整归档到 `artifacts/imagegen/p5/previous-WalkLeft` 和 `previous-WalkRight`。通用 Walk、其余动作及两张原始参考图保持原样。

两个方向使用固定的整格比例匹配体型：左向整格缩至 220 × 220 后置于 256 × 256 透明画布，右向整格缩至 256 × 256。随后只对完整帧作整数垂直平移，使可见接地脚掌下边界统一为 y=228；不单独调整任何身体部位。剩余头部高度变化为左向 5 像素、右向 3 像素。

生成帧有轻微毛发、嘴部与姿态差异。8 帧循环是新的美术素材，不能视为具有严格骨骼连续性的手工动画。
