# Candle Lighting System

这是一个独立于具体场景的 XY 平面光照系统，适用于当前项目的 2.5D 相机与 Tilemap。游戏逻辑不依赖 Unity 灯光或碰撞器，所有怪物显隐、光照伤害、建造、发现和黑暗出生判定都应通过 `IlluminationSystem` 查询。

## 组成

- `LightEmitter2D`：蜡烛光源，支持圆形和等面积扇形。
- `LightGeometry2D`：纯数学判定与面积/射程计算。
- `IlluminationSystem`：自动注册光源并聚合查询结果。
- `DarknessOverlayEffect`：Built-in Render Pipeline 下的全屏黑暗遮罩。
- `InnerCircleLight2D`：跟随蜡烛的固定圆形内圈，始终不能切换为扇形。
- `CandleFocusController`：鼠标瞄准和 F 键锁定/恢复控制。
- `Assets/Scenes/LightingDemo.unity`：独立演示场景，不加入 Build Settings。

## 固定圆形内圈

给同一个蜡烛 GameObject 添加 `InnerCircleLight2D` 后，组件会在运行时创建一个子级光源：

- 内圈形状始终是 `Circle`，不会跟随主光源切换成扇形。
- 半径等于主蜡烛基础半径乘 `Radius Multiplier`，默认是 0.5。
- 基础亮度、基础 DPS、边缘柔化和发光开关与主蜡烛同步。
- 主光源聚光时，内圈不会复制主光源的聚光倍率，因此保持稳定的近距离圆形安全区。

一般蜡烛的组件组合为：

```text
Candle
├── LightEmitter2D       # 可切换圆形/扇形的主光源
└── InnerCircleLight2D   # 固定的小圆形内圈
```

## 在蜡烛上使用

1. 给蜡烛 GameObject 添加 `LightEmitter2D`。
2. 设置 `Base Radius`、`Base Intensity` 和 `Base Damage Per Second`。
3. 圆形模式下有效半径等于基础半径。
4. 扇形模式下有效射程自动按以下公式计算，并与基础圆面积相等：

```text
range = baseRadius * sqrt(360 / sectorAngle)
```

通过代码控制：

```csharp
using Game.Lighting;
using UnityEngine;

public sealed class CandleAimExample : MonoBehaviour
{
    [SerializeField] private LightEmitter2D emitter;

    public void AimAt(Vector2 worldPosition)
    {
        emitter.Shape = LightShape2D.Sector;
        emitter.SetDirectionTowards(worldPosition);
        emitter.SectorAngle = 90f;
    }
}
```

`LightEmitter2D` 使用独立的 XY 方向，不需要旋转 Transform，因此不会与 Billboard 组件冲突。

## 查询光照

```csharp
Vector2 position = transform.position;
IlluminationSample sample = IlluminationSystem.Sample(position);

bool visible = sample.IsLit;
float strongestBrightness = sample.Intensity;
float damageThisTick = sample.DamagePerSecond * tickDeltaTime;
int overlappingLights = sample.SourceCount;
```

聚合规则：

- `Intensity` 取覆盖光源中的最强值。
- `DamagePerSecond` 对所有覆盖光源求和。
- `StrongestSource` 返回亮度贡献最大的光源。
- 光源在 `OnEnable`/`OnDisable` 自动注册和注销。

常用快捷查询：

```csharp
bool canBuild = IlluminationSystem.IsLit(buildPosition);
float lightDps = IlluminationSystem.GetDamagePerSecond(enemyPosition);
```

## 黑暗遮罩

当前项目使用 Built-in Render Pipeline。给目标 Camera 添加 `DarknessOverlayEffect` 后，组件会读取所有活动 `LightEmitter2D`，并在 `OnRenderImage` 中绘制圆形/扇形光照区域。

- `Gameplay Plane Z` 应设置为地图所在平面的 z，当前项目通常为 0。
- 同时支持透视和正交相机。
- Shader 最多渲染 32 个最相关光源；逻辑查询不会受到这个上限影响。
- 遮罩 Shader 位于模块自己的 `Resources/Lighting` 下，可被 Player Build 自动包含；也可以在组件上显式指定其他 Shader。
- 这是 Built-in 专用 Image Effect。未来迁移 URP 时应替换视觉层，但可以保留全部逻辑 API。

## 演示场景

通过菜单 `Tools > Game Lighting > Create Demo Scene` 可重新生成演示场景。进入 Play Mode 后：

- 移动鼠标：改变扇形方向；锁定后不会再改变。
- 滚轮：改变扇形角度。
- `Space`：切换圆形/扇形。
- `F`：锁定或恢复鼠标跟随。
- 演示中的其他光源始终保持点亮。

演示场景及其运行时生成内容不引用 `Stage.unity` 或 Stage 中的资源。

## Stage 1 安装

通过菜单 `Tools > Game Lighting > Install Stage 1 Lighting` 可将光照系统幂等安装到 `Assets/Scenes/Stage 1.unity`。安装器以 Additive 方式处理 Stage 1，不会保存或修改 `Stage.unity`。

Stage 1 运行时会生成中央蜡烛、主光源和固定圆形内圈，并给 Main Camera 添加黑暗遮罩：

- 鼠标：控制主光源扇形方向。
- `F`：锁定或恢复鼠标跟随；锁定后方向保持不变。
- 滚轮：调整主光源扇形角度。
- `Space`：切换主光源圆形/扇形。
- 内圈始终保持较小的圆形，主光源变成扇形时也不会改变形状。
- 其他已注册且处于发光状态的光源不会被演示输入关闭。

Stage 1 当前的中央蜡烛视觉使用 `Assets/Resources/PowerTexture/09416f3344d521839bd708038ebc7229.png`。这是一个带透明背景的单 Sprite，已包含蜡烛主体和蜡芯，不包含独立火焰，因此只配置 `StageLightingBootstrap` 的 `Candle Sprite`，并保持 `Flame Sprite` 为空。运行时会在 `Stage 1 Central Candle/Stage 1 Central Candle Visual` 下创建一个 `SpriteRenderer`，不会再生成占位 Quad。

该 Sprite 使用中心 Pivot 和 100 PPU；Stage 1 场景当前通过 `Candle Visual Local Scale` 调整其世界尺寸。若替换为不同分辨率或不同构图的素材，应优先检查透明通道、Pivot、Pixels Per Unit 和局部缩放。视觉素材应只包含渲染组件，不要在素材上添加会与场景控制冲突的第二个 Billboard 或光照判定组件。

## 注意事项

- 当前光照不处理墙壁遮挡。
- 黑暗遮罩按 XY 游戏平面计算；显隐逻辑应使用对象脚底/锚点的 XY 坐标。
- 不要在怪物进入黑暗时关闭整个 GameObject，只关闭视觉子对象。
- 未完成的蜡烛应保持 `SetEmitting(false)`，完成后再启用，避免它用自己的光完成自身建造。
