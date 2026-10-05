"""Package complete image_gen cells, preserving generated anatomy, colors and alpha."""
import json
from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "artifacts/imagegen/p17"
SCALE = .62
SIZE = 256
BASELINE = 228


def visible_box(image):
    # Analysis only: never use this thresholded copy to change delivered alpha.
    return image.getchannel("A").point(lambda v: 255 if v > 127 else 0).getbbox()


def separators(image):
    alpha = image.getchannel("A")
    counts = [sum(v > 127 for v in alpha.crop((0, y, image.width, y + 1)).getdata())
              for y in range(image.height)]
    cuts = [0]
    for center in (image.height // 3, image.height * 2 // 3):
        candidates = range(center - 80, center + 80)
        low = min(counts[y] for y in candidates)
        assert low <= 2, "Atlas has no clear separation between full sprite rows"
        quiet = [y for y in candidates if counts[y] <= low + 2]
        cuts.append(round((min(quiet) + max(quiet)) / 2))
    return cuts + [image.height]


def columns(image, top, bottom):
    alpha = image.getchannel("A")
    counts = [sum(v > 127 for v in alpha.crop((x, top, x + 1, bottom)).getdata())
              for x in range(image.width)]
    cuts = [0]
    for center in (image.width // 4, image.width // 2, image.width * 3 // 4):
        candidates = range(center - 45, center + 45)
        low = min(counts[x] for x in candidates)
        assert low <= 2, "Atlas has no clear separation between full sprites"
        quiet = [x for x in candidates if counts[x] <= low + 2]
        cuts.append(round((min(quiet) + max(quiet)) / 2))
    return cuts + [image.width]


report = {"mode": "built-in image_gen", "scale": SCALE, "frame_size": [SIZE, SIZE],
          "method": "whole generated cell crop; one equal aspect-preserving scale for all frames; whole-frame registration only",
          "generated_anatomy_colors_alpha_preserved": True, "no_mirroring_or_part_warping": True, "actions": {}}
for direction in ("Left", "Right"):
    source = OUT / f"Walk{direction}-v2-atlas.png"
    with Image.open(source) as image:
        assert image.mode == "RGBA" and image.getchannel("A").getextrema() == (0, 255)
        assert image.width * 3 == image.height * 4
        rows = separators(image)
        column_cuts = [columns(image, rows[row], rows[row + 1]) for row in range(3)]
        previews, details = [], []
        target = OUT / "packaged" / f"Walk{direction}"
        target.mkdir(parents=True, exist_ok=True)
        for index in range(12):
            column, row = index % 4, index // 4
            crop = (column_cuts[row][column], rows[row], column_cuts[row][column + 1], rows[row + 1])
            original = image.crop(crop)
            box = visible_box(original)
            assert box and box[0] > 0 and box[1] > 0 and box[2] < original.width and box[3] < original.height, (direction, index, box)
            cell = original.resize((round(original.width * SCALE), round(original.height * SCALE)), Image.Resampling.LANCZOS)
            box = visible_box(cell)
            # Register the upper face region and grounded paw line via whole-frame translations.
            head_left = 0 if direction == "Left" else round(cell.width * .52)
            head_right = round(cell.width * .58) if direction == "Left" else cell.width
            head = cell.crop((head_left, box[1], head_right, box[1] + round((box[3] - box[1]) * .32)))
            head_box = visible_box(head)
            assert head_box
            head_center = head_left + (head_box[0] + head_box[2]) / 2
            x = round((84 if direction == "Left" else 172) - head_center)
            y = BASELINE - box[3]
            assert box[0] + x >= 4 and box[2] + x <= SIZE - 4 and box[1] + y >= 4, (direction, index, x, y, box)
            frame = Image.new("RGBA", (SIZE, SIZE))
            frame.paste(cell, (x, y))
            alpha = frame.getchannel("A")
            visible = visible_box(frame)
            edge = max(alpha.crop(e).getextrema()[1] for e in [(0, 0, 1, SIZE), (255, 0, SIZE, SIZE), (0, 0, SIZE, 1), (0, 255, SIZE, SIZE)])
            assert edge < 8 and visible[3] == BASELINE, (direction, index, edge, visible)
            assert alpha.histogram()[0] > SIZE * SIZE * .35
            filename = f"walk_{direction.lower()}_{index:03d}.png"
            frame.save(target / filename)
            background = Image.new("RGBA", frame.size, "#304550")
            background.alpha_composite(frame)
            previews.append(background.convert("RGB"))
            details.append({"frame": index, "source_crop": crop, "whole_frame_translation": [x, y],
                            "visible_bbox": visible, "edge_alpha_max": edge, "alpha_range": alpha.getextrema()})
        sheet = Image.new("RGB", (4 * SIZE, 3 * (SIZE + 24)), "#304550")
        draw = ImageDraw.Draw(sheet)
        for index, preview in enumerate(previews):
            x, y = index % 4 * SIZE, index // 4 * (SIZE + 24)
            draw.text((x + 8, y + 6), f"Walk{direction} {index}", fill="white")
            sheet.paste(preview, (x, y + 24))
        sheet.save(OUT / f"Walk{direction}-contact.png")
        small = [p.resize((160, 160), Image.Resampling.LANCZOS) for p in previews]
        small[0].save(OUT / f"Walk{direction}-preview-160.gif", save_all=True, append_images=small[1:],
                      duration=[80, 80, 90] * 4, loop=0, disposal=2)
        report["actions"][f"Walk{direction}"] = {"source": str(source.relative_to(ROOT)), "row_boundaries": rows,
                                                     "column_boundaries_by_row": column_cuts, "frames": details}
(OUT / "validation.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
print(json.dumps({"frames": 24, "target": str(OUT / 'packaged'), "scale": SCALE, "alpha": "generated alpha preserved"}))
