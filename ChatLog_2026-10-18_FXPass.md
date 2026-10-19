# Harvey's Nine Lives: session log, 18–19 October 2026 (gameplay, audio, effects and UI pass, then the 19 October follow-ups)

A record of the 18 October chat and the 19 October follow-up chat: what was asked, what was changed, how it was tested, and what is still open.

**Requests**
1. The big gameplay, audio, effects and UI pass (sections 1–10 below)
2. A markdown log of the session (this file)
3. The samurai monkey's dashing sword attack from a new sprite sheet (the follow-up section)
4. This updated log, with a list of what still needs finishing (the end of this file)
5. The night follow-ups: map after a level, the Egypt fish climb, a dash combo, the AI Assistant relay error, a loading system and the tomb poison
6. Late night: the new DragonRideTwo mount loop, the tomb octopus's walk cycle and zone-by-zone music on the map
7. After midnight: the samurai clash, samurai afterimages and the Z3A2 ghost bounce, then a text dialogue system for the Japan village
8. 19 October (a new chat): one shared stomp/dash combo, a sprite grounding audit, the sky-islands title screen, the Z3A2 ghost stomps and the Sumaria boss fight (the last section)

The session started at 08:51 BST from a clean tree (last commit `8551d6d saveSystem update`). The 18 October work was committed on 19 October as `8b67cc1 gg`. **The 19 October work (the last section) is not committed yet.**

Changed code is marked `// FX:`, `// FLOW:`, `// PAUSE:`, `// OPTIONS:` and `// SFX:` (19 October: `COMBO`, `MARKER`, `STOMPTIE`, `GROUND`, `TITLE` and `SAMURAIBOSS`). The working checklist for the 18 October pass is in `Progress_2026-10-18_FXPass.md`.

---

## The request

One large brief with eleven sections:
1. Pause menu back button
2. Harvey's damage feedback
3. Stomp glow and combo stages
4. Green Zone clouds
5. Coin sound and pickup effects
6. Main menu and save-file flow
7. Title screen and Options
8. Shader improvements
9. A pixel-art particle system
10. Restoring the samurai's walk
11. How to work: inspect first, reuse systems, test, keep a checklist, report honestly, and plan for usage limits

On usage limits: no reliable information about the usage-refresh schedule was available, so none was assumed and nothing was scheduled. A continuation file was kept up to date instead.

---

## The shared effects layer (built first, used by everything else)

### Shaders (`Assets/Shaders`)
- **`HNLSprite.shader`:** one shared sprite shader. With every effect at zero it draws exactly like `Sprites/Default`, so it can be swapped onto any sprite.
  - Runtime effects, set per sprite: hit flash, ordered or random-pixel dither, greyscale, Stone Skin overlay, palette swap, squash.
  - Material variants:
    - shine sweep (coins, chests, spikes, traps, title)
    - wind sway (trees and bushes)
    - fluffy cloud
    - scrolling sky clouds
    - heat haze
    - paw-and-star pattern (title screen)
- **`PixelParticle.shader`:** every particle corner snaps to the 16 px grid, so particles are crisp blocks of whole art pixels.
- **`HNLUI.shader`:** UI greyscale (shop) and the cat-head iris wipe (scene transitions).
- **`TombDarkness.shader`:** darkness with dithered light circles.
- **`SpriteGlow.shader`** was fixed. In Unity 6 a sprite's colour reaches shaders through `unity_SpriteColor`, not only the vertex colour, so every glow had been drawing white. That includes the existing Water Breathing outline.
- **`UnderwaterOverlay.shader`** gained the air vignette.

### Materials and art
- All materials live in `Assets/Resources/FX` and are loaded at runtime. This keeps the shaders in builds.
- Particle shapes are in `Assets/Resources/FX/Shapes`, made by the new `Tools/Art/fx_shapes.py`: star, cross, feather, leaf, petal, cat-head mask and HUD gem mask.

### Scripts (new)
| Script | What it does |
|---|---|
| `PixelFX.cs` | The particle system. `Emit` / `Burst` / `Ring` into a few shared Unity ParticleSystems (one per shape, motion, glow and layer), created only when first used. Zone palettes (grass, sand, stone, snow, water, spirit, confetti). |
| `SpriteFX.cs` | Per-sprite controller for the shared shader's runtime effects. |
| `SpriteAura.cs` | Reusable outline and aura built on SpriteGlow; several requests with priorities (a talisman aura and the stomp glow can coexist). |
| `PlayerFX.cs` | Harvey's hurt flash, sparks and dithered flicker; landing, running and jump dust. Added automatically by `PlayerController`. |
| `StompCombo.cs` | The stomp combo. |
| `TalismanFX.cs` | Every talisman's visuals, driven by `PowerUpManager`'s existing events. |
| `BouncyCloud.cs` | Cloud squash, puffs and sound. |
| `ZoneAtmosphere.cs` | Sets up each zone's atmosphere automatically when a level loads, so **no scenes needed editing**. |
| `TombDarkness.cs` | Tomb darkness and lights. |
| `ScreenGrade.cs` | A runtime URP Volume: screen ripple, spirit-world tint, game-over greyscale pulse. |
| `IrisWipe.cs` | The cat-head iris transition. |
| `ZoneMapFX.cs` | Zone Select effects. |
| `MenuBlur.cs` | Pixelated game view behind menus (the camera rendered into an 80x45 texture). |
| `OptionsMenu.cs` | The Options and Controls screens. |

---

## 1. Pause menu

### What was done
Circle (O) on the pad, or Backspace, now closes the pause menu and returns to the game, like Resume. Esc and Options still toggle as before.
- Closing the Bag or Talisman Holder with Circle doesn't also unpause; the existing same-frame guard handles it.
- The Circle press that closes the menu doesn't fire talisman 3 (its queued press is cleared).

### Tested
A virtual gamepad pressed Circle while paused. The game unpaused and the time scale returned to 1.

---

## 2. Harvey's damage feedback

### What was done
- **Sound:**
  - The meow-like hurt sound (a sustained high tone, about 2–3.5 kHz for 0.7 s) was replaced by `HNL_Hit.wav`: a short punchy thump with a quick high blip, gone in about 0.33 s.
  - It was made in ElevenLabs, picked from four takes, trimmed, and matched to the old clip's loudness.
- **Hit flash and sparks:** a brief cream flash on Harvey with cream/orange impact sparks. Hooked into every damage source: enemies and spikes (`HurtPlayer`), the boss and its hazards (`YokaiBoss`) and drowning (`PlayerWater`).
- **Invincibility:**
  - The old "blink" was baked into the hurt animations as white-silhouette frames.
  - Those frames now point to the neighbouring normal pose. The existing frames were reused and no art was redrawn.
  - Instead, Harvey flickers with a dither pattern for about 0.9 s.
- **Enemies:** the same flash (white) plays on an enemy just before its smoke animation.

### Tested
The cream flash, sparks and dithered flicker, and the enemy white flash, were checked in screenshots. The new sound was **not listened to**.

---

## 3. Stomp combo

> **Updated 19 October:** stomps and dash defeats now share one combo, with new stage colours (yellow, orange, hot pink, violet, rainbow) and effects that last the whole window. See the 19 October section.

### What was done
- Only **successful stomps** count: an enemy's stomp defeat, or a stomp hit on a boss. Jumps, bounces and dashes don't.
- **Timing:** the next stomp must land within **1.5 s** to advance the chain. Otherwise the next stomp starts again at stage 1. A hit, death or respawn also ends the chain.
- **Stages,** each clearly different. The glow, rings, impact stars and orbiting motes share each stage's colours so they read as one effect.

| Stage | Look |
|---|---|
| 1 | cream glow, one ring, 3 impact stars |
| 2 | orange glow with a soft aura, wider ring, 5 stars, rising sparks, 1 orbiting mote |
| 3 | spirit-cyan pulsing aura, double ring, 7 stars, 2 motes |
| 4 | violet pulsing aura, double ring of bigger pixels, 9 stars, 3 motes |
| 5+ | aura cycling the HUD colours, a little confetti, 9 stars, 4 motes |

### Tested
- Real stomps on real enemies: stage 1, then stage 2 0.7 s later. After 1.6 s with no stomp it reset to 0, and the next stomp was stage 1 again.
- Five quick stomps reached stage 5, and a hit reset the chain to 0.
- **Problem found and fixed:** at stage 5 every glow particle started at Harvey's centre and turned him into a white blob. Rings now start just outside him, with fewer particles. Re-checked: Harvey stays readable.

---

## 4. Clouds (Green Zone)

### What was done
- Bounce boxes that are clouds (by name or sprite, such as `JumpCloud` in Z4A7) automatically get `BouncyCloud`. Yellow spring boxes keep their old sound.
- **Look:** the fluffy shader gives a cream rim light on top, a cool shade underneath, and gently "breathing" puffs.
- **Bounce:** a springy squash in whole pixels, plus puff particles.
- **Sound:** a soft springy cloud bounce (`HNL_CloudBounce.wav`) in a new `cloudBounceSound` slot.
- The collider and bounce strength are unchanged.

### Tested
In Z4A7 a dropped Harvey squashed the cloud and the shader showed (screenshot). The sound was not listened to.

---

## 5. Coins and pickups

### What was done
- **Coin sound:**
  - Your `CoinSoundRef` was analysed: bright, C6–C7 with C/E/G partials, instant attack, about 0.7 s tail.
  - A new chime, `HNL_CoinChime.wav`, keeps that C-major sparkle but is glassier and less "coin". It was picked from four takes.
  - `AudioManager.PlayCoin()` plays coins on four overlapping voices, and quick pickups step up a major scale. Rapid collecting no longer cuts sounds off.
- A pop of 3–4 yellow pixels on every coin.
- A shine sweep on coins and chests.
- A pulsing glow on Chi Stones and items, and motes drawn into Harvey on pickup.
- A sparkle fountain when a chest opens, alongside the coin arcs.

### Tested
The chest sparkles and coin shine were seen in screenshots. The coin sound was not listened to.

---

## 6. Main menu and save files

### What was done
- **New flow:** Main Menu → **Play** → choose a save file → **Zone Select**.
  - An empty file starts a new game and opens Zone Select with Act 1 unlocked.
  - A used file can be continued, or replaced after the existing confirmation.
  - Either way the Zone Select opens on that file's level.
- **CONTINUE** and **ZONE SELECT** were removed from the main menu. The buttons are now PLAY, ARCADE, OPTIONS, with wrap-around navigation.
- Arcade is unchanged. Saving logic is unchanged apart from where each choice leads.

### Tested
- Continuing File 1 opened Zone Select. File 1's save was **byte-for-byte unchanged**.
- A new game in empty File 2 opened Zone Select with only Act 1 unlocked.
- The test file was deleted and File 1 was made the active file again.

---

## 7. Title screen and Options

### What was done
- **Backdrop:** the detailed mountains and sand were replaced with calm purple bands and a slowly scrolling, twinkling paw-print and star pattern (a shader). Harvey's dig site sits on a simple sand ledge. Made by `Tools/Art/main_menu.py`, which keeps the old version as `backdrop_dawn()`.
- **Title:** a shine sweep across the title.
- **Particles:** the hand-made sparkle sprites were replaced with PixelFX motes, lantern embers, and dirt flicking out of the dig pit.
  - Harvey only idles on the menu (he doesn't dig), so the dirt comes from the pit.
- **Options → Controls:** lists every action with its keyboard keys and PS5 buttons, taken from the game's actual bindings. A new gear icon, `Icon_Options.png`.
- **Pixelated game view** behind the save-file and Options screens.

### Tested
Screenshots of the title, save-file screen, Options and Controls. The last Controls row overlapped the BACK button; the spacing was fixed, but the fix wasn't re-screenshotted.

---

## 8 and 9. Shader effects and particles

| Area | What was added | Checked |
|---|---|---|
| **Talismans** | Coloured burst ring on activation and a per-talisman aura | yes |
| | Stone Skin grey stone overlay | activation logged |
| | Spirit Dash afterimages and speed streaks | aura seen, afterimages **not** |
| | Super Jump dust ring and spark trail | yes |
| | Feather Fall drifting feathers | yes |
| | Coin Magnet spark streaks and pulse ring | yes |
| **HUD** | Radial wedge per talisman gem (dark = cooldown, cream = time left) | not visually |
| **Water** | Air vignette that closes in as breath runs low and pulses red when drowning; splash bursts, drips on climbing out, drifting specks | yes |
| **Tomb** | Darkness with dithered light circles (Harvey, checkpoints, any torches), dust motes, torch embers | yes (lightened once) |
| **Egypt** | Sand wisps (not underwater), heat haze on the far background | wisps yes, haze not |
| **Japan** | Sakura petals; fireflies in the village | petals yes |
| **Green Zone** | Drifting leaves and pollen; tree and bush sway | leaves yes, sway not |
| **Skies** | Scrolling pixel clouds inside the parallax skies | not visually |
| **Movement** | Landing dust scaled by fall height, footstep dust, jump puffs, all in zone colours | partly |
| **Falling tiles** | 0.3 s shake with a glowing crack before the drop, stone debris on breaking | timing logged |
| **Spikes, traps** | Subtle glint | not visually |
| **Checkpoints** | Activation flash and upward spark burst | not visually |
| **Bosses** | Stage palette swap (stays bright) instead of colour multiplying; a real white hit flash (white × colour never showed before); pixel dissolve for teleport and defeat; slam debris, wisps, defeat burst | defeat dissolve yes |
| **Boss hazards** | Pulsing warning glow, dirt spray from spikes, dust where rocks land | not visually |
| **World switch** | Screen ripple, spirit-world colour tint, spirit wisps, burst at the gate | yes |
| **Transitions** | Cat-head iris for FadeIn, FadeOut and Zone Select | yes |
| **Game over** | Brief greyscale pulse | not visually |
| **Zone Select** | Locked nodes greyscale, cleared gems shine and sparkle, marching dashed path, dust as Harvey walks | iris and map yes; dashes recoloured orange after test |
| **Menus** | Pixelated game view behind pause, Bag, Talismans, shop; unaffordable shop items in greyscale | pause yes, shop not |
| **Level complete** | Confetti and a coin shower | yes |

The falling-tile shake adds a small 0.3 s delay before the drop. This is a deliberate timing change, because the brief asked for a warning before the drop.

**Spirit world:** in the only level with a world switch (Z3A1), the code treats Harvey's starting world as the spirit world: its enemies are the `SpiritEnemy` ones, and switching away plays the "to living" sound. So the cool tint and wisps show there and stop after the gate.

---

## 10. Samurai walk

### What was done
- In `BounceEnemy.controller`, the SwordDraw state and its two transitions were removed, so the samurai walks with the original JSEAnim Walk cycle again.
- Two unrelated animator fixes from the same earlier commit were kept.
- `JSEAnim_Sword.png` and `SwordDraw.anim` are now unused. They were left for you to delete.

### Tested
The controller diff was checked against the pre-sword version. **Not play-tested.**

---

## Audio (ElevenLabs)

- **Generated:** 3 prompts × 4 takes on your existing flow (`mVAqVfpJqNay2ZRo8tyl`), about 200 credits in total.
- **Picked:**
  - Coin: the take matching your reference's notes.
  - Hurt: the thump-and-blip take.
  - Cloud: a short rising "boing".
- **Prepared:** each pick was trimmed and loudness-matched (the coin to the old coin, the hurt to the old hurt, the cloud to 80% of the spring pad).
- **Installed:** the clips use the same import settings as the other HNL clips and are assigned on the `AudioManager` prefab. No scene overrides those slots.
- **Old clips:** left in place, not deleted.

---

## Problems hit during testing (not game bugs)

- **Scripts edited during Play mode:** this reloads Unity and breaks the session, so Play mode was stopped before every later script edit.
- **Error Pause:** a test-script error froze the game until the editor was unpaused.
- **`SendMessage("TryActivate", 0)`:** the 0 bound to the wrong overload. Fixed in the test with `(object)0`.
- **Stuck time scale:** loading a scene during the level-complete freeze left the time scale at 0 in the test. In normal play the level-complete sequence always finishes.
- **Egypt black area:** a black area above the cave at the Z2A1 start turned out to be the level layout while the camera catches up (confirmed in edit mode), not the new effects.

---

## Still open
See **What still needs finishing** at the end of this log (it covers the whole chat).

---

## Follow-up: the samurai's dashing sword attack

### What was asked
Use the new sprite sheet (`SamuraiSwoardattack.jpg`) to give the Japan samurai monkey a dashing slice. It should trigger when Harvey gets close and the two are **facing each other**, to encourage attacking from the top or the back. Remove the watermark and the white background so the sheet is usable.

### The sprite sheet
- The JPEG is 8 frames (4x2) of upscaled pixel art (about 6.2 image pixels per art pixel), on white, with a "Luma AI" watermark.
- **`Tools/Art/samurai_slash.py`** turns it into **`JSEAnim_SwordAttack.png`** (8 frames, transparent):
  - **Scale:** resamples so the monkey is the same height as in the walk frames (43–44 px).
  - **Colours:** snaps every pixel to the walk sheet's own 17-colour palette, which also removes the JPEG noise. Thin dark outlines are kept.
  - **Background:** removes the white background by flood fill from the edges, so the cream fur inside the outline stays.
  - **Watermark:** removed. On white it goes with the background. Where it crossed frame 8's trousers, the trousers' true shades are restored.
  - **Feet:** the original sheet cuts the lower frames' feet off at the image edge, so they get a closing outline.
- **Pivots:** each frame's pivot lines its body up with the walk frames, whose real pivot is their centre (16.5, 23), so the monkey doesn't jump when the attack starts.
- **Original:** the JPEG is left in place. It can be deleted in Unity if you like.

### The attack (`SamuraiSlash.cs`, on the MonkieSamurai prefab's animated sprite)
- **Trigger:** Harvey within 3.2 units horizontally, on the same level (feet within 0.9 units), with the monkey facing him **and** him facing the monkey.
  - Above (a stomp coming) or behind either of them: no attack.
- **Wind-up (0.28 s):** frames 1–2, with a red-orange glint and a hilt spark as the tell. The patrol stops.
- **Dash and slice (0.2 s):** frames 3–4. The monkey dashes 2.6 units, never beyond its patrol path, with dust, red speed streaks and a new slash sound.
  - The blade's reach (1.7 × 1.4 units in front) hurts Harvey **once per attack**, exactly like an enemy hit: a life, the hurt sound and flash, and knockback away from the blade.
- **Recovery (0.52 s):** frames 5–8, then back to Walk and the patrol, with a 1.6 s cooldown.
- **Stomps still work at any time,** including mid-attack, and abort the attack. The Water/Spirit Dash immunity rules are unchanged.
- **Unity setup:** `Assets/Editor/SamuraiSlashSetup.cs` (also on the menu as Tools/HNL/Set Up Samurai Slash) does the wiring:
  - slices the sheet
  - builds `Slash.anim` (1 s, frame times matched to the code)
  - adds a "Slash" state to `BounceEnemy.controller` (death still works from it)
  - adds the component to the prefab, which covers every samurai in Z3A1 and Z3A2
  - sets the sound
- **`MovingTiles`** gained read-only getters (the moving object and the patrol's x range).
- **Sound:** `HNL_SamuraiSlash.wav` (ElevenLabs, 1 of 4 takes: a quick bright blade whoosh, about 0.2 s, matched in level to the enemy dash sound), in a new `AudioManager.samuraiSlashSound` slot.

### Tested so far (Play mode, Z3A1 switched to the samurai's world, Arcade mode)
- **Face to face, same level:** the attack started 0.06 s after Harvey was placed in front. The wind-up, the dash with red streaks and the recovery all played, facing the right way.
- **Above the monkey:** no attack.
- **Bugs found:**
  - The slice and the dashing body both hit in the same moment, so Harvey lost **2** lives. Fixed in code (a slash hit now makes Harvey invincible immediately and is skipped if he's already being knocked back), **but not re-tested yet**.
  - In the **behind** test the monkey still attacked once (no life lost). The likely cause: the dash ended at the patrol's end, so on resuming the patrol the monkey turned round to face Harvey (a genuine face-to-face). Not confirmed yet.
- **Testing stopped:** the Unity editor went unresponsive to the tools while leaving Play mode.

### Where the samurai appears
- **Z3A1:** 21 samurai, all in the "other" world. They are inactive until Harvey crosses the torii gate (`changeWorld`).
- **Z3A2:** 10 samurai. They are also inactive at the start, tagged as world enemies; the scene file doesn't switch them off itself.
- Either way, they're only met after a world switch.

---

## What still needs finishing

### Samurai sword attack (in progress)
- [ ] **Re-test** with Unity responsive again:
  - face to face loses exactly **one** life (the double-hit fix)
  - behind gives no attack (confirm the "turned at the patrol end" explanation; if it really attacks from behind, fix it)
  - above gives no attack, and a stomp mid-attack kills the monkey and cancels the slash
- [ ] **Play it by hand** in Z3A1 or Z3A2 and tune if needed. The values are on the `SamuraiSlash` component of the `MonkieSamurai` prefab:
  - trigger range 3.2
  - wind-up 0.28 s
  - dash 2.6 units in 0.2 s
  - cooldown 1.6 s
- [ ] **Check that the attack frames line up with the walk frames** in game (no jump or shift when the attack starts). The pivots were corrected to the walk's centre pivot (16.5, 23) but haven't been looked at in motion.
- [ ] **Listen to `HNL_SamuraiSlash.wav`.** It was chosen by measurement; 3 other takes are on the ElevenLabs flow.
- [ ] **Decide** whether the samurai in Z3A2 should be visible before a world switch. They currently aren't, and that wasn't changed.

### Sounds (not listened to)
- [ ] `HNL_CoinChime.wav` (coin), `HNL_Hit.wav` (hurt), `HNL_CloudBounce.wav` (cloud), `HNL_SamuraiSlash.wav` (slash). The alternatives are on the ElevenLabs flow `mVAqVfpJqNay2ZRo8tyl`.

### Effects not yet seen working in game
- [ ] Spirit Dash afterimages
- [ ] The talisman HUD radial wedge
  - all equipped talismans have **cooldown 0**, so the dark cooldown wedge never shows
  - give one a cooldown if you want it
- [ ] Game-over greyscale pulse
- [ ] Shop greyscale for items you can't afford
- [ ] Green Zone tree and bush sway
- [ ] Sky clouds in the parallax skies
- [ ] Egypt heat haze
- [ ] Boss hazard warning glow, dirt spray and dust
- [ ] Checkpoint flash
- [ ] Options → Controls page after the row-spacing fix
- [ ] Samurai **walk** after the sword-swing revert (section 10)

### Housekeeping
- [ ] **Commit.** Nothing from this chat is committed: about 95 changed or new files, including `Assets/Resources/FX/`, 15 new scripts, `Assets/Editor/SamuraiSlashSetup.cs`, 4 new shaders, 4 new sounds and `JSEAnim_SwordAttack.png`.
- [ ] **Delete if you like**, from Unity so the references stay clean:
  - `SamuraiSwoardattack.jpg` (the cleaned sheet replaces it)
  - `JSEAnim_Sword.png` and `SwordDraw.anim`
  - `Icon_Continue.png`, `Icon_ZoneSelect.png` and `DustMote.png`
  - the old `HNL_Hurt.wav` and `HNL_Coin.wav`
  - `Tools/Art/__pycache__/`
- [ ] **Optional:** the Z2A1 start shows black above the cave while the camera catches up. This is level layout, not the new effects.


---

## Follow-up (18 October, afternoon): leftovers, dragon ride, simpler title, pause → map

### Samurai sword attack: finished
- **Re-tested in Play mode** (Z3A1, Arcade, so no save was written):
  - Face to face: 1 attack, **exactly 1 life lost**. The double-hit fix works.
  - Behind: Harvey kept 1.6 units behind the monkey, looking at its back, for 6 s including a patrol turn: **0 attacks**.
  - Stomp during the wind-up: the monkey dies at once, the slash is cancelled, 0 lives lost. (A first attempt "failed" only because the test teleported Harvey before he had landed.)
  - Frame alignment, measured from the sprites: every attack frame's feet sit on the walk frames' line (−23 px from the pivot), and head height is within 2 px. No hop when the attack starts.
- **New:** a 0.45 s `turnGrace` on `SamuraiSlash`. After the patrol turns the monkey round, it can't slash straight away, so sneaking up from behind isn't punished the instant it reaches the end of its path.
- Still yours to do: listen to `HNL_SamuraiSlash.wav`, play it by hand, and decide about the Z3A2 samurai being hidden before a world switch.

### DragonRide: the new mount animation
- `Tools/Art/dragon_ride.py` cleans `DragonRide.png` (6 frames, 72x46). The original is kept at `Tools/Art/source/DragonRide_original.png`.
  - Watermark removed (frame 6; it was the only semi-transparent art).
  - 82 colours snapped to the sheet's own 29-colour palette. Harvey's blue eyes are kept.
  - Loose specks dropped.
  - **Steadied:** the frames jumped up to 8 px (frame 3). They now sit on one line with a gentle 1 px flying bob.
  - The pivot is set so Harvey's face sits where it did on the old still mount, keeping the rider over the player's collider.
- `Tools/HNL/Set Up Dragon Ride` (`Assets/Editor/DragonRideSetup.cs`) slices the sheet and builds `DragonRide.anim` (10 fps loop) and `DragonRide.controller`. It adds the Animator to `HarveyDragonMount` in `Player.prefab`, so it applies in every level.
- **Mount effect** (`DragonController.cs`, which was empty):
  - On mounting:
    - the dragon materialises from a random-pixel dissolve, with a cream flash and a short golden aura
    - a screen ripple
    - a gold spark ring and a jade star ring, rising stars, and a cloud puff underneath
  - While flying, golden motes trail off the tail.
  - On dismounting, a cloud puff and sparks.
- **Tested:** mounted in Z3A1. The effect and the flying loop were seen in screenshots, the frames cycle, and there were no console errors. The old `HarveyDragonMount.png` is now unused; delete it in Unity if you like.

### Title screen: simpler
- One flat deep purple. The bands, the paw/star pattern, the title shine, the dig pit, rope, lantern, crates and all ambient particles are gone.
- The title is centred and static.
- The menu is plain text (PLAY / ARCADE / OPTIONS): the selected item is yellow with the paw pointer, the rest cream. The button plates and icons were removed.
- Harvey idles on a small floating sand islet below the menu, with "A SET 8 JAMS GAME" centred at the bottom in muted lilac.
- `Tools/Art/main_menu.py` keeps the old backdrop as `backdrop_dig()`. `MainMenuScene` gained `ambientParticles` (off) and the label colouring.

### Pause → MAP
- In a save file (not Arcade) the pause menu has **MAP** between BAG and MAIN MENU, with a new folded-map icon (`Pause_Map.png`, catalog `pauseIcons[5]`). The buttons were tightened slightly so six still fit.
- MAP asks "BACK TO THE MAP? PROGRESS SINCE YOUR LAST CHECKPOINT IS LOST" with **NO** selected by default.
  - Circle, Backspace, Esc or Options answers no and returns to the pause menu.
  - YES unpauses and opens the Zone Select on the active save file.
- **Tested:** the button and dialog (screenshots). A virtual-pad Circle closed only the question (still paused, MAP reselected). YES loaded ZoneSelect with the time scale back at 1.
- Spotted, not changed: on the Zone Select the "FILE 1  COINS 2034" plate text runs off its left edge at this window size.

### Small edits (later the same afternoon)
- **Samurai slash:** the monkey now attacks whenever **it** faces Harvey, close and on the same level, even if Harvey has his back turned. Coming from behind the monkey or from above is still safe, and the 0.45 s turn grace stays. Tested: back turned → attacked after 0.78 s; standing behind the monkey for 5 s → no attack.
- **Stomp combo window:** 1.5 s → **5 s** (`StompCombo.comboWindow`). The stage-5 aura and the orbiting motes still fade over the window. Tested: a second stomp 3.1 s after the first → stage 2; still stage 2 at 4.5 s; reset to 0 at 5.3 s.

---

## UI sweep: one clean, consistent style across the game (18 October, evening)

### The style (HNL UI style)
- **Palette:** a purple ramp plus two text colours:
  - INK 1f1a33 outline, DEEP 2a2346 rows/slots and the title screen, PANEL 3b3468 panels, LINE 5d4f96 borders and rules
  - text roles: CREAM ffffeb text, YELLOW ffe478 the one accent (selection, titles, coins, good news), MUTED a59bd0 hints, secondary and unavailable things, RED eb564b warnings only
  - orange, grey, aqua, magenta and the brown frames are gone from the chrome; item and talisman icons keep their colours
- **Panels:** one flat rounded panel everywhere (pause, save files, Options/Controls, Talisman Holder, Bag, Shop, level complete, dialogs). No frames, trims, leather, tassels or patterns.
- **Titles:** Yellow text with a 1 px rule under it, at the same height in every view.
- **Buttons:** plain text like the title screen: Cream, and Yellow with the paw beside it when selected. No plates and no icons. This is `MenuUI.TextButton`, used by pause, Options, the save-file questions and the MAP question.
- **Rows and slots:** deep plates with a lilac border; the selected one gets a yellow border. Tabs are text with a yellow underline.
- **Hints:** Muted, small, on the bottom line.
- **One font:** PerfectDOSVGA437 everywhere, at 8 / 7 / 6 / 5 (title / button / body / hint).

### Where it lives
- `Tools/UI/ui_style.py` draws every shared panel, plate, slot, tab and button sprite at the old sizes and 9-slice borders, so every layout keeps its alignment. `menus_art.py`, `main_menu.py` and `zone_map.py` call it last; re-running them was checked to give identical files. The old themed art is still in those scripts.
- `MenuUI.cs`: the palette, standard sizes, `TextButton`, `Title`, and `MenuTextButton` (the yellow label plus a shared paw).
- `LevelUIStyle.cs` (new; applied by HarveyMenus in every level and the Win screen) restyles the old scene-built UI at runtime, so no scene or prefab needed editing:
  - the timer: a small plate in the top-right corner, 6 px from the edges like the map plates (it was white LiberationSans at 25 pt and its text spilled out of place)
  - level complete: a standard panel with STAGE CLEARED and centred stats (it was 100 pt text on a grey box)
  - game over: a big Yellow title with a rule over a purple overlay, on its own canvas
  - a final sweep that moves any remaining text onto the game font
- Pause menu: text buttons with a 14 px step, so all six (including MAP) fit without the compact mode. The icons are gone.
- Zone Select: the FILE/COINS plate sizes to its text (fixes the overflow), the act name is Cream, and the hint plate uses the hint style. The builder matches.
- Shop: the coin plate is on the title row, like the Bag.
- Win scene (edited directly): deep purple, a DEMO COMPLETE title with a rule, a thank-you line and a hint (it was a bright green box with wrapping white text).
- `MenuBlur` fix: the pixel-perfect camera left a grey bar along the bottom of the blurred background behind every menu. Gone.
- `Assets/Editor/UIShot.cs`: a reliable UI screenshot for checking UI from tools.

### Checked (screenshots in Play mode)
Title, save files, Controls, pause, the MAP question, Talisman Holder, Bag, Shop, HUD timer, game over, level complete, Zone Select and Win.
Not re-shot after the last small change: the Shop coin plate move. The Options card and the save-file questions were only seen as code (the screenshot attempts caught other screens).

### Now unused (delete in Unity if you like)
- `Assets/Sprites/UI/Menus/Pause_*.png` (6 icons, including the new Pause_Map; `HNLCatalog.pauseIcons` is no longer read)
- `Assets/Sprites/UI/MainMenu/Icon_Play/Icon_Arcade/Icon_Options.png`

---

## Night follow-ups (18 October, night)

Still uncommitted. New code is marked `// FLOW:`, `// MAP:`, `// STOMPAIR:`, `// DASHCOMBO:`, `// LOADING:`, `// POISON:` and `// RELAY:`.

### Back to the map after a level, and clearer map spots
- **What was done:**
  - `GameSession.FinishLevel()` saves progress (which unlocks the next level) and loads the Zone Select instead of the next level. The treasure (`NextLevel`) and the Japan village exit (`TownExit`) both use it. Arcade runs still go straight to the next level, because they're never saved.
  - Harvey reappears on the level he just finished, and walks to the newly unlocked one.
  - Map pads: unlocked levels (open and cleared) stand on a **green** pad; locked ones stay **dark grey** (and greyscaled). Boss skulls got the green pad too. The change is in `pad()` in `Tools/Art/zone_map.py`; only the node icons were regenerated, not the map.
- **Tested:** finishing Act 2 on a test save loaded the map with Act 3 green and everything after it grey, at normal speed. The real File 1 save wasn't touched.

### Egypt fish climb (Z2A2Sandy, the underwater shaft)
- **The problem:**
  - The four fish sat at uneven heights on long patrols (7–10 units at 2 u/s) with unrelated timing.
  - Bounces of 10 overshot the next fish by about 4 units, and the last bounce peaked below the first block above the water.
  - Breath was the hidden problem. Harvey has 8 s of air and arrives at the ledge with about 2 s left, but the climb takes 7–9 s.
- **What was done:**
  - A zig-zag of fish about 3.3 units apart, on short 3-unit patrols at 1.2 u/s.
  - Bounces of 8 / 8 / 8.5 / 12.5, so each one peaks about 2.5 units above the next fish.
  - Fish 1 swims right up to the ledge wall. Fish 3's bounce stays under the JumpBox block. Fish 4 sits right of the block and launches Harvey about 2 units above it.
  - **Stomps give air (your choice):** each underwater stomp gives back 3 s of breath with a bubble burst (`PlayerWater.GiveStompAir`, field `stompAir`).
- **Tested:** a scripted run from the ledge with no bonus air from the tunnel fish:
  - stomp combo 1 → 4 with no damage
  - breath 1.9 → 3.1 → 3.9 → 4.6 → 8 s
  - Harvey landed on the JumpBox block above the water
  - If Harvey misses fish 1 he still falls to the kill plane (y −29.8) and respawns at the level start, as before.

### Dash combo
> **Replaced 19 October** by one combo shared by stomps and dashes. See the 19 October section.

- **What was done:**
  - `StompCombo` now runs two chains, stomp and dash, with the same look (glow, rings, impact stars, orbiting motes, 5 s window). Only the colours differ:
    - stomp: cream → orange → cyan → violet → rainbow
    - dash: blossom pink → hot pink → red → crimson → a pink/red flicker
  - Dash defeats come from Spirit Dash (`EnemyController`) and a Yokai boss dash hit.
  - While a dash chain is going, the Spirit Dash afterimages and speed streaks change to its current colour (`TalismanFX`).
  - Getting hurt ends both chains.
- **Tested:** dash stages 2 and 4 on screen next to a stomp stage 3 (screenshots). They were triggered directly, not by dashing through enemies with the talisman.

### Luma
The Luma connector only imports finished "Claude Motion" videos into your Luma library. It can't generate animations. Game animations are still made as pixel-art frame sheets (Python) or in Unity.

### The AI Assistant "Process exited unexpectedly" error
- **Cause:** a bug in `com.unity.ai.assistant` 2.20.0-pre.2, which is already the latest version.
  - After a domain reload (entering Play mode or a recompile), the editor sometimes starts a second relay but keeps watching the old one.
  - The old one has no client, so it shuts itself down after 180 s. The editor reads that as a crash, logs `Running → Failed … code=-1`, drops the working connection, and never restarts it.
  - The whole sequence is in `Logs/relay.txt` and `Logs/traces.jsonl`.
- **What was done:** `Assets/Editor/RelayWatchdog.cs` (editor only). When the relay goes Failed, it waits 3 s and starts a fresh one, at most 3 times in 5 minutes. If Unity changes the package it does nothing. Delete it once Unity fixes the bug.
- **Tested:** I shut the relay down on purpose. The watchdog logged "Restarting it (1/3)" and the relay was Running again within about 7 s. Its messages appear in `~/Library/Logs/Unity/Editor.log`.
- This relay is only for Unity's AI Assistant window. It doesn't affect the game or the Claude Code Unity tools.
- **Not a bug:** a `NullReferenceException` at `PlayerController.cs:82` at 19:50 came from adding a script while the game was in Play mode (Unity recompiled mid-play). Restarting Play clears it.

### Loading system
- **What was done** (`LevelLoader.cs`, plus `CameraController.SnapToPlayer` / `IsOnPlayer`):
  - Every scene change goes through `LevelLoader.Load`, so no `SceneManager.LoadScene` calls are left in Scripts. That covers the menus, map, levels, restart, Continue and arcade.
  - A plain loading screen in the UI style: ink background, Cream "LOADING" and Yellow dots at the bottom right.
  - A level is frozen (`timeScale 0`) until every Start has run and the camera has been snapped onto Harvey and checked as settled. Then the screen fades, the game unfreezes, `LevelReady` fires and the usual iris opens.
  - No pausing while loading.
  - Pressing Play on a level in the editor goes through the same wait.
- **Tested:**
  - Reloading Z1A1Tomb: the camera jumped from its stored scene position straight onto Harvey while hidden, and didn't move after the reveal.
  - The level started about 1.9 s after the load began.
  - The loading screen was screenshotted.

### Tomb poison (Z1A1Tomb, the four `PosionLake` objects)
- **Look:** `PoisonSurface.shader` (material `Assets/Resources/FX/PoisonSurface.mat`), built like the water shader:
  - thick, slow waves with a glowing rim
  - purple fading to near-black, with slow toxic swirls
  - pixel bubbles rising (mostly pale purple, some sickly green) that swell the surface before they pop
  - `PoisonLiquid.cs` adds bubble pops and rising fumes along the surface, and Harvey's splashes and drips turn purple in poison.
- **Rules** (`PoisonLiquid.cs`):
  - Without the Water Breathing talisman, touching the liquid is instant death, even with lives left or hit invincibility, and also if the talisman runs out mid-swim.
  - With the talisman, Harvey swims with the water's floaty movement and uses no breath. Jump is a swim stroke upwards, because three of the four pits are bottomless shafts.
- **Level changes:** two kill planes sat inside the liquid and would have killed a talisman swimmer. They were moved under the lakes: `KillPlane` y −12.9 → −15, `KillPlane (1)` y −20.3 → −30.5.
- **Tested:**
  - Without the talisman, Harvey died the moment he touched the surface of pit 1 and respawned.
  - With Water Breathing he stayed alive in the deep shaft with all 3 lives.
  - Screenshots of the pit show the waves, swirls and bubbles.

### Files from these follow-ups
- **New:**
  - `Assets/Scripts/LevelLoader.cs`, `Assets/Scripts/PoisonLiquid.cs`
  - `Assets/Shaders/PoisonSurface.shader`, `Assets/Resources/FX/PoisonSurface.mat`
  - `Assets/Editor/RelayWatchdog.cs`
- **Changed:**
  - `GameSession`, `NextLevel`, `TownExit`, `StompCombo`, `EnemyController`, `YokaiBoss`, `TalismanFX`, `PlayerWater`
  - `CameraController`, `LevelComplete`, `MenueControl`, `MenueController`, `PauseMenuStyle`, `ZoneSelectMap`
  - `Tools/Art/zone_map.py` and the `Node_*` icons
- **Scenes saved:** `Z2A2Sandy` (the fish), `Z1A1Tomb` (poison lakes and kill planes).

### Still open from these follow-ups
- **Swim stroke in poison:** not tested, because the test tools can't press Jump. Try it in the deep shafts. The upward speed is `swimStroke` 4.5 on each lake.
- **Dash combo:** not yet seen with real Spirit Dash kills.
- **Poison talisman:** Water Breathing is the one that allows swimming in poison. If a separate poison talisman was meant, it's a one-line change in `PoisonLiquid`.
- **Tomb lighting:** the tomb darkness only lights circles, so the poison doesn't glow through the dark. A lit edge along the pools would need a TombDarkness change.
- **Loading screen:** only one level route was tested end to end (reloading Z1A1Tomb). The menu → map → level path and Continue use the same code but weren't watched frame by frame.

---

## Late night (18 October): new dragon frames, octopus walk, zone music on the map

### Dragon ride → DragonRideTwo
- **What was done:**
  - `Tools/Art/dragon_ride.py` now cleans `DragonRideTwo.png` (12 frames of 72x46, the same frame size as the first sheet). The delivered file is kept untouched in `Tools/Art/source/DragonRideTwo_original.png`.
    - The watermark (semi-transparent, last frame, bottom right) is removed.
    - 97 stray colours are snapped onto one 27-colour palette.
    - The frames are steadied vertically (they moved by up to 3 px), with a 1 px wingbeat bob spread over the 12 frames.
    - The pivot still puts Harvey's face where it was on the old still mount (42.7, 18.5 px).
  - The clean-up steps are now a reusable `clean()` function.
  - `DragonRideSetup` (Tools/HNL/Set Up Dragon Ride) slices the new sheet and drives the existing `DragonRide.anim` at 12 fps (a 1 s loop). The `HarveyDragonMount` in the Player prefab starts on `DragonRideTwo_0`.
- **Fixed on the way:** the old clip kept its 10 fps sample rate, which snapped the 12 fps keys onto a 0.1 s grid. The extra closing key also showed frame 0 twice at the loop. Both setup scripts now set `clip.frameRate` and add no closing key, because Unity already holds the last key for one frame.
- **Tested:** the menu ran without errors. The clip has 12 keys, runs 12 fps for 1.000 s, and the mount sprite is 72x46 at 16 PPU. The ride hasn't been watched in Play mode yet.
- The old `DragonRide.png` is no longer used. It can be deleted in Unity.

### Tomb octopus walk (the "Ghoul" enemy in Z1A4Tomb and Z1A5Tomb)
- **What was done:**
  - The user's `MrOctopus.png` was in Downloads. It's kept in `Tools/Art/source/MrOctopus_original.png`.
  - `Tools/Art/mr_octopus.py` cleans it with the same steps (watermark, palette of 10 colours, specks) into `Assets/Sprites/Enemies/TempleTiles/OctoGhoul/MrOctopus.png`. The frames already stood on one line, so they aren't moved.
  - `Assets/Editor/MrOctopusSetup.cs` (Tools/HNL/Set Up Mr Octopus):
    - slices 12 frames of 46x35 at 16 PPU, point filter, with a centre pivot. The height and facing match the old still `WitchHatOctoOne`, so the octopus stays over its collider.
    - builds `OctoGhoulWalk.anim` (10 fps, 1.2 s loop) and adds an `OctoGhoulWalk` state as the default in the octopus's `Enemy.controller`. The one-frame `OctoGhoulIdle` state is kept.
    - turns off exit time on the Any State → `OctoGhoulDeathAnim` transition. With the longer walk loop, a stomped octopus could otherwise keep walking for up to a second before dying.
- **Tested:** the menu ran, the clip has 12 keys at 10 fps, the default state is `OctoGhoulWalk`, and the console showed no errors. It hasn't been watched in a level yet.
- **Worth checking:** the new frames are 46 px wide (the old sprite was 34), so the tentacles spread wider. In the Scene view the scenes and the `Ghoul (6)` prefab still show the old still sprite until Play mode.

### Zone select music
- **What was done:**
  - `AudioManager.PlayMusic(clip, fadeTime)` crossfades through a second music source, which it creates when first needed. The same clip already playing just carries on. `Start()` leaves the music alone if a script has already picked a track.
  - `ZoneSelectMap` has a **Zone music** list. Each entry is a name, scene-name prefixes and a clip.
    - The map starts straight on the track for the node Harvey is standing on.
    - Walking into another zone crossfades over 1.2 s (`musicFadeTime`) once Harvey is halfway along the path.
    - An entry with no clip plays the map's own music (`AudioManager.backgroundMusic`).
  - The zone list lives in `ZoneSelectSceneBuilder.ZoneTracks`, so a full rebuild keeps it. **Tools/HNL/Set Up Zone Select Music** applies it to the scene without a rebuild and sets each track to stream, like the rest of the music.

  | Zone | Prefixes | Track |
  |---|---|---|
  | The Old Tomb | Z1 | `TombZoneSelectSoundtrack.wav` |
  | Sandy Dunes | Z2 | `SandZoneSelectSoundtrack.wav` (not made yet: map music for now) |
  | Japan (Shrine Road, Snow Temple village and boss) | Z3 | `JapanZoneSelectSoundTrack.wav` |
  | Grassy Meadow + Journey's End | Z4, Win | `GrassZoneSelectSoundtrack.wav` (not made yet: map music for now) |

- **The two new tracks:** the files are different from the existing music. Their loudness is already in line with the other tracks (about −14 to −16 dBFS RMS), so their levels weren't changed.
- **Sand and Grass music wasn't made.** The ElevenLabs connector here is the restricted one: sound effects and speech only, with no music. To generate the tracks, add the full connector (`https://api.elevenlabs.io/v1/mcp`) as a custom connector. Then save the files under the names above and run Tools/HNL/Set Up Zone Select Music.
- **Tested:** it compiles and the list is saved in `ZoneSelect.unity` (Tomb and Japan clips assigned, Sand and Grass empty). Walking the map with the music changing **hasn't been play-tested**.
- **Problem hit:** the script edits recompiled while the user was in Play mode in Z1A1Tomb, which caused the known `PlayerController.cs:82` NullReferenceException. Play mode was stopped. Z1A1Tomb had unsaved edit-mode changes, so ZoneSelect was opened alongside it, saved and closed again, and the Tomb changes were left as they were.

### Files from this section
- **New:**
  - `Tools/Art/mr_octopus.py`
  - `Tools/Art/source/DragonRideTwo_original.png`, `Tools/Art/source/MrOctopus_original.png`
  - `Assets/Editor/MrOctopusSetup.cs`
  - `Assets/Sprites/Enemies/TempleTiles/OctoGhoul/MrOctopus.png`, `.../OctoGhoulAnim/OctoGhoulWalk.anim`
- **Changed:**
  - `Tools/Art/dragon_ride.py`, `Tools/Art/dragon_ride_layout.txt`
  - `Assets/Editor/DragonRideSetup.cs`, `Assets/Editor/ZoneSelectSceneBuilder.cs`
  - `Assets/Scripts/AudioManager.cs`, `Assets/Scripts/ZoneSelectMap.cs`
  - `DragonRideTwo.png` (cleaned), `DragonRide.anim`, `Player.prefab`, the octopus `Enemy.controller`, `ZoneSelect.unity`, and the two new music imports

### Still open from this section
- Make and add the Sandy Dunes and Grassy Meadow map tracks (this needs the full ElevenLabs connector, or tracks from anywhere else).
- Walk the whole map in Play mode and listen to every crossfade.
- Watch the dragon ride and the octopus walk in a level (the octopus is in Z1A4/Z1A5).
- Optional clean-up in Unity: delete the unused `DragonRide.png`.

---

## After midnight (19 October): samurai clash, samurai afterimages, Z3A2 ghost bounce

### Samurai clash
- **What was done** (`SamuraiSlash.cs`):
  - If Harvey's Spirit Dash meets a samurai monkey **head-on** during its wind-up or slash, the two attacks cancel. Head-on means Harvey is in front of it and facing or moving toward it.
    - Harvey's dash ends (new `PowerUpManager.EndTrait`). He is knocked back without losing a life, without the hurt animation and without the hurt flicker.
    - The samurai staggers back about 0.6 units along its path, freezes on its frame for 0.4 s, then goes back to patrolling.
    - Sparks, a white flash and a clang. The clang is the slash sound pitched up through the new `AudioManager.PlaySfxOneShotPitched`; the `clashSound` slot is empty for a proper one.
  - Dashing into it **from behind**, or during its recovery, still defeats it as before.
  - `SamuraiSlash.ClashProtected` (a short grace after a clash) stops the knockback from counting as a hit in `HurtPlayer`, `playerBodyDetectionArea` and `PlayerFX`. `EnemyController` asks `TryClash()` before a dash defeat.
- **Tested in Z3A2 Play mode** (logged frame by frame):
  - Head-on clash: lives stayed at 3, the samurai survived and was back on patrol about 0.6 s later.
  - Dash from behind: the samurai died the frame they touched, with no life lost.
  - Without a dash, a slash still costs a life.
- Only tested with Harvey standing still and facing the samurai; a running head-on dash in real play is worth a try.

### Samurai afterimages
- Harvey's Spirit Dash afterimage code moved into a shared component, `AfterimageTrail.cs`. `TalismanFX` (Harvey) and `SamuraiSlash` both use it, so they look the same: tinted copies that fade in two steps over 0.22 s.
- The samurai leaves a red-tinted copy every 0.035 s during its dash.
- **Tested:** screenshot of the trail. Harvey's own afterimages after the move to the shared component weren't watched (the test tools couldn't make him run).

### Z3A2 ghost bounce
> **Reverted 19 October:** the ghosts bounce at 9.5 again, and an off-screen marker points to the next ghost instead. See the 19 October section.

- **Cause:** `PlayerController` aims the camera 8 units above Harvey whenever he rises at 8 or faster. The ghosts bounced him at 9.5, so every stomp swung the view up and the next ghost dropped off screen.
- **Fix:** `bounceSpeed` 6.5 on the 18 row ghosts (`SGhostBounce` to `SGhostBounce (17)`), as **scene overrides in Z3A2 only**.
  - The peak drops from about 4 units to about 1.8.
  - About 1.2 s in the air, still more than enough for the widest gap.
  - The prefab and the vertical climb ghosts (`SGBvertical`) are unchanged.
- **Tested:** at the top of the new bounce, the whole row ahead is on screen (screenshot).
- **Z3A2Japan is not saved**: the change sits in the open scene alongside the user's own unsaved edits.

---

## After midnight (19 October): text dialogue system, Japan village

### What it does
- Walk up to a villager in Z3JapanVillage on the ground: the prompt says "UP / E: TALK TO HANA" (the same keys as the shop). Up / E opens a text box at the bottom of the screen.
- **Controls:** Cross, Enter or E shows the rest of the line, then moves on. Up/Down picks a choice. Circle or Esc leaves. Space and Triangle are Jump in the old input system, so they're never used for dialogue.
- **While talking** the game pauses like the shop. Leaving doesn't open the pause menu, and Harvey's velocity is cleared so no jump carries over.
- **The box** is in the HNL UI style: the HUD plate, the name in Yellow, the line typed out in Cream, Muted hints, and choices as plain text with the paw.
- **Who answers:** only the nearest villager. A shop counter Harvey is standing at takes priority (`VillageShop.PlayerAtCounter`).

### Writing dialogue
- Plain-text files in `Assets/Dialogue` (`JapanVillage.txt` for now):
  - `== node` starts a section
  - `Name: line` is a line; a line without a name continues the same speaker; `> line` is narration
  - `* choice -> node` is a choice
  - `-> node`, `-> random: a, b`, `-> cycle: a, b` (the next one each visit), `-> end`
  - `{name}` becomes the villager's name, `{player}` becomes Harvey
- The full format notes are at the top of `DialogueScript.cs`. **Tools/HNL/Check Dialogue Files** lists mistakes with line numbers.
- **One catch:** a line that starts with a short word and a colon (like `Careful: …`) is read as a speaker's name. Start it with `>` or put the speaker first.

### The villagers (Tools/HNL/Set Up Village Villagers, saved in Z3JapanVillage)
| Villager | Start node | Notes |
|---|---|---|
| Hana (shrine maiden, by the shop) | `hana` | first meeting, then friendlier lines and small talk |
| Genji (merchant, beside the shop) | `genji` | random gameplay tips, "another!" |
| Kuro (guard) | `kuro` | the yokai at the Snow Temple |
| Sakura, Yuki, Tetsu, Mochi, Ren, Old Ume | `villager` | a greeting, then random topics: weather, food, rumours, spirits, lanterns, home |

### Files
- **New:** `Assets/Scripts/DialogueScript.cs` (parser), `DialogueBox.cs` (box and runner), `Villager.cs`, `Assets/Editor/VillagerSetup.cs`, `Assets/Dialogue/JapanVillage.txt`
- **Changed:** `VillageShop.cs` (`PlayerAtCounter`), `MenueControl.cs` (no pause toggle while talking), `Z3JapanVillage.unity` (the Villager components)

### Tested (Play mode, village loaded inside Play from Z3A2 so its unsaved edits were left alone)
- Kuro: the prompt showed, Up started the conversation (game paused), Cross completed a line and moved on to the choices.
- Circle left the conversation: the game resumed, the pause menu stayed closed, Harvey didn't move.
- Picking a `-> end` choice closed it cleanly.
- Hana on a later visit (the `cycle`) went into small talk. The choice plate with the paw, then the chosen answer typing out (screenshots).
- Nearest villager: standing between Yuki and Sakura, the closer one (Sakura) answered.

### Testing notes (not game bugs)
- Play mode freezes while the editor is in the background; the test harness forces frames with `EditorApplication.QueuePlayerLoopUpdate()`.
- The keyboard ignores simulated keys without Game view focus, so the tests used a virtual gamepad.
- The AI Assistant relay error tripped Error Pause again mid-test; the harness unpauses it.

### Still open
- Standing at the shop counter with a villager in reach (the counter should win) wasn't tested on the counter itself.
- More dialogue: other zones or story moments just need a new file in `Assets/Dialogue` and a Villager component.
- Save **Z3A2Japan** (the ghost bounce plus the user's own edits). Done: it was saved and committed in `8b67cc1`. The bounce change was reverted on 19 October.

---

## 19 October (a new chat): shared combo, sprite grounding, title screen, Z3A2 ghost stomps, the Sumaria boss

### What was asked
One brief in five parts:
1. **Stomp and dash combo:** one shared progression (dash to stage 3, then stomp, and it's stage 4), the current stage's look on both actions, and much stronger visuals that stay on for the whole combo window, even when Harvey stands still.
2. **Sprite grounding audit:** characters (the samurai monkey above all) and props that float above the ground although their colliders touch it. Check every zone and every animation frame, and leave colliders alone.
3. **Main menu:** match two reference screenshots (a bubbly title, and a blue sky full of floating islands) in the game's pixel style.
4. **Z3A2 ghost stomps:** undo the lowered bounce, and instead point to the next ghost when it is off screen.
5. **Japan boss, Sumaria:** a full three-stage fight using the new boss sprites and slash sheet (`Assets/Sprites/Enemies/JapanEnemies/JapanBoss`).

The chat started from a committed tree (`8b67cc1 gg`). **Nothing from this section is committed yet.**

### 1. One combo for stomps and dashes
- **What was done** (`StompCombo.cs`, `TalismanFX.cs`, `AfterimageTrail.cs`, `SpriteAura.cs`, `PlayerFX.cs`):
  - Stomps and dash defeats feed **one** chain. Any hit within 5 s of the last, of either kind, moves the combo up a stage.
  - The window running out ends it, and so does **getting hurt** (from anything that costs a life). The first pass took the brief's "only the window" literally and let the combo survive hurts; that was put back at your request the same day. Dying or respawning resets the level, so that ends it too.
  - One set of stage colours for both actions: yellow at stage 1, orange at 2, hot pink at 3, violet at 4, and rainbow from stage 5 (it cycles every colour).
  - Cyan was dropped from the stages, so a combo dash never looks like a plain Spirit Dash.
  - **For the whole window, whatever Harvey is doing:**
    - A solid outline in the stage colour. It uses a new solid glow material (`Resources/FX/SpriteGlowSolid.mat`), so it reads on bright skies too.
    - From stage 2, a soft outer glow that pulses faster each stage.
    - Motes swirling round him: two at stage 1, one more each stage, faster and wider; a second counter-swirl from stage 4.
    - Sparks rising off him, more each stage, with stars at stage 5.
    - The outline blinks in the last second as a warning.
  - **Every hit, stomp or dash, plays the same effects:**
    - a flash on Harvey in the stage colour, rings and impact stars;
    - falling sparks from stage 2, speed lines from stage 4, confetti at stage 5;
    - a dash hit also throws speed lines along the dash.
  - **Spirit Dash during a combo:**
    - Each afterimage takes the next stage colour, giving a rainbow trail at stage 5. They come denser and stronger as the stage rises.
    - The speed streaks use the stage colours, with sparkles from stage 3.
    - The combo's outline shows over the talisman's cyan glow.
- **Tested:** real ghost stomps in Z3A2 built the combo to 18, so the stage 5 look was on screen. Close-ups were taken at stages 1, 3 and 5.
- **Tested after hurts were put back** (Z1A1, in Play mode, with a throwaway save):
  - an enemy hit ended a stage 2 combo and cost one life, and the next stomp started again at stage 1;
  - touching an enemy while invincible cost nothing and left the combo alone;
  - a combo built by a dash defeat also ended on a hurt.

### 2. Sprite grounding audit
- **How it was checked:** every enemy in every Z level. For each animation frame, the lowest solid pixel was measured against the collider and the ground under it.
  - The final checks were done in **Play mode**. In Edit mode, Z3A1 and Z3A2 draw both worlds' ground at once: the spirit world's snow hides the living monkeys' legs, so edit-mode pictures mislead.
- **Samurai monkey (the main culprit):**
  - Walk frames 3 and 4 have the whole body drawn 1 and 2 pixels higher than the other frames, but all the frames shared one pivot. So it floated mid-stride, and the planted frames sank about a pixel.
  - New tool **Tools/HNL/Ground Sprite Frames** (`SpriteGrounding.cs`) sets each frame's pivot from its own lowest solid pixel. It covers the walk, slash and sword-draw sheets. Colliders, positions and animation clips are untouched.
  - **Tested in Play mode:** every monkey in Z3A1 and Z3A2 now moves its feet at most 0.2 px across the walk (it was about 1.5 px), and stands within about 1 px of the ground.
- **Also lowered onto the floor, with each collider kept exactly where it was** (its offset raised by the same amount):
  - the tomb octopi and a ghoul in Z1A1 (2–3 px up);
  - the Z1A4 and Z1A5 octopi (7 and 9 px up);
  - one Z4A7 placeholder box (2 px up).
- **Props:** two palm trees in Z2A2 were 1.6 px above the sand; they were lowered 0.1.
- **Left alone:**
  - Ghosts, sharks and the hooded spirit yokai hover by design.
  - Patrols over uneven floors or gaps can't take one offset.
  - Villagers, yokai guards, sand fins and the three zone bosses were already grounded.

### 3. Main menu: title and sky-islands art
- **What was done:**
  - New `Tools/Art/menu_sky.py` draws the art; `main_menu.py`'s `main()` now runs it.
  - The backdrop (`MenuBackdrop.png`) has sky bands, far clouds, hazy far islands and a pagoda.
  - Separate layers sit over it: a wooded island, a small island, a torii island with a cherry tree, the cloud sea, Harvey's island and three drifting clouds. Their positions are in `Tools/Art/menu_sky_layout.txt`.
  - **Title** (`MenuTitle.png`, 197x70):
    - chunky rounded cream letters with a thick navy outline and a heavier drop, with a slight hand-drawn wobble;
    - a navy cat head in the A (ears, cream eyes, pink nose);
    - a cat's tail swooping under NINE LIVES and curling round a paw-print badge.
  - **Motion** (`MainMenuScene.cs`): the islands bob a pixel at their own pace and the clouds drift across and wrap. Everything snaps to whole pixels.
  - Harvey was moved up 2 px to stand on his island.
  - **Labels:** cream (yellow when selected) with a 1-pixel navy drop (`PixelTextOutline.cs`), which also goes on the credit line. A full outline was tried two ways (copies of the text, and TMP's own outline). At this font size PerfectDOSVGA437's strokes and gaps are one pixel wide, so any outline filled each letter into a block.
- **Tested:** Play-mode screenshots. The islands bobbed and the clouds moved between shots, and the labels are crisp.

### 4. Z3A2 ghost stomps
- **What was done:**
  - The 18 row ghosts bounce at **9.5** again; the 6.5 scene overrides were removed.
  - A **pointer to the next ghost** (`StompChainMarker.cs`, the `GhostRowStompMarker` object in Z3A2):
    - While Harvey is in the air over the row, the target is the nearest live ghost ahead of him.
    - When that ghost is off screen, a yellow pixel pointer sits at the screen edge, right where the ghost is, nudging toward it.
    - It goes as soon as the ghost is in view or stomped (the next ghost becomes the target), or when Harvey lands.
    - The arrow art comes from `Tools/UI/stomp_marker.py`: straight and diagonal arrows turned in 90° steps, so it stays crisp in all eight directions.
  - **Bug found while testing, and fixed:**
    - At full bounce speed, Harvey was sometimes hurt landing squarely on a ghost. His stomp box and body could enter the ghost in the same physics step, and Unity runs the two triggers in no fixed order.
    - Now `HurtPlayer` and the knockback ignore an enemy the stomp box is landing on from above (`StompEnemy.StompingOnto`), so the stomp always wins. Stomps themselves are unchanged.
- **Tested** (bots in Play mode):
  - The pointer was never hidden while the next ghost was off screen, and never shown while it was visible.
  - **Steering like a player** (staying a little short while rising, moving over the ghost while falling): 8 of 8 runs crossed the whole row with no hurts. Three runs hopped one ghost but never fell. Before the fix, the same line was hurt in 3 of 6 runs.
  - **Steering straight at the ghost the whole time** can still clip a bobbing ghost's side (4 of 6 runs). A player wouldn't fly into its side, but it's worth knowing.

### 5. Sumaria, the Japan boss (Z3BossBattle)
- **The art** (`Tools/Art/samurai_boss.py`; the supplied files are untouched):
  - Keys out the idle sprite's grey background and drops the soft edges.
  - Snaps both sheets onto one 12-colour palette taken from the slash frames.
  - Scales the idle down to match: it was drawn about 35% bigger.
  - Lines every frame up on the trousers and feet.
  - **Output:**
    - `SamuraiBoss_Sheet.png`, six frames: idle, ready (the charge), sword raised, the slash with its crimson arc, follow-through, and a kneeling frame (built from the ready stance) for the daze;
    - `SamuraiBoss_SlashWave.png`: the arc cut out of the slash frame, used as the projectile;
    - a falling blade, the floor mark for it, and the danger line and band.
- **The fight** (`SamuraiBoss.cs`): three lives with pips over his head, one stage per life.

| Stage | Attacks |
|---|---|
| 1 | **Charging dash slash.** He leaps to the wall away from Harvey and charges: a red glow, sparks drawn in, and a dashed danger line and band across the floor that blink faster. A 0.22 s glint is the jump cue. Then he dashes the whole arena with the slash frame and crimson afterimages. Only the blade's low band (0.6 above the floor) cuts, so jumping it dodges. A **miss** carries him into the far wall: a shake, stone chips, then he drops to one knee dazed, with stars and a cyan flash, and can be stomped or dashed. A **hit** and he skids to a stop with no opening. **Attacking him while he charges** is countered: Harvey is thrown well back and loses a life. |
| 2 | Stage 1, plus a **teleport slash**: he vanishes and reappears behind Harvey with a glint and a red tell, cuts through with a short afterimage dash, then stumbles to a knee (a short opening). Plus **slash waves**: crescents low (jump) and high (stay down), the last one thrown from the far wall after he blinks across. |
| 3 | Stage 2, plus **blade rain**: blades drop on Harvey's spot. The first lands exactly on him, the rest around him, each marked on the floor first and blinking faster as it nears. |

- **Stage tuning** (inspector, `Stages`):
  - charge 1.35 / 1.1 / 0.95 s; dash 18 / 20 / 22 units per second;
  - daze 2.8 / 2.3 / 1.9 s;
  - teleport tell 0.6 / 0.5 / 0.42 s; opening after it 1.0 / 0.85 / 0.7 s;
  - waves 3 / 3 / 4; blades 6 in stage 3;
  - glow: none, crimson, then deep crimson.
- **Rules, like the other bosses:**
  - He can only be hurt in an opening, by a stomp or Spirit Dash. Touching him at other times hurts Harvey.
  - Damage uses the usual lives, knockback and flash. His hits count toward the combo.
  - The fight resets when Harvey respawns, and the boss treasure appears on defeat.
  - For half a second after an opening, while he gets up, touching him doesn't hurt.
- **Size:** he stands about 1.8 units tall and kneels at about 1.1, within reach of a normal jump's stomp.
- **Scene setup:**
  - **Tools/HNL/Set Up Samurai Boss** (`SamuraiBossSetup.cs`) imports the art and builds the rig in Z3BossBattle. It's safe to run again.
  - The old YokaiBoss object stays in Z3BossBattle, **switched off**: `DraftLevelBuilder` copies it as the template for the other zones' boss arenas, and the builder now switches its copies back on.
  - `BossHazard` can now belong to either boss.
- **Tested** (a bot playing from the main menu into Z3BossBattle):
  - All three stages to defeat, and the treasure appeared.
  - The counter threw Harvey back and cost a life.
  - Jumping as the dash starts dodged every cut. The teleport cut was dodged; the waves and blade rain fired.
  - The final run dodged 5 of 5 cuts and landed 3 hits; Harvey lost 1 life, to a projectile.
- **Tuning changes from those tests:**
  - The first version was too hard to dodge: a 0.85 band and a 0.08 s cue, and the bot was hit by every dash. It's now 0.6 and 0.22 s.
  - Harvey was twice hurt by Sumaria standing up into a stomp jump, which led to the half-second grace.

### Other fixes
- **Boss arena camera** (`LevelLoader.cs`): the loading screen snapped the camera onto Harvey even when the follow camera was switched off. So all four boss arenas lost their fixed view whenever a level loaded normally, since the loading screen arrived on 18 October. A switched-off follow camera now stays put.
- **`UIShot`:**
  - It now keeps the pixel-perfect camera's view size; boss arena captures had been mis-framed.
  - It rebuilds TextMeshPro text for the capture; text had looked soft.

### Files
- **New scripts:** `SamuraiBoss.cs`, `StompChainMarker.cs`, `PixelTextOutline.cs`.
- **New editor tools:** `SamuraiBossSetup.cs`, `SpriteGrounding.cs`.
- **New material:** `Resources/FX/SpriteGlowSolid.mat`.
- **New art scripts:** `Tools/Art/menu_sky.py` (+ `menu_sky_layout.txt`), `Tools/Art/samurai_boss.py` (+ `samurai_boss_layout.txt`), `Tools/UI/stomp_marker.py`.
- **New sprites:**
  - `Sprites/UI/MainMenu/MenuIsland_*`, `MenuCloud_*`, `MenuCloudBank`
  - `Sprites/UI/Markers/StompMarker_*`
  - `JapanBoss/SamuraiBoss_*`
- **Changed scripts:** `StompCombo`, `TalismanFX`, `AfterimageTrail`, `SpriteAura`, `PlayerFX`, `StompEnemy`, `HurtPlayer`, `playerBodyDetectionArea`, `BossHazard`, `YokaiBoss`, `EnemyController` (comment only), `MainMenuScene`, `LevelLoader`.
- **Changed editor scripts:** `DraftLevelBuilder`, `UIShot`.
- **Other changes:**
  - `SpriteGlow.shader` (a blend setting; the default is unchanged)
  - `Tools/Art/main_menu.py`
  - the monkey sheets' pivots (`JSEAnim*.png.meta`)
  - `MenuBackdrop.png`, `MenuTitle.png`
- **Changed scenes:** MainMenue, Z3A2Japan, Z3BossBattle, Z1A1Tomb, Z1A4Tomb, Z1A5Tomb, Z2A2Sandy and Z4A7GrassGreen.

### Testing notes (not game bugs)
- **The disk was nearly full** mid-session: down to about 90 MB free, later back to a few GB. The big items are `~/Library/Unity`, `~/Library/Developer`, `~/Library/Caches` and the project's `Recordings` folder (1.8 GB). Nothing was deleted.
- Script recompiles sometimes waited about 10 minutes for the editor to take focus. An asset refresh from the test tools started them.
- The test bots used a throwaway in-memory save store, so real saves weren't touched.

### Still open
- **Shared combo with real Spirit Dash kills:** dash defeats use the same code, but weren't watched in this session.
- **Play Sumaria yourself:** the charge, cue, dash speed and openings were tuned with a bot. The numbers are in the inspector on `SamuraiBoss`.
- **Sumaria's sounds:** he uses the existing samurai slash, boss slam and defeat sounds. A charge-up sound and a clang of his own would help.
- **His name:** the game doesn't show it anywhere (there's no boss name plate).
- **Hovering enemies:** the ones left hovering on purpose (shadow ghouls over uneven floors, spirit yokai) can be moved if any should stand.
- **Python cache:** `Tools/Art/__pycache__/menu_sky.cpython-311.pyc` is a generated file (a `.gitignore` line for `__pycache__` would keep these out of git).
- **Nothing from 19 October is committed.**
