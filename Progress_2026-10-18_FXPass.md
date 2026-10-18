# HNL gameplay / audio / FX / UI pass: progress and continuation notes

Started 2026-10-18 08:51 BST. There's no reliable information about the usage-refresh schedule, so no refresh time is assumed.
Changed code is marked `// FX:`, `// FLOW:`, `// PAUSE:` and `// SFX:`.

## Core FX layer (done, compiles)
- `Assets/Shaders/HNLSprite.shader`: the shared sprite shader. It draws like Sprites/Default when every effect is at zero.
  - Runtime effects: flash, dither, grey, stone and palette swap.
  - Material keyword effects: SHINE, SWAY, FLUFF, CLOUDS and HAZE.
- `Assets/Shaders/PixelParticle.shader`: particle quads snapped to the 16 PPU grid.
- `Assets/Resources/FX/*.mat`: HNLSprite, _Cloud, _Shine, _Sway, _Sky, _Haze, PixelParticle and SpriteGlow. They're loaded at runtime, which keeps the shaders in builds.
- `Assets/Resources/FX/Shapes/*.png`: particle shapes and the CatHead iris mask, made by `Tools/Art/fx_shapes.py`.
- Scripts:
  - `PixelFX.cs`: the particle API (Emit, Burst, Ring), with shared systems created on demand.
  - `SpriteFX.cs`: the MaterialPropertyBlock controller.
  - `SpriteAura.cs`: the outline/aura built on SpriteGlow.
  - `PlayerFX.cs`, `StompCombo.cs`, `TalismanFX.cs` and `BouncyCloud.cs`.

## Status (updated 10:15; everything below compiles)
Tested = verified in Play mode with logs and/or screenshots (Arcade mode, so no save was written).

| # | Item | Status |
|---|---|---|
| 1 | Pause closes on Circle/Backspace | Tested (virtual gamepad Circle -> unpaused, timeScale 1) |
| 2 | Hurt: new non-vocal HNL_Hit sound, cream flash, sparks, dithered flicker; enemy white flash | Visuals tested; the sound is assigned but was chosen by analysis, not by ear |
| 3 | Stomp combo 1.5 s window, 5 stages | Tested: stages 1->2, expiry -> 0, next = 1, a hit resets it; stage 5 visual checked after toning down |
| 4 | Cloud fluff shader, squash, puffs, HNL_CloudBounce | Shader and squash tested in Z4A7; the sound wasn't listened to |
| 5 | HNL_CoinChime and PlayCoin pitch steps, coin pop, shines, Chi/item glow, chest sparkles | Chest sparkles and coin shine seen; the coin sound wasn't listened to |
| 6 | Play -> file -> Zone Select; Continue/Zone Select removed | Tested (continue and new game); File 1 byte-identical; test file deleted |
| 7 | Simple title, shader pattern, title shine, Options -> Controls, menu particles, menu mosaic | Tested (screenshots); Controls row spacing fixed after the screenshot, not re-shot |
| 8 | Talisman auras and FX, HUD radial, water vignette, zone atmospheres, tomb darkness, hazards, checkpoint, boss, world switch, iris, game-over grey, Zone Select FX, shop grey | Most tested by screenshot. Not visually verified: Spirit Dash afterimages, HUD radial wedge, game-over grey pulse, shop greyscale, Green Zone sway, sky clouds, Egypt haze, BossHazard glow/dust, checkpoint flash |
| 9 | PixelFX particle system | Tested throughout |
| 10 | Samurai Walk restored | Done (controller diff checked); not play-tested |

## Notes / follow-ups for the user
- All three equipped talismans have Cooldown 0, so the HUD only ever shows the cream "time left" wedge. The dark cooldown wedge needs a cooldown > 0.
- The Z2A1 start shows black above the cave while the camera catches up. It's the level layout (checked in edit mode), not the FX.
- Unused files the user may delete in Unity: JSEAnim_Sword.png, SwordDraw.anim, Icon_Continue.png, Icon_ZoneSelect.png, DustMote.png, old HNL_Hurt/HNL_Coin clips.
- ElevenLabs alternatives are in flow mVAqVfpJqNay2ZRo8tyl (coin, hurt and cloud nodes, 4 takes each).
