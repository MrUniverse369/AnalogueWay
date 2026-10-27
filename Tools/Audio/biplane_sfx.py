"""INTRO (2026-10-26): the biplane's engine for the Grassy Green opening (PlaneCrashIntro). Pure Python, stdlib only,
on the japan_sfx helpers.

  Sound/World/BiplaneEngine.wav     2 s seamless loop: a putt-putt radial engine (firing pulses over a low throbbing
                                    drone and a little exhaust hiss); the cutscene bends its pitch and volume itself
  Sound/World/BiplaneBackfire.wav   a cough: a dull pop with a short rattle, played at each sputter

Usage:  python3 Tools/Audio/biplane_sfx.py [Assets folder]
"""
import math
import os
import random
import sys
import wave
from array import array

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from japan_sfx import SR, TAU, buf, add, write, reverb  # noqa: E402


def engine_loop(rng, sec=2.0):
    """Periodic over exactly `sec` seconds (every frequency used divides into it), so it loops without a click."""
    n = int(sec * SR)
    out = array('d', bytes(8 * n))
    pulses, f0 = 26.0, 52.0  # firing pulses per second, drone fundamental: both whole numbers of cycles per loop
    lp = 0.0
    for i in range(n):
        t = i / SR
        throb = 0.85 + 0.15 * math.sin(TAU * 6.5 * t)
        v = 0.45 * math.sin(TAU * f0 * t) + 0.22 * math.sin(TAU * 2 * f0 * t + 0.7) + 0.08 * math.sin(TAU * 3 * f0 * t)
        tp = (t * pulses % 1.0) / pulses  # seconds since the last firing pulse
        v += 0.9 * math.exp(-tp / 0.011) * math.sin(TAU * 190 * tp)
        lp += 0.18 * (rng.uniform(-1, 1) - lp)
        v += 0.14 * lp
        out[i] = v * throb
    fade = int(0.05 * SR)  # the hiss isn't periodic: cross-fade the tail into the head
    for i in range(fade):
        a = i / fade
        out[n - fade + i] = out[n - fade + i] * (1 - a) + out[i] * a
    return out


def backfire(rng):
    m = buf(0.32)
    n = len(m)
    ph = 0.0
    for i in range(n):
        t = i / SR
        f = 55 + 160 * math.exp(-t / 0.03)
        ph += TAU * f / SR
        v = math.sin(ph) * math.exp(-t / 0.05) * 1.2  # the pop
        v += rng.uniform(-1, 1) * math.exp(-t / 0.012) * 0.9  # the bang's crack
        for k in range(3):  # a short rattle after it
            tk = t - 0.06 - k * 0.045
            if tk > 0:
                v += rng.uniform(-1, 1) * math.exp(-tk / 0.006) * 0.35 * (0.8 ** k)
        m[i] = v
    wl, wr = reverb(m, wet=0.25)
    return [wl, wr]


def write_loop(path, mono, peak_db):
    """A plain mono WAV with no end fade (japan_sfx.write fades the tail, which would dent a loop)."""
    pk = max(abs(v) for v in mono) or 1.0
    g = 10 ** (peak_db / 20) / pk
    pcm = array('h', (int(max(-1.0, min(1.0, v * g)) * 32767) for v in mono))
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with wave.open(path, 'wb') as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())
    print(f'  {os.path.relpath(path)}  {len(mono) / SR:.2f}s  mono loop  peak {peak_db} dBFS')


def main():
    root = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'Assets')
    rng = random.Random(1926)
    write_loop(os.path.join(root, 'Sound/World/BiplaneEngine.wav'), engine_loop(rng), -6.0)
    write(os.path.join(root, 'Sound/World/BiplaneBackfire.wav'), backfire(rng), -1.5, loud_db=-23.0)


if __name__ == '__main__':
    main()
