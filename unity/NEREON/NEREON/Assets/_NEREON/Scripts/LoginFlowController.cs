using System;
using Cysharp.Threading.Tasks;
using Solana.Unity.SDK;
using Solana.Unity.Wallet;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Attach to a GameObject in the LandingScene.
///
/// Flow:
///   1. User connects wallet via the SDK wallet button.
///   2. HandleLogin fires — enables the "ENTER NEREON" button.
///   3. User presses "ENTER NEREON" → checks on-chain CharacterStats PDA:
///        • Account EXISTS  → returning user  → HomeScene
///        • Account MISSING → first-time user → WelcomeInitScene
///
/// Scene never auto-navigates; the player always initiates via the button.
/// </summary>
public class LoginFlowController : MonoBehaviour
{
    [Tooltip("'ENTER NEREON' button — sits above the wallet button, disabled until logged in.")]
    [SerializeField] private Button _btnEnterNereon;

    [Tooltip("Status text shown below the logo (e.g. 'Connect your wallet to continue').")]
    [SerializeField] private TextMeshProUGUI _walletStatusLabel;

    [Tooltip("Optional spinner/panel shown while checking the blockchain.")]
    [SerializeField] private GameObject _loadingIndicator;

    private void Start()
    {
        // Ensure button starts disabled
        if (_btnEnterNereon != null)
        {
            _btnEnterNereon.interactable = false;
            _btnEnterNereon.onClick.AddListener(OnEnterNereonClicked);
        }

        // Already logged in (e.g. session restored before Start)
        if (Web3.Account != null)
            SetButtonReady(true);
    }

    private void OnEnable()  => Web3.OnLogin += HandleLogin;
    private void OnDisable() => Web3.OnLogin -= HandleLogin;

    public void HandleLogin(Account account)
    {
        if (account == null) return;
        SetButtonReady(true);
        Debug.Log($"[NEREON] Wallet connected: {account.PublicKey}");
    }

    private void SetButtonReady(bool ready)
    {
        if (_btnEnterNereon != null)
            _btnEnterNereon.interactable = ready;

        if (_walletStatusLabel != null)
            _walletStatusLabel.text = ready
                ? "Wallet connected  —  press ENTER NEREON to begin"
                : "Connect your wallet to continue";
    }

    private void OnEnterNereonClicked()
    {
        if (Web3.Account == null)
        {
            Debug.LogWarning("[NEREON] EnterNereon pressed but no wallet connected.");
            return;
        }
        SetButtonReady(false);
        ProceedAsync(Web3.Account).Forget();
    }

    private async UniTaskVoid ProceedAsync(Account account)
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
            SetButtonReady(true);
        }
    }
}

