"""
"Grassy Meadow" - loopable BGM for AnalogueWay's grassy levels (Z4A*GrassGreen).
Sunny, bouncy modern platformer theme: G major, 126 BPM, swung 16ths.
Soft pulse "whistle" verse lead, FM brass chorus, e-piano, slap bass, glockenspiel, strings.

Usage:  python3 grassy_meadow.py <output.wav>
Form (40 bars, ~76 s, loops back to bar 0):
  A  verse    whistle lead, e-piano comping, glock offbeats, light kit
  B  chorus   brass lead (+ whistle an octave up), stabs, strings, claps
  A2 verse 2  whistle + glock doubling
  C  bridge   glockenspiel melody, half-time; lifts to Ebmaj7 -> F before landing on D7
  B2 chorus 2 with harmony line -> loops to G
"""
import sys
from arcade_synth import Song, midi, check_score, SR

BPM, SWING, SEED = 126, 0.12, 4404

CH = {'G': ([59, 62, 67], 43), 'Em7': ([62, 64, 67, 71], 40), 'Cmaj7': ([59, 64, 67], 36), 'D7': ([60, 62, 66], 38),
      'Am7': ([60, 64, 67], 45), 'C': ([60, 64, 67], 36), 'D': ([62, 66, 69], 38), 'Bm7': ([62, 66, 69], 35),
      'E7': ([62, 64, 68], 40), 'Ebmaj7': ([62, 67, 70], 39), 'F': ([60, 65, 69], 41), 'Am7/D': ([60, 64, 67, 69], 38)}

VERSE = [('D5',2),('G5',2),('B5',4),('A5',2),('G5',2),('D5',4),
         ('E5',2),('G5',2),('B5',4),('D6',4),('B5',4),
         ('C6',4),('B5',2),('A5',2),('G5',4),('E5',4),
         ('F#5',6),('E5',2),('D5',8),
         ('D5',2),('G5',2),('B5',4),('A5',2),('G5',2),('D6',4),
         ('D6',4),('B5',2),('G5',2),('E5',4),('G5',4),
         ('A5',4),('C6',2),('A5',2),('G5',4),('E5',4),
         ('F#5',4),('A5',2),('F#5',2),('D5',4),('R',4)]
VERSE_CH = ['G','Em7','Cmaj7','D7','G','Em7','Am7','D7']

CHORUS = [('E5',3),('G5',3),('C6',2),('B5',4),('G5',4),
          ('F#5',3),('A5',3),('D6',2),('C6',4),('A5',4),
          ('B5',6),('A5',2),('F#5',4),('D5',4),
          ('E5',3),('G#5',3),('B5',2),('D6',8),
          ('C6',3),('B5',3),('A5',2),('E5',4),('G5',4),
          ('F#5',3),('A5',3),('C6',2),('B5',4),('A5',4),
          ('G5',6),('B5',2),('D6',8),
          ('D6',2),('B5',2),('G5',4),('A5',2),('B5',2),('D5',4)]
CHORUS_CH = ['C','D','Bm7','E7','Am7','D7','G','G']

BRIDGE = [('E6',4),('D6',4),('B5',4),('G5',4),
          ('D6',4),('B5',4),('A5',4),('F#5',4),
          ('C6',4),('B5',4),('A5',4),('E5',4),
          ('F#5',8),('A5',8),
          ('G5',4),('Bb5',4),('D6',4),('Bb5',4),
          ('A5',4),('C6',4),('F6',4),('C6',4),
          ('E6',8),('D6',8),
          ('C6',4),('A5',4),('F#5',4),('D5',4)]
BRIDGE_CH = ['Cmaj7','Bm7','Am7','D7','Ebmaj7','F','Am7/D','D7']

SECTIONS = [('A', 'verse', VERSE, VERSE_CH), ('B', 'chorus', CHORUS, CHORUS_CH), ('A2', 'verse', VERSE, VERSE_CH),
            ('C', 'bridge', BRIDGE, BRIDGE_CH), ('B2', 'chorus', CHORUS, CHORUS_CH)]
G_MAJOR = [7, 9, 11, 0, 2, 4, 6]

def third_below(m):
    pc = m % 12
    if pc not in G_MAJOR:
        return m - 4
    i = G_MAJOR.index(pc)
    return m - ((pc - G_MAJOR[(i - 2) % 7]) % 12)

def drums(s, style, bar, local, last):
    H = []
    if style == 'verse':
        H += [('kick', st, 1.0) for st in (0, 8, 10)] + [('snare', st, 0.6) for st in (4, 12)]
        H += [('hat', st, 0.9 if st % 4 == 2 else 0.6) for st in range(0, 16, 2)]
        H += [('shaker', st, 0.55) for st in range(1, 16, 2)]
    elif style == 'chorus':
        H += [('kick', st, 1.0) for st in (0, 6, 8, 10)] + [('snare', st, 1.0) for st in (4, 12)]
        H += [('clap', st, 0.9) for st in (4, 12)]
        H += [('ohat' if st % 4 == 2 else 'hat', st, 0.9) for st in range(0, 16, 2)]
        H += [('shaker', st, 0.6) for st in range(1, 16, 2)]
    elif style == 'bridge':
        H += [('kick', 0, 0.8), ('kick', 10, 0.6), ('snare', 8, 0.7)]
        H += [('shaker', st, 0.5) for st in range(0, 16, 2)] + [('rim', st, 0.6) for st in (4, 12)]
    if last:
        H = [h for h in H if h[1] < 8 or h[0] in ('shaker', 'hat')]
        if style == 'bridge':
            H += [('tomH', 8, 0.8), ('tomH', 10, 0.8), ('tomM', 12, 0.9), ('tomM', 13, 0.8), ('tomL', 14, 1.0), ('tomL', 15, 0.9)]
            s.reverse_cymbal_into(bar + 1)
        else:
            H += [('kick', 8, 1.0), ('snare', 10, 0.8), ('snare', 11, 0.6), ('tomM', 12, 0.9), ('tomM', 13, 0.8),
                  ('tomL', 14, 1.0), ('snare', 15, 0.9)]
    for name, st, v in H:
        s.hit(name, bar, st, v)

def bass(style, ch, nxt):
    r = CH[ch][1]; nr = CH[nxt][1]
    if style == 'bridge':
        return [(0, 6, r), (6, 2, r + 7), (8, 6, r + 12), (14, 2, nr - 1)]
    return [(0, 2, r), (2, 2, r + 12), (4, 2, r + 7), (6, 2, r + 12), (8, 2, r), (10, 2, r + 7), (12, 2, r + 9), (14, 2, nr - 1)]

COMP = {'verse': [(0, 3), (3, 3), (6, 2), (10, 4)], 'chorus': [(0, 2), (3, 2), (6, 2), (8, 2), (11, 2), (14, 2)],
        'bridge': [(0, 8), (8, 8)]}

def main(out):
    for name, mel in (('VERSE', VERSE), ('CHORUS', CHORUS), ('BRIDGE', BRIDGE)):
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
                s.mix(s.slap_bass(m, int(ln * s.STEP * 0.8)), s.pos(bar, st), 0.30)
            for st, ln in COMP[style]:
                gate = int(ln * s.STEP * (0.6 if style == 'chorus' else 0.9))
                for k, m in enumerate(voic):
                    s.mix(s.epiano(m, gate), s.pos(bar, st) + 50 * k, 0.07, -0.25 + 0.17 * k, 0.25)
            if style in ('verse',):
                for k, st in enumerate((2, 6, 10, 14)):                 # glock offbeats
                    m = sorted(voic)[k % len(voic)] + 24
                    s.mix(s.bell(m, ratio=3.0, index=1.6, decay=0.35, sec=0.8), s.pos(bar, st), 0.035, 0.5 if k % 2 else -0.5, 0.3)
            if style in ('chorus', 'bridge'):
                s.mix(s.strings(tuple(m - 12 for m in voic[:3]), s.BAR, 0.15 if style == 'chorus' else 0.7),
                      bar * s.BAR, 0.045, 0.1, 0.35)
            drums(s, style, bar, b, b == 7)
        if style == 'chorus':
            s.hit('crash', b0, 0, 1.0)
        if name == 'A':
            s.hit('crash', b0, 0, 0.5)

        for m, start, ln in s.notes(mel, b0):
            if m is None:
                continue
            gate = ln - 500 if ln >= 4 * s.STEP else int(ln * 0.8)
            if style == 'verse':
                s.mix(s.pulse_lead(m, gate, 0.5, 0.3), start, 0.15, -0.1, 0.3)
                if name == 'A2':
                    s.mix(s.bell(m + 12, ratio=3.0, index=1.4, decay=0.4, sec=1.0), start, 0.06, 0.35, 0.3)
            elif style == 'chorus':
                s.mix(s.brass(m, gate, -0.07, 2.3), start, 0.15, -0.3, 0.3)
                s.mix(s.brass(m, gate, 0.07, 2.3), start + 200, 0.15, 0.3, 0.3)
                s.mix(s.pulse_lead(m + 12, gate, 0.25, 0.2), start, 0.035, 0.0, 0.25)
                if name == 'B2':
                    s.mix(s.brass(third_below(m), gate, 0.0, 1.8), start, 0.09, 0.5, 0.3)
            elif style == 'bridge':
                s.mix(s.bell(m, ratio=3.0, index=1.8, decay=0.6, sec=1.6), start, 0.16, -0.15, 0.4)
                s.mix(s.pulse_lead(m - 12, gate, 0.5, 0.25), start, 0.06, 0.2, 0.35)
    s.finish(out, [n for n, _, _, _ in SECTIONS])
    print('wrote', out)

if __name__ == '__main__':
    main(sys.argv[1])
