"""
"Snow Temple" - loopable 90s arcade / Japanese BGM for AnalogueWay's Japan levels.
Pure-Python synth (stdlib only). Output: 44.1 kHz 16-bit stereo WAV.

Form (40 bars @ 150 BPM, ~64 s, loops seamlessly back to bar 0):
  A  (8)  pulse lead melody, pad, bouncing bass, drums
  B  (8)  chorus: higher syncopated lead, 16th arpeggio, taiko hits
  A2 (8)  A again with koto doubling the lead an octave down + arp
  C  (8)  breakdown: koto melody, shakuhachi-style flute, half-time taiko + hyoshigi
  B2 (8)  chorus with a diatonic harmony line, ends on Eb -> Dm (Phrygian cadence) into the loop
Harmony: D Phrygian. Melody mostly miyako-bushi (D Eb G A Bb).
"""
import math, random, struct, wave, sys, time
from array import array

SR = 44100
BPM = 150
STEP = int(round(SR * 60 / BPM / 4))   # one 16th note = 4410 samples
BAR = 16 * STEP
TAU = 2 * math.pi
random.seed(20261016)

# ---------------------------------------------------------------- pitch helpers
PC = {'C': 0, 'C#': 1, 'Db': 1, 'D': 2, 'Eb': 3, 'E': 4, 'F': 5, 'F#': 6, 'G': 7,
      'Ab': 8, 'A': 9, 'Bb': 10, 'B': 11}
SCALE = [2, 3, 5, 7, 9, 10, 0]   # D Phrygian

def midi(name):
    return 12 * (int(name[-1]) + 1) + PC[name[:-1]]

def hz(m):
    return 440.0 * 2 ** ((m - 69) / 12)

def third_below(m):
    pc = m % 12
    if pc not in SCALE:
        return m - 3
    i = SCALE.index(pc)
    target = SCALE[(i - 2) % 7]
    d = (pc - target) % 12
    return m - d

# ---------------------------------------------------------------- score
A = [('A4',4),('G4',2),('A4',2),('D5',6),('R',2),
     ('Eb5',4),('D5',2),('Bb4',2),('G4',6),('A4',2),
     ('A4',4),('D5',2),('Eb5',2),('D5',4),('A4',4),
     ('G4',2),('A4',2),('Bb4',4),('G4',8),
     ('G4',4),('Bb4',2),('D5',2),('G5',6),('A5',2),
     ('G5',4),('Eb5',2),('D5',2),('Eb5',4),('G5',4),
     ('G5',2),('Eb5',2),('D5',2),('Bb4',2),('G4',4),('Bb4',4),
     ('D5',12),('R',4)]
A_CH = ['Dm','Eb','Dm','Eb','Gm','Cm','Eb','Dm']

B = [('D5',3),('D5',3),('Bb4',2),('D5',2),('Eb5',2),('D5',4),
     ('Eb5',3),('Eb5',3),('C5',2),('Eb5',2),('G5',2),('Eb5',4),
     ('A5',4),('G5',2),('A5',2),('D6',4),('A5',4),
     ('G5',2),('A5',2),('G5',2),('Eb5',2),('D5',8),
     ('D5',3),('D5',3),('Bb4',2),('D5',2),('Eb5',2),('G5',4),
     ('Eb5',3),('G5',3),('Eb5',2),('G5',2),('A5',2),('G5',4),
     ('Bb5',4),('A5',2),('G5',2),('Eb5',4),('D5',2),('Eb5',2),
     ('G5',8),('A5',2),('G5',2),('Eb5',2),('D5',2)]
B_CH = ['Bb','Cm','Dm','Dm','Bb','Cm','Eb','Eb']

C_KOTO = [('G4',2),('Bb4',2),('D5',4),('Eb5',2),('D5',2),('Bb4',4),
          ('A4',4),('G4',2),('A4',2),('D4',8),
          ('G4',2),('Bb4',2),('D5',4),('G5',4),('Eb5',2),('D5',2),
          ('D5',4),('A4',4),('G4',2),('A4',2),('D5',4),
          ('Eb5',4),('G5',4),('Bb5',4),('G5',4),
          ('Eb5',2),('D5',2),('Bb4',2),('G4',2),('Bb4',4),('Eb5',4),
          ('D5',6),('Eb5',2),('D5',4),('A4',4),
          ('D5',16)]
C_FLUTE = [('D5',32),('Bb4',16),('A4',16),('G4',16),('Bb4',16),('A4',16),('D5',16)]
C_CH = ['Gm','Dm','Gm','Dm','Eb','Eb','Dm','Dm']

CHORD_PCS = {'Dm': (2, 5, 9), 'Eb': (3, 7, 10), 'Cm': (0, 3, 7), 'Gm': (7, 10, 2), 'Bb': (10, 2, 5)}
BASS_ROOT = {'Dm': 38, 'Eb': 39, 'Cm': 36, 'Gm': 43, 'Bb': 34}

SECTIONS = [('A', A, A_CH), ('B', B, B_CH), ('A2', A, A_CH), ('C', C_KOTO, C_CH), ('B2', B, B_CH)]
TOTAL_BARS = 8 * len(SECTIONS)
LOOP_LEN = TOTAL_BARS * BAR
TAIL = 4 * SR

def voicing(chord, low):
    """Chord tones stacked upward starting from the root placed at or above `low`."""
    pcs = CHORD_PCS[chord]
    r = low + ((pcs[0] - low) % 12)
    out = [r]
    for pc in pcs[1:]:
        n = out[-1] + 1
        while n % 12 != pc:
            n += 1
        out.append(n)
    return out

# ---------------------------------------------------------------- mixer
class Bus:
    def __init__(self, n):
        self.buf = array('d', bytes(8 * n))

    def add(self, src, start, gain=1.0):
        b = self.buf
        end = min(len(b), start + len(src))
        for i in range(start, end):
            b[i] += src[i - start] * gain

L = array('d', bytes(8 * (LOOP_LEN + TAIL)))
R = array('d', bytes(8 * (LOOP_LEN + TAIL)))

def mix(src, start, gain, pan):
    a = (pan + 1) * math.pi / 4
    gl, gr = math.cos(a) * gain, math.sin(a) * gain
    end = min(len(L), start + len(src))
    for i in range(start, end):
        v = src[i - start]
        L[i] += v * gl
        R[i] += v * gr

# ---------------------------------------------------------------- oscillators
def polyblep(t, dt):
    if t < dt:
        t /= dt
        return t + t - t * t - 1.0
    if t > 1.0 - dt:
        t = (t - 1.0) / dt
        return t * t + t + t + 1.0
    return 0.0

def env_value(i, n_gate, a, d, s, r):
    if i < a:
        return i / a
    if i < a + d:
        return 1.0 - (1.0 - s) * (i - a) / d
    if i < n_gate:
        return s
    k = (i - n_gate) / r
    return s * (1.0 - k) if k < 1.0 else 0.0

_cache = {}

def pulse_note(m, gate, duty, vib=0.0, a=0.004, d=0.08, s=0.7, r=0.04):
    key = ('pulse', m, gate, duty, vib, a, d, s, r)
    if key in _cache:
        return _cache[key]
    f = hz(m)
    A_, D_, R_ = int(a * SR), int(d * SR), int(r * SR)
    n = gate + R_
    out = array('d', bytes(8 * n))
    t = 0.0
    vib_start = int(0.12 * SR)
    k = vib * math.log(2) / 12
    for i in range(n):
        if vib and i > vib_start:
            depth = min(1.0, (i - vib_start) / (0.25 * SR)) * k
            ff = f * (1.0 + depth * math.sin(TAU * 5.5 * i / SR))
        else:
            ff = f
        dt = ff / SR
        v = 1.0 if t < duty else -1.0
        v += polyblep(t, dt)
        t2 = t - duty
        if t2 < 0.0:
            t2 += 1.0
        v -= polyblep(t2, dt)
        v -= (2 * duty - 1)
        out[i] = v * env_value(i, gate, A_, D_, s, R_)
        t += dt
        if t >= 1.0:
            t -= 1.0
    _cache[key] = out
    return out

def bass_note(m, gate, soft=False):
    key = ('bass', m, gate, soft)
    if key in _cache:
        return _cache[key]
    f = hz(m)
    R_ = int(0.03 * SR)
    n = gate + R_
    out = array('d', bytes(8 * n))
    t, y = 0.0, 0.0
    dt = f / SR
    sq_mix = 0.0 if soft else 0.35
    alpha = 0.10 if soft else 0.16
    for i in range(n):
        tri = 4.0 * abs(t - 0.5) - 1.0
        sq = (1.0 if t < 0.5 else -1.0) + polyblep(t, dt) - polyblep((t + 0.5) % 1.0, dt)
        x = tri * (1 - sq_mix) + sq * sq_mix
        y += alpha * (x - y)
        out[i] = y * env_value(i, gate, int(0.003 * SR), int(0.12 * SR), 0.75, R_)
        t += dt
        if t >= 1.0:
            t -= 1.0
    _cache[key] = out
    return out

def pad_chord(notes, n, attack, gain_lp=0.07):
    key = ('pad', tuple(notes), n, attack)
    if key in _cache:
        return _cache[key]
    R_ = int(0.25 * SR)
    total = n + R_
    out = array('d', bytes(8 * total))
    A_ = int(attack * SR)
    for m in notes:
        for det in (-0.07, 0.07):
            f = hz(m + det)
            dt = f / SR
            t = random.random()
            y = 0.0
            for i in range(total):
                v = 2.0 * t - 1.0 - polyblep(t, dt)
                y += gain_lp * (v - y)
                out[i] += y * env_value(i, n, A_, 1, 1.0, R_)
                t += dt
                if t >= 1.0:
                    t -= 1.0
    _cache[key] = out
    return out

def koto_note(m, n):
    """Karplus-Strong pluck with a bright, short excitation (koto / shamisen-ish)."""
    key = ('koto', m, n)
    if key in _cache:
        return _cache[key]
    f = hz(m)
    N = max(2, int(round(SR / f)))
    exc = [random.uniform(-1, 1) for _ in range(N)]
    # mix in a little of a sawtooth for a sharper attack, then soften once
    exc = [0.6 * e + 0.4 * (2.0 * j / N - 1.0) for j, e in enumerate(exc)]
    buf = [0.5 * (exc[j] + exc[j - 1]) for j in range(N)]
    rho = 10 ** (-3 / (1.6 * SR))
    out = array('d', bytes(8 * n))
    fade = int(0.03 * SR)
    j = 0
    for i in range(n):
        cur = buf[j]
        nxt = buf[j + 1 if j + 1 < N else 0]
        buf[j] = 0.5 * (cur + nxt) * rho
        g = 1.0 if i < n - fade else (n - i) / fade
        out[i] = cur * g
        j += 1
        if j == N:
            j = 0
    _cache[key] = out
    return out

def flute_note(m, gate):
    f = hz(m)
    R_ = int(0.35 * SR)
    n = gate + R_
    out = array('d', bytes(8 * n))
    ph = 0.0
    y = 0.0
    k = 0.18 * math.log(2) / 12
    vstart = int(0.3 * SR)
    A_ = int(0.18 * SR)
    for i in range(n):
        depth = 0.0 if i < vstart else min(1.0, (i - vstart) / SR) * k
        ff = f * (1.0 + depth * math.sin(TAU * 5.0 * i / SR))
        ph += TAU * ff / SR
        tone = math.sin(ph) + 0.22 * math.sin(2 * ph) + 0.08 * math.sin(3 * ph)
        y += 0.05 * (random.uniform(-1, 1) - y)          # breath noise, low-passed
        out[i] = (tone + 1.6 * y) * env_value(i, gate, A_, 1, 1.0, R_)
    return out

# ---------------------------------------------------------------- drums (rendered once)
def render_drum(n_sec, fn):
    n = int(n_sec * SR)
    out = array('d', bytes(8 * n))
    state = {}
    for i in range(n):
        out[i] = fn(i / SR, state)
    return out

def kick_fn(t, s):
    s['ph'] = s.get('ph', 0.0) + TAU * (45 + 115 * math.exp(-t / 0.035)) / SR
    click = random.uniform(-1, 1) * math.exp(-t / 0.002) * 0.3
    return math.sin(s['ph']) * math.exp(-t / 0.13) + click

def snare_fn(t, s):
    x = random.uniform(-1, 1)
    hp = x - s.get('p', 0.0)
    s['p'] = x
    return 0.45 * math.sin(TAU * 185 * t) * math.exp(-t / 0.05) + 0.6 * hp * math.exp(-t / 0.11)

def hat_fn(decay):
    def fn(t, s):
        x = random.uniform(-1, 1)
        h1 = x - s.get('p', 0.0)
        s['p'] = x
        h2 = h1 - s.get('q', 0.0)
        s['q'] = h1
        return 0.5 * h2 * math.exp(-t / decay)
    return fn

def taiko_fn(t, s):
    s['ph'] = s.get('ph', 0.0) + TAU * (62 + 58 * math.exp(-t / 0.05)) / SR
    s['n'] = s.get('n', 0.0) + 0.08 * (random.uniform(-1, 1) - s.get('n', 0.0))
    return math.sin(s['ph']) * math.exp(-t / 0.33) + 2.5 * s['n'] * math.exp(-t / 0.035)

def hyoshigi_fn(t, s):
    return (math.sin(TAU * 1450 * t) + 0.6 * math.sin(TAU * 2380 * t)) * math.exp(-t / 0.018)

def crash_fn(t, s):
    x = random.uniform(-1, 1)
    h1 = x - s.get('p', 0.0)
    s['p'] = x
    return 0.5 * h1 * math.exp(-t / 0.55)

DR = {
    'kick': render_drum(0.35, kick_fn),
    'snare': render_drum(0.30, snare_fn),
    'hat': render_drum(0.06, hat_fn(0.012)),
    'ohat': render_drum(0.30, hat_fn(0.08)),
    'taiko': render_drum(0.9, taiko_fn),
    'hyo': render_drum(0.12, hyoshigi_fn),
    'crash': render_drum(1.8, crash_fn),
}
DR_MIX = {'kick': (0.62, 0.0), 'snare': (0.36, 0.05), 'hat': (0.10, 0.35), 'ohat': (0.09, 0.35),
          'taiko': (0.55, -0.15), 'hyo': (0.16, 0.45), 'crash': (0.15, -0.3)}

def hit(name, bar, step, vel=1.0):
    g, p = DR_MIX[name]
    jitter = random.randint(-40, 40) if name in ('hat', 'ohat') else 0
    mix(DR[name], max(0, bar * BAR + step * STEP + jitter), g * vel, p)

def drums_for_bar(kind, bar, local, fill):
    hits = []
    if kind == 'A':
        hits += [('kick', s, 1.0) for s in (0, 8, 10)]
        hits += [('snare', s, 1.0) for s in (4, 12)]
        for s in range(0, 16, 2):
            if s == 14 and local % 2 == 1:
                hits.append(('ohat', s, 1.0))
            else:
                hits.append(('hat', s, 1.0 if s % 4 == 2 else 0.6))
    elif kind == 'B':
        hits += [('kick', s, 1.0) for s in (0, 6, 8, 14)]
        hits += [('snare', s, 1.0) for s in (4, 12)]
        hits += [('hat', s, (1.0, 0.45, 0.7, 0.45)[s % 4]) for s in range(16)]
        hits.append(('taiko', 0, 0.55))
        if local % 2 == 1:
            hits.append(('taiko', 10, 0.45))
    elif kind == 'C':
        hits += [('kick', 0, 0.65), ('snare', 8, 0.55)]
        hits += [('hyo', s, v) for s, v in ((4, 0.8), (12, 0.8), (14, 0.5))]
        hits += [('taiko', 0, 0.5), ('taiko', 11, 0.33)]
        hits += [('hat', s, 0.28) for s in range(0, 16, 2)]
    if fill:
        hits = [h for h in hits if h[1] < 8 or h[0] in ('hat',)]
        if kind == 'C':
            # build-up into the chorus: 16th snares rising, taiko on the beats
            hits = [('snare', s, 0.25 + 0.75 * s / 15) for s in range(16)]
            hits += [('taiko', s, 0.9) for s in (0, 4, 8, 12)]
            hits += [('kick', s, 0.8) for s in (0, 8)]
        else:
            hits += [('snare', s, v) for s, v in ((8, 0.8), (10, 0.8), (12, 0.9), (13, 0.7), (14, 1.0), (15, 0.8))]
            hits += [('taiko', 12, 0.8), ('taiko', 14, 1.0), ('kick', 8, 1.0)]
    for name, s, v in hits:
        hit(name, bar, s, v)

# ---------------------------------------------------------------- render
def render():
    t0 = time.time()
    lead = Bus(LOOP_LEN + TAIL)
    for si, (name, melody, chords) in enumerate(SECTIONS):
        bar0 = si * 8
        kind = name[0]
        print(f'  section {name}', flush=True)

        # pad
        attack = 0.5 if kind == 'C' else 0.06
        pad_gain = 0.05 if kind == 'C' else 0.055
        for b, ch in enumerate(chords):
            mix(pad_chord(voicing(ch, 50), BAR, attack), (bar0 + b) * BAR, pad_gain, -0.1)

        # bass
        for b, ch in enumerate(chords):
            root = BASS_ROOT[ch]
            if kind == 'C':
                for s in (0, 8):
                    mix(bass_note(root, int(7.5 * STEP), soft=True), (bar0 + b) * BAR + s * STEP, 0.28, 0.0)
            else:
                for k, s in enumerate(range(0, 16, 2)):
                    m = root + (12 if k % 2 else 0)
                    mix(bass_note(m, int(1.6 * STEP)), (bar0 + b) * BAR + s * STEP, 0.30, 0.0)

        # arpeggio
        if name in ('B', 'A2', 'B2'):
            for b, ch in enumerate(chords):
                tones = voicing(ch, 57) + [voicing(ch, 57)[0] + 12]
                for s in range(16):
                    m = tones[s % 4]
                    mix(pulse_note(m, int(0.55 * STEP), 0.125, d=0.05, s=0.4, r=0.02),
                        (bar0 + b) * BAR + s * STEP, 0.055, 0.6)

        # melody
        pos = bar0 * BAR
        flute_pos = pos
        if kind in ('A', 'B'):
            duty = 0.25
            for note, steps in melody:
                n = steps * STEP
                if note != 'R':
                    m = midi(note)
                    gate = n - 300 if steps >= 4 else int(n * 0.82)
                    lead.add(pulse_note(m, gate, duty, vib=0.28 if steps >= 4 else 0.0), pos, 0.24)
                    if name == 'A2':
                        mix(koto_note(m - 12, min(n + int(0.6 * SR), 2 * SR)), pos, 0.22, -0.35)
                    if name == 'B2':
                        mix(pulse_note(third_below(m), gate, 0.5, vib=0.2 if steps >= 4 else 0.0),
                            pos, 0.10, 0.45)
                pos += n
        else:   # C: koto melody + flute
            for note, steps in melody:
                n = steps * STEP
                if note != 'R':
                    mix(koto_note(midi(note), min(n + int(0.7 * SR), 2 * SR)), pos, 0.22, -0.1)
                pos += n
            for note, steps in C_FLUTE:
                n = steps * STEP
                mix(flute_note(midi(note), n - 2000), flute_pos, 0.095, 0.25)
                flute_pos += n

        # drums
        for b in range(8):
            drums_for_bar(kind, bar0 + b, b, fill=(b == 7))
        if name in ('A', 'B', 'A2', 'B2'):
            hit('crash', bar0, 0, 0.7 if name == 'A' else 1.0)
        else:
            hit('taiko', bar0, 0, 1.0)

    # lead bus -> stereo with dotted-8th echo
    print('  mixing lead + echo', flush=True)
    mix(lead.buf, 0, 1.0, -0.15)
    mix(lead.buf, 3 * STEP, 0.30, 0.65)
    mix(lead.buf, 6 * STEP, 0.13, -0.65)
    print(f'  rendered in {time.time() - t0:.0f}s', flush=True)

def finish(path):
    # wrap the tail (echoes, ringing notes) onto the start so the loop is seamless
    for i in range(TAIL):
        L[i] += L[LOOP_LEN + i]
        R[i] += R[LOOP_LEN + i]
    peak = max(max(abs(L[i]), abs(R[i])) for i in range(LOOP_LEN))
    drive = 1.15 / peak
    th = math.tanh
    outL, outR = [0.0] * LOOP_LEN, [0.0] * LOOP_LEN
    for i in range(LOOP_LEN):
        outL[i] = th(L[i] * drive)
        outR[i] = th(R[i] * drive)
    pk2 = max(max(abs(a), abs(b)) for a, b in zip(outL, outR))
    scale = 0.89 / pk2                     # -1 dBFS
    pcm = array('h', bytes(4 * LOOP_LEN))
    for i in range(LOOP_LEN):
        pcm[2 * i] = int(outL[i] * scale * 32767)
        pcm[2 * i + 1] = int(outR[i] * scale * 32767)
    with wave.open(path, 'wb') as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())

    # report
    def rms(a, b):
        return math.sqrt(sum(outL[i] ** 2 + outR[i] ** 2 for i in range(a, b, 7)) / (2 * len(range(a, b, 7)))) * scale
    print(f'  pre-limit peak {peak:.2f} -> drive {drive:.2f}; final peak -1.0 dBFS')
    for si, (name, _, _) in enumerate(SECTIONS):
        r = rms(si * 8 * BAR, (si + 1) * 8 * BAR)
        print(f'  {name:3s} RMS {20 * math.log10(r):6.1f} dBFS')
    seam = max(abs(outL[-1] - outL[0]), abs(outR[-1] - outR[0])) * scale
    typical = sum(abs(outL[i] - outL[i - 1]) for i in range(1, 200001)) / 200000 * scale
    print(f'  loop seam jump {seam:.4f} (typical sample-to-sample change {typical:.4f})')
    print(f'  length {LOOP_LEN / SR:.2f}s, {TOTAL_BARS} bars @ {BPM} BPM')

if __name__ == '__main__':
    out = sys.argv[1]
    render()
    finish(out)
    print('wrote', out)
