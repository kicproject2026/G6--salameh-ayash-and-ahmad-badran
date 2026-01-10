// RecordingManager.cs
// - Records a corner camera to JPG frames, merges with audio.wav using FFmpeg into recording.mp4
// - Prevents "sped up" issues by using -framerate and waiting pending async writes
// - Names folders like: DoctorName-PatientName/v001, v002, v003...
// - Doctor name comes from SessionData.CurrentUser.username (login)
// - Patient name is typed by doctor in a TMP_InputField (patientNameInput)
// - Shows a TMP_Text error if Start Recording is pressed with empty patient name
// - Error auto-hides after 3 seconds
//
// REQUIREMENTS:
// - ffmpeg.exe at: Assets/StreamingAssets/FFmpeg/ffmpeg.exe (and commit it)
// - UnityAudioRecorder attached to the AudioListener (Main Camera) and assigned here
// - Assign patientNameInput + patientNameErrorText in Inspector (RecordingSystem -> RecordingManager)

using TMPro;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class RecordingManager : MonoBehaviour
{
    [Header("Capture Source")]
    public Camera recordCamera;
    public RenderTexture recordRT;

    [Header("Quality / Performance")]
    [Tooltip("Capture FPS. 10–15 recommended.")]
    public int fps = 15;

    [Tooltip("JPG quality 1–100. 60–80 recommended.")]
    [Range(1, 100)] public int jpgQuality = 70;

    [Tooltip("Write frames to disk on a background thread (less stutter).")]
    public bool asyncDiskWrite = true;

    [Tooltip("Delete frame images after mp4 is created.")]
    public bool deleteFramesAfterEncode = true;

    [Tooltip("Delete audio.wav after mp4 is created.")]
    public bool deleteAudioAfterEncode = true;

    [Header("Output")]
    public string outputFolderName = "Recordings";
    public string ffmpegExePathFallback = "ffmpeg"; // only used if bundled ffmpeg is missing

    [Header("Audio (recommended)")]
    public UnityAudioRecorder audioRecorder;

    [Header("Naming (doctor types patient name)")]
    public TMP_InputField patientNameInput;

    [Header("UI Feedback")]
    public TMP_Text patientNameErrorText;

    // Internal
    private string ffmpegPath;
    private bool isRecording;
    private int frameIndex;
    private string sessionFolder;
    private Texture2D readTex;
    private Coroutine routine;

    // Track pending async file writes so FFmpeg doesn't start early
    private int pendingWrites = 0;

    // Error auto-hide
    private Coroutine errorRoutine;

    void Awake()
    {
        // Bundled ffmpeg path (best for GitHub + other PCs)
        string bundled = Path.Combine(Application.streamingAssetsPath, "FFmpeg", "ffmpeg.exe");
        ffmpegPath = File.Exists(bundled) ? bundled : ffmpegExePathFallback;

        Debug.Log("FFmpeg path used: " + ffmpegPath);

        // Hide error text by default
        if (patientNameErrorText != null)
            patientNameErrorText.gameObject.SetActive(false);

        // Optional: auto-hide error while typing
        if (patientNameInput != null && patientNameErrorText != null)
        {
            patientNameInput.onValueChanged.AddListener(_ =>
            {
                patientNameErrorText.gameObject.SetActive(false);
            });
        }
    }

    public void StartRecording()
    {
        if (isRecording) return;

        if (recordCamera == null || recordRT == null)
        {
            Debug.LogError("RecordingManager: Assign recordCamera and recordRT in Inspector.");
            return;
        }

        // Require patient name (typed by doctor)
        if (patientNameInput != null && string.IsNullOrWhiteSpace(patientNameInput.text))
        {
            ShowPatientNameError("Please enter patient name before recording.");
            return;
        }

        // Hide error once we start correctly
        HidePatientNameError();

        // Root recordings folder
        string root = Path.Combine(Application.persistentDataPath, outputFolderName);
        Directory.CreateDirectory(root);

        // Pair folder: DoctorName-PatientName
        string pairPath = Path.Combine(root, GetPairFolder());
        Directory.CreateDirectory(pairPath);

        // Every recording = next version folder (v001, v002, ...)
        sessionFolder = GetNextVersionFolder(pairPath);

        // Prepare read texture matching RT size
        readTex = new Texture2D(recordRT.width, recordRT.height, TextureFormat.RGB24, false);

        // Start audio recording (captures Unity mixed output)
        if (audioRecorder != null)
            audioRecorder.StartAudio(sessionFolder);
        else
            Debug.LogWarning("RecordingManager: audioRecorder is not assigned. MP4 will be video-only.");

        isRecording = true;
        frameIndex = 0;
        pendingWrites = 0;

        routine = StartCoroutine(CaptureLoop());
        Debug.Log("Recording started: " + sessionFolder);
    }

    public void StopRecording()
    {
        if (!isRecording) return;
        isRecording = false;

        if (routine != null)
            StopCoroutine(routine);

        // Stop audio first so audio.wav is finalized
        if (audioRecorder != null)
            audioRecorder.StopAudio();

        Debug.Log("Recording stopped. Waiting for pending frame writes...");
        WaitForPendingWrites();

        Debug.Log("Encoding mp4...");
        EncodeToMp4();
    }

    private IEnumerator CaptureLoop()
    {
        // Stable timing using unscaled time
        float interval = 1f / Mathf.Max(1, fps);
        float nextTime = Time.unscaledTime;

        while (isRecording)
        {
            yield return new WaitForEndOfFrame();

            if (Time.unscaledTime < nextTime)
                continue;

            nextTime += interval;

            // Render camera to RT and read pixels
            var prev = RenderTexture.active;
            RenderTexture.active = recordRT;

            recordCamera.Render();

            readTex.ReadPixels(new Rect(0, 0, recordRT.width, recordRT.height), 0, 0);
            readTex.Apply(false);

            // Encode JPG (fast)
            byte[] jpg = readTex.EncodeToJPG(jpgQuality);
            string framePath = Path.Combine(sessionFolder, $"frame_{frameIndex:D06}.jpg");
            frameIndex++;

            RenderTexture.active = prev;

            if (asyncDiskWrite)
            {
                Interlocked.Increment(ref pendingWrites);
                byte[] dataCopy = jpg; // capture ref for background thread
                Task.Run(() =>
                {
                    try { File.WriteAllBytes(framePath, dataCopy); }
                    catch { /* ignore */ }
                    finally { Interlocked.Decrement(ref pendingWrites); }
                });
            }
            else
            {
                File.WriteAllBytes(framePath, jpg);
            }
        }
    }

    private void WaitForPendingWrites()
    {
        // Wait up to ~3 seconds (usually much less)
        int safety = 0;
        while (pendingWrites > 0 && safety < 300)
        {
            System.Threading.Thread.Sleep(10);
            safety++;
        }

        if (pendingWrites > 0)
            Debug.LogWarning("Some frame writes may still be pending. Encoding anyway.");
        else
            Debug.Log("All frame writes completed.");
    }

    private void EncodeToMp4()
    {
        string outMp4 = Path.Combine(sessionFolder, "recording.mp4");
        string inputPattern = Path.Combine(sessionFolder, "frame_%06d.jpg");
        string wavPath = Path.Combine(sessionFolder, "audio.wav");

        string args;

        if (File.Exists(wavPath))
        {
            args =
                $"-y -framerate {fps} -i \"{inputPattern}\" " +
                $"-i \"{wavPath}\" " +
                "-c:v libx264 -pix_fmt yuv420p " +
                "-c:a aac -b:a 192k " +
                "-shortest " +
                $"\"{outMp4}\"";
        }
        else
        {
            args =
                $"-y -framerate {fps} -i \"{inputPattern}\" " +
                "-c:v libx264 -pix_fmt yuv420p " +
                $"\"{outMp4}\"";
        }

        try
        {
            var p = new Process();
            p.StartInfo.FileName = ffmpegPath;
            p.StartInfo.Arguments = args;
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.RedirectStandardOutput = true;

            p.Start();
            string err = p.StandardError.ReadToEnd();
            string stdout = p.StandardOutput.ReadToEnd();
            p.WaitForExit();

            if (File.Exists(outMp4))
            {
                Debug.Log("Saved video: " + outMp4);

                if (deleteFramesAfterEncode)
                {
                    foreach (var f in Directory.GetFiles(sessionFolder, "frame_*.jpg"))
                    {
                        try { File.Delete(f); } catch { }
                    }
                    Debug.Log("Deleted frame images.");
                }

                if (deleteAudioAfterEncode && File.Exists(wavPath))
                {
                    try { File.Delete(wavPath); } catch { }
                    Debug.Log("Deleted audio.wav.");
                }
            }
            else
            {
                Debug.LogError("FFmpeg finished but recording.mp4 was not found.");
            }

            if (!string.IsNullOrEmpty(stdout)) Debug.Log(stdout);
            if (!string.IsNullOrEmpty(err)) Debug.Log(err);
        }
        catch (System.Exception e)
        {
            Debug.LogError("FFmpeg failed: " + e.Message);
            Debug.LogError("Expected ffmpeg at: Assets/StreamingAssets/FFmpeg/ffmpeg.exe");
        }
    }

    // ---------- UI helpers ----------

    private void ShowPatientNameError(string msg)
    {
        if (patientNameErrorText == null) return;

        patientNameErrorText.text = msg;
        patientNameErrorText.gameObject.SetActive(true);
        Debug.LogError(msg);

        // restart timer if already running
        if (errorRoutine != null)
            StopCoroutine(errorRoutine);

        errorRoutine = StartCoroutine(HideErrorAfterSeconds(3f));
    }

    private IEnumerator HideErrorAfterSeconds(float seconds)
    {
        yield return new WaitForSeconds(seconds);

        if (patientNameErrorText != null)
            patientNameErrorText.gameObject.SetActive(false);

        errorRoutine = null;
    }

    private void HidePatientNameError()
    {
        if (errorRoutine != null)
        {
            StopCoroutine(errorRoutine);
            errorRoutine = null;
        }

        if (patientNameErrorText != null)
            patientNameErrorText.gameObject.SetActive(false);
    }

    // ---------- Naming helpers ----------

    private static string SanitizeFilePart(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "Unknown";
        s = s.Trim();
        foreach (char c in Path.GetInvalidFileNameChars())
            s = s.Replace(c, '_');
        return s;
    }

    private string GetPairFolder()
    {
        string doctor = (SessionData.CurrentUser != null) ? SessionData.CurrentUser.username : "UnknownDoctor";

        string patient = (patientNameInput != null) ? patientNameInput.text : "";
        if (string.IsNullOrWhiteSpace(patient))
            patient = "WaitingForPatientName";

        doctor = SanitizeFilePart(doctor);
        patient = SanitizeFilePart(patient);

        return $"{doctor}-{patient}";
    }

    private string GetNextVersionFolder(string pairPath)
    {
        int v = 1;
        while (Directory.Exists(Path.Combine(pairPath, $"v{v:D3}")))
            v++;

        string versionPath = Path.Combine(pairPath, $"v{v:D3}");
        Directory.CreateDirectory(versionPath);
        return versionPath;
    }
}
