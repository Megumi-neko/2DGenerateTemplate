# Candle Lighting System

这是一个独立于具体场景的 XY 平面光照系统，适用于当前项目的 2.5D 相机与 Tilemap。游戏逻辑不依赖 Unity 灯光或碰撞器，所有怪物显隐、光照伤害、建造、发现和黑暗出生判定都应通过 `IlluminationSystem` 查询。

## 组成

- `LightEmitter2D`：蜡烛光源，支持圆形和等面积扇形。
- `LightGeometry2D`：纯数学判定与面积/射程计算。
- `IlluminationSystem`：自动注册光源并聚合查询结果。
- `DarknessOverlayEffect`：Built-in Render Pipeline 下的全屏黑暗遮罩。
- `Assets/Scenes/LightingDemo.unity`：独立演示场景，不加入 Build Settings。

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

- 移动鼠标：改变扇形方向。
- 滚轮：改变扇形角度。
- `Space`：切换圆形/扇形。
- `L`：启用/关闭第二个光源。
- `R`：重置主光源。

演示场景及其运行时生成内容不引用 `Stage.unity` 或 Stage 中的资源。

## 注意事项

- 当前光照不处理墙壁遮挡。
- 黑暗遮罩按 XY 游戏平面计算；显隐逻辑应使用对象脚底/锚点的 XY 坐标。
- 不要在怪物进入黑暗时关闭整个 GameObject，只关闭视觉子对象。
- 未完成的蜡烛应保持 `SetEmitting(false)`，完成后再启用，避免它用自己的光完成自身建造。
