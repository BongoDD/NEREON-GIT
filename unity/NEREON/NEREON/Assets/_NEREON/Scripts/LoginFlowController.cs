using System;
using Cysharp.Threading.Tasks;
using Solana.Unity.SDK;
using Solana.Unity.Wallet;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Attach to a GameObject in the LandingScene.
/// After wallet login, checks the on-chain CharacterStats PDA:
///   - Account EXISTS  → returning user  → HomeScene
///   - Account MISSING → first-time user → WelcomeInitScene
/// No local storage used — the blockchain is the source of truth.
/// </summary>
public class LoginFlowController : MonoBehaviour
{
    [Tooltip("Optional spinner/panel to show while checking the blockchain.")]
    [SerializeField] private GameObject _loadingIndicator;

    private void OnEnable()  => Web3.OnLogin += HandleLogin;
    private void OnDisable() => Web3.OnLogin -= HandleLogin;

    public void HandleLogin(Account account)
    {
        if (account == null) return;
        CheckOnChainStatusAsync(account).Forget();
    }

    private async UniTaskVoid CheckOnChainStatusAsync(Account account)
    {
        if (_loadingIndicator) _loadingIndicator.SetActive(true);

        try
        {
            bool isInitialized = await NereonClient.IsUserInitializedAsync(account.PublicKey);
            SceneManager.LoadScene(isInitialized ? "HomeScene" : "WelcomeInitScene");
        }
        catch (Exception e)
        {
            Debug.LogError($"[NEREON] On-chain status check failed: {e.Message}");
            if (_loadingIndicator) _loadingIndicator.SetActive(false);
        }
    }
}

