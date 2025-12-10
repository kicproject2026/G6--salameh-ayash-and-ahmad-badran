using UnityEngine;
using Normal.Realtime;   // Normcore namespace

public class VoiceMicIcon : MonoBehaviour
{
    [Header("Mic icon object (picture next to avatar)")]
    public GameObject micIcon; // assign in Inspector

    [Header("Detection settings")]
    public float checkInterval = 0.05f;
    public float volumeThreshold = 0.02f;

    private RealtimeAvatarVoice _voice;
    private float _timer;

    void Awake()
    {
        // Find the voice component on this avatar (in parent)
        _voice = GetComponentInParent<RealtimeAvatarVoice>();

        if (micIcon != null)
            micIcon.SetActive(false);   // start hidden
    }

    void Update()
    {
        if (_voice == null || micIcon == null)
            return;

        _timer += Time.deltaTime;
        if (_timer < checkInterval)
            return;
        _timer = 0f;

        // 0–1 volume from Normcore
        float v = _voice.voiceVolume;
        bool isTalking = v > volumeThreshold;

        micIcon.SetActive(isTalking);

        if (isTalking && Camera.main != null)
            micIcon.transform.LookAt(Camera.main.transform);
    }
}
