"""Pack complete generated feeding cells; preserve generated colors and alpha."""
import json
from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "artifacts/imagegen/p13"
TARGET = OUT / "packaged/Feeding"
TARGET.mkdir(parents=True, exist_ok=True)
CELL = 224
BASELINE = 228
SOURCE_MOUTHS = [(86, 320), (84, 448), (91, 459), (78, 442), (63, 235), (82, 207)]

with Image.open(OUT / "feeding-atlas.png") as original:
    assert original.mode == "RGBA" and original.size == (1536, 1024)
    assert original.getchannel("A").getextrema()[0] == 0
    atlas = original.resize((CELL * 3, CELL * 2), Image.Resampling.LANCZOS)

frames, contacts, results = [], [], []
for index in range(6):
    cell = atlas.crop((index % 3 * CELL, index // 3 * CELL,
                       (index % 3 + 1) * CELL, (index // 3 + 1) * CELL))
    box = cell.getchannel("A").point(lambda v: 255 if v > 127 else 0).getbbox()
    assert box
    translation = (16, BASELINE - box[3])
    frame = Image.new("RGBA", (256, 256))
    frame.paste(cell, translation)
    alpha = frame.getchannel("A")
    visible = alpha.point(lambda v: 255 if v > 127 else 0).getbbox()
    assert visible[3] == BASELINE and visible[0] >= 4 and visible[2] <= 252 and visible[1] >= 4
    assert max(alpha.crop(edge).getextrema()[1] for edge in
               [(0, 0, 1, 256), (255, 0, 256, 256), (0, 0, 256, 1), (0, 255, 256, 256)]) < 8
    assert alpha.histogram()[0] > 256 * 256 * .25
    frame.save(TARGET / f"feeding_{index:03d}.png")
    mouth = SOURCE_MOUTHS[index]
    contacts.append({"frame": index, "x": (16 + mouth[0] * CELL / 512) / 256,
                     "y": (translation[1] + mouth[1] * CELL / 512) / 256})
    preview = Image.new("RGBA", frame.size, "#304550")
    preview.alpha_composite(frame)
    frames.append(preview.convert("RGB"))
    results.append({"frame": index, "translation": translation, "bbox": visible,
                    "alpha_range": alpha.getextrema(), "transparent_pixels": alpha.histogram()[0]})

sheet = Image.new("RGB", (3 * 256, 2 * 280), "#304550")
draw = ImageDraw.Draw(sheet)
for index, frame in enumerate(frames):
    x, y = index % 3 * 256, index // 3 * 280
    draw.text((x + 8, y + 6), f"Feeding {index}", fill="white")
    sheet.paste(frame, (x, y + 24))
sheet.save(OUT / "feeding-contact.png")
small = [frame.resize((160, 160), Image.Resampling.LANCZOS) for frame in frames]
small[0].save(OUT / "feeding-preview-160.gif", save_all=True, append_images=small[1:],
              duration=250, loop=0, disposal=2)
(OUT / "feeding-anchors.json").write_text(json.dumps(contacts, indent=2) + "\n", encoding="utf-8")
(OUT / "validation.json").write_text(json.dumps({"mode": "built-in image_gen", "frames": results,
    "uniform_scale": CELL / 512, "whole_cell_translation_only": True,
    "colors_and_generated_alpha_preserved": True}, indent=2) + "\n", encoding="utf-8")
print(json.dumps({"frames": len(frames), "target": str(TARGET), "anchors": contacts}))
