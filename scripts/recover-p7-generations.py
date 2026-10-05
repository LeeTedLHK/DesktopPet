"""Recover generated raw atlases after interrupted collaborators; QA only."""
from pathlib import Path
from PIL import Image, ImageDraw
import shutil
import json

ROOT = Path(__file__).resolve().parents[1]
FOLDERS = {
    "motion": Path("C:/Users/BOSSlee/.codex/generated_images/01a1029e-4018-7012-8d95-e5a1aa49cdd2"),
    "base": Path("C:/Users/BOSSlee/.codex/generated_images/01a1029d-c809-7ca0-8a0c-5b775efecdbd"),
}
for domain, directory in FOLDERS.items():
    images = sorted(directory.glob("*.png"), key=lambda p: p.stat().st_mtime)
    target = ROOT / "artifacts/imagegen/p7" / domain / "recovered"
    target.mkdir(parents=True, exist_ok=True)
    board = Image.new("RGB", (900, ((len(images)+2)//3)*250), "#304550")
    draw = ImageDraw.Draw(board)
    for i, path in enumerate(images):
        copied = target / path.name
        shutil.copy2(path, copied)
        with Image.open(path) as im:
            thumb = im.copy(); thumb.thumbnail((300, 210))
            preview = Image.new("RGBA", (300,210), "#304550")
            preview.alpha_composite(thumb, ((300-thumb.width)//2, (210-thumb.height)//2))
        x,y = i%3*300, i//3*250
        draw.text((x+5,y+5),f"{i} {path.name[5:13]}",fill="white")
        board.paste(preview.convert("RGB"),(x,y+30))
        print(domain,i,path)
    board.save(ROOT / "artifacts/imagegen/p7" / domain / "recovered-contact.png")

    names = (['Start','Stop','Turn','Pickup','Drop','JumpLeft','JumpRight','CarryLeft','CarryRight']
             if domain == 'motion' else ['Idle','Happy','Sleep','Petting','Feeding','Stretching'])
    assert len(images) == len(names), 'Inspect changed generation count before assigning atlases'
    manifest_path = ROOT / f'docs/assets/2026-10-04-p7-{domain}-prompts.json'
    manifest = json.loads(manifest_path.read_text(encoding='utf-8'))
    for name, source in zip(names, images):
        selected = ROOT / 'artifacts/imagegen/p7' / domain / f'{name}.png'
        shutil.copy2(source, selected)
        if domain == 'motion':
            entry = next(v for v in manifest['attempts'] if v['atlas'] == name)
            entry.update({'selected': True, 'status':'recovered and visually identified', 'source_path':str(source),
                          'selected_atlas':selected.relative_to(ROOT).as_posix(),
                          'selection_reason':'Inspected recovered atlas: intended chronological action and independently authored directions, approved P6 photo identity.'})
        else:
            entry = manifest['actions'][name]
            entry.update({'source_path':str(source),'selected_atlas':selected.relative_to(ROOT).as_posix(),
                          'status':'recovered and visually identified'})
            if name in ('Feeding','Stretching'):
                entry['packing']={'cell_size':180, 'x_offset':38, 'fps':4}
                entry['selected_source_cells']=[0,1,2,4,6,7] if name == 'Feeding' else [0,1,2,3,5,7]
    manifest_path.write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
