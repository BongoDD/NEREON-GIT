using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Solana.Unity.Rpc.Models;
using Solana.Unity.Rpc.Types;
using Solana.Unity.SDK;
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
            var wallet    = Web3.Account;
            var blockHash = await Web3.Rpc.GetLatestBlockHashAsync();

            var ix = NereonClient.BuildInitializeUserIx(wallet.PublicKey, AvatarId, Username);

            var transaction = new Transaction
            {
                RecentBlockHash = blockHash.Result.Value.Blockhash,
                FeePayer        = wallet.PublicKey,
                Instructions    = new List<TransactionInstruction> { ix },
                Signatures      = new List<SignaturePubKeyPair>()
            };

            var result = await Web3.Wallet.SignAndSendTransaction(transaction);

            if (result?.Result == null)
            {
                SetStatus("Transaction failed — please try again.", loading: false);
                return;
            }

            SetStatus("Confirming on-chain…", loading: true);
            await Web3.Rpc.ConfirmTransaction(result.Result, Commitment.Confirmed);

            Debug.Log($"[NEREON] Profile created on-chain. Tx: {result.Result}");
            SceneManager.LoadScene("HomeScene");
        }
        catch (Exception e)
        {
            Debug.LogError($"[NEREON] initialize_user failed: {e.Message}");
            SetStatus("Something went wrong — please try again.", loading: false);
        }
    }

    private void SetStatus(string message, bool loading)
    {
        // Prefer WelcomeSceneFlow for status display (it owns the Confirming panel)
        if (_flow != null) { _flow.SetConfirmStatus(message, loading); return; }

        Debug.Log($"[WelcomeInit] {message}");
    }
}

