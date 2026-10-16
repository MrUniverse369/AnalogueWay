"""
Sound effects for AnalogueWay (pure Python, stdlib only). Tuned to sit with SnowTemple_Modern (key of D).

Usage:  python3 japan_sfx.py <Assets folder>
Writes:
  Sound/Japan/ToriGate_ToLiving.wav   bright: taiko + rising koto glissando (In scale) + whoosh up + bells
  Sound/Japan/ToriGate_ToSpirit.wav   ethereal: reverse swell + falling koto glissando + whoosh down + low temple bell
  Sound/Player/Dash.wav               airy whoosh with a small arcade "zip", under 0.45 s
  Sound/Player/Footstep_1..4.wav      soft padded steps with a light thump ("cat in human form")
"""
import math, os, random, sys, wave
from array import array

SR = 44100
TAU = 2 * math.pi

def hz(m):
    return 440.0 * 2 ** ((m - 69) / 12)

def buf(sec):
    return array('d', bytes(8 * int(sec * SR)))

def add(dst, src, start, gain=1.0):
    for i in range(max(0, start), min(len(dst), start + len(src))):
        dst[i] += src[i - start] * gain

# ---------------------------------------------------------------- sources
def koto(m, sec, rng):
    f = hz(m); P = max(2, int(round(SR / f)))
    exc = [0.6 * rng.uniform(-1, 1) + 0.4 * (2 * j / P - 1) for j in range(P)]
    b = [0.5 * (exc[j] + exc[j - 1]) for j in range(P)]
    rho = 10 ** (-3 / (1.5 * SR)); n = int(sec * SR); fade = int(0.03 * SR)
    out = array('d', bytes(8 * n)); j = 0
    for i in range(n):
        cur = b[j]; nxt = b[j + 1 if j + 1 < P else 0]
        b[j] = 0.5 * (cur + nxt) * rho
        out[i] = cur * (1.0 if i < n - fade else (n - i) / fade)
        j = j + 1 if j + 1 < P else 0
    return out

def fm_bell(m, sec, ratio=3.5, index=2.6):
    f = hz(m); n = int(sec * SR); out = array('d', bytes(8 * n)); pc = pm = 0.0
    for i in range(n):
        t = i / SR
        out[i] = math.sin(pc + index * math.exp(-t / 0.3) * math.sin(pm)) * math.exp(-t / 0.7)
        pc += TAU * f / SR; pm += TAU * ratio * f / SR
    return out

def temple_bell(m, sec):
    """Bonsho-style: inharmonic partials with slow beating, long decay."""
    f = hz(m); n = int(sec * SR); out = array('d', bytes(8 * n))
    partials = [(1.0, 1.0, 2.2), (1.003, 0.6, 2.0), (2.01, 0.45, 1.4), (2.76, 0.35, 1.0), (5.4, 0.18, 0.5), (8.93, 0.08, 0.3)]
    for ratio, amp, decay in partials:
        w = TAU * f * ratio / SR
        for i in range(n):
            out[i] += amp * math.sin(w * i) * math.exp(-i / SR / decay)
    for i in range(min(n, int(0.004 * SR))):        # soft strike
        out[i] *= i / (0.004 * SR)
    return out

def taiko(sec, rng, pitch=1.0):
    n = int(sec * SR); out = array('d', bytes(8 * n)); ph = 0.0; nz = 0.0
    for i in range(n):
        t = i / SR
        ph += TAU * pitch * (60 + 55 * math.exp(-t / 0.05)) / SR
        nz += 0.08 * (rng.uniform(-1, 1) - nz)
        out[i] = math.sin(ph) * math.exp(-t / 0.35) + 2.5 * nz * math.exp(-t / 0.035)
    return out

def whoosh(sec, f_from, f_to, rng, q=1.4, attack=0.12):
    """Noise through a swept state-variable band-pass filter."""
    n = int(sec * SR); out = array('d', bytes(8 * n)); low = band = 0.0
    for i in range(n):
        t = i / n
        fc = f_from * (f_to / f_from) ** t
        fcoef = 2 * math.sin(math.pi * min(fc, SR / 6) / SR)
        x = rng.uniform(-1, 1)
        high = x - low - band / q
        band += fcoef * high
        low += fcoef * band
        env = min(1.0, t / attack) * (1 - t) ** 1.5
        out[i] = band * env
    return out

# ---------------------------------------------------------------- effects
def reverb(mono, wet=0.3, spread=23):
    """Stereo Schroeder reverb; returns (L, R) with the dry signal included."""
    n = len(mono); outs = []
    for off in (0, spread):
        acc = array('d', bytes(8 * n))
        for c in (1116, 1188, 1277, 1356):
            d = c + off; b = [0.0] * d; j = 0; lp = 0.0
            for i in range(n):
                y = b[j]; lp = y * 0.7 + lp * 0.3
                b[j] = mono[i] + lp * 0.84
                acc[i] += y
                j = j + 1 if j + 1 < d else 0
        for a in (556, 441):
            d = a + off; b = [0.0] * d; j = 0
            for i in range(n):
                bb = b[j]; x = acc[i]
                acc[i] = -x + bb; b[j] = x + bb * 0.5
                j = j + 1 if j + 1 < d else 0
        outs.append(array('d', (mono[i] + acc[i] * wet * 0.25 for i in range(n))))
    return outs

def write(path, channels, peak_db):
    n = len(channels[0])
    pk = max(max(abs(v) for v in ch) for ch in channels) or 1.0
    g = 10 ** (peak_db / 20) / pk
    fade = int(0.01 * SR)
    pcm = array('h', bytes(2 * n * len(channels)))
    for i in range(n):
        f = 1.0 if i < n - fade else (n - i) / fade
        for c, ch in enumerate(channels):
            pcm[i * len(channels) + c] = int(max(-1.0, min(1.0, ch[i] * g * f)) * 32767)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with wave.open(path, 'wb') as w:
        w.setnchannels(len(channels)); w.setsampwidth(2); w.setframerate(SR); w.writeframes(pcm.tobytes())
    print(f'  {os.path.relpath(path)}  {n / SR:.2f}s  {"stereo" if len(channels) == 2 else "mono"}  peak {peak_db} dBFS')

# ---------------------------------------------------------------- sounds
IN_SCALE_UP = [62, 63, 67, 69, 70, 74, 75, 79, 81, 82, 86]     # D Eb G A Bb, D4 -> D6

def tori_to_living(rng):
    L, R = buf(3.6), buf(3.6)
    mono = buf(3.6)
    add(mono, taiko(1.0, rng), 0, 0.55)
    add(mono, whoosh(0.5, 350, 4200, rng), 0, 0.5)
    for k, m in enumerate(IN_SCALE_UP):                        # rising "sararin" glissando
        start = int((0.03 + 0.032 * k) * SR)
        pl = koto(m, 1.2, rng)
        g = 0.35 + 0.02 * k
        add(L, pl, start, g * (0.8 if k % 2 else 0.45))
        add(R, pl, start, g * (0.45 if k % 2 else 0.8))
    t_bell = int(0.38 * SR)
    add(mono, fm_bell(86, 1.6), t_bell, 0.22)                  # D6 sparkle
    add(mono, fm_bell(81, 1.6), t_bell + 900, 0.18)            # A5
    add(mono, temple_bell(50, 2.2), t_bell, 0.28)              # D3 temple bell, light
    dry = array('d', (mono[i] + 0.5 * (L[i] + R[i]) for i in range(len(mono))))
    wet_l, wet_r = reverb(dry, wet=0.9)
    return [array('d', (L[i] + mono[i] + wet_l[i] - dry[i] for i in range(len(L)))),
            array('d', (R[i] + mono[i] + wet_r[i] - dry[i] for i in range(len(R))))]

def tori_to_spirit(rng):
    n = 3.4
    L, R, mono = buf(n), buf(n), buf(n)
    # reverse swell: a reversed reverb'd bell rising into the gate
    sw = buf(0.9)
    add(sw, fm_bell(74, 0.9, ratio=2.0, index=1.5), 0, 1.0)
    swl, _ = reverb(sw, wet=1.5)
    rev = array('d', reversed(swl))
    add(mono, rev, 0, 0.35)
    t0 = int(0.55 * SR)
    add(mono, whoosh(0.7, 4200, 300, rng, attack=0.05), t0 - int(0.1 * SR), 0.45)
    add(mono, taiko(1.0, rng, pitch=0.85), t0, 0.4)
    for k, m in enumerate(reversed(IN_SCALE_UP)):              # falling glissando
        start = t0 + int(0.04 * k * SR)
        pl = koto(m, 1.3, rng)
        add(L, pl, start, 0.38 * (0.8 if k % 2 else 0.45))
        add(R, pl, start, 0.38 * (0.45 if k % 2 else 0.8))
    add(mono, temple_bell(38, 2.2), t0 + int(0.45 * SR), 0.42)  # D2 deep temple bell
    add(mono, fm_bell(69, 1.8, ratio=1.41, index=1.2), t0 + int(0.45 * SR), 0.12)
    wet_l, wet_r = reverb(array('d', (mono[i] + 0.5 * (L[i] + R[i]) for i in range(len(mono)))), wet=1.2)
    dry = array('d', (mono[i] + 0.5 * (L[i] + R[i]) for i in range(len(mono))))
    return [array('d', (L[i] + mono[i] + wet_l[i] - dry[i] for i in range(len(L)))),
            array('d', (R[i] + mono[i] + wet_r[i] - dry[i] for i in range(len(R))))]

def dash(rng):
    n = int(0.42 * SR)
    L, R = array('d', bytes(8 * n)), array('d', bytes(8 * n))
    wl = whoosh(0.42, 500, 3600, rng, q=1.6, attack=0.1)
    wr = whoosh(0.42, 520, 3400, rng, q=1.6, attack=0.1)       # separate noise = width
    ph = 0.0; pz = 0.0
    for i in range(n):
        t = i / SR
        zf = 320 * 2 ** (min(t, 0.16) / 0.16 * 1.4)            # rising "zip"
        pz += TAU * zf / SR
        zip_ = math.sin(pz + 1.2 * math.sin(2 * pz)) * math.exp(-t / 0.07) * 0.18
        ph += TAU * (60 + 60 * math.exp(-t / 0.02)) / SR
        thump = math.sin(ph) * math.exp(-t / 0.04) * 0.35
        L[i] = wl[i] + zip_ + thump
        R[i] = wr[i] + zip_ + thump
    return [L, R]

def footstep(rng, f0, cutoff, tap):
    n = int(0.16 * SR); out = array('d', bytes(8 * n)); ph = 0.0; lp1 = lp2 = 0.0
    a = 2 * math.pi * cutoff / SR
    for i in range(n):
        t = i / SR
        ph += TAU * (f0 * (0.6 + 0.4 * math.exp(-t / 0.015))) / SR
        thump = math.sin(ph) * math.exp(-t / 0.045)
        x = rng.uniform(-1, 1)
        lp1 += a * (x - lp1); lp2 += a * (lp1 - lp2)
        pad = 3.0 * lp2 * min(1.0, t / 0.003) * math.exp(-t / 0.022)
        click = x * math.exp(-t / 0.0035) * tap
        out[i] = 0.9 * thump + pad + click
    return [out]

if __name__ == '__main__':
    root = sys.argv[1]
    rng = random.Random(2026)
    print('rendering:')
    write(os.path.join(root, 'Sound/Japan/ToriGate_ToLiving.wav'), tori_to_living(rng), -1.0)
    write(os.path.join(root, 'Sound/Japan/ToriGate_ToSpirit.wav'), tori_to_spirit(rng), -1.0)
    write(os.path.join(root, 'Sound/Player/Dash.wav'), dash(rng), -2.0)
    for k, (f0, cut, tap) in enumerate([(95, 1100, 0.10), (88, 950, 0.07), (102, 1250, 0.12), (91, 1000, 0.08)], 1):
        write(os.path.join(root, f'Sound/Player/Footstep_{k}.wav'), footstep(rng, f0, cut, tap), -4.0)
