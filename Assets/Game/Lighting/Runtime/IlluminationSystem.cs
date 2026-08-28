using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Lighting
{
    public static class IlluminationSystem
    {
        internal const float ContributionThreshold = 0.0001f;

        private static readonly List<LightEmitter2D> EmittersInternal = new List<LightEmitter2D>();

        public static event Action SourcesChanged;

        public static IReadOnlyList<LightEmitter2D> RegisteredEmitters => EmittersInternal;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            EmittersInternal.Clear();
            SourcesChanged = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RebuildRegistryAfterSceneLoad()
        {
            LightEmitter2D[] sceneEmitters = UnityEngine.Object.FindObjectsOfType<LightEmitter2D>(true);
            foreach (LightEmitter2D emitter in sceneEmitters)
            {
                if (emitter != null && emitter.isActiveAndEnabled)
                {
                    Register(emitter);
                }
            }
        }

        public static IlluminationSample Sample(Vector2 worldPosition)
        {
            float strongestIntensity = 0f;
            float totalDamagePerSecond = 0f;
            int sourceCount = 0;
            LightEmitter2D strongestSource = null;

            for (int i = EmittersInternal.Count - 1; i >= 0; i--)
            {
                LightEmitter2D emitter = EmittersInternal[i];
                if (emitter == null)
                {
                    EmittersInternal.RemoveAt(i);
                    continue;
                }

                if (!emitter.IsOperational)
                {
                    continue;
                }

                float influence = emitter.EvaluateInfluence(worldPosition);
                if (influence <= ContributionThreshold)
                {
                    continue;
                }

                sourceCount++;
                float intensity = emitter.CurrentIntensity * influence;
                totalDamagePerSecond += emitter.CurrentDamagePerSecond * influence;

                if (strongestSource == null || intensity > strongestIntensity)
                {
                    strongestIntensity = intensity;
                    strongestSource = emitter;
                }
            }

            return sourceCount == 0
                ? IlluminationSample.None
                : new IlluminationSample(
                    strongestIntensity,
                    totalDamagePerSecond,
                    sourceCount,
                    strongestSource);
        }

        public static bool IsLit(Vector2 worldPosition)
        {
            return Sample(worldPosition).IsLit;
        }

        public static float GetDamagePerSecond(Vector2 worldPosition)
        {
            return Sample(worldPosition).DamagePerSecond;
        }

        internal static void Register(LightEmitter2D emitter)
        {
            if (emitter == null || EmittersInternal.Contains(emitter))
            {
                return;
            }

            EmittersInternal.Add(emitter);
            SourcesChanged?.Invoke();
        }

        internal static void Unregister(LightEmitter2D emitter)
        {
            if (emitter == null || !EmittersInternal.Remove(emitter))
            {
                return;
            }

            SourcesChanged?.Invoke();
        }

        internal static void NotifyEmitterChanged(LightEmitter2D emitter)
        {
            if (emitter != null && EmittersInternal.Contains(emitter))
            {
                SourcesChanged?.Invoke();
            }
        }

        internal static void GetOperationalEmittersNonAlloc(List<LightEmitter2D> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            results.Clear();
            for (int i = EmittersInternal.Count - 1; i >= 0; i--)
            {
                LightEmitter2D emitter = EmittersInternal[i];
                if (emitter == null)
                {
                    EmittersInternal.RemoveAt(i);
                    continue;
                }

                if (emitter.IsOperational)
                {
                    results.Add(emitter);
                }
            }
        }

        internal static void ResetForTests()
        {
            EmittersInternal.Clear();
            SourcesChanged = null;
        }
    }
}
