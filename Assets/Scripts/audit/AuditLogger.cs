using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public class AuditLogger : MonoBehaviour
{
    public static AuditLogger Instance { get; private set; }

    private string _filePath;
    private float _t0;
    private bool _isActive;

    private readonly List<string> _buffer = new List<string>(64);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ✅ Begin now stores "filename" not "patient"
    public void Begin(string folderPath, string who, string filename)
    {
        try
        {
            Directory.CreateDirectory(folderPath);
            _filePath = Path.Combine(folderPath, "audit.jsonl");
            _t0 = Time.realtimeSinceStartup;
            _isActive = true;

            Log("AuditBegin", who, new Dictionary<string, object> {
                { "filename", filename ?? "" },
                { "unityTime", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") }
            });

            Flush();
        }
        catch (Exception e)
        {
            Debug.LogError("AuditLogger Begin failed: " + e.Message);
            _isActive = false;
        }
    }

    public void End(string who)
    {
        if (!_isActive) return;

        Log("AuditEnd", who, null);
        Flush();
        _isActive = false;
        _filePath = null;
    }

    public void Log(string type, string who, Dictionary<string, object> meta)
    {
        string line = BuildJsonLine(type, who, meta);

        if (_isActive && !string.IsNullOrEmpty(_filePath))
        {
            _buffer.Add(line);
            Flush();
        }
        else
        {
            // If not active yet, buffer anyway
            _buffer.Add(line);
        }
    }

    private void Flush()
    {
        if (string.IsNullOrEmpty(_filePath)) return;
        if (_buffer.Count == 0) return;

        try
        {
            File.AppendAllLines(_filePath, _buffer);
            _buffer.Clear();
        }
        catch (Exception e)
        {
            Debug.LogError("AuditLogger Flush failed: " + e.Message);
        }
    }

    private string BuildJsonLine(string type, string who, Dictionary<string, object> meta)
    {
        float t = Time.realtimeSinceStartup - _t0;
        string time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

        var sb = new StringBuilder(256);
        sb.Append("{");
        sb.Append("\"time\":\"").Append(time).Append("\",");
        sb.Append("\"t\":").Append(t.ToString("0.000")).Append(",");
        sb.Append("\"type\":\"").Append(Escape(type)).Append("\",");
        sb.Append("\"who\":\"").Append(Escape(who)).Append("\"");

        if (meta != null && meta.Count > 0)
        {
            sb.Append(",\"meta\":{");
            bool first = true;
            foreach (var kv in meta)
            {
                if (!first) sb.Append(",");
                first = false;
                sb.Append("\"").Append(Escape(kv.Key)).Append("\":");
                sb.Append(ValueToJson(kv.Value));
            }
            sb.Append("}");
        }

        sb.Append("}");
        return sb.ToString();
    }

    private static string Escape(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static string ValueToJson(object v)
    {
        if (v == null) return "null";

        if (v is bool b) return b ? "true" : "false";
        if (v is int || v is long || v is float || v is double) return Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture);

        // strings
        return "\"" + Escape(v.ToString()) + "\"";
    }
}
