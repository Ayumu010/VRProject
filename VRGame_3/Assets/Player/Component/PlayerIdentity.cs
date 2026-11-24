using UnityEngine;

public class PlayerIdentity : MonoBehaviour
{
    [Header("Player Identity")]
    [SerializeField]
    private int debugOwnerId = 100;

    public ulong OwnerId => (ulong)Mathf.Max(0, debugOwnerId);

    public Transform RootTransform => transform;
}