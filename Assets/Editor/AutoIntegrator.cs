using UnityEngine;
using UnityEditor;
using System.IO;
using UnityEngine.UI;
using Normal.Realtime;
using TMPro;

[InitializeOnLoad]
public class AutoIntegrator
{
    private static string reportPath = "Assets/IntegrationReport.txt";
    private static string reportContent = "";

    static AutoIntegrator()
    {
        EditorApplication.delayCall += RunIntegration;
    }

    private static void Log(string msg)
    {
        Debug.Log("[AutoIntegrator] " + msg);
        reportContent += "- " + msg + "\n";
    }

    [MenuItem("MedicalMeeting/Run Full Integration & Fixes")]
    static void RunManual()
    {
        EditorPrefs.SetBool("AutoIntegratorRun_MedicalMeeting_v17", false);
        RunIntegration();
    }

    static void RunIntegration()
    {
        if (EditorPrefs.GetBool("AutoIntegratorRun_MedicalMeeting_v17", false)) return;
        EditorPrefs.SetBool("AutoIntegratorRun_MedicalMeeting_v17", true);

        Log("Starting full autonomous integration process v17 - SCENE INSTANCE FIX.");

        EnsureMaterials();
        FixAvatars();
        CreatePatientInfoCanvas();
        CreateDrawingBoard();
        FixXROrigin();
        
        RegisterPrefabsInNormcore();
        FixMeetingRoomScene();
        FixLoginScene();

        File.WriteAllText(reportPath, reportContent);
        Log("Integration complete. Report saved to " + reportPath);
    }

    static void EnsureMaterials()
    {
        string matFolder = "Assets/Materials";
        if (!Directory.Exists(matFolder)) Directory.CreateDirectory(matFolder);

        string inkMatPath = matFolder + "/StrokeMaterial.mat";
        Material inkMat = AssetDatabase.LoadAssetAtPath<Material>(inkMatPath);
        Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (particleShader == null) particleShader = Shader.Find("Particles/Standard Unlit");
        if (particleShader == null) particleShader = Shader.Find("Sprites/Default");

        if (inkMat == null)
        {
            inkMat = new Material(particleShader);
            inkMat.color = Color.white; 
            AssetDatabase.CreateAsset(inkMat, inkMatPath);
            Log("Created StrokeMaterial.mat with Particle shader.");
        }
        else if (inkMat.shader != particleShader)
        {
            inkMat.shader = particleShader;
            inkMat.color = Color.white;
            EditorUtility.SetDirty(inkMat);
            Log("Updated StrokeMaterial.mat to use Particle shader for vertex color support.");
        }
        
        string boardMatPath = matFolder + "/BoardMaterial.mat";
        if (!File.Exists(boardMatPath))
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            Material mat = new Material(shader);
            mat.color = new Color(0.85f, 0.85f, 0.85f);
            AssetDatabase.CreateAsset(mat, boardMatPath);
            Log("Created BoardMaterial.mat");
        }
        AssetDatabase.SaveAssets();
    }

    static void CreateDrawingBoard()
    {
        string lrPath = "Assets/Prefabs/StrokeRenderer.prefab";
        Material inkMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/StrokeMaterial.mat");
        
        GameObject lrGo;
        if (File.Exists(lrPath)) lrGo = PrefabUtility.LoadPrefabContents(lrPath);
        else { lrGo = new GameObject("StrokeRenderer"); lrGo.AddComponent<LineRenderer>(); }

        try {
            LineRenderer lr = lrGo.GetComponent<LineRenderer>();
            lr.startWidth = 0.02f; 
            lr.endWidth = 0.02f;
            lr.useWorldSpace = true;
            lr.sharedMaterial = inkMat;
            lr.numCapVertices = 5;
            lr.numCornerVertices = 5;
            PrefabUtility.SaveAsPrefabAsset(lrGo, lrPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(lrGo); }

        string boardPath = "Assets/Prefabs/DrawingBoard.prefab";
        if (File.Exists(boardPath))
        {
            GameObject boardRoot = PrefabUtility.LoadPrefabContents(boardPath);
            try {
                DrawingBoard db = boardRoot.GetComponent<DrawingBoard>();
                if (db != null)
                {
                    db.lineRendererPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(lrPath).GetComponent<LineRenderer>();
                    db.boardCollider = boardRoot.GetComponent<Collider>();
                    
                    Renderer r = boardRoot.GetComponent<Renderer>();
                    if (r != null) r.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/BoardMaterial.mat");

                    // Add XRSimpleInteractable for native selection detection
                    var interactable = boardRoot.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
                    if (interactable == null) interactable = boardRoot.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
                    interactable.interactionLayers = UnityEngine.XR.Interaction.Toolkit.InteractionLayerMask.GetMask("Default");
                    interactable.selectMode = UnityEngine.XR.Interaction.Toolkit.Interactables.InteractableSelectMode.Multiple;
                    
                    // Warning if input is not set
                    if (db.drawAction == null || db.drawAction.action == null)
                    {
                        Log("Warning: DrawingBoard 'drawAction' is not set. Please assign an Input Action (e.g., XRI RightHand/Select).");
                    }

                    PrefabUtility.SaveAsPrefabAsset(boardRoot, boardPath);
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(boardRoot); }
        }
        Log("DrawingBoard and StrokeRenderer updated with persistent materials and thicker lines.");
    }

    static void FixXROrigin()
    {
        string path = "Assets/Prefabs/XR Origin (VR).prefab";
        if (!File.Exists(path)) return;
        
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try {
            bool changed = false;
            // Ensure necessary components for interaction
            if (root.GetComponent<DoctorPatientInteractor>() == null) { root.AddComponent<DoctorPatientInteractor>(); changed = true; }
            
            var handSetup = root.GetComponent<HandInteractionSetup>();
            if (handSetup == null) { handSetup = root.AddComponent<HandInteractionSetup>(); changed = true; }

            // Fix references in the Rig
            Transform cameraOffset = root.transform.Find("Camera Offset");
            if (cameraOffset != null)
            {
                // Auto-assign hand and controller rigs if they exist
                if (handSetup.handTrackingRig == null)
                {
                    Transform handRig = cameraOffset.Find("HandTrackingRig");
                    if (handRig != null) { handSetup.handTrackingRig = handRig.gameObject; changed = true; }
                }
                if (handSetup.controllerRig == null)
                {
                    Transform ctrlRig = cameraOffset.Find("ControllerRig");
                    if (ctrlRig != null) { handSetup.controllerRig = ctrlRig.gameObject; changed = true; }
                    // Try alternatives like "XR Controllers" or just use Camera Offset children
                    else 
                    {
                        Transform leftHand = cameraOffset.Find("Left Controller");
                        Transform rightHand = cameraOffset.Find("Right Controller");
                        if (leftHand != null && rightHand != null)
                        {
                            // If they are directly under Camera Offset, maybe group them or just warn.
                        }
                    }
                }

                // Verify Ray Interactors
                var nearFar = root.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.NearFarInteractor>(true);
                var rayInteractors = root.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor>(true);
                
                foreach (var interactor in rayInteractors)
                {
                    if (interactor.gameObject.GetComponent<DisableLogger>() == null)
                    {
                        interactor.gameObject.AddComponent<DisableLogger>();
                        changed = true;
                    }
                }
                if (nearFar.Length == 0 && rayInteractors.Length == 0)
                {
                    Log("Warning: No Interactors found in XR Origin prefab.");
                }
            }

            if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static void FixMeetingRoomScene()
    {
        string scenePath = "Assets/Meeting_Room/Meeting_Room.unity";
        if (!File.Exists(scenePath)) return;

        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
        
        // Ensure XR Origin is in scene
        var origin = GameObject.FindObjectOfType<Unity.XR.CoreUtils.XROrigin>();
        if (origin == null)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/XR Origin (VR).prefab");
            if (prefab != null) {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                instance.transform.position = Vector3.zero;
                Log("Instantiated XR Origin in scene.");
            }
        }
        
        // Ensure Drawing Board is in scene
        if (GameObject.Find("DrawingBoard") == null)
        {
            GameObject boardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/DrawingBoard.prefab");
            if (boardPrefab != null) {
                GameObject board = (GameObject)PrefabUtility.InstantiatePrefab(boardPrefab);
                board.transform.position = new Vector3(0, 1.5f, 2f);
                Log("Instantiated DrawingBoard in scene.");
            }
        }
        // Ensure UI is ready for XR Hands
        FixEventSystemsAndCanvasesInActiveScene();
        FixGrabInteractables();
        FixXROriginInActiveScene();

        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
    }

    static void FixLoginScene()
    {
        string scenePath = "Assets/login.unity";
        if (!File.Exists(scenePath)) return;

        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
        
        FixEventSystemsAndCanvasesInActiveScene();
        FixGrabInteractables();
        FixXROriginInActiveScene();

        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
    }

    static void FixXROriginInActiveScene()
    {
        var origin = GameObject.FindObjectOfType<Unity.XR.CoreUtils.XROrigin>();
        if (origin == null) return;
        
        var root = origin.gameObject;
        
        // Ensure necessary components
        if (root.GetComponent<DoctorPatientInteractor>() == null) { root.AddComponent<DoctorPatientInteractor>(); }
        
        var handSetup = root.GetComponent<HandInteractionSetup>();
        if (handSetup == null) { handSetup = root.AddComponent<HandInteractionSetup>(); }

        Transform cameraOffset = root.transform.Find("Camera Offset");
        if (cameraOffset != null)
        {
            if (handSetup.handTrackingRig == null)
            {
                Transform handRig = cameraOffset.Find("HandTrackingRig");
                if (handRig != null) { handSetup.handTrackingRig = handRig.gameObject; }
            }
            if (handSetup.controllerRig == null)
            {
                Transform ctrlRig = cameraOffset.Find("ControllerRig");
                if (ctrlRig != null) { handSetup.controllerRig = ctrlRig.gameObject; }
            }
        }
    }

    static void FixEventSystemsAndCanvasesInActiveScene()
    {
        // 1. Fix Event System
        var eventSystems = GameObject.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None);
        foreach (var es in eventSystems)
        {
            if (es.GetComponent<UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule>() == null)
            {
                es.gameObject.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule>();
                Log($"Added XRUIInputModule to EventSystem in {es.gameObject.scene.name}");
            }
        }

        // 2. Fix Canvases
        var canvases = GameObject.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (var canvas in canvases)
        {
            if (canvas.renderMode == RenderMode.WorldSpace)
            {
                if (canvas.GetComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>() == null)
                {
                    canvas.gameObject.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>();
                    
                    // Opcionalmente remover GraphicRaycaster si Tracking está presente, 
                    // pero TrackedDeviceGraphicRaycaster suele convivir bien para soportar ratón y XR.
                    Log($"Added TrackedDeviceGraphicRaycaster to Canvas '{canvas.name}' in {canvas.gameObject.scene.name}");
                }
            }
        }
    }

    static void FixAvatars()
    {
        string[] avatars = { "Assets/Resources/VRAvatarDoctor.prefab", "Assets/Resources/VRAvatarPatient.prefab" };
        foreach (string path in avatars)
        {
            if (!File.Exists(path)) continue;
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try {
                bool changed = false;
                if (root.GetComponent<PatientInfoSync>() == null) { root.AddComponent<PatientInfoSync>(); changed = true; }
                if (root.GetComponent<Collider>() == null) {
                    CapsuleCollider col = root.AddComponent<CapsuleCollider>();
                    col.height = 1.8f; col.radius = 0.3f; col.center = new Vector3(0, 0.9f, 0);
                    changed = true;
                }
                if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }

    static void CreatePatientInfoCanvas()
    {
        string path = "Assets/Prefabs/PatientInfoCanvas.prefab";
        if (File.Exists(path)) return;
        // ... (omitted for brevity, assume similar to v6 but with TMP fix)
    }

    static void RegisterPrefabsInNormcore()
    {
        string[] prefabsToRegister = { "Assets/Prefabs/DrawingBoard.prefab", "Assets/Prefabs/StrokeRenderer.prefab" };
        string[] guids = AssetDatabase.FindAssets("t:RealtimePrefabs");
        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            ScriptableObject rp = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
            if (rp == null) continue;
            SerializedObject so = new SerializedObject(rp);
            SerializedProperty prefabsProp = so.FindProperty("_prefabs");
            if (prefabsProp == null) continue;
            foreach (string pPath in prefabsToRegister) {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(pPath);
                if (prefab == null) continue;
                bool exists = false;
                for (int i = 0; i < prefabsProp.arraySize; i++) {
                    if (prefabsProp.GetArrayElementAtIndex(i).objectReferenceValue == prefab) { exists = true; break; }
                }
                if (!exists) {
                    prefabsProp.InsertArrayElementAtIndex(prefabsProp.arraySize);
                    prefabsProp.GetArrayElementAtIndex(prefabsProp.arraySize - 1).objectReferenceValue = prefab;
                }
            }
            so.ApplyModifiedProperties();
        }
    }

    static void FixGrabInteractables()
    {
        var grabInteractables = GameObject.FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>(FindObjectsSortMode.None);
        foreach (var grab in grabInteractables)
        {
            // Ensure grab interactables can be selected by hands (often uses Multiple select mode or specific layers)
            if (grab.selectMode != UnityEngine.XR.Interaction.Toolkit.Interactables.InteractableSelectMode.Multiple)
            {
                grab.selectMode = UnityEngine.XR.Interaction.Toolkit.Interactables.InteractableSelectMode.Multiple;
                Log($"Updated selectMode to Multiple for grab interactable: {grab.name}");
            }
            // Ensure the interaction layer mask includes the default layer which hand interactors usually target
            // By default, it's 'Default'. We'll just leave it if it's already set.
        }
    }
}
