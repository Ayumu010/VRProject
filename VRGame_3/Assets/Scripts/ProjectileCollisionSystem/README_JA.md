# ProjectileCollisionSystem（OnCollisionEnter版）

Unity 6向け。既存ProjectileControllerの通常Collider / OnCollisionEnter方式を維持する版です。

## 重要な違い

- `OnTriggerEnter` ではなく `OnCollisionEnter` を使用します。
- ProjectileのColliderは `Is Trigger = OFF` にしてください。
- 同じOwnerの弾同士は、Owner IDを比較した後に単に通知を無視するのではなく、生成・発射時に `Physics.IgnoreCollision` をペア設定します。これにより物理的な押し合いや跳ね返りも防ぎます。
- 異なるOwnerの弾同士は通常衝突し、両方のProjectileControllerへ通知します。
- 衝突後の停止・Trail消失待ち・Destroyは既存ProjectileController.StopFlight()が担当します。

## Projectile prefab

ProjectileのRootに以下を配置します。

- Rigidbody
- Collider（Is Trigger OFF）
- TrailRenderer
- ProjectileCollisionSystem
- ProjectileController（IProjectileCollisionReceiverを実装）

`ProjectileCollisionSystem` と `ProjectileController` は同じGameObjectに配置してください。

## Cube側の処理

CubeがProjectileをInstantiateしたら、Launchより前にOwner情報をセットし、その後Launchを呼びます。

```csharp
GameObject bullet = Instantiate(
    bulletPrefab,
    spawnPosition,
    spawnRotation
);

ProjectileCollisionSystem collisionSystem =
    bullet.GetComponent<ProjectileCollisionSystem>();

ProjectileController projectileController =
    bullet.GetComponent<ProjectileController>();

if (collisionSystem == null || projectileController == null)
{
    Debug.LogError("Projectile prefabに必要なコンポーネントがありません。");
    Destroy(bullet);
    return;
}

// CubeがPlayerから受け取って保持していたOwner情報を渡す。
collisionSystem.Initialize(ownerId, ownerRoot);

// Initialize後にLaunchする。
projectileController.Launch(direction, ignoredColliders);
```

`ownerId` は発射者ごとに一意のulongです。同じプレイヤーが発射した全ての弾に同じIDを渡します。
`ownerRoot` は発射者のルートTransformです。

## ProjectileControllerへの変更

同梱の `ProjectileController.cs` は、提示された既存コードをベースに以下を反映しています。

- `IProjectileCollisionReceiver` を実装
- `ProjectileCollisionSystem` をRequireComponent
- `Launch`内でOwner初期化済みか確認
- Colliderを有効にした直後に `RefreshOwnerCollisionIgnores()` を呼ぶ
- 既存の `OnCollisionEnter` を削除
- `OnProjectileCollision` から既存の `StopFlight()` を呼ぶ
- Trailの消失を待ってからDestroyする処理は維持

## 同じOwnerの弾を無視する仕組み

ProjectileCollisionSystemはOwner IDごとに有効な弾を内部登録します。
新しい弾のColliderが有効になった時点で、同じOwnerの既存弾のColliderとの全ペアに `Physics.IgnoreCollision` を設定します。

そのため、`Launch`内の `RefreshOwnerCollisionIgnores()` 呼び出しを削除しないでください。

## 注意

- `Initialize` は必ず `Launch` より先に呼んでください。
- ProjectileのColliderは通常Collider（Is Trigger OFF）です。
- ProjectileのColliderは、Projectile RootのRigidbodyに属する構成にしてください。
- 既存のProjectileControllerにあった `OnCollisionEnter` は削除します。残すと共通システムを通らずStopFlightが呼ばれます。
- `Launch` の `ignoredColliders` は維持しています。CubeやPlayerなど、既存の呼び出し側が渡していた無視Colliderも引き続き利用できます。
- Unity Editor上の実コンパイル・実機物理テストは未実施です。
