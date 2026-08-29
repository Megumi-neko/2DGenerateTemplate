using System;
using UnityEngine;

namespace Game.BaseSystem
{
    public enum AudioVolumeType
    {
        Master,
        Bgm,
        Sfx
    }

    public readonly struct AudioVolumeChanged
    {
        public readonly AudioVolumeType Type;
        public readonly float PreviousValue;
        public readonly float Value;

        public AudioVolumeChanged(AudioVolumeType type, float previousValue, float value)
        {
            Type = type;
            PreviousValue = previousValue;
            Value = value;
        }
    }

    public readonly struct BgmChanged
    {
        public readonly AudioClip PreviousClip;
        public readonly AudioClip Clip;

        public BgmChanged(AudioClip previousClip, AudioClip clip)
        {
            PreviousClip = previousClip;
            Clip = clip;
        }
    }
}
