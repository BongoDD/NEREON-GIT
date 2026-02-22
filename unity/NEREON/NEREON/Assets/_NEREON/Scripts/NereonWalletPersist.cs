using Solana.Unity.Rpc;
using Solana.Unity.SDK;
using UnityEngine;

/// <summary>
/// Attach to the same GameObject as the Solana Web3 component in LandingScene.
/// [DefaultExecutionOrder(-32000)] ensures Awake runs BEFORE Web3's Awake so
/// rpcCluster is already DevNet when Web3 initialises its RPC client.
/// Start() then replaces Web3.Rpc as a belt-and-braces guarantee.
/// </summary>
[DefaultExecutionOrder(-32000)]
public class NereonWalletPersist : MonoBehaviour
{
    private static NereonWalletPersist _instance;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        // Force DevNet BEFORE Web3.Awake() creates its RPC client
        var web3 = GetComponent<Web3>();
        if (web3 != null)
        {
            web3.rpcCluster    = RpcCluster.DevNet;
            web3.customRpc     = string.Empty;   // clear any hard-coded mainnet URL
            web3.webSocketsRpc = string.Empty;
            Debug.Log("[NEREON] NereonWalletPersist: rpcCluster forced → DevNet (pre-init)");
        }

        Debug.Log("[NEREON] Wallet GameObject marked DontDestroyOnLoad.");
    }
}

