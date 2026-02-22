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

### Phase 1 — Toolchain (dev machine, one-time)
- [ ] Install Rust (`rustup.rs`)
- [ ] Install Solana CLI
- [ ] Install Anchor CLI (`avm`)
- [ ] Create deploy keypair, fund on Devnet

### Phase 2 — Anchor Program (Core)
- [ ] `UserProfile` + `CharacterStats` accounts
- [ ] `initialize_user` instruction
- [ ] `update_profile` instruction
- [ ] `submit_score` instruction + XP award logic
- [ ] `GameLeaderboard` + top-5 tracking
- [ ] `distribute_monthly_rewards` instruction (crank)
- [ ] `RewardEscrow` + `fund_escrow` instruction
- [ ] `anchor build` → IDL generated
- [ ] `anchor deploy` → Devnet Program ID captured

### Phase 3 — Unity ↔ Anchor Bridge
- [ ] Add `com.magicblock.solana.anchor` to `Packages/manifest.json`
- [ ] Copy IDL into `Assets/_NEREON/IDL/nereon.json`
- [ ] Write `NereonClient.cs` (PDA derivation, account fetch, instruction builders)

### Phase 4 — Replace PlayerPrefs (Login Flow)
- [ ] `LoginFlowController` → async RPC PDA existence check
- [ ] `WelcomeInitController` → send `initialize_user` transaction

### Phase 5 — Town Hub World
- [ ] Design town layout (buildings map)
- [ ] Third-person character controller
- [ ] Building enter/exit trigger zones
- [ ] Multiplayer presence (other players visible)

### Phase 6 — Mini-Game Framework
- [ ] `IMinigame` interface in Unity
- [ ] Score submission flow (play → score → sign → `submit_score` tx)
- [ ] XP & level-up feedback UI
- [ ] Per-building monthly leaderboard UI panel

### Phase 7 — NEREON Token
- [ ] Mint NEREON SPL token
- [ ] Fund RewardEscrow on-chain
- [ ] Test `distribute_monthly_rewards` on Devnet

### Phase 8 — First Mini-Game (TBD)
- [ ] Choose game type for Building_01
- [ ] Build game mechanics (skill + luck element)
- [ ] Integrate with mini-game framework

### Phase 9 — Seeker Submission
- [ ] End-to-end Devnet test
- [ ] Promote to Mainnet
- [ ] Mobile Wallet Adapter verified on Seeker hardware
- [ ] Submit via https://seeker.solana.com

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
| 2026-02-21 | Character levels up via XP earned from mini-games | RPG progression loop keeps players engaged across all games |
| 2026-02-21 | Each game gets its own leaderboard PDA (game_id + month) | Modular — new games added without changing existing accounts |

---

## ❓ Open Questions / TBD
- What type of mini-games go in each building? (card, puzzle, arcade, dice, fishing…) — *TBD*
- How many buildings in the first version of the town? — *TBD*
- NEREON token: new SPL mint or use SOL directly for rewards? — *TBD*
- Multiplayer solution for the hub world: Photon PUN2, Mirror, Unity Netcode? — *TBD*
- On-chain randomness: Switchboard VRF or Orao VRF? — *TBD*
- Monthly reward distribution: who triggers the crank? (keeper bot, Clockwork, manual?) — *TBD*
- XP curve: how much XP per game? How many levels? — *TBD*
- Art style reference: what games inspire the visual direction? — *TBD*

---

---

## 📋 Session Log

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
