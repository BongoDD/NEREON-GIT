using Cysharp.Threading.Tasks;
using Solana.Unity.SDK;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Drop this component on any scene that requires a connected wallet.
/// On Start it checks Web3.Account; if null it redirects to LandingScene.
///
/// Add to:
///   WelcomeInitScene  — FlowManager GameObject
///   HomeScene         — [Managers] or equivalent root GO
///   Any mini-game scene
/// </summary>
public class NereonAuthGuard : MonoBehaviour
{
    [Tooltip("How long (ms) to wait before redirecting — gives the SDK time to restore a session.")]
    [SerializeField] private int _graceMs = 3000;

    private void Start() => CheckAuthAsync().Forget();

    private async UniTaskVoid CheckAuthAsync()
    {
        // Brief grace period so the Solana SDK can finish restoring a saved session.
        await UniTask.Delay(_graceMs);

        if (Web3.Account != null) return; // authenticated — all good

        Debug.LogWarning($"[NereonAuthGuard] No wallet in '{SceneManager.GetActiveScene().name}' — redirecting to LandingScene.");
        SceneManager.LoadScene("LandingScene");
    }
}
