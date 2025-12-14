using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class OpenKeyboardOnSelect : MonoBehaviour, ISelectHandler
{
    public VirtualKeyboardController keyboard;
    private TMP_InputField field;

    void Awake()
    {
        field = GetComponent<TMP_InputField>();
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (keyboard != null)
            keyboard.OpenFor(field);
    }
}
