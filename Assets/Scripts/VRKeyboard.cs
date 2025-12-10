using TMPro;
using UnityEngine;

public class VRKeyboard : MonoBehaviour
{
    [Header("Input field we are currently typing into")]
    public TMP_InputField currentInput;

    void Awake()
    {
        // Make sure the keyboard starts hidden
        gameObject.SetActive(false);
    }

    // Called from input fields when they get focus
    public void SetCurrentInput(TMP_InputField input)
    {
        currentInput = input;

        if (currentInput != null)
        {
            // Show the keyboard when we select a field
            gameObject.SetActive(true);

            currentInput.ActivateInputField();
            currentInput.caretPosition = currentInput.text.Length;
        }
    }

    // Called by letter/number buttons
    public void AddCharacter(string c)
    {
        if (currentInput == null) return;

        currentInput.text += c;
        currentInput.caretPosition = currentInput.text.Length;
    }

    public void AddSpace()
    {
        AddCharacter(" ");
    }

    public void Backspace()
    {
        if (currentInput == null) return;
        if (currentInput.text.Length == 0) return;

        currentInput.text = currentInput.text.Substring(0, currentInput.text.Length - 1);
        currentInput.caretPosition = currentInput.text.Length;
    }

    public void ClearField()
    {
        if (currentInput == null) return;

        currentInput.text = "";
        currentInput.caretPosition = 0;
    }

    // Optional: hide keyboard, e.g. from a Close button or Login button
    public void HideKeyboard()
    {
        currentInput = null;
        gameObject.SetActive(false);
    }
}
