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
        activeField.selectionAnchorPosition = activeField.text.Length;
        activeField.selectionFocusPosition = activeField.text.Length;
        activeField.caretPosition = activeField.text.Length;
    }

    public void Type(string value)
    {
        if (activeField == null) return;
        activeField.text += value;
        activeField.ActivateInputField();
        activeField.selectionAnchorPosition = activeField.text.Length;
        activeField.selectionFocusPosition = activeField.text.Length;
        activeField.caretPosition = activeField.text.Length;
    }

    public void Backspace()
    {
        if (activeField == null) return;
        if (activeField.text.Length == 0) return;

        activeField.text = activeField.text.Substring(0, activeField.text.Length - 1);
        activeField.ActivateInputField();
        activeField.selectionAnchorPosition = activeField.text.Length;
        activeField.selectionFocusPosition = activeField.text.Length;
        activeField.caretPosition = activeField.text.Length;
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

    void Update()
    {
        if (activeField != null && activeField.isFocused)
        {
            if (activeField.selectionAnchorPosition != activeField.caretPosition || activeField.selectionFocusPosition != activeField.caretPosition)
            {
                int selectionLength = Mathf.Abs(activeField.selectionAnchorPosition - activeField.selectionFocusPosition);
                if (selectionLength == activeField.text.Length)
                {
                    activeField.selectionAnchorPosition = activeField.text.Length;
                    activeField.selectionFocusPosition = activeField.text.Length;
                    activeField.caretPosition = activeField.text.Length;
                }
            }
        }
    }
}
