using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Cysharp.Threading.Tasks;
using Solana.Unity.Programs;
using Solana.Unity.Rpc.Models;
using Solana.Unity.Rpc.Builders;
using Solana.Unity.SDK;
using Solana.Unity.Wallet;
using UnityEngine;

/// <summary>
/// Unity ↔ NEREON Anchor Program bridge.
///
/// Handles:
///   - PDA derivation for all on-chain accounts
///   - Account data fetching + Borsh deserialization
///   - Instruction builders for every program instruction
///
/// ⚠️  After running `anchor deploy`, replace PROGRAM_ID with the real value
///     printed in the console.
/// </summary>
public static class NereonClient
{
    // ── Program Identity ──────────────────────────────────────────────────────

    /// Replace this with your deployed Program ID after `anchor deploy`.
    public const string PROGRAM_ID = "4cPPQDNMuwNnXRaNHxvo2gyDhwue1bE8MLzSW3VqcS4o";

    private static readonly PublicKey ProgramKey = new PublicKey(PROGRAM_ID);

    // ── PDA Seeds (must match lib.rs exactly) ─────────────────────────────────

    private static readonly byte[] SeedUserProfile = Encoding.UTF8.GetBytes("user_profile");
    private static readonly byte[] SeedCharacter   = Encoding.UTF8.GetBytes("character");
    private static readonly byte[] SeedLeaderboard = Encoding.UTF8.GetBytes("leaderboard");

    // ── PDA Derivation ────────────────────────────────────────────────────────

    public static PublicKey DeriveUserProfilePDA(PublicKey wallet)
    {
        PublicKey.TryFindProgramAddress(
            new[] { SeedUserProfile, wallet.KeyBytes },
            ProgramKey, out var pda, out _);
        return pda;
    }

    public static PublicKey DeriveCharacterStatsPDA(PublicKey wallet)
    {
        PublicKey.TryFindProgramAddress(
            new[] { SeedCharacter, wallet.KeyBytes },
            ProgramKey, out var pda, out _);
        return pda;
    }

    /// game_id + month + year must match the seeds in lib.rs SubmitScore context.
    public static PublicKey DeriveLeaderboardPDA(byte gameId, byte month, ushort year)
    {
        var yearBytes = BitConverter.GetBytes(year);
        if (!BitConverter.IsLittleEndian) Array.Reverse(yearBytes);

        PublicKey.TryFindProgramAddress(
            new[] { SeedLeaderboard, new[] { gameId }, new[] { month }, yearBytes },
            ProgramKey, out var pda, out _);
        return pda;
    }

    // ── Account Checks ────────────────────────────────────────────────────────

    /// Returns true if a CharacterStats PDA already exists on-chain for this wallet.
    /// LoginFlowController calls this to decide first-time vs returning user.
    public static async UniTask<bool> IsUserInitializedAsync(PublicKey wallet)
    {
        var pda    = DeriveCharacterStatsPDA(wallet);
        var result = await Web3.Rpc.GetAccountInfoAsync(pda.Key);
        return result?.Result?.Value != null;
    }

    /// Decodes a fixed [u8;32] username byte array (stored on-chain) to a trimmed string.
    public static string DecodeUsername(byte[] usernameBytes)
    {
        if (usernameBytes == null || usernameBytes.Length == 0) return "Adventurer";
        // Find first null byte to trim padding
        int length = System.Array.IndexOf(usernameBytes, (byte)0);
        if (length < 0) length = usernameBytes.Length;
        return length == 0 ? "Adventurer" : Encoding.UTF8.GetString(usernameBytes, 0, length);
    }

    // ── Account Fetching ──────────────────────────────────────────────────────

    /// Fetch and deserialize the CharacterStats account. Returns null if not found.
    public static async UniTask<CharacterStatsData?> FetchCharacterStatsAsync(PublicKey wallet)
    {
        var pda    = DeriveCharacterStatsPDA(wallet);
        var result = await Web3.Rpc.GetAccountInfoAsync(pda.Key);
        if (result?.Result?.Value?.Data == null) return null;

        var raw = Convert.FromBase64String(result.Result.Value.Data[0]);
        return CharacterStatsData.Deserialize(raw);
    }

    /// Fetch and deserialize the UserProfile account. Returns null if not found.
    public static async UniTask<UserProfileData?> FetchUserProfileAsync(PublicKey wallet)
    {
        var pda    = DeriveUserProfilePDA(wallet);
        var result = await Web3.Rpc.GetAccountInfoAsync(pda.Key);
        if (result?.Result?.Value?.Data == null) return null;

        var raw = Convert.FromBase64String(result.Result.Value.Data[0]);
        return UserProfileData.Deserialize(raw);
    }

    /// Fetch and deserialize a GameLeaderboard account. Returns null if no scores yet.
    public static async UniTask<LeaderboardData?> FetchLeaderboardAsync(byte gameId, byte month, ushort year)
    {
        var pda    = DeriveLeaderboardPDA(gameId, month, year);
        var result = await Web3.Rpc.GetAccountInfoAsync(pda.Key);
        if (result?.Result?.Value?.Data == null) return null;

        var raw = Convert.FromBase64String(result.Result.Value.Data[0]);
        return LeaderboardData.Deserialize(raw);
    }

    // ── Instruction Builders ──────────────────────────────────────────────────

    /// initialize_user(avatar_id, username)
    /// Creates UserProfile + CharacterStats PDAs. Called once on first login.
    public static TransactionInstruction BuildInitializeUserIx(
        PublicKey authority,
        byte      avatarId,
        string    username)
    {
        var userProfilePDA    = DeriveUserProfilePDA(authority);
        var characterStatsPDA = DeriveCharacterStatsPDA(authority);

        // username → fixed [u8; 32] (Borsh encoding of fixed array = raw bytes)
        var usernameBytes = new byte[32];
        var encoded       = Encoding.UTF8.GetBytes(username ?? string.Empty);
        Array.Copy(encoded, usernameBytes, Math.Min(encoded.Length, 32));

        var data = new List<byte>(Discriminator("initialize_user")); // 8 bytes
        data.Add(avatarId);                                          // u8
        data.AddRange(usernameBytes);                                // [u8; 32]

        return new TransactionInstruction
        {
            ProgramId = ProgramKey.KeyBytes,
            Keys = new List<AccountMeta>
            {
                AccountMeta.Writable(userProfilePDA,    isSigner: false),
                AccountMeta.Writable(characterStatsPDA, isSigner: false),
                AccountMeta.Writable(authority,         isSigner: true),
                AccountMeta.ReadOnly(SystemProgram.ProgramIdKey, isSigner: false),
            },
            Data = data.ToArray()
        };
    }

    /// update_profile(avatar_id, username)
    public static TransactionInstruction BuildUpdateProfileIx(
        PublicKey authority,
        byte      avatarId,
        string    username)
    {
        var userProfilePDA = DeriveUserProfilePDA(authority);

        var usernameBytes = new byte[32];
        var encoded       = Encoding.UTF8.GetBytes(username ?? string.Empty);
        Array.Copy(encoded, usernameBytes, Math.Min(encoded.Length, 32));

        var data = new List<byte>(Discriminator("update_profile"));
        data.Add(avatarId);
        data.AddRange(usernameBytes);

        return new TransactionInstruction
        {
            ProgramId = ProgramKey.KeyBytes,
            Keys = new List<AccountMeta>
            {
                AccountMeta.Writable(userProfilePDA, isSigner: false),
                AccountMeta.ReadOnly(authority,      isSigner: true),
            },
            Data = data.ToArray()
        };
    }

    /// submit_score(game_id, month, year, score)
    /// Called after a mini-game ends. Awards XP and updates the leaderboard.
    public static TransactionInstruction BuildSubmitScoreIx(
        PublicKey authority,
        byte      gameId,
        byte      month,
        ushort    year,
        uint      score)
    {
        var characterStatsPDA = DeriveCharacterStatsPDA(authority);
        var leaderboardPDA    = DeriveLeaderboardPDA(gameId, month, year);

        var yearBytes  = ToLE(year);
        var scoreBytes = ToLE(score);

        var data = new List<byte>(Discriminator("submit_score"));
        data.Add(gameId);          // u8
        data.Add(month);           // u8
        data.AddRange(yearBytes);  // u16 LE
        data.AddRange(scoreBytes); // u32 LE

        return new TransactionInstruction
        {
            ProgramId = ProgramKey.KeyBytes,
            Keys = new List<AccountMeta>
            {
                AccountMeta.Writable(characterStatsPDA, isSigner: false),
                AccountMeta.Writable(leaderboardPDA,    isSigner: false),
                AccountMeta.Writable(authority,         isSigner: true),
                AccountMeta.ReadOnly(SystemProgram.ProgramIdKey, isSigner: false),
            },
            Data = data.ToArray()
        };
    }

    /// distribute_monthly_rewards(game_id, month, year)
    /// Anyone can call this. Marks the leaderboard closed and emits winner list.
    public static TransactionInstruction BuildDistributeRewardsIx(
        PublicKey caller,
        byte      gameId,
        byte      month,
        ushort    year)
    {
        var leaderboardPDA = DeriveLeaderboardPDA(gameId, month, year);
        var yearBytes      = ToLE(year);

        var data = new List<byte>(Discriminator("distribute_monthly_rewards"));
        data.Add(gameId);
        data.Add(month);
        data.AddRange(yearBytes);

        return new TransactionInstruction
        {
            ProgramId = ProgramKey.KeyBytes,
            Keys = new List<AccountMeta>
            {
                AccountMeta.Writable(leaderboardPDA, isSigner: false),
                AccountMeta.ReadOnly(caller,         isSigner: true),
            },
            Data = data.ToArray()
        };
    }

    // ── Internal Helpers ──────────────────────────────────────────────────────

    /// Compute the 8-byte Anchor instruction discriminator:
    ///   SHA256("global:<instruction_name>")[0..8]
    private static byte[] Discriminator(string name)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes($"global:{name}"));
        return hash[..8];
    }

    private static byte[] ToLE(ushort v)
    {
        var b = BitConverter.GetBytes(v);
        if (!BitConverter.IsLittleEndian) Array.Reverse(b);
        return b;
    }

    private static byte[] ToLE(uint v)
    {
        var b = BitConverter.GetBytes(v);
        if (!BitConverter.IsLittleEndian) Array.Reverse(b);
        return b;
    }
}

// ─── On-Chain Account Data Models ─────────────────────────────────────────────
// Borsh layout mirrors the Rust structs exactly (after the 8-byte discriminator).

[Serializable]
public struct UserProfileData
{
    public string Authority;   // base-58 pubkey string
    public byte   AvatarId;
    public byte[] Username;    // raw [u8; 32] — decode with NereonClient.DecodeUsername()
    public long   CreatedAt;   // Unix timestamp

    // Rust layout (after 8-byte discriminator):
    //   authority [32] | avatar_id [1] | username [32] | created_at [8] | bump [1]
    public static UserProfileData? Deserialize(byte[] raw)
    {
        const int MIN = 8 + 32 + 1 + 32 + 8 + 1;
        if (raw == null || raw.Length < MIN) return null;

        int o = 8; // skip discriminator
        var authority = new PublicKey(raw[o..(o + 32)]).Key; o += 32;
        var avatarId  = raw[o++];
        var username  = raw[o..(o + 32)];                    o += 32;
        var createdAt = BitConverter.ToInt64(raw, o);

        return new UserProfileData
        {
            Authority = authority,
            AvatarId  = avatarId,
            Username  = username,
            CreatedAt = createdAt,
        };
    }
}

[Serializable]
public struct CharacterStatsData
{
    public string Authority;  // base-58 pubkey string
    public ushort Level;
    public uint   Xp;
    public uint   GamesPlayed;

    // Rust layout (after 8-byte discriminator):
    //   authority [32] | level [2] | xp [4] | games_played [4] | bump [1]
    public static CharacterStatsData? Deserialize(byte[] raw)
    {
        const int MIN = 8 + 32 + 2 + 4 + 4 + 1;
        if (raw == null || raw.Length < MIN) return null;

        int o = 8; // skip discriminator
        var authority   = new PublicKey(raw[o..(o + 32)]).Key; o += 32;
        var level       = BitConverter.ToUInt16(raw, o);       o += 2;
        var xp          = BitConverter.ToUInt32(raw, o);       o += 4;
        var gamesPlayed = BitConverter.ToUInt32(raw, o);

        return new CharacterStatsData
        {
            Authority  = authority,
            Level      = level,
            Xp         = xp,
            GamesPlayed = gamesPlayed
        };
    }
}

[Serializable]
public struct LeaderboardEntryData
{
    public string Player; // base-58
    public uint   Score;
}

[Serializable]
public struct LeaderboardData
{
    public byte   GameId;
    public byte   Month;
    public ushort Year;
    public bool   RewardDistributed;
    public LeaderboardEntryData[] TopEntries; // always length 5

    // Rust layout (after 8-byte discriminator):
    //   game_id [1] | month [1] | year [2] | is_initialized [1]
    //   top_entries: 5 × (pubkey[32] + score[4])
    //   reward_distributed [1] | bump [1]
    public static LeaderboardData? Deserialize(byte[] raw)
    {
        const int ENTRIES    = 5;
        const int ENTRY_SIZE = 32 + 4;
        const int MIN        = 8 + 1 + 1 + 2 + 1 + (ENTRIES * ENTRY_SIZE) + 1 + 1;
        if (raw == null || raw.Length < MIN) return null;

        int o = 8;
        var gameId   = raw[o++];
        var month    = raw[o++];
        var year     = BitConverter.ToUInt16(raw, o); o += 2;
        o++; // is_initialized bool

        var entries = new LeaderboardEntryData[ENTRIES];
        for (int i = 0; i < ENTRIES; i++)
        {
            var player = new PublicKey(raw[o..(o + 32)]).Key; o += 32;
            var score  = BitConverter.ToUInt32(raw, o);       o += 4;
            entries[i] = new LeaderboardEntryData { Player = player, Score = score };
        }

        var rewardDist = raw[o] != 0;

        return new LeaderboardData
        {
            GameId            = gameId,
            Month             = month,
            Year              = year,
            RewardDistributed = rewardDist,
            TopEntries        = entries
        };
    }
}
