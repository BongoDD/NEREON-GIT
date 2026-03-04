use anchor_lang::prelude::*;

declare_id!("4cPPQDNMuwNnXRaNHxvo2gyDhwue1bE8MLzSW3VqcS4o"); // ✅ Real Program ID

// ─── Constants ────────────────────────────────────────────────────────────────

/// Top players tracked per game per calendar month.
pub const MAX_TOP_ENTRIES: usize = 5;

/// Base XP awarded on every game completion, regardless of score.
pub const XP_BASE: u32 = 10;

/// Extra XP per 100 score points (e.g. score 350 → +17 extra XP).
pub const XP_PER_100_SCORE: u32 = 5;

/// XP required to reach the next level = current_level × XP_LEVEL_BASE.
pub const XP_LEVEL_BASE: u32 = 100;

// ─── Program ──────────────────────────────────────────────────────────────────

#[program]
pub mod nereon {
    use super::*;

    // ── initialize_user ───────────────────────────────────────────────────────
    /// First-time login: creates UserProfile + CharacterStats PDAs in one tx.
    /// Called once per wallet — subsequent logins go straight to HomeScene.
    pub fn initialize_user(
        ctx:      Context<InitializeUser>,
        avatar_id: u8,
        username:  [u8; 32],
    ) -> Result<()> {
        let now = Clock::get()?.unix_timestamp;

        let p             = &mut ctx.accounts.user_profile;
        p.authority      = ctx.accounts.authority.key();
        p.avatar_id      = avatar_id;
        p.username       = username;
        p.created_at     = now;
        p.player_tier    = 0;  // 0=unset, 1=free, 2=pass holder
        p.bump           = ctx.bumps.user_profile;

        let s          = &mut ctx.accounts.character_stats;
        s.authority    = ctx.accounts.authority.key();
        s.level        = 1;
        s.xp           = 0;
        s.games_played = 0;
        s.bump         = ctx.bumps.character_stats;

        emit!(UserInitialized {
            authority:  ctx.accounts.authority.key(),
            created_at: now,
        });
        Ok(())
    }

    // ── update_profile ────────────────────────────────────────────────────────
    /// Change avatar choice and/or username. Only the owning wallet may call this.
    pub fn update_profile(
        ctx:      Context<UpdateProfile>,
        avatar_id: u8,
        username:  [u8; 32],
    ) -> Result<()> {
        let p       = &mut ctx.accounts.user_profile;
        p.avatar_id = avatar_id;
        p.username  = username;
        Ok(())
    }

    // ── set_player_tier ───────────────────────────────────────────────────────
    /// Set the player's pass tier (0=unset, 1=free w/ ads, 2=pass holder).
    /// Only the owning wallet may call this.
    pub fn set_player_tier(ctx: Context<SetPlayerTier>, tier: u8) -> Result<()> {
        require!(tier <= 2, NereonError::InvalidTier);
        ctx.accounts.user_profile.player_tier = tier;
        Ok(())
    }

    // ── submit_score ──────────────────────────────────────────────────────────
    /// Submit a mini-game score.
    /// - Collects an optional SOL entry fee (transfer to treasury).
    /// - Awards XP to the character and checks for level-up (all tiers).
    /// - Updates the monthly leaderboard top-5 (tier 2 / pass holders only).
    /// - Creates the leaderboard account if this is the first score for that month.
    pub fn submit_score(
        ctx:                Context<SubmitScore>,
        game_id:            u8,
        game_name:          [u8; 32],
        month:              u8,
        year:               u16,
        score:              u32,
        entry_fee_lamports: u64,
    ) -> Result<()> {
        require!(month >= 1 && month <= 12, NereonError::InvalidMonth);

        // ── Entry Fee ─────────────────────────────────────────────────────────
        if entry_fee_lamports > 0 {
            anchor_lang::solana_program::program::invoke(
                &anchor_lang::solana_program::system_instruction::transfer(
                    &ctx.accounts.authority.key(),
                    &ctx.accounts.treasury.key(),
                    entry_fee_lamports,
                ),
                &[
                    ctx.accounts.authority.to_account_info(),
                    ctx.accounts.treasury.to_account_info(),
                    ctx.accounts.system_program.to_account_info(),
                ],
            )?;
        }

        let player = ctx.accounts.authority.key();

        // ── XP + Level-up (all tiers) ─────────────────────────────────────────
        let s = &mut ctx.accounts.character_stats;
        let xp_earned = XP_BASE.saturating_add((score / 100).saturating_mul(XP_PER_100_SCORE));
        s.xp           = s.xp.saturating_add(xp_earned);
        s.games_played = s.games_played.saturating_add(1);

        loop {
            let xp_needed = (s.level as u32).saturating_mul(XP_LEVEL_BASE);
            if s.xp >= xp_needed && s.level < u16::MAX {
                s.level = s.level.saturating_add(1);
            } else {
                break;
            }
        }

        // ── Leaderboard (pass holders only) ───────────────────────────────────
        let lb = &mut ctx.accounts.game_leaderboard;

        // First submission for this game+month+year initialises the account.
        if !lb.is_initialized {
            lb.is_initialized     = true;
            lb.game_id            = game_id;
            lb.game_name          = game_name;
            lb.month              = month;
            lb.year               = year;
            lb.reward_distributed = false;
            lb.bump               = ctx.bumps.game_leaderboard;
        }

        // Only tier-2 (pass holders) earn leaderboard slots.
        let tier = ctx.accounts.user_profile.player_tier;
        if tier >= 2 {
            lb.try_update(LeaderboardEntry { player, score });
        }

        emit!(ScoreSubmitted { player, game_id, score, xp_earned });
        Ok(())
    }

    // ── close_user_accounts ───────────────────────────────────────────────────
    /// Dev / testing helper: closes both UserProfile and CharacterStats PDAs,
    /// returning rent lamports to the authority.
    /// Only the owning wallet can call this — the `has_one` + `close` constraints
    /// on the accounts context enforce this automatically.
    pub fn close_user_accounts(_ctx: Context<CloseUserAccounts>) -> Result<()> {
        // Anchor's `close = authority` attribute handles everything:
        // zeroes account data, transfers lamports, reassigns owner to System Program.
        Ok(())
    }

    // ── distribute_monthly_rewards ────────────────────────────────────────────
    /// Mark a monthly leaderboard as rewards distributed (callable by anyone).
    /// Emits the winners list so an off-chain keeper can transfer tokens.
    /// Full on-chain SPL token transfers added in Phase 7.
    pub fn distribute_monthly_rewards(
        ctx:      Context<DistributeRewards>,
        _game_id: u8,
        _month:   u8,
        _year:    u16,
    ) -> Result<()> {
        let lb = &mut ctx.accounts.game_leaderboard;
        require!(!lb.reward_distributed, NereonError::AlreadyDistributed);

        // TODO Phase 7: transfer NEREON SPL tokens from RewardEscrow to top-5.

        lb.reward_distributed = true;

        emit!(RewardsDistributed {
            game_id: lb.game_id,
            month:   lb.month,
            year:    lb.year,
            winners: lb.top_entries,
        });
        Ok(())
    }
}

// ─── Instruction Contexts ─────────────────────────────────────────────────────

#[derive(Accounts)]
pub struct InitializeUser<'info> {
    #[account(
        init,
        payer = authority,
        space = 8 + UserProfile::INIT_SPACE,
        seeds = [b"user_profile", authority.key().as_ref()],
        bump
    )]
    pub user_profile: Account<'info, UserProfile>,

    #[account(
        init,
        payer = authority,
        space = 8 + CharacterStats::INIT_SPACE,
        seeds = [b"character", authority.key().as_ref()],
        bump
    )]
    pub character_stats: Account<'info, CharacterStats>,

    #[account(mut)]
    pub authority: Signer<'info>,

    pub system_program: Program<'info, System>,
}

#[derive(Accounts)]
pub struct UpdateProfile<'info> {
    #[account(
        mut,
        seeds   = [b"user_profile", authority.key().as_ref()],
        bump    = user_profile.bump,
        has_one = authority,
    )]
    pub user_profile: Account<'info, UserProfile>,

    pub authority: Signer<'info>,
}

#[derive(Accounts)]
pub struct SetPlayerTier<'info> {
    #[account(
        mut,
        seeds   = [b"user_profile", authority.key().as_ref()],
        bump    = user_profile.bump,
        has_one = authority,
    )]
    pub user_profile: Account<'info, UserProfile>,

    pub authority: Signer<'info>,
}

#[derive(Accounts)]
#[instruction(game_id: u8, game_name: [u8; 32], month: u8, year: u16)]
pub struct SubmitScore<'info> {
    #[account(
        mut,
        seeds   = [b"character", authority.key().as_ref()],
        bump    = character_stats.bump,
        has_one = authority,
    )]
    pub character_stats: Account<'info, CharacterStats>,

    /// Read profile to gate leaderboard access by tier.
    #[account(
        seeds   = [b"user_profile", authority.key().as_ref()],
        bump    = user_profile.bump,
        has_one = authority,
    )]
    pub user_profile: Account<'info, UserProfile>,

    #[account(
        init_if_needed,
        payer = authority,
        space = 8 + GameLeaderboard::INIT_SPACE,
        seeds = [b"leaderboard".as_ref(), &[game_id], &[month], &year.to_le_bytes()],
        bump
    )]
    pub game_leaderboard: Account<'info, GameLeaderboard>,

    /// CHECK: Treasury wallet that receives entry fees. Verified by the game client.
    #[account(mut)]
    pub treasury: AccountInfo<'info>,

    #[account(mut)]
    pub authority: Signer<'info>,

    pub system_program: Program<'info, System>,
}

#[derive(Accounts)]
#[instruction(game_id: u8, month: u8, year: u16)]
pub struct DistributeRewards<'info> {
    #[account(
        mut,
        seeds = [b"leaderboard".as_ref(), &[game_id], &[month], &year.to_le_bytes()],
        bump  = game_leaderboard.bump,
    )]
    pub game_leaderboard: Account<'info, GameLeaderboard>,

    /// Anyone can trigger distribution – the check is purely state-based.
    pub caller: Signer<'info>,
}

// ─── Account Structs ───────────────────────────────────────────────────────────

#[derive(Accounts)]
pub struct CloseUserAccounts<'info> {
    #[account(
        mut,
        seeds   = [b"user_profile", authority.key().as_ref()],
        bump    = user_profile.bump,
        has_one = authority,
        close   = authority
    )]
    pub user_profile: Account<'info, UserProfile>,

    #[account(
        mut,
        seeds   = [b"character", authority.key().as_ref()],
        bump    = character_stats.bump,
        has_one = authority,
        close   = authority
    )]
    pub character_stats: Account<'info, CharacterStats>,

    #[account(mut)]
    pub authority: Signer<'info>,
}

#[account]
#[derive(InitSpace)]
pub struct UserProfile {
    pub authority:   Pubkey,   // 32
    pub avatar_id:   u8,       //  1
    pub username:    [u8; 32], // 32  — UTF-8, right-padded with zeros
    pub created_at:  i64,      //  8
    pub player_tier: u8,       //  1  — 0=unset, 1=free, 2=pass holder
    pub bump:        u8,       //  1   total: 75 + 8 disc = 83
}

#[account]
#[derive(InitSpace)]
pub struct CharacterStats {
    pub authority:    Pubkey, // 32
    pub level:        u16,    //  2
    pub xp:           u32,    //  4
    pub games_played: u32,    //  4
    pub bump:         u8,     //  1   total: 43 + 8 disc = 51
}

#[account]
#[derive(InitSpace)]
pub struct GameLeaderboard {
    pub game_id:            u8,                                  //   1
    pub month:              u8,                                  //   1
    pub year:               u16,                                 //   2
    pub is_initialized:     bool,                                //   1
    pub game_name:          [u8; 32],                            //  32  — UTF-8 minigame identifier
    pub top_entries:        [LeaderboardEntry; MAX_TOP_ENTRIES], // 5×36 = 180
    pub reward_distributed: bool,                                //   1
    pub bump:               u8,                                  //   1  total: 219 + 8 = 227
}

#[derive(AnchorSerialize, AnchorDeserialize, Clone, Copy, Default, InitSpace)]
pub struct LeaderboardEntry {
    pub player: Pubkey, // 32
    pub score:  u32,    //  4   total: 36
}

// ─── Leaderboard Logic ────────────────────────────────────────────────────────

impl GameLeaderboard {
    /// Insert or update a player's score in the top-5.
    /// - If the player is already present, update only if the new score is higher.
    /// - Otherwise, replace the lowest existing entry if the new score beats it.
    /// Entries are kept sorted descending by score at all times.
    pub fn try_update(&mut self, new_entry: LeaderboardEntry) {
        // Check if this player already has a slot.
        for entry in self.top_entries.iter_mut() {
            if entry.player == new_entry.player {
                if new_entry.score > entry.score {
                    entry.score = new_entry.score;
                    self.sort_desc();
                }
                return;
            }
        }
        // Find the lowest-scoring slot (score 0 = empty slot).
        let min_idx = self
            .top_entries
            .iter()
            .enumerate()
            .min_by_key(|(_, e)| e.score)
            .map(|(i, _)| i)
            .unwrap_or(0);

        if new_entry.score > self.top_entries[min_idx].score {
            self.top_entries[min_idx] = new_entry;
            self.sort_desc();
        }
    }

    fn sort_desc(&mut self) {
        self.top_entries
            .sort_unstable_by(|a, b| b.score.cmp(&a.score));
    }
}

// ─── Events ───────────────────────────────────────────────────────────────────

#[event]
pub struct UserInitialized {
    pub authority:  Pubkey,
    pub created_at: i64,
}

#[event]
pub struct ScoreSubmitted {
    pub player:    Pubkey,
    pub game_id:   u8,
    pub score:     u32,
    pub xp_earned: u32,
}

#[event]
pub struct RewardsDistributed {
    pub game_id: u8,
    pub month:   u8,
    pub year:    u16,
    pub winners: [LeaderboardEntry; MAX_TOP_ENTRIES],
}

// ─── Errors ───────────────────────────────────────────────────────────────────

#[error_code]
pub enum NereonError {
    #[msg("Rewards have already been distributed for this period.")]
    AlreadyDistributed,
    #[msg("Month must be between 1 and 12.")]
    InvalidMonth,
    #[msg("Only the designated authority may perform this action.")]
    Unauthorized,
    #[msg("Tier must be 0 (unset), 1 (free), or 2 (pass holder).")]
    InvalidTier,
}
