"""
Boss themes for AnalogueWay / Harvey's Nine Lives: one loopable track per boss, each built from its zone's
instruments but faster, darker and more driving than the level music.

  Boss_GraveWarden.wav      Z1BossTomb    D harmonic minor, 148 BPM: galloping bass, tolling bells, pulse lead, organ pad
  Boss_SandscaleScarab.wav  Z2BossSandy   E Hijaz, 156 BPM: staccato brass riff, double-time darbuka + riq, ney climax
  Boss_YokaiWarrior.wav     Z3BossBattle  D hirajoshi, 162 BPM: koto plucks, taiko toms, shakuhachi climax
  Boss_KingMossback.wav     Z4BossGrass   E minor, 144 BPM, bouncy swing: pulse lead + e-piano stabs, brass anthem

Form (32 bars, loops back to bar 0):  A riff, B climax, A2 riff with harmony, B2 climax with harmony + fill.

Usage (from the project root):  python3 Tools/Music/boss_themes.py [GraveWarden SandscaleScarab YokaiWarrior KingMossback]
Writes Assets/Music/Boss_<Name>.wav (all four when no names are given).
"""
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
from arcade_synth import Song, check_score, SR  # noqa: E402

OUT_DIR = "Assets/Music"


def third_below(m, scale):
    """Diatonic third under m in the given pitch-class scale (falls back to a minor third)."""
    pc = m % 12
    if pc not in scale:
        return m - 3
    i = scale.index(pc)
    return m - ((pc - scale[(i - 2) % len(scale)]) % 12)


def gallop(r, nr):
    """Driving 8ths with a gallop and a lead-in to the next root."""
    return [(0, 2, r), (2, 1, r), (3, 1, r), (4, 2, r + 12), (6, 2, r), (8, 2, r), (10, 1, r), (11, 1, r),
            (12, 2, r + 12), (14, 2, nr)]


def eighths(r, nr):
    return [(st, 2, r + (12 if st in (6, 14) else 0)) for st in range(0, 14, 2)] + [(14, 2, nr)]


# ---------------------------------------------------------------- 1. The Grave Warden (tomb)
WARDEN = dict(
    name="GraveWarden", bpm=148, swing=0.0, seed=3101,
    scale=[2, 4, 5, 7, 9, 10, 1],  # D harmonic minor
    ch={'Dm': ([57, 62, 65], 38), 'Bb': ([58, 62, 65], 34), 'Gm': ([55, 58, 62], 43), 'A': ([57, 61, 64], 33),
        'Eb': ([55, 58, 63], 39)},
    a=[('D5', 4), ('A4', 2), ('D5', 2), ('F5', 4), ('E5', 2), ('D5', 2),
       ('D5', 2), ('F5', 2), ('Bb5', 4), ('A5', 4), ('F5', 4),
       ('G5', 6), ('F5', 2), ('E5', 4), ('D5', 4),
       ('C#5', 4), ('E5', 4), ('A5', 6), ('G5', 2),
       ('F5', 4), ('E5', 2), ('D5', 2), ('A5', 4), ('D6', 4),
       ('D6', 2), ('C6', 2), ('Bb5', 4), ('A5', 4), ('F5', 4),
       ('G5', 4), ('Bb5', 4), ('Eb6', 4), ('D6', 4),
       ('C#6', 6), ('A5', 2), ('E5', 4), ('C#5', 4)],
    a_ch=['Dm', 'Bb', 'Gm', 'A', 'Dm', 'Bb', 'Eb', 'A'],
    b=[('Bb5', 8), ('A5', 4), ('G5', 4),
       ('A5', 8), ('F5', 4), ('D5', 4),
       ('F5', 4), ('G5', 4), ('A5', 4), ('Bb5', 4),
       ('A5', 12), ('R', 4),
       ('D6', 8), ('C6', 4), ('Bb5', 4),
       ('A5', 8), ('D6', 4), ('F6', 4),
       ('Eb6', 6), ('D6', 2), ('C6', 4), ('Bb5', 4),
       ('C#6', 4), ('D6', 2), ('E6', 2), ('A5', 8)],
    b_ch=['Gm', 'Dm', 'Bb', 'A', 'Gm', 'Dm', 'Eb', 'A'],
)


def warden_bar(s, t, style, bar, b, ch, nxt):
    voic, r = t['ch'][ch]
    for st, ln, m in gallop(r, t['ch'][nxt][1]):
        s.mix(s.slap_bass(m, int(ln * s.STEP * 0.8)), s.pos(bar, st), 0.30)
    s.mix(s.bell(r + 36, ratio=2.76, index=2.2, decay=0.9, sec=2.0), s.pos(bar, 0), 0.07, -0.35, 0.5)  # the toll
    if style.startswith('climax'):
        s.mix(s.bell(r + 43, ratio=2.76, index=1.8, decay=0.6, sec=1.4), s.pos(bar, 8), 0.05, 0.35, 0.5)
    s.mix(s.strings(tuple(voic), s.BAR, 0.05 if style.startswith('climax') else 0.4), bar * s.BAR,
          0.05 if style.startswith('climax') else 0.035, 0.0, 0.4)
    H = [('kick', st, 1.0) for st in (0, 4, 8, 12)] + [('snare', 4, 1.0), ('snare', 12, 1.0)]
    H += [('hat', st, 0.9 if st % 4 == 2 else 0.5) for st in range(0, 16, 2)]
    if style.startswith('climax'):
        H += [('ohat', 14, 0.8), ('kick', 10, 0.7)]
    if b == 7:
        H = [h for h in H if h[1] < 8] + [('tomH', 8, 1.0), ('tomH', 10, 0.9), ('tomM', 12, 1.0), ('tomL', 14, 1.0)]
    for name, st, v in H:
        s.hit(name, bar, st, v)


def warden_note(s, t, style, sec, m, start, ln):
    gate = int(ln * 0.85)
    if style.startswith('riff'):
        s.mix(s.pulse_lead(m, gate, duty=0.25, vib=0.2), start, 0.13, -0.1, 0.3)
        if sec == 'A2':
            s.mix(s.pulse_lead(m - 12, gate, duty=0.5, vib=0.0), start, 0.07, 0.3, 0.2)
    else:
        s.mix(s.brass(m, gate, -0.06, 1.7), start, 0.14, -0.25, 0.35)
        s.mix(s.brass(m, gate, 0.06, 1.7), start + 180, 0.14, 0.25, 0.35)
        if sec == 'B2':
            s.mix(s.brass(third_below(m, t['scale']), gate, 0.0, 1.4), start, 0.08, 0.45, 0.35)


# ---------------------------------------------------------------- 2. The Sandscale Scarab (sandy dunes)
SCARAB = dict(
    name="SandscaleScarab", bpm=156, swing=0.05, seed=3202,
    scale=[9, 11, 0, 2, 4, 5, 8],  # A harmonic minor (E Hijaz)
    ch={'E': ([56, 59, 62, 64], 40), 'F': ([57, 60, 65], 41), 'Dm': ([57, 62, 65], 38), 'Am': ([57, 60, 64], 45),
        'G': ([55, 59, 62], 43)},
    a=[('E5', 2), ('F5', 2), ('G#5', 2), ('A5', 2), ('B5', 4), ('G#5', 4),
       ('A5', 2), ('G#5', 2), ('F5', 2), ('E5', 2), ('F5', 4), ('C5', 4),
       ('E5', 2), ('F5', 2), ('G#5', 2), ('B5', 2), ('D6', 4), ('C6', 2), ('B5', 2),
       ('A5', 4), ('F5', 2), ('D5', 2), ('F5', 4), ('G#5', 4),
       ('E6', 4), ('D6', 2), ('C6', 2), ('B5', 4), ('G#5', 4),
       ('A5', 2), ('C6', 2), ('A5', 2), ('F5', 2), ('G#5', 4), ('A5', 4),
       ('F5', 2), ('E5', 2), ('D5', 2), ('F5', 2), ('A5', 4), ('D6', 4),
       ('C6', 2), ('B5', 2), ('A5', 2), ('G#5', 2), ('F5', 2), ('E5', 2), ('F5', 2), ('G#5', 2)],
    a_ch=['E', 'F', 'E', 'Dm', 'E', 'F', 'Dm', 'E'],
    b=[('A5', 6), ('B5', 2), ('C6', 8),
       ('B5', 6), ('A5', 2), ('G5', 8),
       ('A5', 4), ('C6', 4), ('F6', 4), ('E6', 4),
       ('E6', 8), ('D6', 4), ('B5', 4),
       ('C6', 6), ('D6', 2), ('E6', 8),
       ('D6', 6), ('C6', 2), ('B5', 8),
       ('C6', 4), ('A5', 4), ('F5', 4), ('G#5', 4),
       ('B5', 4), ('G#5', 4), ('E5', 8)],
    b_ch=['Am', 'G', 'F', 'E', 'Am', 'G', 'F', 'E'],
)


def scarab_bar(s, t, style, bar, b, ch, nxt):
    voic, r = t['ch'][ch]
    for st, ln, m in eighths(r, t['ch'][nxt][1] + 1):
        s.mix(s.slap_bass(m, int(ln * s.STEP * 0.75)), s.pos(bar, st), 0.30)
    if style.startswith('riff'):  # oud tremolo on the chord root
        for st in range(0, 16):
            s.mix(s.pluck(voic[0], int(0.2 * SR), 0.5, 0.55), s.pos(bar, st), 0.04 if st % 4 else 0.06, 0.4, 0.25)
    else:
        s.mix(s.strings(tuple(m - 12 for m in voic[:3]), s.BAR, 0.1), bar * s.BAR, 0.045, 0.1, 0.35)
        for st in (0, 3, 6, 10):  # brass stabs under the ney
            for k, m in enumerate(voic[:3]):
                s.mix(s.brass(m - 12, int(1.3 * s.STEP), 0.0, 2.0), s.pos(bar, st) + 80 * k, 0.05, -0.2 + 0.2 * k, 0.2)
    maqsum = [('doum', 0, 1.0), ('tek', 2, 0.8), ('tek', 6, 0.9), ('doum', 8, 0.9), ('tek', 12, 0.9), ('tek', 14, 0.5)]
    H = maqsum + [(n, st + 1, v * 0.45) for n, st, v in maqsum if n == 'tek']  # double-time ghost teks
    H += [('kick', st, 1.0) for st in (0, 4, 8, 12)] + [('clap', 4, 0.8), ('clap', 12, 0.9)]
    H += [('riq', st, (1.0, 0.4, 0.7, 0.4)[st % 4]) for st in range(16)]
    if b % 4 == 0:
        H.append(('zill', 0, 1.0))
    if b == 7:
        H = [h for h in H if h[1] < 8 or h[0] == 'riq']
        H += [('tek', st, 0.5 + 0.5 * (st - 8) / 7) for st in range(8, 16)] + [('doum', 8, 1.0), ('doum', 12, 1.0)]
    for name, st, v in H:
        s.hit(name, bar, st, v)


def scarab_note(s, t, style, sec, m, start, ln):
    if style.startswith('riff'):
        gate = int(ln * 0.6)  # staccato
        s.mix(s.brass(m, gate, -0.07, 2.4), start, 0.15, -0.3, 0.3)
        s.mix(s.brass(m, gate, 0.07, 2.4), start + 150, 0.15, 0.3, 0.3)
        s.mix(s.pluck(m - 12, min(ln + int(0.2 * SR), SR), 1.0, 0.55), start, 0.11, 0.0, 0.25)
        if sec == 'A2':
            s.mix(s.brass(third_below(m, t['scale']), gate, 0.0, 1.8), start, 0.08, 0.5, 0.3)
    else:
        s.mix(s.flute(m, ln - 1500, scoop=-0.8, breath=1.6), start, 0.21, -0.05, 0.45)
        if sec == 'B2':
            s.mix(s.pluck(m, min(ln + int(0.3 * SR), 2 * SR), 1.3, 0.55), start, 0.12, 0.35, 0.3)


# ---------------------------------------------------------------- 3. The Yokai Warrior (snow temple)
YOKAI = dict(
    name="YokaiWarrior", bpm=162, swing=0.0, seed=3303,
    scale=[2, 3, 7, 9, 10],  # D hirajoshi (D Eb G A Bb)
    ch={'Dm': ([57, 62, 65], 38), 'Bb': ([58, 62, 65], 34), 'C': ([55, 60, 64], 36), 'Eb': ([55, 58, 63], 39),
        'Gm': ([55, 58, 62], 43), 'A': ([57, 61, 64], 33)},
    a=[('D5', 2), ('Eb5', 2), ('G5', 2), ('A5', 2), ('D6', 4), ('A5', 4),
       ('Bb5', 4), ('A5', 2), ('G5', 2), ('Eb5', 4), ('D5', 4),
       ('G5', 2), ('A5', 2), ('C6', 4), ('Bb5', 2), ('A5', 2), ('G5', 4),
       ('A5', 8), ('D5', 4), ('R', 4),
       ('D6', 2), ('C6', 2), ('A5', 2), ('G5', 2), ('A5', 4), ('D6', 4),
       ('Eb6', 4), ('D6', 2), ('Bb5', 2), ('G5', 4), ('Eb5', 4),
       ('G5', 4), ('C6', 4), ('Bb5', 4), ('G5', 4),
       ('A5', 6), ('C#6', 2), ('E6', 4), ('A5', 4)],
    a_ch=['Dm', 'Bb', 'C', 'Dm', 'Dm', 'Eb', 'C', 'A'],
    b=[('G5', 8), ('Bb5', 4), ('A5', 4),
       ('D6', 12), ('C6', 4),
       ('Bb5', 6), ('G5', 2), ('Eb5', 8),
       ('A5', 8), ('C#6', 4), ('E6', 4),
       ('G6', 6), ('F6', 2), ('D6', 8),
       ('F6', 4), ('Eb6', 4), ('D6', 8),
       ('Eb6', 6), ('D6', 2), ('Bb5', 4), ('G5', 4),
       ('A5', 12), ('R', 4)],
    b_ch=['Gm', 'Dm', 'Eb', 'A', 'Gm', 'Dm', 'Eb', 'A'],
)


def yokai_bar(s, t, style, bar, b, ch, nxt):
    voic, r = t['ch'][ch]
    for st, ln, m in eighths(r, t['ch'][nxt][1]):
        s.mix(s.slap_bass(m, int(ln * s.STEP * 0.8)), s.pos(bar, st), 0.30)
    if style.startswith('climax'):
        s.mix(s.strings(tuple(voic), s.BAR, 0.08), bar * s.BAR, 0.05, 0.0, 0.4)
        for st in (0, 6, 12):  # koto chords
            for k, m in enumerate(voic):
                s.mix(s.pluck(m, int(0.6 * SR), 1.0, 0.7), s.pos(bar, st) + 260 * k, 0.07, 0.4 - 0.3 * k, 0.3)
    # taiko: big drum hits with the toms, and a wooden clack
    H = [('kick', st, 1.0) for st in (0, 3, 6, 8, 11, 14)] + [('snare', 4, 1.0), ('snare', 12, 1.0)]
    H += [('tomL', 0, 1.0), ('tomL', 8, 0.9), ('rim', 2, 0.8), ('rim', 10, 0.8)]
    H += [('hat', st, 0.7 if st % 4 == 0 else 0.4) for st in range(0, 16, 2)]
    if style.startswith('climax'):
        H += [('tomM', 6, 0.7), ('tomM', 14, 0.7)]
    if b == 7:
        H = [h for h in H if h[1] < 8] + [('tomL', st, 1.0 if st % 2 == 0 else 0.7) for st in range(8, 16)]
    for name, st, v in H:
        s.hit(name, bar, st, v)


def yokai_note(s, t, style, sec, m, start, ln):
    if style.startswith('riff'):
        s.mix(s.pluck(m, min(ln + int(0.4 * SR), 2 * SR), 1.2, 0.7), start, 0.18, -0.2, 0.3)  # koto lead
        s.mix(s.brass(m, int(ln * 0.7), 0.0, 2.2), start, 0.10, 0.2, 0.3)
        if sec == 'A2':
            s.mix(s.pluck(m - 12, min(ln + int(0.3 * SR), 2 * SR), 1.0, 0.6), start + 120, 0.10, 0.45, 0.3)
    else:
        s.mix(s.flute(m, ln - 1200, scoop=-1.2, breath=1.5), start, 0.22, 0.0, 0.5)  # shakuhachi
        if sec == 'B2':
            s.mix(s.brass(m - 12, int(ln * 0.85), 0.0, 1.6), start, 0.09, -0.4, 0.35)


# ---------------------------------------------------------------- 4. King Mossback (grassy meadow)
MOSSBACK = dict(
    name="KingMossback", bpm=144, swing=0.08, seed=3404,
    scale=[4, 6, 7, 9, 11, 0, 2],  # E natural minor
    ch={'Em': ([55, 59, 64], 40), 'C': ([55, 60, 64], 36), 'D': ([57, 62, 66], 38), 'B': ([59, 63, 66], 35),
        'Am': ([57, 60, 64], 45), 'G': ([55, 59, 62], 43)},
    a=[('E5', 3), ('G5', 3), ('B5', 2), ('E6', 4), ('D6', 2), ('B5', 2),
       ('C6', 3), ('B5', 3), ('G5', 2), ('E5', 4), ('G5', 4),
       ('F#5', 3), ('A5', 3), ('D6', 2), ('F#6', 4), ('E6', 2), ('D6', 2),
       ('D#6', 6), ('B5', 2), ('F#5', 4), ('D#5', 4),
       ('E5', 2), ('F#5', 2), ('G5', 2), ('A5', 2), ('B5', 4), ('E6', 4),
       ('G6', 4), ('E6', 2), ('C6', 2), ('E6', 4), ('G6', 4),
       ('A6', 4), ('G6', 2), ('E6', 2), ('C6', 4), ('A5', 4),
       ('B5', 4), ('D#6', 4), ('F#6', 4), ('D#6', 4)],
    a_ch=['Em', 'C', 'D', 'B', 'Em', 'C', 'Am', 'B'],
    b=[('E6', 8), ('D6', 4), ('C6', 4),
       ('D6', 8), ('B5', 4), ('G5', 4),
       ('A5', 4), ('D6', 4), ('F#6', 4), ('E6', 4),
       ('E6', 12), ('R', 4),
       ('G6', 8), ('F#6', 4), ('E6', 4),
       ('D6', 8), ('G6', 4), ('B5', 4),
       ('C6', 6), ('B5', 2), ('A5', 4), ('E6', 4),
       ('D#6', 8), ('F#6', 4), ('B5', 4)],
    b_ch=['C', 'G', 'D', 'Em', 'C', 'G', 'Am', 'B'],
)


def mossback_bar(s, t, style, bar, b, ch, nxt):
    voic, r = t['ch'][ch]
    nr = t['ch'][nxt][1]
    for st, ln, m in [(0, 3, r), (3, 3, r), (6, 2, r + 12), (8, 2, r), (10, 2, r + 7), (12, 2, r + 12), (14, 2, nr)]:
        s.mix(s.slap_bass(m, int(ln * s.STEP * 0.85)), s.pos(bar, st), 0.32)
    for st, ln in ((0, 2), (3, 2), (6, 2), (10, 2), (12, 3)):  # e-piano stabs
        for k, m in enumerate(voic):
            s.mix(s.epiano(m, int(ln * s.STEP * 0.6)), s.pos(bar, st) + 60 * k, 0.06, -0.3 + 0.2 * k, 0.25)
    if style.startswith('climax'):
        s.mix(s.strings(tuple(m - 12 for m in voic), s.BAR, 0.12), bar * s.BAR, 0.045, 0.1, 0.35)
    H = [('kick', st, 1.0) for st in (0, 7, 8, 10)] + [('snare', 4, 1.0), ('snare', 12, 1.0), ('clap', 12, 0.7)]
    H += [('hat', st, 0.9 if st % 4 == 2 else 0.5) for st in range(0, 16, 2)] + [('ohat', 14, 0.8)]
    if style.startswith('climax'):
        H += [('shaker', st, 0.6) for st in range(1, 16, 2)]
    if b == 7:
        H = [h for h in H if h[1] < 8] + [('tomH', 8, 1.0), ('tomM', 10, 1.0), ('tomL', 12, 1.0), ('snare', 14, 0.9),
                                          ('snare', 15, 0.7)]
    for name, st, v in H:
        s.hit(name, bar, st, v)


def mossback_note(s, t, style, sec, m, start, ln):
    gate = int(ln * 0.8)
    if style.startswith('riff'):
        s.mix(s.pulse_lead(m, gate, duty=0.5, vib=0.25), start, 0.13, -0.1, 0.3)
        s.mix(s.pulse_lead(m + 12, gate, duty=0.125, vib=0.0), start, 0.04, 0.3, 0.3)
        if sec == 'A2':
            s.mix(s.pulse_lead(third_below(m, t['scale']), gate, duty=0.25, vib=0.25), start, 0.08, 0.4, 0.3)
    else:
        s.mix(s.brass(m, gate, -0.07, 2.3), start, 0.15, -0.3, 0.3)
        s.mix(s.brass(m, gate, 0.07, 2.3), start + 200, 0.15, 0.3, 0.3)
        if sec == 'B2':
            s.mix(s.brass(third_below(m, t['scale']), gate, 0.0, 1.8), start, 0.09, 0.5, 0.3)


THEMES = {
    "GraveWarden": (WARDEN, warden_bar, warden_note),
    "SandscaleScarab": (SCARAB, scarab_bar, scarab_note),
    "YokaiWarrior": (YOKAI, yokai_bar, yokai_note),
    "KingMossback": (MOSSBACK, mossback_bar, mossback_note),
}


def render(key):
    t, bar_fn, note_fn = THEMES[key]
    check_score(key + " A", t['a'])
    check_score(key + " B", t['b'])
    sections = [('A', 'riff', t['a'], t['a_ch']), ('B', 'climax', t['b'], t['b_ch']),
                ('A2', 'riff2', t['a'], t['a_ch']), ('B2', 'climax2', t['b'], t['b_ch'])]
    s = Song(t['bpm'], t['swing'], 8 * len(sections), t['seed'])
    flat = [c for _, _, _, chs in sections for c in chs]
    print(f"{key}:", flush=True)
    for si, (sec, style, mel, chords) in enumerate(sections):
        b0 = si * 8
        for b, ch in enumerate(chords):
            bar = b0 + b
            bar_fn(s, t, style, bar, b, ch, flat[(bar + 1) % s.bars])
        s.hit('crash', b0, 0, 1.0 if style.startswith('climax') else 0.6)
        if sec in ('A2', 'B2'):
            s.reverse_cymbal_into(b0)
        for m, start, ln in s.notes(mel, b0):
            if m is not None:
                note_fn(s, t, style, sec, m, start, ln)
    out = os.path.join(OUT_DIR, f"Boss_{key}.wav")
    s.finish(out, [n for n, _, _, _ in sections])
    print('wrote', out)


if __name__ == '__main__':
    for key in (sys.argv[1:] or THEMES):
        render(key)
