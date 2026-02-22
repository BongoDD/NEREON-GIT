use anchor_lang::prelude::*;

declare_id!("11111111111111111111111111111111"); // ⚠️  Replace after `anchor deploy`

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

        let p        = &mut ctx.accounts.user_profile;
        p.authority  = ctx.accounts.authority.key();
        p.avatar_id  = avatar_id;
        p.username   = username;
        p.created_at = now;
        p.bump       = ctx.bumps.user_profile;

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

    // ── submit_score ──────────────────────────────────────────────────────────
    /// Submit a mini-game score.
    /// - Awards XP to the character and checks for level-up.
    /// - Updates the monthly leaderboard top-5.
    /// - Creates the leaderboard account if this is the first score for that month.
    pub fn submit_score(
        ctx:     Context<SubmitScore>,
        game_id: u8,
        month:   u8,
        year:    u16,
        score:   u32,
    ) -> Result<()> {
        require!(month >= 1 && month <= 12, NereonError::InvalidMonth);

        let player = ctx.accounts.authority.key();

        // ── XP + Level-up ─────────────────────────────────────────────────────
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

        // ── Leaderboard ───────────────────────────────────────────────────────
        let lb = &mut ctx.accounts.game_leaderboard;

        // First submission for this game+month+year initialises the account.
        if !lb.is_initialized {
            lb.is_initialized     = true;
            lb.game_id            = game_id;
            lb.month              = month;
            lb.year               = year;
            lb.reward_distributed = false;
            lb.bump               = ctx.bumps.game_leaderboard;
        }

        lb.try_update(LeaderboardEntry { player, score });

        emit!(ScoreSubmitted { player, game_id, score, xp_earned });
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
#[instruction(game_id: u8, month: u8, year: u16)]
pub struct SubmitScore<'info> {
    #[account(
        mut,
        seeds   = [b"character", authority.key().as_ref()],
        bump    = character_stats.bump,
        has_one = authority,
    )]
    pub character_stats: Account<'info, CharacterStats>,

    #[account(
        init_if_needed,
        payer = authority,
        space = 8 + GameLeaderboard::INIT_SPACE,
        seeds = [b"leaderboard", &[game_id], &[month], &year.to_le_bytes()],
        bump
    )]
    pub game_leaderboard: Account<'info, GameLeaderboard>,

    #[account(mut)]
    pub authority: Signer<'info>,

    pub system_program: Program<'info, System>,
}

#[derive(Accounts)]
#[instruction(game_id: u8, month: u8, year: u16)]
pub struct DistributeRewards<'info> {
    #[account(
        mut,
        seeds = [b"leaderboard", &[game_id], &[month], &year.to_le_bytes()],
        bump  = game_leaderboard.bump,
    )]
    pub game_leaderboard: Account<'info, GameLeaderboard>,

    /// Anyone can trigger distribution – the check is purely state-based.
    pub caller: Signer<'info>,
}

// ─── Account Structs ──────────────────────────────────────────────────────────

#[account]
#[derive(InitSpace)]
pub struct UserProfile {
    pub authority:  Pubkey,   // 32
    pub avatar_id:  u8,       //  1
    pub username:   [u8; 32], // 32  — UTF-8, right-padded with zeros
    pub created_at: i64,      //  8
    pub bump:       u8,       //  1   total: 74 + 8 disc = 82
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
    pub game_id:            u8,                              //   1
    pub month:              u8,                              //   1
    pub year:               u16,                             //   2
    pub is_initialized:     bool,                            //   1
    pub top_entries:        [LeaderboardEntry; MAX_TOP_ENTRIES], // 5×36 = 180
    pub reward_distributed: bool,                            //   1
    pub bump:               u8,                              //   1  total: 187 + 8 = 195
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
}
