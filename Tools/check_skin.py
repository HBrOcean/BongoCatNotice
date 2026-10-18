#!/usr/bin/env python3
"""
BongoCatNotice 皮肤自检工具 / Skin checker

用法:
    python check_skin.py Assets/skin/FooLeft.png Assets/skin/FooLeftPunch.png \
                          Assets/skin/FooRight.png Assets/skin/FooRightPunch.png
    python check_skin.py --prefix Assets/skin/Foo      # 自动找四帧
    python check_skin.py Assets/skin/FooLeft.png       # 只查一帧也可以

依赖: Pillow  (pip install pillow)

输出为英文,避免 Windows 控制台中文乱码。
"""
import sys
import os
import argparse

try:
    from PIL import Image
except ImportError:
    print("ERROR: Pillow not installed.  Run:  pip install pillow")
    sys.exit(2)

FRAMES = ['Left', 'LeftPunch', 'Right', 'RightPunch']

# 227 套官方皮肤的统计结果(见 docs/custom-skin-guide.md 第 9 章)
TEMPLATE = {
    'Left':       (22, 90, 247, 196),
    'LeftPunch':  (9, 90, 247, 236),
    'Right':      (164, 129, 218, 196),
    'RightPunch': (149, 166, 242, 237),
}
TOL_MATCH = 3      # 模板偏差视为一致
TOL_WARN = 15      # 偏差提示阈值

results = []       # (level, message)


def add(level, msg):
    results.append((level, msg))
    print(f"      [{level}] {msg}")


def frame_of(path):
    base = os.path.basename(path)
    stem = base[:-4] if base.lower().endswith('.png') else base
    for f in sorted(FRAMES, key=len, reverse=True):
        if stem.endswith(f):
            return f
    return None


def inspect(path):
    name = os.path.basename(path)
    print(f"\n[{name}]")

    if not os.path.exists(path):
        add('FAIL', f'file not found: {path}')
        return None

    try:
        im = Image.open(path)
    except Exception as e:
        add('FAIL', f'cannot open image: {e}')
        return None

    # 1. 尺寸
    if im.size != (256, 256):
        add('FAIL', f'size is {im.size[0]}x{im.size[1]}, expected 256x256')
    else:
        add('OK', 'size 256x256')

    # 2. 透明通道
    has_alpha = ('A' in im.mode) or (im.mode == 'P' and 'transparency' in im.info)
    if has_alpha:
        add('OK', f'alpha channel present (mode {im.mode})')
    else:
        add('FAIL', f'no alpha channel (mode {im.mode}) - transparent background required')

    rgba = im.convert('RGBA')
    alpha = rgba.split()[-1]

    # 3. 不能全透明
    bbox = alpha.getbbox()
    if bbox is None:
        add('FAIL', 'image is fully transparent - window would have zero size')
        return None

    # 4. 不能全不透明(说明没有透明背景)
    lo, hi = alpha.getextrema()
    if lo > 250:
        add('FAIL', 'image has no transparent pixel - background must be transparent')
    hist = alpha.histogram()
    area = sum(hist[9:])          # alpha > 8 的像素数
    add('OK', f'bbox {bbox}  opaque_area {area}')

    # 5. 模板偏差
    frame = frame_of(path) or 'Left'
    t = TEMPLATE.get(frame)
    if t:
        d = tuple(abs(a - b) for a, b in zip(bbox, t))
        worst = max(d)
        tag = 'MATCH' if worst <= TOL_MATCH else ('near' if worst <= TOL_WARN else 'off-template')
        level = 'OK' if worst <= TOL_WARN else 'WARN'
        add(level, f'template[{frame}] {t}  delta dx0={d[0]} dy0={d[1]} dx1={d[2]} dy1={d[3]}  -> {tag}')

    return {'path': path, 'bbox': bbox, 'alpha': alpha, 'frame': frame}


def main():
    ap = argparse.ArgumentParser(description='BongoCatNotice skin checker')
    ap.add_argument('files', nargs='*', help='png files (1-4)')
    ap.add_argument('--prefix', help='auto-locate <prefix>Left/LeftPunch/Right/RightPunch.png')
    args = ap.parse_args()

    paths = list(args.files)
    if args.prefix:
        for f in FRAMES:
            p = args.prefix + f + '.png'
            if os.path.exists(p):
                paths.append(p)
    if not paths:
        ap.print_help()
        return 2

    print('=' * 62)
    print(' BongoCatNotice skin checker')
    print('=' * 62)

    info = {}
    for p in paths:
        r = inspect(p)
        if r:
            info[r['frame']] = r

    # 6. 四帧对齐
    print('\n-- cross-frame checks --')
    if 'Left' in info and 'LeftPunch' in info:
        lt = info['Left']['bbox'][1]
        pt = info['LeftPunch']['bbox'][1]
        if abs(lt - pt) > 2:
            add('WARN', f'Left top y={lt} vs LeftPunch top y={pt} differ by {abs(lt-pt)}px '
                        f'- head will jump when punching. Keep head position identical.')
        else:
            add('OK', f'Left / LeftPunch top edge aligned (y={lt})')

        lh = info['Left']['bbox'][3]
        ph = info['LeftPunch']['bbox'][3]
        stroke = ph - lh
        if stroke <= 0:
            add('WARN', f'LeftPunch bottom ({ph}) is not lower than rest ({lh}) - no punch motion visible')
        elif stroke < 15:
            add('WARN', f'punch stroke only {stroke}px (reference ~40px) - motion may look weak')
        elif stroke > 70:
            add('WARN', f'punch stroke {stroke}px (reference ~40px) - motion may look excessive')
        else:
            add('OK', f'punch stroke {stroke}px (reference ~40px)')

    if 'Left' in info and 'Right' in info:
        lb, rb = info['Left']['bbox'], info['Right']['bbox']
        inside = (rb[0] >= lb[0] - 2 and rb[1] >= lb[1] - 2
                  and rb[2] <= lb[2] + 2 and rb[3] <= lb[3] + 2)
        if inside:
            add('OK', 'Right layer sits inside Left bounds')
        else:
            add('WARN', f'Right bbox {rb} exceeds Left bbox {lb} - '
                        f'right paw layer normally stays within the body bounds')

    # 7. 汇总
    fails = sum(1 for lv, _ in results if lv == 'FAIL')
    warns = sum(1 for lv, _ in results if lv == 'WARN')
    oks = sum(1 for lv, _ in results if lv == 'OK')
    print('\n' + '=' * 62)
    print(f' RESULT:  OK={oks}  WARN={warns}  FAIL={fails}')
    if fails:
        print(' Fix all FAIL items before registering the skin.')
    elif warns:
        print(' No blocking issue. WARN items are style hints - check the preview visually.')
    else:
        print(' All checks passed.')
    print('=' * 62)
    return 1 if fails else 0


if __name__ == '__main__':
    sys.exit(main())
