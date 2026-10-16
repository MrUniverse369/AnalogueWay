"""
General gameplay sound effects for AnalogueWay (pure Python, stdlib only). Reuses the helpers in japan_sfx.py.

Usage:  python3 game_sfx.py <Assets folder>
Writes:
  Sound/Enemy/EnemyDefeat_Stomp.wav   squashy "bop" + smoke poof + little reward sparkle
  Sound/Enemy/EnemyDefeat_Dash.wav    swish + sharp metallic hit + punch + quicker, brighter poof
  Sound/World/Checkpoint.wav          rising D-A-E chime (fits the D, G and E based level music)
  Sound/World/ChestOpen.wav           wooden creak + latch clunk + treasure glitter
  Sound/Player/PlayerHurt.wav         punchy hit + quick falling 'ow' blip
  Sound/Player/LivesDepleted.wav      KO: hit + falling In-scale koto line + taiko + downward whoosh
"""
import math, os, random, sys
from array import array

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from japan_sfx import SR, TAU, hz, buf, add, fm_bell, whoosh, reverb, write, koto, taiko   # noqa: E402

def poof(sec, f_from, f_to, rng, decay):
    """Smoke puff: noise through a closing low-pass, soft attack."""
    n = int(sec * SR); out = array('d', bytes(8 * n)); l1 = l2 = 0.0
    for i in range(n):
        t = i / SR
        fc = f_from * (f_to / f_from) ** min(1.0, t / (sec * 0.7))
        a = 2 * math.pi * fc / SR
        x = rng.uniform(-1, 1)
        l1 += a * (x - l1); l2 += a * (l1 - l2)
        out[i] = 2.2 * l2 * min(1.0, t / 0.008) * math.exp(-t / decay)
    return out

def pitch_drop(sec, f_from, f_to, decay, fm_index=0.0):
    n = int(sec * SR); out = array('d', bytes(8 * n)); ph = 0.0
    for i in range(n):
        t = i / SR
        f = f_to + (f_from - f_to) * math.exp(-t / (sec * 0.25))
        ph += TAU * f / SR
        out[i] = math.sin(ph + fm_index * math.exp(-t / 0.02) * math.sin(2 * ph)) * math.exp(-t / decay)
    return out

def metal_hit(sec, f, rng):
    n = int(sec * SR); out = array('d', bytes(8 * n)); pc = pm = 0.0
    for i in range(n):
        t = i / SR
        pc += TAU * f / SR; pm += TAU * 1.41 * f / SR
        out[i] = (math.sin(pc + 3.0 * math.exp(-t / 0.03) * math.sin(pm)) * math.exp(-t / 0.09)
                  + rng.uniform(-1, 1) * math.exp(-t / 0.004))
    return out

def stereo_from(mono, wet):
    wl, wr = reverb(mono, wet=wet)
    return [wl, wr]

def stomp_defeat(rng):
    m = buf(0.75)
    add(m, pitch_drop(0.16, 520, 130, 0.07, fm_index=0.8), 0, 0.9)     # squash "bop"
    add(m, poof(0.45, 3200, 450, rng, 0.14), int(0.02 * SR), 0.8)       # smoke
    add(m, fm_bell(88, 0.5), int(0.08 * SR), 0.12)                      # E6 sparkle
    add(m, fm_bell(95, 0.5), int(0.14 * SR), 0.10)                      # B6
    return stereo_from(m, 0.35)

def dash_defeat(rng):
    m = buf(0.7)
    add(m, whoosh(0.08, 1500, 6000, rng, q=1.2, attack=0.5), 0, 0.6)    # swish in
    t = int(0.03 * SR)
    add(m, metal_hit(0.3, 1850, rng), t, 0.5)                           # sharp "shing" hit
    add(m, pitch_drop(0.12, 160, 50, 0.05), t, 0.8)                     # punch
    add(m, poof(0.35, 5200, 900, rng, 0.09), t + int(0.01 * SR), 0.75)  # quicker, brighter smoke
    return stereo_from(m, 0.3)

def checkpoint(rng):
    m = buf(1.8)
    for k, note in enumerate((74, 81, 88, 93, 98)):                     # D5 A5 E6 A6 D7
        add(m, fm_bell(note, 1.2, ratio=3.0, index=1.8), int(k * 0.06 * SR), 0.28 - 0.025 * k)
    add(m, fm_bell(62, 1.4, ratio=2.0, index=0.8), 0, 0.18)             # soft D4 body
    shimmer = whoosh(1.0, 6000, 9000, rng, q=3.0, attack=0.3)
    add(m, shimmer, int(0.15 * SR), 0.12)
    return stereo_from(m, 0.6)

def chest_open(rng):
    m = buf(2.5)
    # wooden creak: buzzy tone with jittery pitch through a resonant band-pass
    n = int(0.32 * SR); low = band = 0.0; ph = 0.0
    for i in range(n):
        t = i / SR
        f = 150 + 110 * (t / 0.32) + 25 * math.sin(TAU * 31 * t) * rng.uniform(0.6, 1.0)
        ph += TAU * f / SR
        x = (2 * (ph / TAU % 1.0) - 1) * (0.5 + 0.5 * math.sin(TAU * 47 * t) ** 2)
        fc = 2 * math.sin(math.pi * 900 / SR)
        high = x - low - band / 4.0; band += fc * high; low += fc * band
        m[i] += 0.5 * band * min(1.0, t / 0.03) * (1 - t / 0.32)
    t = int(0.3 * SR)
    add(m, pitch_drop(0.15, 220, 70, 0.05), t, 0.7)                     # latch clunk
    add(m, metal_hit(0.12, 2600, rng), t, 0.12)
    for k, note in enumerate((81, 86, 88, 93)):                         # A5 D6 E6 A6 reveal
        add(m, fm_bell(note, 1.2), t + int((0.08 + 0.07 * k) * SR), 0.22)
    for _ in range(14):                                                 # glitter
        note = rng.choice((98, 100, 105, 110))
        add(m, fm_bell(note, 0.5, ratio=3.5, index=1.0), t + int(rng.uniform(0.3, 1.2) * SR), 0.06)
    return stereo_from(m, 0.6)

def player_hurt(rng):
    """Punchy hit + quick falling 'ow' blip with a wobble."""
    m = buf(0.55)
    add(m, pitch_drop(0.15, 240, 90, 0.05), 0, 0.8)                     # body hit
    n = int(0.05 * SR); snap = array('d', bytes(8 * n)); p = 0.0
    for i in range(n):
        x = rng.uniform(-1, 1); snap[i] = (x - p) * math.exp(-i / SR / 0.008); p = x
    add(m, snap, 0, 0.5)
    n = int(0.3 * SR); blip = array('d', bytes(8 * n)); pc = 0.0
    for i in range(n):                                                  # falling 'ow'
        t = i / SR
        f = 440 + 480 * math.exp(-t / 0.08)
        f *= 1 + 0.03 * math.sin(TAU * 18 * t)
        pc += TAU * f / SR
        blip[i] = math.sin(pc + 1.4 * math.exp(-t / 0.1) * math.sin(2 * pc)) * min(1.0, t / 0.005) * math.exp(-t / 0.11)
    add(m, blip, int(0.015 * SR), 0.45)
    return stereo_from(m, 0.25)

def lives_depleted(rng):
    """KO: hit, falling In-scale koto line (D), taiko, downward whoosh, soft low bell."""
    L, R, m = buf(2.6), buf(2.6), buf(2.6)
    add(m, pitch_drop(0.2, 200, 60, 0.08), 0, 0.8)
    add(m, whoosh(0.9, 3000, 200, rng, q=1.2, attack=0.08), 0, 0.35)
    for k, note in enumerate((74, 70, 69, 63, 62)):                     # D5 Bb4 A4 Eb4 D4
        st = int((0.08 + 0.17 * k) * SR)
        pl = koto(note, 1.5, rng)
        add(L, pl, st, 0.32 * (0.8 if k % 2 else 0.5)); add(R, pl, st, 0.32 * (0.5 if k % 2 else 0.8))
        add(m, fm_bell(note - 12, 1.0, ratio=1.0, index=1.2), st, 0.08)  # soft body under each note
    end = int((0.08 + 0.17 * 4) * SR)
    add(m, taiko(1.0, rng, pitch=0.8), end, 0.7)
    add(m, fm_bell(50, 1.6, ratio=2.0, index=0.8), end, 0.15)           # D3 low bell
    dry = array('d', (m[i] + 0.5 * (L[i] + R[i]) for i in range(len(m))))
    wl, wr = reverb(dry, wet=0.8)
    return [array('d', (L[i] + m[i] + wl[i] - dry[i] for i in range(len(L)))),
            array('d', (R[i] + m[i] + wr[i] - dry[i] for i in range(len(R))))]

if __name__ == '__main__':
    # Loudness targets are set against the music at AudioManager.musicVolume 0.35 (see japan_sfx.loudness_db).
    root = sys.argv[1]
    rng = random.Random(77)
    print('rendering:')
    write(os.path.join(root, 'Sound/Enemy/EnemyDefeat_Stomp.wav'), stomp_defeat(rng), -1.5, loud_db=-25.0)
    write(os.path.join(root, 'Sound/Enemy/EnemyDefeat_Dash.wav'), dash_defeat(rng), -1.5, loud_db=-25.0)
    write(os.path.join(root, 'Sound/World/Checkpoint.wav'), checkpoint(rng), -2.0, loud_db=-26.0)
    write(os.path.join(root, 'Sound/World/ChestOpen.wav'), chest_open(rng), -2.0, loud_db=-25.5)
    write(os.path.join(root, 'Sound/Player/PlayerHurt.wav'), player_hurt(rng), -1.5, loud_db=-24.5)
    write(os.path.join(root, 'Sound/Player/LivesDepleted.wav'), lives_depleted(rng), -1.0, loud_db=-24.5)
