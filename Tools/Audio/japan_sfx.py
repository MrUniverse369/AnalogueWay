"""
Sound effects for AnalogueWay (pure Python, stdlib only). Tuned to sit with SnowTemple_Modern (key of D).

Usage:  python3 japan_sfx.py <Assets folder>
Writes:
  Sound/Japan/ToriGate_ToLiving.wav   bright: taiko + rising koto glissando (In scale) + whoosh up + bells
  Sound/Japan/ToriGate_ToSpirit.wav   calm: clap + reversed koto swell + shakuhachi A-G-Eb-D + falling koto + wind chimes + drone
  Sound/Japan/DragonSummon.wav        taiko roll + rising wind + ryuteki cry + growl + thunder + koto strum
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

def loudness_db(channels):
    """Loudest 400 ms window of the mono mix, after a 100 Hz high-pass (dB re full scale).
    Same measure used to balance every game sound against the music."""
    m = [sum(ch[i] for ch in channels) / len(channels) for i in range(len(channels[0]))]
    al = 1 / (1 + 2 * math.pi * 100 / SR); y = prev = 0.0; hp = []
    for v in m:
        y = al * (y + v - prev); prev = v; hp.append(y)
    win = min(len(hp), int(0.4 * SR)); hop = int(0.1 * SR); best = 0.0
    for s in range(0, max(1, len(hp) - win + 1), hop):
        best = max(best, sum(hp[j] * hp[j] for j in range(s, s + win, 3)) / (win / 3))
    return 10 * math.log10(best + 1e-12)

def write(path, channels, peak_db, loud_db=None):
    """Normalise to loud_db (see loudness_db) if given, never letting the peak exceed peak_db."""
    n = len(channels[0])
    pk = max(max(abs(v) for v in ch) for ch in channels) or 1.0
    g = 10 ** (peak_db / 20) / pk
    if loud_db is not None:
        g = min(g, 10 ** ((loud_db - loudness_db(channels)) / 20))
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

def flute(m, sec, gate, rng, scoop=-1.0, breath=1.5, vib=0.22):
    """Shakuhachi-style breathy flute with a pitch scoop into the note."""
    f = hz(m); n = int(sec * SR); g = int(gate * SR); rel = int(0.3 * SR)
    out = array('d', bytes(8 * n)); ph = 0.0; y = 0.0; vk = vib * math.log(2) / 12
    for i in range(n):
        t = i / SR
        sc = scoop * math.exp(-t / 0.06)
        v = 0.0 if t < 0.3 else min(1.0, (t - 0.3) / 0.4) * vk * math.sin(TAU * 5.0 * t)
        ph += TAU * f * 2 ** (sc / 12) * (1 + v) / SR
        tone = math.sin(ph) + 0.18 * math.sin(2 * ph) + 0.06 * math.sin(3 * ph)
        y += 0.06 * (rng.uniform(-1, 1) - y)
        env = min(1.0, t / 0.07) if i < g else max(0.0, 1 - (i - g) / rel)
        out[i] = (tone + (breath + 2.5 * math.exp(-t / 0.08)) * y) * env
    return out

def drone(notes, sec, attack, release):
    n = int(sec * SR); out = array('d', bytes(8 * n))
    for m in notes:
        w = TAU * hz(m) / SR
        for i in range(n):
            t = i / SR
            env = min(1.0, t / attack) * min(1.0, (sec - t) / release)
            out[i] += (math.sin(w * i) + 0.25 * math.sin(2 * w * i)) * env
    return out

def hyoshigi(sec=0.15):
    n = int(sec * SR); out = array('d', bytes(8 * n))
    for i in range(n):
        t = i / SR
        out[i] = (math.sin(TAU * 1450 * t) + 0.6 * math.sin(TAU * 2380 * t)) * math.exp(-t / 0.018)
    return out

def tori_to_spirit(rng):
    """Calm, musical crossing: shakuhachi phrase on the In scale, falling koto, wind chimes, low drone."""
    n = 3.6
    L, R, mono = buf(n), buf(n), buf(n)
    add(mono, hyoshigi(), 0, 0.6)                                   # ritual clap: marks the moment of switching
    sw = koto(62, 0.6, rng)                                         # reversed koto swell into the gate
    swl, _ = reverb(sw, wet=1.4)
    add(mono, array('d', reversed(swl))[-int(0.15 * SR):], 0, 0.15)   # just the last swell into the clap
    t0 = int(0.12 * SR)
    phrase = [(69, 0.22), (67, 0.22), (63, 0.34), (62, 1.6)]        # A4 G4 Eb4 D4
    t = t0
    for m, d in phrase:
        add(mono, flute(m, d + 0.35, d, rng, scoop=-1.0 if m != 62 else -0.6), t, 0.32)
        t += int(d * SR)
    for k, m in enumerate((74, 69, 67, 62)):                        # slow falling koto: D5 A4 G4 D4
        pl = koto(m, 1.6, rng)
        st = t0 + int(k * 0.2 * SR)
        add(L, pl, st, 0.30 * (0.8 if k % 2 else 0.5))
        add(R, pl, st, 0.30 * (0.5 if k % 2 else 0.8))
    for _ in range(9):                                              # furin glass chimes
        m = rng.choice((86, 93, 98, 99))
        st = t0 + int(rng.uniform(0.6, 2.2) * SR)
        ch = fm_bell(m, 1.2, ratio=2.0, index=0.9)
        side = rng.uniform(-1, 1)
        add(L, ch, st, 0.05 * (1 - 0.5 * side)); add(R, ch, st, 0.05 * (1 + 0.5 * side))
    add(mono, drone((38, 45), 3.0, 0.6, 1.2), int(0.3 * SR), 0.05)  # D2 + A2 spirit drone
    dry = array('d', (mono[i] + 0.5 * (L[i] + R[i]) for i in range(len(mono))))
    wet_l, wet_r = reverb(dry, wet=1.2)
    return [array('d', (L[i] + mono[i] + wet_l[i] - dry[i] for i in range(len(L)))),
            array('d', (R[i] + mono[i] + wet_r[i] - dry[i] for i in range(len(R))))]

def dragon_summon(rng):
    """Taiko roll, rising wind, ryuteki (dragon flute) cry, low growl, thunder, koto strum."""
    n = 3.0
    L, R, mono = buf(n), buf(n), buf(n)
    for k, ts in enumerate((0.0, 0.16, 0.28, 0.37, 0.44)):         # accelerating taiko roll
        add(mono, taiko(0.9, rng, pitch=1.0 + 0.04 * k), int(ts * SR), 0.35 + 0.08 * k)
    big = int(0.5 * SR)
    add(mono, taiko(1.2, rng, pitch=0.8), big, 0.9)                 # the big hit
    add(mono, whoosh(0.9, 200, 2600, rng, q=1.0, attack=0.6), int(0.05 * SR), 0.55)
    cry = flute(86, 1.3, 0.9, rng, scoop=-3.0, breath=1.0, vib=0.45)   # D6 ryuteki cry, slides up 3 semitones
    add(mono, cry, big, 0.22)
    gn = int(1.4 * SR); growl = array('d', bytes(8 * gn)); ph = 0.0; nz = 0.0; lp = 0.0
    for i in range(gn):                                             # low FM growl
        t = i / SR
        nz += 0.02 * (rng.uniform(-1, 1) - nz)
        ph += TAU * (62 + 8 * math.sin(TAU * 3 * t)) / SR
        v = math.sin(ph + 2.5 * math.sin(2 * ph) + 40 * nz)
        lp += 0.08 * (v - lp)
        growl[i] = lp * min(1.0, t / 0.25) * math.exp(-max(0.0, t - 0.4) / 0.35)
    add(mono, growl, big + int(0.05 * SR), 0.55)
    tn = int(2.2 * SR); thunder = array('d', bytes(8 * tn)); l1 = l2 = 0.0
    for i in range(tn):                                             # distant thunder rumble
        t = i / SR
        x = rng.uniform(-1, 1); l1 += 0.01 * (x - l1); l2 += 0.01 * (l1 - l2)
        thunder[i] = 18 * l2 * min(1.0, t / 0.1) * math.exp(-t / 0.7)
    add(mono, thunder, big, 0.5)
    for k, m in enumerate((50, 57, 62, 69, 74)):                    # koto strum D3 A3 D4 A4 D5
        pl = koto(m, 1.6, rng)
        st = big + int((0.55 + 0.025 * k) * SR)
        add(L, pl, st, 0.3 * (0.8 if k % 2 else 0.5)); add(R, pl, st, 0.3 * (0.5 if k % 2 else 0.8))
    dry = array('d', (mono[i] + 0.5 * (L[i] + R[i]) for i in range(len(mono))))
    wet_l, wet_r = reverb(dry, wet=0.8)
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
        out[i] = 0.7 * thump + pad + click
    return [out]

if __name__ == '__main__':
    # Loudness targets (loudness_db) are set against the music at AudioManager.musicVolume 0.35.
    # Footsteps are rendered 4.4 dB hotter because AudioManager plays them at footstepVolume 0.6.
    root = sys.argv[1]
    rng = random.Random(2026)
    print('rendering:')
    write(os.path.join(root, 'Sound/Japan/ToriGate_ToLiving.wav'), tori_to_living(rng), -1.0, loud_db=-26.5)
    write(os.path.join(root, 'Sound/Japan/ToriGate_ToSpirit.wav'), tori_to_spirit(rng), -1.0, loud_db=-23.5)
    write(os.path.join(root, 'Sound/Japan/DragonSummon.wav'), dragon_summon(rng), -1.0, loud_db=-24.5)
    write(os.path.join(root, 'Sound/Player/Dash.wav'), dash(rng), -2.0, loud_db=-27.5)
    # Footsteps: thump pitched up and pad brightened so they carry on laptop/phone speakers.
    for k, (f0, cut, tap) in enumerate([(150, 2200, 0.16), (138, 1900, 0.12), (162, 2500, 0.18), (145, 2050, 0.14)], 1):
        write(os.path.join(root, f'Sound/Player/Footstep_{k}.wav'), footstep(rng, f0, cut, tap), -2.0, loud_db=-24.0)
