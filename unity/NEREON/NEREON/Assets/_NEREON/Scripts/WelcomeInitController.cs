using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Solana.Unity.Rpc.Builders;
using Solana.Unity.Rpc.Types;
using Solana.Unity.SDK;
using Solana.Unity.Wallet;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Handles only the Solana transaction for first-time player registration.
/// All UI flow is managed by WelcomeSceneFlow (same GameObject).
///
/// WelcomeSceneFlow sets AvatarId + Username, then calls CompleteSetup().
/// Status updates are routed back to WelcomeSceneFlow.SetConfirmStatus().
/// </summary>
public class WelcomeInitController : MonoBehaviour
{
    [Header("Player choices — written by WelcomeSceneFlow before CompleteSetup()")]
    [Tooltip("Avatar index chosen by the player (0–255).")]
    [Range(0, 255)]
    public byte AvatarId = 0;

    [Tooltip("Username entered by the player (max 20 chars, stored as 32 bytes on-chain).")]
    public string Username = "Adventurer";

    // WelcomeSceneFlow on the same GameObject — used for status feedback
    private WelcomeSceneFlow _flow;

    private void Awake() => _flow = GetComponent<WelcomeSceneFlow>();

    /// <summary>Called by WelcomeSceneFlow after the player hits "ENTER NEREON".</summary>
    public void CompleteSetup() => CompleteSetupAsync().Forget();

    private async UniTaskVoid CompleteSetupAsync()
    {
        SetStatus("Saving your profile to the blockchain…", loading: true);

        try
        {
            var wallet = Web3.Account;

            if (wallet == null)
            {
                SetStatus("No wallet connected — returning to login.", loading: false);
                Debug.LogWarning("[NEREON] CompleteSetup: Web3.Account is null.");
                await UniTask.Delay(1500);
                SceneManager.LoadScene("LandingScene");
                return;
            }

            // ── Check balance first ───────────────────────────────────────────
            SetStatus("Checking wallet balance…", loading: true);
            var balanceResult = await Web3.Rpc.GetBalanceAsync(wallet.PublicKey.Key);
            ulong lamports = balanceResult?.Result?.Value ?? 0;
            Debug.Log($"[NEREON] Wallet balance: {lamports} lamports ({lamports / 1_000_000_000.0} SOL)");

            if (lamports < 10_000_000) // less than 0.01 SOL
            {
                SetStatus("Wallet has insufficient SOL. Visit faucet.solana.com to get devnet SOL.", loading: false);
                Debug.LogError("[NEREON] Insufficient balance for transaction fees.");
                return;
            }

            SetStatus("Fetching latest block…", loading: true);
            var blockHash = await Web3.Rpc.GetLatestBlockHashAsync();

            if (blockHash?.Result?.Value == null)
            {
                SetStatus("Cannot reach Solana RPC — check your connection.", loading: false);
                Debug.LogError($"[NEREON] GetLatestBlockHash failed: {blockHash?.Reason}");
                return;
            }

            var ix = NereonClient.BuildInitializeUserIx(wallet.PublicKey, AvatarId, Username);

            Debug.Log($"[NEREON] Building tx: programId={NereonClient.PROGRAM_ID}, " +
                      $"avatar={AvatarId}, username={Username}, " +
                      $"accounts={ix.Keys.Count}, dataLen={ix.Data.Length}");

            SetStatus("Signing transaction…", loading: true);
            byte[] txBytes = new TransactionBuilder()
                .SetRecentBlockHash(blockHash.Result.Value.Blockhash)
                .SetFeePayer(wallet.PublicKey)
                .AddInstruction(ix)
                .Build(new List<Account> { wallet });

            SetStatus("Sending to network…", loading: true);
            Debug.Log($"[NEREON] Sending tx ({txBytes.Length} bytes)");

            var result = await Web3.Rpc.SendTransactionAsync(
                txBytes,
                skipPreflight: false,
                commitment: Commitment.Confirmed);

            if (result?.Result == null)
            {
                var errMsg = result?.Reason ?? "unknown error";
                Debug.LogError($"[NEREON] SendTransaction failed: {errMsg}");
                
                // Log the full error for debugging
                if (result?.RawRpcResponse != null)
                    Debug.LogError($"[NEREON] Raw RPC response: {result.RawRpcResponse}");

                SetStatus($"Transaction failed: {errMsg}", loading: false);
                return;
            }

            SetStatus("Confirming on-chain…", loading: true);
            await Web3.Rpc.ConfirmTransaction(result.Result, Commitment.Confirmed);

            Debug.Log($"[NEREON] Profile created. Tx: {result.Result}");
            SceneManager.LoadScene("HomeScene");
        }
        catch (Exception e)
        {
            Debug.LogError($"[NEREON] initialize_user failed: {e.Message}\n{e.StackTrace}");
            SetStatus($"Error: {e.Message}", loading: false);
        }
    }

    private void SetStatus(string message, bool loading)
    {
        if (_flow != null) { _flow.SetConfirmStatus(message, loading); return; }
        Debug.Log($"[WelcomeInit] {message}");
    }
}

