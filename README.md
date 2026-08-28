# 2D 通用模板

一个面向 2D Unity 项目的通用起始模板，提供可以直接复用的基础系统与常用工具。模板适合用来快速创建原型、独立游戏和中小型 2D 项目，避免在每个新项目中重复搭建事件通信、状态切换和对象复用等基础设施。

- **技术文档**：项目代码结构、基础系统、2D 光照和昼夜系统说明请参阅 [TECHNICAL_DOCUMENTATION.md](TECHNICAL_DOCUMENTATION.md)。

## ✨ 特性

- **事件系统**：基于泛型的发布/订阅事件总线，降低模块之间的直接依赖。
- **有限状态机（FSM）**：支持普通状态转换、全局转换、事件队列以及状态生命周期回调。
- **GameObject 对象池**：支持预热、自动创建、最大容量限制、获取/归还回调和资源清理，适合 UI、特效、弹幕等高频对象。
- **DOTween**：已集成 DOTween 及常用 Unity、UI、2D Physics 扩展模块，用于补间动画和过渡效果。
- **Unity MCP**：集成 MCP for Unity，可从 VS Code、Claude Code、Cursor 等 MCP 客户端连接 Unity 编辑器，辅助场景、资源和脚本工作流。
- **Unity Editor 增强**：集成 Infinity Code Ultimate Editor Enhancer，提供层级、检视面板、场景视图、快捷操作和项目管理等编辑器增强功能。
- **2D 工作流**：启用 Unity 2D Feature，包含 Sprite、Tilemap、2D Animation、PSD Importer、SpriteShape、Pixel Perfect 和 Aseprite 等相关包。

## 🧰 环境要求

- Unity **2022.3.62f3c1**
- Windows、macOS 或 Linux（具体平台支持取决于项目中使用的 Unity 功能）
- 使用 Unity MCP 时，额外需要 Python 与 `uv/uvx`；具体要求请参阅 [Unity MCP 文档](https://github.com/CoplayDev/unity-mcp)。

> 建议使用与项目完全一致的 Unity 编辑器版本打开，以避免序列化数据或包版本发生变化。

## 🚀 开始使用

### 1. 获取项目

```bash
git clone <your-repository-url>
cd 2DGenerateTemplate
```

也可以在 GitHub 中选择 **Use this template**，基于本仓库创建新项目。

### 2. 用 Unity Hub 打开

1. 打开 Unity Hub，点击 **Add > Add project from disk**。
2. 选择项目根目录。
3. 使用 Unity **2022.3.62f3c1** 打开项目。
4. 等待 Unity 导入资源和解析 Package Manager 依赖。
5. 打开 `Assets/Scenes/SampleScene.unity` 作为起始场景。

### 3. 配置 DOTween

首次导入或升级后，在 Unity 菜单中打开：

**Tools > Demigiant > DOTween Utility Panel**

点击 **Setup DOTween...** 完成模块配置。在脚本中使用 DOTween 时引入：

```csharp
using DG.Tweening;
```

### 4. 配置 Unity MCP（可选）

1. 在 Unity 中打开 **Window > MCP for Unity**。
2. 点击 **Auto-Setup**。
3. 按提示选择客户端并完成配置。
4. 确认 Unity Bridge 处于 **Running** 状态。
5. 在 VS Code、Claude Code、Cursor 或其他 MCP 客户端中连接 Unity。

MCP 主要用于编辑器自动化和辅助开发，不是运行时游戏依赖；不使用 MCP 时可以忽略这一步。

## 📁 目录结构

```text
Assets/
├── BaseSystem/
│   ├── BaseInterface/
│   │   └── Istate.cs          # 状态接口
│   ├── EventBus.cs             # 泛型事件总线
│   ├── FSM.cs                  # 有限状态机
│   └── GameObjectPool.cs       # GameObject 对象池
├── Plugins/
│   ├── DOTween/                # DOTween 与 Unity 扩展模块
│   ├── Infinity Code/
│   │   └── Ultimate Editor Enhancer/
│   └── ...
├── Resources/
│   └── DOTweenSettings.asset
└── Scenes/
    └── SampleScene.unity
Packages/
├── manifest.json
└── packages-lock.json
ProjectSettings/
└── ProjectVersion.txt
```

## 🧱 基础系统用法

### 事件总线

事件类型可以是任意 C# 类型。订阅者应在不再需要监听时取消订阅，避免对象生命周期结束后仍然收到事件。

```csharp
public readonly struct PlayerDamaged
{
    public readonly int Amount;

    public PlayerDamaged(int amount)
    {
        Amount = amount;
    }
}

void OnEnable()
{
    EventBus.Instance.Subscribe<PlayerDamaged>(OnPlayerDamaged);
}

void OnDisable()
{
    EventBus.Instance.UnSubscribe<PlayerDamaged>(OnPlayerDamaged);
}

void OnPlayerDamaged(PlayerDamaged evt)
{
    Debug.Log($"受到伤害：{evt.Amount}");
}

// 发布事件
EventBus.Instance.Publish(new PlayerDamaged(10));
```

模板内置的 `StateEvent` 示例事件包括 `EnterIdle`、`EnterMove`、`HoverEnter`、`HoverExit` 和 `OffScreen`。实际项目中可以根据需要扩展事件定义，或直接使用自定义事件数据类型。

### 有限状态机

状态实现 `IState` 接口，并通过 `OnEnter`、`OnUpdate` 和 `OnExit` 管理状态生命周期。

```csharp
public sealed class IdleState : IState
{
    public void OnEnter() { }
    public void OnUpdate() { }
    public void OnExit() { }
}

var fsm = new FSM();
var idle = new IdleState();
var move = new MoveState();

fsm.AddTransition(idle, StateEvent.EnterMove, move);
fsm.AddTransition(move, StateEvent.EnterIdle, idle);
fsm.SetInitialState(idle);

// 事件会被立即加入并按顺序处理
fsm.PostEvent(StateEvent.EnterMove);

// 在 MonoBehaviour 的 Update 中驱动当前状态
fsm.Update();
```

`AddGlobalTransition` 可注册不依赖当前状态的全局转换，例如暂停、死亡或返回主菜单等流程。

### GameObject 对象池

对象池通过工厂方法创建对象，并使用回调处理对象取出和归还时的激活、隐藏与状态重置。

```csharp
GameObjectPool pool = new GameObjectPool(
    createFunc: () => Instantiate(effectPrefab),
    parent: effectRoot,
    onGet: obj => obj.SetActive(true),
    onReturn: obj => obj.SetActive(false),
    maxSize: 50
);

pool.Prewarm(10);

GameObject effect = pool.Get();
// 使用 effect ...
pool.Return(effect);

// 场景或系统销毁时释放缓存对象
pool.Dispose();
```

对象池特性：

- 池为空时自动创建对象。
- `Prewarm` 可在游戏开始前预先创建对象，减少运行时抖动。
- 达到 `maxSize` 后归还的对象会被销毁，而不会继续进入池中。
- `CountInactive` 可查询池中当前可用对象数量。
- `Clear`/`Dispose` 可清空并释放池中缓存对象。

## 📦 主要依赖

| 依赖 | 用途 |
| --- | --- |
| Unity 2D Feature | 2D 精灵、Tilemap、动画、PSD、SpriteShape、Pixel Perfect 等工作流 |
| [DOTween](http://dotween.demigiant.com/) | 补间动画、UI 动画和过渡效果 |
| [MCP for Unity](https://github.com/CoplayDev/unity-mcp) | Unity 编辑器与 MCP 客户端之间的桥接与自动化 |
| [Ultimate Editor Enhancer](https://assetstore.unity.com/) | Unity 编辑器工作流增强 |
| Unity Test Framework | 编辑器和运行时测试支持 |
| TextMeshPro | 高质量文本渲染 |
| Timeline | 时间轴与序列编辑 |
| Visual Scripting | 可视化脚本支持 |

MCP 通过 `Packages/manifest.json` 从 Git 仓库引入；其他 Unity 包版本以 `Packages/manifest.json` 和 `Packages/packages-lock.json` 为准。

## 🧪 测试

项目已包含 Unity Test Framework 依赖。可在 Unity 中打开：

**Window > General > Test Runner**

然后运行 EditMode 或 PlayMode 测试。新增基础系统测试时，建议覆盖事件订阅/取消订阅、状态转换、对象池回收和容量限制等行为。

## ⚠️ 注意事项

- `Library/`、`Temp/`、`Logs/`、`obj/` 等 Unity 生成目录不应提交到版本库；团队协作时请使用适用于 Unity 的 `.gitignore`。
- 不要修改或删除 `.meta` 文件，否则可能导致 Unity 资源引用丢失。
- `GameObjectPool` 只管理通过它获取并归还的对象；请避免重复归还同一个对象。
- 使用全局 `EventBus` 时要严格匹配订阅和取消订阅的委托，尤其注意匿名委托无法直接用另一个匿名委托取消。
- DOTween、Ultimate Editor Enhancer 和 MCP for Unity 各自遵循其上游项目或资源包的许可条款。发布项目时请同时检查对应的许可证文件。

## 📄 许可证

本模板的基础系统代码未在仓库中声明独立许可证。使用、修改或再发布前，请根据项目所有者的意愿补充许可证文件。

仓库中集成的第三方插件不自动继承本模板的许可证，请以各插件随附的许可证和官方条款为准。
