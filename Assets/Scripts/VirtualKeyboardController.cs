using TMPro;
using UnityEngine;

public class VirtualKeyboardController : MonoBehaviour
{
    [Header("Drag your keyboard root (Canvas/Virtual Keyboard)")]
    public GameObject keyboardRoot;

    [Header("Auth Manager (optional for Enter)")]
    public AuthManager authManager;

    [Header("Optional direct refs for Enter behavior")]
    public TMP_InputField usernameField;
    public TMP_InputField passwordField;

    [HideInInspector] public TMP_InputField activeField;

    void Start()
    {
        if (keyboardRoot != null)
            keyboardRoot.SetActive(false);
    }

    public void OpenFor(TMP_InputField field)
    {
        activeField = field;
        if (keyboardRoot != null) keyboardRoot.SetActive(true);
        activeField.ActivateInputField();
        activeField.caretPosition = activeField.text.Length;
    }

    public void Type(string value)
    {
        if (activeField == null) return;
        activeField.text += value;
        activeField.caretPosition = activeField.text.Length;
        activeField.ActivateInputField();
    }

    public void Backspace()
    {
        if (activeField == null) return;
        if (activeField.text.Length == 0) return;

        activeField.text = activeField.text.Substring(0, activeField.text.Length - 1);
        activeField.caretPosition = activeField.text.Length;
        activeField.ActivateInputField();
    }

    public void Close()
    {
        if (keyboardRoot != null) keyboardRoot.SetActive(false);
    }

    // Green Enter key
    public void Enter()
    {
        // If we're on username -> jump to password
        if (activeField != null && usernameField != null && passwordField != null && activeField == usernameField)
        {
            OpenFor(passwordField);
            return;
        }

        // If we're on password -> login
        if (authManager != null)
            authManager.OnLogin();

        Close();
    }
}
