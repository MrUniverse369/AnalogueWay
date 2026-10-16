"""
"Snow Temple (Modern)" - loopable BGM for AnalogueWay's Japan levels.
Modern pixel-platformer style (FM electric piano / slap bass / brass, swung shaker, reverb)
with Japanese colour (shakuhachi, koto, taiko, hyoshigi). Pure Python, stdlib only.

Usage:  python3 snow_temple_modern.py <output.wav>

Tempo 125 BPM (matched to the reference track), 4/4, light 16th swing, tresillo (3-3-2) kick in the chorus.
Form (48 bars, ~92 s, loops seamlessly back to bar 0):
  A  (8)  verse      - shakuhachi melody, FM e-piano comping, slap bass, koto offbeats
  B  (8)  pre-chorus - FM lead, strings swell, snare build + reverse cymbal
  C  (8)  chorus     - "royal road" progression (Bbmaj7 C7 Am7 Dm7), brass lead + koto, bells
  A2 (8)  verse 2    - shakuhachi + koto unison, bells
  D  (8)  breakdown  - In scale (D Eb G A Bb), koto melody, shakuhachi, half-time taiko
  C2 (8)  chorus 2   - with harmony line; ends on A7 -> loops to Dm9
Everything you might want to tweak (score, chords, tempo, volumes) is in the SCORE / MIX sections.
"""
import math, random, sys, time, wave
from array import array

SR = 44100
BPM = 125
STEP = int(round(SR * 60 / BPM / 4))     # 16th note
BAR = 16 * STEP
SWING = int(0.13 * STEP)                 # delay applied to off-16ths (0 = straight)
TAU = 2 * math.pi
random.seed(125)

# =============================================================== SCORE
NOTE = {'C': 0, 'C#': 1, 'Db': 1, 'D': 2, 'Eb': 3, 'E': 4, 'F': 5, 'F#': 6, 'G': 7,
        'Ab': 8, 'A': 9, 'Bb': 10, 'B': 11}

def midi(n):
    return 12 * (int(n[-1]) + 1) + NOTE[n[:-1]]

# Chords: e-piano voicing (MIDI notes) and bass root.
CHORDS = {
    'Dm9':    ([65, 69, 72, 76], 38), 'Dm7':  ([60, 65, 69], 38), 'Dm':    ([62, 65, 69, 74], 38),
    'Bbmaj7': ([62, 65, 69], 34),     'Gm9':  ([58, 62, 65, 69], 43), 'Gm7': ([65, 70, 74], 43),
    'A7sus':  ([62, 64, 67], 45),     'A7':   ([61, 64, 67], 45), 'C7':     ([64, 67, 70], 36),
    'Am7':    ([57, 60, 64, 67], 45), 'Ebmaj7': ([62, 67, 70], 39),
}

VERSE = [('A4',6),('G4',2),('A4',4),('C5',4),
         ('D5',8),('C5',2),('A4',2),('F4',4),
         ('G4',6),('A4',2),('Bb4',4),('D5',4),
         ('E5',6),('D5',2),('E5',8),
         ('A4',6),('G4',2),('A4',4),('C5',2),('D5',2),
         ('F5',6),('E5',2),('D5',4),('A4',4),
         ('Bb4',4),('A4',2),('G4',2),('F4',4),('G4',4),
         ('A4',8),('G4',2),('E4',2),('A4',4)]
VERSE_CH = ['Dm9','Bbmaj7','Gm9','A7sus','Dm9','Bbmaj7','Gm9','A7']

PRE = [('F4',3),('G4',3),('A4',2),('D5',4),('C5',4),
       ('Bb4',3),('A4',3),('G4',2),('E4',4),('G4',4),
       ('A4',3),('G4',3),('A4',2),('C5',4),('E5',4),
       ('D5',8),('C5',4),('A4',4),
       ('F4',3),('G4',3),('A4',2),('D5',4),('F5',4),
       ('E5',3),('D5',3),('C5',2),('Bb4',4),('G4',4),
       ('G4',3),('Bb4',3),('D5',2),('Eb5',4),('D5',4),
       ('E5',6),('D5',2),('C#5',4),('E5',4)]
PRE_CH = ['Bbmaj7','C7','Am7','Dm7','Bbmaj7','C7','Ebmaj7','A7']

CHORUS = [('D5',2),('F5',2),('A5',4),('G5',2),('F5',2),('D5',4),
          ('E5',2),('G5',2),('C6',4),('Bb5',2),('G5',2),('E5',4),
          ('A5',4),('G5',2),('E5',2),('G5',4),('A5',4),
          ('F5',6),('E5',2),('D5',8),
          ('D5',2),('F5',2),('A5',4),('G5',2),('F5',2),('D6',4),
          ('C6',4),('Bb5',2),('G5',2),('E5',4),('G5',4),
          ('F5',4),('G5',2),('Bb5',2),('A5',4),('G5',4),
          ('A5',6),('G5',2),('E5',4),('C#5',4)]
CHORUS_CH = ['Bbmaj7','C7','Am7','Dm7','Bbmaj7','C7','Gm7','A7']

BREAK_KOTO = [('A4',4),('D5',4),('Eb5',2),('D5',2),('A4',4),
              ('Bb4',4),('G4',4),('Bb4',2),('D5',2),('Eb5',4),
              ('D5',6),('Eb5',2),('D5',2),('Bb4',2),('A4',4),
              ('G4',4),('Bb4',4),('Eb5',4),('D5',4),
              ('G5',4),('D5',4),('Bb4',4),('D5',4),
              ('Eb5',6),('D5',2),('Bb4',4),('G4',4),
              ('A4',4),('Bb4',2),('A4',2),('D5',8),
              ('A4',4),('E5',4),('R',8)]
BREAK_FLUTE = [('D5',16),('Bb4',16),('A4',16),('G4',16),('D5',16),('Eb5',16),('D5',16),('E5',16)]
BREAK_CH = ['Dm','Ebmaj7','Dm','Ebmaj7','Gm7','Ebmaj7','Dm','A7sus']

SECTIONS = [('A', 'verse', VERSE, VERSE_CH), ('B', 'pre', PRE, PRE_CH), ('C', 'chorus', CHORUS, CHORUS_CH),
            ('A2', 'verse', VERSE, VERSE_CH), ('D', 'break', BREAK_KOTO, BREAK_CH), ('C2', 'chorus', CHORUS, CHORUS_CH)]

D_MINOR = [2, 4, 5, 7, 9, 10, 0]

def third_below(m):
    pc = m % 12
    if pc not in D_MINOR:
        return m - 4
    i = D_MINOR.index(pc)
    return m - ((pc - D_MINOR[(i - 2) % 7]) % 12)

# =============================================================== ENGINE
TOTAL_BARS = 8 * len(SECTIONS)
LOOP = TOTAL_BARS * BAR
TAIL = 5 * SR
N = LOOP + TAIL
L = array('d', bytes(8 * N)); R = array('d', bytes(8 * N))
VL = array('d', bytes(8 * N)); VR = array('d', bytes(8 * N))   # reverb send

def pos(bar, step):
    p = bar * BAR + step * STEP
    return p + SWING if step % 2 == 1 else p

def mix(src, start, gain, pan=0.0, verb=0.0):
    a = (pan + 1) * math.pi / 4
    gl, gr = math.cos(a) * gain, math.sin(a) * gain
    vl, vr = gl * verb, gr * verb
    end = min(N, start + len(src))
    for i in range(max(0, start), end):
        v = src[i - start]
        L[i] += v * gl; R[i] += v * gr
        if verb:
            VL[i] += v * vl; VR[i] += v * vr

def hz(m):
    return 440.0 * 2 ** ((m - 69) / 12)

_cache = {}
def cached(fn):
    def wrap(*args):
        k = (fn.__name__,) + args
        if k not in _cache:
            _cache[k] = fn(*args)
        return _cache[k]
    return wrap

def adsr(i, gate, a, d, s, r):
    if i < a: return i / a
    if i < a + d: return 1 - (1 - s) * (i - a) / d
    if i < gate: return s
    k = (i - gate) / r
    return s * (1 - k) if k < 1 else 0.0

@cached
def epiano(m, gate):
    """FM electric piano: body (ratio 1) + tine (ratio 14), piano-like decay."""
    f = hz(m); rel = int(0.18 * SR); n = gate + rel
    out = array('d', bytes(8 * n)); pc = pm = pt = 0.0
    for i in range(n):
        t = i / SR
        body = (1.8 * math.exp(-t / 0.4) + 0.35) * math.sin(pm)
        tine = 1.3 * math.exp(-t / 0.03) * math.sin(pt)
        amp = math.exp(-t / 1.3) * (1.0 if i < gate else max(0.0, 1 - (i - gate) / rel))
        out[i] = math.sin(pc + body + tine) * amp
        pc += TAU * f / SR; pm += TAU * f / SR; pt += TAU * 14 * f / SR
    return out

@cached
def slap_bass(m, gate):
    """FM bass with a percussive 'slap' index envelope plus a sine sub."""
    f = hz(m); rel = int(0.03 * SR); n = gate + rel
    out = array('d', bytes(8 * n)); pc = pm = 0.0; y = 0.0
    for i in range(n):
        t = i / SR
        idx = 3.2 * math.exp(-t / 0.07) + 0.9
        v = math.sin(pc + idx * math.sin(pm)) * 0.75 + 0.5 * math.sin(pc)
        y += 0.35 * (v - y)
        out[i] = y * adsr(i, gate, int(0.002 * SR), int(0.2 * SR), 0.65, rel)
        pc += TAU * f / SR; pm += TAU * f / SR
    return out

@cached
def brass(m, gate, detune, bright):
    """FM brass lead (ratio 1:1, index swells in on the attack) with delayed vibrato."""
    f = hz(m + detune); rel = int(0.09 * SR); n = gate + rel
    out = array('d', bytes(8 * n)); pc = pm = 0.0
    vk = 0.25 * math.log(2) / 12
    for i in range(n):
        t = i / SR
        idx = bright * (1 - math.exp(-t / 0.045)) * (0.75 + 0.25 * math.exp(-t / 0.3))
        vib = 0.0 if t < 0.2 else min(1.0, (t - 0.2) / 0.3) * vk * math.sin(TAU * 5.2 * t)
        ff = f * (1 + vib)
        out[i] = math.sin(pc + idx * math.sin(pm)) * adsr(i, gate, int(0.015 * SR), int(0.12 * SR), 0.85, rel)
        pc += TAU * ff / SR; pm += TAU * ff / SR
    return out

@cached
def bell(m):
    f = hz(m); n = int(1.4 * SR)
    out = array('d', bytes(8 * n)); pc = pm = 0.0
    for i in range(n):
        t = i / SR
        out[i] = math.sin(pc + 2.6 * math.exp(-t / 0.25) * math.sin(pm)) * math.exp(-t / 0.5)
        pc += TAU * f / SR; pm += TAU * 3.5 * f / SR
    return out

@cached
def koto(m, n):
    """Karplus-Strong pluck."""
    f = hz(m); P = max(2, int(round(SR / f)))
    exc = [0.6 * random.uniform(-1, 1) + 0.4 * (2 * j / P - 1) for j in range(P)]
    buf = [0.5 * (exc[j] + exc[j - 1]) for j in range(P)]
    rho = 10 ** (-3 / (1.8 * SR)); fade = int(0.03 * SR)
    out = array('d', bytes(8 * n)); j = 0
    for i in range(n):
        cur = buf[j]; nxt = buf[j + 1 if j + 1 < P else 0]
        buf[j] = 0.5 * (cur + nxt) * rho
        out[i] = cur * (1.0 if i < n - fade else (n - i) / fade)
        j = j + 1 if j + 1 < P else 0
    return out

def shakuhachi(m, gate):
    """Breathy flute with a 'meri' pitch scoop into the note and late vibrato."""
    f = hz(m); rel = int(0.35 * SR); n = gate + rel
    out = array('d', bytes(8 * n)); ph = 0.0; y = 0.0
    vk = 0.22 * math.log(2) / 12
    for i in range(n):
        t = i / SR
        scoop = -1.0 * math.exp(-t / 0.05)                        # starts a semitone flat, slides up
        vib = 0.0 if t < 0.35 else min(1.0, (t - 0.35) / 0.5) * vk * math.sin(TAU * 5.0 * t)
        ff = f * 2 ** (scoop / 12) * (1 + vib)
        ph += TAU * ff / SR
        tone = math.sin(ph) + 0.18 * math.sin(2 * ph) + 0.06 * math.sin(3 * ph)
        y += 0.06 * (random.uniform(-1, 1) - y)
        breath = 1.4 + 2.5 * math.exp(-t / 0.08)                  # chiff on the attack
        out[i] = (tone + breath * y) * adsr(i, gate, int(0.09 * SR), 1, 1.0, rel)
    return out

@cached
def strings(notes, n, attack):
    rel = int(0.4 * SR); tot = n + rel
    out = array('d', bytes(8 * tot)); A_ = int(attack * SR)
    for m in notes:
        for det in (-0.08, 0.0, 0.08):
            f = hz(m + det); dt = f / SR; t = random.random(); y = 0.0
            for i in range(tot):
                v = 2 * t - 1
                y += 0.05 * (v - y)
                out[i] += y * adsr(i, n, A_, 1, 1.0, rel)
                t += dt
                if t >= 1: t -= 1
    return out

# ---------------------------------------------------------------- drums
def drum(sec, fn):
    n = int(sec * SR); out = array('d', bytes(8 * n)); s = {}
    for i in range(n):
        out[i] = fn(i / SR, s)
    return out

def noise_hp(s, key='p'):
    x = random.uniform(-1, 1); h = x - s.get(key, 0.0); s[key] = x
    return h

def k_kick(t, s):
    s['ph'] = s.get('ph', 0.0) + TAU * (48 + 130 * math.exp(-t / 0.03)) / SR
    return math.sin(s['ph']) * math.exp(-t / 0.18) + 0.4 * random.uniform(-1, 1) * math.exp(-t / 0.003)

def k_snare(t, s):
    return 0.5 * math.sin(TAU * 190 * t) * math.exp(-t / 0.06) + 0.75 * noise_hp(s) * math.exp(-t / 0.13)

def k_clap(t, s):
    burst = sum(math.exp(-(t - o) / 0.006) for o in (0.0, 0.011, 0.022) if t >= o)
    return 0.6 * noise_hp(s) * (0.6 * burst + math.exp(-max(0.0, t - 0.022) / 0.1))

def k_rim(t, s):
    return (math.sin(TAU * 1700 * t) * 0.7 + 0.5 * noise_hp(s)) * math.exp(-t / 0.012)

def k_hat(decay):
    def fn(t, s):
        h = noise_hp(s); h2 = h - s.get('q', 0.0); s['q'] = h
        return 0.5 * h2 * math.exp(-t / decay)
    return fn

def k_shaker(t, s):
    h = noise_hp(s); h2 = h - s.get('q', 0.0); s['q'] = h
    return 0.4 * h2 * min(1.0, t / 0.006) * math.exp(-t / 0.035)

def k_taiko(t, s):
    s['ph'] = s.get('ph', 0.0) + TAU * (60 + 55 * math.exp(-t / 0.05)) / SR
    s['n'] = s.get('n', 0.0) + 0.08 * (random.uniform(-1, 1) - s.get('n', 0.0))
    return math.sin(s['ph']) * math.exp(-t / 0.35) + 2.5 * s['n'] * math.exp(-t / 0.035)

def k_hyo(t, s):
    return (math.sin(TAU * 1450 * t) + 0.6 * math.sin(TAU * 2380 * t)) * math.exp(-t / 0.018)

def k_tom(f0):
    def fn(t, s):
        s['ph'] = s.get('ph', 0.0) + TAU * f0 * (0.65 + 0.35 * math.exp(-t / 0.08)) / SR
        return math.sin(s['ph']) * math.exp(-t / 0.22)
    return fn

def k_crash(t, s):
    return 0.5 * noise_hp(s) * math.exp(-t / 0.6)

DR = {'kick': drum(0.4, k_kick), 'snare': drum(0.35, k_snare), 'clap': drum(0.3, k_clap), 'rim': drum(0.06, k_rim),
      'hat': drum(0.06, k_hat(0.012)), 'ohat': drum(0.3, k_hat(0.09)), 'shaker': drum(0.12, k_shaker),
      'taiko': drum(1.0, k_taiko), 'hyo': drum(0.12, k_hyo), 'crash': drum(2.0, k_crash),
      'tomH': drum(0.5, k_tom(190)), 'tomM': drum(0.5, k_tom(145)), 'tomL': drum(0.5, k_tom(105))}
DR['rev'] = array('d', reversed(drum(2 * 4 * STEP / SR, k_crash)))      # 2-beat reverse cymbal

# =============================================================== MIX (gain, pan, reverb send)
DMIX = {'kick': (0.70, 0.0, 0.0), 'snare': (0.34, 0.05, 0.25), 'clap': (0.26, -0.05, 0.3), 'rim': (0.18, 0.2, 0.15),
        'hat': (0.10, 0.35, 0.05), 'ohat': (0.08, 0.35, 0.1), 'shaker': (0.13, -0.4, 0.05),
        'taiko': (0.50, -0.15, 0.2), 'hyo': (0.16, 0.45, 0.25), 'crash': (0.14, -0.3, 0.2),
        'tomH': (0.32, 0.4, 0.15), 'tomM': (0.32, 0.0, 0.15), 'tomL': (0.34, -0.4, 0.15), 'rev': (0.20, 0.0, 0.2)}

def hit(name, bar, step, vel=1.0):
    g, p, v = DMIX[name]
    mix(DR[name], pos(bar, step), g * vel, p, v)

def drum_bar(style, bar, local, last):
    H = []
    if style == 'verse':
        H += [('kick', s, 1.0) for s in (0, 6, 10)] + [('rim', s, 1.0) for s in (4, 12)]
        H += [('snare', 12, 0.45)]
        H += [('shaker', s, (1.0, 0.5, 0.75, 0.5)[s % 4]) for s in range(16)]
    elif style == 'pre':
        H += [('kick', s, 1.0) for s in (0, 6, 10)] + [('snare', s, 0.9) for s in (4, 12)]
        H += [('hat', s, 0.8 if s % 4 else 1.0) for s in range(0, 16, 2)]
        H += [('shaker', s, 0.6) for s in range(1, 16, 2)]
    elif style == 'chorus':
        H += [('kick', s, 1.0) for s in (0, 3, 6, 8, 11, 14)]
        H += [('snare', s, 1.0) for s in (4, 12)] + [('clap', s, 1.0) for s in (4, 12)]
        H += [('ohat' if s % 4 == 2 else 'hat', s, 0.9) for s in range(0, 16, 2)]
        H += [('shaker', s, 0.7) for s in range(1, 16, 2)]
        if local % 2 == 0:
            H += [('taiko', 0, 0.6)]
    elif style == 'break':
        H += [('kick', 0, 0.8), ('snare', 8, 0.7), ('taiko', 0, 0.6), ('taiko', 10, 0.4)]
        H += [('hyo', s, v) for s, v in ((4, 0.8), (12, 0.8), (14, 0.45))]
        H += [('shaker', s, 0.45) for s in range(0, 16, 2)]
    if last:
        H = [h for h in H if h[1] < 8 or h[0] in ('shaker', 'hat')]
        if style == 'pre':
            H += [('snare', s, 0.35 + 0.65 * (s - 8) / 7) for s in range(8, 16)] + [('kick', 8, 1.0)]
            mix(DR['rev'], pos(bar + 1, 0) - len(DR['rev']), DMIX['rev'][0], 0.0, DMIX['rev'][2])
        elif style == 'break':
            H = [('taiko', s, 0.4 + 0.6 * s / 14) for s in range(0, 16, 2)]
            H += [('tomH', 12, 0.9), ('tomM', 13, 0.9), ('tomL', 14, 1.0), ('tomL', 15, 0.8), ('kick', 0, 0.8)]
            mix(DR['rev'], pos(bar + 1, 0) - len(DR['rev']), DMIX['rev'][0], 0.0, DMIX['rev'][2])
        else:
            H += [('kick', 8, 1.0), ('tomH', 10, 0.8), ('tomH', 11, 0.7), ('tomM', 12, 0.9),
                  ('tomM', 13, 0.8), ('tomL', 14, 1.0), ('snare', 15, 0.8)]
    for name, s, v in H:
        hit(name, bar, s, v)

def notes_with_positions(melody, bar0):
    """(midi or None, start_sample, length_samples, abs_step) with swing applied to off-16th starts."""
    out, step = [], 0
    for name, steps in melody:
        start = pos(bar0 + step // 16, step % 16)
        end = pos(bar0 + (step + steps) // 16, (step + steps) % 16)
        out.append((None if name == 'R' else midi(name), start, end - start, step))
        step += steps
    return out

COMP = {'verse': [(0, 4), (6, 2), (10, 4)], 'pre': [(0, 6), (6, 2), (10, 6)],
        'chorus': [(0, 2), (3, 2), (6, 2), (8, 2), (11, 2), (14, 2)], 'break': [(0, 16)]}

def bass_line(style, ch, nxt):
    root = CHORDS[ch][1]; nroot = CHORDS[nxt][1]
    fifth = root + 7
    if style == 'break':
        return [(0, 8, root), (8, 6, root), (14, 2, root + 12)]
    if style == 'verse':
        return [(0, 3, root), (3, 3, root), (6, 2, root + 12), (8, 2, root), (10, 4, fifth), (14, 2, nroot - 2)]
    return [(0, 3, root), (3, 3, root), (6, 2, root + 12), (8, 2, root), (10, 2, fifth),
            (12, 2, root + 12), (14, 2, nroot - 2)]

def render():
    t0 = time.time()
    chords_flat = [c for _, _, _, chs in SECTIONS for c in chs]
    for si, (name, style, melody, chords) in enumerate(SECTIONS):
        b0 = si * 8
        print(f'  {name:3s} ({style})', flush=True)
        for b, ch in enumerate(chords):
            bar = b0 + b
            voic, _ = CHORDS[ch]
            nxt = chords_flat[(bar + 1) % TOTAL_BARS]
            # e-piano comping
            for s, ln in COMP[style]:
                gate = ln * STEP - 400 if style != 'chorus' else int(ln * STEP * 0.6)
                for k, m in enumerate(voic):
                    mix(epiano(m, gate), pos(bar, s) + k * 60, 0.075 if style != 'break' else 0.06, -0.25 + 0.17 * k, 0.25)
            # bass
            for s, ln, m in bass_line(style, ch, nxt):
                gate = int(ln * STEP * (0.85 if style != 'break' else 0.95))
                mix(slap_bass(m, gate), pos(bar, s), 0.30, 0.0)
            # strings
            if style in ('pre', 'chorus', 'break'):
                att = 0.9 if style == 'break' else (1.2 if style == 'pre' else 0.15)
                mix(strings(tuple(m - 12 for m in voic[:3]), BAR, att), bar * BAR, 0.05 if style != 'chorus' else 0.04, 0.1, 0.35)
            # koto offbeats (verse) / bell sparkle (chorus, verse 2)
            if style == 'verse':
                for k, s in enumerate((2, 6, 10, 14)):
                    m = voic[-1 - (k % len(voic))] + 12
                    mix(koto(m, int(0.6 * SR)), pos(bar, s), 0.10 if name == 'A' else 0.08, 0.45, 0.25)
            if style == 'chorus' or name == 'A2':
                tones = sorted(voic)
                for k, s in enumerate(range(0, 16, 2)):
                    m = tones[k % len(tones)] + 24
                    mix(bell(m), pos(bar, s), 0.045 if style == 'chorus' else 0.03, 0.55 if k % 2 else -0.55, 0.35)
            drum_bar(style, bar, b, b == 7)
        if style == 'chorus' or name == 'A':
            hit('crash', b0, 0, 1.0 if style == 'chorus' else 0.6)
        if style == 'break':
            hit('taiko', b0, 0, 1.0)

        # melody
        for m, start, ln, step in notes_with_positions(melody, b0):
            if m is None:
                continue
            if style == 'verse':
                mix(shakuhachi(m, ln - 1500), start, 0.20, -0.1, 0.4)
                if name == 'A2':
                    mix(koto(m, min(ln + int(0.5 * SR), 2 * SR)), start, 0.16, 0.3, 0.3)
            elif style in ('pre', 'chorus'):
                gate = ln - 600 if ln >= 4 * STEP else int(ln * 0.8)
                bright = 1.6 if style == 'pre' else 2.4
                g = 0.13 if style == 'pre' else 0.17
                mix(brass(m, gate, -0.07, bright), start, g, -0.3, 0.3)
                mix(brass(m, gate, 0.07, bright), start + 200, g, 0.3, 0.3)
                if style == 'chorus':
                    mix(koto(m - 12, min(ln + int(0.4 * SR), 2 * SR)), start, 0.12, -0.1, 0.25)
                if name == 'C2':
                    mix(brass(third_below(m), gate, 0.0, 1.8), start, 0.09, 0.5, 0.3)
            elif style == 'break':
                mix(koto(m, min(ln + int(0.8 * SR), 2 * SR)), start, 0.26, -0.15, 0.35)
        if style == 'break':
            for m, start, ln, step in notes_with_positions(BREAK_FLUTE, b0):
                mix(shakuhachi(m, ln - 2500), start, 0.12, 0.25, 0.45)
    print(f'  notes rendered in {time.time() - t0:.0f}s', flush=True)

def reverb():
    """Schroeder/Freeverb-style: 4 damped combs + 2 allpasses per channel, mixed back in."""
    t0 = time.time()
    combs = [1116, 1188, 1277, 1356]; aps = [556, 441]; spread = 23
    for src, dst, off in ((VL, L, 0), (VR, R, spread)):
        out = array('d', bytes(8 * N))
        for c in combs:
            d = c + off; buf = [0.0] * d; j = 0; lp = 0.0
            for i in range(N):
                y = buf[j]
                lp = y * 0.72 + lp * 0.28
                buf[j] = src[i] + lp * 0.83
                out[i] += y
                j = j + 1 if j + 1 < d else 0
        for a in aps:
            d = a + off; buf = [0.0] * d; j = 0
            for i in range(N):
                b = buf[j]; x = out[i]
                out[i] = -x + b
                buf[j] = x + b * 0.5
                j = j + 1 if j + 1 < d else 0
        for i in range(N):
            dst[i] += out[i] * 0.07
    print(f'  reverb in {time.time() - t0:.0f}s', flush=True)

def master(path):
    for i in range(TAIL):                      # wrap tails so the loop point is seamless
        L[i] += L[LOOP + i]; R[i] += R[LOOP + i]
    sq = sum(L[i] * L[i] + R[i] * R[i] for i in range(0, LOOP, 5)) / (2 * len(range(0, LOOP, 5)))
    target_rms = 10 ** (-13.5 / 20)
    drive = target_rms / math.sqrt(sq)
    th = math.tanh
    ol = [th(L[i] * drive) for i in range(LOOP)]
    orr = [th(R[i] * drive) for i in range(LOOP)]
    pk = max(max(map(abs, ol)), max(map(abs, orr)))
    sc = min(1.0, 0.89 / pk)
    pcm = array('h', bytes(4 * LOOP))
    for i in range(LOOP):
        pcm[2 * i] = int(ol[i] * sc * 32767); pcm[2 * i + 1] = int(orr[i] * sc * 32767)
    with wave.open(path, 'wb') as w:
        w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR); w.writeframes(pcm.tobytes())
    print(f'  {LOOP / SR:.2f}s, {TOTAL_BARS} bars @ {BPM} BPM, peak {20 * math.log10(pk * sc):.1f} dBFS')
    for si, (name, style, _, _) in enumerate(SECTIONS):
        a, b = si * 8 * BAR, (si + 1) * 8 * BAR
        r = math.sqrt(sum(ol[i] ** 2 + orr[i] ** 2 for i in range(a, b, 7)) / (2 * len(range(a, b, 7)))) * sc
        print(f'  {name:3s} {style:7s} RMS {20 * math.log10(r):6.1f} dBFS')

if __name__ == '__main__':
    out = sys.argv[1]
    render(); reverb(); master(out)
    print('wrote', out)
