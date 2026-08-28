# 2D 通用模板技术文档

> 本文档面向后续开发者，说明当前项目的代码结构、运行时模块、程序集依赖、场景接入方式、扩展接口和测试方法。
>
> 文档对应当前仓库代码。若文档与代码不一致，应以代码和 Unity Inspector 中的实际配置为准。

## 1. 项目概览

这是一个基于 Unity 2022.3 的 2D 游戏通用模板，当前包含以下项目能力：

- 泛型事件总线：模块之间进行发布/订阅通信。
- 通用有限状态机：处理状态、状态转换和事件队列。
- GameObject 对象池：复用频繁创建和销毁的游戏对象。
- 2D 光照系统：支持圆形光源、扇形光源、光照采样和黑暗覆盖效果。
- 昼夜系统：管理五天的白天/黑夜流程，并为后续光照适配提供事件接口。
- 瞭望塔建造系统：第一阶段支持 2×2 网格瞭望塔预览、合法性检查、资源扣除和放置。
- Unity Test Framework EditMode 测试。
- DOTween、Tilemap、UGUI、TextMeshPro、Timeline 和 Unity MCP 等项目依赖。

当前项目版本文件为 Unity 2022.3.43f1c1，具体包版本以 `Packages/manifest.json` 和 `Packages/packages-lock.json` 为准。

## 2. 目录结构

```text
Assets/
├── BaseSystem/
│   ├── BaseInterface/
│   │   └── Istate.cs                  # FSM 状态接口
│   ├── EventBus.cs                     # 全局泛型事件总线
│   ├── FSM.cs                          # 通用有限状态机
│   ├── GameObjectPool.cs               # GameObject 对象池
│   └── Game.BaseSystem.asmdef          # 基础系统程序集
├── Game/
│   ├── DayNight/
│   │   ├── Runtime/
│   │   │   ├── DayNightPhase.cs       # 昼夜阶段枚举
│   │   │   ├── DayNightEvents.cs      # 昼夜事件数据
│   │   │   ├── DayNightSystem.cs      # 昼夜核心逻辑
│   │   │   └── AssemblyInfo.cs        # 测试程序集访问内部 API
│   │   ├── Tests/EditMode/
│   │   │   ├── Game.DayNight.Tests.asmdef
│   │   │   └── DayNightSystemTests.cs
│   │   └── Game.DayNight.asmdef
│   ├── UI/Runtime/
│   │   ├── StageUIController.cs       # Stage 旧版 UGUI 控制器
│   │   └── BuildInputController.cs    # 瞭望塔建造输入
│   ├── Building/
│   │   ├── Runtime/                    # 瞭望塔第一阶段建造系统
│   │   ├── Data/                       # 建筑 ScriptableObject 数据
│   │   ├── Prefabs/                    # 项目建筑 Prefab
│   │   └── Tests/EditMode/             # 建造系统测试
│   └── Lighting/
│       ├── Runtime/                   # 光照运行时模块
│       ├── Demo/                      # 光照演示场景脚本
│       ├── Editor/                    # 光照演示场景编辑器工具
│       └── Tests/EditMode/            # 光照测试
├── Plugins/
│   ├── DOTween/
│   └── Infinity Code/Ultimate Editor Enhancer/
├── Resources/
│   └── ...
└── Scenes/
    ├── Stage.unity                    # 主游戏场景，已接入昼夜系统
    └── LightingDemo.unity             # 独立光照演示场景
```

`Library/`、`Temp/`、`Logs/`、`obj/` 和自动生成的 `.csproj` 属于 Unity 或 IDE 的生成文件，不应作为功能代码修改或提交。

## 3. 程序集结构

项目使用 Assembly Definition 将功能拆分为独立程序集：

```text
Game.BaseSystem
    └── EventBus、FSM、GameObjectPool、IState

Game.DayNight
    └── DayNightSystem、DayNightEvents、DayNightPhase
    └── 依赖 Game.BaseSystem

Game.Building.Tests
    └── 依赖 Game.Building、Game.DayNight 和 Game.BaseSystem

Game.UI（当前属于 Assembly-CSharp）
    └── StageUIController、BuildInputController

Game.Lighting
    └── 光照运行时代码

Game.Lighting.Tests
    └── 依赖 Game.Lighting
```

### 为什么昼夜系统使用独立程序集

昼夜测试需要访问 `DayNightSystem` 的内部测试方法，而 Unity 的预定义 `Assembly-CSharp` 不适合作为测试程序集的稳定引用目标。因此运行时代码放入 `Game.DayNight`，测试程序集通过 Assembly Definition 引用它。

`Game.BaseSystem` 单独拆分后，昼夜系统可以继续使用现有的 `EventBus`，且不需要复制一份事件总线实现。

修改程序集定义后，应等待 Unity 完成重新编译，再运行测试或打开相关场景。

## 4. 基础系统

### 4.1 EventBus

文件：[EventBus.cs](Assets/BaseSystem/EventBus.cs)

`EventBus` 是一个全局单例事件总线，事件类型由泛型参数决定：

```csharp
EventBus.Instance.Subscribe<PlayerDamaged>(OnPlayerDamaged);
EventBus.Instance.Publish(new PlayerDamaged(10));
EventBus.Instance.UnSubscribe<PlayerDamaged>(OnPlayerDamaged);
```

事件数据可以是任意值类型或引用类型，项目中的状态事件推荐使用 `readonly struct`。

#### 订阅规范

订阅和取消订阅必须使用同一个具名方法：

```csharp
private void OnEnable()
{
    EventBus.Instance.Subscribe<DayNightStateChanged>(OnDayNightStateChanged);
}

private void OnDisable()
{
    EventBus.Instance.UnSubscribe<DayNightStateChanged>(OnDayNightStateChanged);
}

private void OnDayNightStateChanged(DayNightStateChanged state)
{
    // 响应事件
}
```

不要使用两个不同的匿名委托进行取消订阅，否则事件总线无法找到原回调，可能产生对象销毁后的残留引用。

事件总线不负责线程同步，也不负责事件持久化。当前游戏逻辑应在 Unity 主线程中使用它。

### 4.2 FSM

文件：[FSM.cs](Assets/BaseSystem/FSM.cs)

状态实现 [Istate.cs](Assets/BaseSystem/BaseInterface/Istate.cs) 中的 `IState` 接口：

```csharp
public interface IState
{
    void OnEnter();
    void OnUpdate();
    void OnExit();
}
```

基本使用方式：

```csharp
FSM fsm = new FSM();
IState idle = new IdleState();
IState move = new MoveState();

fsm.AddTransition(idle, StateEvent.EnterMove, move);
fsm.AddTransition(move, StateEvent.EnterIdle, idle);
fsm.SetInitialState(idle);

fsm.PostEvent(StateEvent.EnterMove);
fsm.Update();
```

主要 API：

- `SetInitialState(state)`：设置初始状态并调用 `OnEnter()`。
- `AddTransition(from, event, to)`：添加指定状态下的转换。
- `AddGlobalTransition(event, to)`：添加不依赖当前状态的全局转换。
- `PostEvent(event)`：将事件放入队列并处理转换。
- `Update()`：调用当前状态的更新逻辑。

FSM 适合角色行为、菜单流程、交互流程等离散状态逻辑。昼夜系统没有直接使用 FSM，因为它还需要同时管理天数、数组配置、剩余时间和不可循环的通关终态。

### 4.3 GameObjectPool

文件：[GameObjectPool.cs](Assets/BaseSystem/GameObjectPool.cs)

对象池接收一个工厂方法，并可配置获取/归还回调：

```csharp
GameObjectPool pool = new GameObjectPool(
    createFunc: () => Instantiate(effectPrefab),
    parent: effectRoot,
    onGet: obj => obj.SetActive(true),
    onReturn: obj => obj.SetActive(false),
    maxSize: 50);

pool.Prewarm(10);
GameObject effect = pool.Get();
pool.Return(effect);
pool.Dispose();
```

主要行为：

- 池为空时自动通过工厂创建对象。
- `Prewarm(count)` 预先创建缓存对象。
- `Get()` 获取对象并执行获取回调。
- `Return(obj)` 归还对象并执行归还回调。
- 超过 `maxSize` 时，多余归还对象会被销毁。
- `CountInactive` 查询池中当前可用对象数量。
- `Clear()` 或 `Dispose()` 清理缓存。

同一个对象不能重复归还。对象池的生命周期通常应由创建它的系统负责，并在场景或系统销毁时调用 `Dispose()`。

## 5. 2D 光照系统

光照系统位于 [Assets/Game/Lighting/](Assets/Game/Lighting/)，当前是独立模块，尚未和昼夜系统耦合。

### 5.1 LightEmitter2D

文件：[LightEmitter2D.cs](Assets/Game/Lighting/Runtime/LightEmitter2D.cs)

`LightEmitter2D` 是场景中的光源组件，支持两种形状：

```csharp
LightShape2D.Circle
LightShape2D.Sector
```

主要配置和属性：

| 属性 | 说明 |
| --- | --- |
| `Shape` | 圆形或扇形光源 |
| `BaseRadius` | 基础半径 |
| `SectorAngle` | 扇形角度 |
| `MinimumSectorAngle` | 扇形最小角度 |
| `Direction` | 光源方向 |
| `BaseIntensity` | 基础光照强度 |
| `BaseDamagePerSecond` | 光源造成的基础每秒伤害 |
| `MaximumFocusMultiplier` | 扇形聚焦时的最大倍率 |
| `EdgeSoftness` | 边缘衰减宽度 |
| `IsEmitting` | 是否正在发光 |
| `IsOperational` | 是否有效参与光照计算 |

光源会在 `OnEnable()` 中注册到 [IlluminationSystem.cs](Assets/Game/Lighting/Runtime/IlluminationSystem.cs)，在 `OnDisable()` 中注销。

常用操作：

```csharp
emitter.SetEmitting(false);
emitter.ToggleShape();
emitter.SetDirectionTowards(targetPosition);
emitter.SetDirectionAngle(90f);

bool inside = emitter.Contains(worldPosition);
float influence = emitter.EvaluateInfluence(worldPosition);
float intensity = emitter.EvaluateIntensity(worldPosition);
float damage = emitter.EvaluateDamagePerSecond(worldPosition);
```

组件使用 `[ExecuteAlways]`，因此在编辑器中修改属性时也会更新注册表和相关显示。

### 5.2 IlluminationSystem

文件：[IlluminationSystem.cs](Assets/Game/Lighting/Runtime/IlluminationSystem.cs)

`IlluminationSystem` 是静态光照查询中心，维护所有已注册光源。

公开查询 API：

```csharp
IlluminationSample sample = IlluminationSystem.Sample(worldPosition);
bool isLit = IlluminationSystem.IsLit(worldPosition);
float dps = IlluminationSystem.GetDamagePerSecond(worldPosition);
```

`Sample()` 会遍历所有有效光源：

1. 移除已经销毁的光源引用。
2. 忽略未激活、未发光或无有效半径/强度的光源。
3. 计算每个光源对指定位置的影响值。
4. 累加所有光源的伤害贡献。
5. 找出强度最高的光源。
6. 返回一个 `IlluminationSample`。

`IlluminationSample` 包含：

- `IsLit`：是否有光源贡献。
- `Intensity`：最强光照强度。
- `DamagePerSecond`：所有有效光源累加后的伤害。
- `SourceCount`：有效光源数量。
- `StrongestSource`：贡献最强的光源。

光照系统还提供 `SourcesChanged` 事件，用于通知光源注册、注销或配置变化。

### 5.3 LightGeometry2D

文件：[LightGeometry2D.cs](Assets/Game/Lighting/Runtime/LightGeometry2D.cs)

这是无场景状态的几何计算工具，负责：

- 圆形/扇形范围判断。
- 圆形面积和扇形面积计算。
- 扇形等面积半径计算。
- 扇形聚焦度计算。
- 径向和角度方向上的平滑衰减。
- 方向向量归一化和非法值处理。

新玩法如果只需要判断某点是否在光照范围内，应优先使用 `LightEmitter2D.Contains()` 或 `IlluminationSystem.IsLit()`，不要重复实现几何判断。

### 5.4 DarknessOverlayEffect

文件：[DarknessOverlayEffect.cs](Assets/Game/Lighting/Runtime/DarknessOverlayEffect.cs)

这是挂在 Camera 上的屏幕黑暗覆盖效果，要求目标对象具有 `Camera` 组件。

主要职责：

- 收集场景中的有效光源。
- 按相机视野相关性排序。
- 最多向 Shader 传递 32 个光源。
- 通过光源位置、范围、方向、形状和边缘软化参数绘制黑暗区域。

注意：

- Shader 渲染最多支持 `MaximumSupportedLights = 32` 个光源。
- Gameplay 光照查询仍会计算全部有效光源，不受屏幕渲染上限影响。
- `DarknessOpacity` 只控制覆盖效果，不影响 `IlluminationSystem` 的游戏逻辑采样。
- 目前昼夜系统没有直接修改这个组件。

### 5.5 光照演示场景

[LightingDemo.unity](Assets/Scenes/LightingDemo.unity) 是独立演示场景，不是主游戏流程的一部分。

[LightingDemoBootstrap.cs](Assets/Game/Lighting/Demo/LightingDemoBootstrap.cs) 会自动创建：

- 演示相机。
- 黑暗覆盖效果。
- 地面和网格。
- 光照探针。
- 主扇形光源。
- 次级圆形光源。

[LightingDemoController.cs](Assets/Game/Lighting/Demo/LightingDemoController.cs) 提供演示输入：

- 移动鼠标：控制扇形光源方向。
- 鼠标滚轮：改变扇形角度。
- `Space`：在圆形和扇形之间切换。
- `F`：锁定或解除方向跟随。

后续接入昼夜效果时，应优先新建独立的 Lighting Bridge，而不是把昼夜判断硬编码进 `LightEmitter2D` 或 `DarknessOverlayEffect`。

## 6. 昼夜系统

昼夜系统位于 [Assets/Game/DayNight/](Assets/Game/DayNight/)，当前只负责游戏流程数据，不负责 UI 和视觉效果。

### 6.1 状态定义

文件：[DayNightPhase.cs](Assets/Game/DayNight/Runtime/DayNightPhase.cs)

```csharp
public enum DayNightPhase
{
    Day,
    Night,
    Completed
}
```

`Completed` 是五天流程结束后的终态，不是正常循环阶段。

### 6.2 DayNightSystem

文件：[DayNightSystem.cs](Assets/Game/DayNight/Runtime/DayNightSystem.cs)

在 [Stage.unity](Assets/Scenes/Stage.unity) 中，场景根部已经放置 `Day Night System` 对象，并挂载 `DayNightSystem` 组件。

默认规则：

| 天数 | 黑夜时间 |
| --- | ---: |
| 第 1 天 | 60 秒 |
| 第 2 天 | 120 秒 |
| 第 3 天 | 240 秒 |
| 第 4 天 | 420 秒 |
| 第 5 天 | 660 秒 |

初始状态为第 1 天白天。

#### 运行时属性

```csharp
int CurrentDay
DayNightPhase CurrentPhase
bool IsCompleted
bool UseUnscaledTime
float NightDurationSeconds
float NightRemainingSeconds
float NightRemainingRatio
```

说明：

- `CurrentDay`：当前天数，范围为 1 到 5。
- `CurrentPhase`：当前昼夜阶段。
- `IsCompleted`：是否已进入通关终态。
- `UseUnscaledTime`：是否使用不受 `Time.timeScale` 影响的时间。
- `NightDurationSeconds`：当前天黑夜总时长。
- `NightRemainingSeconds`：当前黑夜剩余时长。
- `NightRemainingRatio`：剩余时间比例，黑夜开始时为 1，结束时为 0。

白天和通关状态下，`NightRemainingSeconds` 和 `NightRemainingRatio` 均为 0。

#### EndDay()

```csharp
bool startedNight = dayNightSystem.EndDay();
```

`EndDay()` 是外部系统结束白天的唯一入口：

- 白天调用：返回 `true`，进入黑夜并装载当前天的时长。
- 黑夜调用：返回 `false`，不会重置倒计时。
- `Completed` 调用：返回 `false`。

玩家建造、探索或其他系统不应自行修改天数和剩余时间，只应调用这个方法。

#### 时间推进

运行时 `Update()` 根据 `useUnscaledTime` 选择 `Time.deltaTime` 或 `Time.unscaledDeltaTime`，只有在 `Night` 状态下才会扣减剩余时间。

当剩余时间降到 0：

- 第 1 至第 4 天：天数加 1，进入下一天白天。
- 第 5 天：进入 `Completed`，发布完成事件并停止继续计时。

时间会被限制为不小于 0，帧间隔较大时也不会产生负倒计时。

#### 配置校验

`nightDurationsMinutes` 是 Inspector 配置数组，必须包含五个合法的正数。系统会在 `OnValidate()` 和初始化时修正：

- `null` 数组。
- 长度不是 5 的数组。
- 0 或负数。
- `NaN`。
- 正负无穷。

非法项目会回退到对应默认值 `[1, 2, 4, 7, 11]`，不会产生第六天。

### 6.3 昼夜事件

文件：[DayNightEvents.cs](Assets/Game/DayNight/Runtime/DayNightEvents.cs)

#### DayNightStateChanged

状态切换时发布，包含：

```csharp
int Day
DayNightPhase Phase
float NightDurationSeconds
float NightRemainingSeconds
float NightRemainingRatio
```

订阅示例：

```csharp
using Game.DayNight;
using UnityEngine;

public sealed class DayNightConsumer : MonoBehaviour
{
    private void OnEnable()
    {
        EventBus.Instance.Subscribe<DayNightStateChanged>(OnStateChanged);
    }

    private void OnDisable()
    {
        EventBus.Instance.UnSubscribe<DayNightStateChanged>(OnStateChanged);
    }

    private void OnStateChanged(DayNightStateChanged state)
    {
        if (state.Phase == DayNightPhase.Day)
        {
            // 白天逻辑
        }
        else if (state.Phase == DayNightPhase.Night)
        {
            // 黑夜逻辑
        }
    }
}
```

当前状态事件在初始化和阶段转换时发布，不是每帧发布。需要高频显示倒计时的系统应直接读取 `NightRemainingSeconds`，或者自行按固定频率刷新。

#### DayNightCompleted

第五天黑夜结束时发布一次：

```csharp
EventBus.Instance.Subscribe<DayNightCompleted>(OnCompleted);
```

事件包含完成天数 `Day`，可用于：

- 结算和通关流程。
- 奖励发放。
- 存档记录。
- 场景切换。
- 成就系统。

### 6.4 当前明确未实现的内容

以下功能暂时不属于昼夜系统：

- 昼夜颜色变化。
- 黑暗覆盖透明度变化。
- 天空、环境光或光源亮度变化。
- 昼夜 UI。
- 敌人行为切换。
- 音乐或环境音切换。

后续实现视觉效果时，建议新增独立组件订阅 `DayNightStateChanged`，保持 `DayNightSystem` 只管理状态和时间。

## 7. 第一阶段建造系统：瞭望塔

建造系统位于 [Assets/Game/Building/](Assets/Game/Building/)，当前只实现第一阶段的瞭望塔建造闭环。它复用 Stage 已有的 `Grid`、`Tilemap`、`ConstructPanel` 和 `StageUIController`，不依赖 Text Mesh Pro。

当前唯一建筑类型是瞭望塔，使用 [LookoutTower.asset](Assets/Game/Building/Data/LookoutTower.asset) 配置：

- 建筑 ID：`lookout_tower`。
- 占用范围：2×2 个 Grid 格子。
- 建造消耗：10 个影结晶。
- 只允许白天建造。
- 第一阶段不包含旋转、拆除、维修、升级、攻击和光照联动。

### 7.1 模块职责

- [BuildDefinition.cs](Assets/Game/Building/Runtime/BuildDefinition.cs)：以 ScriptableObject 保存建筑 ID、Prefab、占用尺寸和资源消耗。
- [BuildInstance.cs](Assets/Game/Building/Runtime/BuildInstance.cs)：表示场景中已经创建的建筑实例。
- [BuildGrid.cs](Assets/Game/Building/Runtime/BuildGrid.cs)：负责世界坐标/Grid 坐标转换和多格占用登记。
- [BuildPlacementValidator.cs](Assets/Game/Building/Runtime/BuildPlacementValidator.cs)：集中判断昼夜阶段、边界、占用和资源条件。
- [BuildSystem.cs](Assets/Game/Building/Runtime/BuildSystem.cs)：建造的唯一正式入口，负责扣资源、实例化和发布事件。
- [BuildPreview.cs](Assets/Game/Building/Runtime/BuildPreview.cs)：显示绿色/红色的瞭望塔预览，不登记正式占用。
- [CoinInventory.cs](Assets/Game/Building/Runtime/CoinInventory.cs)：管理本阶段使用的影结晶数量。
- [BuildInputController.cs](Assets/Game/UI/Runtime/BuildInputController.cs)：处理选择瞭望塔、鼠标预览、左键确认和右键/Escape 取消。

### 7.2 Stage 使用流程

Stage 场景中的 `Building System` 对象挂载了 `BuildGrid`、`CoinInventory`、`BuildSystem`、`BuildPreview` 和 `BuildInputController`。

运行时操作流程：

1. 确保当前是白天，并且影结晶不少于 10。
2. 点击现有的建造入口打开 `ConstructPanel`。
3. `BuildInputController` 会在面板中创建“瞭望塔”按钮。
4. 点击瞭望塔按钮进入建造模式。
5. 鼠标移动时，预览会吸附到 Grid，并检查 2×2 区域。
6. 绿色表示可以建造，红色表示不能建造。
7. 在绿色位置点击左键，调用 `BuildSystem.TryPlace()`。
8. 建造成功后扣除 10 个影结晶，生成 [LookoutTower.prefab](Assets/Game/Building/Prefabs/LookoutTower.prefab)，并登记四个格子。
9. 右键或 Escape 取消预览，不会生成建筑，也不会扣除资源。

`StageUIController` 仍然负责现有面板开关、阶段按钮和影结晶文本显示；建造系统不直接修改 UI 文本。

### 7.3 建造事件

[BuildEvents.cs](Assets/Game/Building/Runtime/BuildEvents.cs) 当前提供：

- `BuildPlaced`：建造成功后发布。
- `BuildPlacementFailed`：建造检查失败时发布，便于调试和后续提示。

订阅示例：

```csharp
private void OnEnable()
{
    EventBus.Instance.Subscribe<BuildPlaced>(OnBuildPlaced);
}

private void OnDisable()
{
    EventBus.Instance.UnSubscribe<BuildPlaced>(OnBuildPlaced);
}

private void OnBuildPlaced(BuildPlaced placed)
{
    Debug.Log($"建造完成：{placed.BuildingId}");
}
```

### 7.4 当前建造限制

- 使用左下角 Grid 坐标作为建筑逻辑坐标。
- 瞭望塔固定 2×2 占用。
- 不支持旋转。
- 不支持拆除，所以当前没有资源返还。
- 黑夜和 `Completed` 阶段禁止建造。
- 瞭望塔 Prefab 的根对象保持 Grid 位置和 2D 碰撞不旋转，`Visual` 子对象通过 `EnvironmentBillboard` 在 `LateUpdate()` 中朝向主相机，以保持和场景环境一致的 2.5D 立体表现。
- 建筑 Prefab 暂不包含攻击、索敌、生命值或光照等玩法逻辑。
- 现有 tower 图片资源属于第三方资源，项目只通过自己的 Prefab 引用，不应修改源文件。


## 8. 测试体系

### 8.1 测试位置

- 昼夜测试：[DayNightSystemTests.cs](Assets/Game/DayNight/Tests/EditMode/DayNightSystemTests.cs)
- 光照测试：[Assets/Game/Lighting/Tests/EditMode/](Assets/Game/Lighting/Tests/EditMode/)
- 测试程序集：`Game.DayNight.Tests`、`Game.Lighting.Tests`

在 Unity 中通过以下菜单打开测试窗口：

```text
Window > General > Test Runner
```

然后选择 EditMode 测试。

### 8.2 昼夜测试辅助 API

为了避免测试真实等待几分钟，昼夜系统提供了仅供测试程序集使用的内部方法：

```csharp
internal void InitializeForTests()
internal void AdvanceTime(float deltaSeconds)
internal void SetNightDurationsForTests(float[] durationsMinutes)
```

这些方法通过 [AssemblyInfo.cs](Assets/Game/DayNight/Runtime/AssemblyInfo.cs) 的 `InternalsVisibleTo` 暴露给 `Game.DayNight.Tests`。

示例：

```csharp
DayNightSystem system = CreateSystem();

Assert.That(system.CurrentPhase, Is.EqualTo(DayNightPhase.Day));
system.EndDay();
system.AdvanceTime(60f);

Assert.That(system.CurrentDay, Is.EqualTo(2));
Assert.That(system.CurrentPhase, Is.EqualTo(DayNightPhase.Day));
```

### 8.3 新功能测试要求

新增昼夜功能时，至少应覆盖：

- 初始状态。
- 白天主动结束。
- 各天黑夜时长。
- 黑夜中重复结束请求。
- 跨天边界。
- 第五天完成终态。
- 完成事件只发布一次。
- 非法配置和非法时间输入。
- 事件订阅与取消订阅。

新增光照功能时，应覆盖：

- 光源注册和注销。
- 光源启用/禁用。
- 圆形和扇形范围判断。
- 光照强度及伤害叠加。
- 光源销毁后的注册表清理。
- 几何边界和非法数值处理。

## 9. 场景和运行流程

### Stage 场景

`Stage.unity` 是主游戏场景，目前已经接入昼夜系统：

```text
Day Night System
└── DayNightSystem
```

场景启动后状态为第 1 天白天。Stage 中已有旧版 UGUI 控制面板和建造入口，昼夜系统本身没有单独的倒计时 UI；瞭望塔按钮由 `BuildInputController` 在 `ConstructPanel` 中创建。

### LightingDemo 场景

`LightingDemo.unity` 只用于验证光照系统，包含独立的演示初始化和 OnGUI 控制面板。不要把它当作主游戏场景，也不要为了昼夜需求直接修改它。

## 10. 后续开发建议

### 10.1 昼夜与光照对接

建议新增一个独立的组件，例如 `DayNightLightingBridge`：

```csharp
private void OnStateChanged(DayNightStateChanged state)
{
    switch (state.Phase)
    {
        case DayNightPhase.Day:
            // 设置白天参数
            break;
        case DayNightPhase.Night:
            // 设置黑夜参数
            break;
        case DayNightPhase.Completed:
            // 设置通关后的环境参数
            break;
    }
}
```

该组件可以控制 `DarknessOverlayEffect`、环境颜色、灯光强度和音频，但不应把这些依赖反向写入 `DayNightSystem`。

### 10.2 昼夜倒计时显示

后续增加 UI 时，UI 只负责显示和调用入口，不复制天数规则：

```csharp
dayText.text = $"第 {system.CurrentDay} 天";
phaseText.text = system.CurrentPhase.ToString();
countdownText.text = Format(system.NightRemainingSeconds);
```

结束白天时调用：

```csharp
system.EndDay();
```

UI 应在 `OnDisable()` 中取消事件订阅，并且不直接修改 `currentDay` 或 `nightRemainingSeconds`。

### 10.3 新增模块的原则

- 优先通过事件总线通信，避免跨系统直接持有过多引用。
- 每个运行时模块使用自己的 Assembly Definition。
- 每个订阅都必须有对应的取消订阅。
- 对 Inspector 数值做有限性、范围和空值校验。
- 把纯计算抽成无场景依赖的方法，便于 EditMode 测试。
- 不修改第三方资源或自动生成文件来解决业务问题。
- 场景引用变更后检查对应 `.meta` 文件和 GUID。
- 修改光照系统时保持 `LightingDemo.unity` 的独立可运行性。

## 11. 常见问题排查

### 脚本无法添加到 GameObject

检查：

1. Unity Console 是否有编译错误。
2. 脚本文件名是否和类名一致。
3. Assembly Definition 是否成功导入。
4. `.meta` 文件中的 GUID 是否为合法的 32 位十六进制字符串。
5. 当前场景是否已经完成资源刷新。

### 昼夜测试找不到运行时类型

检查 `Game.DayNight.Tests.asmdef` 是否引用：

```text
Game.DayNight
Game.BaseSystem
```

并确认 Unity 已完成程序集重新编译。

### 夜晚不倒计时

依次检查：

1. `DayNightSystem` 是否启用。
2. 当前阶段是否为 `Night`。
3. 是否已经进入 `Completed`。
4. `Time.timeScale` 是否为 0。
5. `useUnscaledTime` 是否符合预期。
6. Inspector 中的夜晚数组是否被修正成五个合法值。

### 光照查询结果为不亮

检查：

1. `LightEmitter2D` 是否处于激活状态。
2. `IsEmitting` 是否为 `true`。
3. 光源半径和基础强度是否大于 0。
4. 查询位置是否在光源范围内。
5. 光源是否已经注册到 `IlluminationSystem`。
6. 如果只是屏幕上没有黑暗/光照效果，另行检查 Camera 上的 `DarknessOverlayEffect` 和 Shader。

## 12. 提交前检查清单

- [ ] Unity Console 没有新增编译错误。
- [ ] 运行相关 EditMode 测试。
- [ ] 新增脚本拥有对应 `.meta` 文件。
- [ ] 场景中的脚本 GUID 与 `.meta` 文件一致。
- [ ] 没有提交 `Library/`、`Temp/`、`Logs/`、`obj/` 或自动生成的工程文件。
- [ ] 没有误修改 `LightingDemo.unity` 或第三方资源。
- [ ] 事件订阅和取消订阅成对出现。
- [ ] 新增模块不直接复制其他系统的业务规则。
- [ ] 修改昼夜系统后确认第 5 天不会进入第 6 天。
