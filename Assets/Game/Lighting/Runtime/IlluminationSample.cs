using UnityEngine;

namespace Game.Lighting
{
    public readonly struct IlluminationSample
    {
        public static IlluminationSample None => new IlluminationSample(0f, 0f, 0, null);

        public bool IsLit => SourceCount > 0;
        public float Intensity { get; }
        public float DamagePerSecond { get; }
        public int SourceCount { get; }
        public LightEmitter2D StrongestSource { get; }

        internal IlluminationSample(
            float intensity,
            float damagePerSecond,
            int sourceCount,
            LightEmitter2D strongestSource)
        {
            Intensity = Mathf.Max(0f, intensity);
            DamagePerSecond = Mathf.Max(0f, damagePerSecond);
            SourceCount = Mathf.Max(0, sourceCount);
            StrongestSource = strongestSource;
        }
    }
}
