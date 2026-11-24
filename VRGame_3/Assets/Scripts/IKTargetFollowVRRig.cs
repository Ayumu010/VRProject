
using UnityEngine;

[System.Serializable]
public class VRMap
{
    public Transform vrTarget;
    public Transform ikTarget;
    public Vector3 trackingPositionOffset;
    public Vector3 trackingRotationOffset;

    public void Map()
    {
        ikTarget.position =
            vrTarget.TransformPoint(trackingPositionOffset);

        ikTarget.rotation =
            vrTarget.rotation *
            Quaternion.Euler(trackingRotationOffset);
    }
}

public class IKTargetFollowVRRig : MonoBehaviour
{
    [Header("Tracking")]
    [Range(0, 1)]
    public float turnSmoothness = 0.1f;

    public VRMap head;
    public VRMap leftHand;
    public VRMap rightHand;

    public Vector3 headBodyPositionOffset;

    [Header("Head Alignment")]
    [Tooltip("モデルの左右の目の中間に配置したEyeAnchor")]
    public Transform eyeAnchor;

    void LateUpdate()
    {
        // 1. VRトラッキングをIKターゲットに反映
        head.Map();
        leftHand.Map();
        rightHand.Map();

        // 2. PlayerModelの位置を更新
        transform.position =
            head.ikTarget.position + headBodyPositionOffset;

        // 3. PlayerModelの水平回転を更新
        float yaw = head.vrTarget.eulerAngles.y;

        transform.rotation = Quaternion.Lerp(
            transform.rotation,
            Quaternion.Euler(
                transform.eulerAngles.x,
                yaw,
                transform.eulerAngles.z
            ),
            turnSmoothness
        );

        // 4. モデルの目の位置をMain Cameraに合わせる
        if (eyeAnchor != null &&
            head.vrTarget != null &&
            eyeAnchor.IsChildOf(transform))
        {
            Vector3 positionCorrection =
                head.vrTarget.position - eyeAnchor.position;

            transform.position += positionCorrection;
        }
    }
}
