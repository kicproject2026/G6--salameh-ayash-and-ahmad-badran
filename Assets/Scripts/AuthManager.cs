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

    // --- Campos clínicos del Paciente (opcionales para Doctor) ---
    public string age;
    public string patientId;     // Número de identificación del paciente
    public string height;        // Ej: "1.72m"
    public string weight;        // Ej: "68kg"
    public string description;   // Notas clínicas o descripción libre
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

    [Header("Sign Up — Campos Paciente (opcionales)")]
    [Tooltip("Solo visible/relevante cuando el rol es Paciente.")]
    public TMP_InputField signupAgeInput;
    public TMP_InputField signupPatientIdInput;
    public TMP_InputField signupHeightInput;
    public TMP_InputField signupWeightInput;
    public TMP_InputField signupDescriptionInput;
    [Tooltip("Panel que contiene los campos clínicos. Se puede mostrar/ocultar según el rol seleccionado.")]
    public GameObject patientFieldsPanel;

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

        signupRoleDropdown.onValueChanged.AddListener(index => {
            bool isPatient = signupRoleDropdown.options[index].text == "Patient";
            if (patientFieldsPanel != null)
                patientFieldsPanel.SetActive(isPatient);
        });
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
            username    = username,
            password    = password,
            role        = role,
            // Campos clínicos — solo se guardan si el usuario los completó
            age         = signupAgeInput         != null ? signupAgeInput.text.Trim()         : "",
            patientId   = signupPatientIdInput    != null ? signupPatientIdInput.text.Trim()    : "",
            height      = signupHeightInput       != null ? signupHeightInput.text.Trim()       : "",
            weight      = signupWeightInput       != null ? signupWeightInput.text.Trim()       : "",
            description = signupDescriptionInput  != null ? signupDescriptionInput.text.Trim()  : ""
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

                // ✅ Save names for recording folder
if (LoginContext.Instance != null)
{
    // Since this login screen has only ONE user, we save the local user in A.
    // B will be filled later when the other person joins (doctor/patient/doctor).
    LoginContext.Instance.SetUsers(u.username, "WaitingForOther");
}
else
{
    Debug.LogWarning("LoginContext.Instance is null (make sure LoginContext object exists in LoginScene).");
}


                // load your meeting room scene
                SceneManager.LoadScene("Meeting_Room");
                return;
            }
        }

        errorText.text = "Wrong username, password or role.";
    }
}
