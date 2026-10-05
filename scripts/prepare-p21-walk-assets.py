"""Package complete generated poses with uniform whole-frame registration.

Only complete-frame uniform resizing and translation are used. No masks,
segmented body compositing, motion warping or invented interpolated poses.
"""
import argparse
import hashlib
import json
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'artifacts/imagegen/p21'
SIZE=256

def head_geometry(image,direction):
    a=np.asarray(image.getchannel('A'))
    xx,yy=np.meshgrid(np.arange(image.width),np.arange(image.height))
    # The head is isolated by screen side; the tail never enters this region.
    domain=(xx < image.width*.58) if direction=='Left' else (xx > image.width*.43)
    mask=(a>127)&domain
    ys=np.nonzero(mask)[0]
    assert len(ys)>0
    top=int(ys.min())
    dome=mask&(yy>=top+round(image.height*.055))&(yy<top+round(image.height*.25))
    ys,xs=np.nonzero(dome)
    assert len(xs)>0
    return {'top':top,'center':float(xs.mean()),'width':int(xs.max()-xs.min()+1)}

def visible_box(image):
    return image.getchannel('A').point(lambda a:255 if a>127 else 0).getbbox()

report={'method':'whole-frame uniform resizing and head-based translation',
        'generated_alpha_preserved':True,'body_part_warps':False,
        'fps':15,'frames_per_direction':8,'actions':{}}

parser=argparse.ArgumentParser()
parser.add_argument('--direction',choices=['Left','Right'],default=None)
parser.add_argument('--register-floor',action='store_true',
                    help='Register head top and supporting floor by scaling the complete frame uniformly.')
args=parser.parse_args()
if args.register_floor:
    report['method']='whole-frame uniform resizing between head top and supporting floor; horizontal head registration'
for direction in ([args.direction] if args.direction else ('Left','Right')):
    reference=Image.open(OUT/f'previous-runtime/Walk{direction}/walk_{direction.lower()}_000.png').convert('RGBA')
    goal=head_geometry(reference,direction)
    frames=[]
    details=[]
    directory=OUT/f'packaged/Walk{direction}'
    directory.mkdir(parents=True,exist_ok=True)
    for phase in range(8):
        source=OUT/f'single-generated/{direction}-{phase}.png'
        original=Image.open(source)
        assert original.mode=='RGBA',f'No alpha in {source}'
        assert original.width==original.height,original.size
        base=original.resize((SIZE,SIZE),Image.Resampling.LANCZOS)
        before=head_geometry(base,direction)
        floor=222 if phase==5 else 228
        source_floor=visible_box(base)[3]-1
        scale=((floor-goal['top'])/(source_floor-before['top'])) if args.register_floor else goal['width']/before['width']
        # Single-image generation varies canvas framing. Calibrate the entire
        # figure to the original head size; never resize individual limbs.
        assert .80<=scale<=1.20,('Unexpected canvas framing',direction,phase,scale)
        cell=base.resize((round(SIZE*scale),round(SIZE*scale)),Image.Resampling.LANCZOS)
        geometry=head_geometry(cell,direction)
        x=round(goal['center']-geometry['center'])
        y=goal['top']-geometry['top']
        frame=Image.new('RGBA',(SIZE,SIZE))
        frame.paste(cell,(x,y))
        box=visible_box(frame)
        assert box and min(box[:2])>=4 and box[2]<=252 and box[3]<=244,('Clipped pose',direction,phase,box)
        alpha=frame.getchannel('A')
        edge=max(alpha.crop(e).getextrema()[1] for e in [(0,0,1,SIZE),(255,0,SIZE,SIZE),(0,0,SIZE,1),(0,255,SIZE,SIZE)])
        assert edge<=8 and alpha.getextrema()==(0,255),(direction,phase,edge,alpha.getextrema())
        filename=f'walk_{direction.lower()}_{phase:03d}.png'
        frame.save(directory/filename)
        frames.append(frame)
        details.append({'phase':phase,'source':str(source.relative_to(ROOT)),
                        'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),
                        'source_size':list(original.size),'uniform_scale_after_256_normalization':scale,
                        'whole_frame_translation':[x,y],'head_geometry':head_geometry(frame,direction),
                        'visible_box':box,'edge_alpha_max':edge,'intended_supporting_floor':floor if args.register_floor else None,
                        'sha256':hashlib.sha256((directory/filename).read_bytes()).hexdigest()})
    contact=Image.new('RGB',(1024,560),'#304550')
    draw=ImageDraw.Draw(contact)
    for phase,frame in enumerate(frames):
        row,col=divmod(phase,4)
        tile=Image.new('RGBA',(256,256),'#304550')
        tile.alpha_composite(frame)
        contact.paste(tile.convert('RGB'),(col*256,row*280+24))
        draw.text((col*256+8,row*280+6),f'{direction} {phase}',fill='white')
    contact.save(OUT/f'Walk{direction}-contact.png')
    for color,label in [('#304550','dark'),('#ece8e1','light')]:
        previews=[]
        for frame in frames:
            bg=Image.new('RGBA',frame.size,color);bg.alpha_composite(frame)
            previews.append(bg.resize((160,160),Image.Resampling.LANCZOS).convert('RGB'))
        previews[0].save(OUT/f'Walk{direction}-preview-{label}.gif',save_all=True,append_images=previews[1:],
                         duration=[70,60,70,70,60,70,60,70],loop=0,disposal=2)
    report['actions'][direction]={'reference_head':goal,'poses':details}
(OUT/'validation.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print(f"Packaged {len(report['actions'])*8} complete poses; generated alpha retained.")
