// UnityAudioRecorder.cs
// Records Unity's mixed output (what you hear) to audio.wav.
// Attach to the GameObject that has AudioListener (your Main Camera).

using System.IO;
using UnityEngine;

[RequireComponent(typeof(AudioListener))]
public class UnityAudioRecorder : MonoBehaviour
{
    private FileStream _stream;
    private string _path;
    private int _sampleRate;
    private int _channels;
    private int _dataLength;

    public string CurrentWavPath => _path;
    public bool IsRecording { get; private set; }

    public void StartAudio(string folderPath)
    {
        if (IsRecording) return;

        _sampleRate = AudioSettings.outputSampleRate;
        _channels = 2; // stereo mix (what you hear)
        _dataLength = 0;

        Directory.CreateDirectory(folderPath);
        _path = Path.Combine(folderPath, "audio.wav");

        _stream = new FileStream(_path, FileMode.Create);
        WriteWavHeader(_stream, _sampleRate, _channels, 0);

        IsRecording = true;
        Debug.Log("Audio recording started: " + _path);
    }

    public void StopAudio()
    {
        if (!IsRecording) return;
        IsRecording = false;

        // Fix header sizes
        _stream.Seek(0, SeekOrigin.Begin);
        WriteWavHeader(_stream, _sampleRate, _channels, _dataLength);

        _stream.Flush();
        _stream.Close();
        _stream = null;

        Debug.Log("Audio recording stopped.");
    }

    // Unity calls this on the audio thread with the final mixed output.
    private void OnAudioFilterRead(float[] data, int channels)
    {
        if (!IsRecording || _stream == null) return;

        // Convert float [-1..1] to 16-bit PCM
        byte[] bytes = new byte[data.Length * 2];
        int o = 0;

        for (int i = 0; i < data.Length; i++)
        {
            short s = (short)Mathf.Clamp(data[i] * 32767f, short.MinValue, short.MaxValue);
            bytes[o++] = (byte)(s & 0xff);
            bytes[o++] = (byte)((s >> 8) & 0xff);
        }

        _stream.Write(bytes, 0, bytes.Length);
        _dataLength += bytes.Length;
    }

    private static void WriteWavHeader(Stream stream, int sampleRate, int channels, int dataLength)
    {
        using var bw = new BinaryWriter(stream, System.Text.Encoding.UTF8, true);

        int byteRate = sampleRate * channels * 2;
        int blockAlign = channels * 2;
        int chunkSize = 36 + dataLength;

        bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        bw.Write(chunkSize);
        bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));

        bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        bw.Write(16);
        bw.Write((short)1); // PCM
        bw.Write((short)channels);
        bw.Write(sampleRate);
        bw.Write(byteRate);
        bw.Write((short)blockAlign);
        bw.Write((short)16); // bits per sample

        bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        bw.Write(dataLength);
    }
}
