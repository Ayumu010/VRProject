
#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

/// <summary>
/// 既存のKogetsuSword.prefabを修復するEditorユーティリティ。
///
/// ・XRGeneralGrabTransformerの追加
/// ・KogetsuCollisionGrabTransformerの追加
/// ・Collider参照の設定
/// ・Transformerの実行順序設定
/// ・KogetsuWeaponの参照設定
/// ・既存KogetsuBehaviour.assetのPrefab参照更新
///
/// 既存Prefabを別名で複製せず、元のパスに保存する。
/// </summary>
public static class KogetsuPrefabRepairUtility
{
    private const string SwordPrefabPath =
        "Assets/Trigger/Kogetu/KogetsuSword.prefab";

    private const string KogetsuBehaviourPath =
        "Assets/Trigger/Kogetu/KogetsuBehaviour.asset";

    [MenuItem("Tools/VR/Repair Existing Kogetsu Sword Prefab")]
    private static void RepairExistingPrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(SwordPrefabPath) == null)
        {
            EditorUtility.DisplayDialog(
                "Kogetsu Repair",
                "KogetsuSword.prefab was not found:\n" + SwordPrefabPath,
                "OK");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<KogetsuTriggerBehaviour>(
                KogetsuBehaviourPath) == null)
        {
            EditorUtility.DisplayDialog(
                "Kogetsu Repair",
                "KogetsuBehaviour.asset was not found:\n" +
                KogetsuBehaviourPath,
                "OK");
            return;
        }

        GameObject prefabRoot = null;
        bool saveSucceeded = false;

        try
        {
            prefabRoot = PrefabUtility.LoadPrefabContents(SwordPrefabPath);

            if (prefabRoot == null)
            {
                throw new System.InvalidOperationException(
                    "Failed to load KogetsuSword.prefab.");
            }

            ConfigurePrefab(prefabRoot);

            PrefabUtility.SaveAsPrefabAsset(
                prefabRoot,
                SwordPrefabPath,
                out saveSucceeded);

            if (!saveSucceeded)
            {
                throw new System.InvalidOperationException(
                    "Failed to save KogetsuSword.prefab.");
            }
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);

            EditorUtility.DisplayDialog(
                "Kogetsu Repair Failed",
                "Prefab repair failed.\nCheck the Unity Console.",
                "OK");

            return;
        }
        finally
        {
            if (prefabRoot != null)
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        if (!saveSucceeded)
            return;

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        try
        {
            UpdateKogetsuBehaviourReference();

            AssetDatabase.SaveAssets();

            Debug.Log(
                "Kogetsu Prefab Repair Completed.\n" +
                "Prefab: " + SwordPrefabPath,
                AssetDatabase.LoadAssetAtPath<GameObject>(SwordPrefabPath));

            EditorUtility.DisplayDialog(
                "Kogetsu Repair Complete",
                "Existing KogetsuSword.prefab was repaired.\n\n" +
                "XRGeneralGrabTransformer: Added / Configured\n" +
                "KogetsuCollisionGrabTransformer: Added / Configured\n" +
                "KogetsuWeapon: References updated\n" +
                "KogetsuBehaviour.asset: Prefab reference updated",
                "OK");
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);

            EditorUtility.DisplayDialog(
                "Kogetsu Reference Update Failed",
                "The prefab was saved, but the Behaviour reference " +
                "could not be updated.\nCheck the Unity Console.",
                "OK");
        }
    }

    /// <summary>
    /// Prefab内部のコンポーネントと参照を設定する。
    /// </summary>
    private static void ConfigurePrefab(GameObject root)
    {
        // --------------------------------------------------
        // 基本コンポーネント
        // --------------------------------------------------

        Rigidbody body = RequireRootComponent<Rigidbody>(root);

        XRGrabInteractable grab =
            RequireRootComponent<XRGrabInteractable>(root);

        // --------------------------------------------------
        // Transform取得
        // --------------------------------------------------

        Transform gripAttach =
            RequireChildTransform(root.transform, "GripAttach");

        Transform bladeBase =
            RequireChildTransform(root.transform, "BladeBase");

        Transform bladeTip =
            RequireChildTransform(root.transform, "BladeTip");

        // --------------------------------------------------
        // 物理Collider取得
        // --------------------------------------------------

        BoxCollider handleCollider =
            RequireChildComponent<BoxCollider>(
                root.transform,
                "HandlePhysicalCollider");

        CapsuleCollider bladeCollider =
            RequireChildComponent<CapsuleCollider>(
                root.transform,
                "BladePhysicalCollider");

        BoxCollider extensionCollider =
            RequireChildComponent<BoxCollider>(
                root.transform,
                "SenkuExtensionVisual");

        // 初期状態では旋空延長Colliderを無効にする。
        handleCollider.isTrigger = false;
        bladeCollider.isTrigger = false;
        extensionCollider.isTrigger = false;
        extensionCollider.enabled = false;

        // --------------------------------------------------
        // 旋空Trail
        // --------------------------------------------------

        TrailRenderer trail =
            RequireChildComponent<TrailRenderer>(
                root.transform,
                "SenkuTrailAnchor");

        // --------------------------------------------------
        // 攻撃判定
        // --------------------------------------------------

        KogetsuBladeDamage bladeDamage =
            GetOrAddRootComponent<KogetsuBladeDamage>(root);

        SetObjectReference(
            bladeDamage,
            "bladeBase",
            bladeBase);

        SetObjectReference(
            bladeDamage,
            "bladeTip",
            bladeTip);

        // --------------------------------------------------
        // 旋空表示
        // --------------------------------------------------

        KogetsuBladeExtensionVisual extensionVisual =
            RequireChildComponent<KogetsuBladeExtensionVisual>(
                root.transform,
                "SenkuExtensionVisual");

        SetObjectReference(
            extensionVisual,
            "bladeBase",
            bladeBase);

        SetObjectReference(
            extensionVisual,
            "bladeTip",
            bladeTip);

        SetObjectReference(
            extensionVisual,
            "senkuTrail",
            trail);

        // --------------------------------------------------
        // Grab Transformer
        // --------------------------------------------------

        XRGeneralGrabTransformer generalTransformer =
            GetOrAddRootComponent<XRGeneralGrabTransformer>(root);

        KogetsuCollisionGrabTransformer collisionTransformer =
            GetOrAddRootComponent<KogetsuCollisionGrabTransformer>(root);

        generalTransformer.enabled = true;
        collisionTransformer.enabled = true;

        // --------------------------------------------------
        // XRGrabInteractable設定
        // --------------------------------------------------

        grab.selectMode = InteractableSelectMode.Single;

        grab.movementType =
            XRBaseInteractable.MovementType.Kinematic;

        grab.trackPosition = true;
        grab.trackRotation = true;

        grab.smoothPosition = false;
        grab.smoothRotation = false;

        grab.throwOnDetach = true;

        grab.attachTransform = gripAttach;

        // 掴み判定は従来どおり柄Colliderだけにする。
        grab.colliders.Clear();
        grab.colliders.Add(handleCollider);

        // XRI標準Transformerの自動追加を無効化する。
        grab.addDefaultGrabTransformers = false;

        // Single Grabの実行順序を明示する。
        grab.startingSingleGrabTransformers.Clear();

        // 1. 手のGripAttachに合わせた通常追従
        grab.startingSingleGrabTransformers.Add(
            generalTransformer);

        // 2. 衝突判定によるPose制限
        grab.startingSingleGrabTransformers.Add(
            collisionTransformer);

        // Multiple Grabは今回使用しない。
        grab.startingMultipleGrabTransformers.Clear();

        // --------------------------------------------------
        // Collision Transformer参照
        // --------------------------------------------------

        SetObjectReference(
            collisionTransformer,
            "grabInteractable",
            grab);

        SetObjectReference(
            collisionTransformer,
            "weaponBody",
            body);

        SetObjectArrayReference(
            collisionTransformer,
            "collisionColliders",
            new UnityEngine.Object[]
            {
                handleCollider,
                bladeCollider,
                extensionCollider
            });

        // --------------------------------------------------
        // KogetsuWeapon参照
        // --------------------------------------------------

        KogetsuWeapon weapon =
            GetOrAddRootComponent<KogetsuWeapon>(root);

        SetObjectReference(
            weapon,
            "grabInteractable",
            grab);

        SetObjectReference(
            weapon,
            "gripAttachTransform",
            gripAttach);

        SetObjectReference(
            weapon,
            "collisionTransformer",
            collisionTransformer);

        SetObjectReference(
            weapon,
            "bladeDamage",
            bladeDamage);

        SetObjectReference(
            weapon,
            "extensionVisual",
            extensionVisual);
    }

    /// <summary>
    /// KogetsuBehaviour.assetのPrefab参照を更新する。
    /// </summary>
    private static void UpdateKogetsuBehaviourReference()
    {
        GameObject swordPrefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(
                SwordPrefabPath);

        KogetsuTriggerBehaviour behaviour =
            AssetDatabase.LoadAssetAtPath<KogetsuTriggerBehaviour>(
                KogetsuBehaviourPath);

        if (swordPrefab == null || behaviour == null)
        {
            throw new System.InvalidOperationException(
                "Failed to load updated prefab or KogetsuBehaviour.");
        }

        SetObjectReference(
            behaviour,
            "swordPrefab",
            swordPrefab);
    }

    /// <summary>
    /// Prefabルートのコンポーネントを取得する。
    /// </summary>
    private static T RequireRootComponent<T>(GameObject root)
        where T : Component
    {
        T component = root.GetComponent<T>();

        if (component == null)
        {
            throw new System.InvalidOperationException(
                "Required component is missing: " + typeof(T).Name);
        }

        return component;
    }

    /// <summary>
    /// Prefabルートにコンポーネントがなければ追加する。
    /// </summary>
    private static T GetOrAddRootComponent<T>(GameObject root)
        where T : Component
    {
        T component = root.GetComponent<T>();

        if (component == null)
        {
            component = root.AddComponent<T>();
        }

        return component;
    }

    /// <summary>
    /// 子階層から名前に一致するTransformを取得する。
    /// </summary>
    private static Transform RequireChildTransform(
        Transform root,
        string childName)
    {
        Transform[] transforms =
            root.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i].name == childName)
                return transforms[i];
        }

        throw new System.InvalidOperationException(
            "Required child Transform was not found: " + childName);
    }

    /// <summary>
    /// 子階層から指定名のGameObjectにあるコンポーネントを取得する。
    /// </summary>
    private static T RequireChildComponent<T>(
        Transform root,
        string childName)
        where T : Component
    {
        Transform child =
            RequireChildTransform(root, childName);

        T component = child.GetComponent<T>();

        if (component == null)
        {
            throw new System.InvalidOperationException(
                "Required component " + typeof(T).Name +
                " was not found on child: " + childName);
        }

        return component;
    }

    /// <summary>
    /// SerializedObject経由でObject参照を設定する。
    /// </summary>
    private static void SetObjectReference(
        UnityEngine.Object target,
        string propertyName,
        UnityEngine.Object value)
    {
        SerializedObject serializedObject =
            new SerializedObject(target);

        serializedObject.Update();

        SerializedProperty property =
            serializedObject.FindProperty(propertyName);

        if (property == null)
        {
            throw new System.InvalidOperationException(
                "Serialized field was not found: " +
                target.GetType().Name + "." + propertyName);
        }

        property.objectReferenceValue = value;

        serializedObject.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(target);
    }

    /// <summary>
    /// SerializedObject経由でObject配列を設定する。
    /// </summary>
    private static void SetObjectArrayReference(
        UnityEngine.Object target,
        string propertyName,
        UnityEngine.Object[] values)
    {
        SerializedObject serializedObject =
            new SerializedObject(target);

        serializedObject.Update();

        SerializedProperty property =
            serializedObject.FindProperty(propertyName);

        if (property == null || !property.isArray)
        {
            throw new System.InvalidOperationException(
                "Serialized array field was not found: " +
                target.GetType().Name + "." + propertyName);
        }

        property.arraySize =
            values != null ? values.Length : 0;

        for (int i = 0; i < property.arraySize; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue =
                values[i];
        }

        serializedObject.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(target);
    }
}

#endif
