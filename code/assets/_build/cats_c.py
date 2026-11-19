from gen_common import *
from cats_a import bike_parts, tree_parts, pine_parts, cloud_parts, rock_parts, lamp_post, fence_parts, bush_parts, bench_parts, hill_parts


# ================================================================ ROCK CLIMBING (cliff wall)
def RockClimbing():
    B = Builder('RockClimbing')
    B.prop('Ledge', [cy((0, 0, -0.3), 7.0, 0.6, '#b8a58a', seg=18), cy((0, 0, 0.05), 6.6, 0.1, '#8fcf7e', seg=18)])
    B.prop('CliffBlock', [b((0, 0, 3.0), (6, 3, 6), '#9a8f86'), b((1, -0.4, 4.4), (4, 3, 3.2), '#a89d93'), b((-1.6, -0.6, 2.0), (3, 3, 2.4), '#8a7f76')])
    B.prop('Cliff', [b((x, 0.3 * ((i * 7) % 3), h / 2), (4.4, 3.0 + (i % 2), h), c) for i, (x, h, c) in enumerate(zip((-14 + k * 4 for k in range(8)), (16, 20, 24, 21, 26, 23, 19, 17), ('#9a8f86', '#a89d93', '#8a7f76', '#b0a59a', '#968b82', '#a89d93', '#8a7f76', '#9a8f86')))])
    B.prop('Hold', [sp((0, 0, 0), (0.14, 0.09, 0.1), '#e0524a')]); B.prop('HoldBlue', [sp((0, 0, 0), (0.12, 0.08, 0.1), '#4a86e8')]); B.prop('HoldYellow', [sp((0, 0, 0), (0.13, 0.09, 0.09), '#ffd04a')]); B.prop('HoldGreen', [sp((0, 0, 0), (0.13, 0.09, 0.1), '#3dd98f')])
    B.prop('Rope', [cy((0, 0, 4), 0.025, 8.0, '#ff8a1f', seg=5)] + [sp((0, 0, 0.3 + i * 0.5), (0.06, 0.06, 0.06), '#ffffff') for i in range(3)])
    B.prop('Carabiner', [b((0, 0, 0.1), (0.1, 0.03, 0.2), '#c6ced6', bev=False), b((0, 0, 0.22), (0.1, 0.03, 0.04), '#c6ced6', bev=False)]); B.prop('Helmet', [sp((0, 0, 0.12), (0.18, 0.2, 0.14), '#ffd04a'), b((0, 0.05, 0.05), (0.2, 0.2, 0.02), '#e0b830')])
    B.prop('Tent', [co((0, 0, 0.8), 1.7, 1.6, '#4fc3b0', seg=4), b((0, -0.85, 0.35), (0.7, 0.04, 0.7), '#2a8f80')]); B.prop('Pine', pine_parts(6.0)); B.prop('Rock', rock_parts('#8a7f76', 1.4)); B.prop('Cloud', cloud_parts())
    B.prop('Backpack', [b((0, 0, 0.3), (0.35, 0.22, 0.6), '#e0524a'), b((0, -0.12, 0.3), (0.25, 0.04, 0.3), '#c0463b')]); B.prop('Mountain', [co((0, 0, 9), 16, 18, '#7a8aa3', seg=7), co((0, 0, 14), 7, 8, '#f4f7fb', seg=7)])
    B.place('Ledge'); B.place('Cliff', (0, 6.5, 0), 0, 1.4); B.place('Cliff', (-20, 4, 0), 25, 1.2); B.place('Cliff', (20, 4, 0), -25, 1.2)
    import random as _r; rr = _r.Random(3); holds = ('Hold', 'HoldBlue', 'HoldYellow', 'HoldGreen')
    for i in range(70):
        x = rr.uniform(-6, 6); z = rr.uniform(0.9, 10); B.place(holds[i % 4], (x, 4.9, z), rr.uniform(0, 360), 1.6, tag=holds[i % 4])
    B.place('Rope', (-2.0, 4.8, 0)); B.place('Rope', (2.5, 4.8, 0)); B.place('Tent', (-4.0, -2.0, 0), 30); B.place('Tent', (3.0, -3.5, 0), -20); B.place('Backpack', (-1.5, -1.0, 0), 20); B.place('Helmet', (-0.9, -1.2, 0)); B.place('Carabiner', (-0.5, -1.2, 0))
    B.scatter('Pine', 40, 14, 70, avoid=lambda x, y: abs(x) < 10 and y > -5); B.scatter('Rock', 14, 8, 50)
    for x, y, s in ((-60, 70, 3.0), (60, 80, 3.4), (0, 95, 4.0)): B.place('Mountain', (x, y, 0), 0, s)
    for i in range(7): B.place('Cloud', (-60 + i * 18, 60 + (i % 3) * 14, 30 + (i % 4) * 6), i * 20, 2.4, tag='Cloud')
    return B.finish()


# ================================================================ SCUBA (underwater)
def fish_parts(col='#ff8a1f', fin='#ffd04a'):
    return [sp((0, 0, 0), (0.12, 0.32, 0.16), col), b((0, -0.3, 0), (0.02, 0.18, 0.22), fin, (0, 0, 0)), b((0, 0.02, 0.17), (0.02, 0.16, 0.1), fin), sp((0.08, 0.2, 0.04), 0.03, '#ffffff'), sp((-0.08, 0.2, 0.04), 0.03, '#ffffff'), sp((0.09, 0.22, 0.04), 0.015, '#1e1f24'), sp((-0.09, 0.22, 0.04), 0.015, '#1e1f24')]


def Scuba():
    B = Builder('Scuba')
    B.prop('Seabed', [cy((0, 0, -0.5), 90, 1.0, '#e8d3a0', seg=36)] + [sp((math.cos(a) * r, math.sin(a) * r, 0.0), (6, 5, 0.9), '#dcc690') for a, r in ((k * 0.9, 20 + k * 6) for k in range(12))])
    B.prop('Fish', fish_parts()); B.prop('FishBlue', fish_parts('#4a86e8', '#8fd0ff')); B.prop('FishYellow', fish_parts('#ffd04a', '#ff8a1f')); B.prop('FishPink', fish_parts('#ff6b9e', '#ffb3cf'))
    B.prop('Shark', [sp((0, 0, 0), (0.5, 1.8, 0.5), '#8d96a3'), sp((0, 0.2, -0.15), (0.4, 1.4, 0.3), '#f4f1ea'), b((0, 0.1, 0.55), (0.05, 0.5, 0.55), '#7a828c', (-20, 0, 0)), b((0, -1.7, 0.2), (0.05, 0.5, 0.7), '#7a828c', (30, 0, 0)), b((0.75, 0.3, 0), (0.7, 0.3, 0.05), '#7a828c', (0, 0, -25)), b((-0.75, 0.3, 0), (0.7, 0.3, 0.05), '#7a828c', (0, 0, 25)), sp((0.25, 1.35, 0.15), 0.05, '#1e1f24'), sp((-0.25, 1.35, 0.15), 0.05, '#1e1f24')])
    B.prop('Turtle', [sp((0, 0, 0.25), (0.55, 0.7, 0.28), '#5aa56a'), sp((0, 0, 0.3), (0.4, 0.5, 0.22), '#79c97f'), sp((0, 0.8, 0.25), (0.16, 0.2, 0.14), '#a9d18f'), b((0.6, 0.3, 0.2), (0.5, 0.2, 0.05), '#a9d18f', (0, 0, -30)), b((-0.6, 0.3, 0.2), (0.5, 0.2, 0.05), '#a9d18f', (0, 0, 30)), b((0.4, -0.5, 0.2), (0.3, 0.16, 0.05), '#a9d18f', (0, 0, 30)), b((-0.4, -0.5, 0.2), (0.3, 0.16, 0.05), '#a9d18f', (0, 0, -30))])
    def coral(c1, c2): return [co((0, 0, 0.5), 0.2, 1.0, c1, seg=6)] + [co((x, y, 0.4 + z), 0.13, 0.7, c2, (8 * x * 3, -8 * y * 3, 0), 6) for x, y, z in ((0.3, 0.1, 0), (-0.3, 0.0, 0.05), (0.1, -0.3, 0.0), (-0.1, 0.3, 0.1))] + [sp((0, 0, 0.1), (0.5, 0.5, 0.2), c2)]
    B.prop('CoralPink', coral('#ff6b9e', '#ff9ebf')); B.prop('CoralOrange', coral('#ff8a1f', '#ffc06b')); B.prop('CoralPurple', coral('#8a5cff', '#b79aff'))
    B.prop('BrainCoral', [sp((0, 0, 0.3), (0.6, 0.6, 0.4), '#ffb347'), sp((0.1, 0.1, 0.42), (0.35, 0.35, 0.2), '#ffd08a')]); B.prop('Kelp', [b((0, 0, 0.8 + i * 0.0), (0.16, 0.05, 1.6), '#2f8f5b') for i in range(1)] + [b((0.12, 0.02, 1.6 + 0.2), (0.14, 0.05, 1.5), '#3dad6c', (0, 8, 0)), b((-0.08, 0.0, 2.9), (0.12, 0.05, 1.2), '#2f8f5b', (0, -8, 0))])
    B.prop('Rock', rock_parts('#7d8aa0', 1.6)); B.prop('Starfish', [sp((0, 0, 0.04), (0.14, 0.14, 0.04), '#ff6b4a')] + [b((math.cos(a) * 0.16, math.sin(a) * 0.16, 0.03), (0.09, 0.09, 0.04), '#ff6b4a', (0, 0, math.degrees(a))) for a in [k * math.pi * 2 / 5 for k in range(5)]])
    B.prop('Chest', [b((0, 0, 0.25), (0.8, 0.5, 0.5), '#8a5a36'), b((0, 0, 0.55), (0.8, 0.5, 0.18), '#a06a3e'), b((0, -0.26, 0.4), (0.12, 0.03, 0.14), '#ffd04a'), sp((0, 0, 0.62), (0.3, 0.2, 0.1), '#ffd04a'), b((0, 0, 0.3), (0.84, 0.54, 0.05), '#ffd04a')])
    B.prop('Ship', [b((0, 0, 1.4), (4.0, 10.0, 2.8), '#8a5a36', (6, 0, 4)), wd((0, 5.5, 1.4), (4.0, 3.0, 2.6), '#7a5230'), b((0, -1, 5.5), (0.3, 0.3, 7.0), '#7a5230', (0, 8, 0)), b((0, -1, 4.0), (3.6, 0.1, 2.2), '#d9c9a8', (10, 0, 0)), b((0, 4, 3.6), (0.2, 0.2, 5.0), '#7a5230', (0, -18, 0))])
    B.prop('Anchor', [cy((0, 0, 0.6), 0.05, 1.2, '#7a828c', seg=6), b((0, 0, 1.1), (0.5, 0.05, 0.05), '#7a828c'), b((0, 0, 0.1), (0.8, 0.05, 0.05), '#7a828c', (0, 0, 0)), sp((0, 0, 1.3), 0.1, '#7a828c')])
    B.prop('Jellyfish', [sp((0, 0, 0.5), (0.35, 0.35, 0.25), '#ff9ee0')] + [rod((x, y, 0.4), (x * 1.1, y * 1.1, -0.2), 0.02, '#ffc2ee') for x, y in ((0.12, 0.0), (-0.12, 0.0), (0.0, 0.12), (0.0, -0.12))])
    B.prop('Bubble', [sp((0, 0, 0), 0.1, '#e9fbff')]); B.prop('Seaweed', [b((0, 0, 0.6), (0.1, 0.04, 1.2), '#4fe08f'), b((0.1, 0, 0.5), (0.1, 0.04, 0.9), '#3dd98f', (0, 12, 0)), b((-0.1, 0, 0.45), (0.1, 0.04, 0.8), '#6ff0a8', (0, -12, 0))])
    B.prop('Boat', [b((0, 0, 0.25), (1.6, 4.0, 0.5), '#e0524a'), wd((0, 2.0, 0.25), (1.6, 1.2, 0.5), '#e0524a'), b((0, 0, 0.55), (1.4, 3.6, 0.06), '#f4f1ea')])
    B.place('Seabed')
    for i in range(34): B.scatter(['CoralPink', 'CoralOrange', 'CoralPurple', 'BrainCoral'][i % 4], 1, 5, 50, 1.2, 2.4)
    B.scatter('Kelp', 40, 6, 60, 1.4, 2.4); B.scatter('Seaweed', 40, 4, 45, 1.2, 2.0); B.scatter('Rock', 16, 10, 60, 1.0, 2.4); B.scatter('Starfish', 14, 4, 30, 1.5, 2.5)
    B.place('Ship', (14, 28, 0), 25, 1.2); B.place('Chest', (3.5, 6.0, 0), 160, 1.4); B.place('Anchor', (-6, 8, 0), 30, 1.6); B.place('Turtle', (-7, 14, 3.0), 40, 1.8, tag='Fish'); B.place('Shark', (10, 20, 5.0), 200, 2.0, tag='Fish')
    kinds = ('Fish', 'FishBlue', 'FishYellow', 'FishPink')
    for i in range(32): B.place(kinds[i % 4], (math.cos(i) * (6 + i % 7), math.sin(i) * (6 + i % 7) + 6, 1.4 + (i % 6) * 0.8), i * 37, 1.3, tag='Fish')
    for i in range(6): B.place('Jellyfish', (-12 + i * 5, 16 + (i % 2) * 8, 4.0 + (i % 3)), i * 30, 1.6, tag='Fish')
    for i in range(40): B.place('Bubble', (-4 + (i * 7) % 9, 3 + (i * 5) % 8, 0.5 + (i % 10) * 0.55), 0, 1.0 + (i % 3) * 0.6, tag='Bubble')
    B.place('Boat', (0, 0, 14), 0, 1.0)
    return B.finish()


# ================================================================ SKATE (skate park)
def skateboard_parts(col='#ff4d6d'):
    return [b((0, 0, 0.1), (0.22, 0.75, 0.03), col), b((0, 0.4, 0.12), (0.22, 0.14, 0.02), col, (-14, 0, 0)), b((0, -0.4, 0.12), (0.22, 0.14, 0.02), col, (14, 0, 0))] + [cy((x, y, 0.05), 0.05, 0.04, '#f4f1ea', (0, 90, 0), 8) for x in (-0.14, 0.14) for y in (-0.25, 0.25)]


def Skate():
    B = Builder('Skate')
    B.prop('Ground', [cy((0, 0, -0.1), 90, 0.2, '#79c97f', seg=36)]); B.prop('Concrete', [b((0, 0, 0.02), (46, 40, 0.1), '#bfc6cf', bev=False)] + [b((0, -20 + i * 4, 0.075), (46, 0.04, 0.01), '#a4acb7', bev=False) for i in range(1, 10)])
    B.prop('Skateboard', skateboard_parts()); B.prop('SkateboardBlue', skateboard_parts('#4a86e8'))
    B.prop('QuarterPipe', [wd((0, 0, 0.9), (4.0, 2.6, 1.8), '#8d96a3'), b((0, 1.4, 0.9), (4.0, 0.2, 1.8), '#7a828c'), b((0, 1.2, 1.85), (4.0, 0.12, 0.1), '#e0524a')])
    B.prop('HalfPipe', [wd((-3, 0, 0.9), (0.001 + 2.4, 3.6, 1.8), '#8d96a3'), wd((3, 0, 0.9), (2.4, 3.6, 1.8), '#8d96a3', (0, 0, 180)), b((0, 0, 0.04), (3.7, 3.6, 0.08), '#9aa5b1'), b((-3.8, 0, 1.85), (0.1, 3.6, 0.1), '#e0524a'), b((3.8, 0, 1.85), (0.1, 3.6, 0.1), '#e0524a')])
    B.prop('Funbox', [b((0, 0, 0.4), (3.0, 3.0, 0.8), '#c9788a'), wd((0, -2.0, 0.4), (3.0, 1.2, 0.8), '#c9788a', (0, 0, 180)), wd((0, 2.0, 0.4), (3.0, 1.2, 0.8), '#c9788a'), b((0, 0, 0.84), (3.1, 3.1, 0.06), '#e8dcc4')])
    B.prop('Rail', [b((0, 0, 0.75), (4, 0.08, 0.08), '#ffd04a')] + [b((x, 0, 0.37), (0.08, 0.08, 0.75), '#7a828c') for x in (-1.8, 0, 1.8)]); B.prop('Ledge', [b((0, 0, 0.3), (3.5, 0.7, 0.6), '#d9dee4'), b((0, 0, 0.62), (3.6, 0.75, 0.05), '#e0524a')])
    B.prop('Stairs', [b((0, i * 0.5, 0.15 + i * 0.15), (3.0, 0.5, 0.3 + i * 0.3), '#c6ced6') for i in range(5)]); B.prop('Bowl', [cy((0, 0, 0.02), 5.0, 0.04, '#9aa5b1', seg=24), cy((0, 0, -0.5), 4.2, 1.0, '#3b4350', seg=24), cy((0, 0, 0.0), 5.0, 0.12, '#e0524a', seg=24)])
    B.prop('GraffitiWall', [b((0, 0, 1.5), (8, 0.3, 3), '#8d96a3'), b((-2.4, -0.17, 1.8), (2.0, 0.02, 1.0), '#ff4d6d'), b((0.6, -0.17, 1.5), (2.4, 0.02, 1.4), '#4a86e8'), b((3.0, -0.17, 1.9), (1.4, 0.02, 0.8), '#ffd04a'), sp((-0.5, -0.18, 2.3), (0.5, 0.02, 0.4), '#3dd98f')])
    B.prop('Bin', [tc((0, 0, 0.45), 0.28, 0.24, 0.9, '#4fc3b0', seg=10)]); B.prop('LampPost', lamp_post()); B.prop('Tree', tree_parts(5.0)); B.prop('Bench', bench_parts()); B.prop('Cloud', cloud_parts()); B.prop('Cone', [co((0, 0, 0.18), 0.14, 0.36, '#ff8a1f')])
    B.place('Ground'); B.place('Concrete')
    B.place('QuarterPipe', (-14, 14, 0), 180); B.place('QuarterPipe', (14, 14, 0), 180); B.place('HalfPipe', (0, 16, 0), 90); B.place('Funbox', (-8, -4, 0), 20); B.place('Funbox', (9, -6, 0), -30); B.place('Rail', (0, -8, 0), 0); B.place('Rail', (-14, -3, 0), 90)
    B.place('Ledge', (4, 4, 0), 10); B.place('Ledge', (-5, 5, 0), -15); B.place('Stairs', (16, 2, 0), 90); B.place('Bowl', (-14, -14, 0)); B.place('GraffitiWall', (0, 19.8, 0), 180); B.place('GraffitiWall', (-22, 0, 0), 90)
    B.place('Skateboard', (-1.5, -1.0, 0), 20); B.place('SkateboardBlue', (1.0, -2.5, 0), -50)
    for p in ((-18, 18), (18, 18), (-18, -18), (18, -18)): B.place('LampPost', (p[0], p[1], 0), 180 if p[0] > 0 else 0)
    for p in ((-3, 2), (3, 0), (6, -4)): B.place('Cone', (p[0], p[1], 0))
    B.place('Bench', (-10, 10, 0), 90); B.place('Bin', (10, 10, 0)); B.scatter('Tree', 26, 26, 70)
    for i in range(7): B.place('Cloud', (-55 + i * 18, 50 + (i % 3) * 15, 28 + (i % 4) * 6), i * 20, 2.3, tag='Cloud')
    return B.finish()


# ================================================================ SKI (snow slope)
def Ski():
    B = Builder('Ski')
    B.prop('Snow', [cy((0, 0, -0.2), 95, 0.4, '#f4f9ff', seg=36)] + [sp((math.cos(a) * r, math.sin(a) * r, 0.0), (7, 6, 0.8), '#eaf2fb') for a, r in ((k * 0.8, 15 + k * 5) for k in range(14))])
    B.prop('PineSnow', pine_parts(5.5, '#2f8f5b', '#6b4a2f', '#f4f9ff')); B.prop('Mountain', [co((0, 0, 9), 16, 18, '#7a8aa3', seg=7), co((0, 0, 14), 7, 8, '#f4f7fb', seg=7)]); B.prop('Rock', rock_parts('#8a94a3', 1.4)); B.prop('Cloud', cloud_parts())
    B.prop('Skis', [b((x, 0, 0.03), (0.1, 1.7, 0.03), '#e0524a') for x in (-0.12, 0.12)] + [b((x, 0.85, 0.06), (0.1, 0.25, 0.03), '#e0524a', (-20, 0, 0)) for x in (-0.12, 0.12)] + [b((0, 0, 0.07), (0.34, 0.3, 0.06), '#3b4350')])
    B.prop('Poles', [rod((0, 0, 0), (0.1, 0, 1.3), 0.025, '#3b4350'), cy((0.1, 0, 1.3), 0.04, 0.2, '#ff8a1f')]); B.prop('Flag', [cy((0, 0, 1.2), 0.03, 2.4, '#9aa5b1'), b((0.35, 0, 2.1), (0.7, 0.03, 0.4), '#ff8a1f')])
    B.prop('Lodge', [b((0, 0, 1.6), (8, 5, 3.2), '#b9825a'), wd((0, 0, 3.7), (8.8, 5.6, 1.8), '#f4f9ff'), b((0, -2.51, 1.0), (1.2, 0.06, 2.0), '#7a5230'), b((-2.5, -2.51, 1.8), (1.4, 0.06, 1.0), '#ffe8a0'), b((2.5, -2.51, 1.8), (1.4, 0.06, 1.0), '#ffe8a0'), b((2.5, 0, 4.4), (0.8, 0.8, 1.6), '#8a5a36')])
    B.prop('LiftPylon', [cy((0, 0, 4), 0.14, 8, '#7a828c', seg=6), b((0, 0, 8.0), (3.0, 0.2, 0.2), '#7a828c'), cy((-1.4, 0, 7.8), 0.1, 0.4, '#3b4350', (90, 0, 0)), cy((1.4, 0, 7.8), 0.1, 0.4, '#3b4350', (90, 0, 0))]); B.prop('Chair', [b((0, 0, 0.5), (0.9, 0.5, 0.1), '#e0524a'), b((0, 0.22, 0.85), (0.9, 0.08, 0.7), '#e0524a'), rod((0, 0, 0.55), (0, 0, 2.4), 0.04, '#3b4350')])
    B.prop('Snowman', [sp((0, 0, 0.4), 0.45, '#ffffff'), sp((0, 0, 1.0), 0.33, '#ffffff'), sp((0, 0, 1.45), 0.24, '#ffffff'), co((0, -0.24, 1.45), 0.05, 0.25, '#ff8a1f', (90, 0, 0), 6), sp((0.08, -0.2, 1.52), 0.025, '#1e1f24'), sp((-0.08, -0.2, 1.52), 0.025, '#1e1f24'), cy((0, 0, 1.68), 0.2, 0.18, '#3b4350', seg=10), b((0, 0, 1.2), (0.5, 0.1, 0.08), '#e0524a')])
    B.prop('Fence', [b((x, 0, 0.5), (0.08, 0.08, 1.0), '#ff8a1f') for x in (-1.2, 0, 1.2)] + [b((0, 0, 0.6), (2.6, 0.04, 0.4), '#ff8a1f', bev=False)]); B.prop('Igloo', [sp((0, 0, 0), (1.6, 1.6, 1.3), '#f4f9ff'), b((0, -1.5, 0.35), (0.7, 0.8, 0.7), '#e6eef8')])
    B.place('Snow')
    B.scatter('PineSnow', 60, 12, 80, avoid=lambda x, y: abs(x) < 6 and y > -10); B.scatter('Rock', 14, 8, 60)
    for i in range(10): B.place('Fence', (-6, -30 + i * 6, 0), 90); B.place('Fence', (6, -27 + i * 6, 0), 90)
    for y in (-14, -6, 4, 12, 22): B.place('Flag', (-2.5 + (y % 3) * 2.5, y, 0), 0)
    B.place('Skis', (-1.0, -2.0, 0), 20); B.place('Poles', (-0.4, -2.6, 0), 10); B.place('Poles', (0.2, -2.7, 0), 0); B.place('Snowman', (3.0, 5.0, 0), 160); B.place('Lodge', (-18, 6, 0), 90); B.place('Igloo', (14, -8, 0), 120)
    for i in range(5): B.place('LiftPylon', (14, -20 + i * 14, 0), 0, 1.0)
    for i in range(4): B.place('Chair', (14, -14 + i * 14, 6.5), 90)
    for x, y, s in ((-60, 70, 3.0), (30, 85, 3.6), (75, 40, 2.6), (-85, 10, 3.0), (0, 100, 4.2)): B.place('Mountain', (x, y, 0), 0, s)
    for i in range(7): B.place('Cloud', (-60 + i * 18, 55 + (i % 3) * 14, 30 + (i % 4) * 6), i * 20, 2.4, tag='Cloud')
    return B.finish()


# ================================================================ SURFING (beach)
def Surfing():
    B = Builder('Surfing')
    B.prop('Sand', [b((0, -10, -0.1), (120, 40, 0.3), '#f0d9a0', bev=False), b((0, 14, -0.3), (120, 30, 0.2), '#e8cf90', bev=False)])
    B.prop('Water', [b((0, 0, -0.1), (160, 100, 0.3), '#3fb4d8', bev=False), b((0, -40, 0.1), (160, 12, 0.06), '#7fe0f0', bev=False)])
    B.prop('Wave', [wd((0, 0, 1.0), (10, 4, 2.0), '#2f9cc4', (0, 0, 180)), b((0, -1.2, 2.05), (10, 1.0, 0.25), '#ffffff'), b((0, 0.4, 0.2), (10, 5, 0.4), '#3fb4d8', bev=False)])
    B.prop('Foam', [sp((0, 0, 0.05), (2.0, 0.6, 0.08), '#ffffff'), sp((1.8, 0.2, 0.05), (1.4, 0.5, 0.07), '#ffffff'), sp((-1.8, -0.1, 0.05), (1.3, 0.45, 0.07), '#ffffff')])
    def board(c1, c2): return [sp((0, 0, 0.06), (0.28, 1.1, 0.05), c1), b((0, 0, 0.1), (0.04, 1.8, 0.01), c2, bev=False), co((0, -0.95, 0.07), 0.05, 0.16, '#3b4350', (-90, 0, 0), 5)]
    B.prop('Surfboard', board('#ffd04a', '#ff4d6d')); B.prop('SurfboardBlue', board('#4fc3b0', '#ffffff')); B.prop('SurfboardPink', board('#ff6b9e', '#ffd04a'))
    B.prop('Palm', [rod((0, 0, 0), (0.4, 0, 4.0), 0.3, '#a07a50')] + [b((0.4 + math.cos(a) * 1.2, math.sin(a) * 1.2, 4.1), (2.4, 0.3, 0.06), '#3dad6c', (0, -18, math.degrees(a))) for a in [k * math.pi / 3 for k in range(6)]] + [sp((0.4, 0.1, 3.85), 0.16, '#7a5230'), sp((0.55, -0.1, 3.8), 0.15, '#7a5230')])
    B.prop('Umbrella', [cy((0, 0, 1.1), 0.03, 2.2, '#f4f1ea', seg=5), co((0, 0, 2.3), 1.3, 0.5, '#ff4d6d', seg=8), co((0, 0, 2.28), 0.9, 0.42, '#ffffff', seg=8)])
    B.prop('Towel', [b((0, 0, 0.015), (0.8, 1.7, 0.03), '#4a86e8', bev=False), b((0, 0, 0.032), (0.8, 0.3, 0.01), '#ffffff', bev=False), b((0, 0.4, 0.032), (0.8, 0.3, 0.01), '#ffffff', bev=False)])
    B.prop('Lifeguard', [b((x, y, 1.0), (0.14, 0.14, 2.0), '#f4f1ea') for x in (-1.0, 1.0) for y in (-0.9, 0.9)] + [b((0, 0, 2.0), (2.6, 2.2, 0.15), '#ff4d6d'), b((0, 0.9, 2.6), (2.6, 0.12, 1.0), '#f4f1ea'), co((0, 0, 3.6), 2.0, 1.0, '#ff4d6d', seg=4), wd((0, -1.8, 1.0), (1.0, 3.0, 1.9), '#c28a54', (0, 0, 180))])
    B.prop('Crab', [sp((0, 0, 0.08), (0.14, 0.1, 0.07), '#ff6b4a'), sp((0.16, 0.12, 0.14), 0.05, '#ff6b4a'), sp((-0.16, 0.12, 0.14), 0.05, '#ff6b4a')] + [b((x, y, 0.04), (0.1, 0.02, 0.02), '#ff6b4a') for x in (-0.14, 0.14) for y in (-0.05, 0.0, 0.05)])
    B.prop('Sandcastle', [b((0, 0, 0.25), (1.0, 1.0, 0.5), '#e8c98a'), cy((-0.5, -0.5, 0.45), 0.16, 0.9, '#e8c98a'), cy((0.5, 0.5, 0.45), 0.16, 0.9, '#e8c98a'), co((-0.5, -0.5, 1.0), 0.2, 0.22, '#ff4d6d', seg=8), co((0.5, 0.5, 1.0), 0.2, 0.22, '#4a86e8', seg=8)])
    B.prop('Rock', rock_parts('#7d8aa0', 1.8)); B.prop('Cloud', cloud_parts()); B.prop('Buoy', [sp((0, 0, 0.25), (0.28, 0.28, 0.4), '#ff4d6d'), cy((0, 0, 0.3), 0.29, 0.12, '#ffffff', seg=10), cy((0, 0, 0.75), 0.02, 0.5, '#3b4350', seg=5)])
    B.prop('Boat', [b((0, 0, 0.3), (1.6, 4.2, 0.6), '#f4f1ea'), wd((0, 2.1, 0.3), (1.6, 1.2, 0.6), '#f4f1ea'), b((0, 0, 0.62), (1.5, 3.7, 0.06), '#ff4d6d'), rod((0, 0, 0.6), (0, 0, 3.8), 0.06, '#7a5230'), b((0, 0.7, 2.4), (0.04, 1.4, 2.0), '#ffffff', (0, 0, 0))])
    B.prop('Hut', [b((0, 0, 1.2), (4, 3, 2.4), '#e8c98a'), wd((0, 0, 2.8), (4.6, 3.6, 1.3), '#a07a50'), b((0, -1.51, 0.8), (1.0, 0.06, 1.6), '#7a5230'), b((1.2, -1.51, 1.5), (1.0, 0.06, 0.7), '#bfe8ff')])
    B.place('Sand', (0, 0, 0)); B.place('Water', (0, 62, 0))
    for i in range(12): B.place('Wave', (-55 + i * 10, 16 + (i % 3) * 12, 0), 180 if False else 0, 1.0, tag='Wave')
    for i in range(10): B.place('Foam', (-50 + i * 11, 9 + (i % 2) * 2, 0), 0, 1.4)
    B.place('Surfboard', (-2.2, -2.0, 0.0), 20); B.place('SurfboardBlue', (-0.6, -2.6, 0.0), 10); B.place('SurfboardPink', (1.4, -2.2, 0.0), 160)
    B.place('Umbrella', (-5, -4, 0), 0); B.place('Umbrella', (6, -6, 0), 20); B.place('Towel', (-5.6, -3.5, 0), 20); B.place('Towel', (5.4, -5.2, 0), -30); B.place('Lifeguard', (12, -3, 0), 180); B.place('Hut', (-16, -12, 0), 20); B.place('Sandcastle', (3.0, -5.0, 0), 20); B.place('Crab', (0.0, 3.0, 0), 40); B.place('Crab', (2.0, 5.0, 0), -20)
    B.scatter('Palm', 16, 8, 50, 1.0, 1.5, ang=(180, 360)); B.scatter('Rock', 10, 20, 45, 1.0, 2.0, ang=(180, 360))
    for i in range(6): B.place('Buoy', (-30 + i * 12, 22 + (i % 2) * 10, 0.1), 0, 1.3, tag='Buoy')
    B.place('Boat', (24, 60, 0), 70, 1.5)
    for i in range(8): B.place('Cloud', (-60 + i * 16, 60 + (i % 3) * 18, 28 + (i % 4) * 6), i * 20, 2.4, tag='Cloud')
    return B.finish()
