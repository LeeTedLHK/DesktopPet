# P2 新动作素材

使用内置 ImageGen，根据本目录 pet.jpg（外形、红项圈与黄色铃铛）和 比熊犬三视图参考图.png（体型、卷毛与比例）生成。

| 目录 | 帧数 | 功能 |
|---|---:|---|
| Petting | 6 | 摸头反应 |
| Feeding | 6 | 低头吃饭、舔嘴与吃饱 |
| Stretching | 6 | 站立、前伏伸展、恢复 |
| Yawning | 6 | 困倦、张嘴哈欠、恢复 |
| Dragging | 6 | 悬空小爪子与左右歪头 |
| LookLeft | 3 | 坐姿看向左侧 |
| LookRight | 3 | 坐姿看向右侧 |

所有运行帧为 256×256 RGBA，透明背景。原精灵图保存在 artifacts/imagegen/p2，提示词与来源记录在 docs/assets/2026-10-02-p2-imagegen-prompts.json。
scripts/prepare-p2-assets.py 仅按等格切分并缩小图像，保留生成的 alpha，不绘制动作或替代 ImageGen。旧动作和两张原参考图未被覆盖。
