# Crops raw VM captures (E:\VMs\shots\<lang>, from shots-lang.ps1) into screenshots/<lang>/
# Usage (WSL): python3 crop_shots.py en de es fr ru
import sys, glob, os
from PIL import Image, ImageChops
W, H, TASKBAR = 3840, 2160, 2112
ICON_RIGHT = 3703   # right edge of the pinned tray icon's hover box
def load(p): return Image.open(p).convert('RGB')
def bbox(a, b, limit_y=H):
    # Ignore faint changes and the outermost 2 px of the screen (edge artifacts).
    d = ImageChops.difference(a, b).convert('L').point(lambda v: 255 if v > 20 else 0)
    bb = d.crop((2, 0, W - 2, limit_y)).getbbox()
    return None if bb is None else (bb[0] + 2, bb[1], bb[2] + 2, bb[3])
def pad(bb, n): return (max(0, bb[0]-n), max(0, bb[1]-n), min(W, bb[2]+n), min(H, bb[3]+n))
def crop_lang(lang, outdir):
    src = f'/mnt/e/VMs/shots/{lang}/'
    os.makedirs(outdir, exist_ok=True)
    base, tray = load(src+'base.png'), load(src+'base-tray.png')
    out = {}
    out['welcome'] = load(src+'welcome.png').crop(bbox(base, load(src+'welcome.png'), TASKBAR))
    for name, file in (('tray-menu','menu'), ('device-checklist','checklist'), ('language-menu','language')):
        # Menu bounds from above the taskbar only (the clock may have ticked), then
        # extend down through the taskbar next to the tray icon, like the old shots.
        im = load(src+file+'.png'); bb = bbox(tray, im, TASKBAR)
        bb = (bb[0], bb[1], max(bb[2], ICON_RIGHT), H)
        out[name] = im.crop(pad(bb, 8))
    for name in ('hotkeys', 'about'):
        im = load(src+name+'.png'); out[name] = im.crop(bbox(tray, im, TASKBAR))
    frames = sorted(glob.glob(src+'toast-frames/f*.png'))
    f0 = load(frames[0]); best = None
    for f in frames:
        bb = bbox(f0, load(f), TASKBAR)
        if bb and (best is None or (bb[2]-bb[0])*(bb[3]-bb[1]) > (best[1][2]-best[1][0])*(best[1][3]-best[1][1])): best = (f, bb)
    out['toast'] = load(best[0]).crop(pad(best[1], 16))
    for name, im in out.items():
        im.save(os.path.join(outdir, name+'.png'), optimize=True)
        print(lang, name, im.size)
    return out
if __name__ == '__main__':
    for lang in sys.argv[1:]:
        repo = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..', '..'))
        crop_lang(lang, os.path.join(repo, 'screenshots', lang))
