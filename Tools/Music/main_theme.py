"""
"Harvey's Theme" - loopable main menu music for Harvey's Nine Lives.
Warm, welcoming adventure: D major with a mixolydian C for lift, 104 BPM, lightly swung.
Flute and plucked harp for the explorer, brass for the call to adventure, bells for the old tombs.

Usage:  python3 main_theme.py <output.wav>
Form (32 bars, ~74 s, loops back to bar 0):
  A  intro    flute melody over harp arpeggios and a soft string pad, almost no drums (dawn at the dig site)
  B  theme    brass + flute lead, strings, slap bass, light kit: setting out
  C  bridge   bells over minor chords, half-time: the tombs and their secrets
  B2 theme    the theme again with a harmony line, fuller kit -> loops to the intro
"""
import sys
from arcade_synth import Song, check_score

BPM, SWING, SEED = 104, 0.08, 9119

CH = {'D': ([62, 66, 69], 38), 'Bm': ([62, 66, 71], 35), 'G': ([62, 67, 71], 43), 'A': ([61, 64, 69], 45),
      'Em': ([64, 67, 71], 40), 'C': ([60, 64, 67], 36), 'F#m': ([61, 66, 69], 42), 'Asus4': ([62, 64, 69], 45)}

INTRO = [('A4', 8), ('D5', 8),
         ('F#5', 8), ('E5', 4), ('D5', 4),
         ('D5', 12), ('B4', 4),
         ('C#5', 8), ('E5', 8),
         ('A4', 8), ('D5', 8),
         ('F#5', 8), ('A5', 4), ('F#5', 4),
         ('G5', 8), ('E5', 4), ('C5', 4),
         ('D5', 8), ('E5', 8)]
INTRO_CH = ['D', 'Bm', 'G', 'A', 'D', 'Bm', 'C', 'Asus4']

THEME = [('D5', 3), ('E5', 1), ('F#5', 4), ('A5', 6), ('F#5', 2),
         ('G5', 4), ('E5', 4), ('C5', 4), ('E5', 4),
         ('D5', 6), ('B4', 2), ('G4', 4), ('B4', 4),
         ('A4', 12), ('R', 4),
         ('B4', 3), ('C#5', 1), ('D5', 4), ('F#5', 6), ('D5', 2),
         ('G5', 6), ('F#5', 2), ('E5', 4), ('D5', 4),
         ('E5', 4), ('G5', 4), ('B5', 4), ('A5', 4),
         ('A5', 8), ('C#6', 4), ('E5', 4)]
THEME_CH = ['D', 'C', 'G', 'D', 'Bm', 'G', 'Em', 'A']

BRIDGE = [('F#5', 4), ('D5', 4), ('B4', 4), ('D5', 4),
          ('G5', 8), ('F#5', 4), ('E5', 4),
          ('E5', 4), ('G5', 4), ('B5', 8),
          ('A5', 8), ('C#6', 4), ('A5', 4),
          ('B5', 4), ('G5', 4), ('D5', 4), ('G5', 4),
          ('A5', 4), ('E5', 4), ('C#5', 4), ('E5', 4),
          ('F#5', 8), ('D5', 4), ('B4', 4),
          ('C#5', 8), ('E5', 8)]
BRIDGE_CH = ['Bm', 'G', 'Em', 'F#m', 'G', 'A', 'Bm', 'A']

SECTIONS = [('A', 'intro', INTRO, INTRO_CH), ('B', 'theme', THEME, THEME_CH),
            ('C', 'bridge', BRIDGE, BRIDGE_CH), ('B2', 'theme', THEME, THEME_CH)]
D_MAJOR = [2, 4, 6, 7, 9, 11, 1]


def third_below(m):
    pc = m % 12
    if pc not in D_MAJOR:
        return m - 3
    i = D_MAJOR.index(pc)
    return m - ((pc - D_MAJOR[(i - 2) % 7]) % 12)


def drums(s, name, style, bar, local, last):
    H = []
    if style == 'intro':
        if local >= 4:
            H += [('shaker', st, 0.35 + 0.05 * (local - 4)) for st in range(0, 16, 2)]
        if last:
            H += [('tomL', 12, 0.5), ('tomM', 14, 0.6)]
            s.reverse_cymbal_into(bar + 1)
    elif style == 'theme':
        full = name == 'B2'
        H += [('kick', st, 1.0) for st in ((0, 7, 8) if full else (0, 8))]
        H += [('snare' if full else 'rim', st, 0.8) for st in (4, 12)]
        H += [('hat', st, 0.7 if st % 4 == 2 else 0.45) for st in range(0, 16, 2)]
        H += [('shaker', st, 0.45) for st in range(1, 16, 2)]
        if last:
            H = [h for h in H if h[1] < 8]
            H += [('tomH', 8, 0.8), ('tomH', 10, 0.8), ('tomM', 12, 0.9), ('tomL', 14, 1.0), ('snare', 15, 0.7)]
    elif style == 'bridge':
        H += [('kick', 0, 0.7), ('kick', 10, 0.5), ('rim', 8, 0.6)]
        H += [('shaker', st, 0.4) for st in range(0, 16, 4)]
        if last:
            H += [('tomM', 12, 0.7), ('tomL', 14, 0.9)]
            s.reverse_cymbal_into(bar + 1)
    for n, st, v in H:
        s.hit(n, bar, st, v)


def main(out):
    for name, mel in (('INTRO', INTRO), ('THEME', THEME), ('BRIDGE', BRIDGE)):
        check_score(name, mel)
    s = Song(BPM, SWING, 8 * len(SECTIONS), SEED)
    flat = [c for _, _, _, chs in SECTIONS for c in chs]
    for si, (name, style, mel, chords) in enumerate(SECTIONS):
        b0 = si * 8
        print(f'  {name:3s} ({style})', flush=True)
        for b, ch in enumerate(chords):
            bar = b0 + b
            voic, root = CH[ch]
            nxt_root = CH[flat[(bar + 1) % s.bars]][1]
            # harp: rolling arpeggio of the chord, up an octave
            arp = sorted(voic) + [sorted(voic)[0] + 12]
            for k, st in enumerate(range(0, 16, 2)):
                m = arp[k % len(arp)] + (0 if style == 'bridge' else 12)
                s.mix(s.pluck(m, int(s.STEP * 3), 1.4, 0.35), s.pos(bar, st), 0.07 if style == 'intro' else 0.05, -0.35 + 0.1 * (k % 4), 0.35)
            # pad
            s.mix(s.strings(tuple(m - 12 for m in voic), s.BAR, 0.6 if style != 'theme' else 0.2),
                  bar * s.BAR, 0.05 if style == 'intro' else 0.045, 0.1, 0.4)
            # bass
            if style == 'theme':
                for st, ln, m in [(0, 4, root), (4, 2, root + 7), (6, 2, root + 12), (8, 4, root), (12, 2, root + 7), (14, 2, nxt_root - 1)]:
                    s.mix(s.slap_bass(m, int(ln * s.STEP * 0.8)), s.pos(bar, st), 0.26)
            elif style == 'bridge':
                s.mix(s.slap_bass(root, int(14 * s.STEP)), s.pos(bar, 0), 0.22)
            elif b >= 4:
                s.mix(s.slap_bass(root, int(12 * s.STEP)), s.pos(bar, 0), 0.16)
            drums(s, name, style, bar, b, b == 7)
        if style == 'theme':
            s.hit('crash', b0, 0, 0.9)

        for m, start, ln in s.notes(mel, b0):
            if m is None:
                continue
            gate = ln - 500 if ln >= 4 * s.STEP else int(ln * 0.85)
            if style == 'intro':
                s.mix(s.flute(m, gate, -1.0, 1.4, 0.22), start, 0.17, -0.05, 0.4)
            elif style == 'theme':
                s.mix(s.brass(m, gate, -0.06, 2.2), start, 0.13, -0.3, 0.3)
                s.mix(s.brass(m, gate, 0.06, 2.2), start + 180, 0.13, 0.3, 0.3)
                s.mix(s.flute(m + 12, gate, -1.0, 1.2, 0.2), start, 0.07, 0.0, 0.35)
                if name == 'B2':
                    s.mix(s.brass(third_below(m), gate, 0.0, 1.8), start, 0.08, 0.5, 0.3)
            elif style == 'bridge':
                s.mix(s.bell(m, ratio=3.0, index=1.8, decay=0.6, sec=1.6), start, 0.15, -0.15, 0.45)
                s.mix(s.flute(m - 12, gate, -0.6, 1.6, 0.18), start, 0.07, 0.2, 0.4)
    s.finish(out, [n for n, _, _, _ in SECTIONS])
    print('wrote', out)


if __name__ == '__main__':
    main(sys.argv[1])
