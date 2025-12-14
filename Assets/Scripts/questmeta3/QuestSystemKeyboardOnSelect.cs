using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class QuestSystemKeyboardOnSelect : MonoBehaviour, ISelectHandler
{
    [Header("Editor Testing")]
    [Tooltip("Lets you test the flow in Unity Editor without a Quest. Remove/disable later.")]
    public bool simulateQuestInEditor = true;

    private TMP_InputField field;

    void Awake()
    {
        field = GetComponent<TMP_InputField>();
        if (field == null)
            Debug.LogError($"QuestSystemKeyboardOnSelect: No TMP_InputField found on {gameObject.name}");
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (field == null) return;

        field.ActivateInputField();

        bool isQuestAndroid = Application.platform == RuntimePlatform.Android;
        bool simulate = Application.isEditor && simulateQuestInEditor;

        // This log lets you confirm the script is firing when you click the field.
        Debug.Log($"[QuestKeyboard] Selected: {gameObject.name} | Platform={Application.platform} | isQuestAndroid={isQuestAndroid} | simulate={simulate}");

        if (isQuestAndroid || simulate)
        {
            bool isPassword = field.contentType == TMP_InputField.ContentType.Password;

            // On Quest (Android APK) this should pop the system keyboard.
            // In Editor, you might not see a VR keyboard (because it's headset UI),
            // but the log confirms the call path is correct.
            TouchScreenKeyboard.Open(
                field.text,
                TouchScreenKeyboardType.Default,
                false,
                false,
                isPassword,
                false
            );

            Debug.Log("[QuestKeyboard] TouchScreenKeyboard.Open() called.");
        }
    }
}
