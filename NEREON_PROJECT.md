NEREON — Project Bible
Single source of truth. Read this before every session.
Every Copilot session, every developer decision, every architecture change is reflected here.
Last updated: Session 22 — 2026-03-03 (NereonPassPopup.cs redesigned: regular MonoBehaviour on PassPopUP_Panel — self-as-backdrop pattern (panel IS full-screen dark overlay, stretch anchors + semi-transparent Image). Wires own buttons by name. BtnPassPopUp auto-wired from scene. Auto-shows in Start() if tier==0. No sibling backdrop GO needed. NereonPassSession.cs stable. Full project evaluation completed — see Session 22 decisions log.)

🎯 The Vision
NEREON is a restricted open-world, third-person online RPG built entirely on the Solana blockchain.
It is simultaneously a gaming platform — a small, beautiful hub town where players walk around,
discover buildings, enter them to play skill-based mini-games, level up their character, and compete on
monthly leaderboards that automatically reward the top 5 players per game with real tokens via smart contract.

The World
A small, beautifully crafted 3D hub town — the persistent multiplayer world.
Third-person camera — you see your character from behind, walking through the town.
Restricted open world — explorable but naturally bounded (cliffs, river, forests).
Online — other players visible walking around in real time.
Each building is a portal to one specific mini-game.
Town grows as the platform grows — new building = new game.
The Gameplay Loop
Wallet Login → Hub Town → Walk to Building → Press E to Enter
    → Play Mini-Game → Score submitted on-chain → XP + Level Up
    → Monthly Leaderboard updates → Top 5 auto-rewarded by smart contract

content_copy
Core Principles
Principle	Detail
Decentralised	All persistent data lives on Solana. No central database.
Skill + Luck	Mini-games reward skill with optional provably-fair on-chain randomness (VRF).
Beautiful	High-quality toon art, Ghibli-inspired aesthetic. Cute village feel.
Rewarding	Monthly top-5 per game auto-paid by Anchor program. No manual claiming.
RPG Progression	Character levels up via XP from games. Level is permanent on-chain.
Mobile-first	Targets Seeker (Solana Mobile) via Mobile Wallet Adapter + Seed Vault.

content_copy
🏗️ Repository Structure (Actual)
NEREON GIT/
├── NEREON_PROJECT.md              ← THIS FILE — read first every session
├── anchor/                        ← Solana smart contract (Rust / Anchor 0.30.1)
│   ├── Anchor.toml
│   ├── programs/nereon/src/lib.rs ← Full program (deployed ✅)
│   └── target/idl/nereon.json    ← Generated IDL (source of truth for C# bridge)
├── shared/                        ← (reserved — currently unused)
└── unity/NEREON/NEREON/
    └── Assets/
        ├── _NEREON/
        │   ├── Scripts/           ← All NEREON C# scripts
        │   │   ├── generated/     ← NereonClient.cs (Anchor bridge — do not hand-edit)
        │   │   └── Editor/        ← Editor-only tools (scene builders, fixers)
        │   ├── Scenes/            ← LandingScene, WelcomeInitScene, HomeScene, LeaderboardScene
        │   ├── Prefabs/           ← HubPlayer.prefab, WelcomeCanvas.prefab
        │   │   └── Avatars/       ← Fire_Avatar, Water_Avatar, Air_Avatar, Earth_Avatar
        │   ├── Data/              ← WorldConfig.asset, AvatarRegistry.asset, biome assets
        │   ├── IDL/               ← nereon.json (copy of anchor IDL)
        │   ├── Models/            ← Imported 3D art (buildings, characters)
        │   ├── Materials/
        │   ├── Sounds/            ← Audio clips (Weightless.mp3 + future tracks)
        │   └── Animations/
        └── [Third-party packages] ← See Asset Inventory section

content_copy
Note: The unity/ folder is excluded from git tracking (.gitignore).
The Anchor program and scripts are the only version-controlled artefacts.

🔄 Scene Flow (Current Implementation)
LandingScene (build index 2)
  └─ AmbientMusicManager (DontDestroyOnLoad) starts playing
  └─ LoginFlowController
        └─ wallet connects (Mobile Wallet Adapter / Web3Auth)
              └─ button enabled: "ENTER NEREON"
                    └─ user clicks → NereonClient.IsUserInitializedAsync()
                          ├─ YES (returning user)  → SceneLoader.Load("HomeScene")
                          └─ NO  (first time)       → SceneLoader.Load("WelcomeInitScene")

WelcomeInitScene (build index 3)
  └─ 3-panel flow (WelcomeSceneFlow):
        Panel 1: Choose Avatar (carousel, 4 options: Fire/Water/Air/Earth)
        Panel 2: Choose Username (TMP_InputField, 20 char max)
        Panel 3: Confirming (spinner + status text)
  └─ WelcomeInitController.CompleteSetup()
        └─ sends initialize_user tx → Anchor program → on-chain confirmed
              └─ SceneLoader.Load("HomeScene")

HomeScene (build index 4)
  └─ HomeSceneManager.InitialiseAsync()
        └─ _fadeOverlay alpha=1 (world hidden during load)
        └─ _statusText shows loading steps: "Fetching character…" → "Spawning avatar…" → "Welcome, {name}!"
        └─ NereonClient.FetchCharacterStatsAsync() + FetchUserProfileAsync() (4 retries)
        └─ AvatarManager.LoadAvatarAsync() — spawns Invector avatar at SpawnPoint
        └─ SkyboxController.Apply(WorldVariant.skyboxMaterial)
        └─ HomeSceneCinematic.PlayAsync() — optional bird-eye → player sweep
        └─ NereonNetworkManager.ConnectAsync() — UGS Relay+Lobby, NGO multiplayer
        └─ PlayerInput unlocked, player in control
        └─ FadeOverlay fades to 0 LAST — world only revealed after all data loaded + avatar spawned
  ⚠️ Session 13 fix: FadeOutAsync(_fadeOverlay) moved from START → END of InitialiseAsync().
     World was previously visible for 6+ seconds while data loaded — now stays black until ready.

LeaderboardScene (build index TBD)
  └─ Accessed from HomeScene options menu → "Leaderboards"
  └─ Full historical leaderboard view (all months, all games)
  └─ Filter by game, month, year
  └─ Back button → SceneLoader.Load("HomeScene")

[Mini-Game Scene]
  └─ Loaded by BuildingInteraction (level-gated)
  └─ MiniGameContext stores current gameId
  └─ On finish: submit_score tx → SceneLoader.Load("HomeScene")

content_copy
Build Settings
Index	Scene	Status
0	ExampleScene	❌ Disabled
1	test.unity	❌ Disabled
2	LandingScene	✅ Enabled
3	WelcomeInitScene	✅ Enabled
4	HomeScene	✅ Enabled
5	LeaderboardScene	🔲 Not yet created

content_copy
Scene Transitions — SceneLoader
SceneLoader is a DontDestroyOnLoad singleton:

Fades screen to solid black (0.3s) → loads scene async with ThreadPriority.High → fades back in (0.4s)
Spinner + "LOADING..." label (dot animation) visible during load
Post-activation frame wait: 15 frames (was 4) to survive Unity's main-thread shader compilation stall
No prefab dependency — built entirely in code; nothing to wire in Inspector
Call from anywhere: SceneLoader.Load("HomeScene")
⚠️ Session 13 fix: Spinner was freezing because Unity's scene activation stall blocks Update().
   Frame buffer increased from 4 → 15. LOADING... label with animated dots added below spinner.
🎮 Current Architecture (What Is Actually Built)
Camera — SimpleFollowCamera.cs
Attached to Main Camera in HomeScene. No Cinemachine dependency.
Fixed overhead camera — MU Dark Awakening style. The camera is locked at a fixed pitch and yaw; it does NOT rotate with player input.

Field	Default	Description
_pitchAngle	55°	Degrees above horizon looking down (50-60 = MU DA feel)
_yawAngle	45°	World-space compass angle the camera orbits FROM
_followDist	10 m	Orbital distance from player
_lookOffsetY	1.2 m	Aim height above player feet
_smoothSpeed	10	Position lerp speed
_minDist	5 m	Closest zoom distance (scroll wheel)
_maxDist	20 m	Furthest zoom distance (scroll wheel)
_zoomSpeed	4	Scroll wheel sensitivity

content_copy
Scroll wheel zooms in/out. Camera tracks player root (NOT PlayerCameraRoot — the fixed camera ignores Invector's free-rotate target).
SphereCast pulls the camera forward if geometry is between it and the player.

⚠️ Cinemachine is installed (com.unity.cinemachine 3.1.2) but is NOT used for
the main player follow camera. It exists for assets that depend on it (3DGamekitLite, etc.).
Do NOT add Cinemachine Virtual Cameras for the player — SimpleFollowCamera handles it.

⚠️ PlayerCameraRoot is still present on avatar prefabs (Invector requires it) but SimpleFollowCamera
no longer uses it. The camera now follows the player's root transform with a fixed orbital angle.

Character Controller — Invector Third Person Controller
The player avatar uses the Invector locomotion system (vThirdPersonInput, vThirdPersonController,
vThirdPersonAnimator). All avatars share this system — it is added at runtime by AvatarManager,
not baked into individual prefabs.

Invector is the backbone of the entire player experience — movement, actions, and mobile input
all flow through it. Mobile UI buttons/joystick feed into Invector via NereonMobileInput.cs.

Avatar System
Four avatar types stored as prefabs at Assets/_NEREON/Prefabs/Avatars/:

avatar_id (on-chain)	Prefab	Class Name
0	Fire_Avatar.prefab	Fire
1	Water_Avatar.prefab	Water
2	Air_Avatar.prefab	Air
3	Earth_Avatar.prefab	Earth

content_copy
Runtime spawn flow (all in AvatarManager.LoadAvatarAsync()):

Reads profile.AvatarId → looks up prefab in AvatarRegistry.asset
Instantiates at SpawnPoint transform
Adds PlayerSetup, NereonMobileInput (runtime — not in prefab)
Calls PlayerSetup.SetupAsLocalPlayer() (warps to terrain, locks cursor)
Adds SnapToTerrain component (polls terrain height, snaps Y)
Adds AvatarProgressionEffects → applies level-gated cosmetics
Creates PlayerWorldUI child GO → displays name + level above head
Calls WireCameraToAvatarAsync() → SimpleFollowCamera locks onto PlayerCameraRoot
Avatar prefab requirements:

Visual-only: mesh + Animator + AvatarProgressionEffects (no controller scripts)
Must have a child named PlayerCameraRoot (Invector target for camera)
Invector components are added at runtime by AvatarManager
Tag set to "Player" by AvatarManager after spawn
AvatarRegistry.asset lives at Assets/_NEREON/Data/AvatarRegistry.asset.
Right-click → Create → NEREON → Avatar Registry to create a new one.

Skybox — SkyboxController.cs + WorldVariant
Skybox is driven by static Material references — no Blockade Labs API calls at runtime.

WorldVariant (ScriptableObject)
  └─ skyboxMaterial: Material  ← just a Material reference
SkyboxController.Apply(material)
  └─ RenderSettings.skybox = material

content_copy
WorldVariantRegistry holds 5 biome variants. WorldSyncManager picks the active biome
based on (Year * 12 + Month) % 5 — automatically rotates monthly.

⚠️ The BlockadeLabs-SDK-Unity package is installed but the API integration was
removed from the live code. SkyboxController now only calls RenderSettings.skybox = material.
Do not add API-based skybox generation without explicit decision to do so.

HomeScene skybox — auto-setup flow:
  HomeSceneManager.Start() auto-creates SkyboxController if not in scene.
  SkyboxController.ApplyDefault() priority order:
    1. _defaultSkybox (Inspector) → applied immediately
    2. fantasySkyDay (Inspector) → applied immediately
    3. Resources.Load<Material>("Skyboxes/FS013_Day") → works in builds
    4. AssetDatabase.LoadAssetAtPath (Editor-only) → auto-loads FS013_Day.mat
    5. Procedural sky shader → final fallback

  ✅ For Editor: works automatically (AssetDatabase loads FS013_Day in step 4).
  ✅ For builds: run NEREON → Setup → Wire Skybox (Resources) ONCE.
    Copies all FS013 variants → Assets/_NEREON/Resources/Skyboxes/
    Also wires SkyboxController Inspector fields in the open scene.

Fantasy Skybox FREE — FS013 variants (panoramic, URP-compatible):
  FS013_Day.mat         → bright fantasy day sky (default)
  FS013_Night.mat       → deep night, stars
  FS013_Sunrise.mat     → golden morning
  FS013_Sunset.mat      → warm amber dusk
  FS013_Rainy.mat       → overcast / rain
  FS013_Snowy.mat       → winter / snow
  FS013_Day_Sunless.mat → cool overcast day
  FS013_Night_Moonless.mat → moonless night

Use SkyboxController.ApplyTimeOfDay("day"|"night"|"sunrise"|"sunset"|"rainy"|"snowy").

5 Biomes:

Biome Asset	Seed	Notes
SpringForest.asset	11111	
AutumnHighlands.asset	22222	
WinterPeaks.asset	33333	
SummerSavanna.asset	44444	
StormyCoast.asset	55555	

content_copy
Create biome assets: NEREON → Create World Variant Assets

Floating Name Tag — PlayerWorldUI.cs
Screen-space name tag above every player (local + remote). Built entirely at runtime — no prefab needed.

Render mode: ScreenSpaceOverlay canvas (sortingOrder=50) — NOT world-space.
Positioning: Camera.WorldToScreenPoint(avatar.position + up*2.2m) → RectTransformUtility.ScreenPointToLocalPointInRectangle() → _namePanelRT.localPosition
Pivot: (0.5, 0) on name panel — tag sits above the projected head point.
Name: white, bold, 15pt; Level: light-blue, 12pt; both in a dark semi-transparent 170×32 panel.
Chat bubble (190×50): shown on ShowMessage(), auto-hides after 10s + 1s fade.
⚠️ Always flat 2D — never tilts at any camera angle. Do NOT revert to world-space canvas or transform.rotation = _cam.rotation.
⚠️ FloatingNameTag.cs is an older, simpler alternative that still exists in the project.
Use PlayerWorldUI for all new work. AvatarManager creates PlayerWorldUI, not FloatingNameTag.

Terrain — Hand-Crafted
The terrain is hand-crafted and finalised — no procedural generation at runtime.

Property	Value
World size	~1500 × 1500 units
Terrain type	Unity Terrain (manually sculpted)
Safety ground	Flat MeshCollider plane at Y = -2 (fallback)

content_copy
⚠️ MapMagic 2 has been fully removed from the project. All terrain is now static/hand-built.
Do NOT add MapMagic or any procedural terrain generation.

Player spawn fix: SnapToTerrain.cs polls Terrain.SampleHeight() every 100ms until
terrain is ready, then snaps player to exact surface Y. CharacterController disabled during poll.

Notice Board — World-Space Leaderboard (HomeScene)
A physical 3D notice board prop placed near the Central Plaza / SpawnPoint area.

World-space canvas attached to a 3D notice board mesh
Displays current month only — latest leaderboard data
Reads GameLeaderboard PDAs on-chain for each game_id
Shows top-5 players per game with scores
Player approaches → board is readable at close range
Minimal, clean UI that fits the village toon aesthetic
Implemented by NoticeBoard.cs
For historical leaderboard data (past months, detailed stats), see LeaderboardScene below.

LeaderboardScene — Dedicated Leaderboard View
A separate scene accessible from the HomeScene options/pause menu.

Shows all months (historical data), not just current
Filter/browse by game, month, year
More detailed stats and player information than the village notice board
Back button returns to HomeScene via SceneLoader.Load("HomeScene")
Build index: TBD (next available after HomeScene)
Multiplayer — NGO + Unity Relay + Lobby
Component	Role
NereonNetworkManager.cs	UGS init, anonymous sign-in, Lobby join/create, Relay setup, NGO Host/Client
HubPlayerNetwork.cs	NGO NetworkBehaviour — syncs AvatarId, Level, Username; chat ServerRpc/ClientRpc
RemotePlayerVisuals.cs	Spawns correct avatar for remote players from synced NetworkVars
BubbleChat.cs	World-space speech bubbles above head, stack + fade
ChatInputUI.cs	Press T to open, Enter to send, Escape to cancel

content_copy
HubPlayer prefab at Assets/_NEREON/Prefabs/HubPlayer.prefab:

HubPlayer (root)
├── CharacterController
├── NetworkObject
├── NetworkTransform          ← position/rotation sync
├── HubPlayerNetwork          ← AvatarId, Level, Username, chat
├── RemotePlayerVisuals       ← wire: AvatarRegistry, NameTagPrefab, ModelAnchor
├── BubbleChat                ← wire: BubbleContainer, BubblePrefab
├── PlayerSetup
├── ModelAnchor               (empty GO at 0,0,0)
└── BubbleContainer           (empty GO at 0, 2.8, 0)

content_copy
⚠️ Use com.unity.services.lobby + com.unity.services.relay separately.
Do NOT install com.unity.services.multiplayer — it bundles websocket-sharp-latest.dll
which conflicts with the Solana SDK's websocket-sharp.dll.

World variant sync: Host writes worldVariantId (int) to Lobby data.
Clients read it on join → same skybox and biome visuals on all clients.

Ambient Music — AmbientMusicManager + HomeAmbientManager
AmbientMusicManager.cs: DontDestroyOnLoad singleton, Landing + WelcomeInit (vol 0.4, 2s fade)
HomeAmbientManager.cs: HomeScene-only, random track from pool, never repeats consecutive
Current track: Assets/_NEREON/Sounds/Marconi Union - Weightless...mp3
Setup: NEREON → Setup → Wire Ambient Music (runs the Editor script automatically)
Cinematic Intro — HomeSceneCinematic.cs
Optional ~14s sequence on HomeScene load. Attach to Main Camera.

Black screen (world loading)
High-altitude orbital pan over map (~5s) — "NEREON" + biome name cards
Bezier descent to player position (~5s) — "A New World Has Emerged..."
Hand-off to SimpleFollowCamera (~2s) — "Welcome, {PlayerName}"
Player input unlocked
Wire: _followCamera (SimpleFollowCamera on Main Camera), three TMP labels, two CanvasGroups.
HomeSceneManager calls await _cinematic.PlayAsync(username, biomeName) after avatar spawns.

World Districts & Buildings
District	Building Name	game_id	Min Level
Market Bazaar	The Coin Flip	0	1
Market Bazaar	The Card Table	1	1
Mystic Quarter	The Puzzle Tower	2	3
Mystic Quarter	The Oracle	3	5
Champion Arena	The Arena	4	8

content_copy
Level gating: BuildingInteraction.cs checks HomeSceneManager.CachedStats.Level (from on-chain,
loaded at scene start). Client-side UX only — actual cheat prevention is in submit_score on-chain.

WorldConfig.asset at Assets/_NEREON/Data/WorldConfig.asset:
Right-click → Create → NEREON → World Config. All building definitions are data-driven here.

Building world positions (XZ, terrain Y auto-snapped at runtime):

Building	Position
The Coin Flip	(-55, 0, -25)
The Card Table	(-35, 0, -55)
The Puzzle Tower	(45, 0, 55)
The Oracle	(65, 0, 30)
The Arena	(55, 0, -50)
SpawnPoint (Central Plaza)	(0, 0, 0)
Notice Board	Near SpawnPoint (~5-10 units from Central Plaza)

content_copy
🔗 On-Chain Architecture (Deployed)
Program ID: 4cPPQDNMuwNnXRaNHxvo2gyDhwue1bE8MLzSW3VqcS4o (Solana Devnet)
IDL Account: 5aUkwLTHGoVYvB71Xzm6CS1V4vjBCt9RjvqqnuUEeDqc
Deploy wallet: GEsk7ishSsqmKebunMUWcPeV3GWfh2CpnLa6ErhMeZsh

PDAs
wallet_pubkey
  ├── UserProfile      seeds: ["user_profile", wallet]
  │     authority, avatar_id (u8), username ([u8;32]), created_at, bump
  │
  └── CharacterStats   seeds: ["character", wallet]
        authority, level (u16), xp (u32), games_played (u32), bump

game_id (u8)
  └── GameLeaderboard  seeds: ["leaderboard", game_id, month_year_str]
        top_entries: [(player: Pubkey, score: u32); 5], reward_distributed: bool, bump

RewardEscrow           seeds: ["escrow"]
  holds_spl_tokens: u64

content_copy
Instructions
Instruction	Caller	Action
initialize_user	First-time player	Creates UserProfile + CharacterStats PDAs
update_profile	Player	Updates avatar_id / username
submit_score	Player (post mini-game)	Updates leaderboard; awards XP; levels up character
distribute_monthly_rewards	Crank / keeper bot	Pays top-5 from RewardEscrow; marks month closed
fund_escrow	Protocol / admin	Deposits NEREON tokens into RewardEscrow

content_copy
Unity ↔ Anchor Bridge — NereonClient.cs
Located at Assets/_NEREON/Scripts/generated/NereonClient.cs.
Do not hand-edit. Re-generate from IDL if Anchor program changes.

Key methods:

NereonClient.IsUserInitializedAsync(PublicKey wallet)       // → bool
NereonClient.FetchCharacterStatsAsync(PublicKey wallet)     // → CharacterStatsData
NereonClient.FetchUserProfileAsync(PublicKey wallet)        // → UserProfileData
NereonClient.BuildInitializeUserIx(PublicKey wallet, byte avatarId, string username)
NereonClient.DecodeUsername(byte[] usernameBytes)           // → string

content_copy
📜 Complete Script Reference
Scene Management
Script	Location	Purpose
SceneLoader.cs	Scripts/	DontDestroyOnLoad singleton. Fade-to-black + spinner. Call SceneLoader.Load("name"). No prefabs.
LoginFlowController.cs	Scripts/	LandingScene. Wallet login → on-chain check → route to Home or Welcome.
WelcomeInitController.cs	Scripts/	WelcomeInitScene. Sends initialize_user tx. Status → WelcomeSceneFlow.
WelcomeSceneFlow.cs	Scripts/	3-panel onboarding flow UI (avatar select → name entry → confirming).
NereonAuthGuard.cs	Scripts/	Redirects to LandingScene if no wallet on any scene.
NereonWalletPersist.cs	Scripts/	DontDestroyOnLoad — keeps wallet session across scenes.

content_copy
HomeScene Core
Script	Location	Purpose
HomeSceneManager.cs	Scripts/	Hub world controller. On-chain data fetch, avatar spawn, skybox, multiplayer init, cinematic.
AvatarManager.cs	Scripts/	Spawns local player avatar. Adds Invector + PlayerSetup + PlayerWorldUI at runtime.
PlayerSetup.cs	Scripts/	Warps to SpawnPoint, locks cursor, exposes LocalPlayer singleton.
PlayerHUD.cs	Scripts/	Username / LVL / XP bar. Call Refresh(username, level, xp).
PlayerWorldUI.cs	Scripts/	World-space name tag above player. Centered, no background. Call Initialise(name, level).
SimpleFollowCamera.cs	Scripts/	Third-person follow camera. Scroll wheel zoom. No Cinemachine.
HomeSceneCinematic.cs	Scripts/	Optional bird-eye intro cinematic. Attach to Main Camera.
HomeSceneHUDFilter.cs	Scripts/	Hides Invector combat HUD panels in the social hub.

content_copy
World / Terrain
Script	Location	Purpose
WorldConfig.cs	Scripts/	ScriptableObject: all buildings, game IDs, positions, min levels, spawn point.
WorldVariant.cs	Scripts/	ScriptableObject: biome identity, skybox material.
WorldVariantRegistry.cs	Scripts/	Holds 5 WorldVariants. Returns active biome by month.
WorldSyncManager.cs	Scripts/	Applies active WorldVariant to skybox. Syncs biome via lobby.
SkyboxController.cs	Scripts/	Apply(Material) → RenderSettings.skybox. No API calls.
SnapToTerrain.cs	Scripts/	Polls terrain height; snaps attached GO's Y. Used by player spawn.
NereonWorldBoundary.cs	Scripts/	Invisible boundary colliders at world edges.

content_copy
Buildings & Interaction
Script	Location	Purpose
BuildingInteraction.cs	Scripts/	Level-gate check → show Enter/Lock prompt → load mini-game scene.
MiniGameContext.cs	Scripts/	Static: stores CurrentGameId between scenes.
ProximityInteractor.cs	Scripts/	Detects nearby IInteractable objects; shows "Press E" prompt.
IInteractable.cs	Scripts/	Interface all interactable buildings implement.
InfoPopupInteractable.cs	Scripts/	Interactable that shows a world-space info popup.
NoticeBoard.cs	Scripts/	Reads GameLeaderboard PDAs; displays current month top-5 per game on world-space canvas attached to 3D notice board prop.

content_copy
Avatar & Progression
Script	Location	Purpose
AvatarRegistry.cs	Scripts/	ScriptableObject: avatar_id byte → prefab + display name.
AvatarProgressionEffects.cs	Scripts/	Level-gated cosmetics: aura particles, trail, ground rune, TCP2 outline colour.
AvatarIdTag.cs	Scripts/	MonoBehaviour on avatar root: stores avatar_id for runtime lookup.
NereonAvatarSetup.cs	Scripts/	On avatar prefab: defines attachment points for cosmetics.
PlaceholderAvatarFactory.cs	Scripts/	Creates coloured capsule+sphere placeholder when no real model prefab is set.
AvatarSelector.cs	Scripts/	WelcomeInitScene carousel. Falls back to PlaceholderAvatarFactory.
FloatingNameTag.cs	Scripts/	Legacy — simple name billboard. Replaced by PlayerWorldUI. Keep, don't use for new work.

content_copy
Multiplayer
Script	Location	Purpose
NereonNetworkManager.cs	Scripts/	UGS + NGO lifecycle: init → sign-in → Lobby → Relay → NGO Host/Client.
HubPlayerNetwork.cs	Scripts/	NGO NetworkBehaviour: AvatarId/Level/Username sync; chat RPC.
RemotePlayerVisuals.cs	Scripts/	Spawns + updates avatar for remote players from synced NetworkVars.
BubbleChat.cs	Scripts/	World-space speech bubbles above head (stack + fade).
ChatInputUI.cs	Scripts/	Press T → input field → Enter to send → chat RPC.

content_copy
Audio
Script	Location	Purpose
AmbientMusicManager.cs	Scripts/	DontDestroyOnLoad. Landing + WelcomeInit background music.
HomeAmbientManager.cs	Scripts/	HomeScene music: random track from pool, no consecutive repeats.

content_copy
UI & Utilities
Script	Location	Purpose
UIAnimations.cs	Scripts/	Fade/slide/pop/typewriter animations. Uses DOTween if present, coroutines otherwise.
UISpinner.cs	Scripts/	Rotating arc spinner (used in WelcomeInit confirming panel).
PlayerHUD.cs	Scripts/	Top-left HUD: username, level badge, XP bar.
MobileHUDCanvas.cs	Scripts/	Runtime mobile joystick/button overlay.
NereonMobileInput.cs	Scripts/	Bridges MobileHUDCanvas input to Invector's vThirdPersonController.
JoystickWidget.cs	Scripts/	Virtual joystick implementation.
DebugUIController.cs	Scripts/	In-game debug overlay (Editor + DEBUG_NEREON builds only).
NereonCultureEnforcer.cs	Scripts/	[RuntimeInitializeOnLoadMethod] — forces InvariantCulture on all threads. Auto, no setup.
NereonOrientationGuard.cs	Scripts/	[RuntimeInitializeOnLoadMethod] — forces landscape before any scene loads. DontDestroyOnLoad singleton. Polls screen dimensions; shows blocking popup if portrait detected. Auto-dismisses when landscape confirmed.
NereonCanvasHelper.cs	Scripts/	Static utility. Setup(CanvasScaler) → 2400×1080 Seeker target, Match=0.5. CreateSafeContent(Transform) → RectTransform for safe-area content.
NereonSafeAreaFitter.cs	Scripts/	MonoBehaviour. Polls Screen.safeArea every frame; adjusts child RectTransform anchors. Attach to SafeContent child of any ScreenSpaceOverlay Canvas.
NereonToastService.cs	Scripts/	DontDestroyOnLoad toast singleton. Pyro integration + UGUI fallback. ShowSuccess/Info/Warning/Error(msg).

content_copy
Blockchain
Script	Location	Purpose
generated/NereonClient.cs	Scripts/	Solana/Anchor bridge. All PDA derivations, discriminators, ix builders. Do not hand-edit.

content_copy
Editor Tools (Assets/_NEREON/Scripts/Editor/)
Script	Menu Path	Purpose
NereonHomeSceneBuilder.cs	NEREON → Build HomeScene	Full HomeScene rebuild: districts, buildings, SafetyGround
NereonLandingSceneBuilder.cs	NEREON → Build LandingScene	LandingScene UI: logo, wallet button, Enter button
NereonWelcomeSceneBuilder.cs	NEREON → Build WelcomeScene	WelcomeInitScene: 3-panel canvas, preview stage, FlowManager
NereonWorldBuilder.cs	NEREON → Create World Variant Assets	Creates 5 biome ScriptableObjects + WorldVariantRegistry
NereonPlayerPrefabBuilder.cs	NEREON → Build Player Prefab	Creates Invector-based HubPlayer prefab
NereonAvatarPrefabBuilder.cs	NEREON → Build Avatar Prefabs	Creates Fire/Water/Air/Earth avatar prefabs
NereonSkyboxSetup.cs	NEREON → Setup → Wire Skybox (Resources)	Copies FS013 variants to Resources/Skyboxes/; wires SkyboxController Inspector fields
NereonAmbientMusicSetup.cs	NEREON → Setup → Wire Ambient Music	Wires AudioClips to AmbientMusicManager and HomeAmbientManager
NereonPackageFixer.cs	NEREON → Fix → ...	Various one-click fixes (avatar registry refs, bubble font, etc.)

content_copy
📦 Tech Stack
Layer	Technology	Notes
Blockchain	Solana	Devnet deployed
Smart Contract	Rust + Anchor 0.30.1	Deployed ✅
Unity SDK	Solana.Unity-SDK (MagicBlock)	git package
Wallet (mobile)	Mobile Wallet Adapter	Seeker Seed Vault compatible
Wallet (web/desktop)	Web3Auth	Google/Twitter social login
Character Controller	Invector Third Person	Backbone of player experience — runtime-added by AvatarManager
Camera	SimpleFollowCamera.cs (custom)	Scroll-wheel zoom, no Cinemachine dependency
Terrain	Hand-crafted Unity Terrain	Static, manually sculpted
Multiplayer	NGO 2.1.1 + Unity Relay + Unity Lobby	UGS packages
Async	UniTask (Cysharp)	All async/await in NEREON code uses UniTask
Animation	DOTween Pro	UIAnimations.cs; graceful fallback without it
Shaders	Toony Colors Pro 2	TCP2 toon shader on characters
Scene transitions	SceneLoader.cs (custom)	Spinner overlay, no external packages

content_copy
Installed Unity Packages
Package	Version	Notes
com.unity.cinemachine	3.1.2	Installed but NOT used for player camera
com.unity.netcode.gameobjects	2.1.1	NGO
com.unity.services.relay	1.0.3	Use this, NOT com.unity.services.multiplayer
com.unity.services.lobby	1.2.2	Use this, NOT com.unity.services.multiplayer
com.unity.services.authentication	3.3.4	UGS anonymous sign-in
com.unity.services.core	1.12.5	UGS core
com.unity.ai.navigation	2.0.10	NavMesh (compile errors patched)

content_copy
🎨 Asset Inventory
Already In Project
Asset	Status	Use
NatureStarterKit2	✅ In project	Trees (tree01-04), bushes — vegetation
StylizedRocksPackFREE	✅ In project	Rock props
3DGamekitLite	✅ In project	Enemy AI, melee (compile errors patched)
MaximeBrunoni	✅ In project	Building17th, Well — village buildings
LowpolyBakersHouse	✅ In project	Bakery building
PolyKebap	✅ In project	Tavern/food building
Skyden_Games	✅ In project	Interactive props
Vefects	✅ In project	VFX_Stylized_Fire, torch fire
Eric VFX Studio	✅ In project	FX_Fireball, FX_LootDrop, FX_LightPillar
SmoothShakeFree	✅ In project	Camera shake
Pyro Entertainment	✅ In project	Toast notifications (NereonToastService.cs — Pyro + UGUI fallback)
DialogGraphSystem	✅ In project	NPC/building dialog trees
Fantasy Skybox FREE	✅ In project	HomeScene skybox. Run NEREON → Setup → Wire Skybox (Resources) to populate Resources/Skyboxes/
Free_Casual_GUI (Skyden_Games)	✅ In project	Button/panel/HUD sprites. Demo/Sprites/Buttons/Buttons.png (atlas), HUD/HUD.png (atlas), Others/ (individual PNGs + 24 Shape sprites). SVGs require Vector Graphics package.
Action Icons	✅ In project	RPG icons. Technology 01 — Smartphone used in NereonOrientationGuard overlay.
2D Game UI KIT (300Mind)	✅ In project	UI-pack_Sprite_1/2.png sprite atlases + Oswald/GROBOLD fonts for headers.
JazzCreate BubbleFontFree	✅ In project	JazzCreateBubble SDF — default TMP font
BlockadeLabs-SDK-Unity	✅ In project	Not used at runtime — package present only
Viking Village	✅ In project	Environment dressing, water shaders
Invector Third Person Controller	✅ In project	Active player controller — backbone
Toony Colors Pro 2	✅ In project	Toon shader
DOTween Pro	✅ In project	UIAnimations.cs

content_copy
🎫 NEREON Pass NFT System (Seeker dApp Store Monetisation)
Inspired by the "Astrolab" / Astro Shooter model on Seeker. NEREON uses a Pass NFT to
gate premium access — no central subscription, fully on-chain ownership.

Monetisation Flow
┌─────────────────────────────────────────────────────────┐
│  User opens app → LandingScene load                     │
│  → NereonPassGatePopup appears IMMEDIATELY              │
│     (before wallet login prompt)                         │
│                                                         │
│  ┌─────────────────┐   ┌──────────────────────────────┐ │
│  │  🏆 MINT PASS   │   │  🎮 PLAY FREE (with ads)     │ │
│  │  0.5 SOL        │   │  Close popup → sign in →     │ │
│  │  → sign in →    │   │  enter NEREON normally        │ │
│  │  → mint tx →    │   └──────────────────────────────┘ │
│  │  → Pass minted  │                                    │
│  └─────────────────┘                                    │
└─────────────────────────────────────────────────────────┘

"Play Free" OR closing the popup = same action: dismiss, proceed to normal login.
Mint Pass = triggers wallet login FIRST, then the 0.5 SOL mint transaction.

Pass Holder vs Free Player
Feature                     Pass Holder         Free Player
─────────────────────────── ─────────────────── ───────────────────────────
Reward claims               Instant (tap Claim) Must watch Unity Ad first
Ad frequency                Zero                Rewarded ad per claim
Pass indicator on HUD       ✅ Gold badge        ❌ None
Secondary market            Tradeable NFT       N/A

On-Chain Implementation
Option A (recommended first): Custom Anchor PDA — simpler, faster to ship
  mint_nereon_pass(price_lamports: u64) instruction:
    - Transfers SOL from user wallet → treasury wallet
    - Creates NereonPassAccount PDA: seeds=["nereon_pass", wallet]
      { holder: Pubkey, minted_at: i64, is_active: bool, bump }
  check_pass: NereonClient.HasPassAsync(PublicKey) → bool
  No Metaplex dependency; check is a single getProgramAccount call.

Option B (phase 2): Upgrade to full Metaplex NFT with visual art, tradeable on
  Magic Eden / Tensor. Uses Candy Machine v3 for mint. Adds collection-level royalties.
  Unity checks: getTokenAccountsByOwner filtered by NereonPassCollectionMint.

Pass Popup Trigger Rules
  AUTO (first open):  Popup appears automatically when player_tier == 0 (unset) — i.e. first-time
                      users who have never chosen. Appears before wallet login panel.
  MANUAL (returning): A "Upgrade to Pass" button is always visible on LandingScene (bottom of screen
                      or options area). Tapping it reopens the popup at any time — lets free players
                      upgrade later without needing to uninstall. Button is always present but changes
                      label depending on session state:
                        • player_tier == 0  → "GET THE PASS" (gold, prominent)
                        • player_tier == 1  → "Upgrade to Pass" (subtle, secondary)
                        • player_tier == 2  → "Pass Active ✓" (disabled/greyed, no action)
  Implementation note: NereonPassPopup.cs exposes a static Show() method so both triggers
                       (auto on Awake + manual button click) call the same code path.

Unity Implementation Plan
  NereonPassPopup.cs          — ✅ Script done. MonoBehaviour on PassPopUP_Panel. Self-as-backdrop (panel IS fullscreen dark overlay). Wires own buttons by name. BtnPassPopUp auto-wired. Auto-shows if tier==0. ⚠️ Component not yet added to PassPopUP_Panel in Inspector — add via Add Component → NereonPassPopup.
  NereonPassSession.cs        — ✅ DONE. PlayerPrefs tier cache (tier 0=unset/1=free/2=pass). SetTier(), HasChosenTier, HasPass.
  NereonPassChecker.cs        — Async check: HasPassAsync(pubkey) → bool (cached per session)
  NereonPassMinter.cs         — Builds + sends mint_nereon_pass tx via NereonClient
  NereonAdManager.cs          — Unity Ads rewarded ad wrapper; Claim() = watch ad → callback
  RewardClaimController.cs    — Orchestrates claim: HasPass? → instant : watch ad → claim

Ad Network: Unity Ads (com.unity.ads)
  Rewarded ad shown only at reward-claim time (never interstitials unprompted).
  Seeker / Android dApp Store policy: user-initiated only.
  Implementation: Advertisement.Load(adUnitId) + Advertisement.Show(adUnitId, listener)

content_copy
🌐 Seeker Ecosystem Features (Astrolab-Inspired)
Four features from Astrolab's successful Seeker apps to shadow:

1. Rent Reclaim (SOL Recovery)
   Users close empty token accounts (ATAs) to reclaim ~0.002 SOL each.
   Script: NereonRentReclaimer.cs
   Menu location: Options → "Reclaim SOL" → lists empty ATAs → user selects → closeAccount tx
   Value: tangible financial return keeps users opening the app daily.

2. Activity Ring Heartbeat
   Seeker users need on-chain activity to qualify for $SKR airdrop tiers.
   Script: NereonActivityPulse.cs
   Behaviour: On HomeScene entry + every 10 min session → send 1 low-cost memo-program tx.
   Tx content: memo="NEREON:heartbeat:{pubkey_short}" — costs ~5000 lamports (0.000005 SOL).
   Result: Every NEREON session counts as high-frequency "active use" in the Seeker ecosystem.

3. Seeker ID (.skr Domain) on Leaderboards
   Instead of username, resolve the wallet's .skr domain name automatically.
   Script: SeekerIdResolver.cs
   API: SNS (Solana Name Service) — resolve .skr TLD for a given PublicKey.
   Fallback: If no .skr domain, use the on-chain NEREON username as before.
   Display: Leaderboard rows show ".skr" badge next to resolved names.

4. Double-Tap Seed Vault Signing (Biometric UX)
   Already implemented via BiometricAuthManager.cs + Mobile Wallet Adapter.
   Enhancement: all reward-claim transactions use the native Seed Vault flow
   (not Web3Auth popup) so the UX feels like a system-level utility.

content_copy
🗺️ Roadmap (Current Status)
✅ Phase 1 — Toolchain (COMPLETE)
Rust, Solana CLI v3.0.15, AVM v0.32.1, Anchor 0.30.1 installed
✅ Phase 2 — Anchor Program (COMPLETE)
initialize_user, update_profile, submit_score, distribute_monthly_rewards
Deployed to Devnet: 4cPPQDNMuwNnXRaNHxvo2gyDhwue1bE8MLzSW3VqcS4o
✅ Phase 3 — Unity ↔ Anchor Bridge (COMPLETE)
NereonClient.cs — all PDAs, discriminators, instruction builders verified against deployed IDL
✅ Phase 4 — Login Flow (COMPLETE)
LoginFlowController — on-chain PDA check, zero PlayerPrefs routing
WelcomeInitController — sends real initialize_user tx; PlayerPrefs is display cache only
✅ Phase 5 — Hub World Scripts (COMPLETE)
All runtime scripts written and tested
Scene builders (Editor tools) for all 3 scenes
Avatar system, progression effects, world variant biomes
Multiplayer (NGO + Relay + Lobby)
Ambient music, cinematic intro, bubble chat
🔧 Phase 5B — Scene Polish (IN PROGRESS)
 Link Unity Project ID — Edit → Project Settings → Services ✅
 Register HubPlayer.prefab in NGO NetworkManager prefabs list
 Wire Inspector refs for HomeSceneCinematic (3 TMP labels + 2 CanvasGroups)
 Wire WorldSyncManager in HomeScene Inspector
 Place Notice Board 3D prop near SpawnPoint with world-space leaderboard canvas
 Test multiplayer: 2 clients same lobby → same terrain → chat working
 ✅ Fix SceneLoader spinner freeze (frame buffer 4→15 + LOADING... label)
 ✅ Fix HomeSceneManager loading order (FadeOverlay moved to end; status text steps)
 ✅ Fix PlayerWorldUI billboard tilt (cylindrical Y-axis; no camera-pitch copy)
 ✅ Redesign SimpleFollowCamera to fixed overhead (MU Dark Awakening style; pitch=55, yaw=45)
 ✅ Redesign PlayerHUD (dark panel, gold portrait ring, level badge, XP fill bar)
 ✅ Redesign MobileHUDCanvas (circular buttons + visible joystick ring/knob + toon colour palette)
 ✅ Fix PlayerHUD anchor — panel appearing center-screen (removed `if (_usernameText == null)` guard; always calls BuildPanel(); also destroys pre-existing canvas child to prevent duplicates)
 ✅ Fix PlayerWorldUI scale — CanvasScale 0.01→0.005, _heightOffset 2.1→1.0, name font 22→14pt, level font 13→10pt
 ✅ Fix SimpleFollowCamera to use PlayerSetup.LocalPlayer (not FindWithTag — no more remote player lock in multiplayer)
 ✅ Fix AvatarManager vThirdPersonCamera conflict — NereonCameraSetup component added to local player; PlayerSetup.WireCinemachineCamera() now disables instead of wires; vThirdPersonInput.tpCamera nulled + lockCameraInput=true; 3-layer defence against frame-timing races
 ✅ Fix AudioListener duplicate warning — NereonCameraSetup now removes AudioListener from player in Start() (player prefab has one, Main Camera has one → was causing "2 audio listeners" warning)
 ✅ Fix AudioListener spam — PlayerSetup.SetupAsLocalPlayer() now removes duplicate AudioListener immediately on spawn (before NereonCameraSetup fires at order 5000), eliminating per-frame spam
 ✅ Fix avatar walking-on-air after jump — NereonCameraSetup no longer scales castLengthGrounded by ×scale. At ×3 scale the cast reached 9 m, hitting terrain while airborne → Invector thought player was grounded → zero fall gravity → hover. castLengthGrounded kept at original value; only sphereCastRadius and castLengthAirborne are scaled.
 ✅ Fix TMP missing-glyph warnings — NereonToastService icon chars changed to ASCII (OK/X/i/!); overflow mode Ellipsis→Truncate; LoginFlowController status strings changed from Unicode … → ... and — → -
 ✅ Fix CS4014 unawaited async warning — LoginFlowController.OnReconnectClicked() LoginWalletAdapter call wrapped in #pragma disable CS4014
 ✅ Fix PlayerWorldUI CS0618 — enableWordWrapping replaced with textWrappingMode = Normal/NoWrap
 ✅ Fix WASD editor movement — NereonMobileInput keyboard fallback now runs before _active guard; movement injection also runs before guard; WASD works from first frame in editor without waiting for MobileHUDCanvas to link
 ✅ Fix PlayerHUD world-space inheritance bug — PlayerHUDCanvas was parented to PlayerHUD GO (inside [HUD Canvas] WorldSpace Canvas); Unity inherited WorldSpace render mode, making panel appear wrong position/size. Fixed by creating PlayerHUDCanvas as scene-root GO (no parent). Tracked via _canvasGO field, destroyed in OnDestroy().
 ✅ Fix camera "too far" — SimpleFollowCamera defaults tuned: _pitchAngle 55→45 (more forward-facing, character more visible), _followDist 10→6 (closer final view), _introStartDist 60→100 (dramatic zoom-in), _introZoomTime 2.5→3s, _lookOffsetY 1.2→1.5 (aim at torso/head)
 ✅ NereonPassPopup — MonoBehaviour on PassPopUP_Panel (child of LandingCanvas). Self-as-backdrop: panel IS the full-screen dark overlay (stretch anchors, semi-transparent Image, Button on root). Auto-wires close_btn/mintPass_btn/playFreeWithAdds_btn by name. BtnPassPopUp (scene button) auto-wired in Start(). SetActive(false) in Awake; shows in Start() if !HasChosenTier. Mint Pass → tier 2 + toast; Play Free → tier 1 + toast; backdrop tap or X → hide. ⚠️ STILL NEEDS: Add NereonPassPopup component to PassPopUP_Panel in Unity Inspector.
 ✅ NereonPassSession — PlayerPrefs tier cache. tier 0=unset (never chosen), 1=free, 2=pass holder.
 🔧 Wire Fantasy Wooden GUI sprites into PlayerHUD + MobileHUDCanvas via [SerializeField] fields
 🔧 True circular button shapes (runtime Texture2D circle sprites, or Fantasy Wooden GUI sprites)
🔲 Phase 6 — Mini-Game Framework + Leaderboards
 IMinigame interface + scene template
 Score submission flow → submit_score tx → XP + level-up feedback UI
 MiniGamePortal.cs → load mini-game scene + store MiniGameContext.CurrentGameId
 LeaderboardScene — dedicated scene for historical leaderboard browsing (all months, all games)
 Leaderboard UI: filter by game, month, year; detailed player stats
 HomeScene options menu → "Leaderboards" navigation to LeaderboardScene
🔲 Phase 7 — NEREON Token
 Mint NEREON SPL token on Devnet
 Add fund_escrow instruction to Anchor program
 Test distribute_monthly_rewards end-to-end
🔲 Phase 8 — First Mini-Game
 Choose game type for The Coin Flip (building 0)
 Build mechanics (skill + luck, on-chain VRF for randomness)
 Integrate with score submission framework
🔲 Phase 9 — Seeker Submission
 End-to-end Devnet test (login → game → reward)
 Promote Anchor program to Mainnet
 **Register own Web3Auth account + clientId at dashboard.web3auth.io (MANDATORY before submission)**
 Test MWA on real Seeker hardware
 Submit via https://seeker.solana.com
⚠️ Architecture Gotchas — Important for Every Session
Gotcha	Detail
Seeker target resolution	2400×1080, 20:9 landscape, ~395 PPI. All CanvasScalers use NereonCanvasHelper.Setup() — NEVER hardcode 1080×1920 (portrait) or 1920×1080 (old standard). MobileHUDCanvas and PlayerHUD wrap all visual content in a SafeContent/NereonSafeAreaFitter child so controls avoid the punch-hole camera and Android gesture bars. MinimapController.Create(canvas, player, safeContent) accepts optional safe parent.
Cinemachine installed but unused	SimpleFollowCamera.cs handles the player follow camera. Do not add Cinemachine Virtual Cameras for the player.
BlockadeLabs SDK installed but not active	SkyboxController.Apply(Material) sets a static material reference. No API calls at runtime.
Loading screen package exists but unused	Assets/Loading screen package/ remains in project. SceneLoader.cs uses only its own built-in spinner — it does NOT load the NereonLoadingScreen prefab.
FloatingNameTag.cs is legacy	PlayerWorldUI.cs is the current name-tag system. AvatarManager creates PlayerWorldUI, not FloatingNameTag.
wallet_scene 1.unity	Old/unused scene from early development. Do not use.
PlayerPrefs rule	PlayerPrefs is only ever used as a display cache (show name immediately after WelcomeInit while chain propagates). Never route scenes or gate actions on PlayerPrefs.
UniTask everywhere	All async code in _NEREON/Scripts/ uses UniTask / UniTaskVoid. Never use async Task or IEnumerator coroutines for blockchain calls.
No DOTween hard dependency	UIAnimations.cs detects DOTween at runtime via reflection. Always compile clean without DOTween.
Separate Lobby + Relay packages	Use com.unity.services.lobby + com.unity.services.relay. Never add com.unity.services.multiplayer (DLL conflict with Solana SDK).
InvariantCulture	NereonCultureEnforcer.cs forces InvariantCulture on all threads at startup. Never use float.Parse without CultureInfo.InvariantCulture — use NereonCultureEnforcer instead.
MapMagic 2 removed	Terrain is now hand-crafted. Do NOT add MapMagic or any procedural terrain generation back.
Starter Assets removed	Invector is the sole character controller. Do NOT add Unity Starter Assets ThirdPersonController.
Invector is backbone	All player movement, actions, and mobile input flow through Invector. NereonMobileInput.cs bridges touch UI to Invector.
PlayerWorldUI cylindrical billboard	LateUpdate: lookDir = (canvasPos-camPos).withY(0); rotation = Quaternion.LookRotation(lookDir, up). NEVER use transform.rotation = _cam.rotation — copies camera pitch, canvas tilts at overhead angles.
SimpleFollowCamera uses LocalPlayer	SimpleFollowCamera must resolve target via PlayerSetup.LocalPlayer first, NOT FindWithTag("Player") — FindWithTag can lock onto remote NGO players in multiplayer.
NereonCameraSetup.cs — camera ownership component	Added to local player by AvatarManager. [DefaultExecutionOrder(5000)] runs AFTER vThirdPersonInput.Start(). Nulls tpCamera field, sets lockCameraInput=true, disables vThirdPersonCamera component. Also removes duplicate AudioListener from player. castLengthGrounded is intentionally NOT scaled (see jumping gotcha). This is the definitive Invector camera kill — do not remove it.
vThirdPersonInput re-enables camera on Start()	vThirdPersonInput.Start() → CharacterInit() coroutine → FindCamera() → SetMainTarget() → Init() — this re-wires vThirdPersonCamera even if we disabled it earlier. NereonCameraSetup at order 5000 is the correct kill because it runs AFTER vThirdPersonInput.Start() (order 0).
AudioListener duplicate	Invector player prefab includes an AudioListener (so audio follows the character in third-person mode). Main Camera always has one too. PlayerSetup.SetupAsLocalPlayer() removes it IMMEDIATELY on spawn (before NereonCameraSetup fires). NereonCameraSetup Step 3 is a second-pass safety net. Main Camera is authoritative because SimpleFollowCamera keeps it near the player.
Avatar walking on air after jump	Root cause: NereonCameraSetup was scaling castLengthGrounded by ×localScale.y (×3 → 9 m). A 9 m cast still hits terrain when the player is 2-3 m above ground after a jump, so Invector marks the player "grounded" and removes fall gravity. FIX: only scale sphereCastRadius and castLengthAirborne; leave castLengthGrounded at its original Invector value. NEVER add motor.castLengthGrounded *= sy back.
NereonMobileInput WASD in editor	_active starts false; Activate() is called by MobileHUDCanvas.TryLinkPlayer(). The WASD keyboard block and movement injection run BEFORE the _active guard so editor testing works from the first frame. Sprint/jump/action (below the guard) still require _active=true.

content_copy
💬 Key Decisions Log
Date	Decision	Reason
2026-02-21	Anchor (Rust) for all on-chain logic	Industry standard; IDL → typed C# client
2026-02-21	PlayerPrefs removed from all core data	Seeker dApp store qualification requirement
2026-02-21	Mobile Wallet Adapter for Seeker	SDK includes MWA; Seed Vault is MWA-compatible
2026-02-21	Monthly top-5 auto-rewarded by smart contract	No central authority; fully trustless
2026-02-24	Unity Relay + Lobby (not multiplayer package)	Avoids websocket-sharp DLL conflict with Solana SDK
2026-02-24	World variant system (5 biomes, monthly rotation)	Free visual refresh every month without content update
2026-02-24	Static skybox materials (not Blockade Labs API)	No API key needed at runtime; fully offline capable
2026-02-24	Invector Third Person Controller (not Starter Assets)	Better Animator integration; AvatarManager adds at runtime
2026-02-24	SimpleFollowCamera (not Cinemachine)	No Cinemachine dependency; scroll-wheel zoom built-in
2026-02-27	SceneLoader: spinner-only (no loading screen package)	Loading screen package caused world-space banner bug in HomeScene; spinner is simpler and reliable
2026-02-27	PlayerWorldUI: no background panel, centered text	Background box was visually wrong; plain floating text looks cleaner above the player
2026-02-27	Hand-crafted terrain (MapMagic 2 removed)	Procedural terrain replaced with manually sculpted static terrain for full artistic control
2026-02-27	Starter Assets fully removed	Invector is the sole controller; no dual-controller confusion
2026-02-27	Notice Board: current month only (in village)	3D prop near SpawnPoint; historical data in separate LeaderboardScene
2026-02-27	LeaderboardScene: dedicated historical view	Separate scene for browsing past months, accessible from options menu
2026-02-28	Fixed overhead camera (MU Dark Awakening style)	Pitch=55°, yaw=45°, follows player root (not PlayerCameraRoot); no runtime rotation; better spatial awareness in hub world
2026-02-28	PlayerWorldUI: cylindrical Y-axis billboard	Replaces camera-rotation copy which caused canvas tilt at overhead angles; name tag now always world-upright
2026-02-28	SceneLoader: 15-frame post-activation buffer	Prevents spinner freeze during Unity's main-thread shader compilation stall on scene activation
2026-02-28	HomeSceneManager: FadeOverlay moved to end	World stays hidden until all on-chain data loaded + avatar spawned; prevents empty-world flash
2026-03-03	castLengthGrounded NOT scaled in NereonCameraSetup	At ×3 scale the 9 m cast hit terrain while player was airborne after a jump — Invector marked player grounded, gravity zeroed, avatar hovered. sphereCastRadius and castLengthAirborne scale correctly; castLengthGrounded must stay at original Invector value.
2026-03-03	AudioListener removal moved to PlayerSetup.SetupAsLocalPlayer()	NereonCameraSetup fires at DefaultExecutionOrder(5000) — too late; per-frame spam filled the log for ~20 frames. Removing the AudioListener synchronously in SetupAsLocalPlayer() eliminates the spam immediately on spawn.	All runtime CanvasScalers now use NereonCanvasHelper.Setup(): 2400×1080, Match=0.5, ScaleWithScreenSize. Seeker is 2400×1080 FHD+ 20:9. Safe area handled by NereonSafeAreaFitter on a SafeContent wrapper inside every HUD canvas — protects joystick, buttons, minimap, and HUD panel from punch-hole camera and gesture bars.
2026-03-02      ENTER NEREON button is mandatory — no auto-login bypass       Seeker dApp Store policy: user must always confirm entry. SDK silent session-restore shows Welcome Back panel but does NOT auto-load HomeScene. _userInitiatedLogin flag gates HandleLogin → OnWalletReady.
2026-03-02      Biometric auth before ENTER NEREON (Seeker requirement)        BiometricAuthManager.AuthenticateAsync() called in OnReconnectClicked. Required for Seeker dApp Store. Applies to both MWA and Web3Auth reconnect paths.
2026-03-02      Avatar 3× scale — stepOffset normalised in NereonCameraSetup  go.transform.localScale *= 3 inflates CC.stepOffset 3× in world space (0.9 m → avatar floats after jump). NereonCameraSetup (order 5000) divides stepOffset by scale.y to keep world step ≈ 0.3 m.
2026-03-02      Spine01 kinematic Rigidbody added by NereonCameraSetup         Invector ragdoll code does GetComponent<Rigidbody>() on spine bones and throws MissingComponentException when no ragdoll is set up. NereonCameraSetup.Start() adds a kinematic Rigidbody to Spine01 (no physics effect).
2026-03-02      Debug panel: compact tab button replaces full-width bar         DebugUIController redesigned: small 88×24 px tab button sits physically above the 210×118 panel. Button label is "⚙ DEBUG ▼/▲". Panel anchored at (0, -30) below tab.
2026-03-02      Minimap live via MobileHUDCanvas.TryLinkPlayer()                MinimapController.Create() called once player links. Buildings auto-register MinimapMarker(Building). Remote players auto-register MinimapMarker(OtherPlayer) in RemotePlayerVisuals.
2026-02-28      NereonCameraSetup.cs created — definitive Invector camera kill	vThirdPersonInput.Start() re-wires vThirdPersonCamera after any earlier disable. NereonCameraSetup at DefaultExecutionOrder(5000) runs after all default-order Start() methods, nulls tpCamera ref, sets lockCameraInput=true, disables component. 3-layer defence: PlayerSetup (early disable) + NereonCameraSetup (late Start kill) + WireCameraToAvatarAsync (1-frame insurance pass).
2026-02-28	PlayerWorldUI scale reduced (CanvasScale 0.005, heightOffset 1.0)	At overhead camera pitch=55° from 10m, 0.01 scale was too large. 0.005 + reduced font sizes gives clean compact tag.
2026-02-28	SimpleFollowCamera prefers PlayerSetup.LocalPlayer	FindWithTag("Player") could lock onto a remote NGO player. Now resolves LocalPlayer first, fallback to FindWithTag only.
2026-02-28	PlayerHUD always rebuilds in Awake()	Removed `if (_usernameText == null)` guard; always calls BuildPanel(). Destroys pre-existing canvas child first.
2026-03-02	NereonCameraSetup removes player AudioListener	Player prefab has AudioListener (Invector default); Main Camera also has one → Unity "2 audio listeners" warning. Since SimpleFollowCamera keeps Main Camera near player, player's listener is redundant. Destroyed in NereonCameraSetup.Start().
2026-03-02	NereonMobileInput WASD runs before _active guard	Editor testing convenience: keyboard movement works from first frame without waiting for MobileHUDCanvas to link. Sprint/jump/action remain guarded (require canvas link).
2026-03-02	HomeSceneCinematic._blackFade alpha fixed in Awake	Was set to 1f (black) in Awake — if cinematic not wired in HomeSceneManager, PlayAsync() never runs and screen stays permanently black. Fixed: set to 0f in Awake. PlayAsync() sets it to 1f itself at Phase 0 start.
2026-03-02	HomeSceneCinematic descent endPos fixed	Was using _cam.transform.rotation from orbital phase to compute follow-camera endPos — orbital rotation aims upward, giving an endPos above the player. Fixed: use Quaternion.Euler(45,45,0) * Vector3.back * 8 for a predictable close-up landing.
2026-03-02	NereonNetworkManager async null-guard	async void methods + Unity async = race condition: object destroyed while awaiting Relay/Lobby → MissingReferenceException on resume. Fixed: IsAlive() check after every await in CreateLobbyAndHostAsync and JoinLobbyAsync.
2026-03-02	Auth stack — NereonSessionManager + BiometricAuthManager	Session persistence (7-day expiry) via PlayerPrefs (public key + wallet type only — no private keys). Biometric gate uses OS BiometricPrompt on Android / LAContext on iOS — dApp never sees biometric data (Solana dApp Store policy compliant). LoginFlowController now shows "Welcome Back" panel for returning users.
2026-03-03NereonPassPopup redesign: self-as-backdrop patternPassPopUP_Panel IS the full-screen dark overlay (stretch to fill canvas, semi-transparent Image). No sibling backdrop GO needed. Button on root panel closes on tap-outside. WireSelfAsBackdrop() adds it in Awake(). Single SetActive call — simpler and fewer GOs.
2026-03-03NereonPassPopup: regular MonoBehaviour, not DontDestroyOnLoadPopup is LandingScene-only. DontDestroyOnLoad would survive into HomeScene. Simple MonoBehaviour on PassPopUP_Panel with static Instance ref, cleared on Destroy.
2026-03-03No .asmdef for _NEREON/Scripts — intentional for nowAll scripts share Assembly-CSharp. A compile error anywhere (MightyDevOps, etc.) blocks all scripts. Monitor compile times; add _NEREON.asmdef in a future cleanup sprint if needed.
Web3Auth clientId — PRODUCTION BLOCKER	⚠️ Current clientId (`BPi5PB_UiIZ...`) is the Solana Unity SDK **shared demo key** (network: SAPPHIRE_MAINNET). It works for development but is NOT suitable for production launch. Before Seeker submission: (1) Create account at dashboard.web3auth.io, (2) Register a new project, (3) Add custom redirectUrl matching your Android bundle ID (e.g. `torusapp://com.nereon.app/auth`), (4) Whitelist your redirect URIs, (5) Replace clientId in LandingScene Web3 component. The network value must match the registered project environment (SAPPHIRE_MAINNET for production).
Web3Auth network mismatch fix (Session 23)	Scene had `network: 4` (SAPPHIRE_DEVNET) but the demo clientId is only registered on `network: 5` (SAPPHIRE_MAINNET) — caused `"Init parameters not found... localStorage"` error on the Web3Auth login page. Fixed by setting network: 5 in LandingScene. Root cause: different network environments use separate localStorage buckets on the auth page.
LoginWeb3Auth task was silently dropped (Session 23)	`Web3.Instance.LoginWeb3Auth(Provider.GOOGLE)` returns `async Task<Account>` — calling it without `.AsUniTask().Forget()` let the state machine be GC'd before the OAuth browser opened. Fixed: all login calls now use `.AsUniTask().Forget(errorHandler)`. Same fix applied to `LoginWalletAdapter()` in reconnect path.
2026-03-04	Anchor lib.rs: player_tier, set_player_tier, game_name, entry_fee_lamports	Player pass tier (0/1/2) now stored in UserProfile on-chain. submit_score gates leaderboard slots to tier≥2. Entry fee SOL transfer to treasury added. game_name [u8;32] stored in GameLeaderboard.
2026-03-04	NereonClient.cs: SetPlayerTierAsync + updated BuildSubmitScoreIx	New async helper fires set_player_tier instruction. BuildSubmitScoreIx updated with game_name, entry_fee_lamports, treasury account, user_profile read account.
2026-03-04	NereonPassPopup: fires on-chain set_player_tier after local SetTier()	OnMintPass/OnPlayFree now call SetTierOnChainAsync(tier).Forget() immediately after NereonPassSession.SetTier(). Shows toast on confirm or warning on fail — local cache always kept.

content_copy
---

## 🌳 Technical Architecture Map
*Last updated: Session 24. AI agents: read this before any code change involving GameObjects, scenes, or script wiring.*

This section is the single authoritative tree of every scene, every relevant GameObject, every component, and every inter-script dependency in NEREON. Keep it updated whenever a new GameObject, component, or script relationship is added.

---

### Persistent Objects — DontDestroyOnLoad
These GameObjects survive ALL scene transitions. They are created once and never duplicated.

```
[DontDestroyOnLoad]
├── SceneLoader              (Scripts/SceneLoader.cs)
│     └─ [built at runtime by SceneLoader.Instance] — spinner overlay canvas
├── AmbientMusicManager      (Scripts/AmbientMusicManager.cs)
│     └─ AudioSource         ← LandingScene/WelcomeInit background music
├── DebugUI                  (Scripts/DebugUIController.cs)       [EDITOR / DEBUG_NEREON only]
│     └─ [builds DebugCanvas at runtime] ← ` + "`" + `~` + "`" + ` or F1/F2/F3 hotkeys
├── NereonCultureEnforcer    [RuntimeInitializeOnLoadMethod] — no GO, pure static
└── NereonOrientationGuard   [RuntimeInitializeOnLoadMethod] — spawns landscape-lock popup
```

---

### LandingScene  (build index 0)

```
LandingScene
├── EventSystem              [EventSystem, InputSystemUIInputModule]
├── Main Camera              [Camera, UniversalAdditionalCameraData, AudioListener]
├── [WalletController]       [MainThreadDispatcher, Web3]
│     ← INSPECTOR: Web3AuthWalletOptions.network = 5 (SAPPHIRE_MAINNET)
│     ← INSPECTOR: clientId = BPi5PB_UiIZ... (demo key — replace before production)
├── Directional Light        [Light, UniversalAdditionalLightData]
├── Global Volume            [Volume]
├── LandingCanvas            [Canvas, CanvasScaler, GraphicRaycaster]
│   ├── Logo / Title
│   ├── BtnConnectWallet     ← LoginFlowController._btnConnect
│   ├── BtnEnterNereon       ← LoginFlowController._btnEnterNereon / _btnReconnect
│   ├── StatusText           ← LoginFlowController._statusText
│   ├── SocialLoginPanel
│   │   └── BtnGoogleLogin   ← LoginFlowController._btnSocialLogin
│   └── PassPopUP_Panel      [Image(backdrop), Button(close), NereonPassPopup]
│       ├── close_btn
│       ├── mintPass_btn
│       └── playFreeWithAdds_btn
├── wallet_holder            [Canvas, CanvasScaler, WalletHolder, GraphicRaycaster]
│   ├── (wallet adapter panels — auto-managed by Solana SDK)
├── LoginManager             [LoginFlowController]    ← SINGLE instance (duplicate deleted Session 24)
└── AmbientMusicManager      [AudioSource, AmbientMusicManager]
```

**Script call chain (LandingScene):**
```
Web3.OnLogin event
  └─ LoginFlowController.HandleLogin(Account)
        ├─ NereonSessionManager.Save(pubkey, walletType, expiry)
        ├─ [if _userInitiatedLogin] → NereonClient.IsUserInitializedAsync()
        │     ├─ true  → SceneLoader.Load("HomeScene")
        │     └─ false → SceneLoader.Load("WelcomeInitScene")
        └─ [else] → show "Welcome Back" panel, wait for BtnEnterNereon tap

NereonPassPopup (Start, if tier==0)
  └─ SetActive(true) → show pass-choice overlay
        ├─ OnMintPass()  → NereonPassSession.SetTier(2) + SetTierOnChainAsync(2).Forget()
        └─ OnPlayFree()  → NereonPassSession.SetTier(1) + SetTierOnChainAsync(1).Forget()
              └─ NereonClient.SetPlayerTierAsync(tier) → set_player_tier on-chain tx
```

**⚠️ Inspector wiring required (LandingScene):**

| Field | On GO | Must point to |
|---|---|---|
| `_btnConnect` | LoginManager | LandingCanvas/BtnConnectWallet |
| `_btnEnterNereon` | LoginManager | LandingCanvas/BtnEnterNereon |
| `_btnSocialLogin` | LoginManager | LandingCanvas/SocialLoginPanel/BtnGoogleLogin |
| `_statusText` | LoginManager | LandingCanvas/StatusText |
| Web3 component | [WalletController] | network=5, redirectUrl, clientId |

---

### WelcomeInitScene  (build index 3)

```
WelcomeInitScene
├── EventSystem              [EventSystem, InputSystemUIInputModule]
├── Main Camera              [Camera, AudioListener]
├── WelcomeCanvas            [Canvas, CanvasScaler, GraphicRaycaster]
│   ├── Panel_AvatarSelect   ← WelcomeSceneFlow panel 1
│   │   └── AvatarCarousel   ← AvatarSelector.cs (reads AvatarRegistry)
│   ├── Panel_UsernameEntry  ← WelcomeSceneFlow panel 2
│   │   └── TMP_InputField
│   └── Panel_Confirming     ← WelcomeSceneFlow panel 3
│       ├── UISpinner
│       └── StatusText
├── WelcomeFlowController    [WelcomeInitController, WelcomeSceneFlow]
└── AvatarPreviewStage       (3D preview — rotates selected avatar model)
```

**Script call chain (WelcomeInitScene):**
```
WelcomeSceneFlow (3-panel UI nav)
  └─ Panel 3 confirm → WelcomeInitController.CompleteSetup(avatarId, username)
        └─ NereonClient.BuildInitializeUserIx(wallet, avatarId, username)
              └─ send tx → Anchor: initialize_user (creates UserProfile + CharacterStats PDAs)
                    └─ on confirmed → PlayerPrefs.SetString("NEREON_Username", username)
                          └─ SceneLoader.Load("HomeScene")
```

---

### HomeScene  (build index 4)

#### Static GameObjects (placed in Editor, persistent)

```
HomeScene
├── Main Camera              [Camera, UniversalAdditionalCameraData, AudioListener,
│                             SimpleFollowCamera, HomeSceneCinematic?]
│     └─ SimpleFollowCamera: auto-discovers via PlayerSetup.LocalPlayer (NOT FindWithTag)
├── Directional Light        [Light, UniversalAdditionalLightData]
├── GlobalVolume             [Volume]
│
├── [Player]                 (empty root — avatar spawned here by AvatarManager)
│   └── SpawnPoint           Tag="SpawnPoint"  ← WIRE to AvatarManager._spawnPoint
│
├── [Managers]
│   ├── HomeSceneManager     [HomeSceneManager]
│   │     ← INSPECTOR: _avatarManager → [Managers]/AvatarManager ✅ wired
│   │     ← INSPECTOR: _networkManager → [Managers]/NereonNetworkManager
│   │     ← INSPECTOR: _hud → (PlayerHUD, auto-spawns if null)
│   │     ← INSPECTOR: _fadeOverlay → (CanvasGroup on fade panel)
│   ├── AvatarManager        [AvatarManager]
│   │     ← INSPECTOR: _registry → Assets/_NEREON/Data/AvatarRegistry.asset  ⚠️ MUST WIRE
│   │     ← INSPECTOR: _spawnPoint → [Player]/SpawnPoint  ⚠️ MUST WIRE (currently fileID=0)
│   ├── NereonNetworkManager [NereonNetworkManager]
│   │     ← INSPECTOR: NetworkManager component (NGO)
│   └── WorldSyncManager     [WorldSyncManager]
│         ← INSPECTOR: _registry → Assets/_NEREON/Data/WorldVariantRegistry.asset
│
├── [HUD Canvas]             [Canvas (WorldSpace or ScreenSpace)]
│   └── PlayerHUD            [PlayerHUD]  ← Refresh(name, level, xp) called by HomeSceneManager
│
├── HomeAmbientManager       [HomeAmbientManager, AudioSource]
├── SafetyGround             [MeshFilter, MeshRenderer, MeshCollider] ← flat plane at Y=-2
│
├── [Village]
│   ├── Building_CoinFlip    [MeshFilter, MeshRenderer, BuildingInteraction, IInteractable]
│   ├── Building_CardTable   [BuildingInteraction]
│   ├── Building_PuzzleTower [BuildingInteraction]
│   ├── Building_Oracle      [BuildingInteraction]
│   └── Building_Arena       [BuildingInteraction]
│         BuildingInteraction: checks HomeSceneManager.CachedStats.Level → loads mini-game scene
│
├── NoticeBoard              [MeshFilter, MeshRenderer, NoticeBoard]
│     └─ world-space Canvas  ← reads GameLeaderboard PDAs; shows top-5 per game
│
├── [ScatterManager]         [TerrainScatterOnReady]
│     ← spawns tree/bush/rock instances on terrain at runtime (NatureStarterKit2 prefabs)
│
├── [Terrain]                [Terrain, TerrainCollider]
│     Size: ~1500×1500 units. Manually sculpted. Do NOT add MapMagic.
│
├── [TerrainProps]           (static rocks, road, river etc — scene-placed)
├── [Buildings]              (static building meshes — no scripts, visual only)
└── [Vegetation]             (static foliage — may be disabled if TerrainScatterOnReady handles it)
```

#### Runtime-Spawned GameObjects (created by scripts during play)

```
[RUNTIME — created in HomeScene after play begins]
│
├── SkyboxController         spawned by HomeSceneManager.Start() if not already in scene
│     [SkyboxController] → RenderSettings.skybox = WorldVariant.skyboxMaterial
│
├── MobileHUD                spawned by HomeSceneManager.Start()
│     [MobileHUDCanvas] → builds joystick + action buttons overlay canvas
│       └─ Calls NereonCanvasHelper.GetCircleSprite() for button shapes
│       └─ Calls MinimapController.Create() once player is linked
│
├── DebugUI                  spawned by HomeSceneManager.Start() [EDITOR only]
│     [DebugUIController] → DontDestroyOnLoad, collapsible debug tab
│
├── SceneLoader              spawned if not present from DontDestroyOnLoad
│
└── PlayerAvatar             spawned by AvatarManager.LoadAvatarAsync()
      └─ Parented under [Player] GO
      └─ Prefab: Assets/_NEREON/Prefabs/Avatars/<Name>_Avatar.prefab
            Components added at runtime by AvatarManager:
            ├── PlayerSetup              ← warps to SpawnPoint, locks cursor, LocalPlayer singleton
            ├── NereonMobileInput        ← bridges MobileHUDCanvas touch to Invector
            ├── SnapToTerrain            ← polls terrain height; keeps player on surface
            ├── AvatarProgressionEffects ← auras/trails/runes by level
            ├── NereonCameraSetup        [DefaultExecutionOrder(5000)] ← kills vThirdPersonCamera
            │     - Destroys ThirdPersonCamera child GO
            │     - tpInput.tpCamera = null, lockCameraInput = true, ignoreTpCamera = true
            │     - Removes duplicate AudioListener
            │     - Normalises CC.stepOffset / skinWidth / sphereCastRadius for 3× scale
            └── PlayerWorldUI            ← floating name+level tag above avatar head
```

---

### HomeScene — Full Initialisation Sequence (Frame-by-frame)

```
[Scene loads]
  ├── HomeSceneManager.Awake()       → Instance = this
  ├── HomeSceneManager.Start()
  │     ├─ auto-spawn SkyboxController (if missing)
  │     ├─ auto-spawn MobileHUD
  │     ├─ auto-spawn HomeSceneHUDFilter (hides Invector HUD)
  │     ├─ auto-spawn DebugUI (Editor only)
  │     └─ InitialiseAsync().Forget()
  │
  └── InitialiseAsync() [UniTaskVoid]
        ├─ fadeOverlay.alpha = 1 (world stays black)
        ├─ guard: Web3.Account == null → redirect to LandingScene
        ├─ await LoadOnChainDataAsync()
        │     ├─ NereonClient.FetchCharacterStatsAsync(wallet)  [4 retries × 1.5s]
        │     ├─ NereonClient.FetchUserProfileAsync(wallet)     [4 retries × 1.5s]
        │     ├─ fallback: PlayerPrefs "NEREON_Username" → local CachedStats/Profile
        │     ├─ no data + no prefs → SceneLoader.Load("WelcomeInitScene")
        │     ├─ _hud.Refresh(username, level, xp)
        │     ├─ AvatarManager.LoadAvatarAsync(profile, stats)
        │     │     ├─ Instantiate <AvatarId>_Avatar.prefab at spawnPos
        │     │     ├─ Parent under [Player] GO
        │     │     ├─ Add PlayerSetup → SetupAsLocalPlayer()
        │     │     │     ├─ PlaceAtSpawnPoint() → finds SpawnPoint tag → WaitForGroundAsync()
        │     │     │     │     └─ polls every 150ms up to 45s; Physics.Raycast → terrain Y
        │     │     │     │           → Teleport(cc, groundY+1.1m, rot) → cc.enabled=true
        │     │     │     ├─ WireCinemachineCamera() → disable vThirdPersonCamera early
        │     │     │     └─ DisablePlayerInput()    → locks movement during cinematic
        │     │     ├─ Add NereonMobileInput, SnapToTerrain, AvatarProgressionEffects
        │     │     ├─ SetupAsLocalPlayer() → PlayerSetup.LocalPlayer = this
        │     │     ├─ await UniTask.Yield()
        │     │     ├─ setup.EnablePlayerInput()  (safety unlock after 1 frame)
        │     │     ├─ Create PlayerWorldUI child GO
        │     │     └─ Add NereonCameraSetup (fires at order 5000 on next Start() pass)
        │     ├─ HomeSceneCinematic.PlayAsync() (optional, if wired)
        │     ├─ PlayerSetup.LocalPlayer.EnablePlayerInput() (final unlock)
        │     ├─ SetStatus("Entering NEREON…")
        │     ├─ farClipPlane = 4000 on all cameras
        │     └─ NereonNetworkManager.ConnectAsync(wallet)
        │           └─ UGS init → anonymous sign-in → Lobby create/join → Relay → NGO Host/Client
        │
        ├─ SkyboxController.Apply(WorldVariant.skyboxMaterial)
        ├─ UIAnimations.FadeOutAsync(fadeOverlay, 0.8s)  ← world revealed HERE
        └─ SetStatus("Welcome, {username}!")

[After Start() completes — NereonCameraSetup.Start() fires at order 5000]
  NereonCameraSetup
    ├─ Destroy vThirdPersonCamera child GO (permanent, survives UpdateCameraStates searches)
    ├─ tpInput.tpCamera = null, lockCameraInput = true, ignoreTpCamera = true
    ├─ Destroy duplicate AudioListener on player
    └─ Normalise CC.stepOffset, skinWidth, sphereCastRadius for 3× avatar scale

[SimpleFollowCamera — every LateUpdate]
  └─ target = PlayerSetup.LocalPlayer.Transform (NOT FindWithTag)
       └─ smoothly follows player at pitchAngle=45°, yaw=45°, dist=6m
```

---

### Script Inter-Dependency Map

```
NereonClient.cs (generated)               ← NEVER hand-edit
  provides:
    FetchCharacterStatsAsync / FetchUserProfileAsync / IsUserInitializedAsync
    BuildInitializeUserIx / BuildSubmitScoreIx / BuildSetPlayerTierIx
    SetPlayerTierAsync / DecodeUsername
  called by:
    HomeSceneManager, WelcomeInitController, LoginFlowController,
    NereonPassPopup, NoticeBoard, BuildingInteraction

HomeSceneManager.cs
  depends on: AvatarManager, NereonClient, PlayerHUD, SkyboxController,
              NereonNetworkManager, WorldSyncManager, HomeSceneCinematic,
              SceneLoader, PlayerSetup (static .LocalPlayer), UIAnimations

AvatarManager.cs
  depends on: AvatarRegistry (ScriptableObject), PlayerSetup, NereonMobileInput,
              SnapToTerrain, AvatarProgressionEffects, PlayerWorldUI,
              NereonCameraSetup, FloatingNameTag (unused, kept for reference)

PlayerSetup.cs
  depends on: vThirdPersonInput (Invector), vThirdPersonCamera (Invector)
  provides: LocalPlayer (static singleton), EnablePlayerInput, DisablePlayerInput

NereonCameraSetup.cs  [DefaultExecutionOrder(5000)]
  depends on: vThirdPersonCamera, vThirdPersonInput, CharacterController (Invector motor)
  ALWAYS runs AFTER vThirdPersonInput.Start() (order 0) to kill Invector camera wiring

SimpleFollowCamera.cs
  depends on: PlayerSetup.LocalPlayer (resolves target) — NO FindWithTag in multiplayer

MobileHUDCanvas.cs
  depends on: NereonMobileInput (links to player), ProximityInteractor, MinimapController,
              NereonCanvasHelper.GetCircleSprite(), JoystickWidget

NereonMobileInput.cs
  depends on: vThirdPersonController (Invector) — injects axis/button input

LoginFlowController.cs
  depends on: Web3.OnLogin (Solana SDK), NereonClient, NereonSessionManager,
              BiometricAuthManager, SceneLoader

NereonNetworkManager.cs
  depends on: UGS (Authentication, Lobby, Relay, QoS), NetworkManager (NGO),
              WorldSyncManager

WorldSyncManager.cs
  depends on: WorldVariantRegistry (ScriptableObject), SkyboxController, NereonNetworkManager

SceneLoader.cs  [DontDestroyOnLoad singleton]
  called by: LoginFlowController, HomeSceneManager, WelcomeInitController,
             BuildingInteraction, NereonAuthGuard, DebugUIController
```

---

### Inspector Wiring Checklist (HomeScene)
⚠️ Run this check every time HomeScene is opened or rebuilt.

| Component | On GameObject | Field | Must point to | Status |
|---|---|---|---|---|
| HomeSceneManager | [Managers] | `_avatarManager` | AvatarManager on [Managers] | ✅ wired |
| HomeSceneManager | [Managers] | `_networkManager` | NereonNetworkManager on [Managers] | check |
| HomeSceneManager | [Managers] | `_hud` | PlayerHUD | check |
| HomeSceneManager | [Managers] | `_fadeOverlay` | FadeOverlay CanvasGroup | check |
| HomeSceneManager | [Managers] | `_cinematic` | HomeSceneCinematic on Main Camera | check |
| AvatarManager | [Managers] | `_registry` | AvatarRegistry.asset | ⚠️ check |
| AvatarManager | [Managers] | `_spawnPoint` | [Player]/SpawnPoint | ⚠️ fileID=0 (uses tag fallback) |
| AvatarManager | [Managers] | `_nameTagPrefab` | FloatingNameTag prefab (optional) | check |
| NereonNetworkManager | [Managers] | NetworkManager prefab list | HubPlayer.prefab registered | ⚠️ check |
| WorldSyncManager | [Managers] | `_registry` | WorldVariantRegistry.asset | check |
| SimpleFollowCamera | Main Camera | (auto-discovers via PlayerSetup.LocalPlayer) | — | ✅ auto |
| HomeSceneCinematic | Main Camera | `_followCamera` | SimpleFollowCamera | check |

---

### Active Bugs & Resolutions  (session-updated)

| Bug | Root Cause | Fix | Status |
|---|---|---|---|
| Duplicate HandleLogin / double session save | Two `LoginFlowController` GOs in LandingScene (`LoginManager` + standalone `LoginFlowController` GO) | Deleted standalone `LoginFlowController` GO in Session 24 | ✅ Fixed |
| `Failed to find UI/Skin/Knob.psd` (MobileHUDCanvas, DebugUIController) | Unity 6 stripped built-in path | Added `NereonCanvasHelper.GetCircleSprite()` with programmatic fallback | ✅ Fixed |
| Player not visible in HomeScene | Most likely: AvatarManager `_registry` or `_spawnPoint` not wired in Inspector; or avatar spawned at Y=2000 while WaitForGroundAsync polls terrain | Wire `_registry` to AvatarRegistry.asset; wire `_spawnPoint` to [Player]/SpawnPoint | ⚠️ Investigate |
| `[Netcode] NetworkPrefab cannot be null` | HubPlayer.prefab not registered in NetworkManager's prefab list | Open NetworkManager → Prefabs list → assign HubPlayer.prefab | ⚠️ Needs fix |
| Tree prefab `tree01/tree02` no valid mesh renderer | NatureStarterKit2 trees lack MeshRenderer — terrain uses them as detail prototypes | Assign trees as Terrain Detail (SpeedTree/billboard) not as network-style prefab instances | ⚠️ Cosmetic |
| `[HomeSceneHUDFilter] vHUDController not found` | Invector's HUD controller not in scene (expected — HomeScene has no Invector HUD) | Suppress log level to LogWarning (already done). Non-blocking. | ✅ Harmless |
| `anchor deploy` not run after lib.rs update (Session 23) | set_player_tier + updated submit_score not live on devnet | Run `anchor deploy --provider.cluster devnet` from anchor/ directory | ⚠️ Pending |

---

### Key Runtime Data Flows

```
[Wallet Login Flow]
  User taps Google login
    → LoginFlowController.OnSocialLoginClicked()
    → Web3.Instance.LoginWeb3Auth(Provider.GOOGLE).AsUniTask().Forget()
    → Web3Auth OAuth browser opens
    → Web3Auth.OnLogin callback
    → Web3.set_WalletBase → Web3.OnLogin event fires
    → LoginFlowController.HandleLogin(account)
    → [if _userInitiatedLogin] NereonClient.IsUserInitializedAsync()
    → SceneLoader.Load("HomeScene")

[On-Chain Data → Avatar Visible]
  HomeSceneManager.LoadOnChainDataAsync()
    → NereonClient.FetchCharacterStatsAsync()   [Solana RPC getAccountInfo]
    → NereonClient.FetchUserProfileAsync()      [Solana RPC getAccountInfo]
    → AvatarManager.LoadAvatarAsync(profile, stats)
    → Instantiate avatar prefab at SpawnPoint
    → PlayerSetup.WaitForGroundAsync()          [Physics.Raycast every 150ms]
    → Teleport to groundY + 1.1m
    → NereonCameraSetup.Start() [order 5000] kills Invector camera
    → SimpleFollowCamera.LateUpdate() follows PlayerSetup.LocalPlayer
    → fadeOverlay.alpha = 0   ← world now visible

[Score Submission Flow]
  mini-game scene ends → MiniGameContext.CurrentGameId set
    → NereonClient.BuildSubmitScoreIx(authority, treasury, gameId, gameName,
                                     month, year, score, entryFeeLamports)
    → SOL entry fee transfer to treasury (if >0)
    → submit_score instruction: tier gate (tier≥2 gets leaderboard slot)
    → CharacterStats.xp += game_xp; level-up check
    → SceneLoader.Load("HomeScene")
    → HomeSceneManager.LoadOnChainDataAsync() refreshes XP bar

[Player Tier / Pass Flow]
  NereonPassPopup.OnMintPass()
    → NereonPassSession.SetTier(2)              [PlayerPrefs cache]
    → NereonClient.SetPlayerTierAsync(2)        [set_player_tier on-chain tx]
    → UserProfile.player_tier = 2 on devnet
```

---

❓ Open Questions
What game mechanic goes inside each building? (Coin Flip, Card Table, Puzzle Tower, Oracle, Arena)
NEREON token: new SPL mint or use SOL directly for rewards?
Monthly reward crank: keeper bot, Clockwork (Solana cron), or manual admin?
XP curve: how much XP per game? How many total levels?
Art style reference: what games inspire the visual direction? (Stardew? Genshin? Animal Crossing?)
On-chain randomness: Switchboard VRF or Orao VRF for coin-flip / oracle games?
Will NPCs ever be added to the town? (quest givers, ambient characters)
[Session 13] Exact desired UI layout positions — annotated screenshot referenced but fixes pending:
  - PlayerHUD top-left: portrait ring + name + level badge + XP bar
  - MobileHUDCanvas bottom-left: joystick; bottom-right: 3 circular ability/action buttons
  - PlayerWorldUI: small floating name above head, not giant text filling sky
  - Debug overlay (red markings in screenshot): to be removed in later phase
[Session 13] Camera yaw angle (currently 45°) — should it be 0° (straight-behind) or remain diagonal?
[Session 13] Fantasy Wooden GUI sprites — move to Resources/ or wire via [SerializeField] in Inspector?
[Session 13] MobileHUDCanvas buttons — generate circle Texture2D at runtime, or rely on Fantasy Wooden GUI sprites?

[Session 22] NereonPassPopup component — has it been added to PassPopUP_Panel in LandingScene Inspector?
[Session 22] PassPopUP_Panel visual setup — does it have stretch-fill RectTransform + semi-transparent Image as backdrop?
[Session 22] HomeScene errors from paste file — investigate and fix NullRef/missing component errors in HomeSceneManager/AvatarManager.
[Session 22] NereonPassMinter.cs + NereonPassChecker.cs — when do we implement actual on-chain mint_nereon_pass instruction? Phase 5B or Phase 6?
[Session 22] LeaderboardScene — create it and wire from HomeScene options menu. Which session?
[Session 22] Add _NEREON.asmdef? — would isolate compile errors from third-party packages (MightyDevOps, etc.). Low risk, fast to do.

[Session 23] NereonPassPopup + PassPopUP_Panel — confirm NereonPassPopup component is attached in LandingScene Inspector and PassPopUP_Panel has stretch-fill RectTransform + semi-transparent Image.
[Session 23] set_player_tier on-chain — needs re-deploy of Anchor program (lib.rs updated with player_tier field, set_player_tier instruction, game_name in GameLeaderboard, entry_fee_lamports in submit_score). Run `anchor deploy` on devnet.
[Session 23] Web3Auth own account — BEFORE Seeker submission, register at dashboard.web3auth.io and replace demo clientId in LandingScene. See Architecture Gotchas above.
[Session 23] Camera rewrite: vThirdPersonCamera FixedAngle mode — NereonCameraSetup.cs still destroys vThirdPersonCamera. Plan fully designed (use TPCameraMode.FixedAngle, fixedAngle=Vector2(45,52), call tpCam.Init()) — implementation pending.