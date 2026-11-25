
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ShieldSystemCoordinator : MonoBehaviour
{
    private WholeBodyShieldController activeWholeBodyShield;

    public bool IsWholeBodyShieldActive =>
        activeWholeBodyShield != null;

    public WholeBodyShieldController ActiveWholeBodyShield =>
        activeWholeBodyShield;

    // ==================================================
    // Toggle
    // ==================================================

    public bool ToggleWholeBodyShield(
        GameObject prefab,
        Transform playerRoot)
    {
        // 既に展開中なら解除
        if (activeWholeBodyShield != null)
        {
            DeactivateWholeBodyShield();

            return false;
        }

        if (prefab == null || playerRoot == null)
        {
            Debug.LogError(
                "ShieldSystemCoordinator: Prefab or PlayerRoot is missing.",
                this
            );

            return false;
        }

        // プレイヤーの子として生成
        GameObject instance = Instantiate(
            prefab,
            playerRoot
        );

        WholeBodyShieldController controller =
            instance.GetComponent<WholeBodyShieldController>();

        if (controller == null)
        {
            Debug.LogError(
                "WholeBodyShield Prefab requires WholeBodyShieldController on its Root.",
                instance
            );

            Destroy(instance);

            return false;
        }

        if (!controller.Initialize(
                playerRoot,
                this))
        {
            Destroy(instance);

            return false;
        }

        activeWholeBodyShield = controller;

        // 全身シールドの展開中は通常シールドをすべて解除
        DestroyNormalShields();

        Debug.Log(
            "WholeBodyShield : Activated",
            this
        );

        return true;
    }

    // ==================================================
    // Deactivate
    // ==================================================

    public void DeactivateWholeBodyShield()
    {
        WholeBodyShieldController previous =
            activeWholeBodyShield;

        if (previous == null)
            return;

        activeWholeBodyShield = null;

        Destroy(previous.gameObject);

        Debug.Log(
            "WholeBodyShield : Deactivated",
            this
        );
    }

    // ==================================================
    // Destroy Notification
    // ==================================================

    internal void NotifyWholeBodyShieldDestroyed(
        WholeBodyShieldController shield)
    {
        if (ReferenceEquals(activeWholeBodyShield, shield))
        {
            activeWholeBodyShield = null;

            Debug.Log(
                "WholeBodyShield : Instance Destroyed",
                this
            );
        }
    }

    // ==================================================
    // Destroy Normal Shields
    // ==================================================

    private void DestroyNormalShields()
    {
        // PlayerRoot以下に存在する通常シールドをすべて取得
        ShieldController[] shields =
            GetComponentsInChildren<ShieldController>(true);

        int destroyedCount = 0;

        foreach (ShieldController shield in shields)
        {
            if (shield == null)
                continue;

            // Destroyの実行完了を待たず、先に表示とColliderを無効化する
            shield.gameObject.SetActive(false);

            Destroy(shield.gameObject);

            destroyedCount++;
        }

        Debug.Log(
            $"WholeBodyShield : Removed {destroyedCount} normal shields.",
            this
        );
    }
}
