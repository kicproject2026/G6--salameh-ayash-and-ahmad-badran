// RecordingManager.cs
// Fixes "sped up / too fast" recordings by:
// 1) Using -framerate (correct for image sequences)
// 2) Waiting for all async frame writes to finish before running FFmpeg
// 3) Using -shortest so video ends with audio
// 4) Keeping your bundled ffmpeg.exe in StreamingAssets
//
// REQUIREMENTS:
// - Put ffmpeg.exe here (and commit it): Assets/StreamingAssets/FFmpeg/ffmpeg.exe
// - Attach UnityAudioRecorder to the AudioListener object (Main Camera)
// - Assign audioRecorder in the Inspector (RecordingSystem -> RecordingManager)

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

    [Header("Audio (optional, recommended)")]
    public UnityAudioRecorder audioRecorder;

    // Internal
    private string ffmpegPath;
    private bool isRecording;
    private int frameIndex;
    private string sessionFolder;
    private Texture2D readTex;
    private Coroutine routine;

    // Track pending async file writes so FFmpeg doesn't start early
    private int pendingWrites = 0;

    void Awake()
    {
        // Bundled ffmpeg path (best for GitHub + other PCs)
        string bundled = Path.Combine(Application.streamingAssetsPath, "FFmpeg", "ffmpeg.exe");
        ffmpegPath = File.Exists(bundled) ? bundled : ffmpegExePathFallback;

        Debug.Log("FFmpeg path used: " + ffmpegPath);
    }

    public void StartRecording()
    {
        if (isRecording) return;

        if (recordCamera == null || recordRT == null)
        {
            Debug.LogError("RecordingManager: Assign recordCamera and recordRT in Inspector.");
            return;
        }

        // Create session folder
        string root = Path.Combine(Application.persistentDataPath, outputFolderName);
        Directory.CreateDirectory(root);

        sessionFolder = Path.Combine(root, System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
        Directory.CreateDirectory(sessionFolder);

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

        // IMPORTANT: -framerate is the correct input rate for image sequences
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
}
