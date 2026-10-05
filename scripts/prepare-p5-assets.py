"""Package generated walk sheets without synthesizing poses or masking alpha.

Whole equal square cells are uniformly downsampled. Runtime files remain RGBA;
contact sheets and GIFs composite a background only for visual inspection.
Run without arguments for staged assets, or --install after archiving the old
runtime directories. Installation refuses to mix old and new PNG frames.
"""
import argparse
import json
from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "artifacts" / "imagegen" / "p5"
RUNTIME = ROOT / "src" / "DesktopPet" / "Assets" / "Pet"
SIZE = 256


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--install", action="store_true")
    args = parser.parse_args()
    target = RUNTIME if args.install else SOURCE / "packaged"
    report = {"method": "equal-cell crop and whole-cell LANCZOS downsample; generated alpha preserved",
              "common_framing": {"WalkLeft": {"whole_cell_size": 220, "canvas_offset": [18, 22]},
                                 "WalkRight": {"whole_cell_size": 256, "canvas_offset": [0, 0]}},
              "foot_contact_baseline": 228,
              "frame_size": [SIZE, SIZE], "grid": [4, 2], "actions": {}}
    all_previews = {}
    for action in ("WalkLeft", "WalkRight"):
        directory = target / action
        if args.install and directory.exists() and list(directory.glob("*.png")):
            raise RuntimeError(f"Archive existing {directory} before --install; refusing mixed frame sets")
        directory.mkdir(parents=True, exist_ok=True)
        prefix = "walk_left" if action == "WalkLeft" else "walk_right"
        frames, preview, checks = [], [], []
        with Image.open(SOURCE / f"{action}.png") as source:
            assert source.mode == "RGBA", f"{action}: genuine alpha required"
            assert source.width == source.height * 2, f"{action}: square cells require a 2:1 sheet"
            assert source.getchannel("A").getextrema() == (0, 255)
            cell = source.width / 4
            for index in range(8):
                column, row = index % 4, index // 4
                # Rounded shared boundaries support odd image generator sizes.
                box = (round(column * cell), round(row * cell),
                       round((column + 1) * cell), round((row + 1) * cell))
                common_size = 220 if action == "WalkLeft" else SIZE
                cell_image = source.crop(box).resize((common_size, common_size), Image.Resampling.LANCZOS)
                frame = Image.new("RGBA", (SIZE, SIZE))
                # One fixed camera scale for an entire direction, never a
                # pose-dependent crop, resize or mask.
                frame.paste(cell_image, (18, 22) if action == "WalkLeft" else (0, 0))
                foot_bottom = frame.getchannel("A").point(lambda p: 255 if p > 127 else 0).getbbox()[3]
                baseline_offset = 228 - foot_bottom
                registered = Image.new("RGBA", (SIZE, SIZE))
                # Full-frame integer translation registers the lowest visible
                # contact paw; no part of the dog is independently transformed.
                registered.paste(frame, (0, baseline_offset))
                frame = registered
                output = directory / f"{prefix}_{index:03d}.png"
                frame.save(output)
                alpha = frame.getchannel("A")
                histogram = alpha.histogram()
                assert histogram[0] > SIZE * SIZE * .25, f"{action} {index}: transparent margins missing"
                assert sum(histogram[128:]) > SIZE * SIZE * .15, f"{action} {index}: subject missing"
                # LANCZOS can introduce alpha=1 ringing beside fully clear margins.
                assert all(alpha.getpixel(point) < 8 for point in ((0, 0), (255, 0), (0, 255), (255, 255)))
                checks.append({"frame": index, "file": str(output.relative_to(ROOT)),
                               "source_cell": list(box), "size": list(frame.size), "mode": frame.mode,
                               "baseline_translation_y": baseline_offset,
                               "alpha_range": list(alpha.getextrema()), "transparent_pixels": histogram[0],
                               "semi_transparent_pixels": sum(histogram[1:255]),
                               "visible_bbox_alpha_gt_127": list(alpha.point(lambda p: 255 if p > 127 else 0).getbbox())})
                background = Image.new("RGBA", frame.size, "#304550")
                background.alpha_composite(frame)
                preview.append(background.convert("RGB"))
                frames.append(frame)
        all_previews[action] = preview
        contact = Image.new("RGB", (4 * SIZE, 2 * (SIZE + 26)), "#e5e9eb")
        draw = ImageDraw.Draw(contact)
        for index, image in enumerate(preview):
            x, y = (index % 4) * SIZE, (index // 4) * (SIZE + 26)
            draw.text((x + 10, y + 7), f"{action} {index}", fill="#21313a")
            contact.paste(image, (x, y + 26))
        contact.save(SOURCE / f"{action}-contact.png")
        preview[0].save(SOURCE / f"{action}-loop.gif", save_all=True, append_images=preview[1:],
                        duration=100, loop=0, disposal=2)
        report["actions"][action] = {"sheet": str((SOURCE / f"{action}.png").relative_to(ROOT)),
                                     "source_size": list(source.size), "count": len(checks), "frames": checks}
    both = []
    for index in range(8):
        pair = Image.new("RGB", (SIZE * 2, SIZE + 26), "#e5e9eb")
        draw = ImageDraw.Draw(pair)
        for column, action in enumerate(("WalkLeft", "WalkRight")):
            draw.text((column * SIZE + 10, 7), f"{action} / frame {index}", fill="#21313a")
            pair.paste(all_previews[action][index], (column * SIZE, 26))
        both.append(pair)
    both[0].save(SOURCE / "walk-both-loop.gif", save_all=True, append_images=both[1:], duration=100, loop=0, disposal=2)
    (SOURCE / "validation.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps({"target": str(target), "frames": 16, "size": [SIZE, SIZE], "mode": "RGBA",
                      "alpha": "original generated alpha; no background masking", "previews": str(SOURCE)}))


if __name__ == "__main__":
    main()
