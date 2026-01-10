using System.Collections;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class RecordingManager : MonoBehaviour
{
    public Camera recordCamera;
    public RenderTexture recordRT;
    public int fps = 30;

    public string outputFolderName = "Recordings";
    public string ffmpegExePath = "ffmpeg"; // fallback if not bundled

    // ✅ Drag the UnityAudioRecorder (from your main camera) here
    public UnityAudioRecorder audioRecorder;

    private string ffmpegPath;

    bool isRecording;
    int frameIndex;
    string sessionFolder;
    Texture2D readTex;
    Coroutine routine;

    void Awake()
    {
        string bundled = Path.Combine(Application.streamingAssetsPath, "FFmpeg", "ffmpeg.exe");
        ffmpegPath = File.Exists(bundled) ? bundled : ffmpegExePath;
        Debug.Log("FFmpeg path used: " + ffmpegPath);
    }

    public void StartRecording()
    {
        if (isRecording) return;

        if (recordCamera == null || recordRT == null)
        {
            Debug.LogError("Assign recordCamera and recordRT in Inspector.");
            return;
        }

        string root = Path.Combine(Application.persistentDataPath, outputFolderName);
        Directory.CreateDirectory(root);

        sessionFolder = Path.Combine(root, System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
        Directory.CreateDirectory(sessionFolder);

        readTex = new Texture2D(recordRT.width, recordRT.height, TextureFormat.RGB24, false);

        // ✅ start audio first
        if (audioRecorder != null)
            audioRecorder.StartAudio(sessionFolder);
        else
            Debug.LogWarning("No audioRecorder assigned. Video will have no voice.");

        isRecording = true;
        frameIndex = 0;

        routine = StartCoroutine(CaptureLoop());
        Debug.Log("Recording started: " + sessionFolder);
    }

    public void StopRecording()
    {
        if (!isRecording) return;
        isRecording = false;

        if (routine != null) StopCoroutine(routine);

        // ✅ stop audio before encoding
        if (audioRecorder != null)
            audioRecorder.StopAudio();

        Debug.Log("Recording stopped. Encoding mp4...");
        EncodeToMp4();
    }

    IEnumerator CaptureLoop()
    {
        float interval = 1f / Mathf.Max(1, fps);

        while (isRecording)
        {
            yield return new WaitForEndOfFrame();

            var prev = RenderTexture.active;
            RenderTexture.active = recordRT;

            recordCamera.Render();

            readTex.ReadPixels(new Rect(0, 0, recordRT.width, recordRT.height), 0, 0);
            readTex.Apply(false);

            byte[] png = readTex.EncodeToPNG();
            File.WriteAllBytes(Path.Combine(sessionFolder, $"frame_{frameIndex:D06}.png"), png);

            frameIndex++;
            RenderTexture.active = prev;

            yield return new WaitForSeconds(interval);
        }
    }

    void EncodeToMp4()
    {
        string outMp4 = Path.Combine(sessionFolder, "recording.mp4");
        string inputPattern = Path.Combine(sessionFolder, "frame_%06d.png");
        string wavPath = Path.Combine(sessionFolder, "audio.wav");

        // ✅ If audio exists, merge it. If not, do video-only.
        string args;
        if (File.Exists(wavPath))
        {
            args =
                $"-y -r {fps} -i \"{inputPattern}\" " +
                $"-i \"{wavPath}\" " +
                "-c:v libx264 -pix_fmt yuv420p " +
                "-c:a aac -b:a 192k " +
                "-shortest " + // ✅ avoids extra silence mismatch
                $"\"{outMp4}\"";
        }
        else
        {
            args =
                $"-y -r {fps} -i \"{inputPattern}\" " +
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
            p.WaitForExit();

            if (File.Exists(outMp4))
                Debug.Log("Saved video: " + outMp4);
            else
                Debug.LogError("FFmpeg finished but MP4 not found.");

            if (!string.IsNullOrEmpty(err)) Debug.Log(err);
        }
        catch (System.Exception e)
        {
            Debug.LogError("FFmpeg failed: " + e.Message);
        }
    }
}
