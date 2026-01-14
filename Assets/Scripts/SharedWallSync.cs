using TMPro;
using UnityEngine;
using Normal.Realtime;

public class SharedWallSync : RealtimeComponent<SharedWallModel>
{
    [Header("UI targets (the wall browser panel)")]
    public TMP_Text headerText;   // optional
    public TMP_Text outputText;   // the big scrollable text

    // Doctor calls these:
    public void DoctorShowAudit(string title, string content)
    {
        if (!EnsureOwnership()) return;
        model.mode = 1;
        model.title = title;
        model.content = content;
        model.nonce++;
    }

    public void DoctorShowAnalytics(string title, string content)
    {
        if (!EnsureOwnership()) return;
        model.mode = 2;
        model.title = title;
        model.content = content;
        model.nonce++;
    }

    // take ownership so doctor can write to model
    private bool EnsureOwnership()
    {
        var view = GetComponent<RealtimeView>();
        if (view != null && !view.isOwnedLocallyInHierarchy)
            view.RequestOwnership();

        // ownership request may take a frame, but usually immediate
        return (view == null) || view.isOwnedLocallyInHierarchy;
    }

    protected override void OnRealtimeModelReplaced(SharedWallModel previousModel, SharedWallModel currentModel)
    {
        if (previousModel != null)
        {
            previousModel.titleDidChange -= OnTitleChanged;
            previousModel.contentDidChange -= OnContentChanged;
            previousModel.nonceDidChange -= OnNonceChanged;
        }

        if (currentModel != null)
        {
            currentModel.titleDidChange += OnTitleChanged;
            currentModel.contentDidChange += OnContentChanged;
            currentModel.nonceDidChange += OnNonceChanged;

            // Apply immediately for late joiners
            ApplyAll();
        }
    }

    private void OnTitleChanged(SharedWallModel m, string value) => ApplyTitle(value);
    private void OnContentChanged(SharedWallModel m, string value) => ApplyContent(value);

    private void OnNonceChanged(SharedWallModel m, int value)
    {
        // force refresh even if user clicked same thing again
        ApplyAll();
    }

    private void ApplyAll()
    {
        ApplyTitle(model.title);
        ApplyContent(model.content);
    }

    private void ApplyTitle(string t)
    {
        if (headerText != null) headerText.text = t ?? "";
    }

    private void ApplyContent(string c)
    {
        if (outputText != null) outputText.text = c ?? "";
    }
}
