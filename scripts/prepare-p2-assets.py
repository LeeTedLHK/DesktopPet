"""Split generated sprite sheets into runtime frames, preserving their alpha.

This script packages ImageGen output; it does not synthesize pet poses or remove
backgrounds. Pillow is only a development dependency, never an app dependency.
"""
import json
from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'artifacts' / 'imagegen' / 'p2'
TARGET = ROOT / 'src' / 'DesktopPet' / 'Assets' / 'Pet'


def main():
    report = []
    previews = []
    for key in ('Petting', 'Feeding', 'Stretching', 'Yawning', 'Dragging', 'Looking'):
        with Image.open(SOURCE / f'{key}.png') as sheet:
            assert sheet.mode == 'RGBA', f'{key}: missing alpha'
            assert sheet.width * 2 == sheet.height * 3, f'{key}: expected 3:2 grid'
            cell = sheet.width // 3
            assert cell * 2 == sheet.height
            for index in range(6):
                x, y = (index % 3) * cell, (index // 3) * cell
                frame = sheet.crop((x, y, x + cell, y + cell)).resize((256, 256), Image.Resampling.LANCZOS)
                action = ('LookLeft' if index < 3 else 'LookRight') if key == 'Looking' else key
                number = index % 3 if key == 'Looking' else index
                directory = TARGET / action
                directory.mkdir(parents=True, exist_ok=True)
                output = directory / f'{action.lower()}_{number:03d}.png'
                frame.save(output)
                alpha = frame.getchannel('A')
                histogram = alpha.histogram()
                assert sum(histogram[:8]) > 0.25 * 256 * 256, f'{output}: no transparent margins'
                assert sum(histogram[128:]) > 0.1 * 256 * 256, f'{output}: pet is not opaque'
                report.append({'file': str(output.relative_to(ROOT)), 'size': list(frame.size),
                               'alpha_range': list(alpha.getextrema()), 'transparent_pixels': histogram[0]})
                background = Image.new('RGBA', frame.size, '#263b37')
                background.alpha_composite(frame)
                previews.append((action, number, background.convert('RGB')))
    contact = Image.new('RGB', (6 * 256, 6 * 286), '#e8eeeb')
    draw = ImageDraw.Draw(contact)
    for i, (action, number, preview) in enumerate(previews):
        x, y = (i % 6) * 256, (i // 6) * 286
        contact.paste(preview, (x, y + 30))
        draw.text((x + 10, y + 8), f'{action} {number}', fill='#243c36')
    contact.save(SOURCE / 'contact-sheet.jpg', quality=93)
    (SOURCE / 'validation.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    print(f'{len(report)} frames validated: 256x256 RGBA, transparent margins, visible subjects.')


if __name__ == '__main__':
    main()
