using System.IO;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

[InitializeOnLoad]
public class StatusDumper
{
    static StatusDumper()
    {
        EditorApplication.delayCall += Dump;
    }

    static void Dump()
    {
        if (EditorPrefs.GetBool("StatusDumper_Run1", false)) return;
        EditorPrefs.SetBool("StatusDumper_Run1", true);

        string log = "--- STATUS DUMP ---\n";
        
        // Check LocalBodyRig_Doctor
        var docRig = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/LocalBodyRig_Doctor.prefab");
        log += "LocalBodyRig_Doctor: " + (docRig != null ? "Found" : "Missing") + "\n";
        if (docRig != null) {
            var rigComp = docRig.GetComponent<LocalBodyRig>();
            log += "  Has LocalBodyRig: " + (rigComp != null) + "\n";
            if (rigComp != null) {
                log += $"  bodyRoot: {(rigComp.bodyRoot != null ? rigComp.bodyRoot.name : "null")}\n";
                log += $"  xrCamera: {(rigComp.xrCamera != null ? rigComp.xrCamera.name : "null")}\n";
                log += $"  hiddenRenderers count: {(rigComp.hiddenRenderers != null ? rigComp.hiddenRenderers.Length : 0)}\n";
                log += $"  visibleRenderers count: {(rigComp.visibleRenderers != null ? rigComp.visibleRenderers.Length : 0)}\n";
            }
        }

        // Check LocalBodyRig_Patient
        var patRig = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/LocalBodyRig_Patient.prefab");
        log += "LocalBodyRig_Patient: " + (patRig != null ? "Found" : "Missing") + "\n";
        
        // Check VRAvatarDoctor and Patient
        var docAvatar = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/VRAvatarDoctor.prefab");
        log += "VRAvatarDoctor: " + (docAvatar != null ? "Found" : "Missing") + "\n";
        if (docAvatar != null) {
            log += "  Has PatientInfoSync: " + (docAvatar.GetComponent<PatientInfoSync>() != null) + "\n";
        }
        var patAvatar = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/VRAvatarPatient.prefab");
        log += "VRAvatarPatient: " + (patAvatar != null ? "Found" : "Missing") + "\n";
        if (patAvatar != null) {
            log += "  Has PatientInfoSync: " + (patAvatar.GetComponent<PatientInfoSync>() != null) + "\n";
        }

        // Check XR Origin (VR)
        var xrOrigin = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/XR Origin (VR).prefab");
        log += "XR Origin (VR): " + (xrOrigin != null ? "Found" : "Missing") + "\n";
        if (xrOrigin != null) {
            log += "  Has HandInteractionSetup: " + (xrOrigin.GetComponent<HandInteractionSetup>() != null) + "\n";
            log += "  Has DoctorPatientInteractor: " + (xrOrigin.GetComponent<DoctorPatientInteractor>() != null) + "\n";
            var rig = xrOrigin.GetComponent<HandInteractionSetup>();
            if (rig != null) {
                log += $"  handTrackingRig: {(rig.handTrackingRig != null ? rig.handTrackingRig.name : "null")}\n";
                log += $"  controllerRig: {(rig.controllerRig != null ? rig.controllerRig.name : "null")}\n";
            }
            var interactor = xrOrigin.GetComponent<DoctorPatientInteractor>();
            if (interactor != null) {
                log += $"  patientInfoCanvas: {(interactor.patientInfoCanvas != null ? interactor.patientInfoCanvas.name : "null")}\n";
            }
        }

        // Check PatientInfoCanvas
        var canvas = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PatientInfoCanvas.prefab");
        log += "PatientInfoCanvas: " + (canvas != null ? "Found" : "Missing") + "\n";
        
        // Check DrawingBoard
        var board = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/DrawingBoard.prefab");
        log += "DrawingBoard: " + (board != null ? "Found" : "Missing") + "\n";

        File.WriteAllText("dump.txt", log);
        Debug.Log("StatusDumper finished writing to dump.txt");
    }
}
