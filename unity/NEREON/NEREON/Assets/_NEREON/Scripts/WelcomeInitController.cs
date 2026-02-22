trausing UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Attach to a GameObject in the WelcomeInitScene.
/// Call CompleteSetup() (e.g. from the "Finish" / "Enter" button) once the user
/// has finished avatar customisation.  This marks the account as initialised and
/// loads the HomeScene.  The scene will never be shown again for this wallet.
/// </summary>
public class WelcomeInitController : MonoBehaviour
{
    /// <summary>
    /// Call this when the user finishes the welcome / avatar-customisation flow.
    /// Wire it up to your UI "Finish" button via the Inspector or code.
    /// </summary>
    public void CompleteSetup()
    {
        // Mark this wallet as having completed the initial setup
        string prefKey = PlayerPrefs.GetString("nereon_pending_setup_key", string.Empty);
        if (!string.IsNullOrEmpty(prefKey))
        {
            PlayerPrefs.SetString(prefKey, "done");
            PlayerPrefs.DeleteKey("nereon_pending_setup_key");
            PlayerPrefs.Save();
        }

        SceneManager.LoadScene("HomeScene");
    }
}
