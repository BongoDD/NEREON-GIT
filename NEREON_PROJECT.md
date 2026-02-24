# NEREON — Project Vision & Progress Log

> **This file is the single source of truth for the NEREON project.**
> It is updated at the end of every working session.
> Every Copilot session should read this file first before making any changes.

---

## 🎯 The Big Vision

NEREON is a **restricted open-world, third-person online RPG** built entirely on the Solana blockchain.
It is simultaneously a **gaming platform** — a small, aesthetically beautiful town where players walk around,
discover buildings, enter them to play skill-based mini-games, level up their character, and compete on
**monthly leaderboards** that automatically reward the top 5 players per game with real tokens via smart contract.

### The World
- A **small, cute, beautifully designed town** — the persistent hub world.
- **Third-person camera** — you see your character walking through the town.
- **Restricted open world** — explorable but bounded; curated experience.
- **Online** — other players are visible in the town at the same time.
- Each **building in the town** is a portal to one specific mini-game.
- More buildings (games) can be added over time → the town grows as the platform grows.

### The Gameplay Loop
```
Enter Town → Walk around → Enter a Building → Play Mini-Game
    → Score submitted on-chain → XP awarded to character → Level up
    → Monthly leaderboard updates → Top 5 auto-rewarded by smart contract
```

### Core Principles
| Principle | Detail |
|---|---|
| **Decentralised** | All core data on Solana. No traditional database owns anything. |
| **Skill + Luck** | Mini-games reward skill but include a provably-fair luck element (on-chain VRF). |
| **Beautiful** | High-quality art and aesthetics first-class priority. Cute town aesthetic. |
| **Rewarding** | Monthly top-5 per game auto-paid by smart contract. No manual claiming. |
| **RPG Progression** | Character levels up via XP from mini-games. Level is on-chain, permanent. |
| **Mobile-first** | Targets Seeker (Solana Mobile) via Mobile Wallet Adapter + Seed Vault. |

---

## 🏗️ Repository Structure

```
NEREON GIT/                        ← git root
├── NEREON_PROJECT.md              ← THIS FILE
├── anchor/                        ← Solana smart contract (Rust / Anchor framework)
│   └── programs/
│       └── nereon/
│           └── src/lib.rs         ← on-chain program logic (TO BE WRITTEN)
├── shared/                        ← shared types / IDL (bridge between Rust & Unity)
└── unity/
    └── NEREON/
        └── NEREON/                ← Unity project root
            └── Assets/
                └── _NEREON/
                    ├── Scripts/
                    │   ├── LoginFlowController.cs       ✅ created
                    │   ├── WelcomeInitController.cs     ✅ created
                    │   └── generated/
                    │       └── NereonClient.cs          🔲 generate from IDL
                    ├── IDL/
                    │   └── nereon.json                  🔲 copy from anchor build
                    └── Scenes/
                        ├── LandingScene.unity           ✅ LoginFlowController wired
                        ├── WelcomeInitScene.unity       ✅ WelcomeInitController wired
                        └── HomeScene.unity              🔲 to be built
```

---

## 🧠 On-Chain Data Architecture

### Account Map
```
wallet_pubkey
    │
    ├── UserProfile PDA          seeds: ["user_profile", wallet]
    │     identity, avatar, username, created_at
    │
    ├── CharacterStats PDA       seeds: ["character", wallet]
    │     level, xp, total_games_played, xp_to_next_level
    │
    └── LeaderboardEntry PDA     seeds: ["lb_entry", game_id, month_year, wallet]
          score, rank (per game, per calendar month)

game_id (u8 constant per building)
    │
    └── GameLeaderboard PDA      seeds: ["leaderboard", game_id, month_year]
          top_5: [(pubkey, score); 5], reward_distributed: bool

RewardEscrow PDA                 seeds: ["escrow"]
    holds_spl_tokens: u64        funded by protocol / fees
```

### All Anchor Accounts

```rust
#[account] pub struct UserProfile {
    pub authority:      Pubkey,
    pub avatar_id:      u8,
    pub username:       [u8; 32],
    pub created_at:     i64,
    pub bump:           u8,
}

#[account] pub struct CharacterStats {
    pub authority:      Pubkey,
    pub level:          u16,
    pub xp:             u32,
    pub games_played:   u32,
    pub bump:           u8,
}

#[account] pub struct GameLeaderboard {
    pub game_id:              u8,
    pub month:                u8,    // 1-12
    pub year:                 u16,
    pub top_entries:          [LeaderboardEntry; 5],
    pub reward_distributed:   bool,
    pub bump:                 u8,
}

#[derive(AnchorSerialize, AnchorDeserialize, Clone, Copy)]
pub struct LeaderboardEntry {
    pub player:  Pubkey,
    pub score:   u32,
}
```

### All Anchor Instructions

| Instruction | Who triggers it | What it does |
|---|---|---|
| `initialize_user` | Player (first login) | Creates UserProfile + CharacterStats PDAs |
| `update_profile` | Player (settings) | Updates avatar_id / username |
| `submit_score` | Player (after mini-game) | Updates LeaderboardEntry; awards XP; updates top-5 |
| `distribute_monthly_rewards` | Crank / automation | Pays top-5 from RewardEscrow; marks month closed |
| `fund_escrow` | Protocol / admin | Deposits NEREON tokens into RewardEscrow |

### Game IDs (one per building)
```
0 = Building_01  (TBD game type)
1 = Building_02  (TBD game type)
...expandable up to 255 games
```

---

## 🔄 User Flow

```
LandingScene
  └─ wallet connects (Mobile Wallet Adapter / Web3Auth)
        └─ LoginFlowController.HandleLogin()
              └─ async RPC: does UserProfile PDA exist?
                    ├─ YES  →  HomeScene         (returning user)
                    └─ NO   →  WelcomeInitScene  (first-time)

WelcomeInitScene
  └─ user picks avatar + username
        └─ WelcomeInitController.CompleteSetup()
              └─ sends initialize_user transaction (signed by wallet)
                    └─ on-chain confirmed → HomeScene

HomeScene
  └─ browse games, leaderboard, profile, rewards   (TO BE DESIGNED)
```

---

## 📦 Tech Stack

| Layer | Technology |
|---|---|
| Blockchain | Solana |
| Smart Contract | Rust + Anchor framework |
| On-chain randomness | Switchboard VRF (or similar) — TBD |
| Unity SDK | Solana.Unity-SDK (MagicBlock Labs) v1.2.10 |
| Wallet (mobile) | Mobile Wallet Adapter → Seeker Seed Vault |
| Wallet (web/desktop) | Web3Auth (Google/Twitter social login) |
| Unity Anchor bridge | Solana.Unity.Anchor (to be added to manifest.json) |

---

## ✅ Work Completed

### Session — 2026-02-21

#### Navigation / Scene Flow
- **Problem:** After wallet login the app had no routing logic; `HomeScene` and `WelcomeInitScene` were missing from Build Settings so `SceneManager.LoadScene()` would fail silently.
- **Fixed `ProjectSettings/EditorBuildSettings.asset`** — added `WelcomeInitScene` (index 1) and `HomeScene` (index 2) alongside the existing `LandingScene` (index 0).
- **Created `LoginFlowController.cs`** — subscribes to `Web3.OnLogin`; checks PlayerPrefs to decide first-time vs returning user; routes to correct scene. *(Will be replaced with on-chain PDA check — see roadmap.)*
- **Created `WelcomeInitController.cs`** — call `CompleteSetup()` from the Finish button; marks user done in PlayerPrefs and loads `HomeScene`. *(Will be replaced with `initialize_user` on-chain tx.)*
- **Wired `WelcomeInitController` into `WelcomeInitScene.unity`** — added `FlowManager` GameObject with the component attached.

#### DAPP Architecture Planning
- Decided to use **Anchor (Rust)** for all on-chain data.
- Designed `UserProfile` PDA account struct.
- Mapped out the 5 phases to full DAPP status (see roadmap below).
- Identified that `PlayerPrefs` must be fully replaced with on-chain reads/writes to qualify for Seeker.

---

## 🗺️ Roadmap

### Phase 1 — Toolchain ✅ COMPLETE
- [x] Install Rust, Solana CLI, Anchor CLI (`avm`)
- [x] Create deploy keypair, fund on Devnet

### Phase 2 — Anchor Program ✅ COMPLETE
- [x] `UserProfile` + `CharacterStats` + `GameLeaderboard` accounts
- [x] `initialize_user` instruction
- [x] `update_profile` instruction
- [x] `submit_score` + XP award logic + top-5 leaderboard
- [x] `distribute_monthly_rewards` instruction
- [x] `anchor build` → IDL generated at `anchor/target/idl/nereon.json`
- [x] `anchor deploy` → **Program ID `4cPPQDNMuwNnXRaNHxvo2gyDhwue1bE8MLzSW3VqcS4o`** live on Devnet

### Phase 3 — Unity ↔ Anchor Bridge ✅ COMPLETE
- [x] `com.solana.unity_sdk` in manifest (MagicBlock)
- [x] IDL at `Assets/_NEREON/IDL/nereon.json`
- [x] `NereonClient.cs` at `Assets/_NEREON/Scripts/generated/`
- [x] Verified `NereonClient.cs` — PDA derivation, all discriminators, all instruction builders correct

### Phase 4 — Replace PlayerPrefs (Login Flow) ✅ COMPLETE
- [x] `LoginFlowController` → async RPC check if `UserProfile` PDA exists
- [x] `WelcomeInitController` → send real `initialize_user` transaction

### Phase 5 — Town Hub World
- [x] All hub world scripts written (HomeSceneManager, PlayerHUD, MiniGamePortal, WorldConfig, BuildingInteraction, etc.)
- [x] HomeScene structured: MapMagic terrain, [Districts] (CentralPlaza, MarketBazaar, MysticQuarter, ChampionArena), [Player], [Managers], [HUD Canvas], NetworkManager
- [x] `com.unity.services.relay`, `com.unity.services.lobby`, `com.unity.services.authentication`, `com.unity.netcode.gameobjects` all installed
- [x] Billing profile set up on NEREON Unity organisation
- [ ] Link Unity Project ID in Editor (Project Settings → Services)
- [ ] Wire `NereonNetworkManager.cs` with UGS Authentication + Relay initialisation
- [ ] Third-person character controller (Starter Assets + `PlayerSetup.cs`)
- [ ] `WorldConfig` ScriptableObject asset created with 4 initial buildings
- [ ] Place building meshes in Districts (placeholder or real art)
- [ ] Leaderboard Notice Board UI (reads `GameLeaderboard` PDAs)

### Phase 6 — Mini-Game Framework
- [ ] `IMinigame` interface
- [ ] Score submission flow → `submit_score` tx
- [ ] XP & level-up feedback UI

### Phase 7 — NEREON Token
- [ ] Mint NEREON SPL token
- [ ] Add `fund_escrow` instruction to Anchor program
- [ ] Test `distribute_monthly_rewards` on Devnet

### Phase 8 — First Mini-Game
- [ ] Choose game type for `CentralPlaza` building
- [ ] Build mechanics (skill + luck element)
- [ ] Integrate with mini-game framework

### Phase 9 — Seeker Submission
- [ ] End-to-end Devnet test
- [ ] Promote to Mainnet
- [ ] MWA verified on Seeker hardware
- [ ] Submit via https://seeker.solana.com

---

## 💬 Key Decisions Made
| Date | Decision | Reason |
|---|---|---|
| 2026-02-21 | Use Anchor (Rust) for all on-chain logic | Industry standard; IDL enables typed C# client |
| 2026-02-21 | UserProfile + CharacterStats stored as PDAs keyed on wallet | Deterministic, no centralised lookup needed |
| 2026-02-21 | PlayerPrefs fully removed from core data | Required for Seeker dApp store qualification |
| 2026-02-21 | Mobile Wallet Adapter for Seeker hardware | SDK includes MWA; Seeker Seed Vault is MWA-compatible |
| 2026-02-21 | Restricted open-world third-person RPG hub town | Core vision: cute town, each building = 1 mini-game |
| 2026-02-21 | Monthly top-5 auto-rewarded by smart contract | No central authority; fully trustless |
| 2026-02-21 | Character levels up via XP from mini-games | RPG loop keeps players engaged across all games |
| 2026-02-21 | Each game gets its own leaderboard PDA (game_id + month) | Modular — new games added without changing existing accounts |
| 2026-02-24 | Unity Relay + Lobby + NGO for hub multiplayer | All packages already installed; billing activated |
| 2026-02-24 | MapMagic for procedural terrain | Already in HomeScene; dynamic world generation |
| 2026-02-24 | 4 Districts: CentralPlaza, MarketBazaar, MysticQuarter, ChampionArena | Scene already structured this way |

---

## ❓ Open Questions / TBD
- What game types per district building? (CentralPlaza, MarketBazaar, MysticQuarter, ChampionArena) — *TBD*
- NEREON token: new SPL mint or use SOL directly for rewards? — *TBD*
- Monthly reward crank: keeper bot, Clockwork, or manual? — *TBD*
- XP curve: how much XP per game? How many levels total? — *TBD*
- Art style reference: what games inspire the visual direction? — *TBD*
- Third-person character: Unity Starter Assets or custom rig? — *TBD*
- On-chain randomness: Switchboard VRF or Orao VRF? — *TBD*

---

## 🔌 Unity MCP Integration

Unity MCP server (`mcp-for-unity-server v2.14.1`) is running at `http://localhost:8080/mcp`.
- Instance: `NEREON@a4fa1463f7878701` (Unity 6000.3.9f1)
- Use `build_index` (not `path`) for `manage_scene` load actions
- `.mcp.json` at repo root configures the connection

---

## ✅ Work Completed

### Session — 2026-02-24 (Session 6)

#### Anchor Deploy
- Ran `anchor deploy --provider.cluster devnet`
- **Program ID confirmed live:** `4cPPQDNMuwNnXRaNHxvo2gyDhwue1bE8MLzSW3VqcS4o`
- IDL Account: `5aUkwLTHGoVYvB71Xzm6CS1V4vjBCt9RjvqqnuUEeDqc`
- Deploy slot: 444407275 | Upgrade authority: `GEsk7ishSsqmKebunMUWcPeV3GWfh2CpnLa6ErhMeZsh`
- Balance remaining: ~6.36 SOL (deploy cost ~1.81 SOL held in program data account)
- Phase 2 now **fully complete** ✅
- **`NereonClient.cs` verified** — all PDA seeds, discriminators (SHA256 confirmed), account layouts, and instruction builders match the deployed IDL exactly. Phase 3 **fully complete** ✅

---

### Session — 2026-02-24

#### Anchor Program
- Verified `lib.rs` is **fully complete**: `initialize_user`, `update_profile`, `submit_score`, `distribute_monthly_rewards`, all PDAs, events, errors.
- **`anchor build` succeeded** → IDL generated at `anchor/target/idl/nereon.json`
- IDL copied to `Assets/_NEREON/IDL/nereon.json` ✅
- **`anchor deploy` succeeded** → Program live on Devnet ✅
  - Program ID: `4cPPQDNMuwNnXRaNHxvo2gyDhwue1bE8MLzSW3VqcS4o`
  - IDL Account: `5aUkwLTHGoVYvB71Xzm6CS1V4vjBCt9RjvqqnuUEeDqc`
  - Deploy slot: 444407275 | Upgrade authority: wallet `GEsk7ish...`

#### Unity Project Audit (via MCP)
- **LandingScene**: Fixed — removed duplicate `LoginManager` GameObject (had redundant `LoginFlowController`). Now has exactly one `LoginFlowController`. ✅
- **WelcomeInitScene**: Verified correct — `FlowManager` has `WelcomeSceneFlow`, `WelcomeInitController`, `AvatarSelector`, `NereonAuthGuard`. ✅
- **HomeScene**: Fully structured ✅
  - `MapMagic` + `TerrainBuildingPlacer` — procedural terrain ready
  - `[Environment]`: Sun, River, SkyboxManager (SkyboxController)
  - `[Districts]`: CentralPlaza, MarketBazaar, MysticQuarter, ChampionArena
  - `[Player]`: SpawnPoint
  - `[Managers]`: HomeSceneManager, AuthGuard (NereonAuthGuard)
  - `[HUD Canvas]`: FadeOverlay, PlayerHUD, ChatInputPanel
  - `NetworkManager`: NetworkManager + UnityTransport + NereonNetworkManager
  - Build settings: LandingScene(0), WelcomeInitScene(1), HomeScene(2) ✅

#### Unity Packages (all already installed)
| Package | Version | Purpose |
|---|---|---|
| `com.unity.services.authentication` | 3.3.4 | UGS identity |
| `com.unity.services.relay` | 1.0.3 | Multiplayer relay |
| `com.unity.services.lobby` | 1.2.2 | Room/lobby system |
| `com.unity.services.core` | 1.12.5 | UGS core |
| `com.unity.netcode.gameobjects` | 2.1.1 | NGO multiplayer |
| `com.unity.cinemachine` | 3.1.2 | Camera system |
| `com.unity.ai.navigation` | 2.0.10 | NavMesh |
| `com.solana.unity_sdk` | git | Solana wallet |

#### Unity Dashboard / Organisation
- **Billing profile set up on NEREON organisation** ✅ — UGS cloud services (Relay, Lobby, Authentication) now fully active.
- **TODO**: In Unity Editor → Edit > Project Settings > Services → link to NEREON org project ID to activate UGS in-editor.

---

## 🗺️ Roadmap

### Phase 1 — Toolchain ✅ COMPLETE
### Phase 2 — Anchor Program ✅ COMPLETE
- [x] All instructions written and compiled
- [x] `anchor deploy` → **Program ID: `4cPPQDNMuwNnXRaNHxvo2gyDhwue1bE8MLzSW3VqcS4o`** live on Devnet (slot 444407275)

### Phase 3 — Unity ↔ Anchor Bridge ✅ COMPLETE
- [x] IDL generated and copied to `Assets/_NEREON/IDL/nereon.json`
- [x] `com.solana.unity_sdk` already in manifest
- [x] `NereonClient.cs` verified — all PDA seeds, discriminators, account layouts, instruction builders confirmed correct vs deployed IDL

### Phase 4 — Replace PlayerPrefs (Login Flow) ✅ COMPLETE
- [x] `LoginFlowController` → async RPC `IsUserInitializedAsync()` — zero PlayerPrefs, pure on-chain routing
- [x] `WelcomeInitController` → sends real `initialize_user` transaction; PlayerPrefs write is display cache only (post-confirm)
- [x] `HomeSceneManager` → fetches on-chain first (4 retries); PlayerPrefs is fallback display cache, overwritten by on-chain data
- No core decisions (routing, identity, game state) rely on PlayerPrefs ✅ Seeker-compliant

### Phase 5 — Town Hub World (Scenes Built ✅)
- [x] All hub world scripts written (HomeSceneManager, PlayerHUD, MiniGamePortal, etc.)
- [x] HomeScene structured (MapMagic terrain, Districts, Player, Managers, HUD)
- [ ] Link Unity Project ID in Editor to activate UGS services
- [ ] Wire `NereonNetworkManager.cs` to use UGS Authentication + Relay properly
- [ ] Place actual district buildings (placeholder meshes → real art)
- [ ] Third-person character controller (Starter Assets import)
- [ ] `WorldConfig` ScriptableObject asset created with initial buildings
- [ ] Leaderboard Notice Board UI (reads GameLeaderboard PDAs)

### Phase 6 — Mini-Game Framework
- [ ] `IMinigame` interface
- [ ] Score submission flow → `submit_score` tx
- [ ] XP & level-up feedback UI

### Phase 7 — NEREON Token
- [ ] Mint NEREON SPL token
- [ ] Add `fund_escrow` instruction to Anchor program
- [ ] Fund RewardEscrow, test `distribute_monthly_rewards` on Devnet

### Phase 8 — First Mini-Game (TBD)
### Phase 9 — Seeker Submission

---

## 📦 Tech Stack

| Layer | Technology |
|---|---|
| Blockchain | Solana |
| Smart Contract | Rust + Anchor framework |
| On-chain randomness | Switchboard VRF (or similar) — TBD |
| Unity SDK | Solana.Unity-SDK (MagicBlock Labs) — git |
| Wallet (mobile) | Mobile Wallet Adapter → Seeker Seed Vault |
| Wallet (web/desktop) | Web3Auth (Google/Twitter social login) |
| Multiplayer | Unity Netcode for GameObjects + Unity Relay + Unity Lobby |
| Terrain | MapMagic (procedural) |
| Camera | Cinemachine 3.1.2 |
| Navigation | Unity AI Navigation (NavMesh) |

---

## 💬 Key Decisions Made
| Date | Decision | Reason |
|---|---|---|
| 2026-02-21 | Use Anchor (Rust) for all on-chain logic | Industry standard for Solana; IDL enables typed C# client |
| 2026-02-21 | UserProfile + CharacterStats stored as PDAs keyed on wallet | Deterministic, no centralised lookup needed |
| 2026-02-21 | PlayerPrefs fully removed from core data | Required for Seeker dApp store qualification |
| 2026-02-21 | Mobile Wallet Adapter for Seeker hardware | SDK already includes MWA; Seeker Seed Vault is MWA-compatible |
| 2026-02-21 | Restricted open-world third-person RPG hub town | Core game vision: cute town, each building = 1 mini-game |
| 2026-02-21 | Monthly leaderboard top-5 auto-rewarded by smart contract | No central authority needed; fully trustless rewards |
| 2026-02-24 | Unity Relay + Lobby for hub multiplayer | Already installed; billing profile activated on NEREON org |
| 2026-02-24 | MapMagic for procedural terrain | Already in HomeScene; allows dynamic world generation |

---

## ❓ Open Questions / TBD
- What type of mini-games go in each building? (card, puzzle, arcade, dice, fishing…) — *TBD*
- How many buildings in the first version? CentralPlaza, MarketBazaar, MysticQuarter, ChampionArena defined — *need game types*
- NEREON token: new SPL mint or use SOL directly for rewards? — *TBD*
- Monthly reward distribution: who triggers the crank? (keeper bot, Clockwork, manual?) — *TBD*
- XP curve: how much XP per game? How many levels? — *TBD*
- Art style reference: what games inspire the visual direction? — *TBD*
- Third-person character: use Unity Starter Assets or custom? — *TBD*

---

*Last updated: Session 6 — anchor deploy ✅, NereonClient.cs verified ✅, Phase 4 confirmed complete ✅ (Phases 1–4 all done)*

---

## 🌍 World Design — NEREON Hub Town

### Overview
The NEREON Hub Town is a **small, restricted open-world 3D environment** the player
is dropped into after logging in.  It looks like a cute fantasy anime village —
warm lighting, hand-painted/toon textures (via Toony Colors Pro 2), AI-generated
skybox (Blockade Labs).  Other players are visible walking around (multiplayer — TBD).

### Terrain Shape
```
         ╔══════════════════════════════════╗
         ║  [CLIFF WALL - boundary]         ║
         ║                                  ║
         ║   🌲 Forest   [ Mystic Quarter ] ║
         ║                 🏚 🏚 🏚        ║
         ║        ╔══════════════╗          ║
         ║  🌲    ║  Central Hub ║    🌊   ║
         ║        ║   Plaza  ⛲  ║  (river) ║
         ║        ╚══════════════╝          ║
         ║  [ Market Bazaar ]   🌲          ║
         ║    🏪 🏪 🏪                      ║
         ║                [ Champion Arena ]║
         ║     (path)          🏟          ║
         ╚══════════════════════════════════╝
              [natural cliffs + river = boundaries]
```

### Districts & Buildings (V1 — 3 districts, 5 buildings)

| District | Building # | Game ID | Name on door | Min Level | Game Type (TBD) |
|---|---|---|---|---|---|
| **Market Bazaar** | Building_01 | 0 | "The Coin Flip" | 1 | Luck + prediction |
| **Market Bazaar** | Building_02 | 1 | "The Card Table" | 1 | Card skill game |
| **Mystic Quarter** | Building_03 | 2 | "The Puzzle Tower" | 3 | Puzzle / brain |
| **Mystic Quarter** | Building_04 | 3 | "The Oracle" | 5 | VRF/luck ritual |
| **Champion Arena** | Building_05 | 4 | "The Arena" | 8 | High-score action |

> District and building count grows with the platform.  New game = new building added.
> All numbers (game IDs, min levels) are defined in `WorldConfig.cs` ScriptableObject.

### Level-Gating System
- Each building has a **minimum level** defined in `WorldConfig.cs`.
- When the player approaches a building, `BuildingInteraction.cs` checks
  `HomeSceneManager.CachedStats.Level` (fetched from on-chain at scene load).
- If level is **insufficient**: door shows a padlock icon + "Requires LVL X" text.
  Player cannot enter. No on-chain call needed.
- If level is **sufficient**: door shows "Press E to Enter" prompt.
  Player presses E → mini-game scene loads.
- This is **purely a client-side UX check** (no on-chain gate needed for entry;
  on-chain validation happens in `submit_score` to reject cheated scores).

### Central Hub Plaza
- **Spawn point** — all players appear here on login.
- **Fountain** — decorative centrepiece.
- **Notice Board** — a world-space UI panel that shows the current month's
  top-5 leaderboard for each game.  Data fetched from `GameLeaderboard` PDAs.
  Updates every 60 seconds via a coroutine in `NoticeBoard.cs` (to be built).
- **Character Level Display** — floating name tag above player showing username + level.
- **Daily quest giver NPC** (Phase 2+, TBD).

### Terrain Technical Setup (Unity Editor steps)
1. **Create Terrain** in HomeScene:
   - Size: 200 × 200 units (intimate, not huge)
   - Height variation: gentle hills (max height 15 units)
   - Paint 3 terrain layers: Grass, Dirt Path, Stone Plaza
2. **Boundary enforcement** (invisible walls + natural features):
   - Cliff walls on North + West edges (use terrain height sculpting)
   - River on East edge (water plane + invisible collider)
   - Dense tree line on South edge
3. **Terrain Layers** (textures from free assets — see Asset List):
   - Grass (base layer, Stylized Nature MegaKit)
   - Dirt path (connecting buildings to plaza)
   - Stone tile (plaza area)
4. **NavMesh bake** — bake a NavMesh so future NPCs can pathfind.
5. **LOD** — keep terrain mesh simple (LOD 0 for mobile target).

### Scene Hierarchy (target layout in HomeScene)
```
HomeScene
├── [Environment]
│   ├── Terrain
│   ├── River (plane + water shader)
│   ├── Lighting (Directional Light "Sun", warm colour)
│   └── Skybox (SkyboxController.cs)
├── [Districts]
│   ├── CentralPlaza
│   │   ├── Fountain
│   │   ├── NoticeBoardGO  ← NoticeBoard.cs + WorldCanvas
│   │   └── SpawnPoint     ← player appears here
│   ├── MarketBazaar
│   │   ├── Building_01    ← BuildingInteraction(gameId=0, minLevel=1)
│   │   └── Building_02    ← BuildingInteraction(gameId=1, minLevel=1)
│   ├── MysticQuarter
│   │   ├── Building_03    ← BuildingInteraction(gameId=2, minLevel=3)
│   │   └── Building_04    ← BuildingInteraction(gameId=3, minLevel=5)
│   └── ChampionArena
│       └── Building_05    ← BuildingInteraction(gameId=4, minLevel=8)
├── [Player]
│   └── ThirdPersonPlayer  ← Starter Assets prefab + PlayerSetup.cs
├── [Managers]
│   ├── HomeSceneManager   ← HomeSceneManager.cs
│   └── SkyboxManager      ← SkyboxController.cs
└── [HUD Canvas]
    └── PlayerHUD          ← PlayerHUD.cs
```

---

## 🎨 Free Asset List (Cartoon / Toon Style)

All assets below are **CC0 or free to use in commercial projects**.
Download before next session, then import via Unity Package Manager or drag into Assets.

### Priority 1 — Environment & Buildings

| Asset | Source | Link | Why |
|---|---|---|---|
| **KayKit Medieval Hexagon** | kaylousberg.itch.io | https://kaylousberg.itch.io/kaykit-medieval-hexagon | Cartoon buildings, trees, props — CC0, perfect fit |
| **Medieval Village MegaKit** | quaternius.com | https://quaternius.com/packs/medievalvillagemegakit.html | Modular medieval buildings, fences, carts — CC0 |
| **Stylized Nature MegaKit** | quaternius.com | https://quaternius.com/packs/ultimatestylizednature.html | Ghibli-style trees, grass, rocks — CC0 |
| **Nature Kit** | kenney.nl | https://kenney.nl/assets/nature-kit | Low-poly trees, rocks, water — CC0 |

### Priority 2 — Characters

| Asset | Source | Link | Why |
|---|---|---|---|
| **RPG Character Pack** | quaternius.com | https://quaternius.com/packs/rpgcharacters.html | Animated RPG characters — CC0, rigged |
| **Ultimate Modular Men** | quaternius.com | https://quaternius.com/packs/ultimatemodularcharacters.html | Customisable male base — CC0 |
| **Ultimate Modular Women** | quaternius.com | https://quaternius.com/packs/ultimatemodularwomen.html | Customisable female base — CC0 |

### Priority 3 — Props & Details

| Asset | Source | Link | Why |
|---|---|---|---|
| **Fantasy Props MegaKit** | quaternius.com | https://quaternius.com/packs/fantasypropsmegakit.html | Barrels, crates, furniture, signs — CC0 |
| **Cute Animated Monsters** | quaternius.com | https://quaternius.com/packs/cutemonsters.html | Ambient NPCs / creatures — CC0, animated |
| **UI Icons Fantasy** | kenney.nl | https://kenney.nl/assets/game-icons | Fantasy game icons for HUD — CC0 |

### Already in Project / Subscribed

| Asset | Status | Use |
|---|---|---|
| **Starter Assets – Third Person** | Download from Asset Store | Player character controller + Cinemachine camera |
| **DOTween Pro** | Download from Asset Store | All UI animations (UIAnimations.cs is ready for it) |
| **Toony Colors Pro 2** | Download from Asset Store | Toon shader on all characters + buildings |
| **Skybox AI (Blockade Labs)** | Subscription | AI-generated hub world skybox (SkyboxController.cs is ready) |

### Import Order (do this in Unity)
1. Import **Starter Assets – Third Person** → this pulls in Cinemachine 3 automatically
2. Import **DOTween Pro** → run its Setup wizard → add `DOTWEEN` to Scripting Define Symbols
3. Import **Toony Colors Pro 2** → it auto-detects URP
4. Download KayKit + Quaternius packs (GLB files) → drag into `Assets/_NEREON/Models/`
5. In the Inspector: set all imported models to **Read/Write Enabled** + **Generate Colliders**

---

### 2026-02-21 — Session 1: Foundation
- Fixed scene navigation (LandingScene → WelcomeInitScene / HomeScene)
- Added WelcomeInitScene & HomeScene to Build Settings
- Wired WelcomeInitController into WelcomeInitScene
- Planned full DAPP architecture (Anchor, PDAs, leaderboard)

### 2026-02-21 — Session 2: Full Vision + On-Chain Implementation
**Vision defined:** Restricted open-world third-person online RPG. Small cute town hub. Each building = one mini-game portal. Monthly top-5 leaderboard per game, auto-rewarded by smart contract. Character levels up via XP from playing games.

**Files created / rewritten:**
| File | Status |
|---|---|
| `anchor/Anchor.toml` | ✅ Created |
| `anchor/Cargo.toml` | ✅ Created |
| `anchor/.gitignore` | ✅ Created |
| `anchor/programs/nereon/Cargo.toml` | ✅ Created |
| `anchor/programs/nereon/src/lib.rs` | ✅ Created — full Anchor program |
| `anchor/migrations/deploy.ts` | ✅ Created |
| `Assets/_NEREON/IDL/nereon.json` | ✅ Created — hand-written IDL |
| `Assets/_NEREON/Scripts/generated/NereonClient.cs` | ✅ Created — full Unity bridge |
| `Assets/_NEREON/Scripts/LoginFlowController.cs` | ✅ Rewritten — async on-chain PDA check, no PlayerPrefs |
| `Assets/_NEREON/Scripts/WelcomeInitController.cs` | ✅ Rewritten — sends initialize_user tx on-chain |

**PlayerPrefs completely removed** from all core scripts. ✅

**⚠️  Next required action by developer:**
1. Install toolchain (Rust + Solana CLI + Anchor CLI) — see Phase 1 in Roadmap
2. Run `anchor build` from `C:\NBF\NEREON GIT\anchor\`
3. Run `anchor deploy` → copy the printed Program ID
4. Replace `"11111111111111111111111111111111"` in:
   - `anchor/Anchor.toml` (both [programs.devnet] and [programs.mainnet])
   - `Assets/_NEREON/Scripts/generated/NereonClient.cs` (PROGRAM_ID constant)
   - `Assets/_NEREON/IDL/nereon.json` (metadata.address)

*Last updated: 2026-02-21 — Session 2: Anchor program written, Unity bridge complete, PlayerPrefs removed.*

### 2026-02-22 — Session 3: Toolchain Install + Program ID
**Toolchain installed:**
- Rust (rustc + cargo) ✅
- Solana CLI v3.0.15 (Agave) ✅
- AVM v0.32.1 ✅
- Anchor 0.30.1 (via avm) ✅

**Keypair created:** `GEsk7ishSsqmKebunMUWcPeV3GWfh2CpnLa6ErhMeZsh`
**Solana config:** Devnet

**`anchor build` completed** → Program keypair generated.

**Program ID:** `4cPPQDNMuwNnXRaNHxvo2gyDhwue1bE8MLzSW3VqcS4o`

**Files updated with real Program ID:**
| File | Status |
|---|---|
| `anchor/programs/nereon/src/lib.rs` | ✅ `declare_id!` updated |
| `anchor/Anchor.toml` | ✅ devnet + mainnet updated |
| `Assets/_NEREON/IDL/nereon.json` | ✅ metadata.address updated |
| `Assets/_NEREON/Scripts/generated/NereonClient.cs` | ✅ PROGRAM_ID updated |

**⚠️ Next required actions:**
1. Activate Anchor: `avm use 0.30.1` (was not set during bat run)
2. Rebuild with real Program ID: `anchor build` (in `C:\NBF\NEREON GIT\anchor\`)
3. Get Devnet SOL: visit https://faucet.solana.com → paste `GEsk7ishSsqmKebunMUWcPeV3GWfh2CpnLa6ErhMeZsh`
4. Deploy: `anchor deploy`

*Last updated: 2026-02-22 — Session 3: Toolchain complete, Program ID set in all files.*

### Session 4 — World Design, Asset Plan & Hub World Scripts

#### World Design Finalised (see full section below)
**Files created this session:**
| File | Purpose |
|---|---|
| `Assets/_NEREON/Scripts/UIAnimations.cs` | DOTween-ready fade/slide/pop/typewriter utilities (graceful fallback without DOTween) |
| `Assets/_NEREON/Scripts/SkyboxController.cs` | Blockade Labs REST API — generates AI skybox at runtime from text prompt |
| `Assets/_NEREON/Scripts/HomeSceneManager.cs` | Hub world manager — loads on-chain CharacterStats, refreshes HUD, fade-in |
| `Assets/_NEREON/Scripts/PlayerHUD.cs` | Level / XP bar / username HUD display |
| `Assets/_NEREON/Scripts/MiniGamePortal.cs` | Trigger-zone → scene load (stores CurrentGameId in MiniGameContext) |
| `Assets/_NEREON/Scripts/WorldConfig.cs` | ScriptableObject — all building/game definitions in one data asset |
| `Assets/_NEREON/Scripts/BuildingInteraction.cs` | Level-gate eligibility check (on-chain) + enter/deny logic + prompt UI |
| `Packages/manifest.json` | Added `com.unity.cinemachine 3.1.2` (required by Starter Assets Third Person) |

### Session 5 — Avatar System, Progression Effects & Skybox Integration

The player's on-chain `avatar_id` (stored in `UserProfile` PDA) drives which 3D character
appears in HomeScene. Visual effects on the avatar scale with `CharacterStats.level`.
The Blockade Labs AI skybox generates in the background on HomeScene load.

**Avatar Progression Tiers:**

| Tier | Levels | Visual Effect |
|---|---|---|
| 0 | 1–5 | No effect (newcomer) |
| 1 | 6–10 | Soft blue aura particles |
| 2 | 11–20 | Purple aura + trail renderer |
| 3 | 21–35 | Gold aura + bright trail |
| 4 | 36+ | Rainbow aura + trail + ground rune (legend) |

Outline colour on the avatar's TCP2 shader also shifts per tier (black → blue → purple → gold → pink).
Achievement slot GameObjects on the prefab can be toggled per on-chain badge (future).

**Files created this session:**
| File | Purpose |
|---|---|
| `Assets/_NEREON/Scripts/AvatarRegistry.cs` | ScriptableObject: avatarId byte → prefab + preview image mapping |
| `Assets/_NEREON/Scripts/AvatarManager.cs` | Spawns correct avatar at SpawnPoint, attaches FloatingNameTag, calls effects |
| `Assets/_NEREON/Scripts/AvatarProgressionEffects.cs` | Level-gated: aura PS × 4 tiers, TrailRenderer, ground rune, TCP2 outline colour |
| `Assets/_NEREON/Scripts/FloatingNameTag.cs` | World-space billboard: username + "LVL X" above player head |
| `Assets/_NEREON/Scripts/AvatarSelector.cs` | WelcomeInitScene left/right carousel → sets WelcomeInitController.AvatarId |

**Files updated this session:**
| File | Change |
|---|---|
| `HomeSceneManager.cs` | Added `CachedProfile` static; calls `AvatarManager.LoadAvatarAsync(profile, stats)` after chain fetch |

**Editor steps still needed (by developer):**
1. Import **Quaternius RPG Character Pack** GLBs → `Assets/_NEREON/Models/Avatars/`
2. Create a prefab per character: add `AvatarProgressionEffects` + empty child GOs for particle systems
3. Create `Assets/_NEREON/Data/AvatarRegistry.asset` (right-click → NEREON → Avatar Registry) → populate
4. In WelcomeInitScene: add `AvatarSelector` panel inside `WelcomeCanvas` prefab, wire all fields
5. Create `FloatingNameTag.prefab`: world-space Canvas inside avatar prefab, two TMP labels, scale 0.01
6. In HomeScene: add `SkyboxController` component → enter Blockade Labs API key → create `Skybox/Panoramic` Material

*Last updated: Session 5 — Avatar system (code complete), skybox wired into HomeSceneManager.*

### Session 6 — Placeholder Avatars + Full WelcomeInitScene Flow

**Feature:** WelcomeInitScene now has a complete 3-step onboarding flow that works
immediately with zero art assets — coloured capsule placeholders stand in for real
character models. Swap the prefab references in AvatarRegistry.asset when custom
models are ready.

**Welcome flow (3 panels, smooth slide transitions):**
```
Panel 1: AVATAR SELECT
  ◄  [3D rotating preview]  ►
     WARRIOR / MAGE / ROGUE / PALADIN
         "Fearless frontline fighter…"
              1 / 4       [NEXT →]

Panel 2: CHOOSE YOUR NAME
  ┌───────────────────────────┐
  │  gmavr                    │
  └───────────────────────────┘
  5 / 20         [← BACK]  [ENTER NEREON]

Panel 3: CONFIRMING (shown during tx)
  ⟳  Saving your profile to the blockchain…
```

**Placeholder avatar classes (no art needed — generated at runtime):**
| avatar_id | Class | Body colour |
|---|---|---|
| 0 | WARRIOR | Deep red |
| 1 | MAGE | Royal blue |
| 2 | ROGUE | Forest green |
| 3 | PALADIN | Gold |

To swap in real models: populate `AvatarRegistry.asset` with your prefab for each
avatar_id. `AvatarSelector` uses the prefab if found, placeholder otherwise.

**Files created this session:**
| File | Purpose |
|---|---|
| `Assets/_NEREON/Scripts/PlaceholderAvatarFactory.cs` | Runtime capsule+sphere humanoid per class, coloured material + accent light |
| `Assets/_NEREON/Scripts/WelcomeSceneFlow.cs` | 3-step flow controller: panel transitions, validation, fires `WelcomeInitController.CompleteSetup()` |

**Files updated this session:**
| File | Change |
|---|---|
| `Assets/_NEREON/Scripts/AvatarSelector.cs` | Rewritten: falls back to `PlaceholderAvatarFactory` when no prefab; fires `OnAvatarSelected` event |
| `Assets/_NEREON/Scripts/WelcomeInitController.cs` | Slimmed to transaction-only; status routed to `WelcomeSceneFlow.SetConfirmStatus()` |

**WelcomeInitScene hierarchy to build in Editor:**
```
WelcomeInitScene
├── [Preview Stage]  — GO at pos (500,0,500), far from main camera
│   ├── PreviewCamera  — Target Texture: AvatarPreviewRT (512×512 RenderTexture asset)
│   │                    Clear: Solid black | Culling: Default layer
│   ├── PreviewLight   — Directional, warm colour
│   └── PreviewAnchor  — empty GO → wire to AvatarSelector._previewStage
│
├── FlowManager (empty GO)
│   ├── WelcomeSceneFlow    ← wire all panel + button fields
│   └── WelcomeInitController
│
└── WelcomeCanvas (Screen Space Overlay)
    ├── FadeOverlay          — full-screen black Image + CanvasGroup
    ├── Panel_AvatarSelect   — CanvasGroup
    │   ├── Title TMP        — "CHOOSE YOUR AVATAR"
    │   ├── PreviewRawImage  — RawImage, texture = AvatarPreviewRT
    │   ├── BtnLeft          — Button "◄"
    │   ├── BtnRight         — Button "►"
    │   ├── ClassNameLabel   — TMP bold
    │   ├── ClassDescLabel   — TMP italic small
    │   ├── PageIndicator    — TMP "1 / 4"
    │   └── BtnNext          — Button "NEXT →"
    ├── Panel_NameEntry      — CanvasGroup
    │   ├── Title TMP        — "CHOOSE YOUR NAME"
    │   ├── UsernameInput    — TMP_InputField, char limit 20
    │   ├── CharCount        — TMP "0 / 20"
    │   ├── NameError        — TMP red, hidden until invalid
    │   ├── BtnBack          — Button "← BACK"
    │   └── BtnConfirm       — Button "ENTER NEREON"
    └── Panel_Confirming     — CanvasGroup
        ├── Spinner          — rotating Image
        └── StatusText       — TMP "Saving to blockchain…"
```

*Last updated: Session 6 — WelcomeInitScene flow complete with placeholder avatars.*

### Session 7 — MapMagic 2 Terrain + Multiplayer Presence + Bubble Chat

#### Architectural Decisions
| Decision | Reason |
|---|---|
| Fixed building positions in WorldConfig | Exploration feel from terrain/trees hiding sight lines, not random building placement |
| MapMagic 2 single 200×200 tile | Restricted world — no infinite terrain needed |
| Unity Netcode + Unity Relay for multiplayer | No CCU hard cap (pay-per-use relay), no server to manage, first-party |
| Real-time presence/chat NOT on Solana | Blockchain is for persistent state; real-time ephemeral data belongs on relay |

#### Files created / updated this session
| File | Change |
|---|---|
| `NereonHomeSceneBuilder.cs` | ✅ New (Editor) — one-click HomeScene builder: MapMagic GO, terrain graph, districts, 5 building placeholders, SpawnPoint, Managers, HUD Canvas |
| `WorldConfig.cs` | Added `WorldPosition` (Vector3) to `BuildingDefinition` with preset positions; added `SpawnPoint` + `SpawnFacingYaw` fields |
| `TerrainBuildingPlacer.cs` | ✅ New — snaps buildings + SpawnPoint to MapMagic terrain height via `TerrainTile.OnAllComplete` |
| `NereonNetworkManager.cs` | ✅ New — UGS init, anonymous sign-in, Unity Lobby join/create, Relay setup, NGO Host/Client start |
| `HubPlayerNetwork.cs` | ✅ New — NGO NetworkBehaviour syncing AvatarId, Level, Username; ServerRpc → ClientRpc chat broadcast |
| `RemotePlayerVisuals.cs` | ✅ New — spawns correct avatar for remote players from synced NetworkVars |
| `BubbleChat.cs` | ✅ New — world-space speech bubbles above player head, stack + fade |
| `ChatInputUI.cs` | ✅ New — press T to open, Enter to send, Escape to cancel |
| `HomeSceneManager.cs` | Added `_networkManager` field; calls `ConnectAsync(walletPubkey)` after on-chain data loads |
| `Packages/manifest.json` | Added NGO `2.1.1`, Relay `1.1.2`, Lobby `1.2.2`, Authentication `3.3.4`, Core `1.12.5` |

#### Building World Positions (XZ on 200×200 terrain, origin = Central Plaza)
| Building | Position |
|---|---|
| The Coin Flip (Market Bazaar) | (-55, 0, -25) |
| The Card Table (Market Bazaar) | (-35, 0, -55) |
| The Puzzle Tower (Mystic Quarter) | (45, 0, 55) |
| The Oracle (Mystic Quarter) | (65, 0, 30) |
| The Arena (Champion Arena) | (55, 0, -50) |
| SpawnPoint (Central Plaza) | (0, 0, 0) |

#### Editor Setup Steps Required
**Hub Player Prefab** (create as `Assets/_NEREON/Prefabs/HubPlayer.prefab`):
```
HubPlayer (root)
├── NetworkObject
├── NetworkTransform       ← syncs position/rotation
├── HubPlayerNetwork       ← syncs AvatarId, Level, Username; handles chat
├── RemotePlayerVisuals    ← wire AvatarRegistry, FloatingNameTag prefab, ModelAnchor
├── BubbleChat             ← wire BubbleContainer (child GO above head), BubblePrefab
├── ModelAnchor            (empty GO, local pos 0,0,0)
└── BubbleContainer        (empty GO, local pos 0, 2.8, 0)
```

**HomeScene Additions:**
- Add `NetworkManager` GO → add `NetworkManager` + `UnityTransport` → set transport to Unity Relay
- Add `NereonNetworkManager` to `[Managers]` GO → wire `_networkManager`, `_hubPlayerPrefab`
- Wire `HomeSceneManager._networkManager`
- Add `ChatInputUI` to `[HUD Canvas]` → build `ChatInputPanel` with TMP_InputField

**Bubble Chat Prefab** (`Assets/_NEREON/Prefabs/ChatBubble.prefab`):
```
ChatBubble (World Space Canvas, scale 0.01)
├── CanvasGroup
├── Background (Image, white rounded rect)
└── MessageText (TMP_Text, black, size 14, word wrap)
```

**Unity Dashboard (one-time setup):**
1. https://dashboard.unity3d.com → create project
2. Enable: Relay, Lobby, Authentication
3. Copy Project ID → Unity Editor: Edit → Project Settings → Services

*Last updated: Session 7 — MapMagic terrain integration + multiplayer presence + bubble chat.*

### Session 8 — Inspector Wiring via MCP + HomeScene Finalised

#### Key Discovery: MCP Object Reference Format
`manage_components set_property` accepts scene object references as `{"instanceID": <int>}` (not integer directly, not string path). Asset references (prefabs, scriptable objects) use the asset path string directly. This unblocks all inspector wiring via MCP.

#### All Inspector Refs Wired (HomeScene — fully wired, saved, 0 errors)

**HomeSceneManager (`[Managers]/HomeSceneManager`):**
| Field | Wired To |
|---|---|
| `_hud` | `PlayerHUD` component on `[HUD Canvas]/PlayerHUD` |
| `_skybox` | `SkyboxController` on `[Environment]/SkyboxManager` |
| `_networkManager` | `NereonNetworkManager` on `NetworkManager` GO |
| `_fadeOverlay` | `CanvasGroup` on `[HUD Canvas]/FadeOverlay` |

**NereonNetworkManager (`NetworkManager` GO):**
| Field | Wired To |
|---|---|
| `_networkManager` | `NetworkManager` component on same GO |
| `_hubPlayerPrefab` | `Assets/_NEREON/Prefabs/HubPlayer.prefab` |

#### HubPlayer Prefab Verified
`Assets/_NEREON/Prefabs/HubPlayer.prefab` already exists and is fully set up:
- Components: `CharacterController`, `ThirdPersonController`, `PlayerInput`, `StarterAssetsInputs`, `NetworkObject`, `NetworkTransform`, `PlayerSetup`, `RemotePlayerVisuals`, `BubbleChat`, `HubPlayerNetwork`
- Variant of `StarterAssets/ThirdPersonController/Prefabs/PlayerCapsule.prefab`
- Registered in NetworkManager's prefab list (required for NGO spawning)

#### WorldConfig.asset Verified
All 5 buildings populated with correct data (GameId 0-4, positions, districts, min levels). No changes needed.

#### SpawnPoint Tagged
`[Player]/SpawnPoint` GO now has `tag = "SpawnPoint"` — `PlayerSetup.PlaceAtSpawnPoint()` will find it correctly.

#### HomeScene State (as of end of session)
```
HomeScene
├── Main Camera
├── Directional Light
├── MapMagic       ← MapMagicObject + TerrainBuildingPlacer
├── [Environment]
│   ├── Sun (Light)
│   ├── River (MeshRenderer)
│   └── SkyboxManager  ← SkyboxController ✅ wired
├── [Districts]    ← 4 districts
├── [Player]
│   └── SpawnPoint  ← tag="SpawnPoint" ✅
├── [Managers]
│   ├── HomeSceneManager  ← ALL 4 refs wired ✅
│   └── AuthGuard (NereonAuthGuard)
├── [HUD Canvas]
│   ├── FadeOverlay   ← CanvasGroup ✅
│   ├── PlayerHUD     ← PlayerHUD ✅
│   └── ChatInputPanel
└── NetworkManager  ← NetworkManager + UnityTransport + NereonNetworkManager (all refs wired ✅)
```

#### Remaining Tasks (next session)
1. **Unity Project Settings → Services**: Link NEREON Unity org Project ID in Editor (Edit → Project Settings → Services). Required for Relay/Lobby to work at runtime.
2. **Register HubPlayer prefab in NGO**: NetworkManager → NetworkPrefabs list → add HubPlayer.prefab. Required for `SpawnWithOwnership` to work.
3. **Third-person camera setup**: Cinemachine Virtual Camera targeting player, follow + look-at.
4. **Building meshes**: Place KayKit/Quaternius building models in the 4 districts at WorldConfig positions.
5. **Notice Board UI**: World-space canvas on Central Plaza showing top-5 leaderboard per game.
6. **Test multiplayer flow**: Hit Play, ensure UGS initialises, Relay+Lobby connect, second player can join.

*Last updated: Session 8 — HomeScene fully wired (all Inspector refs), SpawnPoint tagged, WorldConfig verified.*

### Session 9 — Ambient Music System + Viking Village Water Fix

#### Viking Village Water (WaterSystemFeature.cs)
- `RenderTargetHandle` → `RTHandle` migration for Unity 6 / URP 17 compatibility
- `cmd.GetTemporaryRT` → `RenderingUtils.ReAllocateIfNeeded`
- `ConfigureTarget(m_WaterFX.Identifier())` → `ConfigureTarget(m_WaterFX)`
- `cmd.ReleaseTemporaryRT` → `m_WaterFX?.Release(); m_WaterFX = null` in `OnCameraCleanup`

#### Ambient Music System
**Architecture:**
- `AmbientMusicManager.cs` — DontDestroyOnLoad singleton, persists Landing → WelcomeInit, fades out when HomeScene loads
- `HomeAmbientManager.cs` — HomeScene-only, picks random track from pool each session, never repeats same track twice in a row

**Current audio asset:**
- `Assets/_NEREON/Sounds/Marconi Union - Weightless (253 Edit) (The Ambient Zone).mp3`
- Used for ALL scenes now (one track). Add more tracks to the arrays as more music is acquired.

**Volume settings:**
- LandingScene/WelcomeInit: `targetVolume = 0.4`, `fadeIn = 2s`, `fadeOut = 2s`
- HomeScene: `targetVolume = 0.35`, `fadeIn = 3s`, `loopCurrentTrack = true` (single track loops; set false when multiple tracks added)

**Setup (one-time):**
Run **NEREON → Setup → Wire Ambient Music** from Unity menu bar.
This Editor script (`NereonAmbientMusicSetup.cs`) will:
1. Open LandingScene → create `AmbientMusicManager` GO → wire `landingTracks[0]` = Weightless → save
2. Open WelcomeInitScene → remove any stale `AmbientMusicManager` GO (manager persists from Landing) → save
3. Open HomeScene → create `HomeAmbientManager` GO → wire `homeTracks[0]` = Weightless → save
4. Restore your original scene

**Adding more home tracks later:**
Select `HomeAmbientManager` GO in HomeScene Inspector → expand `homeTracks` array → add more AudioClips.
Set `loopCurrentTrack = false` so it auto-advances to next random track when one ends.

*Last updated: Session 9 — Viking Village water fixed, ambient music system implemented.*
