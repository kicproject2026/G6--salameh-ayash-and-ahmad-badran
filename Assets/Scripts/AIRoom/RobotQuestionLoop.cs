using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class RobotQuestionEntry
{
    public Button button;

    [TextArea(2, 5)]
    public string answer;
}

[Serializable]
public class RobotQuestionFormEntry
{
    public TMP_Text questionLabel;
    public TMP_InputField answerInput;
}

public class RobotQuestionLoop : MonoBehaviour
{
    private static readonly string[] PredefinedQuestions =
    {
        "What brings you here today?",
        "Are you experiencing any pain or discomfort?",
        "How would you rate your pain from 1 to 10?",
        "Where is the pain or problem located?",
        "What would you like to improve through rehabilitation?"
    };

    private const string SubmitValidationMessage = "Please answer every question before continuing.";
    private const string SubmitConfirmationMessage = "Thank you. I have recorded your answers for the doctor.";

    [Header("Panels")]
    public GameObject questionPanelRoot;
    public GameObject answerPanelRoot;

    [Header("Answer UI")]
    public TMP_Text answerText;
    public float answerVisibleSeconds = 4f;

    [HideInInspector]
    public List<RobotQuestionEntry> questions = new List<RobotQuestionEntry>();

    [Header("Form")]
    public List<RobotQuestionFormEntry> formQuestions = new List<RobotQuestionFormEntry>(PredefinedQuestions.Length);
    public Button submitButton;
    public TMP_Text formStatusText;

    private readonly List<Action> _unbindActions = new List<Action>();
    private Coroutine _answerRoutine;
    private readonly List<string> _submittedAnswers = new List<string>();

    private void Awake()
    {
        BindButtons();
        BindForm();
        ApplyFormQuestions();
        RefreshSubmitState();
        ShowQuestionPanel();
    }

    private void OnDestroy()
    {
        UnbindButtons();
        UnbindForm();
    }

    private void BindButtons()
    {
        UnbindButtons();

        for (int i = 0; i < questions.Count; i++)
        {
            RobotQuestionEntry entry = questions[i];
            if (entry == null || entry.button == null)
                continue;

            int index = i;
            UnityEngine.Events.UnityAction action = () => SelectQuestion(index);
            entry.button.onClick.AddListener(action);
            _unbindActions.Add(() => entry.button.onClick.RemoveListener(action));
        }
    }

    private void UnbindButtons()
    {
        for (int i = 0; i < _unbindActions.Count; i++)
            _unbindActions[i]?.Invoke();

        _unbindActions.Clear();
    }

    private void BindForm()
    {
        if (submitButton != null)
            submitButton.onClick.AddListener(SubmitForm);

        for (int i = 0; i < formQuestions.Count; i++)
        {
            RobotQuestionFormEntry entry = formQuestions[i];
            if (entry == null || entry.answerInput == null)
                continue;

            entry.answerInput.onValueChanged.AddListener(OnFormValueChanged);
        }
    }

    private void UnbindForm()
    {
        if (submitButton != null)
            submitButton.onClick.RemoveListener(SubmitForm);

        for (int i = 0; i < formQuestions.Count; i++)
        {
            RobotQuestionFormEntry entry = formQuestions[i];
            if (entry == null || entry.answerInput == null)
                continue;

            entry.answerInput.onValueChanged.RemoveListener(OnFormValueChanged);
        }
    }

    private void ApplyFormQuestions()
    {
        for (int i = 0; i < formQuestions.Count; i++)
        {
            RobotQuestionFormEntry entry = formQuestions[i];
            if (entry == null || entry.questionLabel == null)
                continue;

            entry.questionLabel.text = i < PredefinedQuestions.Length
                ? PredefinedQuestions[i]
                : string.Empty;
        }
    }

    private void OnFormValueChanged(string _)
    {
        RefreshSubmitState();
        SetFormStatus(string.Empty);
    }

    private void RefreshSubmitState()
    {
        if (submitButton == null)
            return;

        submitButton.interactable = AreAllFormAnswersValid();
    }

    public void SelectQuestion(int index)
    {
        if (index < 0 || index >= questions.Count)
            return;

        RobotQuestionEntry entry = questions[index];
        if (entry == null)
            return;

        if (_answerRoutine != null)
            StopCoroutine(_answerRoutine);

        ShowAnswer(entry.answer);
        _answerRoutine = StartCoroutine(ReturnToQuestionPanel());
    }

    public void SubmitForm()
    {
        if (!AreAllFormAnswersValid())
        {
            SetFormStatus(SubmitValidationMessage);
            RefreshSubmitState();
            return;
        }

        CacheSubmittedAnswers();
        SetFormStatus(string.Empty);

        if (_answerRoutine != null)
            StopCoroutine(_answerRoutine);

        ShowAnswer(BuildSubmissionMessage());
        _answerRoutine = StartCoroutine(ReturnToQuestionPanel());
    }

    public void ShowQuestionPanel()
    {
        if (questionPanelRoot != null)
            questionPanelRoot.SetActive(true);

        if (answerPanelRoot != null)
            answerPanelRoot.SetActive(false);

        RefreshSubmitState();
    }

    private void ShowAnswer(string message)
    {
        if (questionPanelRoot != null)
            questionPanelRoot.SetActive(false);

        if (answerPanelRoot != null)
            answerPanelRoot.SetActive(true);

        if (answerText != null)
            answerText.text = string.IsNullOrWhiteSpace(message) ? "No answer configured." : message.Trim();
    }

    private IEnumerator ReturnToQuestionPanel()
    {
        yield return new WaitForSeconds(answerVisibleSeconds);
        _answerRoutine = null;
        ShowQuestionPanel();
    }

    public string GetAnswerForQuestion(int index)
    {
        if (index < 0 || index >= _submittedAnswers.Count)
            return string.Empty;

        return _submittedAnswers[index];
    }

    public string BuildCollectedSummary()
    {
        if (_submittedAnswers.Count == 0)
            return string.Empty;

        StringBuilder summaryBuilder = new StringBuilder();

        for (int i = 0; i < formQuestions.Count && i < _submittedAnswers.Count; i++)
        {
            if (i < PredefinedQuestions.Length)
                summaryBuilder.Append(PredefinedQuestions[i]).Append(' ');

            summaryBuilder.Append(_submittedAnswers[i]);

            if (i < _submittedAnswers.Count - 1)
                summaryBuilder.AppendLine();
        }

        return summaryBuilder.ToString().Trim();
    }

    private bool AreAllFormAnswersValid()
    {
        if (formQuestions.Count != PredefinedQuestions.Length)
            return false;

        for (int i = 0; i < formQuestions.Count; i++)
        {
            RobotQuestionFormEntry entry = formQuestions[i];
            if (entry == null || entry.answerInput == null)
                return false;

            if (string.IsNullOrWhiteSpace(entry.answerInput.text))
                return false;
        }

        return true;
    }

    private void CacheSubmittedAnswers()
    {
        _submittedAnswers.Clear();

        for (int i = 0; i < formQuestions.Count; i++)
        {
            RobotQuestionFormEntry entry = formQuestions[i];
            _submittedAnswers.Add(entry != null && entry.answerInput != null
                ? entry.answerInput.text.Trim()
                : string.Empty);
        }
    }

    private string BuildSubmissionMessage()
    {
        return SubmitConfirmationMessage;
    }

    private void SetFormStatus(string message)
    {
        if (formStatusText != null)
            formStatusText.text = string.IsNullOrWhiteSpace(message) ? string.Empty : message.Trim();
    }
}
