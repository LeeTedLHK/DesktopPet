"""Pack generated P7 motion sprites into staged 256px RGBA frames only.

Each atlas is cropped into complete cells (Turn-v2 follows transparent gutters), then uniformly resized at
one fixed action scale and translated as a whole frame. Generated alpha is
never replaced or thresholded for delivery. No body warps, alpha masks,
mirrored directions, synthetic frames or runtime Assets writes are used.
"""
import json
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "artifacts" / "imagegen" / "p7" / "motion"
TARGET = SOURCE / "packaged"
MANIFEST = ROOT / "docs" / "assets" / "2026-10-04-p7-motion-prompts.json"
SIZE, BASELINE = 256, 228
BACKGROUND = "#304550"
CONFIG = {
    "StartLeft": {"sheet": "Start", "indices": [0, 1, 2, 3], "cell_size": 232, "fps": 8},
    "StartRight": {"sheet": "Start", "indices": [4, 5, 6, 7], "cell_size": 232, "fps": 8},
    "StopLeft": {"sheet": "Stop", "indices": [0, 1, 2, 3], "cell_size": 232, "fps": 8},
    "StopRight": {"sheet": "Stop", "indices": [4, 5, 6, 7], "cell_size": 232, "fps": 8},
    "TurnLeft": {"sheet": "Turn", "indices": [0, 1, 2, 3], "cell_size": 232, "fps": 8},
    "TurnRight": {"sheet": "Turn", "indices": [4, 5, 6, 7], "cell_size": 232, "fps": 8},
    "PickupLeft": {"sheet": "Pickup", "indices": [0, 1, 2, 3], "cell_size": 232, "fps": 8},
    "PickupRight": {"sheet": "Pickup", "indices": [4, 5, 6, 7], "cell_size": 232, "fps": 8},
    "DropLeft": {"sheet": "Drop", "indices": [0, 1, 2, 3], "cell_size": 232, "fps": 8},
    "DropRight": {"sheet": "Drop", "indices": [4, 5, 6, 7], "cell_size": 232, "fps": 8},
    "JumpLeft": {"sheet": "JumpLeft", "indices": list(range(8)), "cell_size": 232, "fps": 12},
    "JumpRight": {"sheet": "JumpRight", "indices": list(range(8)), "cell_size": 232, "fps": 12},
    "CarryLeft": {"sheet": "CarryLeft", "indices": list(range(8)), "cell_size": 232, "fps": 10},
    "CarryRight": {"sheet": "CarryRight", "indices": list(range(8)), "cell_size": 232, "fps": 10},
}
# Contacts align to the floor. Airborne paw clearance remains the generated
# difference from the contact ground in its source row; no new flight pose or
# prescribed upward translation is synthesized. Runtime supplies jump travel.
JUMP_CONTACTS = {0, 1, 2, 6, 7}
# Turn-v2 uses irregular transparent gutters. Cut complete rectangular cells at
# those gutters instead of severing its noses/paws at an ideal grid boundary.
# All eight cells use ONE common pixel scale; their bodies are never stretched.
TURN_X_GUTTERS = [0, 500 / 1774, 900 / 1774, 1270 / 1774, 1]
TURN_Y_GUTTERS = [0, 475 / 887, 1]


def visible_box(image, threshold=127):
    return image.getchannel("A").point(lambda v: 255 if v > threshold else 0).getbbox()


def composite(frame, size=SIZE):
    result = Image.new("RGBA", (size, size), BACKGROUND)
    image = frame if size == SIZE else frame.resize((size, size), Image.Resampling.LANCZOS)
    result.alpha_composite(image)
    return result.convert("RGB")


def contact_sheet(previews, action, size=SIZE):
    rows = (len(previews) + 3) // 4
    result = Image.new("RGB", (4 * size, rows * (size + 24)), BACKGROUND)
    draw = ImageDraw.Draw(result)
    for index, preview in enumerate(previews):
        x, y = index % 4 * size, index // 4 * (size + 24)
        draw.text((x + 8, y + 5), f"{action} {index}", fill="#e6eff4")
        result.paste(preview, (x, y + 24))
    return result


def save_loop(previews, path, fps):
    duration = round(1000 / fps / 10) * 10
    previews[0].save(path, save_all=True, append_images=previews[1:],
                     duration=duration, loop=0, disposal=2)


def main():
    SOURCE.mkdir(parents=True, exist_ok=True)
    report = {
        "method": "whole equal-cell crop; fixed per-action uniform LANCZOS resize; whole-frame translation only; generated alpha preserved",
        "frame_size": [SIZE, SIZE], "grid": [4, 2], "contact_baseline": BASELINE,
        "no_runtime_install": True, "actions": {},
    }
    previews_by_action = {}
    for action, config in CONFIG.items():
        source_path = SOURCE / f"{config['sheet']}.png"
        with Image.open(source_path) as atlas:
            assert atlas.mode == "RGBA", f"{action}: genuine RGBA required"
            assert atlas.width == atlas.height * 2, f"{action}: square-cell 4x2 sheet required"
            assert atlas.getchannel("A").getextrema() == (0, 255), f"{action}: original generated alpha required"
            source_size = list(atlas.size)
            cell_size = atlas.width / 4
            whole_size = config["cell_size"]
            cells = []
            for source_index in config["indices"]:
                col, row = source_index % 4, source_index // 4
                source_box = ((round(TURN_X_GUTTERS[col] * atlas.width), round(TURN_Y_GUTTERS[row] * atlas.height),
                               round(TURN_X_GUTTERS[col + 1] * atlas.width), round(TURN_Y_GUTTERS[row + 1] * atlas.height))
                              if config['sheet'] == 'Turn' else
                              (round(col * cell_size), round(row * cell_size),
                               round((col + 1) * cell_size), round((row + 1) * cell_size)))
                original = atlas.crop(source_box)
                raw = visible_box(original)
                assert raw and min(raw[:2]) > 0 and raw[2] < original.width and raw[3] < original.height, f"{action}: source clipping in cell{source_index}"
                factor = whole_size / cell_size
                resized = original.resize((round(original.width * factor), round(original.height * factor)), Image.Resampling.LANCZOS)
                cells.append((resized, source_box, raw))

        direction = "left" if action.endswith("Left") else "right"
        prefix = action.removesuffix("Left").removesuffix("Right").lower()
        target_dir = TARGET / action
        target_dir.mkdir(parents=True, exist_ok=True)
        expected_names = {f"{prefix}_{direction}_{i:03d}.png" for i in range(len(cells))}
        actual_names = {p.name for p in target_dir.glob("*.png")}
        assert actual_names <= expected_names, f"{action}: stale staged frame needs explicit review"
        row_ground = {
            row: max(visible_box(cells[i][0])[3]
                     for i in range(row * 4, row * 4 + 4)
                     if i in JUMP_CONTACTS)
            for row in range(2)
        } if action.startswith("Jump") else {}
        large, small, checks = [], [], []
        for index, (resized, source_box, raw) in enumerate(cells):
            before = visible_box(resized)
            clearance = max(0, row_ground[index // 4] - before[3]) if action.startswith("Jump") and index not in JUMP_CONTACTS else 0
            x_offset = (SIZE - whole_size) // 2 + config.get("x_offset", 0)
            if config['sheet'] == 'Turn': x_offset = round(SIZE / 2 - (before[0] + before[2]) / 2)
            y_offset = BASELINE - clearance - before[3]
            frame = Image.new("RGBA", (SIZE, SIZE))
            frame.paste(resized, (x_offset, y_offset))
            final = visible_box(frame)
            soft = visible_box(frame, 8)
            assert final and soft and min(soft[:2]) >= 4 and soft[2] <= SIZE - 4 and soft[3] <= SIZE - 4, f"{action} frame{index}: four-pixel margins failed {soft}"
            alpha = frame.getchannel("A")
            histogram = alpha.histogram()
            edge_max = max(alpha.crop(edge).getextrema()[1] for edge in
                           ((0, 0, 1, SIZE), (SIZE-1, 0, SIZE, SIZE),
                            (0, 0, SIZE, 1), (0, SIZE-1, SIZE, SIZE)))
            assert histogram[0] > SIZE * SIZE * .30, f"{action}: clear alpha missing"
            assert sum(histogram[128:]) > SIZE * SIZE * .10, f"{action}: subject missing"
            assert edge_max < 8, f"{action} frame{index}: edge contamination"
            assert final[3] == BASELINE - clearance
            output = target_dir / f"{prefix}_{direction}_{index:03d}.png"
            frame.save(output)
            large.append(composite(frame))
            small.append(composite(frame, 160))
            checks.append({
                "frame": index, "source_frame": config["indices"][index],
                "file": output.relative_to(ROOT).as_posix(), "source_cell": list(source_box),
                "source_opaque_bbox": list(raw), "size": list(frame.size), "mode": frame.mode,
                "fixed_uniform_whole_cell_size": whole_size,
                "whole_frame_translation": [x_offset, y_offset],
                "visible_bbox_alpha_gt127": list(final), "visible_bbox_alpha_gt8": list(soft),
                "alpha_range": list(alpha.getextrema()), "transparent_pixels": histogram[0],
                "semi_transparent_pixels": sum(histogram[1:255]), "edge_alpha_max": edge_max,
                "paw_clearance_from_ground": clearance,
            })
        contact_sheet(large, action).save(SOURCE / f"{action}-contact.png")
        contact_sheet(small, action, 160).save(SOURCE / f"{action}-contact-160.png")
        save_loop(large, SOURCE / f"{action}-loop.gif", config["fps"])
        save_loop(small, SOURCE / f"{action}-loop-160.gif", config["fps"])
        previews_by_action[action] = small
        report["actions"][action] = {
            "sheet": source_path.relative_to(ROOT).as_posix(),
            "source_size": source_size, "config": config,
            "source_row_ground_before_registration": row_ground, "frames": checks,
        }
    assert sum(len(v["frames"]) for v in report["actions"].values()) == 72
    all_contact = Image.new("RGB", (8 * 160, len(CONFIG) * 184), BACKGROUND)
    draw = ImageDraw.Draw(all_contact)
    for row, (action, previews) in enumerate(previews_by_action.items()):
        for col, preview in enumerate(previews):
            draw.text((col * 160 + 6, row * 184 + 5), f"{action} {col}", fill="#e6eff4")
            all_contact.paste(preview, (col * 160, row * 184 + 24))
    all_contact.save(SOURCE / "all-actions-contact-160.png")
    (SOURCE / "validation.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
    manifest["packaging"]["fixed_framing"] = CONFIG
    manifest["packaging"]["jump_registration"] = "contact paws align to228; airborne/recovery poses retain generated paw clearance from the same source row contact ground; runtime supplies the main vertical trajectory"
    MANIFEST.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"frames": 72, "actions": len(CONFIG), "size": [SIZE, SIZE],
                      "mode": "RGBA", "staged": str(TARGET), "alpha": "generated alpha preserved"}))


if __name__ == "__main__":
    main()
