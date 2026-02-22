using UnityEngine;

/// <summary>Rotates a UI element continuously — used for the loading spinner.</summary>
public class UISpinner : MonoBehaviour
{
    [SerializeField] private float _speed = 300f;
    private void Update() => transform.Rotate(0f, 0f, -_speed * Time.deltaTime);
}
