using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class InspectXROriginSetup : MonoBehaviour
{
    [MenuItem("Tools/Inspect XR Origin in MeetingRoom")]
    public static void Inspect()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.name != "Meeting_Room") {
            Debug.LogError("Please open Meeting_Room scene first.");
            return;
        }

        var xrOrigin = GameObject.Find("XR Origin (XR Rig)");
        if (xrOrigin == null) {
            Debug.LogError("XR Origin (XR Rig) not found.");
            return;
        }

        var interactors = xrOrigin.GetComponentsInChildren<XRBaseInteractor>(true);
        Debug.Log("Found interactors on XR Origin:");
        foreach (var i in interactors) {
            Debug.Log($"- {i.gameObject.name}: {i.GetType().Name} (Active: {i.gameObject.activeInHierarchy})");
        }
    }
}
