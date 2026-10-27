// QA catalogue for Harvey's Nine Lives (2026-10-27): what the game is made of, for the Playtest agent and the
// dashboard's QA records. It is versioned with the game so a new script, scene or mechanic is added here in the same
// commit. The records themselves (sessions, results, bugs, regressions) live in the dashboard's QA store; `qa sync`
// (HNLDashboard/qa/cli.mjs) merges this file into it and never deletes anything:
//   - SYSTEMS   gameplay systems and the files that make them up. Dependencies between systems are not typed here:
//               qa sync derives them from the code (a script naming another script's class) and adds `dependsOn`
//   - zoneOf / kindOf   how a scene in Assets/Scenes becomes a level record
//   - LEVEL_TESTS       the test cases every level of a kind gets (the required ones define "fully tested")
//   - MECHANIC_TESTS    tests of one mechanic or system, run in a named level
//
// Test methods: play-human (a person played it), play-scripted (the agent drove Play mode through the Unity MCP and
// QARunner), automated (Unity Test Framework), editor-check (scene or asset inspected in the editor, nothing played),
// code-review (source read only). A test lists the methods that count for it; a code review never passes a test
// that needs play.

export const CATALOGUE_VERSION = "2026-10-27";

export const ZONES = {
  Z1: "Old Tomb",
  Z2: "Sandy Dunes",
  Z3: "Japan",
  Z4: "Grassy Green",
};

/** Zone of a scene file name, or "" for menus and tools. */
export function zoneOf(name) {
  const m = /^Z(\d)/.exec(name);
  return m ? ZONES[`Z${m[1]}`] || "" : name.startsWith("Greybox_GrassyGreen") ? ZONES.Z4 : "";
}

/** act | boss | village | menu | tutorial | prototype | sandbox */
export function kindOf(name) {
  if (/^Z\dBoss/.test(name)) return "boss";
  if (/^Z\d.*Village$/.test(name)) return "village";
  if (/^Z\dA\d+/.test(name)) return "act";
  if (/^Tutorial_/.test(name)) return "tutorial";
  if (/^Greybox_/.test(name)) return "prototype";
  if (["MainMenue", "ZoneSelect", "Win"].includes(name)) return "menu";
  return "sandbox";
}

/** Kinds that count toward the game's level coverage. Menus, prototypes and sandboxes are tracked but not counted. */
export const GAMEPLAY_KINDS = ["act", "boss", "village", "tutorial"];

// ---------------------------------------------------------------- systems
// `files` are script names in Assets/Scripts (a script can serve two systems); `paths` are path prefixes for other
// assets. Editor tools (Assets/Editor) map through the scripts they set up, not here.
export const SYSTEMS = [
  {
    id: "SYS-MOVEMENT", name: "Player movement and input",
    description: "Run, jump, coyote time, Super Jump, Double Jump, ground detection, collisions, input.",
    files: ["PlayerController", "PlayerControls", "CharacterBehaviour", "playerBodyDetectionArea", "UIInputModuleFix"],
    paths: ["Assets/Animations/Player", "ProjectSettings/InputManager.asset", "ProjectSettings/Physics2DSettings.asset"],
  },
  {
    id: "SYS-BOUNCE", name: "Bounce pads, clouds and mushrooms",
    description: "Jump boxes, bouncy clouds, floating and flying mushrooms.",
    files: ["JumpBox", "jumpBoxHori", "BouncyCloud", "MushroomBounce", "CloudPairParallax"],
    paths: [],
  },
  {
    id: "SYS-COMBAT", name: "Stomp, Spirit Dash and combo",
    description: "Stomp damage and bounce, dash hits, the shared five-stage combo, the samurai dash clash.",
    files: ["StompEnemy", "StompCombo", "StompChainMarker", "ClashCard", "AfterimageTrail"],
    paths: [],
  },
  {
    id: "SYS-ENEMIES", name: "Enemies",
    description: "Enemy patrol, detection, contact damage, toughness by zone, death; Spirit Fox, ghouls, samurai monkeys.",
    files: ["EnemyController", "GhostEnemy", "SamuraiSlash", "SpiritFoxCharge", "DetectPlayer", "MoveToObject"],
    paths: ["Assets/Sprites/Enemies", "Assets/Prefabs/Enemies"],
  },
  {
    id: "SYS-HAZARDS", name: "Hazards and traps",
    description: "Spikes and spike rhythms, boulder trap, poison pools, moving obstacles, anything that hurts on touch.",
    files: ["HurtPlayer", "SpikeTrap", "SpikeRhythm", "BoulderTrap", "RollingBoulder", "PoisonLiquid", "MovingObstacles", "GravitonController", "SpinObj"],
    paths: [],
  },
  {
    id: "SYS-RESPAWN", name: "Health, death, checkpoints and respawn",
    description: "Three health segments, knockback, death, checkpoint respawn hold, objects that reset on respawn.",
    files: ["LevelManager", "CheckPoint", "HealthUIManager", "ResetOnRespawn"],
    paths: [],
  },
  {
    id: "SYS-CAMERA", name: "Camera and parallax",
    description: "Follow camera, respawn glide, screen shake, level-load camera placement, parallax layers.",
    files: ["CameraController", "LevelLoader", "ParallaxBackGround", "MovingTiles"],
    paths: [],
  },
  {
    id: "SYS-WORLDSWITCH", name: "Living and spirit worlds",
    description: "World switching and the Torii gates that show the other world.",
    files: ["changeWorld", "ToriGateAppearanceManager", "ToriGateAppearanceManagerTwo"],
    paths: [],
  },
  {
    id: "SYS-WATER", name: "Water and breathing",
    description: "Swimming, breath, Water Breathing visuals.",
    files: ["PlayerWater", "WaterBreathingVisual"],
    paths: [],
  },
  {
    id: "SYS-TALISMANS", name: "Chi and talismans",
    description: "The 90-point chi meter, talisman slots and quick select, every talisman's effect and drain.",
    files: ["Chi", "ChiStone", "PowerUpManager", "Talisman", "TalismanFX", "TalismanHUD", "TalismanHolderView", "TalismanQuickSelect", "TalismanUnlock", "CoinMagnetTalisman", "FeatherFallTalisman", "HealingTalisman", "SpeedBoostTalisman", "SuperJumpTalisman", "QuickItemHUD", "CardActive"],
    paths: [],
  },
  {
    id: "SYS-MOUNTS", name: "Mounts (dragon and Spirit Fox)",
    description: "Summoning Talisman, the dragon ride and the fox ride.",
    files: ["MountTalisman", "DragonController", "FoxMountController"],
    paths: [],
  },
  {
    id: "SYS-SKILLS", name: "Skill tree",
    description: "Gem-bought skills: Jump Power, Sky Leap, Double Jump, Stomp Power, Dash Power, Combo Mastery, Chi Flow.",
    files: ["SkillTree", "SkillTreeView"],
    paths: [],
  },
  {
    id: "SYS-COLLECT", name: "Coins, chests and items",
    description: "Coins and the coin pull, chests (trapped too), rewards, items, inventory and the item catalogue.",
    files: ["CollectableCollisiondetector", "CoinAttractor", "CoinBurstArc", "CoinDetectionArea", "OpenChest", "ChestReward", "ItemPickup", "Inventory", "ItemDefinition", "HNLCatalog", "ActionButton", "BossChests"],
    paths: [],
  },
  {
    id: "SYS-BOSSES", name: "Bosses",
    description: "The shared YokaiBoss stages, the Void Shrine, the Landshark, Sumaria, the Forest Spirit Fox, boss self-healing (BossRegen) and boss treasure.",
    files: ["YokaiBoss", "SamuraiBoss", "VoidShrineBoss", "LandsharkBoss", "BossRegen", "BossHazard", "MoveJapanBoss", "JapanBossAttacks", "SpiritBlastFX", "BossChests", "LandsharkBossSetup", "BossPips"],
    paths: [],
  },
  {
    id: "SYS-LEVELFLOW", name: "Level start, exit and rank",
    description: "Level loading and fades, the treasure exit, S to D grading, the timer, the Challenge Arena, the crash-landing intro.",
    files: ["LevelLoader", "NextLevel", "LevelComplete", "LevelGrade", "LevelGradeSettings", "LevelGradeView", "Timer", "FadeIn", "FadeOut", "IrisWipe", "ChallengeArena", "PlaneCrashIntro", "WreckFX"],
    paths: ["ProjectSettings/EditorBuildSettings.asset"],
  },
  {
    id: "SYS-SAVE", name: "Saving and progression",
    description: "Three save slots, autosave at checkpoints, save version migration, unlock order, Arcade mode.",
    files: ["SaveData", "SaveStorage", "SaveSystem", "SaveSlotMenu", "GameSession"],
    paths: ["ProjectSettings/EditorBuildSettings.asset"],
  },
  {
    id: "SYS-ZONEMAP", name: "Zone Select map",
    description: "The 2x2 zone map, level nodes, ranks shown on nodes, zone music.",
    files: ["ZoneSelectMap", "ZoneMapCamera", "ZoneMapFX", "ZoneMapNode"],
    paths: [],
  },
  {
    id: "SYS-MENUS", name: "Menus and HUD",
    description: "Main menu, pause, options, Talisman Holder, Bag, HUD styling.",
    files: ["MainMenuScene", "MenuBlur", "MenuUI", "MenueControl", "MenueController", "OptionsMenu", "PauseMenuStyle", "HarveyMenus", "BagView", "LevelUIStyle", "PixelTextOutline", "HealthUIManager", "QuickItemHUD"],
    paths: ["Assets/Sprites/UI"],
  },
  {
    id: "SYS-VILLAGES", name: "Villages, dialogue and shops",
    description: "Villagers, dialogue boxes, the village shop and town exits.",
    files: ["Villager", "VillageShop", "DialogueBox", "DialogueScript", "ShopView", "TownExit"],
    paths: ["Assets/Resources/Dialogue"],
  },
  {
    id: "SYS-TUTORIAL", name: "Tutorial",
    description: "The Basic Controls tutorial's goals and guide.",
    files: ["TutorialGoal", "TutorialGuide"],
    paths: [],
  },
  {
    id: "SYS-AUDIO", name: "Audio",
    description: "The audio manager, music crossfades, sound effects.",
    files: ["AudioManager"],
    paths: ["Assets/Music", "Assets/Audio", "Assets/Sounds", "Assets/SFX"],
  },
  {
    id: "SYS-RENDER", name: "Rendering, shaders and FX",
    description: "HNLSprite and the other shaders, sky clouds, zone atmosphere, tomb darkness, pixel particles, screen grading.",
    files: ["PixelFX", "SpriteFX", "SpriteAura", "PlayerFX", "ScreenGrade", "ZoneAtmosphere", "TombDarkness", "SpiritBlastFX", "AfterimageTrail", "MenuBlur", "GreyboxSkin"],
    paths: ["Assets/Shaders", "Assets/Resources/FX", "Assets/Settings", "ProjectSettings/QualitySettings.asset", "ProjectSettings/GraphicsSettings.asset"],
  },
  {
    id: "SYS-DEVTOOLS", name: "Test and capture tools",
    description: "Trailer and test drivers; not shipped gameplay. Changes here only retest the tools themselves.",
    files: ["HNLClashTest", "HNLDemoBot", "HNLTrailerPilot", "HNLTrailerTake", "QAProbe", "QABossDriver"],
    paths: ["Assets/Editor/QA"],
  },
];

// ---------------------------------------------------------------- level test templates
// `{level}` is the level id, `{scene}` the scene path, `{name}` the scene name. `required` tests define "fully tested".
const ALL_PLAY = ["play-human", "play-scripted"];

export const LEVEL_TESTS = {
  act: [
    {
      key: "STRUCT", title: "Scene structure check", kind: "structure", required: true, methods: ["editor-check", "play-human", "play-scripted"],
      systems: ["SYS-RESPAWN", "SYS-LEVELFLOW"],
      description: "The scene has what a playable act needs, checked in the editor without playing.",
      preconditions: "Unity editor open with the project; QARunner compiled.",
      steps: ["Run QARunner.StructureReport(\"{scene}\") through the Unity MCP.", "Read the report's checks."],
      expected: "No missing scripts; a player with PlayerController; a LevelManager with a respawn point; at least one CheckPoint; a level exit (NextLevel or LevelComplete).",
    },
    {
      key: "SMOKE", title: "Loads into play cleanly", kind: "smoke", required: true, methods: ALL_PLAY,
      systems: ["SYS-LEVELFLOW", "SYS-CAMERA", "SYS-MOVEMENT"],
      description: "Enter Play mode in the level and let it run.",
      preconditions: "Save slots backed up (QARunner.BackupSaves). Scene opened from the Project window.",
      steps: ["Open {scene}.", "Enter Play mode and wait 10 seconds without input (QAProbe records errors and frame times).", "Stop Play mode."],
      expected: "Harvey spawns and is controllable, the camera is on him, and no errors or exceptions are logged.",
    },
    {
      key: "PLAYTHROUGH", title: "Start-to-finish playthrough", kind: "playthrough", required: true, methods: ALL_PLAY,
      systems: ["SYS-MOVEMENT", "SYS-ENEMIES", "SYS-HAZARDS", "SYS-LEVELFLOW", "SYS-SAVE"],
      description: "Play the level from its start to the treasure, the way a player would.",
      preconditions: "A save slot with this level unlocked, or the scene played directly with save slots backed up.",
      steps: ["Start the level.", "Reach every checkpoint in order.", "Take the treasure at the end."],
      expected: "Every section can be crossed with the abilities available at this point in the game; the treasure grades the run S to D, saves and returns to the zone map.",
    },
    {
      key: "RESPAWN", title: "Death and respawn", kind: "mechanic", required: true, methods: ALL_PLAY,
      systems: ["SYS-RESPAWN", "SYS-CAMERA"],
      description: "Die and come back at the current respawn point. Setting a new point at a checkpoint is TC-SAVE-AUTOSAVE.",
      preconditions: "Play mode in the level, Harvey on the ground.",
      steps: ["Die: by hand, lose all health to a hazard or enemy; scripted, QARunner.StartPlayCapture(..., action: \"respawn\") forces LevelManager.Respawn.", "Wait for the respawn."],
      expected: "Harvey reappears at the level's current respawn point (within 0.25 units), comes to rest on the first solid ground beneath it (not pushed through it), with three lives and full chi; the camera glides back to him and control returns within 3.5 seconds of the death.",
    },
    {
      key: "PERF", title: "Performance benchmark at spawn", kind: "performance", required: false, methods: ["play-scripted"],
      systems: ["SYS-RENDER"],
      description: "A repeatable 10-second idle capture at the spawn point.",
      preconditions: "Editor Play mode, Game view at a fixed size, nothing else running in the editor.",
      steps: ["Enter Play mode in {scene}.", "Run QAProbe for 10 seconds after a 2-second settle.", "Record the probe's JSON with `qa perf`."],
      expected: "Average frame time at or under 16.7 ms and no frame over 50 ms after the settle.",
    },
  ],
  boss: [
    { key: "STRUCT", title: "Scene structure check", kind: "structure", required: true, methods: ["editor-check", "play-human", "play-scripted"], systems: ["SYS-BOSSES", "SYS-RESPAWN"], description: "The arena has a player, a boss and a respawn point.", preconditions: "Unity editor open.", steps: ["Run QARunner.StructureReport(\"{scene}\")."], expected: "No missing scripts; a player; a boss component (YokaiBoss, SamuraiBoss, VoidShrineBoss or LandsharkBoss); a LevelManager with a respawn point." },
    { key: "SMOKE", title: "Loads into play cleanly", kind: "smoke", required: true, methods: ALL_PLAY, systems: ["SYS-LEVELFLOW", "SYS-BOSSES"], description: "Enter Play mode in the arena and let it run.", preconditions: "Save slots backed up.", steps: ["Open {scene}.", "Enter Play mode, wait 10 seconds.", "Stop."], expected: "Harvey and the boss spawn, the fight starts, no errors are logged." },
    { key: "BOSSFIGHT", title: "Boss fight to defeat", kind: "playthrough", required: true, methods: ALL_PLAY, systems: ["SYS-BOSSES", "SYS-COMBAT", "SYS-COLLECT"], description: "Fight the boss through every stage.", preconditions: "Play mode in the arena.", steps: ["Damage the boss through all of its stages.", "Watch each attack at least once.", "Collect the boss chests and take the treasure."], expected: "Every stage starts, every attack can be avoided, the boss is defeated, chests drop by speed of the win, and the treasure awards the zone's talisman and returns to the map." },
    { key: "RESPAWN", title: "Death and respawn in the arena", kind: "mechanic", required: true, methods: ALL_PLAY, systems: ["SYS-RESPAWN", "SYS-BOSSES"], description: "Lose all health to the boss.", preconditions: "Play mode in the arena.", steps: ["Take three hits.", "Wait for the respawn."], expected: "Harvey respawns in the arena; the boss fight resets or continues as designed; no softlock." },
    { key: "PERF", title: "Performance benchmark in the arena", kind: "performance", required: false, methods: ["play-scripted"], systems: ["SYS-RENDER", "SYS-BOSSES"], description: "10-second capture with the boss active.", preconditions: "Editor Play mode at a fixed Game view size.", steps: ["Enter Play mode.", "QAProbe 10 s after a 2 s settle.", "Record with `qa perf`."], expected: "Average frame time at or under 16.7 ms and no frame over 50 ms after the settle." },
  ],
  village: [
    { key: "STRUCT", title: "Scene structure check", kind: "structure", required: true, methods: ["editor-check", "play-human", "play-scripted"], systems: ["SYS-VILLAGES"], description: "The village has a player, villagers and an exit.", preconditions: "Unity editor open.", steps: ["Run QARunner.StructureReport(\"{scene}\")."], expected: "No missing scripts; a player; at least one Villager; a way out (TownExit or NextLevel)." },
    { key: "SMOKE", title: "Loads into play cleanly", kind: "smoke", required: true, methods: ALL_PLAY, systems: ["SYS-LEVELFLOW", "SYS-VILLAGES"], description: "Enter Play mode in the village.", preconditions: "Save slots backed up.", steps: ["Open {scene}.", "Enter Play mode, wait 10 seconds.", "Stop."], expected: "Harvey spawns, villagers idle, no errors are logged." },
    { key: "NPCS", title: "Talk to every villager", kind: "mechanic", required: true, methods: ALL_PLAY, systems: ["SYS-VILLAGES"], description: "Every villager's dialogue opens, pages and closes.", preconditions: "Play mode in the village.", steps: ["Walk to each villager.", "Open their dialogue and page to the end.", "Close it and move on."], expected: "Every dialogue opens and closes, text fits its box, Harvey can move again afterwards." },
    { key: "SHOP", title: "Buy from the shop", kind: "mechanic", required: true, methods: ALL_PLAY, systems: ["SYS-VILLAGES", "SYS-COLLECT", "SYS-SAVE"], description: "Buy one item and see it in the Bag.", preconditions: "Enough coins in the save.", steps: ["Open the shop.", "Buy one item.", "Open the Bag."], expected: "Coins go down by the price, the item appears in the Bag, and it is still there after a save and reload." },
    { key: "EXIT", title: "Leave the village", kind: "mechanic", required: true, methods: ALL_PLAY, systems: ["SYS-LEVELFLOW"], description: "Use the village exit.", preconditions: "Play mode in the village.", steps: ["Walk to the exit and use it."], expected: "The next scene loads and the village counts as visited." },
  ],
  tutorial: [
    { key: "STRUCT", title: "Scene structure check", kind: "structure", required: true, methods: ["editor-check", "play-human", "play-scripted"], systems: ["SYS-TUTORIAL"], description: "The tutorial has a player and its goals.", preconditions: "Unity editor open.", steps: ["Run QARunner.StructureReport(\"{scene}\")."], expected: "No missing scripts; a player; TutorialGuide and at least one TutorialGoal." },
    { key: "SMOKE", title: "Loads into play cleanly", kind: "smoke", required: true, methods: ALL_PLAY, systems: ["SYS-TUTORIAL"], description: "Enter Play mode in the tutorial.", preconditions: "Save slots backed up.", steps: ["Open {scene}.", "Enter Play mode, wait 10 seconds.", "Stop."], expected: "Harvey spawns and the first prompt shows; no errors are logged." },
    { key: "PLAYTHROUGH", title: "Complete the tutorial", kind: "playthrough", required: true, methods: ALL_PLAY, systems: ["SYS-TUTORIAL", "SYS-MOVEMENT"], description: "Follow every prompt to the end.", preconditions: "Play mode in the tutorial.", steps: ["Do what each prompt asks.", "Reach the final goal."], expected: "Every prompt completes and the tutorial returns to the menu." },
  ],
  menu: [
    { key: "SMOKE", title: "Loads cleanly", kind: "smoke", required: true, methods: ALL_PLAY, systems: ["SYS-MENUS"], description: "Enter Play mode in the scene.", preconditions: "Save slots backed up.", steps: ["Open {scene}.", "Enter Play mode, wait 5 seconds.", "Stop."], expected: "The scene draws and no errors are logged." },
    { key: "FLOW", title: "Every button works", kind: "mechanic", required: true, methods: ALL_PLAY, systems: ["SYS-MENUS", "SYS-SAVE"], description: "Use every button with keyboard and pad.", preconditions: "Play mode in the scene.", steps: ["Navigate to each button with the keyboard, then a pad.", "Activate it and come back."], expected: "Every button responds and leads where its label says; focus is never lost." },
  ],
  prototype: [
    { key: "SMOKE", title: "Loads into play cleanly", kind: "smoke", required: true, methods: ALL_PLAY, systems: ["SYS-LEVELFLOW"], description: "Enter Play mode in the prototype.", preconditions: "Save slots backed up.", steps: ["Open {scene}.", "Enter Play mode, wait 10 seconds.", "Stop."], expected: "Harvey spawns and no errors are logged." },
    { key: "PLAYTHROUGH", title: "Route can be completed", kind: "playthrough", required: true, methods: ALL_PLAY, systems: ["SYS-MOVEMENT", "SYS-BOUNCE"], description: "Climb the route to the treasure.", preconditions: "Play mode in the prototype.", steps: ["Follow the route to the summit.", "Take the treasure."], expected: "Every gap is reachable with the base jump plus the level's clouds, mushrooms and spring." },
  ],
  sandbox: [],
};

// ---------------------------------------------------------------- mechanic and system tests
// `level` is the scene name to run it in (the test is filed under that level too, as a non-required test).
export const MECHANIC_TESTS = [
  // LANDSHARK / BOSSREGEN (2026-10-27): the Egypt boss, boss self-healing and the boss difficulty curve. Scripted runs:
  // QARunner.StartPlayCapture(<arena>, <runId>, <scenario>, 1, 1, "boss:<suite>") with the QABossDriver suites named below.
  { id: "TC-BOSS-LANDSHARK-ATTACKS", title: "Landshark attacks: hit, dodge, opening", systems: ["SYS-BOSSES", "SYS-COMBAT"], level: "Z2BossSandy", methods: ALL_PLAY, required: true,
    description: "Every Landshark attack hurts a Harvey who stands still, can be avoided at Harvey's own speed, and leaves an opening when it misses. Scripted: boss:landshark-attacks.",
    preconditions: "Play mode in Z2BossSandy.",
    steps: ["Stand still through a Hunt, a Charge, a Bite Combo and a Devour.", "Then dodge each: run off the sand mound, jump the torpedo's fin, back away from the lunges, run out of the whirlpool.", "Watch each animation (idle, dive, swim, torpedo, breach, gulp, bite, on its back, roar)."],
    expected: "Each attack hits a standing Harvey; each can be dodged with no hit; a missed Hunt, Charge, Bite Combo or Devour leaves it on its back (the opening); every animation renders." },
  { id: "TC-BOSS-LANDSHARK-STAGES", title: "Landshark: three stages to defeat", systems: ["SYS-BOSSES", "SYS-COLLECT"], level: "Z2BossSandy", methods: ALL_PLAY, required: true,
    description: "Four lives over three stages (Sand Hunter 4, Apex Predator 3, Desert Devourer 2-1). Scripted: boss:landshark-fight.",
    preconditions: "Play mode in Z2BossSandy.", steps: ["Hit it in every opening and break every heal.", "Collect the chests and the treasure."],
    expected: "Stages 1, 2, 3 in order, each with its own attacks; defeat shows the treasure and drops the chests; a respawn after the win doesn't bring it back." },
  { id: "TC-BOSS-LANDSHARK-REGEN", title: "Landshark Buried Regeneration", systems: ["SYS-BOSSES"], level: "Z2BossSandy", methods: ALL_PLAY, required: true,
    description: "Its heal from stage 2: only its fin shows, golden sand pours in, a pip blinks, a heartbeat thuds. Scripted: boss:landshark-fight.",
    preconditions: "Play mode in Z2BossSandy, the Landshark at 3 lives.",
    steps: ["Let one heal finish.", "Stomp or dash the fin during another.", "Count the heals in one fight.", "Die while it heals."],
    expected: "A finished heal gives back one life (never past 4) in about 4 s and the stage stays; a hit on the fin cancels it and throws it on its back with nothing gained; at most two tries a fight, 18 s apart; dying mid-heal resets the fight with no heal left running; no heal once it is beaten." },
  { id: "TC-BOSS-SUMARIA-MEDITATE", title: "Sumaria's meditation (Mokuso)", systems: ["SYS-BOSSES", "SYS-COMBAT"], level: "Z3BossBattle", methods: ALL_PLAY, required: true,
    description: "His heal from stage 2: kneeling, petals, a bell, eyes shut then open (zanshin). Scripted: boss:samurai.",
    preconditions: "Play mode in Z3BossBattle, Sumaria at 4 lives.",
    steps: ["Let one meditation finish.", "Hit him while his eyes are shut.", "Hit him after they open.", "Die while he meditates."],
    expected: "Finished: one life back (never past 5), the stage stays, a dash slash follows; eyes shut: the hit counts and the heal is lost; eyes open: Harvey is countered and the heal goes on; dying mid-meditation resets the fight." },
  { id: "TC-BOSS-SUMARIA-STAGES", title: "Sumaria: five lives to defeat", systems: ["SYS-BOSSES", "SYS-COLLECT"], level: "Z3BossBattle", methods: ALL_PLAY, required: true,
    description: "Five lives over three stages (Charging Blade 5, Phantom Steps 4-3, Storm of Blades 2-1). Scripted: boss:samurai.",
    preconditions: "Play mode in Z3BossBattle.", steps: ["Hit him in every opening.", "Collect the treasure."],
    expected: "Stages 1, 2, 3 in order; defeat shows the treasure; no meditation after defeat." },
  { id: "TC-BOSS-BALANCE", title: "Boss difficulty rises with the zones", systems: ["SYS-BOSSES"], level: "Z2BossSandy", methods: ["editor-check", "code-review", "play-human", "play-scripted"], required: false,
    description: "The bosses in play order (Grass fox, Tomb Void Shrine, Egypt Landshark, Japan Sumaria) follow the curve in Tools/QA/boss_balance.md.",
    preconditions: "Unity editor open.", steps: ["Read each boss's lives, stage tuning and heal from its arena.", "Compare with Tools/QA/boss_balance.md."],
    expected: "Lives 3, 3, 4, 5; only the Landshark and Sumaria heal; each later boss has more attacks per stage and no longer tells and openings than the one before it at the same stage, or the table says why." },
  { id: "TC-MOVE-RUNJUMP", title: "Run and normal jump", systems: ["SYS-MOVEMENT"], level: "Tutorial_BasicControls", methods: ALL_PLAY, required: true,
    description: "Walking speed and the normal jump height.", preconditions: "Play mode, no skills learned.",
    steps: ["Hold right for 2 seconds on flat ground.", "Press jump once from standing.", "Press jump once while running."],
    expected: "Walking speed about 4.5 units/s; one press is always the normal jump (about 1.16 tiles high), with or without skills." },
  { id: "TC-MOVE-COYOTE", title: "Coyote time off a ledge", systems: ["SYS-MOVEMENT"], level: "Tutorial_BasicControls", methods: ALL_PLAY, required: true,
    description: "A jump pressed just after leaving a ledge still counts.", preconditions: "Play mode next to a ledge.",
    steps: ["Run off a ledge.", "Press jump a few frames after the feet leave it."], expected: "The jump happens." },
  { id: "TC-MOVE-SUPERJUMP", title: "Super Jump by double tap", systems: ["SYS-MOVEMENT", "SYS-SKILLS"], level: "Z4A6GrassGreen", methods: ALL_PLAY, required: true,
    description: "Tapping jump twice while rising tops the jump up, once Jump Power is learned.", preconditions: "A save with Jump Power at tier 1, 2 and 3 in turn.",
    steps: ["Jump and tap jump again while still rising.", "Repeat at each Jump Power tier.", "Repeat with no Jump Power."],
    expected: "Take-off x1.25, x1.5, x1.65 by tier; without Jump Power the second tap does nothing." },
  { id: "TC-MOVE-DOUBLEJUMP", title: "Double Jump skill", systems: ["SYS-MOVEMENT", "SYS-SKILLS"], level: "Z4A6GrassGreen", methods: ALL_PLAY, required: true,
    description: "A later mid-air press jumps again once Double Jump is learned.", preconditions: "A save with Double Jump learned, and one without.",
    steps: ["Jump, wait until falling, press jump.", "Repeat without the skill."], expected: "A second jump with the skill; nothing without it." },
  { id: "TC-MOVE-GROUND", title: "Ground detection on slopes, moving platforms and edges", systems: ["SYS-MOVEMENT", "SYS-CAMERA"], level: "Z1A2Tomb", methods: ALL_PLAY, required: true,
    description: "Harvey stands, lands and jumps from every kind of ground.", preconditions: "Play mode in a level with moving tiles.",
    steps: ["Stand on a moving platform for a full cycle.", "Jump from it.", "Land on a tile edge and on a slope."], expected: "No sliding off, no stuck jumps, no falling through; the landing animation plays once." },
  { id: "TC-BOUNCE-ALL", title: "Bounce pads, clouds and mushrooms", systems: ["SYS-BOUNCE"], level: "Z4A7GrassGreen", methods: ALL_PLAY, required: true,
    description: "Every bouncing object launches Harvey the same way each time.", preconditions: "Play mode next to each bounce object.",
    steps: ["Land on a jump box, a bouncy cloud and a floating mushroom.", "Stomp a flying mushroom.", "Touch a flying mushroom's side."], expected: "Consistent bounce heights; the bounce cloud squashes; a flying mushroom's side hurts." },
  { id: "TC-COMBAT-STOMP", title: "Stomp damage by zone", systems: ["SYS-COMBAT", "SYS-ENEMIES"], level: "Z1A1Tomb", methods: ALL_PLAY, required: true,
    description: "Stomps needed to defeat an enemy follow the zone's toughness.", preconditions: "No Stomp Power learned.",
    steps: ["Stomp a regular enemy until it is defeated in each zone."], expected: "Grassy Green 1 stomp, Tomb and Dunes 2, Japan 3; Harvey bounces on every stomp; landing beside an enemy is not a stomp." },
  { id: "TC-COMBAT-DASH", title: "Spirit Dash", systems: ["SYS-COMBAT", "SYS-TALISMANS"], level: "Z1A1Tomb", methods: ALL_PLAY, required: true,
    description: "The starting dash talisman.", preconditions: "Spirit Dash in slot X, chi above 20.",
    steps: ["Press X on the ground.", "Hold X.", "Try X in the air.", "Dash through an enemy."], expected: "Speed 16 from the ground only; holding keeps it going while chi drains 20/s; enemies in the way are defeated and cannot hurt Harvey." },
  { id: "TC-COMBAT-COMBO", title: "Shared stomp and dash combo", systems: ["SYS-COMBAT", "SYS-SKILLS"], level: "Z4A6GrassGreen", methods: ALL_PLAY, required: true,
    description: "Defeats within 5 seconds climb the combo.", preconditions: "Combo Mastery learned.",
    steps: ["Defeat enemies by stomp and dash, each within 5 seconds of the last.", "Get hurt mid-combo."], expected: "Each defeat climbs a stage up to five; getting hurt ends it; stages above 1 need Combo Mastery." },
  { id: "TC-COMBAT-CLASH", title: "Samurai dash clash", systems: ["SYS-COMBAT", "SYS-ENEMIES"], level: "Z3A2Japan", methods: ALL_PLAY, required: false,
    description: "A head-on Spirit Dash into a samurai's dash cancels both.", preconditions: "Spirit world active in Z3A2 (samurai only appear there).",
    steps: ["Dash head-on into a samurai monkey during its wind-up or slash."], expected: "Both dashes cancel, the clash card shows, Harvey is knocked back without damage." },
  { id: "TC-ENEMY-SAMURAI", title: "Samurai monkey block, slash and stun", systems: ["SYS-ENEMIES"], level: "Z3A2Japan", methods: ALL_PLAY, required: true,
    description: "Front-only block and slash.", preconditions: "Spirit world active in Z3A2.",
    steps: ["Approach from the front.", "Approach from behind and from above."], expected: "It blocks and slashes from the front only; being slashed stuns Harvey 2 s; behind or above is safe." },
  { id: "TC-ENEMY-FOX", title: "Spirit Fox spot, crouch and charge", systems: ["SYS-ENEMIES"], level: "Z4A6GrassGreen", methods: ALL_PLAY, required: true,
    description: "Grassy Green's only regular enemy.", preconditions: "Play mode near a fox.",
    steps: ["Walk into a fox's view.", "Dodge its charge.", "Stomp it."], expected: "It spots Harvey, crouches and glows, then charges; one stomp defeats it in Grassy Green." },
  { id: "TC-ENEMY-GHOST", title: "Tomb ghost passes through", systems: ["SYS-ENEMIES"], level: "Z1A4Tomb", methods: ALL_PLAY, required: false,
    description: "Harvey passes straight through the purple ghoul.", preconditions: "Play mode near a ghost.",
    steps: ["Walk into the ghost."], expected: "Behaves as designed for ghosts (pass-through, as documented in GhostEnemy)." },
  { id: "TC-HAZ-SPIKERHYTHM", title: "Spike rhythm timing", systems: ["SYS-HAZARDS"], level: "Z1A6Tomb", methods: ALL_PLAY, required: true,
    description: "Rhythm spikes leave a fair jump window.", preconditions: "Play mode at the checkerboard.",
    steps: ["Watch two full cycles.", "Cross in the safe window."], expected: "The window matches the warning; the spike sound plays on the beat; contact hurts only when raised." },
  { id: "TC-HAZ-BOULDER", title: "Rolling boulder trap", systems: ["SYS-HAZARDS"], level: "Z1A3Tomb", methods: ALL_PLAY, required: true,
    description: "The shaft boulder.", preconditions: "Play mode before the shaft.",
    steps: ["Trigger the trap.", "Outrun it.", "Let it hit Harvey once."], expected: "It rolls without sticking, can be outrun, hurts on contact and resets after respawn." },
  { id: "TC-HAZ-POISON", title: "Poison pools", systems: ["SYS-HAZARDS", "SYS-WATER"], level: "Z1A5Tomb", methods: ALL_PLAY, required: true,
    description: "Instant death without Water Breathing.", preconditions: "Play mode next to a pool, Water Breathing equipped.",
    steps: ["Enter the pool without the talisman.", "Enter with Water Breathing active."], expected: "Instant death without it; safe with it while chi lasts." },
  { id: "TC-RESP-CONTACT", title: "Contact damage, knockback and death", systems: ["SYS-RESPAWN", "SYS-HAZARDS"], level: "Z1A1Tomb", methods: ALL_PLAY, required: true,
    description: "Side contact costs one segment.", preconditions: "Full health.",
    steps: ["Touch an enemy from the side three times."], expected: "One segment per hit with knockback and a flicker; the third hit is a death back to the last checkpoint." },
  { id: "TC-TAL-CHI", title: "Chi meter tiers and drain", systems: ["SYS-TALISMANS"], level: "Z1A1Tomb", methods: ALL_PLAY, required: true,
    description: "90-point meter in nine units, three tiers.", preconditions: "Full chi.",
    steps: ["Hold Spirit Dash until chi is empty.", "Collect a Chi Crystal."], expected: "Nine units blue, then green, then red; a spent unit shows the next colour; drain matches each talisman's rate; the crystal refills." },
  { id: "TC-TAL-HEALING", title: "Healing Stone", systems: ["SYS-TALISMANS", "SYS-RESPAWN"], level: "Z1A2Tomb", methods: ALL_PLAY, required: true,
    description: "Refill then shield.", preconditions: "Healing Stone owned, chi 60+, one health segment.",
    steps: ["Use the Healing Stone.", "Touch a hazard during its 7 s.", "Fall into a pit during it."], expected: "60 chi up front; health refills a segment at a time; then every kind of damage is blocked and the pit rescues Harvey until it ends." },
  { id: "TC-TAL-WATER", title: "Water Breathing", systems: ["SYS-TALISMANS", "SYS-WATER"], level: "Z1A5Tomb", methods: ALL_PLAY, required: true,
    description: "No breath used underwater for 10 s.", preconditions: "Water Breathing equipped.",
    steps: ["Activate underwater.", "Try to activate in Z4A6."], expected: "Aqua glow and bubble helmet, no breath used, 2.5 chi/s; blocked in Z4A6." },
  { id: "TC-TAL-QUICKSELECT", title: "Talisman slots and quick select", systems: ["SYS-TALISMANS", "SYS-MENUS"], level: "Z1A1Tomb", methods: ALL_PLAY, required: true,
    description: "Z, X, C fire the slots; L1/Q quick item; L2 cycles slot 1.", preconditions: "Several talismans owned.",
    steps: ["Fire each slot.", "Use the quick item.", "Cycle slot 1."], expected: "Each key fires its slot; the HUD shows the right icons; cycling changes slot 1 only." },
  { id: "TC-MOUNT-SUMMON", title: "Summoning Talisman: fox and dragon", systems: ["SYS-MOUNTS", "SYS-TALISMANS"], level: "Z2A1Sandy", methods: ALL_PLAY, required: true,
    description: "Calls the Spirit Fox or the dragon.", preconditions: "Summoning Talisman owned, chi full.",
    steps: ["Summon the fox and ride for its 45 s.", "Switch to the dragon with R1.", "Dismount."], expected: "Mount dissolve FX, 3 chi/s, R1 switches, dismount returns control; combo FX work on the dragon." },
  { id: "TC-SKILL-TREE", title: "Buy skills with gems", systems: ["SYS-SKILLS", "SYS-SAVE"], level: "Z4GrassVillage", methods: ALL_PLAY, required: true,
    description: "Spend gems on a skill and keep it.", preconditions: "A save with gems.",
    steps: ["Open the Skill Tree.", "Buy a skill.", "Save, quit and reload."], expected: "Gems go down by the cost; the skill applies at once and is still learned after reloading." },
  { id: "TC-COLLECT-CHESTS", title: "Chests, trapped chests and rewards", systems: ["SYS-COLLECT"], level: "Z1A2Tomb", methods: ALL_PLAY, required: true,
    description: "Opened by hand with Up/E.", preconditions: "Play mode next to a chest and a trapped chest.",
    steps: ["Open a normal chest.", "Listen for the rattle, then open a trapped chest."], expected: "Normal chests give coins, a Spirit Peach, Chi Crystals or gems; trapped chests rattle faintly and burst for two lives." },
  { id: "TC-COLLECT-COINS", title: "Coins and coin pull", systems: ["SYS-COLLECT", "SYS-AUDIO"], level: "Z4A6GrassGreen", methods: ALL_PLAY, required: true,
    description: "Coins count and chime.", preconditions: "Play mode.",
    steps: ["Collect a row of coins.", "Use a coin pull (rainbow combo or magnet)."], expected: "Each coin counts once with a rising chime and a pixel pop; pulled coins fly to Harvey." },
  { id: "TC-WORLD-SWITCH", title: "Living and spirit world switch", systems: ["SYS-WORLDSWITCH"], level: "Z3A1Japan", methods: ALL_PLAY, required: true,
    description: "Only one world exists at a time.", preconditions: "Play mode in Z3A1 (the only changeWorld level).",
    steps: ["Switch worlds at a Torii gate.", "Check platforms and enemies of both worlds."], expected: "Each world's objects appear only in it; the screen grade changes; Harvey keeps his position." },
  { id: "TC-SAVE-SLOTS", title: "Three save slots", systems: ["SYS-SAVE", "SYS-MENUS"], level: "MainMenue", methods: ALL_PLAY, required: true,
    description: "New game, continue, delete.", preconditions: "Save slots backed up; at least one empty slot.",
    steps: ["Start a new game in an empty slot.", "Return to the menu and continue it.", "Delete it."], expected: "The new slot starts at the zone map with only the first level open; continue resumes; delete empties it." },
  { id: "TC-SAVE-AUTOSAVE", title: "Checkpoint autosave and level-end save", systems: ["SYS-SAVE", "SYS-RESPAWN", "SYS-LEVELFLOW"], level: "Z4A6GrassGreen", methods: ALL_PLAY, required: true,
    description: "Progress survives quitting.", preconditions: "Save slots backed up; a slot in use.",
    steps: ["Touch a checkpoint, quit to menu and continue.", "Finish the level, quit and continue."], expected: "Continue returns to the checkpoint; the finished level stays cleared with its best rank and the next one is open." },
  { id: "TC-SAVE-MIGRATION", title: "Old save files load", systems: ["SYS-SAVE"], level: "MainMenue", methods: ["play-scripted", "play-human", "automated"], required: true,
    description: "Version 1 saves migrate to version 2 (world order).", preconditions: "A version 1 save file in a backed-up slot.",
    steps: ["Load the version 1 save."], expected: "It migrates to version 2 with progress mapped to the new order; nothing is lost." },
  { id: "TC-FLOW-RANK", title: "Treasure grade S to D", systems: ["SYS-LEVELFLOW"], level: "Z4A6GrassGreen", methods: ALL_PLAY, required: true,
    description: "The rank card after the treasure.", preconditions: "Play mode near the level's end.",
    steps: ["Take the treasure.", "Read the grade card.", "Check the zone map node."], expected: "The run is graded from time, coins and deaths; the best rank is saved and shown on the map (an S node glows)." },
  { id: "TC-FLOW-INTRO", title: "Crash-landing intro", systems: ["SYS-LEVELFLOW", "SYS-AUDIO"], level: "Z4A6GrassGreen", methods: ALL_PLAY, required: true,
    description: "The biplane opening.", preconditions: "A save that has not seen the intro.",
    steps: ["Start Z4A6.", "Start again in the same save.", "Press Jump during the intro in a fresh save."], expected: "Plays once per save; Jump skips it; Harvey climbs out and control starts." },
  { id: "TC-FLOW-ARENA", title: "Challenge Arena", systems: ["SYS-LEVELFLOW", "SYS-ENEMIES"], level: "Z4A6GrassGreen", methods: ALL_PLAY, required: true,
    description: "Z4A6's cave arena.", preconditions: "Play mode at the cave.",
    steps: ["Enter the arena.", "Clear all five rounds."], expected: "It locks, runs rounds of 2, 3, 4, 5 and 6 Spirit Foxes, then drops a restore chest and unlocks." },
  { id: "TC-MAP-ZONESELECT", title: "Zone Select map", systems: ["SYS-ZONEMAP", "SYS-SAVE", "SYS-AUDIO"], level: "ZoneSelect", methods: ALL_PLAY, required: true,
    description: "Walk the map and enter levels.", preconditions: "A save with several levels cleared.",
    steps: ["Walk between nodes in every zone.", "Enter an open level.", "Try a locked one."], expected: "Only unlocked nodes can be entered; ranks show on nodes; zone music crossfades at the walk midpoint." },
  { id: "TC-MENU-PAUSE", title: "Pause menu and MAP", systems: ["SYS-MENUS"], level: "Z1A1Tomb", methods: ALL_PLAY, required: true,
    description: "Pause, resume, options and back to the map.", preconditions: "Play mode in a level.",
    steps: ["Pause.", "Open options and change volume.", "Choose MAP and confirm."], expected: "The game freezes and resumes cleanly; options persist; MAP asks first, then returns to the zone map." },
  { id: "TC-VILLAGE-DIALOGUE", title: "Dialogue system", systems: ["SYS-VILLAGES"], level: "Z1TombVillage", methods: ALL_PLAY, required: false,
    description: "Text dialogue files drive villagers.", preconditions: "Play mode in a village.",
    steps: ["Talk to a villager with several pages."], expected: "Pages advance on input, text fits, the box closes and control returns." },
  { id: "TC-RENDER-PIXELS", title: "Pixel-art integrity", systems: ["SYS-RENDER", "SYS-CAMERA"], level: "Z4A6GrassGreen", methods: ["play-human", "play-scripted"], required: true,
    description: "Sprites, particles and shader effects stay on the art grid.", preconditions: "Game view at 1920x1080 and at 1280x720.",
    steps: ["Walk the level and watch tiles, sprites and FX.", "Capture the Game view at each size."], expected: "No blurred or half-pixel sprites, no seams between tiles or parallax tiles, FX snapped to the grid." },
  { id: "TC-RENDER-SKY", title: "Sky clouds", systems: ["SYS-RENDER"], level: "Z4A6GrassGreen", methods: ["play-human", "play-scripted", "editor-check"], required: true,
    description: "Designed clouds drift inside every sky.", preconditions: "Play mode, or the edit-mode rig in the playtest skill.",
    steps: ["Look at the parallax and box skies for 20 seconds."], expected: "Clouds from the library, crisp pixels, no cloud cut at a sky tile seam, lower rows hazed; edges bubble subtly." },
  { id: "TC-RENDER-TOMBDARK", title: "Tomb darkness and lights", systems: ["SYS-RENDER"], level: "Z1A2Tomb", methods: ALL_PLAY, required: true,
    description: "Light circles around Harvey and torches.", preconditions: "Play mode in a Tomb act.",
    steps: ["Walk past torches and checkpoints."], expected: "Harvey and every torch, lamp, lantern and checkpoint carry a light; the rest is dark; no flicker at the edges." },
  { id: "TC-AUDIO-MIX", title: "Music and effects", systems: ["SYS-AUDIO"], level: "Z1A1Tomb", methods: ["play-human"], required: false,
    description: "A listening pass.", preconditions: "Headphones.",
    steps: ["Play two minutes: jump, stomp, coin, hurt, die, checkpoint."], expected: "Every effect plays once at a sensible level; music loops cleanly." },
];
