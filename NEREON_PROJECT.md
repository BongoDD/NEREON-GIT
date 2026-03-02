NEREON — Project Bible
Single source of truth. Read this before every session.
Every Copilot session, every developer decision, every architecture change is reflected here.
Last updated: Session 16 — 2026-03-02 (NetworkManager null guard, camera black-overlay fix, cinematic descent fix, auth stack)

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
Pyro Entertainment	✅ In project	Toast notifications
DialogGraphSystem	✅ In project	NPC/building dialog trees
JazzCreate BubbleFontFree	✅ In project	JazzCreateBubble SDF — default TMP font
BlockadeLabs-SDK-Unity	✅ In project	Not used at runtime — package present only
Viking Village	✅ In project	Environment dressing, water shaders
Invector Third Person Controller	✅ In project	Active player controller — backbone
Toony Colors Pro 2	✅ In project	Toon shader
DOTween Pro	✅ In project	UIAnimations.cs

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
 ✅ Fix WASD editor movement — NereonMobileInput keyboard fallback now runs before _active guard; movement injection also runs before guard; WASD works from first frame in editor without waiting for MobileHUDCanvas to link
 ✅ Fix PlayerHUD world-space inheritance bug — PlayerHUDCanvas was parented to PlayerHUD GO (inside [HUD Canvas] WorldSpace Canvas); Unity inherited WorldSpace render mode, making panel appear wrong position/size. Fixed by creating PlayerHUDCanvas as scene-root GO (no parent). Tracked via _canvasGO field, destroyed in OnDestroy().
 ✅ Fix camera "too far" — SimpleFollowCamera defaults tuned: _pitchAngle 55→45 (more forward-facing, character more visible), _followDist 10→6 (closer final view), _introStartDist 60→100 (dramatic zoom-in), _introZoomTime 2.5→3s, _lookOffsetY 1.2→1.5 (aim at torso/head)
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
 Test MWA on real Seeker hardware
 Submit via https://seeker.solana.com
⚠️ Architecture Gotchas — Important for Every Session
Gotcha	Detail
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
NereonCameraSetup.cs — camera ownership component	Added to local player by AvatarManager. [DefaultExecutionOrder(5000)] runs AFTER vThirdPersonInput.Start(). Nulls tpCamera field, sets lockCameraInput=true, disables vThirdPersonCamera component. Also removes duplicate AudioListener from player. This is the definitive Invector camera kill — do not remove it.
vThirdPersonInput re-enables camera on Start()	vThirdPersonInput.Start() → CharacterInit() coroutine → FindCamera() → SetMainTarget() → Init() — this re-wires vThirdPersonCamera even if we disabled it earlier. NereonCameraSetup at order 5000 is the correct kill because it runs AFTER vThirdPersonInput.Start() (order 0).
AudioListener duplicate	Invector player prefab includes an AudioListener (so audio follows the character in third-person mode). Main Camera always has one too. NereonCameraSetup.Start() removes the player's AudioListener. Main Camera is authoritative because SimpleFollowCamera keeps it near the player.
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
2026-02-28	NereonCameraSetup.cs created — definitive Invector camera kill	vThirdPersonInput.Start() re-wires vThirdPersonCamera after any earlier disable. NereonCameraSetup at DefaultExecutionOrder(5000) runs after all default-order Start() methods, nulls tpCamera ref, sets lockCameraInput=true, disables component. 3-layer defence: PlayerSetup (early disable) + NereonCameraSetup (late Start kill) + WireCameraToAvatarAsync (1-frame insurance pass).
2026-02-28	PlayerWorldUI scale reduced (CanvasScale 0.005, heightOffset 1.0)	At overhead camera pitch=55° from 10m, 0.01 scale was too large. 0.005 + reduced font sizes gives clean compact tag.
2026-02-28	SimpleFollowCamera prefers PlayerSetup.LocalPlayer	FindWithTag("Player") could lock onto a remote NGO player. Now resolves LocalPlayer first, fallback to FindWithTag only.
2026-02-28	PlayerHUD always rebuilds in Awake()	Removed `if (_usernameText == null)` guard; always calls BuildPanel(). Destroys pre-existing canvas child first.
2026-03-02	NereonCameraSetup removes player AudioListener	Player prefab has AudioListener (Invector default); Main Camera also has one → Unity "2 audio listeners" warning. Since SimpleFollowCamera keeps Main Camera near player, player's listener is redundant. Destroyed in NereonCameraSetup.Start().
2026-03-02	NereonMobileInput WASD runs before _active guard	Editor testing convenience: keyboard movement works from first frame without waiting for MobileHUDCanvas to link. Sprint/jump/action remain guarded (require canvas link).
2026-03-02	HomeSceneCinematic._blackFade alpha fixed in Awake	Was set to 1f (black) in Awake — if cinematic not wired in HomeSceneManager, PlayAsync() never runs and screen stays permanently black. Fixed: set to 0f in Awake. PlayAsync() sets it to 1f itself at Phase 0 start.
2026-03-02	HomeSceneCinematic descent endPos fixed	Was using _cam.transform.rotation from orbital phase to compute follow-camera endPos — orbital rotation aims upward, giving an endPos above the player. Fixed: use Quaternion.Euler(45,45,0) * Vector3.back * 8 for a predictable close-up landing.
2026-03-02	NereonNetworkManager async null-guard	async void methods + Unity async = race condition: object destroyed while awaiting Relay/Lobby → MissingReferenceException on resume. Fixed: IsAlive() check after every await in CreateLobbyAndHostAsync and JoinLobbyAsync.
2026-03-02	Auth stack — NereonSessionManager + BiometricAuthManager	Session persistence (7-day expiry) via PlayerPrefs (public key + wallet type only — no private keys). Biometric gate uses OS BiometricPrompt on Android / LAContext on iOS — dApp never sees biometric data (Solana dApp Store policy compliant). LoginFlowController now shows "Welcome Back" panel for returning users.

content_copy
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
