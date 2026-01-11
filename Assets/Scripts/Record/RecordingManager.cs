// RecordingManager.cs
// - Records corner camera to JPG frames, merges with audio.wav using FFmpeg into recording.mp4
// - Prevents "sped up" issues by using -framerate and waiting pending async writes
// - Names folders like: DoctorName-PatientName/v001, v002, v003...
// - Doctor name from SessionData.CurrentUser.username (login)
// - Patient name typed by doctor in TMP_InputField
// - Error TMP_Text if Start pressed with empty patient name (auto hides after 3 seconds)
// - TASK 2: Writes audit logs into SAME version folder: audit.jsonl
//
// CHANGES YOU ASKED:
// - StartRecordingBlocked: meta now contains {"filename": ""} (no reason)
// - AuditBegin meta key is now "filename" (handled in AuditLogger.Begin)
// - who is now consistent (Dr: / Patient:) for Start/Stop/Encode logs

using TMPro;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
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

    [Header("Naming (doctor types filename/patient name)")]
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

        // Auto-hide error while typing
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

        // Require filename/patient name (typed by doctor)
        if (patientNameInput != null && string.IsNullOrWhiteSpace(patientNameInput.text))
        {
            // ✅ CHANGED: no reason, only empty filename
            AuditLogger.Instance?.Log(
                "StartRecordingBlocked",
                GetWho(),
                new Dictionary<string, object> { { "filename", "" } }
            );

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

        // ---- TASK 2: Start audit log in SAME version folder ----
        string who = GetWho();
        string filename = GetPatientName(); // you type it

        if (AuditLogger.Instance != null)
        {
            // ✅ AuditBegin will store meta {"filename": filename} (implemented in AuditLogger.cs)
            AuditLogger.Instance.Begin(sessionFolder, who, filename);

            AuditLogger.Instance.Log("StartRecording", who, new Dictionary<string, object> {
                {"versionFolder", Path.GetFileName(sessionFolder)},
                {"pairFolder", Path.GetFileName(pairPath)},
                {"fps", fps},
                {"resolution", $"{recordRT.width}x{recordRT.height}"},
                {"asyncDiskWrite", asyncDiskWrite}
            });
        }

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

        // ---- TASK 2: log stop ----
        AuditLogger.Instance?.Log("StopRecording", GetWho(), new Dictionary<string, object> {
            {"frames", frameIndex}
        });

        Debug.Log("Recording stopped. Waiting for pending frame writes...");
        WaitForPendingWrites();

        Debug.Log("Encoding mp4...");
        EncodeToMp4();

        // ---- TASK 2: end audit (after encoding attempt) ----
        AuditLogger.Instance?.End(GetWho());
        var analytics = FindObjectOfType<PeopleAnalyticsGenerator>();
if (analytics != null)
    analytics.Generate(sessionFolder);

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
                byte[] dataCopy = jpg;
                Task.Run(() =>
                {
                    try { File.WriteAllBytes(framePath, dataCopy); }
                    catch { }
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
        int safety = 0;
        while (pendingWrites > 0 && safety < 300)
        {
            Thread.Sleep(10);
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

                // ---- TASK 2: log encode success ----
                AuditLogger.Instance?.Log("EncodeMp4Success", GetWho(), new Dictionary<string, object> {
                    {"mp4", "recording.mp4"},
                    {"hasAudio", File.Exists(wavPath)}
                });

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
                AuditLogger.Instance?.Log("EncodeMp4Failed", GetWho(), new Dictionary<string, object> {
                    {"filename", ""} // keep it simple, no reason text if you prefer
                });
            }

            if (!string.IsNullOrEmpty(stdout)) Debug.Log(stdout);
            if (!string.IsNullOrEmpty(err)) Debug.Log(err);
        }
        catch (System.Exception e)
        {
            Debug.LogError("FFmpeg failed: " + e.Message);
            Debug.LogError("Expected ffmpeg at: Assets/StreamingAssets/FFmpeg/ffmpeg.exe");

            AuditLogger.Instance?.Log("EncodeMp4Exception", GetWho(), new Dictionary<string, object> {
                {"filename", ""} // keep it simple
            });
        }
    }

    // ---------- UI helpers ----------

    private void ShowPatientNameError(string msg)
    {
        if (patientNameErrorText == null) return;

        patientNameErrorText.text = msg;
        patientNameErrorText.gameObject.SetActive(true);
        Debug.LogError(msg);

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

    private string GetPatientName() =>
        (patientNameInput != null) ? patientNameInput.text.Trim() : "";

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
        // Doctor name (without role prefix) for folder name:
        string doctorRaw = (SessionData.CurrentUser != null) ? SessionData.CurrentUser.username : "UnknownDoctor";
        string doctor = SanitizeFilePart(doctorRaw);

        string filename = GetPatientName();
        if (string.IsNullOrWhiteSpace(filename))
            filename = "WaitingForPatientName";
        filename = SanitizeFilePart(filename);

        return $"{doctorRaw}-{filename}".Replace(doctorRaw, doctor); // keep sanitized
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

    private string GetWho()
    {
        if (SessionData.CurrentUser == null) return "UnknownUser";
        return (SessionData.CurrentUser.role == "Doctor")
            ? $"Dr:{SessionData.CurrentUser.username}"
            : $"Patient:{SessionData.CurrentUser.username}";
    }
}
