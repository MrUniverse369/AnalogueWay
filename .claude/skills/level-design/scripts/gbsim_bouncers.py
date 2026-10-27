"""gbsim plus moving, one-use flying bounce enemies (MovingTiles ping-pong, GHOSTSYNC followers), for any route that
stomps across moving enemies. Written by the 26 Oct Grassy Green test plan (Level 4) and adopted into the skill.
Usage: set BSim.bouncers = [Bouncer(x0, x1, y), Bouncer(..., leader=b0, lag=1.5), ...] and call run(level, start,
route); route steps are gbsim's plus ('waitb', 0, i, xlo, xhi, heading), ('chase', d, i) and ('jumpnow', d).

Bouncer model (from the project files): holder starts with the enemy at the patrol centre heading to pointB (x1);
MoveTowards at `speed`; a follower sits where its loop is `lag` s behind its leader's LoopTime. A stomp (stomp box
falling onto the hit box top) sets vy = 7 and the enemy is gone until a respawn (EnemyController -> SetActive false,
LevelManager re-enables on respawn). Touching it any other way = a hit (recorded, not death)."""
import os
import sys

sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import gbsim as g  # noqa: E402

HB = (-0.48, 0.48, 0.22, 0.5)
DELAY = 0.0  # seconds of reaction lag when steering at a moving bouncer


class Bouncer:
    def __init__(self, x0, x1, y, speed=2.0, leader=None, lag=0.0, phase0=0.0):
        self.x0, self.x1, self.y, self.speed = x0, x1, y, speed
        self.leader, self.lag = leader, lag
        self.leg = (x1 - x0) / speed
        self.t0 = phase0          # loop time at sim t=0 (leader only); centre heading to B = leg/2
        self.alive = True

    def loop(self, t):
        if self.leader is None:
            return (self.t0 + t) % (2 * self.leg)
        return (self.leader.loop(t) - self.lag) % (2 * self.leg)

    def x(self, t):
        u = self.loop(t)
        if u < self.leg:
            return self.x0 + self.speed * u
        return self.x1 - self.speed * (u - self.leg)

    def heading(self, t):
        return 1 if self.loop(t) < self.leg else -1

    def box(self, t):
        x = self.x(t)
        return (x + HB[0], x + HB[1], self.y + HB[2], self.y + HB[3])


class BSim(g.Sim):
    bouncers = []
    tbase = 0.0

    def __init__(self, level, x, y):
        super().__init__(level, x, y)
        self.hits = []
        self.stomps = []
        for b in self.bouncers:
            b.alive = True

    def step(self, move, press):
        falling_before = self.vy < -0.5
        super().step(move, press)
        t = self.t + self.tbase
        sb = g.rel_box(g.STOMP, self.x, self.y, self.facing)
        body = self.body()
        for i, b in enumerate(self.bouncers):
            if not b.alive:
                continue
            bx = b.box(t)
            if falling_before and g.overlap(sb, bx) and (sb[2] + sb[3]) / 2 >= bx[3] - 0.25:
                self.vy = 7.0
                b.alive = False
                self.stomps.append((i, round(self.t, 2), round(self.x, 2)))
            elif g.overlap(body, bx):
                if not self.hits or self.hits[-1][0] != i:
                    self.hits.append((i, round(self.t, 2), round(self.x, 2), round(self.y, 2)))


def run(level, start, route, limit=400.0):
    """gbsim.run_route plus: ('waitb', 0, i, xlo, xhi, heading) wait still until bouncer i is in [xlo, xhi] heading
    that way (0 = either); ('chase', d, i) steer at bouncer i (offset d) until it is stomped; ('jumpnow', d) jump as
    soon as grounded. Contacts other than a stomp are recorded in sim.hits (a hit, not a death)."""
    s = BSim(level, *start)
    i, state, log = 0, {}, []
    while i < len(route) and s.t < limit and s.dead is None:
        st = route[i]
        kind, d = st[0], st[1]
        press = False
        done = False
        t = s.t + s.tbase
        if kind == "waitb":
            b = BSim.bouncers[st[2]]
            bx = b.x(t)
            done = st[3] <= bx <= st[4] and (st[5] == 0 or b.heading(t) == st[5])
            d = 0
        elif kind == "chase":
            b = BSim.bouncers[st[2]]
            if not b.alive:
                done = True
            else:
                dx = b.x(t - DELAY) + d - s.x
                d = 0 if abs(dx) < 0.12 else (1 if dx > 0 else -1)
        elif kind == "go":
            done = (s.x >= st[2]) if d > 0 else (s.x <= st[2])
        elif kind == "jump":
            gnd = s.ground_check()
            crossed = (s.x >= st[2]) if d > 0 else (s.x <= st[2])
            if state.get("pressed"):
                done = not gnd or s.vy > 0.5
            elif gnd and crossed:
                press = True
                state["pressed"] = True
        elif kind == "jumpnow":
            if state.get("pressed"):
                done = not s.ground_check() or s.vy > 0.5
            elif s.ground_check():
                press = True
                state["pressed"] = True
        elif kind == "air":
            done = (s.x >= st[2]) if d > 0 else (s.x <= st[2])
        elif kind == "until_y":
            done = s.y >= st[2]
        elif kind == "until_vy":
            done = s.vy >= st[2]
        elif kind == "aim":
            if s.vy < 0:
                state["fell"] = True
            if len(st) > 3 and st[3] == "launch":
                done = state.get("fell", False) and s.vy > 6
            else:
                if not s.ground_check():
                    state["air"] = True
                done = state.get("air", False) and s.ground_check() and s.vy <= 0.01
            dx = st[2] - s.x
            d = 0 if abs(dx) < 0.15 else (1 if dx > 0 else -1)
        elif kind == "land":
            gnd = s.ground_check()
            if not gnd:
                state["air"] = True
            done = gnd and s.vy <= 0.01 and (state.get("air") or s.t - state.get("t0", s.t) > 0.1)
            state.setdefault("t0", s.t)
        elif kind == "wait":
            state.setdefault("t0", s.t)
            done = s.t - state["t0"] >= st[2]
        if done:
            log.append((round(s.t, 2), i, st, round(s.x, 2), round(s.y, 2)))
            i += 1
            state = {}
            continue
        s.step(d, press)
    return s, i, log
