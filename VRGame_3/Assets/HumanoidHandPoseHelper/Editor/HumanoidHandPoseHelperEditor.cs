
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(HumanoidHandPoseHelper))]
public class HumanoidHandPoseHelperEditor : Editor
{
    private HumanoidHandPoseHelper helper;

    private enum HandType
    {
        Left,
        Right
    }

    private enum FingerType
    {
        Thumb,
        Index,
        Middle,
        Ring,
        Little
    }

    private enum PoseType
    {
        Open,
        Close
    }

    private HandType selectedHand = HandType.Left;
    private FingerType selectedFinger = FingerType.Index;
    private PoseType selectedPose = PoseType.Open;

    private void OnEnable()
    {
        helper = (HumanoidHandPoseHelper)target;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Individual Finger Animation Export",
            EditorStyles.boldLabel);

        selectedHand = (HandType)EditorGUILayout.EnumPopup(
            "Hand", selectedHand);

        selectedFinger = (FingerType)EditorGUILayout.EnumPopup(
            "Finger", selectedFinger);

        selectedPose = (PoseType)EditorGUILayout.EnumPopup(
            "Pose", selectedPose);

        EditorGUILayout.Space(5);

        EditorGUILayout.HelpBox(
            "現在設定されている選択指の筋肉値だけをAnimationClipに出力します。\n" +
            "Open / Closeはファイル名の区別です。実際のポーズは下のスライダーで調整してください。",
            MessageType.Info);

        if (GUILayout.Button("Export Selected Finger Animation"))
        {
            ExportSelectedFinger();
        }

        EditorGUILayout.Space(10);

        if (GUILayout.Button("Reset HandPose"))
        {
            ResetHandPose();
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Left Hand", EditorStyles.boldLabel);

        DrawPoseSliders(helper.leftHandPoseValueMap);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Right Hand", EditorStyles.boldLabel);

        DrawPoseSliders(helper.rightHandPoseValueMap);
    }

    private void DrawPoseSliders(Dictionary<string, float> poseMap)
    {
        Dictionary<string, float> updateValues =
            new Dictionary<string, float>();

        foreach (var pair in poseMap)
        {
            float value = EditorGUILayout.Slider(
                pair.Key, pair.Value, -1f, 1f);

            updateValues[pair.Key] = value;
        }

        foreach (var pair in updateValues)
        {
            poseMap[pair.Key] = pair.Value;
        }
    }

    private void ResetHandPose()
    {
        foreach (var key in helper.leftHandPoseValueMap.Keys)
        {
            helper.leftHandPoseValueMap[key] = 0f;
        }

        foreach (var key in helper.rightHandPoseValueMap.Keys)
        {
            helper.rightHandPoseValueMap[key] = 0f;
        }

        EditorUtility.SetDirty(helper);
        EditorApplication.QueuePlayerLoopUpdate();
        Repaint();
    }

    private void ExportSelectedFinger()
    {
        bool isLeft = selectedHand == HandType.Left;

        var poseMap = isLeft
            ? helper.leftHandPoseValueMap
            : helper.rightHandPoseValueMap;

        var propMap = isLeft
            ? HumanoidHandPoseHelper.TraitLeftHandPropMap
            : HumanoidHandPoseHelper.TraitRightHandPropMap;

        string handName = isLeft ? "Left" : "Right";
        string fingerName = selectedFinger.ToString();

        string musclePrefix = handName + " " + fingerName;

        AnimationClip clip = new AnimationClip
        {
            frameRate = 30
        };

        AnimationUtility.SetAnimationClipSettings(
            clip,
            new AnimationClipSettings { loopTime = true }
        );

        int exportedCount = 0;

        for (int i = 0; i < HumanTrait.MuscleCount; i++)
        {
            string muscle = HumanTrait.MuscleName[i];

            if (!muscle.StartsWith(musclePrefix + " "))
                continue;

            if (!propMap.ContainsKey(muscle))
                continue;

            if (!poseMap.ContainsKey(muscle))
                continue;

            AnimationCurve curve = new AnimationCurve();

            curve.AddKey(
                0f,
                poseMap[muscle]
            );

            string propertyName = propMap[muscle];

            clip.SetCurve(
                "",
                typeof(Animator),
                propertyName,
                curve
            );

            exportedCount++;
        }

        if (exportedCount == 0)
        {
            DestroyImmediate(clip);

            EditorUtility.DisplayDialog(
                "Export Failed",
                "選択した指の筋肉情報が見つかりませんでした。",
                "OK"
            );

            return;
        }

        const string folder = "Assets/HandPoseClips";

        if (!AssetDatabase.IsValidFolder(folder))
        {
            AssetDatabase.CreateFolder("Assets", "HandPoseClips");
        }

        string fileName =
            handName + "_" +
            fingerName + "_" +
            selectedPose + ".anim";

        string path = AssetDatabase.GenerateUniqueAssetPath(
            folder + "/" + fileName
        );

        AssetDatabase.CreateAsset(clip, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Export Completed",
            "AnimationClipを出力しました。\n\n" +
            path + "\n\n" +
            "出力した筋肉カーブ数: " + exportedCount,
            "OK"
        );
    }
}
