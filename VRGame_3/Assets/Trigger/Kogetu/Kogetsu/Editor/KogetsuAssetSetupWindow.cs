#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Imported sword/scabbard FBX models are wrapped in runtime-ready prefab roots,
/// then Kogetsu/Senku Behaviour and TriggerDefinition assets are generated.
/// Geometry locators are intentionally editable in Prefab Mode because FBX pivots differ.
/// </summary>
public sealed class KogetsuAssetSetupWindow : EditorWindow
{
    private GameObject swordModel;
    private GameObject scabbardModel;
    private Material senkuExtensionMaterial;
    private DefaultAsset outputFolder;
    private bool createTriggerAssets = true;

    private const string DefaultOutputFolder = "Assets/Trigger/Kogetsu/Generated";

    [MenuItem("Tools/VR/Kogetsu Weapon Setup")]
    private static void Open()
    {
        GetWindow<KogetsuAssetSetupWindow>("Kogetsu Setup");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Model References", EditorStyles.boldLabel);
        swordModel = (GameObject)EditorGUILayout.ObjectField(
            "Kogetsu Sword FBX",
            swordModel,
            typeof(GameObject),
            false);
        scabbardModel = (GameObject)EditorGUILayout.ObjectField(
            "Scabbard FBX",
            scabbardModel,
            typeof(GameObject),
            false);
        senkuExtensionMaterial = (Material)EditorGUILayout.ObjectField(
            "Senku Extension Material",
            senkuExtensionMaterial,
            typeof(Material),
            false);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
        outputFolder = (DefaultAsset)EditorGUILayout.ObjectField(
            "Output Folder",
            outputFolder,
            typeof(DefaultAsset),
            false);
        createTriggerAssets = EditorGUILayout.ToggleLeft(
            "Create Kogetsu / Senku Trigger Assets",
            createTriggerAssets);

        EditorGUILayout.HelpBox(
            "生成されるPrefabはローカル+Z方向を刀身方向とする初期構成です。" +
            "FBXの向き・GripAttach・BladeBase/BladeTip・SocketAttachは、生成後にPrefab Modeで実モデルに合わせて調整してください。",
            MessageType.Info);

        using (new EditorGUI.DisabledScope(swordModel == null || scabbardModel == null))
        {
            if (GUILayout.Button("Build Kogetsu Prefabs and Trigger Assets", GUILayout.Height(34f)))
                BuildAll();
        }
    }

    private void BuildAll()
    {
        string folderPath = outputFolder != null
            ? AssetDatabase.GetAssetPath(outputFolder)
            : DefaultOutputFolder;

        if (string.IsNullOrEmpty(folderPath) || !folderPath.StartsWith("Assets"))
        {
            EditorUtility.DisplayDialog("Kogetsu Setup", "Output Folder must be inside the Assets folder.", "OK");
            return;
        }

        EnsureFolder(folderPath);

        GameObject swordPrefab = BuildSwordPrefab(folderPath);
        GameObject scabbardPrefab = BuildScabbardPrefab(folderPath);

        if (swordPrefab == null || scabbardPrefab == null)
        {
            EditorUtility.DisplayDialog("Kogetsu Setup", "Prefab generation failed. Check the Console.", "OK");
            return;
        }

        if (createTriggerAssets)
            CreateTriggerAssets(folderPath, swordPrefab, scabbardPrefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Kogetsu Setup Complete",
            "Kogetsu sword/scabbard prefabs were created.\n\n" +
            "Open both prefabs and align the FBX model and locator transforms before testing.",
            "OK");
    }

    private GameObject BuildSwordPrefab(string folderPath)
    {
        GameObject root = new GameObject("KogetsuSword");
        try
        {
            Rigidbody body = root.GetComponent<Rigidbody>();
            if (body == null)
                body = root.AddComponent<Rigidbody>();

            body.mass = 0.7f;
            body.useGravity = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            Transform visuals = CreateChild(root.transform, "VisualRoot");
            AddModelChild(visuals, swordModel, "SwordModel");

            Transform gripAttach = CreateChild(root.transform, "GripAttach");
            Transform bladeBase = CreateChild(root.transform, "BladeBase");
            bladeBase.localPosition = new Vector3(0f, 0f, 0.16f);
            Transform bladeTip = CreateChild(root.transform, "BladeTip");
            bladeTip.localPosition = new Vector3(0f, 0f, 0.95f);
            // Solid colliders are deliberately separate from sweep-based damage detection.
            Transform handleColliderRoot = CreateChild(root.transform, "HandlePhysicalCollider");
            BoxCollider handleCollider = handleColliderRoot.gameObject.AddComponent<BoxCollider>();
            handleCollider.size = new Vector3(0.07f, 0.05f, 0.22f);
            handleCollider.isTrigger = false;

            Transform bladeColliderRoot = CreateChild(root.transform, "BladePhysicalCollider");
            CapsuleCollider bladeCollider = bladeColliderRoot.gameObject.AddComponent<CapsuleCollider>();
            bladeCollider.direction = 2;
            bladeCollider.center = new Vector3(0f, 0f, 0.50f);
            bladeCollider.radius = 0.025f;
            bladeCollider.height = 0.86f;
            bladeCollider.isTrigger = false;

            // Add the interactable after the child colliders exist so its initial
            // collider discovery includes both the handle and blade colliders.
            XRGrabInteractable grab = root.AddComponent<XRGrabInteractable>();
            // 掴み判定・Socket検出には柄のColliderだけを使用する。
            grab.colliders.Clear();
            grab.colliders.Add(handleCollider);
            grab.selectMode = InteractableSelectMode.Single;
            grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
            grab.throwOnDetach = true;
            grab.attachTransform = gripAttach;

            Transform extensionObject = CreateChild(root.transform, "SenkuExtensionVisual");
            extensionObject.gameObject.AddComponent<MeshFilter>();
            MeshRenderer extensionRenderer = extensionObject.gameObject.AddComponent<MeshRenderer>();
            extensionRenderer.sharedMaterial = senkuExtensionMaterial;
            BoxCollider extensionCollider = extensionObject.gameObject.AddComponent<BoxCollider>();
            extensionCollider.isTrigger = false;
            extensionCollider.enabled = true;
            extensionCollider.center = Vector3.zero;
            extensionCollider.size = new Vector3(0.025f, 0.012f, 0.001f);

            KogetsuBladeExtensionVisual extensionVisual = extensionObject.gameObject.AddComponent<KogetsuBladeExtensionVisual>();
            SetObjectReference(extensionVisual, "bladeBase", bladeBase);
            SetObjectReference(extensionVisual, "bladeTip", bladeTip);

            KogetsuBladeDamage bladeDamage = root.AddComponent<KogetsuBladeDamage>();
            SetObjectReference(bladeDamage, "bladeBase", bladeBase);
            SetObjectReference(bladeDamage, "bladeTip", bladeTip);

            KogetsuWeapon weapon = root.AddComponent<KogetsuWeapon>();
            SetObjectReference(weapon, "grabInteractable", grab);
            SetObjectReference(weapon, "gripAttachTransform", gripAttach);
            SetObjectReference(weapon, "bladeDamage", bladeDamage);
            SetObjectReference(weapon, "extensionVisual", extensionVisual);

            string path = AssetDatabase.GenerateUniqueAssetPath(folderPath + "/KogetsuSword.prefab");
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Debug.Log($"Kogetsu sword prefab created: {path}", prefab);
            return prefab;
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
            return null;
        }
        finally
        {
            DestroyImmediate(root);
        }
    }

    private GameObject BuildScabbardPrefab(string folderPath)
    {
        GameObject root = new GameObject("KogetsuScabbard");
        try
        {
            Transform visuals = CreateChild(root.transform, "VisualRoot");
            AddModelChild(visuals, scabbardModel, "ScabbardModel");

            Transform socketAttach = CreateChild(root.transform, "SocketAttach");
            Transform socketZone = CreateChild(root.transform, "SocketZone");
            SphereCollider trigger = socketZone.gameObject.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 0.12f;

            XRSocketInteractor socket = socketZone.gameObject.AddComponent<XRSocketInteractor>();
            socket.attachTransform = socketAttach;
            socket.hoverSocketSnapping = true;
            socket.showInteractableHoverMeshes = false;
            socket.socketActive = false;

            KogetsuScabbardSocket scabbardSocket = root.AddComponent<KogetsuScabbardSocket>();
            SetObjectReference(scabbardSocket, "socketInteractor", socket);
            SetObjectReference(scabbardSocket, "socketAttachTransform", socketAttach);

            string path = AssetDatabase.GenerateUniqueAssetPath(folderPath + "/KogetsuScabbard.prefab");
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Debug.Log($"Kogetsu scabbard prefab created: {path}", prefab);
            return prefab;
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
            return null;
        }
        finally
        {
            DestroyImmediate(root);
        }
    }

    private void CreateTriggerAssets(string folderPath, GameObject swordPrefab, GameObject scabbardPrefab)
    {
        KogetsuTriggerBehaviour kogetsuBehaviour = CreateInstance<KogetsuTriggerBehaviour>();
        SetObjectReference(kogetsuBehaviour, "swordPrefab", swordPrefab);
        SetObjectReference(kogetsuBehaviour, "scabbardPrefab", scabbardPrefab);
        AssetDatabase.CreateAsset(
            kogetsuBehaviour,
            AssetDatabase.GenerateUniqueAssetPath(folderPath + "/KogetsuBehaviour.asset"));

        TriggerDefinition kogetsuDefinition = CreateInstance<TriggerDefinition>();
        SetString(kogetsuDefinition, "triggerId", "kogetsu");
        SetString(kogetsuDefinition, "triggerName", "Kogetsu");
        SetObjectReference(kogetsuDefinition, "behaviourAsset", kogetsuBehaviour);
        AssetDatabase.CreateAsset(
            kogetsuDefinition,
            AssetDatabase.GenerateUniqueAssetPath(folderPath + "/Kogetsu.asset"));

        SenkuTriggerBehaviour senkuBehaviour = CreateInstance<SenkuTriggerBehaviour>();
        SetFloat(senkuBehaviour, "extensionLength", 5f);
        SetFloat(senkuBehaviour, "activeDuration", 0.4f);
        SetFloat(senkuBehaviour, "cooldown", 1.5f);
        SetFloat(senkuBehaviour, "damageMultiplier", 1.5f);
        SetFloat(senkuBehaviour, "minimumSwingSpeed", 0f);
        AssetDatabase.CreateAsset(
            senkuBehaviour,
            AssetDatabase.GenerateUniqueAssetPath(folderPath + "/SenkuBehaviour.asset"));

        TriggerDefinition senkuDefinition = CreateInstance<TriggerDefinition>();
        SetString(senkuDefinition, "triggerId", "senku");
        SetString(senkuDefinition, "triggerName", "Senku");
        SetObjectReference(senkuDefinition, "behaviourAsset", senkuBehaviour);
        AssetDatabase.CreateAsset(
            senkuDefinition,
            AssetDatabase.GenerateUniqueAssetPath(folderPath + "/Senku.asset"));
    }

    private static Transform CreateChild(Transform parent, string childName)
    {
        GameObject child = new GameObject(childName);
        child.transform.SetParent(parent, false);
        child.transform.localPosition = Vector3.zero;
        child.transform.localRotation = Quaternion.identity;
        child.transform.localScale = Vector3.one;
        return child.transform;
    }

    private static void AddModelChild(Transform parent, GameObject sourceModel, string childName)
    {
        GameObject model = PrefabUtility.InstantiatePrefab(sourceModel) as GameObject;
        if (model == null)
            model = Instantiate(sourceModel);

        model.name = childName;
        model.transform.SetParent(parent, false);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one;
    }

    private static void SetObjectReference(Object target, string propertyName, Object value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            Debug.LogError($"Kogetsu Setup: Serialized field '{propertyName}' was not found on {target.GetType().Name}.", target);
            return;
        }

        property.objectReferenceValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    private static void SetString(Object target, string propertyName, string value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
            return;

        property.stringValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    private static void SetFloat(Object target, string propertyName, float value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
            return;

        property.floatValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    private static void EnsureFolder(string folderPath)
    {
        string[] segments = folderPath.Split('/');
        string current = segments[0];
        for (int i = 1; i < segments.Length; i++)
        {
            string next = current + "/" + segments[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, segments[i]);
            current = next;
        }
    }
}
#endif
