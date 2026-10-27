"""
"Harvey Rainbow Combo" entry sting - one-shot hit for AnalogueWay, plays once the instant
Harvey reaches rainbow combo, then hands off to harvey_rainbow_combo_loop's loop.
Hard taiko-style hit + shamisen/koto-style pulse stab + bell shimmer tail, same In-scale-on-D
palette and 160 BPM feel as the loop, so the sting->loop handoff reads as one musical idea.
Non-looping (unlike Song.finish(), this does NOT wrap the tail into the start).

Usage:  python3 harvey_rainbow_combo_sting.py <output.wav>
"""
import sys, math, wave
from array import array
from arcade_synth import Song, midi, SR

BPM, SEED = 160, 2607

def main(out):
    s = Song(BPM, 0.0, 1, SEED)
    # hard taiko-style stab: kick + both toms together, crash on top
    s.hit('kick', 0, 0, 1.0)
    s.hit('tomL', 0, 0, 0.9)
    s.hit('tomM', 0, 0, 0.8)
    s.hit('crash', 0, 0, 0.5)
    # shamisen/koto stab, rendered as the twin NES pulses an octave up from the loop's riff start
    root = midi('D6')
    gate = int(0.12 * SR)
    s.mix(s.pulse_lead(root, gate, duty=0.5, vib=0.0), 0, 0.55, -0.1, 0.2)
    s.mix(s.pulse_lead(root, gate, duty=0.25, vib=0.0), 0, 0.30, 0.1, 0.2)
    # bright rainbow-shimmer bell tail
    s.mix(s.bell(root + 12, ratio=3.4, index=2.2, decay=0.35, sec=0.6), 0, 0.22, 0.3, 0.35)
    s.mix(s.bell(root + 19, ratio=2.8, index=1.8, decay=0.3, sec=0.5), int(0.03 * SR), 0.14, -0.3, 0.35)

    s._reverb()
    N = int(1.3 * SR)                      # ~1.3s, no loop-wrap - this is a one-shot
    L, R = s.L[:N], s.R[:N]
    rms = math.sqrt(sum(x * x + y * y for x, y in zip(L, R)) / (2 * N))
    drive = 10 ** (-10.5 / 20) / max(rms, 1e-9)   # louder/punchier than the loop's -13.5 dB target
    ol = [math.tanh(v * drive) for v in L]
    orr = [math.tanh(v * drive) for v in R]
    pk = max(max(map(abs, ol)), max(map(abs, orr)))
    sc = min(1.0, 0.95 / pk)
    pcm = array('h', bytes(4 * N))
    for i in range(N):
        pcm[2 * i] = int(ol[i] * sc * 32767)
        pcm[2 * i + 1] = int(orr[i] * sc * 32767)
    with wave.open(out, 'wb') as w:
        w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR); w.writeframes(pcm.tobytes())
    print(f'wrote {out}  {N/SR:.2f}s, peak {20*math.log10(pk*sc):.1f} dBFS')

if __name__ == '__main__':
    main(sys.argv[1])
