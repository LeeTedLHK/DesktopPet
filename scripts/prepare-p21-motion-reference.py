"""Create code-native motion diagrams and atlas layout; do not deform pet pixels.

The user's video provides the support/recovery order. Joint positions below
adapt the observed trajectories to the existing compact character; obscured
far-side joints are inferred, never described as motion capture.
"""
import hashlib
import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'artifacts/imagegen/p21'
OUT.mkdir(parents=True, exist_ok=True)
SIZE = (1536, 1024)
FACTOR = 1.35
ORIGIN = (19, 85)
VIDEO = ROOT / 'src/DesktopPet/Assets/Pet/小狗行走.mp4'

# Four independently specified paths, in right-facing character coordinates.
# The order was checked against frames 45..68 and a second cycle 90..113.
# Near fore remains planted in video 55 while near hind starts lifting.
PATHS = {
    'NF': [
        [(152,168),(156,191),(178,214),(188,228)],
        [(152,168),(152,192),(167,213),(178,228)],
        [(152,168),(150,193),(153,214),(163,228)],
        [(152,168),(148,194),(139,214),(149,228)],
        [(152,168),(146,194),(129,215),(137,228)],
        [(152,168),(159,192),(133,204),(143,216)],
        [(152,168),(172,187),(153,199),(165,208)],
        [(152,168),(172,190),(175,211),(186,220)],
    ],
    'NH': [
        [(80,168),(89,190),(72,211),(87,228)],
        [(80,168),(85,191),(65,211),(79,228)],
        [(80,168),(83,192),(56,211),(70,228)],
        [(80,168),(81,192),(46,212),(59,228)],
        [(80,168),(85,188),(45,208),(54,220)],
        [(80,168),(96,188),(63,201),(73,211)],
        [(80,168),(97,190),(86,215),(102,228)],
        [(80,168),(93,190),(80,213),(96,228)],
    ],
    'FF': [
        [(140,164),(135,190),(116,209),(123,222)],
        [(140,164),(142,186),(122,198),(131,210)],
        [(140,164),(156,184),(140,195),(151,202)],
        [(140,164),(159,187),(164,207),(175,215)],
        [(140,164),(150,188),(158,209),(170,222)],
        [(140,164),(145,190),(146,209),(157,222)],
        [(140,164),(139,190),(133,209),(142,222)],
        [(140,164),(135,190),(118,209),(128,222)],
    ],
    'FH': [
        [(69,164),(71,184),(42,203),(48,214)],
        [(69,164),(82,182),(54,196),(64,207)],
        [(69,164),(86,186),(80,207),(91,222)],
        [(69,164),(81,187),(71,207),(84,222)],
        [(69,164),(76,188),(63,207),(75,222)],
        [(69,164),(72,188),(53,207),(65,222)],
        [(69,164),(69,188),(43,207),(55,222)],
        [(69,164),(68,187),(37,207),(47,222)],
    ],
}
STATES = {
    'NF': ['land','support','support','support','support','lift','swing','reach'],
    'NH': ['support','support','support','push','lift','swing','land','support'],
    'FF': ['support','lift','swing','reach','land','support','support','support'],
    'FH': ['lift','swing','land','support','support','support','support','push'],
}
COLORS = {'NF':'#1762c5','NH':'#128458','FF':'#9bb3d0','FH':'#a3c5b5'}
font = ImageFont.truetype('C:/Windows/Fonts/arial.ttf', 17)
small = ImageFont.truetype('C:/Windows/Fonts/arial.ttf', 14)

for direction in ('Left','Right'):
    # Repack the complete clean P20 identity frame, without masks/body edits.
    identity = Image.open(OUT / f'previous-runtime/Walk{direction}/walk_{direction.lower()}_000.png').convert('RGBA')
    identity = identity.resize((346,346), Image.Resampling.LANCZOS)
    target = Image.new('RGBA', SIZE)
    diagram = Image.new('RGB', SIZE, '#f4f7fa')
    draw = ImageDraw.Draw(diagram)
    def point(pt, col, row):
        x,y=pt
        if direction == 'Left': x=256-x
        return (round(col*384+ORIGIN[0]+x*FACTOR),round(row*512+ORIGIN[1]+y*FACTOR))
    for phase in range(8):
        row,col=divmod(phase,4)
        target.paste(identity,(col*384+ORIGIN[0],row*512+ORIGIN[1]))
        draw.text((col*384+14,row*512+16),f'{phase}: video {47+phase*2}  {direction.upper()}',font=font,fill='#203441')
        draw.text((col*384+14,row*512+46),'NF near fore / NH near hind',font=small,fill='#203441')
        draw.text((col*384+14,row*512+66),'FF far fore / FH far hind',font=small,fill='#203441')
        draw.line([point((69,164),col,row),point((152,168),col,row)],fill='#677a86',width=8)
        for key in ('FH','FF','NH','NF'):
            pts=[point(p,col,row) for p in PATHS[key][phase]]
            width=4 if key.startswith('F') else 6
            if key in ('NF','NH'): width=6
            draw.line(pts,fill=COLORS[key],width=width,joint='curve')
            for p in pts[:-1]:
                draw.ellipse((p[0]-3,p[1]-3,p[0]+3,p[1]+3),fill=COLORS[key])
            toe=pts[-1]
            radius=6
            ground=STATES[key][phase] in ('land','support','push')
            draw.ellipse((toe[0]-radius,toe[1]-radius,toe[0]+radius,toe[1]+radius),
                         fill=COLORS[key] if ground else '#f4f7fa',outline=COLORS[key],width=2)
        draw.line([point((25,228),col,row),point((212,228),col,row)],fill='#c4ccd3',width=1)
        for n,key in enumerate(('NF','NH','FF','FH')):
            draw.text((col*384+16+(n%2)*179,row*512+427+(n//2)*24),
                      f'{key}: {STATES[key][phase]}',font=small,fill=COLORS[key])
    target.save(OUT / f'Walk{direction}-identity-target.png')
    diagram.save(OUT / f'Walk{direction}-four-paw-guide.png')

# The authored left character has different occlusion topology from right.
# Its broad outer NH root is farther right, under the tail (194), while
# the smaller inner FH root is under the belly (176). A simple mirror
# assigned those legs to the wrong roots. Keep the measured phase order.
left_paths={key:[] for key in PATHS}
for key,poses in PATHS.items():
    offset=18 if key=='NH' else -11 if key=='FH' else 0
    left_paths[key]=[[(256-x+offset,y) for x,y in pose] for pose in poses]
diagram=Image.new('RGB',SIZE,'#f4f7fa')
draw=ImageDraw.Draw(diagram)
for phase in range(8):
    row,col=divmod(phase,4)
    def left_point(p):
        return (round(col*384+ORIGIN[0]+p[0]*FACTOR),round(row*512+ORIGIN[1]+p[1]*FACTOR))
    draw.text((col*384+14,row*512+16),f'{phase}: LEFT / video {47+2*phase}',font=font,fill='#203441')
    draw.text((col*384+14,row*512+46),'NH = broad outer rump leg, hip194',font=small,fill=COLORS['NH'])
    draw.text((col*384+14,row*512+68),'FH = small inner belly leg, hip176',font=small,fill=COLORS['FH'])
    draw.line([left_point((194,168)),left_point((104,168))],fill='#677a86',width=8)
    for key in ('FH','FF','NH','NF'):
        pts=[left_point(p) for p in left_paths[key][phase]]
        draw.line(pts,fill=COLORS[key],width=8 if key in ('NH','NF') else 3,joint='curve')
        for p in pts[:-1]:draw.ellipse((p[0]-3,p[1]-3,p[0]+3,p[1]+3),fill=COLORS[key])
        toe=pts[-1];ground=STATES[key][phase] in ('land','support','push')
        draw.ellipse((toe[0]-6,toe[1]-6,toe[0]+6,toe[1]+6),
                     fill=COLORS[key] if ground else '#f4f7fa',outline=COLORS[key],width=2)
    draw.line([left_point((40,228)),left_point((232,228))],fill='#c4ccd3',width=1)
    for n,key in enumerate(('NF','NH','FF','FH')):
        draw.text((col*384+16+(n%2)*179,row*512+427+(n//2)*24),f'{key}: {STATES[key][phase]}',
                  font=small,fill=COLORS[key])
    diagram.crop((col*384,row*512,(col+1)*384,(row+1)*512)).save(OUT/f'single-inputs/Left-{phase}-guide-v2.png')
diagram.save(OUT/'WalkLeft-four-paw-guide-v2.png')

report={
    'video':str(VIDEO.relative_to(ROOT)),'sha256':hashlib.sha256(VIDEO.read_bytes()).hexdigest(),
    'checked_video_frames':list(range(45,69))+list(range(90,114)),
    'eight_pose_reference_frames':[47,49,51,53,55,57,59,61],
    'fps':15,'cycle_seconds':8/15,'paths':PATHS,'support_states':STATES,
    'left_character_paths':left_paths,
    'left_root_correction':'Near hind outer rump hip194; far hind inner belly hip176. Never determine leg ownership by toe x-order.',
    'coordinate_role':'manually adapted trajectories in original compact character coordinates',
    'uncertainty':'Fur and perspective obscure far-side joints; inferred joints are not measured motion capture.',
    'observed_correction':'Near fore remains grounded through phase4/video55; hind recovery begins first. Preserve each paw identity; no grounded forepaw direction reversal.',
    'input_roles':'Existing pet = appearance. Colored diagrams = motion only. Video appearance never passed to ImageGen.',
}
(OUT/'reference/four-paw-motion.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print('Prepared two complete identity atlases and four independently specified paw guides.')
