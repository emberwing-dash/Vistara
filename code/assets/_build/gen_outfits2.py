"""Remaining 13 category outfits (see gen_outfits.py for the contract). Run inside Blender: import gen_outfits2 as g; g.BUILDERS2['Scuba']()"""
import sys, os, importlib
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import gen_outfits
importlib.reload(gen_outfits)
from gen_outfits import *

SIDES = (('L', 1), ('R', -1))


def helmet(cat, P, col, acc, dark='#22232b', visor=True):
    parts = [sp((0, 0.045, 1.36), (0.255, 0.24, 0.215), col, sub=3, zmin=-0.06), tc((0, 0.045, 1.302), 0.248, 0.248, 0.02, dark, seg=20), b((0, 0.045, 1.572), (0.045, 0.24, 0.010), acc, bev=False)]
    if visor: parts.append(b((0, -0.205, 1.318), (0.19, 0.075, 0.012), dark, (-22, 0, 0)))
    parts += [rod((s * 0.225, 0.03, 1.30), (s * 0.075, -0.07, 1.10), 0.016, dark) for s in (-1, 1)] + [b((0, -0.09, 1.09), (0.065, 0.028, 0.032), dark)]
    piece(cat, 'Helmet', 'Head', parts, P)


def gloves(cat, P, col, cuff, big=False):
    for sd, sx in SIDES:
        p0, p1 = hand(sd, sx)
        parts = [rod(tuple(p0), tuple(p1), 0.05, col, True), rod(tuple(p0 + (p1 - p0) * 0.15), tuple(p0 + (p1 - p0) * 0.35), 0.06, cuff, True)]
        if big: parts = [sp(tuple(p1 * 0.95 + p0 * 0.05), (0.085, 0.085, 0.085), col, sub=3), rod(tuple(p0 - (p1 - p0) * 0.2), tuple(p0 + (p1 - p0) * 0.3), 0.075, cuff, True)]
        piece(cat, 'Glove' + sd, 'hand.' + sd, parts, P)


def kneepads(cat, P, col, acc):
    for sd, sx in SIDES:
        kx = 0.088 * sx
        piece(cat, 'KneePad' + sd, 'shin.' + sd, [sp((kx, -0.078, 0.325), (0.058, 0.03, 0.062), col, sub=3), sp((kx, -0.092, 0.325), (0.04, 0.012, 0.04), acc, sub=3), cy((kx, -0.011, 0.30), 0.066, 0.04, acc, seg=14), cy((kx, -0.011, 0.36), 0.064, 0.03, col, seg=14)], P)


def elbowpads(cat, P, col, acc):
    for sd, sx in SIDES:
        c = Vector((0.29 * sx, 0.045, 0.85))
        piece(cat, 'Elbow' + sd, 'forearm.' + sd, [sp(tuple(c + Vector((0, -0.04, 0.0))), (0.052, 0.04, 0.052), col, sub=3), sp(tuple(c + Vector((0, -0.06, 0.0))), (0.034, 0.014, 0.034), acc, sub=3)], P)


def wristbands(cat, P, col, acc=None):
    for sd, sx in SIDES:
        p0, p1 = hand(sd, sx)
        parts = [rod(tuple(p0 - (p1 - p0) * 0.6), tuple(p0 - (p1 - p0) * 0.1), 0.062, col, True)]
        if acc: parts.append(rod(tuple(p0 - (p1 - p0) * 0.35), tuple(p0 - (p1 - p0) * 0.25), 0.066, acc, True))
        piece(cat, 'Wrist' + sd, 'forearm.' + sd, parts, P)


def boots(cat, P, col, acc, h=0.2):
    for sd, sx in SIDES:
        x = 0.09 * sx
        piece(cat, 'Boot' + sd, 'shin.' + sd, [tc((x, 0.04, 0.07 + h / 2), 0.058, 0.066, h, col, seg=14), cy((x, 0.04, 0.07 + h), 0.07, 0.025, acc, seg=14)], P)


def goggles(cat, P, strap, lens, rim, pos_z=1.26):
    parts = [b((0, -0.215, pos_z), (0.215, 0.03, 0.075), rim)] + [cy((s * 0.058, -0.235, pos_z), 0.052, 0.02, lens, (90, 0, 0), 14) for s in (-1, 1)]
    parts += [tc((0, 0.03, pos_z), 0.232, 0.232, 0.045, strap, seg=20)]
    piece(cat, 'Goggles', 'Head', parts, P)


def sunglasses(cat, P, frame='#1a1a1f', lens='#2b2f3a'):
    piece(cat, 'Sunglasses', 'Head', [cy((s * 0.06, -0.218, 1.26), 0.052, 0.014, lens, (90, 0, 0), 14) for s in (-1, 1)] + [b((0, -0.222, 1.275), (0.04, 0.012, 0.012), frame, bev=False)] + [rod((s * 0.11, -0.21, 1.275), (s * 0.215, -0.07, 1.275), 0.012, frame) for s in (-1, 1)], P)


def stripe(cat, P, name, z0, z1, col, off=0.019, hwf=0.118):
    rows = [z0 + (z1 - z0) * k / 3 for k in range(4)]
    patch(cat, name, 'spine.002', rows, lambda z: hwf, 10, col, P, off)


def Bike():
    P = coll_for('Bike'); BL, WH, YL = '#2d7ff9', '#f4f1ea', '#ffd84a'
    helmet('Bike', P, BL, WH); sunglasses('Bike', P); gloves('Bike', P, '#22232b', BL)
    stripe('Bike', P, 'JerseyStripeA', 0.90, 0.935, BL); stripe('Bike', P, 'JerseyStripeB', 0.835, 0.865, YL); kneepads('Bike', P, '#22232b', BL)
    piece('Bike', 'Bottle', 'spine.002', [cy((0.14, 0.15, 0.80), 0.032, 0.17, '#e8f4ff', seg=12), cy((0.14, 0.15, 0.905), 0.020, 0.04, YL, seg=10)], P)
    export('Bike', P); return P


def BikePolo():
    P = coll_for('BikePolo'); RD, DK, WH = '#e5384f', '#22232b', '#f4f1ea'
    helmet('BikePolo', P, RD, WH)
    piece('BikePolo', 'Faceguard', 'Head', [rod((s * 0.17, -0.17, 1.33), (s * 0.12, -0.24, 1.16), 0.012, WH) for s in (-1, 1)] + [rod((-0.12, -0.24, 1.16), (0.12, -0.24, 1.16), 0.012, WH), rod((-0.15, -0.21, 1.25), (0.15, -0.21, 1.25), 0.012, WH)], P)
    gloves('BikePolo', P, DK, RD); kneepads('BikePolo', P, DK, RD)
    piece('BikePolo', 'Mallet', 'hand.R', [rod((-0.50, 0.02, 1.10), (-0.50, -0.02, 0.20), 0.03, '#b07a3c'), cy((-0.50, -0.02, 0.17), 0.05, 0.17, RD, (0, 90, 0), 12), cy((-0.50, -0.02, 0.17), 0.052, 0.03, WH, (0, 90, 0), 12)], P)
    stripe('BikePolo', P, 'JerseyStripe', 0.865, 0.905, RD)
    export('BikePolo', P); return P


def Boxing():
    P = coll_for('Boxing'); RD, DK, GD = '#d92b2b', '#3a1218', '#ffd24a'
    gloves('Boxing', P, RD, '#f4f1ea', big=True)
    piece('Boxing', 'Headgear', 'Head', [sp((0, 0.05, 1.37), (0.262, 0.248, 0.225), DK, sub=3, zmin=0.0), tc((0, 0.045, 1.43), 0.262, 0.262, 0.05, RD, seg=20)] + [sp((s * 0.235, 0.03, 1.27), (0.05, 0.075, 0.095), RD, sub=2) for s in (-1, 1)] + [b((0, -0.20, 1.44), (0.19, 0.04, 0.035), RD, (-15, 0, 0))], P)
    patch('Boxing', 'Belt', 'spine', [0.70 - k * 0.016 for k in range(4)], lambda z: 0.125, 10, GD, P, 0.02)
    piece('Boxing', 'BeltBuckle', 'spine', [b((0, -0.14, 0.675), (0.09, 0.02, 0.06), '#fff2a8'), b((0, -0.152, 0.675), (0.05, 0.01, 0.03), RD, bev=False)], P)
    piece('Boxing', 'Towel', 'spine.004', [b((s * 0.075, -0.085, 1.0), (0.05, 0.02, 0.2), '#f4f1ea') for s in (-1, 1)] + [b((0, 0.065, 1.03), (0.17, 0.04, 0.05), '#f4f1ea')], P)
    export('Boxing', P); return P


def Horse():
    P = coll_for('Horse'); BK, BR, WH = '#1d1d22', '#7a4a27', '#f4f1ea'
    piece('Horse', 'RidingHat', 'Head', [sp((0, 0.045, 1.36), (0.258, 0.245, 0.225), BK, sub=3, zmin=-0.04), tc((0, 0.045, 1.318), 0.25, 0.25, 0.02, BK, seg=20), b((0, -0.235, 1.34), (0.17, 0.12, 0.012), BK, (-12, 0, 0)), tc((0, 0.045, 1.43), 0.258, 0.258, 0.018, '#c9a24a', seg=20)] + [rod((s * 0.225, 0.03, 1.28), (s * 0.075, -0.07, 1.10), 0.016, BK) for s in (-1, 1)], P)
    piece('Horse', 'Cravat', 'spine.004', [b((0, -0.115, 1.0), (0.11, 0.03, 0.07), WH), b((0, -0.13, 0.965), (0.06, 0.03, 0.06), WH), sp((0, -0.14, 1.0), 0.022, '#c9a24a')], P)
    boots('Horse', P, BR, BK, h=0.24); gloves('Horse', P, WH, BK)
    piece('Horse', 'Crop', 'hand.R', [rod((-0.46, -0.01, 0.74), (-0.50, -0.42, 0.62), 0.012, BK), b((-0.50, -0.43, 0.615), (0.04, 0.02, 0.07), BR)], P)
    export('Horse', P); return P


def Jumping():
    P = coll_for('Jumping'); PK, WH, CY = '#ff5fa2', '#f4f1ea', '#31d6e8'
    piece('Jumping', 'Headband', 'Head', [tc((0, 0.04, 1.43), 0.236, 0.236, 0.05, PK, seg=20), b((0.19, -0.13, 1.43), (0.06, 0.02, 0.06), CY, (0, 0, -25)), b((-0.19, -0.13, 1.43), (0.06, 0.02, 0.06), CY, (0, 0, 25))], P)
    wristbands('Jumping', P, PK, WH)
    for sd, sx in SIDES:
        x = 0.09 * sx
        piece('Jumping', 'Sock' + sd, 'shin.' + sd, [tc((x, 0.04, 0.17), 0.058, 0.064, 0.14, WH, seg=14), cy((x, 0.04, 0.21), 0.066, 0.02, PK, seg=14), cy((x, 0.04, 0.245), 0.066, 0.02, CY, seg=14)], P)
    piece('Jumping', 'Rope', 'hand.R', [rod((-0.50, 0.02, 0.76), (-0.62, -0.08, 0.40), 0.014, PK), rod((-0.62, -0.08, 0.40), (-0.30, -0.20, 0.18), 0.014, PK), rod((-0.30, -0.20, 0.18), (0.30, -0.20, 0.18), 0.014, PK), rod((0.30, -0.20, 0.18), (0.50, -0.05, 0.76), 0.014, PK), cy((-0.50, 0.02, 0.76), 0.022, 0.12, CY, (0, 90, 0), 8), cy((0.50, 0.02, 0.76), 0.022, 0.12, CY, (0, 90, 0), 8)], P)
    export('Jumping', P); return P


def Longboarding():
    P = coll_for('Longboarding'); TL, WH, DK = '#1fc7b6', '#f4f1ea', '#22232b'
    helmet('Longboarding', P, WH, TL, visor=False)
    for sd, sx in SIDES:
        p0, p1 = hand(sd, sx)
        piece('Longboarding', 'WristGuard' + sd, 'hand.' + sd, [rod(tuple(p0 - (p1 - p0) * 0.5), tuple(p0 + (p1 - p0) * 0.25), 0.062, WH, True), b(tuple(p0 + Vector((0, -0.04, 0.0))), (0.07, 0.015, 0.06), TL, (0, 0, -20 * sx)), rod(tuple(p0 + (p1 - p0) * 0.4), tuple(p0 + (p1 - p0) * 0.6), 0.058, DK, True)], P)
    kneepads('Longboarding', P, WH, TL); elbowpads('Longboarding', P, WH, TL)
    export('Longboarding', P); return P


def Motor():
    P = coll_for('Motor'); OR, DK, WH = '#ff7a1a', '#1c1d24', '#f4f1ea'
    piece('Motor', 'FullHelmet', 'Head', [sp((0, 0.04, 1.34), (0.275, 0.27, 0.255), OR, sub=3), b((0, -0.2, 1.26), (0.19, 0.06, 0.085), DK, (-8, 0, 0)), b((0, -0.215, 1.12), (0.1, 0.07, 0.065), OR), b((0, 0.04, 1.598), (0.05, 0.26, 0.01), WH, bev=False), tc((0, 0.04, 1.58), 0.04, 0.05, 0.03, DK)] + [b((s * 0.282, 0.0, 1.28), (0.02, 0.07, 0.06), DK) for s in (-1, 1)], P)
    gloves('Motor', P, DK, OR); boots('Motor', P, DK, OR, h=0.17); kneepads('Motor', P, DK, OR)
    stripe('Motor', P, 'JacketStripeA', 0.90, 0.945, OR, hwf=0.125); stripe('Motor', P, 'JacketStripeB', 0.80, 0.82, WH, hwf=0.125)
    for sd, sx in SIDES:
        piece('Motor', 'Shoulder' + sd, 'upper_arm.' + sd, [sp((0.2 * sx, 0.03, 0.945), (0.07, 0.06, 0.05), DK, sub=2), sp((0.2 * sx, 0.03, 0.97), (0.05, 0.045, 0.02), OR, sub=2)], P)
    export('Motor', P); return P


def Plane():
    P = coll_for('Plane'); BR, DK, WH, GL = '#8a5a34', '#2c2a2f', '#f4f1ea', '#79c7ff'
    piece('Plane', 'AviatorCap', 'Head', [sp((0, 0.05, 1.37), (0.252, 0.245, 0.215), BR, sub=3, zmin=-0.03), tc((0, 0.045, 1.335), 0.246, 0.246, 0.02, DK, seg=20)] + [b((s * 0.232, 0.03, 1.19), (0.03, 0.09, 0.14), BR, (0, 0, s * -10)) for s in (-1, 1)], P)
    piece('Plane', 'Goggles', 'Head', [tc((0, 0.04, 1.51), 0.236, 0.236, 0.04, DK, seg=20)] + [cy((s * 0.062, -0.215, 1.50), 0.055, 0.03, '#c9a24a', (62, 0, 0), 14) for s in (-1, 1)] + [cy((s * 0.062, -0.228, 1.498), 0.043, 0.02, GL, (62, 0, 0), 14) for s in (-1, 1)] + [b((0, -0.21, 1.51), (0.03, 0.03, 0.02), DK)], P)
    piece('Plane', 'Scarf', 'spine.004', [tc((0, 0.0, 1.005), 0.115, 0.125, 0.06, WH, seg=16), b((0.07, 0.12, 0.96), (0.07, 0.015, 0.34), WH, (6, 0, 6)), b((0.06, 0.15, 0.78), (0.07, 0.015, 0.2), '#e8e2d0', (6, 0, 14))], P)
    gloves('Plane', P, BR, DK)
    piece('Plane', 'Wings', 'spine.002', [sp((0.075, -0.15, 0.90), (0.055, 0.012, 0.012), '#ffd84a', (0, 0, 20), sub=1), sp((-0.075, -0.15, 0.90), (0.055, 0.012, 0.012), '#ffd84a', (0, 0, -20), sub=1)], P)
    export('Plane', P); return P


def RockClimbing():
    P = coll_for('RockClimbing'); YL, DK, OR, GR = '#ffd024', '#2a2d38', '#ff7a1a', '#c5ccd6'
    helmet('RockClimbing', P, YL, DK, visor=False)
    patch('RockClimbing', 'Harness', 'spine', [0.66 - k * 0.018 for k in range(4)], lambda z: 0.13, 10, OR, P, 0.022)
    piece('RockClimbing', 'HarnessLoops', 'spine', [rod((s * 0.105, -0.07, 0.62), (s * 0.11, -0.04, 0.45), 0.032, OR) for s in (-1, 1)] + [rod((s * 0.105, 0.06, 0.62), (s * 0.11, 0.03, 0.45), 0.032, OR) for s in (-1, 1)] + [b((0, -0.14, 0.645), (0.06, 0.02, 0.045), GR)], P)
    piece('RockClimbing', 'ChalkBag', 'spine', [tc((0.0, 0.15, 0.66), 0.06, 0.075, 0.12, '#f4f1ea', seg=12), cy((0, 0.15, 0.725), 0.065, 0.012, DK, seg=12)], P)
    piece('RockClimbing', 'Carabiners', 'spine', [b((s * 0.1, -0.15, 0.64), (0.022, 0.012, 0.055), GR) for s in (-1, 1)] + [b((0.1 * s, -0.152, 0.625), (0.04, 0.012, 0.012), '#ff4a4a') for s in (-1, 1)], P)
    piece('RockClimbing', 'Rope', 'spine', [cy((0.2, 0.02, 0.62 + k * 0.025), 0.055, 0.016, '#2f7de0' if k % 2 else '#ff7a1a', seg=14) for k in range(5)], P)
    wristbands('RockClimbing', P, '#f4f1ea', OR)
    export('RockClimbing', P); return P


def Scuba():
    P = coll_for('Scuba'); DK, YL, GL, TQ = '#20242e', '#ffd024', '#6fd8ff', '#1fb7cf'
    piece('Scuba', 'Mask', 'Head', [b((0, -0.215, 1.255), (0.235, 0.04, 0.115), DK), b((0, -0.235, 1.255), (0.19, 0.02, 0.085), GL), tc((0, 0.04, 1.255), 0.235, 0.235, 0.05, DK, seg=20), b((0, -0.225, 1.255), (0.02, 0.05, 0.12), DK)], P)
    piece('Scuba', 'Snorkel', 'Head', [rod((0.245, -0.12, 1.2), (0.255, -0.08, 1.62), 0.026, YL), b((0.25, -0.2, 1.205), (0.04, 0.05, 0.05), DK), rod((0.255, -0.08, 1.62), (0.26, -0.05, 1.68), 0.032, DK)], P)
    piece('Scuba', 'Tank', 'spine.002', [cy((s * 0.06, 0.16, 0.88), 0.07, 0.38, '#5b6677', seg=14) for s in (-1, 1)] + [cy((s * 0.06, 0.16, 1.085), 0.04, 0.03, YL, seg=10) for s in (-1, 1)] + [b((0, 0.16, 0.9), (0.16, 0.02, 0.05), DK), b((0, 0.13, 1.1), (0.08, 0.06, 0.06), YL)], P)
    piece('Scuba', 'Hose', 'spine.004', [rod((0.0, 0.14, 1.1), (0.14, 0.02, 1.04), 0.018, DK), rod((0.14, 0.02, 1.04), (0.1, -0.12, 0.99), 0.018, DK), sp((0.1, -0.13, 0.985), 0.03, YL)], P)
    for sd, sx in SIDES:
        x = 0.09 * sx
        piece('Scuba', 'Fin' + sd, 'foot.' + sd, [b((x, -0.15, 0.015), (0.115, 0.28, 0.018), TQ, (4, 0, 0)), b((x, -0.30, 0.012), (0.17, 0.12, 0.016), DK, (4, 0, 0)), b((x, 0.04, 0.045), (0.105, 0.09, 0.05), DK)], P)
    piece('Scuba', 'Gauge', 'forearm.L', [cy((0.40, 0.0, 0.795), 0.045, 0.022, DK, (90, 0, 10), 12), cy((0.40, -0.02, 0.795), 0.032, 0.01, GL, (90, 0, 10), 12)], P)
    export('Scuba', P); return P


def Skate():
    P = coll_for('Skate'); LM, DK, PK, WH = '#c8ff3a', '#22232b', '#ff4fa0', '#f4f1ea'
    piece('Skate', 'SkateHelmet', 'Head', [sp((0, 0.045, 1.37), (0.258, 0.25, 0.215), LM, sub=3, zmin=-0.05), tc((0, 0.045, 1.318), 0.252, 0.252, 0.03, DK, seg=20), b((0.17, -0.12, 1.50), (0.07, 0.025, 0.07), PK, (20, 0, -30)), b((-0.1, -0.17, 1.52), (0.06, 0.025, 0.05), WH, (30, 0, 15)), b((0, 0.045, 1.585), (0.05, 0.22, 0.01), DK, bev=False)] + [rod((s * 0.225, 0.03, 1.30), (s * 0.075, -0.07, 1.10), 0.016, DK) for s in (-1, 1)] + [b((0, -0.09, 1.09), (0.065, 0.028, 0.032), DK)], P)
    wristbands('Skate', P, DK, LM); kneepads('Skate', P, DK, LM); elbowpads('Skate', P, DK, LM)
    for sd, sx in SIDES:
        x = 0.09 * sx
        piece('Skate', 'Shoe' + sd, 'foot.' + sd, [b((x, -0.03, 0.03), (0.105, 0.2, 0.06), WH), b((x, -0.03, 0.005), (0.11, 0.21, 0.012), DK), b((x, -0.11, 0.045), (0.095, 0.05, 0.05), PK)], P)
    export('Skate', P); return P


def Ski():
    P = coll_for('Ski'); RD, WH, DK, OR = '#e5384f', '#f4f1ea', '#22232b', '#ff9a1f'
    piece('Ski', 'Beanie', 'Head', [sp((0, 0.045, 1.38), (0.258, 0.25, 0.215), RD, sub=3, zmin=-0.03), tc((0, 0.04, 1.355), 0.262, 0.262, 0.06, WH, seg=20), sp((0, 0.04, 1.605), 0.07, WH, sub=2)], P)
    goggles('Ski', P, DK, OR, DK, pos_z=1.25)
    piece('Ski', 'Gaiter', 'spine.004', [tc((0, 0.0, 1.0), 0.125, 0.135, 0.07, WH, seg=16), tc((0, 0.0, 1.05), 0.115, 0.125, 0.035, RD, seg=16)], P)
    for sd, sx in SIDES:
        p0, p1 = hand(sd, sx)
        piece('Ski', 'Mitten' + sd, 'hand.' + sd, [sp(tuple(p1 * 0.9 + p0 * 0.1), (0.095, 0.08, 0.075), RD, sub=3), rod(tuple(p0 - (p1 - p0) * 0.15), tuple(p0 + (p1 - p0) * 0.3), 0.075, WH, True)], P)
        piece('Ski', 'Pole' + sd, 'hand.' + sd, [rod((0.50 * sx, 0.0, 0.92), (0.58 * sx, -0.08, 0.04), 0.014, DK), cy((0.58 * sx, -0.08, 0.075), 0.045, 0.012, OR, seg=10)], P)
        x = 0.09 * sx
        piece('Ski', 'Boot' + sd, 'shin.' + sd, [tc((x, 0.04, 0.17), 0.062, 0.07, 0.2, DK, seg=14), cy((x, 0.04, 0.265), 0.072, 0.025, RD, seg=14), b((x, -0.04, 0.045), (0.1, 0.18, 0.06), DK)], P)
    export('Ski', P); return P


def Surfing():
    P = coll_for('Surfing'); TQ, CR, WH, YL = '#17c1c9', '#ff8f7a', '#f4f1ea', '#ffd84a'
    piece('Surfing', 'SunHat', 'Head', [sp((0, 0.04, 1.40), (0.235, 0.23, 0.14), YL, sub=3, zmin=-0.02), tc((0, 0.035, 1.395), 0.43, 0.43, 0.02, YL, seg=24), tc((0, 0.035, 1.43), 0.24, 0.24, 0.05, CR, seg=20), sp((0.2, -0.1, 1.46), 0.04, WH)], P)
    sunglasses('Surfing', P, frame=WH, lens='#2a5f78')
    piece('Surfing', 'ShellNecklace', 'spine.004', [rod((-0.09, -0.12, 0.99), (0.0, -0.14, 0.935), 0.01, WH), rod((0.09, -0.12, 0.99), (0.0, -0.14, 0.935), 0.01, WH), sp((0, -0.15, 0.925), (0.035, 0.015, 0.03), CR, sub=2), sp((0.04, -0.14, 0.945), (0.02, 0.012, 0.018), WH, sub=1), sp((-0.04, -0.14, 0.945), (0.02, 0.012, 0.018), WH, sub=1)], P)
    stripe('Surfing', P, 'RashStripeA', 0.90, 0.935, TQ); stripe('Surfing', P, 'RashStripeB', 0.845, 0.865, CR)
    piece('Surfing', 'Board', 'hand.R', [sp((-0.64, 0.03, 0.62), (0.095, 0.022, 0.62), TQ, sub=3), b((-0.64, 0.012, 0.62), (0.014, 0.012, 1.02), WH, bev=False)], P)
    piece('Surfing', 'Leash', 'shin.R', [rod((-0.09, -0.05, 0.12), (-0.2, -0.08, 0.05), 0.01, '#22232b'), cy((-0.09, 0.03, 0.12), 0.05, 0.03, YL, seg=12)], P)
    export('Surfing', P); return P


BUILDERS2 = {'Bike': Bike, 'BikePolo': BikePolo, 'Boxing': Boxing, 'Horse': Horse, 'Jumping': Jumping, 'Longboarding': Longboarding, 'Motor': Motor, 'Plane': Plane,
             'RockClimbing': RockClimbing, 'Scuba': Scuba, 'Skate': Skate, 'Ski': Ski, 'Surfing': Surfing}
