"""
"Harvey Rainbow Combo" - loopable combo-mode music for AnalogueWay (max-combo state).
v2: longer (2 bars, ~3s) and punchier - a real "new state unlocked" feel, not just a blip.
Fast, pumped, 8-bit/chiptune-voiced Japanese battle-theme riff: In scale on D, 160 BPM,
taiko-style kick/tom/snare pulse + sub-weight slap bass on the beat, twin square-pulse lead
(NES-style) with brass stabs doubling the strong beats for extra power, sparkly bell top layer.
Two-bar phrase that loops seamlessly for as long as the combo holds - 1 rep for a short combo,
many reps back-to-back for a long chain.

Usage:  python3 harvey_rainbow_combo_loop.py <output.wav>
"""
import sys
from arcade_synth import Song, midi, check_score

BPM, SWING, BARS, SEED = 160, 0.0, 2, 2607

# In scale on D (matches ToriGate_ToSpirit's A-G-Eb-D phrase and SnowTemple_Modern's palette)
A = [('D5',1),('Eb5',1),('G5',1),('A5',1),('Bb5',1),('A5',1),('G5',1),('Eb5',1)] * 2   # bar 1: running riff
B = [('G5',1),('A5',1),('Bb5',1),('D6',1),('Bb5',1),('A5',1),('G5',1),('A5',1),
     ('Bb5',2),('D6',2),('Bb5',2),('G5',2)]  # bar 2: climbs and lands hard
RIFF = A + B

STABS = {0, 8}  # strong beats that get a brass stab + bass hit, for extra punch

def drums(s, bar):
    # taiko-style pulse, thicker: kick on every beat, both toms rolling, snare cracking the strong beats
    s.hit('kick', bar, 0, 1.0); s.hit('kick', bar, 4, 0.9); s.hit('kick', bar, 8, 1.0); s.hit('kick', bar, 12, 0.9)
    s.hit('tomL', bar, 2, 0.7); s.hit('tomM', bar, 6, 0.7); s.hit('tomL', bar, 10, 0.7); s.hit('tomM', bar, 14, 0.8)
    s.hit('snare', bar, 0, 0.7); s.hit('snare', bar, 8, 0.7)   # extra crack right on the downbeats, with the kick
    s.hit('clap', bar, 4, 0.55); s.hit('clap', bar, 12, 0.55)
    for st in range(0, 16, 2):
        s.hit('hat', bar, st, 0.6 if st % 4 else 0.85)

def main(out):
    check_score('RIFF', RIFF, bars=BARS)
    s = Song(BPM, SWING, BARS, SEED)
    s.hit('crash', 0, 0, 0.8)
    for bar in range(BARS):
        drums(s, bar)
        for st in STABS:
            # bass gives it sub-weight, brass gives it a hard-hitting stab on top - this is where the "punch" lives
            s.mix(s.slap_bass(midi('D3'), int(0.9 * s.STEP)), s.pos(bar, st), 0.42)
            s.mix(s.brass(midi('D5'), int(0.9 * s.STEP), bright=2.8), s.pos(bar, st), 0.22, 0.0, 0.25)
    for m, start, ln in s.notes(RIFF, 0):
        if m is None:
            continue
        gate = int(ln * 0.85)
        # twin NES-style square pulses (classic 2A03 lead stack), tight and punchy
        s.mix(s.pulse_lead(m, gate, duty=0.5, vib=0.0), start, 0.34, -0.12, 0.15)
        s.mix(s.pulse_lead(m, gate, duty=0.25, vib=0.0), start, 0.19, 0.12, 0.15)
        # sparkly rainbow shimmer layer, an octave up, sparse+quiet
        s.mix(s.bell(m + 12, ratio=3.2, index=1.4, decay=0.3, sec=0.5), start, 0.06, 0.4, 0.3)
    s.finish(out, ['A', 'B'], target_rms_db=-11.5)   # louder/punchier than the default -13.5
    print('wrote', out)

if __name__ == '__main__':
    main(sys.argv[1])
