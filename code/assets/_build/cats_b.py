from gen_common import *
from cats_a import bike_parts, tree_parts, pine_parts, cloud_parts, rock_parts, lamp_post, fence_parts, bush_parts, bench_parts, hill_parts, road_strip


def building(w, d, h, wall, win='#bfe8ff', roof='#9aa5b1', flat=True):
    P = [b((0, 0, h / 2), (w, d, h), wall), b((0, 0, h + 0.1), (w + 0.3, d + 0.3, 0.2), roof)]
    nz = max(1, int(h // 3)); nx = max(1, int(w // 2.2))
    for iz in range(nz):
        for ix in range(nx):
            x = -w / 2 + (ix + 0.5) * w / nx; z = 1.6 + iz * 3.0
            if z < h - 0.8: P.append(b((x, -d / 2 - 0.02, z), (0.9, 0.05, 1.3), win, bev=False))
    return P


# ================================================================ JUMPING (rooftop jumping)
def Jumping():
    B = Builder('Jumping')
    B.prop('Roof', [b((0, 0, -0.3), (14, 14, 0.6), '#8d96a3'), b((0, 0, 0.03), (13.4, 13.4, 0.06), '#a9b2be', bev=False)] + [b((x, y, 0.25), (14, 0.3, 0.5), '#6e7684') for x, y in ((0, -7), (0, 7))] + [b((x, 0, 0.25), (0.3, 14, 0.5), '#6e7684') for x in (-7, 7)])
    B.prop('TallBuilding', building(8, 8, 28, '#d6a77a')); B.prop('MidBuilding', building(7, 7, 16, '#c9788a')); B.prop('LowBuilding', building(9, 8, 9, '#7aa9d6')); B.prop('Tower', building(6, 6, 40, '#8f7ad6', roof='#3b4350'))
    B.prop('AC', [b((0, 0, 0.4), (1.2, 0.8, 0.8), '#d9dee4'), cy((0.2, -0.41, 0.4), 0.28, 0.04, '#7a828c', (90, 0, 0), 14), b((-0.4, -0.41, 0.4), (0.2, 0.02, 0.5), '#aab4be')])
    B.prop('WaterTank', [cy((0, 0, 2.0), 0.9, 2.0, '#b9825a', seg=14), co((0, 0, 3.4), 1.0, 0.8, '#7a5230', seg=14)] + [cy((x, y, 0.5), 0.05, 1.0, '#3b4350', seg=6) for x, y in ((0.6, 0.6), (-0.6, 0.6), (0.6, -0.6), (-0.6, -0.6))])
    B.prop('Antenna', [cy((0, 0, 2.5), 0.04, 5.0, '#7a828c', seg=6), b((0, 0, 4.2), (1.0, 0.04, 0.04), '#7a828c'), b((0, 0, 3.6), (0.7, 0.04, 0.04), '#7a828c'), sp((0, 0, 5.05), 0.08, '#ff4d6d')])
    B.prop('Crate', [b((0, 0, 0.5), (1.0, 1.0, 1.0), '#c28a54'), b((0, -0.51, 0.5), (0.9, 0.02, 0.12), '#7a5230'), b((0, -0.51, 0.5), (0.12, 0.02, 0.9), '#7a5230')])
    B.prop('Box', [b((0, 0, 0.45), (2.0, 1.0, 0.9), '#e0524a'), b((0, 0, 0.92), (2.0, 1.0, 0.06), '#f4f1ea')])
    B.prop('Ramp', [wd((0, 0, 0.5), (2.4, 3.0, 1.0), '#ffd04a')]); B.prop('Rail', [b((0, 0, 0.9), (3.0, 0.06, 0.06), '#d9dee4')] + [b((x, 0, 0.45), (0.06, 0.06, 0.9), '#7a828c') for x in (-1.4, 0, 1.4)])
    B.prop('Chimney', [b((0, 0, 1.2), (0.9, 0.9, 2.4), '#b0564a'), b((0, 0, 2.45), (1.1, 1.1, 0.12), '#7a828c')])
    B.prop('Billboard', [b((0, 0, 0), (5, 0.2, 2.4), '#ff4d6d'), b((0, -0.12, 0), (4, 0.02, 0.5), '#ffffff'), b((0, -0.12, 0.7), (2, 0.02, 0.3), '#ffd04a'), b((-1.5, 0, -1.6), (0.18, 0.18, 1.6), '#3b4350'), b((1.5, 0, -1.6), (0.18, 0.18, 1.6), '#3b4350')])
    B.prop('Cloud', cloud_parts()); B.prop('Bird', [sp((0, 0, 0), (0.12, 0.2, 0.1), '#3b4350'), b((-0.18, 0, 0.04), (0.3, 0.1, 0.02), '#3b4350', (0, 0, 20)), b((0.18, 0, 0.04), (0.3, 0.1, 0.02), '#3b4350', (0, 0, -20))])
    B.place('Roof'); B.place('Roof', (22, 6, -1.4)); B.place('Roof', (-24, 10, 0.6)); B.place('Ramp', (11, 0, -1.4), 270)
    B.place('Ramp', (-4, -3, 0), 0); B.place('Box', (2, 3, 0), 20); B.place('Rail', (-3, 4, 0), 0); B.place('Crate', (4, -2, 0), 15); B.place('Crate', (4.2, -2, 1.0), 40); B.place('AC', (-5.5, 5.5, 0)); B.place('Chimney', (5.5, 5.5, 0)); B.place('WaterTank', (-5.4, -5.4, 0)); B.place('Antenna', (6, -5.5, 0))
    B.place('Box', (22, 3, -1.4), 90); B.place('Crate', (24, 8, -1.4)); B.place('AC', (-22, 12, 0.6)); B.place('Rail', (-24, 8, 0.6), 90)
    B.place('TallBuilding', (-40, 40, -14)); B.place('Tower', (30, 50, -20)); B.place('MidBuilding', (45, 15, -8)); B.place('LowBuilding', (-45, -5, -4)); B.place('MidBuilding', (0, 50, -8)); B.place('TallBuilding', (55, -30, -14)); B.place('LowBuilding', (-10, -40, -4)); B.place('Tower', (-60, -25, -20))
    B.place('Billboard', (0, 40, 14), 180); B.place('Billboard', (-50, 20, 12), 130)
    for i in range(8): B.place('Cloud', (-70 + i * 18, 70 + (i % 3) * 20, 30 + (i % 4) * 8), i * 20, 2.6, tag='Cloud')
    for i in range(5): B.place('Bird', (-20 + i * 9, 20 + i * 3, 10 + i), i * 40, 1.2, tag='Bird')
    return B.finish()


# ================================================================ LONGBOARDING (downhill mountain road)
def Longboarding():
    B = Builder('Longboarding')
    B.prop('Ground', [cy((0, 0, -0.1), 100, 0.2, '#8fcf7e', seg=36)]); B.prop('Road', road_strip(120, 7))
    B.prop('Board', [b((0, 0, 0.14), (0.26, 1.1, 0.03), '#ff8a1f'), b((0, 0.55, 0.15), (0.26, 0.2, 0.02), '#ff8a1f', (-12, 0, 0)), b((0, -0.55, 0.15), (0.26, 0.2, 0.02), '#ff8a1f', (12, 0, 0))] +
           [cy((x, y, 0.06), 0.06, 0.05, '#f4f1ea', (0, 90, 0), 10) for x in (-0.16, 0.16) for y in (-0.35, 0.35)] + [b((0, y, 0.1), (0.2, 0.06, 0.04), '#7a828c') for y in (-0.35, 0.35)])
    B.prop('Mountain', [co((0, 0, 8), 14, 16, '#7a8aa3', seg=7), co((0, 0, 13), 7, 8, '#f4f7fb', seg=7)]); B.prop('Pine', pine_parts(6.0)); B.prop('Rock', rock_parts('#8a94a3', 1.6)); B.prop('Cloud', cloud_parts())
    B.prop('Guardrail', [b((0, 0, 0.55), (3.0, 0.08, 0.3), '#c6ced6')] + [b((x, 0, 0.3), (0.1, 0.1, 0.6), '#7a828c') for x in (-1.4, 0, 1.4)])
    B.prop('Cone', [co((0, 0, 0.18), 0.14, 0.36, '#ff8a1f'), b((0, 0, 0.02), (0.34, 0.34, 0.04), '#ff8a1f')]); B.prop('Sign', [cy((0, 0, 1.0), 0.04, 2.0, '#9aa5b1'), b((0, 0, 2.0), (0.9, 0.06, 0.9), '#ffd04a', (0, 0, 0)), b((0, -0.04, 2.0), (0.1, 0.02, 0.5), '#3b4350')])
    B.prop('Flag', [cy((0, 0, 1.2), 0.03, 2.4, '#9aa5b1'), b((0.35, 0, 2.1), (0.7, 0.03, 0.4), '#e0524a')]); B.prop('Chalet', [b((0, 0, 1.3), (4, 3.2, 2.6), '#c28a54'), wd((0, 0, 3.0), (4.6, 3.6, 1.4), '#7a3b2e'), b((0, -1.61, 0.8), (0.9, 0.06, 1.6), '#7a5230')])
    B.prop('Bush', bush_parts())
    B.place('Ground'); B.place('Road', (0, 0, 0)); B.place('Board', (-1.0, -1.5, 0), 0)
    for i in range(14): B.place('Guardrail', (4.6, -40 + i * 6, 0), 90, (1, 2, 1)); B.place('Guardrail', (-4.6, -40 + i * 6, 0), 90, (1, 2, 1))
    for y in (-5, 0, 5, 10): B.place('Cone', (-1.5 + (y % 3), y, 0))
    B.place('Sign', (5.4, 8, 0), 180); B.place('Flag', (-5.4, -6, 0))
    B.scatter('Pine', 50, 9, 70, avoid=lambda x, y: abs(x) < 7); B.scatter('Rock', 14, 8, 50, avoid=lambda x, y: abs(x) < 6); B.scatter('Bush', 24, 7, 40, avoid=lambda x, y: abs(x) < 6)
    for x, y, s in ((-50, 70, 3.0), (30, 85, 3.6), (70, 50, 2.6), (-85, 20, 3.0), (0, 100, 4.2)): B.place('Mountain', (x, y, 0), 0, s)
    B.place('Chalet', (-16, 14, 0), 90)
    for i in range(8): B.place('Cloud', (-60 + i * 16, 55 + (i % 3) * 16, 30 + (i % 4) * 6), i * 20, 2.4, tag='Cloud')
    return B.finish()


# ================================================================ MOTOR (race track)
def moto_parts(col='#e0524a'):
    P = wheel(0, -0.7, 0.34, 0.34, '#1e1f24', '#aab4be') + wheel(0, 0.7, 0.34, 0.34, '#1e1f24', '#aab4be')
    P += [sp((0, 0.05, 0.62), (0.2, 0.55, 0.22), col), b((0, -0.35, 0.8), (0.22, 0.5, 0.1), '#2a2a2e'), rod((0, 0.7, 0.34), (0, 0.5, 0.95), 0.06, '#aab4be'), b((0, 0.5, 0.98), (0.6, 0.06, 0.06), '#2a2a2e'),
          sp((0, 0.5, 0.72), (0.18, 0.12, 0.16), '#fff2b0'), cy((0, -0.15, 0.3), 0.12, 0.3, '#7a828c', (0, 90, 0), 10), b((0.2, -0.65, 0.28), (0.06, 0.6, 0.06), '#aab4be')]; return P


def Motor():
    B = Builder('Motor')
    B.prop('Ground', [cy((0, 0, -0.1), 120, 0.2, '#79c97f', seg=36)])
    B.prop('Track', [b((0, 0, 0.02), (12, 140, 0.06), '#3a3f48', bev=False)] + [b((sx * 6.2, 0, 0.05), (0.5, 140, 0.08), '#ffffff' if i % 2 else '#e0524a', bev=False) for sx in (-1, 1) for i in range(1)] +
           [b((sx * 6.2, -70 + i * 3, 0.06), (0.5, 1.5, 0.02), '#e0524a', bev=False) for sx in (-1, 1) for i in range(46)] + [b((0, -70 + i * 6, 0.06), (0.15, 2.5, 0.02), '#ffffff', bev=False) for i in range(23)])
    B.prop('StartLine', [b((0, 0, 0.06), (12, 0.8, 0.02), '#ffffff', bev=False)] + [b((-5.5 + ix * 1.0, -0.2 + (ix % 2) * 0.4, 0.075), (0.5, 0.4, 0.01), '#1e1f24', bev=False) for ix in range(12)])
    B.prop('Bike', moto_parts('#e0524a')); B.prop('BikeBlue', moto_parts('#4a86e8'))
    B.prop('TyreStack', [cy((0, 0, 0.15 + i * 0.3), 0.4, 0.28, '#1e1f24' if i % 2 else '#e0524a', seg=12) for i in range(3)])
    B.prop('Barrier', [b((0, 0, 0.5), (3.0, 0.3, 1.0), '#f4f1ea'), b((0, -0.16, 0.5), (3.0, 0.02, 0.3), '#e0524a')])
    B.prop('Grandstand', [b((0, 0, 0.5 + i * 0.7), (16, 2.0, 0.8), c) for i, c in enumerate(('#7a828c', '#8d96a3', '#a0a9b6', '#b3bcc8'))] + [sp((x, -0.5 + (i % 2) * 0.2, 1.1 + (i // 6) * 0.7), (0.18, 0.18, 0.22), c) for i, (x, c) in enumerate(zip([-7 + k * 1.2 for k in range(12)], ['#ff4d6d', '#ffd04a', '#4fc3b0', '#4a86e8'] * 3))] +
           [cy((x, 0.9, 3.0), 0.08, 6.0, '#3b4350', seg=6) for x in (-7.8, 7.8)] + [b((0, 0.9, 6.0), (16.4, 3.2, 0.2), '#e0524a')])
    B.prop('Arch', [b((-7.0, 0, 3.0), (0.6, 0.6, 6.0), '#3b4350'), b((7.0, 0, 3.0), (0.6, 0.6, 6.0), '#3b4350'), b((0, 0, 6.0), (14.6, 0.7, 1.4), '#e0524a'), b((0, -0.4, 6.0), (10, 0.02, 0.6), '#ffffff')])
    B.prop('Pit', [b((0, 0, 1.5), (10, 5, 3), '#f4f1ea'), b((0, -2.6, 2.5), (10, 0.1, 1.0), '#e0524a')] + [b((x, -2.56, 1.0), (1.8, 0.06, 1.8), '#3b4350') for x in (-3.5, -1.2, 1.2, 3.5)])
    B.prop('Flag', [cy((0, 0, 1.5), 0.03, 3.0, '#9aa5b1'), b((0.5, 0, 2.7), (1.0, 0.03, 0.6), '#4a86e8')]); B.prop('Cone', [co((0, 0, 0.18), 0.14, 0.36, '#ff8a1f'), b((0, 0, 0.02), (0.34, 0.34, 0.04), '#ff8a1f')])
    B.prop('LightPole', lamp_post()); B.prop('Tree', tree_parts(5.0)); B.prop('Cloud', cloud_parts())
    B.place('Ground'); B.place('Track', (0, 0, 0)); B.place('StartLine', (0, 4, 0)); B.place('Arch', (0, 4, 0)); B.place('Bike', (-2.0, -3.0, 0.02), 0); B.place('BikeBlue', (2.0, -5.0, 0.02), 0)
    for i in range(14): B.place('Barrier', (7.4, -40 + i * 6, 0), 90, (1, 2, 1)); B.place('Barrier', (-7.4, -40 + i * 6, 0), 90, (1, 2, 1))
    for y in (-30, -12, 12, 30): B.place('TyreStack', (6.8, y, 0)); B.place('TyreStack', (-6.8, y + 2, 0))
    B.place('Grandstand', (-16, 12, 0), 90); B.place('Grandstand', (16, -10, 0), -90); B.place('Pit', (-14, -14, 0), 90)
    for i in range(6): B.place('Flag', (-9 + (i % 2) * 18, -20 + i * 8, 0))
    for y in (-26, 0, 26): B.place('LightPole', (9.5, y, 0), 180)
    B.scatter('Tree', 26, 30, 90); B.scatter('Cone', 6, 3, 9, avoid=lambda x, y: False, zr=0.0)
    for i in range(7): B.place('Cloud', (-55 + i * 18, 50 + (i % 3) * 16, 28 + (i % 4) * 6), i * 20, 2.4, tag='Cloud')
    return B.finish()


# ================================================================ PARKOUR (outdoor park + bike)
def Parkour():
    B = Builder('Parkour')
    B.prop('Ground', [cy((0, 0, -0.1), 90, 0.2, '#79c97f', seg=36)]); B.prop('Plaza', [b((0, 0, 0.02), (26, 26, 0.06), '#d9c9a8', bev=False)] + [b((-12 + i * 2, y, 0.06), (0.05, 26, 0.01), '#c4b28e', bev=False) for i in range(13) for y in (0,)])
    B.prop('Path', [b((0, 0, 0.02), (3.0, 80, 0.06), '#cfc0a0', bev=False)])
    B.prop('Bike', bike_parts('#ff8a1f')); B.prop('BikeRack', [b((0, 0, 0.3), (2.0, 0.06, 0.06), '#7a828c')] + [b((x, 0, 0.15), (0.06, 0.06, 0.3), '#7a828c') for x in (-0.8, 0, 0.8)] + [b((x, 0, 0.5), (0.06, 0.06, 0.4), '#7a828c', (0, 0, 0)) for x in (-0.4, 0.4)])
    B.prop('Box', [b((0, 0, 0.5), (2, 1, 1.0), '#e8dcc4'), b((0, 0, 1.02), (2.1, 1.1, 0.08), '#c4b28e')]); B.prop('LowWall', [b((0, 0, 0.6), (4, 0.5, 1.2), '#c9788a'), b((0, 0, 1.24), (4.1, 0.6, 0.08), '#e8dcc4')]); B.prop('Wall', [b((0, 0, 1.4), (5, 0.6, 2.8), '#8d96a3'), b((0, 0, 2.85), (5.1, 0.7, 0.1), '#c6ced6')])
    B.prop('Rail', [b((0, 0, 1.0), (4, 0.08, 0.08), '#e0524a')] + [b((x, 0, 0.5), (0.08, 0.08, 1.0), '#7a828c') for x in (-1.8, 0, 1.8)])
    B.prop('Stairs', [b((0, i * 0.4, 0.15 + i * 0.15), (3.0, 0.4, 0.3 + i * 0.3), '#c4b28e') for i in range(6)])
    B.prop('Bench', bench_parts()); B.prop('Tree', tree_parts(5.0)); B.prop('Bush', bush_parts()); B.prop('LampPost', lamp_post()); B.prop('Cloud', cloud_parts()); B.prop('Rock', rock_parts())
    B.prop('Fountain', [cy((0, 0, 0.25), 2.0, 0.5, '#c6ced6', seg=20), cy((0, 0, 0.4), 1.8, 0.2, '#5fa8d8', seg=20), cy((0, 0, 1.0), 0.3, 1.2, '#c6ced6', seg=10), cy((0, 0, 1.7), 0.8, 0.15, '#c6ced6', seg=14), sp((0, 0, 1.95), 0.3, '#bfe8ff')])
    B.prop('Pillar', [b((0, 0, 1.2), (0.8, 0.8, 2.4), '#e8dcc4'), b((0, 0, 2.45), (1.0, 1.0, 0.12), '#c4b28e')]); B.prop('Bin', [tc((0, 0, 0.45), 0.28, 0.24, 0.9, '#4fc3b0', seg=10), b((0, 0, 0.92), (0.6, 0.6, 0.06), '#3faa98')])
    B.place('Ground'); B.place('Plaza'); B.place('Path', (0, 40, 0)); B.place('Path', (0, -40, 0))
    B.place('Bike', (-2.2, -1.0, 0), 25); B.place('BikeRack', (3.0, -3.5, 0), 0); B.place('Bike', (2.4, -3.5, 0), 90); B.place('Bike', (3.6, -3.5, 0), 90)
    B.place('Box', (-5, 3, 0), 20); B.place('Box', (-7.5, 4, 0), -10); B.place('Box', (-7.5, 4, 1.0), 40); B.place('LowWall', (6, 5, 0), 90); B.place('Wall', (9, 10, 0), 0); B.place('Rail', (-8, -3, 0), 90); B.place('Rail', (4, 8, 0), 30)
    B.place('Stairs', (0, 9, 0), 0); B.place('Pillar', (-3, 10, 0)); B.place('Pillar', (3, 10, 0)); B.place('Fountain', (0, 18, 0)); B.place('Bench', (-6, -8, 0), 90); B.place('Bench', (6, -9, 0), -90)
    for p in ((-10, 12), (10, -3), (-11, -6), (11, 12)): B.place('LampPost', (p[0], p[1], 0), 180 if p[0] > 0 else 0)
    B.place('Bin', (-2.5, -7, 0)); B.place('Bin', (2.5, 12, 0))
    B.scatter('Tree', 30, 16, 70, avoid=lambda x, y: abs(x) < 2.2); B.scatter('Bush', 30, 14, 50); B.scatter('Rock', 10, 20, 55)
    for i in range(8): B.place('Cloud', (-55 + i * 16, 50 + (i % 3) * 15, 28 + (i % 4) * 6), i * 20, 2.3, tag='Cloud')
    return B.finish()


# ================================================================ PLANE (sky)
def plane_parts(col='#e0524a'):
    return [cy((0, 0, 0), 0.7, 5.0, '#f4f1ea', (90, 0, 0), 12), co((0, 3.1, 0), 0.7, 1.2, col, (-90, 0, 0), 12), b((0, 0.3, 0), (8.0, 1.1, 0.12), col), b((0, -2.3, 0.55), (0.12, 0.9, 1.1), col), b((0, -2.3, 0.1), (2.6, 0.6, 0.1), col),
            sp((0, 0.6, 0.45), (0.5, 0.8, 0.4), '#7fd0ff'), cy((0, 3.7, 0), 0.05, 0.1, '#3b4350', (90, 0, 0)), b((0, 3.76, 0), (0.1, 0.06, 1.8), '#3b4350')]


def Plane():
    B = Builder('Plane')
    B.prop('Deck', [cy((0, 0, -0.2), 4.5, 0.4, '#e8dcc4', seg=24), cy((0, 0, 0.05), 4.2, 0.1, '#f4f1ea', seg=24), cy((0, 0, 0.12), 1.6, 0.04, '#ffd04a', seg=24)] + [cy((math.cos(a) * 4.3, math.sin(a) * 4.3, 0.3), 0.06, 0.6, '#7a828c', seg=5) for a in [k * math.pi / 6 for k in range(12)]])
    B.prop('Plane', plane_parts('#e0524a')); B.prop('PlaneBlue', plane_parts('#4a86e8')); B.prop('Cloud', cloud_parts()); B.prop('BigCloud', [sp((0, 0, 0), (3.4, 2.4, 1.6), '#ffffff'), sp((3, 0.3, -0.2), (2.4, 1.8, 1.3), '#ffffff'), sp((-3, -0.2, -0.2), (2.2, 1.6, 1.2), '#ffffff'), sp((0.6, 0.4, 1.0), (2.2, 1.7, 1.4), '#f4f9ff')])
    B.prop('Island', [co((0, 0, -2.0), 4.0, 4.5, '#8a6a4a', (180, 0, 0), 9), cy((0, 0, 0.1), 4.1, 0.5, '#6fbf73', seg=14)] + tree_parts(3.0)[0:0]); B.prop('Palm', [rod((0, 0, 0), (0.3, 0, 2.4), 0.18, '#a07a50')] + [b((0.3 + math.cos(a) * 0.7, math.sin(a) * 0.7, 2.5), (1.4, 0.2, 0.05), '#4aa05a', (0, -15, math.degrees(a))) for a in [k * math.pi / 3 for k in range(6)]])
    B.prop('Balloon', [sp((0, 0, 3.0), (1.2, 1.2, 1.5), '#ff4d6d'), co((0, 0, 1.55), 0.45, 0.9, '#ff4d6d', (180, 0, 0), 10), b((0, 0, 0.5), (0.5, 0.5, 0.4), '#b9825a')] + [rod((x, y, 1.0), (x * 2, y * 2, 2.1), 0.02, '#3b4350') for x, y in ((0.2, 0.2), (-0.2, 0.2), (0.2, -0.2), (-0.2, -0.2))])
    B.prop('Bird', [sp((0, 0, 0), (0.12, 0.2, 0.1), '#3b4350'), b((-0.2, 0, 0.04), (0.35, 0.1, 0.02), '#3b4350', (0, 0, 20)), b((0.2, 0, 0.04), (0.35, 0.1, 0.02), '#3b4350', (0, 0, -20))])
    B.prop('Ring', [cy((math.cos(a) * 2.6, 0, math.sin(a) * 2.6 + 2.6), 0.18, 0.5, '#ffd04a', (0, 0, 0)) for a in [k * math.pi / 8 for k in range(16)]])
    B.prop('Sun', [sp((0, 0, 0), 6.0, '#ffe27a'), sp((0, 0, 0), 8.2, '#fff2b0')])
    B.place('Deck', (0, 0, 0))
    B.place('Plane', (12, 18, 6), 60, 1.6); B.place('PlaneBlue', (-20, 40, 10), -50, 1.6); B.place('Plane', (30, 70, 14), 160, 1.6)
    for i in range(22): B.place('Cloud', (-90 + (i * 37) % 180, -70 + (i * 53) % 220, -8 + (i * 11) % 36), i * 31, 3.4, tag='Cloud')
    for i in range(7): B.place('BigCloud', (-110 + i * 38, 90 + (i % 3) * 20, -6 + (i % 3) * 8), i * 12, 3.4, tag='Cloud')
    for p in ((-22, 22, -3), (26, -12, 2), (-34, -26, 5), (40, 40, -5)): B.place('Island', p, 0, 2.2); B.place('Palm', (p[0] + 1, p[1], p[2] + 0.5), 20, 1.6)
    for i in range(5): B.place('Balloon', (-30 + i * 14, 28 + (i % 2) * 14, 4 + (i % 3) * 5), i * 30, 1.4, tag='Balloon')
    for i in range(6): B.place('Bird', (-18 + i * 7, 12 + (i % 3) * 5, 5 + (i % 4)), i * 30, 1.5, tag='Bird')
    for i in range(5): B.place('Ring', (4 + i * 8, 26 + i * 12, 3 + i * 2), 80 + i * 12, 2.0)
    B.place('Sun', (-90, 140, 70), 0, 2.4)
    return B.finish()
