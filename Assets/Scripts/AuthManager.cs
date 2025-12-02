using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

[Serializable]
public class UserData
{
    public string username;
    public string password;
    public string role; // "Doctor" or "Patient"
}

[Serializable]
public class UserDatabaseData
{
    public List<UserData> users = new List<UserData>();
}

public class AuthManager : MonoBehaviour
{
    [Header("Login UI")]
    public TMP_InputField loginUsernameInput;
    public TMP_InputField loginPasswordInput;
    public TMP_Dropdown   loginRoleDropdown;

    [Header("Sign Up UI")]
    public TMP_InputField signupUsernameInput;
    public TMP_InputField signupPasswordInput;
    public TMP_Dropdown   signupRoleDropdown;

    [Header("Panels")]
    public GameObject loginPanel;
    public GameObject signupPanel;

    [Header("Other")]
    public TMP_Text errorText;

    private UserDatabaseData db;
    private const string DB_KEY = "USER_DB";

    void Awake()
    {
        LoadDB();
        ShowLogin();
    }

    // ------- database in PlayerPrefs --------
    void LoadDB()
    {
        string json = PlayerPrefs.GetString(DB_KEY, "");
        if (string.IsNullOrEmpty(json))
            db = new UserDatabaseData();
        else
            db = JsonUtility.FromJson<UserDatabaseData>(json);
    }

    void SaveDB()
    {
        string json = JsonUtility.ToJson(db);
        PlayerPrefs.SetString(DB_KEY, json);
        PlayerPrefs.Save();
    }

    // ------- switch panels --------
    public void ShowLogin()
    {
        loginPanel.SetActive(true);
        signupPanel.SetActive(false);
        errorText.text = "";
    }

    public void ShowSignup()
    {
        loginPanel.SetActive(false);
        signupPanel.SetActive(true);
        errorText.text = "";
    }

    // ------- SIGN UP --------
    public void OnSignup()
    {
        string username = signupUsernameInput.text.Trim();
        string password = signupPasswordInput.text;
        string role     = signupRoleDropdown.options[signupRoleDropdown.value].text;

        if (username == "" || password == "")
        {
            errorText.text = "Please enter username and password.";
            return;
        }

        foreach (var u in db.users)
        {
            if (u.username.Equals(username, StringComparison.OrdinalIgnoreCase))
            {
                errorText.text = "Username already exists.";
                return;
            }
        }

        UserData newUser = new UserData
        {
            username = username,
            password = password,
            role = role
        };

        db.users.Add(newUser);
        SaveDB();

        errorText.text = "Sign up successful! You can log in now.";
        ShowLogin();
    }

    // ------- LOGIN --------
    public void OnLogin()
    {
        string username = loginUsernameInput.text.Trim();
        string password = loginPasswordInput.text;
        string role     = loginRoleDropdown.options[loginRoleDropdown.value].text;

        foreach (var u in db.users)
        {
            if (u.username.Equals(username, StringComparison.OrdinalIgnoreCase) &&
                u.password == password &&
                u.role == role)
            {
                // remember this user for the next scene
                SessionData.CurrentUser = u;

                // load your meeting room scene
                SceneManager.LoadScene("Meeting_Room");
                return;
            }
        }

        errorText.text = "Wrong username, password or role.";
    }
}
