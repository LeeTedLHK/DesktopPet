"""Package complete ImageGen cells; preserve anatomy, colors and generated alpha."""
import json
from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'artifacts/imagegen/p19'
SCALE = .60
SIZE = 256
BASELINE = 228


def visible_box(image):
    return image.getchannel('A').point(lambda v: 255 if v > 127 else 0).getbbox()


def cuts(image, axis, pieces):
    alpha = image.getchannel('A')
    length = image.width if axis == 'x' else image.height
    counts = []
    for n in range(length):
        strip = (n, 0, n + 1, image.height) if axis == 'x' else (0, n, image.width, n + 1)
        counts.append(sum(v > 8 for v in alpha.crop(strip).getdata()))
    result = [0]
    for part in range(1, pieces):
        center = round(length * part / pieces)
        candidates = range(center - 64, center + 64)
        low = min(counts[n] for n in candidates)
        assert low <= 2, ('No clean whole-cell separation', axis, center, low)
        quiet = [n for n in candidates if counts[n] <= low]
        result.append(round((min(quiet) + max(quiet)) / 2))
    return result + [length]


report = {'mode': 'built-in image_gen', 'frame_size': [SIZE, SIZE], 'scale': SCALE,
          'fps': 15, 'cycle_seconds': 8 / 15, 'paw_baseline': BASELINE,
          'method': 'whole-cell crop, uniform aspect-preserving scale and whole-frame registration only',
          'generated_alpha_preserved': True, 'no_body_part_warping_or_alpha_masks': True, 'actions': {}}
for direction in ('Left', 'Right'):
    source = OUT / f'Walk{direction}-selected-atlas.png'
    image = Image.open(source)
    assert image.mode == 'RGBA' and image.size == (1536, 1024)
    rows = cuts(image, 'y', 2)
    columns = [cuts(image.crop((0, rows[row], image.width, rows[row + 1])), 'x', 4) for row in range(2)]
    # ImageGen's left counterpart reverses the entire atlas; restore chronology
    # by assigning whole cells to filenames. No pixels are mirrored by this script.
    order = [3, 2, 1, 0, 7, 6, 5, 4] if direction == 'Left' else list(range(8))
    previews, details = [], []
    target = OUT / 'packaged' / f'Walk{direction}'
    target.mkdir(parents=True, exist_ok=True)
    for index, original_index in enumerate(order):
        row, col = divmod(original_index, 4)
        crop = (columns[row][col], rows[row], columns[row][col + 1], rows[row + 1])
        original = image.crop(crop)
        box = visible_box(original)
        assert box and min(box[:2]) > 0 and box[2] < original.width and box[3] < original.height
        cell = original.resize((round(original.width * SCALE), round(original.height * SCALE)), Image.Resampling.LANCZOS)
        box = visible_box(cell)
        head_left = 0 if direction == 'Left' else round(cell.width * .55)
        head_right = round(cell.width * .45) if direction == 'Left' else cell.width
        head = cell.crop((head_left, box[1], head_right, box[1] + round((box[3] - box[1]) * .60)))
        head_box = visible_box(head)
        assert head_box
        head_center = head_left + (head_box[0] + head_box[2]) / 2
        x = round((77 if direction == 'Left' else 179) - head_center)
        y = BASELINE - box[3]
        assert box[0] + x >= 4 and box[2] + x <= SIZE - 4 and box[1] + y >= 4
        frame = Image.new('RGBA', (SIZE, SIZE))
        frame.paste(cell, (x, y))
        alpha = frame.getchannel('A')
        edge = max(alpha.crop(e).getextrema()[1] for e in [(0, 0, 1, SIZE), (255, 0, SIZE, SIZE), (0, 0, SIZE, 1), (0, 255, SIZE, SIZE)])
        assert edge <= 8 and visible_box(frame)[3] == BASELINE
        assert alpha.histogram()[0] > SIZE * SIZE * .35
        frame.save(target / f'walk_{direction.lower()}_{index:03d}.png')
        background = Image.new('RGBA', frame.size, '#304550')
        background.alpha_composite(frame)
        previews.append(background.convert('RGB'))
        details.append({'frame': index, 'source_cell': original_index, 'source_crop': crop,
                        'whole_frame_translation': [x, y], 'visible_bbox': visible_box(frame),
                        'alpha_range': alpha.getextrema(), 'edge_alpha_max': edge})
    sheet = Image.new('RGB', (4 * SIZE, 2 * (SIZE + 24)), '#304550')
    draw = ImageDraw.Draw(sheet)
    for index, preview in enumerate(previews):
        x, y = index % 4 * SIZE, index // 4 * (SIZE + 24)
        draw.text((x + 8, y + 6), f'Walk{direction} {index}', fill='white')
        sheet.paste(preview, (x, y + 24))
    sheet.save(OUT / f'Walk{direction}-contact.png')
    small = [p.resize((160, 160), Image.Resampling.LANCZOS) for p in previews]
    small[0].save(OUT / f'Walk{direction}-preview-160.gif', save_all=True, append_images=small[1:],
                  duration=[70, 60, 70, 70, 60, 70, 60, 70], loop=0, disposal=2)
    report['actions'][f'Walk{direction}'] = {'source': str(source.relative_to(ROOT)),
                                          'rows': rows, 'columns_by_row': columns, 'frames': details}
(OUT / 'validation.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
print(json.dumps({'frames': 16, 'target': str(OUT / 'packaged'), 'cycle_seconds': 8 / 15}))
