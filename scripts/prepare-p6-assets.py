"""Stage P6 walking/running frames; never write to runtime Assets.

Generated 4x2 atlases are cut into whole equal square cells. One fixed scale
and horizontal offset applies to each complete action. Whole-frame vertical
registration aligns contact poses only; running suspension retains source
clearance relative to a common baseline for its source row. No masks, body
part transforms, mirroring, interpolation or synthetic poses are produced.
"""
import json
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "artifacts" / "imagegen" / "p6"
TARGET = SOURCE / "packaged"
SIZE = 256
BASELINE = 228
BACKGROUND = "#304550"
CONFIG = {
    "WalkLeft": {"cell_size": 240, "x_offset": 8,
                 "order": [0, 7, 3, 4, 2, 1, 5, 6], "fps": 10},
    "WalkRight": {"cell_size": 245, "x_offset": 5,
                  "order": [7, 0, 1, 3, 2, 4, 5, 6], "fps": 10},
    "RunLeft": {"cell_size": 256, "x_offset": -12,
                "order": list(range(8)), "fps": 12},
    "RunRight": {"cell_size": 280, "x_offset": -12,
                 "order": list(range(8)), "fps": 12},
}
# Chronological source phase indices; never normalize an airborne paw to ground.
RUN_CONTACTS = {0, 1, 3, 4, 7}
RUN_FLIGHTS = {2, 6}


def visible_box(image, threshold=127):
    """Threshold only an analysis copy; never change the delivered image alpha."""
    return image.getchannel("A").point(lambda value: 255 if value > threshold else 0).getbbox()


def composite(frame, size=SIZE):
    background = Image.new("RGBA", (size, size), BACKGROUND)
    image = frame if size == SIZE else frame.resize((size, size), Image.Resampling.LANCZOS)
    background.alpha_composite(image)
    return background.convert("RGB")


def contact_sheet(previews, action, size=SIZE):
    label_height = 24
    result = Image.new("RGB", (4 * size, 2 * (size + label_height)), BACKGROUND)
    draw = ImageDraw.Draw(result)
    for index, preview in enumerate(previews):
        x, y = (index % 4) * size, (index // 4) * (size + label_height)
        draw.text((x + 8, y + 5), f"{action} {index}", fill="#e6eff4")
        result.paste(preview, (x, y + label_height))
    return result


def save_loop(previews, path, fps):
    # GIF timing has 10ms resolution; this repeats approximately 12fps.
    duration = 100 if fps == 10 else [80, 80, 90, 80, 80, 90, 80, 90]
    previews[0].save(path, save_all=True, append_images=previews[1:],
                     duration=duration, loop=0, disposal=2)


def main():
    report = {
        "method": "whole equal-cell crop; fixed per-action whole-cell LANCZOS resize; whole-frame translation only; generated alpha preserved",
        "frame_size": [SIZE, SIZE], "grid": [4, 2], "contact_baseline": BASELINE,
        "no_runtime_install": True, "actions": {},
        "running_registration": "contact poses align to228; airborne/recovery poses preserve their generated paw clearance relative to contact baseline in the same source row",
    }
    runtime_previews = {}
    for action, config in CONFIG.items():
        with Image.open(SOURCE / f"{action}.png") as atlas:
            assert atlas.mode == "RGBA", f"{action}: RGBA alpha required"
            assert atlas.width == atlas.height * 2, f"{action}: square-cell 4x2 sheet required"
            assert atlas.getchannel("A").getextrema() == (0, 255)
            source_size = list(atlas.size)
            cells, boxes, raw_boxes = [], [], []
            cell = atlas.width / 4
            common_size = config["cell_size"]
            for index in range(8):
                column, row = index % 4, index // 4
                box = (round(column * cell), round(row * cell),
                       round((column + 1) * cell), round((row + 1) * cell))
                original = atlas.crop(box)
                raw = visible_box(original)
                assert raw and raw[0] > 0 and raw[1] > 0
                assert raw[2] < original.width and raw[3] < original.height, f"{action} source{index}: opaque source clipping"
                resized = original.resize((common_size, common_size), Image.Resampling.LANCZOS)
                frame = Image.new("RGBA", (SIZE, SIZE))
                frame.paste(resized, (config["x_offset"], (SIZE - common_size) // 2))
                cells.append(frame)
                boxes.append(list(box))
                raw_boxes.append(list(raw))

        is_running = action.startswith("Run")
        # Use the latest contact height within a row as its shared generated ground.
        row_ground = {
            row: max(visible_box(cells[i])[3] for i in range(row * 4, row * 4 + 4)
                     if not is_running or i in RUN_CONTACTS)
            for row in range(2)
        }
        directory = TARGET / action
        directory.mkdir(parents=True, exist_ok=True)
        prefix = action.removesuffix("Left").removesuffix("Right").lower()
        direction = "left" if action.endswith("Left") else "right"
        previews, small_previews, checks = [], [], []
        for frame_index, source_index in enumerate(config["order"]):
            frame = cells[source_index]
            before = visible_box(frame)
            # Walk and ground-contact run poses register their actual contact paw.
            ground = before[3] if not is_running or source_index in RUN_CONTACTS else row_ground[source_index // 4]
            y_offset = BASELINE - ground
            registered = Image.new("RGBA", (SIZE, SIZE))
            registered.paste(frame, (0, y_offset))
            final_box = visible_box(registered)
            assert final_box[0] >= 4 and final_box[1] >= 4, f"{action} frame{frame_index}: top/left margin"
            assert final_box[2] <= SIZE - 4 and final_box[3] <= SIZE - 4, f"{action} frame{frame_index}: bottom/right margin"
            alpha = registered.getchannel("A")
            histogram = alpha.histogram()
            assert histogram[0] > SIZE * SIZE * .35, f"{action}: transparent margins missing"
            assert sum(histogram[128:]) > SIZE * SIZE * .12, f"{action}: missing subject"
            edge_max = max(alpha.crop(edge).getextrema()[1] for edge in
                           ((0, 0, 1, SIZE), (SIZE - 1, 0, SIZE, SIZE),
                            (0, 0, SIZE, 1), (0, SIZE - 1, SIZE, SIZE)))
            assert edge_max < 8, f"{action} frame{frame_index}: edge alpha/clipping"
            if is_running and source_index in RUN_FLIGHTS:
                assert final_box[3] <= BASELINE - 12, f"{action}: running flight flattened"
            output = directory / f"{prefix}_{direction}_{frame_index:03d}.png"
            registered.save(output)
            preview = composite(registered)
            small = composite(registered, 160)
            previews.append(preview)
            small_previews.append(small)
            checks.append({
                "frame": frame_index, "source_frame": source_index, "file": str(output.relative_to(ROOT)),
                "source_cell": boxes[source_index], "source_opaque_bbox": raw_boxes[source_index],
                "size": list(registered.size), "mode": registered.mode,
                "whole_frame_y_translation": y_offset, "visible_bbox_alpha_gt127": list(final_box),
                "visible_bbox_alpha_gt8": list(visible_box(registered, 8)),
                "alpha_range": list(alpha.getextrema()), "transparent_pixels": histogram[0],
                "semi_transparent_pixels": sum(histogram[1:255]), "edge_alpha_max": edge_max,
                "paw_clearance_from_ground": BASELINE - final_box[3],
                "run_phase": ("extended-flight" if source_index == 2 else "gathered-flight" if source_index == 6
                              else "ground-contact" if source_index in RUN_CONTACTS else "push-recovery") if is_running else None,
            })
        contact_sheet(previews, action).save(SOURCE / f"{action}-contact.png")
        contact_sheet(small_previews, action, 160).save(SOURCE / f"{action}-contact-160.png")
        save_loop(previews, SOURCE / f"{action}-loop.gif", config["fps"])
        save_loop(small_previews, SOURCE / f"{action}-loop-160.gif", config["fps"])
        runtime_previews[action] = small_previews
        report["actions"][action] = {"sheet": str((SOURCE / f"{action}.png").relative_to(ROOT)),
                                      "source_size": source_size, "config": config,
                                      "source_row_ground_before_registration": row_ground, "frames": checks}
    all_contact = Image.new("RGB", (8 * 160, 4 * 184), BACKGROUND)
    draw = ImageDraw.Draw(all_contact)
    for row, (action, previews) in enumerate(runtime_previews.items()):
        for column, preview in enumerate(previews):
            draw.text((column * 160 + 6, row * 184 + 5), f"{action} {column}", fill="#e6eff4")
            all_contact.paste(preview, (column * 160, row * 184 + 24))
    all_contact.save(SOURCE / "all-actions-contact-160.png")
    (SOURCE / "validation.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"target": str(TARGET), "frames": 32, "size": [SIZE, SIZE], "mode": "RGBA",
                      "alpha": "generated alpha preserved; no masks", "previews": str(SOURCE)}))


if __name__ == "__main__":
    main()
