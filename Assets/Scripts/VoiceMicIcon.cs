using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class VoiceMicIcon : MonoBehaviour
{
    [Header("Mic icon object (picture next to avatar)")]
    public GameObject micIcon;      // Drag your MicIcon here in the Inspector

    [Header("Detection settings")]
    public float checkInterval = 0.1f;   // How often to check volume (seconds)
    public float volumeThreshold = 0.01f; // Sensitivity: lower = more sensitive

    private AudioSource _audioSource;
    private float _timer;
    private float[] _samples;

    void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        _samples = new float[256];

        if (micIcon != null)
            micIcon.SetActive(false);
    }

    void Update()
    {
        if (_audioSource == null || micIcon == null)
            return;

        _timer += Time.deltaTime;
        if (_timer < checkInterval)
            return;
        _timer = 0f;

        // Read audio samples from the voice AudioSource
        _audioSource.GetOutputData(_samples, 0);

        float sum = 0f;
        for (int i = 0; i < _samples.Length; i++)
        {
            sum += _samples[i] * _samples[i]; // power
        }

        float rms = Mathf.Sqrt(sum / _samples.Length); // Root Mean Square

        bool isTalking = rms > volumeThreshold && _audioSource.isPlaying;

        // Show / hide the mic picture
        micIcon.SetActive(isTalking);

        // Optional: make mic icon face the camera
        if (micIcon.activeSelf && Camera.main != null)
        {
            micIcon.transform.LookAt(Camera.main.transform);
        }
    }
}
