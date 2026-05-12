using UnityEngine;

public class DisableLogger : MonoBehaviour
{
    private void OnDisable()
    {
        Debug.Log($"[DisableLogger] {gameObject.name} se desactivó. StackTrace: " + System.Environment.StackTrace);
    }
}
