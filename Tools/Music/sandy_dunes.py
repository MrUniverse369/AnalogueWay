"""
"Sandy Dunes" - loopable BGM for AnalogueWay's sandy levels (Z3A1_Egypt, Z3A2_Egypt).
Modern pixel-platformer desert groove: E Hijaz / A harmonic minor, 118 BPM, light swing.
Ney flute + oud plucks in the verses, FM brass chorus, darbuka (maqsum rhythm), riq, finger cymbals.

Usage:  python3 sandy_dunes.py <output.wav>
Form (40 bars, ~81 s, loops back to bar 0):
  A  verse      ney melody, oud riff, darbuka + light kit
  B  chorus     brass lead + oud double, e-piano stabs, strings, full kit + riq
  A2 verse 2    ney + oud unison, riq
  C  breakdown  ney solo, oud tremolo, hand drums only, darbuka roll + reverse cymbal
  B2 chorus 2   with harmony line; ends on a Hijaz run over E -> loops to Am
"""
import sys
from arcade_synth import Song, midi, check_score, SR

BPM, SWING, SEED = 118, 0.10, 2201

CH = {'Am': ([57, 60, 64], 45), 'G': ([55, 59, 62], 43), 'F': ([57, 60, 65], 41),
      'E': ([56, 59, 62, 64], 40), 'Dm': ([57, 62, 65], 38)}

VERSE = [('E4',4),('A4',4),('B4',2),('C5',2),('B4',4),
         ('D5',6),('C5',2),('B4',4),('G4',4),
         ('A4',4),('C5',4),('B4',2),('A4',2),('F4',4),
         ('G#4',6),('A4',2),('G#4',2),('F4',2),('E4',4),
         ('E4',4),('A4',4),('C5',2),('E5',2),('D5',4),
         ('B4',6),('C5',2),('D5',4),('B4',4),
         ('C5',4),('A4',2),('F4',2),('A4',4),('G#4',4),
         ('E4',8),('F4',2),('E4',2),('R',4)]
VERSE_CH = ['Am','G','F','E','Am','G','F','E']

CHORUS = [('A5',3),('A5',3),('F5',2),('D5',4),('E5',4),
          ('C6',3),('B5',3),('A5',2),('E5',8),
          ('G#5',3),('A5',3),('B5',2),('D6',4),('B5',4),
          ('C6',6),('B5',2),('A5',8),
          ('F5',3),('E5',3),('D5',2),('A5',4),('F5',4),
          ('E5',3),('F5',3),('G#5',2),('A5',8),
          ('A5',3),('C6',3),('A5',2),('F5',4),('G#5',4),
          ('B5',4),('A5',2),('G#5',2),('F5',2),('E5',2),('F5',2),('G#5',2)]
CHORUS_CH = ['Dm','Am','E','Am','Dm','Am','F','E']

BREAK = [('E5',8),('F5',4),('E5',4),
         ('F5',6),('E5',2),('C5',8),
         ('B4',4),('G#4',4),('A4',4),('B4',4),
         ('C5',8),('A4',4),('F4',4),
         ('D5',6),('E5',2),('F5',4),('A5',4),
         ('A5',6),('G#5',2),('F5',8),
         ('E5',4),('F5',2),('E5',2),('D5',2),('C5',2),('B4',4),
         ('G#4',8),('E4',8)]
BREAK_CH = ['E','F','E','F','Dm','F','E','E']

SECTIONS = [('A', 'verse', VERSE, VERSE_CH), ('B', 'chorus', CHORUS, CHORUS_CH), ('A2', 'verse', VERSE, VERSE_CH),
            ('C', 'break', BREAK, BREAK_CH), ('B2', 'chorus', CHORUS, CHORUS_CH)]
HARM_SCALE = [9, 11, 0, 2, 4, 5, 8]          # A harmonic minor

def third_below(m):
    pc = m % 12
    if pc not in HARM_SCALE:
        return m - 3
    i = HARM_SCALE.index(pc)
    return m - ((pc - HARM_SCALE[(i - 2) % 7]) % 12)

def drums(s, style, bar, local, last):
    H = []
    maqsum = [('doum', 0, 1.0), ('tek', 2, 0.8), ('tek', 6, 0.9), ('doum', 8, 0.9), ('tek', 12, 0.9), ('tek', 14, 0.5)]
    if style == 'verse':
        H += maqsum + [('kick', 0, 0.8), ('kick', 8, 0.6), ('rim', 12, 0.7)]
        H += [('shaker', st, (0.9, 0.4, 0.6, 0.4)[st % 4]) for st in range(16)]
        if local % 2 == 0:
            H.append(('zill', 0, 1.0))
    elif style == 'chorus':
        H += maqsum + [('kick', st, 1.0) for st in (0, 7, 8, 10)]
        H += [('snare', st, 1.0) for st in (4, 12)] + [('clap', 12, 0.9)]
        H += [('riq', st, (1.0, 0.45, 0.7, 0.45)[st % 4]) for st in range(16)] + [('ohat', 14, 0.9)]
        if local % 4 == 0:
            H.append(('zill', 0, 1.0))
    elif style == 'break':
        H += [(n, st, v * 0.6) for n, st, v in maqsum] + [('zill', 0, 0.6)]
    if last:
        H = [h for h in H if h[1] < 8 or h[0] in ('shaker', 'riq')]
        if style == 'break':
            H += [('tek', st, 0.4 + 0.6 * (st - 8) / 7) for st in range(8, 16)] + [('doum', 8, 1.0), ('doum', 12, 1.0)]
            s.reverse_cymbal_into(bar + 1)
        else:
            H += [('doum', 8, 1.0), ('tek', 10, 0.9), ('tek', 11, 0.8), ('doum', 12, 1.0), ('tek', 13, 0.8),
                  ('tomL', 14, 1.0), ('snare', 15, 0.8), ('kick', 8, 1.0)]
    for name, st, v in H:
        s.hit(name, bar, st, v)

def bass(style, ch, nxt):
    r = CH[ch][1]; nr = CH[nxt][1]
    if style == 'break':
        return [(0, 8, r), (8, 8, r)]
    if style == 'verse':
        return [(0, 3, r), (3, 3, r), (6, 2, r + 12), (8, 4, r), (12, 2, r + 7), (14, 2, nr + 1)]
    return [(0, 3, r), (3, 3, r), (6, 2, r + 12), (8, 2, r), (10, 2, r + 7), (12, 2, r + 12), (14, 2, nr + 1)]

def main(out):
    for name, mel in (('VERSE', VERSE), ('CHORUS', CHORUS), ('BREAK', BREAK)):
        check_score(name, mel)
    s = Song(BPM, SWING, 8 * len(SECTIONS), SEED)
    flat = [c for _, _, _, chs in SECTIONS for c in chs]
    for si, (name, style, mel, chords) in enumerate(SECTIONS):
        b0 = si * 8
        print(f'  {name:3s} ({style})', flush=True)
        for b, ch in enumerate(chords):
            bar = b0 + b
            voic = CH[ch][0]
            nxt = flat[(bar + 1) % s.bars]
            for st, ln, m in bass(style, ch, nxt):
                s.mix(s.slap_bass(m, int(ln * s.STEP * 0.85)), s.pos(bar, st), 0.30)
            # oud riff (verse) / tremolo (break)
            if style == 'verse':
                for k, st in enumerate((0, 3, 6, 10, 12)):
                    m = voic[k % len(voic)] - 12 + (12 if k == 4 else 0)
                    s.mix(s.pluck(m, int(0.7 * SR), 1.1, 0.55), s.pos(bar, st), 0.16, 0.35, 0.2)
            if style == 'break':
                for st in range(0, 16, 1 if b % 2 == 0 else 2):
                    s.mix(s.pluck(voic[0], int(0.25 * SR), 0.6, 0.5), s.pos(bar, st), 0.045, 0.4, 0.3)
            # e-piano stabs + strings in the chorus, pad in the break
            if style == 'chorus':
                for st, ln in ((0, 2), (3, 2), (6, 2), (10, 2), (12, 3)):
                    for k, m in enumerate(voic):
                        s.mix(s.epiano(m, int(ln * s.STEP * 0.6)), s.pos(bar, st) + 60 * k, 0.06, -0.3 + 0.2 * k, 0.25)
            if style in ('chorus', 'break'):
                s.mix(s.strings(tuple(m - 12 for m in voic[:3]), s.BAR, 0.15 if style == 'chorus' else 0.9),
                      bar * s.BAR, 0.045 if style == 'chorus' else 0.04, 0.1, 0.35)
            drums(s, style, bar, b, b == 7)
        if style == 'chorus':
            s.hit('crash', b0, 0, 1.0)
        if name == 'A':
            s.hit('crash', b0, 0, 0.5)

        for m, start, ln in s.notes(mel, b0):
            if m is None:
                continue
            if style == 'verse':
                s.mix(s.flute(m, ln - 1500, scoop=-0.6, breath=1.7), start, 0.20, -0.1, 0.4)
                if name == 'A2':
                    s.mix(s.pluck(m, min(ln + int(0.4 * SR), 2 * SR), 1.4, 0.55), start, 0.14, 0.3, 0.3)
            elif style == 'chorus':
                gate = ln - 600 if ln >= 4 * s.STEP else int(ln * 0.8)
                s.mix(s.brass(m, gate, -0.07, 2.2), start, 0.16, -0.3, 0.3)
                s.mix(s.brass(m, gate, 0.07, 2.2), start + 200, 0.16, 0.3, 0.3)
                s.mix(s.pluck(m - 12, min(ln + int(0.3 * SR), 2 * SR), 1.2, 0.55), start, 0.12, -0.1, 0.25)
                if name == 'B2':
                    s.mix(s.brass(third_below(m), gate, 0.0, 1.7), start, 0.09, 0.5, 0.3)
            elif style == 'break':
                s.mix(s.flute(m, ln - 2000, scoop=-0.8, breath=1.9), start, 0.17, -0.05, 0.5)
    s.finish(out, [n for n, _, _, _ in SECTIONS])
    print('wrote', out)

if __name__ == '__main__':
    main(sys.argv[1])
