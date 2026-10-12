# OPS 混淆导致构建包打不开 —— 真正根因与修复（2026-09-20 二次排查）

> 结论先行：**黑屏的根因是 OPS 把「靠字段名绑定 UI」的私有字段改名了。**
> `FUnityUI` 框架的绑定规则是：**字段名 → kebab-case → 在 View 预制体里找同名子节点**。
> OPS 把 `_gridItem` 改成 `ccf` 后，运行时去找名为 `ccf` 的节点 → 找不到 → 字段为 null → 整套 UI 绑不上 → **黑屏**。
> 而 Unity 运行时、TTSDK、场景加载全部正常，所以控制台看起来「一切正常」，极具迷惑性。

---

## 一、先排除的（这些都不是原因）

| 怀疑对象 | 实测结论 |
|---|---|
| 打包配置 / 两个包结构不同 | ❌ 逐文件 md5 对比：`game.json`、`project.config.json`、`plugin-config.js`、`unity-namespace.js`、`webgl-wasm-split.js`、图片 **全部 SAME**；只有混淆过的 `webgl.wasm` / `webgl.data` / `webgl.framework.js` / `webgl.symbols.json` 不同。**打包配置一个字没变。** |
| 开发者工具/代理环境 | ❌ 本次会话日志只有 `Error mutex already exists` + `timeout`（残留实例），与包无关。 |
| MonoBehaviour 类名被改 | ❌ **已确认没有被改**。逐个核对 26 个 MonoBehaviour（`Runtime`、`BoomGrid`、`PowerUpButton`、`HomeView` 里的 `AdManager`、`cebian`、`SpinWheel`…）`Rename=0`；元数据实测 `AutoLayoutGameView` / `MemoryGameView` / `RankingView` 在两个包里出现次数**完全一致**。 |
| ScriptableObject 类名被改 | ❌ `FTools.Config.FToolsConfigAsset` 已被正常跳过（日志：`Obfuscation of ScriptableObjects got deactivated by settings`），字段 `aesKey/aesIv/serverBaseUrl/...` 全部保留。 |
| 命名空间被改 | ❌ `Obfuscate_Namespaces=false`，本次 `Rename Namespace` **0 条**。 |
| 字符串混淆吃掉资源路径 | ❌ `Enable_String_Obfuscation=false`，`FDependence/LocalCacheConfig` 等路径字符串两个包都在。 |
| 协程 `<>1__state` 被改名 | ❌ 本案**无关**。实测工程里**没有任何** `StartCoroutine("名")` / `Invoke("名")` / `SendMessage`（`onFail?.Invoke("…")` 是 C# 委托调用，不是 Unity 字符串 API）。 |
| `[FOnClick]` 方法名被改导致按钮失效 | ❌ 39 处 `[FOnClick]` **全部带显式节点名**（`[FOnClick("open-settings-btn")]`），框架按字符串找按钮，与 C# 方法名无关。 |
| 重命名字段破坏 Newtonsoft JSON | ⚠️ 理论上存在，但本次不是致命项（公开字段已不参与混淆）。 |

---

## 二、真正的根因：字段名 = 节点名（kebab-case）

### 2.1 框架证据

`Library/Bee/artifacts/WebGL/ManagedStripped/FUnityUI.dll` 的元数据字符串：

```
FAutoBind / FAutoBinderFactory / FDynamicUIRuntimeAutoBinder / FGameObjectAutoBinder
FMonoAutoBinder / FMethodAutoBinder / FMonoFinderAttribute / FOnClickAttribute
FResourcesLoadBinder / FViewLoaderAttribute / FViewManager
CacheUIElements / CacheComponent / BindUIElements / BindUiToMono / BindSingle
CreateBinder / GetBinder / RunBinding / _binderCache
GetFields / GetCustomAttribute / FieldInfo / get_FieldType / SetValue
get_Name / get_UIName / get_UiNames / uiName / TryGetComponent / GetComponentsInChildren
```

→ 这是一个**按字段反射自动绑定 UI** 的框架：遍历 View 类的字段 → 读特性 → **用字段名/特性名去找子节点** → `SetValue` 赋值。

### 2.2 代码证据（`Assets/Scripts/View/AutoLayoutGameView.cs`）

```csharp
[FGameObject] private GameObject _gridItem;                    // ← 不带名字，靠字段名
[FGameObject] private GameObject _gameGrid;                    // ← 不带名字，靠字段名
[FGameObject("open-settings-btn")] private GameObject _openSettingsBtn;  // ← 显式名字，安全
[FuiAutowire] public RectTransform gameArea;                   // ← 不带名字，靠字段名
[FuiAutowire("Progress-Bar")] public Slider progressBar;       // ← 显式名字，安全
[FuiAutowire] private TextMeshProUGUI _currentLevel;           // ← 不带名字，靠字段名
```

### 2.3 预制体证据（`Assets/Resources/View/GameView.prefab` 的子节点名）

```
grid-item      ←  [FGameObject] private GameObject _gridItem;
game-grid      ←  [FGameObject] private GameObject _gameGrid;
current-level  ←  [FuiAutowire] private TextMeshProUGUI _currentLevel;
game-area      ←  [FuiAutowire] public RectTransform gameArea;
open-settings-btn / Progress-Bar / consume-time / score-multiplier / no-grid-tip …
```

**4 组独立对应关系完全吻合**：`_gridItem→grid-item`、`_gameGrid→game-grid`、`_currentLevel→current-level`、`gameArea→game-area`。
规则即：**去掉前导 `_`，camelCase → kebab-case，然后按名字找同名子节点。**

### 2.4 OPS 日志证据（`Assets/OPS/Obfuscator/Log/WebGL.txt`，18:38 那次构建）

```
Rename Field [[Assembly-CSharp] UnityEngine.GameObject View.AutoLayoutGameView::_gridItem]        to ccf
Rename Field [[Assembly-CSharp] UnityEngine.GameObject View.AutoLayoutGameView::_gameGrid]        to ccg
Rename Field [[Assembly-CSharp] TMPro.TextMeshProUGUI View.AutoLayoutGameView::_currentLevel]     to cci
Rename Field [[Assembly-CSharp] UnityEngine.Sprite   View.AutoLayoutGameView::_obstacleIcon]      to ccl
Rename Field [[Assembly-CSharp] UnityEngine.RectTransform View.AutoLayoutGameView::_renderRoot]   to ccr
Rename Field [[Assembly-CSharp] UnityEngine.GameObject View.AutoLayoutGameView::_colorGamePanel]  to ccm
…
```

字段名变成 `ccf`，框架就在预制体里找名为 `ccf` 的子节点 → 找不到 → `_gridItem` 等全为 `null`
→ 格子不生成、HUD 不刷新、面板不显示 → **黑屏**。

> 之前把 `Obfuscate_Field_Public` 关掉只保住了**公开**字段（`collectionRoot`、`gameArea`、`amazingIcon`…），
> 但**绝大多数绑定字段是 `_xxx` 私有字段**，正好落在 `Obfuscate_Field_Private=true` 的射程内 —— 所以上一轮修复没解决。

---

## 三、受影响范围（实测）

| 特性 | 总数 | 带显式名字（安全） | **靠字段名（会坏）** |
|---|---:|---:|---:|
| `[FGameObject]` | 34 | 20 | **14** |
| `[FuiAutowire]` | 70 | 27 | **43** |
| **合计** | 104 | 47 | **57** |

涉及脚本（共 53 处字段声明）：

```
Scripts/View/AutoLayoutGameView.cs   _gridItem _gameGrid collectionRoot collectionItem boomItem
                                     gameArea _currentLevel amazingIcon shadowImage
Scripts/View/GameSettingsView.cs     _panel _audioSlider _musicSlider _openVibrate _closeVibrate
                                     _staminaTimeText _staminaValueText _staminaIcon _restartGame
Scripts/View/GameView.cs             _gridItem _gameGrid
Scripts/View/GameWinView.cs          _currentScore _historyScore
Scripts/View/HomeView.cs             _staminaIcon _playerAvatar _rankNum _staminaTimeText
                                     _staminaValueText _particleSpawnRoot
Scripts/View/MemoryGameView.cs       _staminaRenderRoot gameArea _gridItem _readyText _staminaPrefab
Scripts/View/MemoryGameWinView.cs    _currentScore _historyScore
Scripts/View/MenuSettingsView.cs     _panel _audioSlider _musicSlider _openVibrate _closeVibrate
Scripts/View/RankingView.cs          _rankingTitle _staminaIcon _staminaTimeText _staminaValueText
                                     _rankNum _playerName _playerRankNum _playerScore
                                     _playerAvatar _topAvatar _topRankArea
Scripts/View/ShareView.cs            _panel
Scripts/View/VideoStaminaView.cs     _panel
```

---

## 四、已做的修复

文件：`Assets/OPS/Obfuscator/Settings/Obfuscator_Settings.json`
备份：`Obfuscator_Settings.json.bak-20260920-v2`（上一版在 `…bak-20260920`）

| 开关 | 改前 | 改后 |
|---|---|---|
| `Obfuscate_Fields` | True | **false** |
| `Obfuscate_Field_Internal` | True | **false** |
| `Obfuscate_Field_Private` | True | **false** |
| `Obfuscate_Field_Protected` | True | **false** |

**保留的全部强度**：`Obfuscate_Classes`（非 Unity 类）、`Obfuscate_Methods`（含 Public）、`Obfuscate_Properties`、`Obfuscate_Events`、`Obfuscate_Method_Parameter`、`Enable_Add_Random_Code`。
**为什么可以放心关字段改名**：`Obfuscate_Field_Public` 之前本来就是关的；字段名只是元数据里的可读字符串，真正的抗逆向来自 **IL2CPP（C++→wasm）+ 1104 个方法改名**。

---

## 五、下一步

1. Unity 里打开 `OPS → Obfuscator → Settings`，确认 `Field` 页签 4 个开关都是 OFF。
2. **完全退出**抖音开发者工具（确保没有残留实例，否则又是一堆 `mutex/timeout`）。
3. 重新构建到 `tt-minigame-ops`，再打开。

### 若还是不行，按此顺序**每次只动一项**二分

1. `Obfuscate_Properties` → false（93 个属性改名；虽然实测 `FUnityUI` 只 `GetFields` 不 `GetProperties`，但可作为第二嫌疑）
2. `Enable_Assembly_Suppress_ILDasm` → false（它会扰动程序集元数据，对 IL2CPP 属于高风险低收益）
3. `Enable_Add_Random_Code` → false
4. `Obfuscate_Methods` / `Obfuscate_Method_Public` → false（最后手段）

---

## 六、如果想「既要混淆字段名、又不坏 UI」的长期方案

给这 57 个字段**补上显式名字**，绑定就不再依赖字段名，字段就可以随便改：

```csharp
// 改前（OPS 一改字段名就坏）
[FGameObject] private GameObject _gridItem;
// 改后（绑定写死成字符串，字段名随便混淆）
[FGameObject("grid-item")] private GameObject _gridItem;
```

名字按同一规则推导：去 `_` + camelCase→kebab-case（`_currentLevel` → `current-level`），
并且**必须与该 View 预制体里的子节点名逐个核对**。
注意：走这条路时 `Enable_String_Obfuscation` **必须保持关闭**，否则特性里的字符串会被加密，反而更坏。

> 另外 `[FHideIf("useCustomCurve")]` / `[FShowIf("fromCurrentScale")]` / `[FormerlySerializedAs("aesIV")]`
> 这类**字符串引用字段名**的写法只有编辑器/序列化迁移用途，运行时不受影响；
> 但今后若新增同类特性，记得字段名和字符串要同步。
