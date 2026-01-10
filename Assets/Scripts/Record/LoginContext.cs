using System.IO;
using UnityEngine;

public class LoginContext : MonoBehaviour
{
    public static LoginContext Instance { get; private set; }

    // The two participants in this PC's session (order doesn't matter)
    public string userA = "UnknownA";
    public string userB = "UnknownB";

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

    public void SetUsers(string a, string b)
    {
        userA = Sanitize(a);
        userB = Sanitize(b);
    }

    public string GetPairFolderName()
    {
        // Sort so "Ali__Omar" and "Omar__Ali" become the SAME folder
        string x = userA;
        string y = userB;

        if (string.Compare(x, y) > 0)
        {
            var tmp = x; x = y; y = tmp;
        }

        return $"{x}__{y}";
    }

    private static string Sanitize(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "Unknown";
        s = s.Trim();
        foreach (char c in Path.GetInvalidFileNameChars())
            s = s.Replace(c, '_');
        return s;
    }
}
