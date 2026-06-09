using System;
using System.Collections;
using System.IO;
using System.Text;
using Microsoft.MixedReality.Toolkit.Experimental.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.UI;

[Serializable]
public class AIDoctorResponseInputMessage
{
    public string role;
    public string content;
}

[Serializable]
public class AIDoctorResponseTextFormat
{
    public string type = "text";
}

[Serializable]
public class AIDoctorResponseTextOptions
{
    public AIDoctorResponseTextFormat format = new AIDoctorResponseTextFormat();
}

[Serializable]
public class AIDoctorResponseReasoningOptions
{
    public string effort = "medium";
}

[Serializable]
public class AIDoctorResponseRequest
{
    public string model;
    public AIDoctorResponseInputMessage[] input;
    public AIDoctorResponseTextOptions text = new AIDoctorResponseTextOptions();
    public AIDoctorResponseReasoningOptions reasoning = new AIDoctorResponseReasoningOptions();
}

[Serializable]
public class AIDoctorResponseError
{
    public string message;
    public string type;
    public string code;
}

[Serializable]
public class AIDoctorResponseOutputContent
{
    public string type;
    public string text;
}

[Serializable]
public class AIDoctorResponseOutputItem
{
    public string type;
    public string role;
    public string status;
    public AIDoctorResponseOutputContent[] content;
}

[Serializable]
public class AIDoctorResponseResponse
{
    public string status;
    public AIDoctorResponseOutputItem[] output;
    public AIDoctorResponseError error;
}

[Serializable]
public class AIDoctorLocalConfig
{
    public string apiKey;
    public string model;
}

public class AIDoctorChatPanel : MonoBehaviour
{
    [Header("UI")]
    public GameObject questionPanel;
    public GameObject responsePanel;
    public TMP_InputField promptInput;
    public TMP_Text questionOutput;
    public TMP_Text responseOutput;
    public TMP_Text statusLabel;
    public Button askButton;
    public Button doneButton;
    public GameObject loadingRoot;

    [Header("Keyboard")]
    public NonNativeKeyboard nonNativeKeyboard;
    public bool autoFocusPromptOnShow = true;
    public bool autoFocusOnSceneStart = false;

    [Header("API")]
    public string apiKey;
    public string endpoint = "https://api.openai.com/v1/responses";
    public string model = "gpt-5";
    public string localConfigFileName = "AIDoctorConfig.json";
    [TextArea(3, 8)]
    public string systemPrompt = "You are a warm, empathetic medical doctor for a VR medical experience. Speak like a calm human doctor, not like a robotic assistant. You may respond kindly to greetings, thanks, brief small talk, or simple conversational openers, and then gently invite the user to share their medical concern or question. Your main purpose is still medical guidance related to health, symptoms, anatomy, treatment, prevention, or clinical guidance. If the user asks for something clearly unrelated to medicine, respond politely and redirect them back to health-related topics instead of sounding cold or mechanical. Keep answers brief, clear, and reassuring. Prefer short medical guidance: usually 2 to 4 sentences, or a very short list only when truly helpful. Do not give long explanations, exhaustive step-by-step plans, or overly detailed background unless the user explicitly asks for more detail. Focus on the most useful practical advice first. Always include a short disclaimer when giving medical guidance that this does not replace a licensed physician.";

    [Header("Behavior")]
    public bool clearInputAfterSuccess = false;
    public string loadingText = "...";
    public float loadingStepSeconds = 0.4f;
    public float typingStepSeconds = 0.02f;

    private bool _isSending;
    private Coroutine _loadingDotsRoutine;
    private Coroutine _typingRoutine;

    private void Awake()
    {
        LoadLocalConfig();

        if (askButton != null)
            askButton.onClick.AddListener(SendCurrentPrompt);

        if (doneButton != null)
            doneButton.onClick.AddListener(ResetCycle);

        ApplyQuestionPanelState(autoFocusOnSceneStart);
    }

    private void OnDestroy()
    {
        if (askButton != null)
            askButton.onClick.RemoveListener(SendCurrentPrompt);

        if (doneButton != null)
            doneButton.onClick.RemoveListener(ResetCycle);
    }

    public void SendCurrentPrompt()
    {
        if (_isSending)
            return;

        string prompt = promptInput != null ? promptInput.text.Trim() : string.Empty;
        if (string.IsNullOrWhiteSpace(prompt))
        {
            ShowQuestionValidation("Write a question first.");
            return;
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            ShowQuestionValidation("Missing API key in Inspector or StreamingAssets config.");
            return;
        }

        StartCoroutine(SendPromptRoutine(prompt));
    }

    private IEnumerator SendPromptRoutine(string prompt)
    {
        _isSending = true;
        CloseVirtualKeyboard();

        if (questionOutput != null)
            questionOutput.text = prompt;

        SetQuestionPanelVisible(false);
        SetResponsePanelVisible(true);
        SetLoading(true);
        StartLoadingDots();
        SetDoneInteractable(false);

        if (responseOutput != null)
            responseOutput.text = string.Empty;

        SetStatus(string.Empty);

        AIDoctorResponseRequest payload = new AIDoctorResponseRequest
        {
            model = model,
            input = new[]
            {
                new AIDoctorResponseInputMessage { role = "developer", content = systemPrompt },
                new AIDoctorResponseInputMessage { role = "user", content = prompt }
            }
        };

        string json = JsonUtility.ToJson(payload);
        byte[] body = Encoding.UTF8.GetBytes(json);

        using (UnityWebRequest request = new UnityWebRequest(endpoint, UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + apiKey.Trim());

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                StopLoadingDots();
                SetStatus("Request failed: " + request.error);
                if (responseOutput != null)
                    responseOutput.text = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;

                SetDoneInteractable(true);

                FinishRequest();
                yield break;
            }

            string responseJson = request.downloadHandler.text;
            AIDoctorResponseResponse response = null;

            try
            {
                response = JsonUtility.FromJson<AIDoctorResponseResponse>(responseJson);
            }
            catch (Exception ex)
            {
                StopLoadingDots();
                SetStatus("Invalid response.");
                if (responseOutput != null)
                    responseOutput.text = responseJson + "\n\n" + ex.Message;

                SetDoneInteractable(true);

                FinishRequest();
                yield break;
            }

            if (response != null && response.error != null && !string.IsNullOrWhiteSpace(response.error.message))
            {
                StopLoadingDots();
                SetStatus("API error.");
                if (responseOutput != null)
                    responseOutput.text = response.error.message.Trim();

                SetDoneInteractable(true);

                FinishRequest();
                yield break;
            }

            string finalText = ExtractResponseText(response);
            if (string.IsNullOrWhiteSpace(finalText))
            {
                StopLoadingDots();
                SetStatus("The API returned no answer.");
                if (responseOutput != null)
                    responseOutput.text = responseJson;

                SetDoneInteractable(true);

                FinishRequest();
                yield break;
            }

            StopLoadingDots();
            SetStatus(string.Empty);
            yield return StartTypingResponse(finalText.Trim());
            SetDoneInteractable(true);

            if (clearInputAfterSuccess && promptInput != null)
                promptInput.text = string.Empty;
        }

        FinishRequest();
    }

    private string ExtractResponseText(AIDoctorResponseResponse response)
    {
        if (response == null)
            return string.Empty;

        if (response.output == null || response.output.Length == 0)
            return string.Empty;

        StringBuilder builder = new StringBuilder();

        for (int i = 0; i < response.output.Length; i++)
        {
            AIDoctorResponseOutputItem outputItem = response.output[i];
            if (outputItem == null || outputItem.type != "message" || outputItem.content == null)
                continue;

            for (int j = 0; j < outputItem.content.Length; j++)
            {
                AIDoctorResponseOutputContent contentItem = outputItem.content[j];
                if (contentItem == null || contentItem.type != "output_text" || string.IsNullOrWhiteSpace(contentItem.text))
                    continue;

                if (builder.Length > 0)
                    builder.Append('\n');

                builder.Append(contentItem.text.Trim());
            }
        }

        return builder.ToString().Trim();
    }

    private void LoadLocalConfig()
    {
        if (string.IsNullOrWhiteSpace(localConfigFileName))
            return;

        string configPath = Path.Combine(Application.streamingAssetsPath, localConfigFileName);
        if (!File.Exists(configPath))
            return;

        try
        {
            string json = File.ReadAllText(configPath);
            AIDoctorLocalConfig config = JsonUtility.FromJson<AIDoctorLocalConfig>(json);
            if (string.IsNullOrWhiteSpace(apiKey) && config != null && !string.IsNullOrWhiteSpace(config.apiKey))
                apiKey = config.apiKey.Trim();

            if (config != null && !string.IsNullOrWhiteSpace(config.model))
                model = config.model.Trim();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("AIDoctorChatPanel could not load local config: " + ex.Message);
        }
    }

    private void FinishRequest()
    {
        _isSending = false;
        StopLoadingDots();
        StopTyping();
        SetLoading(false);
    }

    public void ResetCycle()
    {
        if (_isSending)
            return;

        StopLoadingDots();
        StopTyping();

        if (promptInput != null)
            promptInput.text = string.Empty;

        if (questionOutput != null)
            questionOutput.text = string.Empty;

        if (responseOutput != null)
            responseOutput.text = string.Empty;

        CloseVirtualKeyboard();
        SetStatus(string.Empty);
        ApplyQuestionPanelState(autoFocusPromptOnShow);
    }

    private void StartLoadingDots()
    {
        StopLoadingDots();
        _loadingDotsRoutine = StartCoroutine(AnimateLoadingDots());
    }

    private void StopLoadingDots()
    {
        if (_loadingDotsRoutine != null)
        {
            StopCoroutine(_loadingDotsRoutine);
            _loadingDotsRoutine = null;
        }
    }

    private IEnumerator StartTypingResponse(string finalText)
    {
        StopTyping();
        _typingRoutine = StartCoroutine(TypeResponseRoutine(finalText));
        yield return _typingRoutine;
        _typingRoutine = null;
    }

    private void StopTyping()
    {
        if (_typingRoutine != null)
        {
            StopCoroutine(_typingRoutine);
            _typingRoutine = null;
        }
    }

    private IEnumerator TypeResponseRoutine(string finalText)
    {
        if (responseOutput == null)
            yield break;

        responseOutput.text = string.Empty;

        foreach (char character in finalText)
        {
            responseOutput.text += character;
            if (typingStepSeconds > 0f)
                yield return new WaitForSeconds(typingStepSeconds);
            else
                yield return null;
        }
    }

    private IEnumerator AnimateLoadingDots()
    {
        int maxDots = string.IsNullOrEmpty(loadingText) ? 3 : Mathf.Max(1, loadingText.Length);
        string[] frames = new string[maxDots];
        for (int i = 0; i < maxDots; i++)
            frames[i] = new string('.', i + 1);

        int index = 0;

        while (true)
        {
            if (responseOutput != null)
                responseOutput.text = frames[index];

            index = (index + 1) % frames.Length;
            yield return new WaitForSeconds(loadingStepSeconds);
        }
    }

    private void ApplyQuestionPanelState(bool focusPrompt)
    {
        SetQuestionPanelVisible(true);
        SetResponsePanelVisible(false);
        SetLoading(false);
        SetDoneInteractable(false);

        if (focusPrompt)
            FocusPromptInput();
    }

    private void ShowQuestionValidation(string message)
    {
        Debug.LogWarning("AIDoctorChatPanel validation: " + message, this);
        SetStatus(message);

        if (responseOutput != null && responsePanel != null && responsePanel.activeSelf)
            responseOutput.text = message;
    }

    private void FocusPromptInput()
    {
        if (promptInput == null)
            return;

        promptInput.ActivateInputField();
        promptInput.Select();

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(promptInput.gameObject);
    }

    private void CloseVirtualKeyboard()
    {
        if (nonNativeKeyboard != null)
            nonNativeKeyboard.Close();
    }

    private void SetQuestionPanelVisible(bool isVisible)
    {
        if (questionPanel != null)
            questionPanel.SetActive(isVisible);
    }

    private void SetResponsePanelVisible(bool isVisible)
    {
        if (responsePanel != null)
            responsePanel.SetActive(isVisible);
    }

    private void SetDoneInteractable(bool isInteractable)
    {
        if (doneButton != null)
            doneButton.interactable = isInteractable;
    }

    private void SetLoading(bool isLoading)
    {
        if (loadingRoot != null)
            loadingRoot.SetActive(isLoading);

        if (askButton != null)
            askButton.interactable = !isLoading;
    }

    private void SetStatus(string message)
    {
        if (statusLabel != null)
            statusLabel.text = message;
    }
}
