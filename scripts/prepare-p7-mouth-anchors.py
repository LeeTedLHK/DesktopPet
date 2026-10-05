"""Record visually measured mouth points; draw QA markers only on previews."""
import json
from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'artifacts/imagegen/p7/motion'
POINTS = {
    'CarryLeft': [(57,126),(59,125),(58,126),(58,126),(57,136),(56,136),(57,138),(57,137)],
    'CarryRight': [(220,134),(222,134),(221,134),(222,135),(221,146),(221,143),(220,144),(225,149)],
    'JumpLeft': [(55,125),(56,167),(47,100),(50,76),(61,93),(50,110),(52,166),(48,168)],
    'JumpRight': [(210,119),(221,158),(207,98),(202,70),(211,77),(216,121),(217,157),(221,157)],
    'PickupLeft': [(51,143),(38,217),(33,183),(39,117)],
    'PickupRight': [(211,138),(208,217),(215,170),(214,137)],
    'DropLeft': [(49,126),(37,183),(39,226),(48,131)],
    'DropRight': [(207,134),(211,203),(219,222),(217,133)],
    'TurnLeft': [(208,128),(171,137),(126,133),(48,132)],
    'TurnRight': [(53,132),(72,137),(144,139),(215,133)],
}
metadata = {'frame_size':[256,256], 'coordinate_space':'normalized image coordinates of mouth contact point; app offsets toy center slightly outward/downward',
            'source':'visually measured from reviewed P7 256px generated frames; metadata does not change raster pixels',
            'actions':{}}
board=Image.new('RGB',(8*160,len(POINTS)*184),'#304550')
draw=ImageDraw.Draw(board)
for row,(action,points) in enumerate(POINTS.items()):
    frames=sorted((SOURCE/'packaged'/action).glob('*.png'))
    assert len(frames)==len(points)
    metadata['actions'][action]=[{'frame':i,'x':x/256,'y':y/256} for i,(x,y) in enumerate(points)]
    for col,(frame,(x,y)) in enumerate(zip(frames,points)):
        with Image.open(frame) as im:
            preview=Image.new('RGBA',(256,256),'#304550'); preview.alpha_composite(im)
        marker=ImageDraw.Draw(preview); marker.ellipse((x-3,y-3,x+3,y+3),outline='#fbbd67',width=2)
        board.paste(preview.resize((160,160),Image.Resampling.LANCZOS).convert('RGB'),(col*160,row*184+24))
        draw.text((col*160+4,row*184+5),f'{action} {col}',fill='white')
(SOURCE/'mouth-anchors.json').write_text(json.dumps(metadata,indent=2)+'\n',encoding='utf-8')
board.save(SOURCE/'mouth-anchors-qa.png')
print(f'{sum(map(len,POINTS.values()))} visually measured frame mouth anchors staged; runtime raster pixels unchanged')
