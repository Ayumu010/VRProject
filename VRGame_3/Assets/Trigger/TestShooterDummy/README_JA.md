# TestShooterDummy

VRProject の既存 `TriggerControl` / `TriggerSide` と ProjectileCollisionSystem を利用する、ローカルテスト用ダミーです。

## 配置先

ZIP内の `Assets/Trigger/TestDummy` と `Assets/Trigger/TestDummy.meta` を、プロジェクトの `VRGame_3/Assets/Trigger/` にコピーしてください。

## 最初の動作確認

1. Unity に戻り、スクリプトのコンパイル完了を待ちます。
2. `Assets/Trigger/TestDummy/TestShooterDummy.prefab` を `GameScene_test` に配置します。
3. プレイヤーから数メートル離した位置に置きます。
4. Prefab は既存の `AsteroidProjectileEffect.prefab` を弾Prefabとして参照しています。
5. `Target` が空の場合は `MainCamera` を自動取得します。
6. Play すると、2秒後から約1.5秒間隔で弾を発射します。

## トリガー設定

`TestShooterDummy` コンポーネントの `Trigger Slots` に要素を追加します。

- `Side`: `Main` / `Sub`（既存ルーターの定義では `Main = Right Controller`、`Sub = Left Controller`）
- `Control`: `Trigger` / `Grip` / `Primary` / `Secondary`
- `Activation`:
  - `OnPressed`: 押した瞬間に1回
  - `WhileHeld`: 押した瞬間に1回、その後 `Repeat Interval` 間隔で繰り返す
  - `OnReleased`: 離した瞬間に1回
- `Action`: 実行する ScriptableObject アクション
- `On Triggered`: 任意の UnityEvent。Inspector から他コンポーネントの public メソッドを呼び出せます

同じ Side + Control に複数の Slot を設定すると、該当入力でそれぞれ実行されます。

## コントローラー入力の割り当て

`Controller Input Bindings` に要素を追加し、`Side` と `Control` を選び、`Action` に Input System の Action Reference を設定します。

例：

- Right Controller の Trigger 入力 → `Side = Main`, `Control = Trigger`
- Right Controller の Grip 入力 → `Side = Main`, `Control = Grip`
- Left Controller の Trigger 入力 → `Side = Sub`, `Control = Trigger`
- Left Controller の Grip 入力 → `Side = Sub`, `Control = Grip`

使用中の XRI Default Input Actions に合わせて、各 InputActionProperty に該当する Action を割り当ててください。既存 `TriggerInputRouter` は変更しません。既存ルーターに設定されている Action Reference と同じものを参照させると、現在のプロジェクトの入力構成に合わせられます。実コントローラー入力だけを試す場合は `Auto Trigger Enabled` を OFF にしてください。

`Enable Actions If Needed` は初期状態では OFF です。既存 `TriggerInputRouter` など、他のコンポーネントが同じ Action を有効化する構成を優先しています。入力Actionを有効化するコンポーネントが他にない場合だけ ON にしてください。

## 自動発火の設定

`Automatic Trigger Test` は実際のコントローラー入力とは別に、選択した `Side + Control` を疑似的に押して離します。

- `Auto Trigger Enabled`: 自動入力の有効/無効
- `Auto Trigger Side / Control`: 疑似入力を送る先
- `Auto Trigger Interval`: 疑似入力間隔
- `First Auto Trigger Delay`: 初回までの待ち時間
- `Auto Trigger Hold Duration`: 疑似的に押している時間

そのため、Prefab初期状態では `Main / Trigger` の入力イベントが自動で繰り返され、デフォルトの弾発射アクションが実行されます。

## 拡張方法

### UnityEvent で追加処理

`Trigger Slots > On Triggered` に GameObject と public メソッドを登録すると、既存アクションに追加して実行できます。HP減少、SE、Animator、テストログなどの検証に利用できます。

### 新しいアクションをコードで追加

`TestDummyTriggerAction` を継承した ScriptableObject を作成し、`Execute(TestDummyContext context)` を実装します。`CreateAssetMenu` を付けると Project ウィンドウの Create メニューからアクションアセットを作れます。

`TestDummyContext` から以下を利用できます。

- `Shooter`: ダミー本体
- `OwnerRoot` / `OwnerId`: 既存 ProjectileCollisionSystem 用の所有者情報
- `Target` / `Muzzle`: 狙う Transform と発射位置
- `DefaultProjectilePrefab`: 共通の弾Prefab
- `TryGetAimDirection(out Vector3 direction)`: ターゲット方向の取得
- `SpawnProjectile(GameObject prefab, Vector3 direction)`: 既存 ProjectileController を使って弾を発射

標準の `TestDummyProjectileAction` は、弾数と水平拡散角を変更できるサンプル実装です。

## 仕様・注意

- 既存の `ProjectileController` と `ProjectileCollisionSystem` を再利用します。
- `ProjectileCollisionSystem.Initialize(ownerId, ownerRoot)` を `Launch()` より前に呼びます。
- 既存のシールドへの命中処理は、現在の `ProjectileController` 側の実装がそのまま働きます。
- ダミーはローカルテスト用です。ネットワーク同期、プレイヤーHP減少、敵AI移動は含みません。
- 実際の Unity Editor / Quest 実機でのコンパイル・動作確認は、この環境からは実施していません。
