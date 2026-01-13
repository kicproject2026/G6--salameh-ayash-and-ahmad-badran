using System.IO;
using UnityEngine;
using UnityEngine.Video;
using TMPro;

public class VrVideoPlayback : MonoBehaviour
{
    [Header("References")]
    public VideoPlayer videoPlayer;
    public TMP_Text statusText; // optional (you can reuse OutputText if you want)

    [Header("Expected file name inside a version folder")]
    public string videoFileName = "recording.mp4";

    // This is set by SessionBrowserUI when user selects a version
    private string selectedVersionFolder;

    public void SetSelectedVersionFolder(string folderPath)
    {
        selectedVersionFolder = folderPath;
        Log($"Selected version folder:\n{selectedVersionFolder}");
    }

    public void PlaySelectedVideo()
    {
        if (videoPlayer == null)
        {
            Log("VideoPlayer reference is missing.");
            return;
        }

        if (string.IsNullOrEmpty(selectedVersionFolder))
        {
            Log("Pick a session + version first.");
            return;
        }

        string mp4Path = Path.Combine(selectedVersionFolder, videoFileName);

        if (!File.Exists(mp4Path))
        {
            Log("Video not found:\n" + mp4Path);
            return;
        }

        videoPlayer.Stop();
        videoPlayer.url = mp4Path;

        videoPlayer.Prepare();
        videoPlayer.prepareCompleted += OnPrepared;
        Log("Preparing video...");
    }

    private void OnPrepared(VideoPlayer vp)
    {
        vp.prepareCompleted -= OnPrepared;
        vp.Play();
        Log("Playing video.");
    }

    public void StopVideo()
    {
        if (videoPlayer == null) return;
        videoPlayer.Stop();
        Log("Video stopped.");
    }

    private void Log(string msg)
    {
        Debug.Log("[VrVideoPlayback] " + msg);
        if (statusText != null) statusText.text = msg;
    }
}
