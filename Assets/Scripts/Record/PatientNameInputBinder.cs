using TMPro;
using UnityEngine;

public class PatientNameInputBinder : MonoBehaviour
{
    public TMP_InputField patientNameInput;

    public void ApplyPatientName()
    {
        if (LoginContext.Instance == null || SessionData.CurrentUser == null) return;

        string doctor = SessionData.CurrentUser.username;
        string patient = patientNameInput != null ? patientNameInput.text : "";

        // Save doctor + typed patient
        LoginContext.Instance.SetUsers(doctor, patient);

        Debug.Log("[PatientName] Set pair to: " + LoginContext.Instance.GetPairFolderName());
    }
}
