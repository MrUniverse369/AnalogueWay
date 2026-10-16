"""
Shared synth engine for AnalogueWay level music (pure Python, stdlib only).
A song script creates Song(bpm, swing, bars, seed), places notes/drums with the instrument
methods + mix()/hit(), then calls finish(path). See sandy_dunes.py / grassy_meadow.py.
(snow_temple_modern.py predates this module and is kept as-is so the approved Japan track never changes.)
"""
import math, random, time, wave
from array import array

SR = 44100
TAU = 2 * math.pi
NOTE = {'C': 0, 'C#': 1, 'Db': 1, 'D': 2, 'D#': 3, 'Eb': 3, 'E': 4, 'F': 5, 'F#': 6, 'Gb': 6, 'G': 7,
        'G#': 8, 'Ab': 8, 'A': 9, 'A#': 10, 'Bb': 10, 'B': 11}

def midi(n):
    return 12 * (int(n[-1]) + 1) + NOTE[n[:-1]]

def hz(m):
    return 440.0 * 2 ** ((m - 69) / 12)

def adsr(i, gate, a, d, s, r):
    if i < a: return i / a
    if i < a + d: return 1 - (1 - s) * (i - a) / d
    if i < gate: return s
    k = (i - gate) / r
    return s * (1 - k) if k < 1 else 0.0

def check_score(name, melody, bars=8, allow_ties=False):
    pos, bad = 0, []
    for note, steps in melody:
        if not allow_ties and pos // 16 != (pos + steps - 1) // 16:
            bad.append((note, pos))
        pos += steps
    assert pos == bars * 16, f'{name}: {pos} steps, expected {bars * 16}'
    assert not bad, f'{name}: notes cross barlines at {bad}'

class Song:
    def __init__(self, bpm, swing, bars, seed):
        self.bpm, self.bars = bpm, bars
        self.STEP = int(round(SR * 60 / bpm / 4))
        self.BAR = 16 * self.STEP
        self.SWING = int(swing * self.STEP)
        self.LOOP = bars * self.BAR
        self.TAIL = 5 * SR
        self.N = self.LOOP + self.TAIL
        z = bytes(8 * self.N)
        self.L, self.R, self.VL, self.VR = array('d', z), array('d', z), array('d', z), array('d', z)
        self.rng = random.Random(seed)
        self._cache = {}
        self.t0 = time.time()
        self._build_kit()

    # ------------------------------------------------------------ placement
    def pos(self, bar, step):
        p = bar * self.BAR + step * self.STEP
        return p + self.SWING if step % 2 == 1 else p

    def mix(self, src, start, gain, pan=0.0, verb=0.0):
        a = (pan + 1) * math.pi / 4
        gl, gr = math.cos(a) * gain, math.sin(a) * gain
        vl, vr = gl * verb, gr * verb
        L, R, VL, VR = self.L, self.R, self.VL, self.VR
        for i in range(max(0, start), min(self.N, start + len(src))):
            v = src[i - start]
            L[i] += v * gl; R[i] += v * gr
            if verb:
                VL[i] += v * vl; VR[i] += v * vr

    def notes(self, melody, bar0):
        """[(midi or None, start, length)] with swing applied to off-16th starts."""
        out, step = [], 0
        for name, steps in melody:
            start = self.pos(bar0 + step // 16, step % 16)
            end = self.pos(bar0 + (step + steps) // 16, (step + steps) % 16)
            out.append((None if name == 'R' else midi(name), start, end - start))
            step += steps
        return out

    def _cached(self, key, fn):
        if key not in self._cache:
            self._cache[key] = fn()
        return self._cache[key]

    # ------------------------------------------------------------ instruments
    def epiano(self, m, gate):
        def make():
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
        return self._cached(('ep', m, gate), make)

    def slap_bass(self, m, gate):
        def make():
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
        return self._cached(('bass', m, gate), make)

    def brass(self, m, gate, detune=0.0, bright=2.4, vib=0.25):
        def make():
            f = hz(m + detune); rel = int(0.09 * SR); n = gate + rel
            out = array('d', bytes(8 * n)); pc = pm = 0.0
            vk = vib * math.log(2) / 12
            for i in range(n):
                t = i / SR
                idx = bright * (1 - math.exp(-t / 0.045)) * (0.75 + 0.25 * math.exp(-t / 0.3))
                v = 0.0 if t < 0.2 else min(1.0, (t - 0.2) / 0.3) * vk * math.sin(TAU * 5.2 * t)
                ff = f * (1 + v)
                out[i] = math.sin(pc + idx * math.sin(pm)) * adsr(i, gate, int(0.015 * SR), int(0.12 * SR), 0.85, rel)
                pc += TAU * ff / SR; pm += TAU * ff / SR
            return out
        return self._cached(('brass', m, gate, detune, bright, vib), make)

    def pulse_lead(self, m, gate, duty=0.5, vib=0.3):
        """Soft band-limited pulse 'whistle' lead with delayed vibrato."""
        def make():
            f = hz(m); rel = int(0.07 * SR); n = gate + rel
            out = array('d', bytes(8 * n)); t = 0.0; y = 0.0
            vk = vib * math.log(2) / 12
            for i in range(n):
                s = i / SR
                v = 0.0 if s < 0.15 else min(1.0, (s - 0.15) / 0.3) * vk * math.sin(TAU * 5.6 * s)
                dt = f * (1 + v) / SR
                x = 1.0 if t < duty else -1.0
                x += _blep(t, dt); t2 = t - duty + (1.0 if t < duty else 0.0); x -= _blep(t2, dt)
                y += 0.25 * (x - y)                                  # soften
                out[i] = y * adsr(i, gate, int(0.008 * SR), int(0.1 * SR), 0.75, rel)
                t += dt
                if t >= 1.0: t -= 1.0
            return out
        return self._cached(('pulse', m, gate, duty, vib), make)

    def bell(self, m, ratio=3.5, index=2.6, decay=0.5, sec=1.4):
        def make():
            f = hz(m); n = int(sec * SR); out = array('d', bytes(8 * n)); pc = pm = 0.0
            for i in range(n):
                t = i / SR
                out[i] = math.sin(pc + index * math.exp(-t / 0.25) * math.sin(pm)) * math.exp(-t / decay)
                pc += TAU * f / SR; pm += TAU * ratio * f / SR
            return out
        return self._cached(('bell', m, ratio, index, decay, sec), make)

    def pluck(self, m, n, decay_sec=1.8, bright=0.4):
        """Karplus-Strong string. bright = how much sawtooth is mixed into the excitation."""
        def make():
            f = hz(m); P = max(2, int(round(SR / f))); rng = self.rng
            exc = [(1 - bright) * rng.uniform(-1, 1) + bright * (2 * j / P - 1) for j in range(P)]
            b = [0.5 * (exc[j] + exc[j - 1]) for j in range(P)]
            rho = 10 ** (-3 / (decay_sec * SR)); fade = int(0.03 * SR)
            out = array('d', bytes(8 * n)); j = 0
            for i in range(n):
                cur = b[j]; nxt = b[j + 1 if j + 1 < P else 0]
                b[j] = 0.5 * (cur + nxt) * rho
                out[i] = cur * (1.0 if i < n - fade else (n - i) / fade)
                j = j + 1 if j + 1 < P else 0
            return out
        return self._cached(('pluck', m, n, decay_sec, bright), make)

    def flute(self, m, gate, scoop=-1.0, breath=1.4, vib=0.22):
        """Breathy flute (shakuhachi / ney style). scoop = semitones it slides up from."""
        f = hz(m); rel = int(0.3 * SR); n = gate + rel
        out = array('d', bytes(8 * n)); ph = 0.0; y = 0.0; rng = self.rng
        vk = vib * math.log(2) / 12
        for i in range(n):
            t = i / SR
            sc = scoop * math.exp(-t / 0.05)
            v = 0.0 if t < 0.35 else min(1.0, (t - 0.35) / 0.5) * vk * math.sin(TAU * 5.0 * t)
            ph += TAU * f * 2 ** (sc / 12) * (1 + v) / SR
            tone = math.sin(ph) + 0.18 * math.sin(2 * ph) + 0.06 * math.sin(3 * ph)
            y += 0.06 * (rng.uniform(-1, 1) - y)
            out[i] = (tone + (breath + 2.5 * math.exp(-t / 0.08)) * y) * adsr(i, gate, int(0.08 * SR), 1, 1.0, rel)
        return out

    def strings(self, notes, n, attack):
        def make():
            rel = int(0.4 * SR); tot = n + rel
            out = array('d', bytes(8 * tot)); A_ = int(attack * SR)
            for m in notes:
                for det in (-0.08, 0.0, 0.08):
                    f = hz(m + det); dt = f / SR; t = self.rng.random(); y = 0.0
                    for i in range(tot):
                        y += 0.05 * ((2 * t - 1) - y)
                        out[i] += y * adsr(i, n, A_, 1, 1.0, rel)
                        t += dt
                        if t >= 1: t -= 1
            return out
        return self._cached(('str', tuple(notes), n, attack), make)

    # ------------------------------------------------------------ drums
    def _drum(self, sec, fn):
        n = int(sec * SR); out = array('d', bytes(8 * n)); s = {}
        for i in range(n):
            out[i] = fn(i / SR, s)
        return out

    def _build_kit(self):
        rng = self.rng
        def nhp(s, k='p'):
            x = rng.uniform(-1, 1); h = x - s.get(k, 0.0); s[k] = x
            return h
        def kick(t, s):
            s['ph'] = s.get('ph', 0.0) + TAU * (48 + 130 * math.exp(-t / 0.03)) / SR
            return math.sin(s['ph']) * math.exp(-t / 0.18) + 0.4 * rng.uniform(-1, 1) * math.exp(-t / 0.003)
        def snare(t, s):
            return 0.5 * math.sin(TAU * 190 * t) * math.exp(-t / 0.06) + 0.75 * nhp(s) * math.exp(-t / 0.13)
        def clap(t, s):
            burst = sum(math.exp(-(t - o) / 0.006) for o in (0.0, 0.011, 0.022) if t >= o)
            return 0.6 * nhp(s) * (0.6 * burst + math.exp(-max(0.0, t - 0.022) / 0.1))
        def rim(t, s):
            return (math.sin(TAU * 1700 * t) * 0.7 + 0.5 * nhp(s)) * math.exp(-t / 0.012)
        def hat(decay):
            def fn(t, s):
                h = nhp(s); h2 = h - s.get('q', 0.0); s['q'] = h
                return 0.5 * h2 * math.exp(-t / decay)
            return fn
        def shaker(t, s):
            h = nhp(s); h2 = h - s.get('q', 0.0); s['q'] = h
            return 0.4 * h2 * min(1.0, t / 0.006) * math.exp(-t / 0.035)
        def tom(f0):
            def fn(t, s):
                s['ph'] = s.get('ph', 0.0) + TAU * f0 * (0.65 + 0.35 * math.exp(-t / 0.08)) / SR
                return math.sin(s['ph']) * math.exp(-t / 0.22)
            return fn
        def crash(t, s):
            return 0.5 * nhp(s) * math.exp(-t / 0.6)
        def doum(t, s):                       # darbuka low "doum"
            s['ph'] = s.get('ph', 0.0) + TAU * (85 + 60 * math.exp(-t / 0.02)) / SR
            return math.sin(s['ph']) * math.exp(-t / 0.2) + 0.25 * nhp(s) * math.exp(-t / 0.006)
        def tek(t, s):                        # darbuka rim "tek"
            return (0.8 * math.sin(TAU * 820 * t) * math.exp(-t / 0.018) + 0.6 * nhp(s) * math.exp(-t / 0.012))
        def riq(t, s):                        # tambourine jingles
            h = nhp(s); h2 = h - s.get('q', 0.0); s['q'] = h
            ring = math.sin(TAU * 5400 * t) * 0.3 + math.sin(TAU * 7300 * t) * 0.2
            return (0.45 * h2 + ring * 0.4) * min(1.0, t / 0.002) * math.exp(-t / 0.06)
        def zill(t, s):                       # finger cymbals
            return (math.sin(TAU * 3100 * t) + 0.6 * math.sin(TAU * 4270 * t) + 0.3 * math.sin(TAU * 6900 * t)) * math.exp(-t / 0.35)
        self.DR = {'kick': self._drum(0.4, kick), 'snare': self._drum(0.35, snare), 'clap': self._drum(0.3, clap),
                   'rim': self._drum(0.06, rim), 'hat': self._drum(0.06, hat(0.012)), 'ohat': self._drum(0.3, hat(0.09)),
                   'shaker': self._drum(0.12, shaker), 'crash': self._drum(2.0, crash),
                   'tomH': self._drum(0.5, tom(190)), 'tomM': self._drum(0.5, tom(145)), 'tomL': self._drum(0.5, tom(105)),
                   'doum': self._drum(0.45, doum), 'tek': self._drum(0.1, tek), 'riq': self._drum(0.2, riq),
                   'zill': self._drum(1.0, zill)}
        self.DR['rev'] = array('d', reversed(self._drum(8 * self.STEP / SR, crash)))
        # (gain, pan, reverb send)
        self.DMIX = {'kick': (0.70, 0.0, 0.0), 'snare': (0.34, 0.05, 0.25), 'clap': (0.26, -0.05, 0.3),
                     'rim': (0.18, 0.2, 0.15), 'hat': (0.10, 0.35, 0.05), 'ohat': (0.08, 0.35, 0.1),
                     'shaker': (0.13, -0.4, 0.05), 'crash': (0.14, -0.3, 0.2), 'rev': (0.20, 0.0, 0.2),
                     'tomH': (0.32, 0.4, 0.15), 'tomM': (0.32, 0.0, 0.15), 'tomL': (0.34, -0.4, 0.15),
                     'doum': (0.45, -0.1, 0.15), 'tek': (0.22, 0.25, 0.2), 'riq': (0.10, 0.45, 0.15),
                     'zill': (0.05, -0.45, 0.3)}

    def hit(self, name, bar, step, vel=1.0):
        g, p, v = self.DMIX[name]
        self.mix(self.DR[name], self.pos(bar, step), g * vel, p, v)

    def reverse_cymbal_into(self, bar):
        g, p, v = self.DMIX['rev']
        self.mix(self.DR['rev'], self.pos(bar, 0) - len(self.DR['rev']), g, p, v)

    # ------------------------------------------------------------ finishing
    def _reverb(self):
        combs = [1116, 1188, 1277, 1356]; aps = [556, 441]; spread = 23; N = self.N
        for src, dst, off in ((self.VL, self.L, 0), (self.VR, self.R, spread)):
            out = array('d', bytes(8 * N))
            for c in combs:
                d = c + off; b = [0.0] * d; j = 0; lp = 0.0
                for i in range(N):
                    y = b[j]; lp = y * 0.72 + lp * 0.28
                    b[j] = src[i] + lp * 0.83
                    out[i] += y
                    j = j + 1 if j + 1 < d else 0
            for a in aps:
                d = a + off; b = [0.0] * d; j = 0
                for i in range(N):
                    bb = b[j]; x = out[i]
                    out[i] = -x + bb; b[j] = x + bb * 0.5
                    j = j + 1 if j + 1 < d else 0
            for i in range(N):
                dst[i] += out[i] * 0.07

    def finish(self, path, section_names, target_rms_db=-13.5):
        self._reverb()
        L, R, LOOP = self.L, self.R, self.LOOP
        for i in range(self.TAIL):                     # wrap tails onto the start: seamless loop
            L[i] += L[LOOP + i]; R[i] += R[LOOP + i]
        idx = range(0, LOOP, 5)
        drive = 10 ** (target_rms_db / 20) / math.sqrt(sum(L[i] * L[i] + R[i] * R[i] for i in idx) / (2 * len(idx)))
        ol = [math.tanh(L[i] * drive) for i in range(LOOP)]
        orr = [math.tanh(R[i] * drive) for i in range(LOOP)]
        pk = max(max(map(abs, ol)), max(map(abs, orr)))
        sc = min(1.0, 0.89 / pk)
        pcm = array('h', bytes(4 * LOOP))
        for i in range(LOOP):
            pcm[2 * i] = int(ol[i] * sc * 32767); pcm[2 * i + 1] = int(orr[i] * sc * 32767)
        with wave.open(path, 'wb') as w:
            w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR); w.writeframes(pcm.tobytes())
        print(f'  {LOOP / SR:.2f}s, {self.bars} bars @ {self.bpm} BPM, peak {20 * math.log10(pk * sc):.1f} dBFS, '
              f'rendered in {time.time() - self.t0:.0f}s')
        per = self.bars // len(section_names)
        for si, name in enumerate(section_names):
            a, b = si * per * self.BAR, (si + 1) * per * self.BAR
            r = math.sqrt(sum(ol[i] ** 2 + orr[i] ** 2 for i in range(a, b, 7)) / (2 * len(range(a, b, 7)))) * sc
            print(f'  {name:4s} RMS {20 * math.log10(r):6.1f} dBFS')

def _blep(t, dt):
    if t < dt:
        t /= dt; return t + t - t * t - 1.0
    if t > 1.0 - dt:
        t = (t - 1.0) / dt; return t * t + t + t + 1.0
    return 0.0
