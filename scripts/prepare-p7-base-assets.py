"""Stage P7 base actions using whole raster cells only.

Never writes to runtime Assets. Source atlases and their original generated
alpha remain unchanged. One uniform whole-atlas scale applies to each action,
then complete equal cells are cut and translated as indivisible frames.
Sleeping poses are deliberately packed at their natural reduced height.
"""
import argparse
import json
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "artifacts" / "imagegen" / "p7" / "base"
TARGET = SOURCE / "packaged"
MANIFEST = ROOT / "docs" / "assets" / "2026-10-04-p7-base-prompts.json"
SIZE = 256
BASELINE = 228
BACKGROUND = "#304550"


def visible_box(image, threshold=127):
    """Analyze an alpha copy; never threshold or replace delivered alpha."""
    return image.getchannel("A").point(
        lambda value: 255 if value > threshold else 0
    ).getbbox()


def composite(frame, size=SIZE):
    background = Image.new("RGBA", (size, size), BACKGROUND)
    image = frame if size == SIZE else frame.resize(
        (size, size), Image.Resampling.LANCZOS
    )
    background.alpha_composite(image)
    return background.convert("RGB")


def contact_sheet(frames, action, size=SIZE):
    columns = min(4, len(frames))
    rows = (len(frames) + columns - 1) // columns
    label_height = 24
    result = Image.new(
        "RGB", (columns * size, rows * (size + label_height)), BACKGROUND
    )
    draw = ImageDraw.Draw(result)
    for index, frame in enumerate(frames):
        x = (index % columns) * size
        y = (index // columns) * (size + label_height)
        draw.text((x + 7, y + 5), f"{action} {index}", fill="#e6eff4")
        result.paste(frame, (x, y + label_height))
    return result


def save_loop(frames, path, fps):
    frames[0].save(
        path, save_all=True, append_images=frames[1:],
        duration=round(1000 / fps), loop=0, disposal=2,
    )


def pack(action, entry):
    atlas_path = ROOT / entry["selected_atlas"]
    columns, rows = entry["grid"]
    selected = entry["selected_source_cells"]
    packing = entry.get("packing", {})
    cell_size = packing.get("cell_size", 220)
    fps = packing.get("fps", 8 if action != "Sleep" else 5)
    x_offset = packing.get("x_offset", (SIZE - cell_size) // 2)
    with Image.open(atlas_path) as source:
        assert source.mode == "RGBA", f"{action}: generated RGBA required"
        assert abs(source.width * rows - source.height * columns) <= max(columns, rows), (
            f"{action}: exact atlas aspect ratio required"
        )
        assert source.getchannel("A").getextrema() == (0, 255)
        source_size = list(source.size)
        # Resize the COMPLETE atlas uniformly first. This handles fractional
        # 443.5px source cells without unequal x/y source scaling.
        resized = source.resize(
            (columns * cell_size, rows * cell_size), Image.Resampling.LANCZOS
        )
        source_stats = []
        for index in selected:
            column, row = index % columns, index // columns
            source_box = (
                column * source.width / columns,
                row * source.height / rows,
                (column + 1) * source.width / columns,
                (row + 1) * source.height / rows,
            )
            source_stats.append(list(source_box))

    directory = TARGET / action
    directory.mkdir(parents=True, exist_ok=True)
    prefix = action.lower()
    previews, small_previews, checks = [], [], []
    for frame_index, source_index in enumerate(selected):
        column, row = source_index % columns, source_index // columns
        cell = resized.crop((
            column * cell_size, row * cell_size,
            (column + 1) * cell_size, (row + 1) * cell_size,
        ))
        box = visible_box(cell)
        assert box, f"{action}: empty source cell {source_index}"
        y_offset = BASELINE - box[3]
        frame = Image.new("RGBA", (SIZE, SIZE))
        frame.paste(cell, (x_offset, y_offset))
        final_box = visible_box(frame)
        assert final_box[0] >= 4 and final_box[1] >= 4, (
            f"{action} {frame_index}: top/left margin {final_box}"
        )
        assert final_box[2] <= SIZE - 4 and final_box[3] <= SIZE - 4, (
            f"{action} {frame_index}: bottom/right margin {final_box}"
        )
        assert final_box[3] == BASELINE, f"{action}: ground registration"
        alpha = frame.getchannel("A")
        histogram = alpha.histogram()
        edge_max = max(
            alpha.crop(edge).getextrema()[1] for edge in (
                (0, 0, 1, SIZE), (SIZE - 1, 0, SIZE, SIZE),
                (0, 0, SIZE, 1), (0, SIZE - 1, SIZE, SIZE),
            )
        )
        assert edge_max < 8, f"{action}: clipping/edge alpha {edge_max}"
        assert histogram[0] > SIZE * SIZE * 0.25, (
            f"{action}: transparent margins missing"
        )
        assert sum(histogram[128:]) > SIZE * SIZE * 0.06, (
            f"{action}: missing opaque subject"
        )
        output = directory / f"{prefix}_{frame_index:03d}.png"
        frame.save(output)
        previews.append(composite(frame))
        small_previews.append(composite(frame, 160))
        checks.append({
            "frame": frame_index, "source_frame": source_index,
            "file": str(output.relative_to(ROOT)),
            "source_cell": source_stats[frame_index],
            "size": list(frame.size), "mode": frame.mode,
            "whole_frame_translation": [x_offset, y_offset],
            "visible_bbox_alpha_gt127": list(final_box),
            "visible_bbox_alpha_gt8": list(visible_box(frame, 8)),
            "alpha_range": list(alpha.getextrema()),
            "transparent_pixels": histogram[0],
            "semi_transparent_pixels": sum(histogram[1:255]),
            "edge_alpha_max": edge_max,
            "ground_bottom_exclusive": final_box[3],
        })
    contact_sheet(previews, action).save(SOURCE / f"{action}-contact.png")
    contact_sheet(small_previews, action, 160).save(
        SOURCE / f"{action}-contact-160.png"
    )
    save_loop(previews, SOURCE / f"{action}-loop.gif", fps)
    save_loop(small_previews, SOURCE / f"{action}-loop-160.gif", fps)
    return {
        "sheet": str(atlas_path.relative_to(ROOT)),
        "source_size": source_size,
        "uniform_source_scale": cell_size * columns / source_size[0],
        "cell_size": cell_size, "fps": fps, "frames": checks,
    }, small_previews


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--actions", nargs="*", help="Pack only named actions")
    args = parser.parse_args()
    manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
    selected_actions = args.actions or list(manifest["actions"])
    report_path = SOURCE / "validation.json"
    report = json.loads(report_path.read_text(encoding="utf-8")) if report_path.exists() else {
        "method": "fixed per-action whole-atlas uniform LANCZOS scale; whole equal cell cut; indivisible whole-frame translation; generated alpha preserved",
        "frame_size": [SIZE, SIZE],
        "ground_bottom_exclusive": BASELINE,
        "no_runtime_install": True, "actions": {},
    }
    combined = []
    for action in selected_actions:
        result, small = pack(action, manifest["actions"][action])
        report["actions"][action] = result
        combined.append((action, small))
    if len(selected_actions) == len(manifest["actions"]):
        all_contact = Image.new(
            "RGB", (8 * 160, len(combined) * 184), BACKGROUND
        )
        draw = ImageDraw.Draw(all_contact)
        for row, (action, previews) in enumerate(combined):
            for column, preview in enumerate(previews):
                x, y = column * 160, row * 184
                draw.text((x + 6, y + 5), f"{action} {column}", fill="#e6eff4")
                all_contact.paste(preview, (x, y + 24))
        all_contact.save(SOURCE / "all-actions-contact-160.png")
    report_path.write_text(
        json.dumps(report, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    print(json.dumps({
        "target": str(TARGET), "actions": selected_actions,
        "frames": sum(len(report["actions"][name]["frames"]) for name in selected_actions),
        "size": [SIZE, SIZE], "mode": "RGBA",
        "alpha": "generated alpha preserved; no masks",
        "qa": str(SOURCE),
    }))


if __name__ == "__main__":
    main()
