using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.BaseSystem
{
    [AddComponentMenu("Game/Base System/Audio Manager")]
    [DisallowMultipleComponent]
    public sealed class AudioManager : MonoBehaviour
    {
        private const float DefaultVolume = 1f;
        private const int DefaultSfxSourceCount = 8;

        private static AudioManager instance;
        private readonly List<AudioSource> sfxSources = new List<AudioSource>();
        private readonly Dictionary<AudioSource, int> sfxStartOrder = new Dictionary<AudioSource, int>();
        private int nextSfxStartOrder;
        private Coroutine bgmFadeCoroutine;
        private AudioSource bgmSource;
        private AudioClip currentBgm;
        private int sfxSourceCount = DefaultSfxSourceCount;
        private bool initialized;

        public static AudioManager Instance
        {
            get
            {
                if (instance == null && Application.isPlaying)
                {
                    GameObject managerObject = new GameObject(nameof(AudioManager));
                    instance = managerObject.AddComponent<AudioManager>();
                }

                return instance;
            }
        }

        public static bool HasInstance => instance != null;
        public float MasterVolume { get; private set; }
        public float BgmVolume { get; private set; }
        public float SfxVolume { get; private set; }
        public AudioClip CurrentBgm => currentBgm;
        public bool IsBgmPlaying => bgmSource != null && bgmSource.isPlaying;
        public int SfxSourceCount => sfxSources.Count;

        public event Action<AudioVolumeChanged> VolumeChanged;
        public event Action<BgmChanged> BgmChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateOnStartup()
        {
            _ = Instance;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }

            Initialize();
        }

        private void OnDestroy()
        {
            if (instance != this)
            {
                return;
            }

            if (bgmFadeCoroutine != null)
            {
                StopCoroutine(bgmFadeCoroutine);
                bgmFadeCoroutine = null;
            }

            instance = null;
        }

        public void PlayBgm(AudioClip clip, bool loop = true, float fadeDuration = 0f)
        {
            Initialize();
            if (clip == null)
            {
                return;
            }

            if (currentBgm == clip && bgmSource.isPlaying)
            {
                return;
            }

            if (bgmFadeCoroutine != null)
            {
                StopCoroutine(bgmFadeCoroutine);
                bgmFadeCoroutine = null;
            }

            if (fadeDuration > 0f && bgmSource.isPlaying)
            {
                bgmFadeCoroutine = StartCoroutine(SwitchBgmWithFade(clip, loop, fadeDuration));
                return;
            }

            AudioClip previousClip = currentBgm;
            currentBgm = clip;
            bgmSource.clip = clip;
            bgmSource.loop = loop;
            bgmSource.volume = GetBgmOutputVolume();
            bgmSource.Play();
            PublishBgmChanged(previousClip, clip);
        }

        public void StopBgm(float fadeDuration = 0f)
        {
            Initialize();
            if (!bgmSource.isPlaying && currentBgm == null)
            {
                return;
            }

            if (bgmFadeCoroutine != null)
            {
                StopCoroutine(bgmFadeCoroutine);
                bgmFadeCoroutine = null;
            }

            if (fadeDuration > 0f && bgmSource.isPlaying)
            {
                bgmFadeCoroutine = StartCoroutine(FadeOutBgm(fadeDuration));
                return;
            }

            AudioClip previousClip = currentBgm;
            bgmSource.Stop();
            bgmSource.clip = null;
            currentBgm = null;
            PublishBgmChanged(previousClip, null);
        }

        public void PauseBgm()
        {
            Initialize();
            bgmSource.Pause();
        }

        public void ResumeBgm()
        {
            Initialize();
            if (bgmSource.clip != null)
            {
                bgmSource.UnPause();
            }
        }

        public AudioSource PlaySfx(AudioClip clip, float volumeScale = 1f, float pitch = 1f)
        {
            Initialize();
            if (clip == null)
            {
                return null;
            }

            AudioSource source = FindAvailableSfxSource();
            source.Stop();
            source.clip = clip;
            source.loop = false;
            source.pitch = Mathf.Max(0.01f, pitch);
            source.volume = GetSfxOutputVolume() * Mathf.Clamp01(volumeScale);
            source.Play();
            sfxStartOrder[source] = ++nextSfxStartOrder;
            return source;
        }

        public void SetMasterVolume(float value)
        {
            SetVolume(AudioVolumeType.Master, value);
        }

        public void SetBgmVolume(float value)
        {
            SetVolume(AudioVolumeType.Bgm, value);
        }

        public void SetSfxVolume(float value)
        {
            SetVolume(AudioVolumeType.Sfx, value);
        }

        private void Initialize()
        {
            if (initialized)
            {
                return;
            }

            MasterVolume = ReadVolume(AudioPlayerPrefsKeys.MasterVolume);
            BgmVolume = ReadVolume(AudioPlayerPrefsKeys.BgmVolume);
            SfxVolume = ReadVolume(AudioPlayerPrefsKeys.SfxVolume);

            Transform root = transform.Find("AudioSources");
            if (root == null)
            {
                GameObject rootObject = new GameObject("AudioSources");
                root = rootObject.transform;
                root.SetParent(transform, false);
            }

            GameObject bgmObject = new GameObject("BGM");
            bgmObject.transform.SetParent(root, false);
            bgmSource = bgmObject.AddComponent<AudioSource>();
            bgmSource.playOnAwake = false;
            bgmSource.loop = true;

            for (int i = 0; i < sfxSourceCount; i++)
            {
                GameObject sfxObject = new GameObject($"SFX_{i + 1}");
                sfxObject.transform.SetParent(root, false);
                AudioSource source = sfxObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                sfxSources.Add(source);
                sfxStartOrder[source] = 0;
            }

            initialized = true;
        }

        private AudioSource FindAvailableSfxSource()
        {
            for (int i = 0; i < sfxSources.Count; i++)
            {
                if (!sfxSources[i].isPlaying)
                {
                    return sfxSources[i];
                }
            }

            AudioSource oldestSource = sfxSources[0];
            for (int i = 1; i < sfxSources.Count; i++)
            {
                if (sfxStartOrder[sfxSources[i]] < sfxStartOrder[oldestSource])
                {
                    oldestSource = sfxSources[i];
                }
            }

            return oldestSource;
        }

        private void SetVolume(AudioVolumeType type, float value)
        {
            Initialize();
            float clampedValue = Mathf.Clamp01(value);
            float previousValue = GetVolume(type);
            if (Mathf.Approximately(previousValue, clampedValue))
            {
                return;
            }

            switch (type)
            {
                case AudioVolumeType.Master:
                    MasterVolume = clampedValue;
                    PlayerPrefs.SetFloat(AudioPlayerPrefsKeys.MasterVolume, MasterVolume);
                    break;
                case AudioVolumeType.Bgm:
                    BgmVolume = clampedValue;
                    PlayerPrefs.SetFloat(AudioPlayerPrefsKeys.BgmVolume, BgmVolume);
                    break;
                case AudioVolumeType.Sfx:
                    SfxVolume = clampedValue;
                    PlayerPrefs.SetFloat(AudioPlayerPrefsKeys.SfxVolume, SfxVolume);
                    break;
            }

            ApplyVolumes();
            PlayerPrefs.Save();
            AudioVolumeChanged evt = new AudioVolumeChanged(type, previousValue, clampedValue);
            EventBus.Instance.Publish(evt);
            VolumeChanged?.Invoke(evt);
        }

        private void ApplyVolumes()
        {
            if (bgmSource != null && bgmSource.isPlaying)
            {
                bgmSource.volume = GetBgmOutputVolume();
            }

            for (int i = 0; i < sfxSources.Count; i++)
            {
                if (sfxSources[i].isPlaying)
                {
                    sfxSources[i].volume = GetSfxOutputVolume();
                }
            }
        }

        private float GetVolume(AudioVolumeType type)
        {
            return type == AudioVolumeType.Master
                ? MasterVolume
                : type == AudioVolumeType.Bgm ? BgmVolume : SfxVolume;
        }

        private float GetBgmOutputVolume()
        {
            return MasterVolume * BgmVolume;
        }

        private float GetSfxOutputVolume()
        {
            return MasterVolume * SfxVolume;
        }

        private static float ReadVolume(string key)
        {
            return Mathf.Clamp01(PlayerPrefs.GetFloat(key, DefaultVolume));
        }

        private void PublishBgmChanged(AudioClip previousClip, AudioClip clip)
        {
            BgmChanged evt = new BgmChanged(previousClip, clip);
            EventBus.Instance.Publish(evt);
            BgmChanged?.Invoke(evt);
        }

        private IEnumerator FadeOutBgm(float duration)
        {
            float startVolume = bgmSource.volume;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                bgmSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
                yield return null;
            }

            AudioClip previousClip = currentBgm;
            bgmSource.Stop();
            bgmSource.clip = null;
            currentBgm = null;
            bgmSource.volume = GetBgmOutputVolume();
            bgmFadeCoroutine = null;
            PublishBgmChanged(previousClip, null);
        }

        private IEnumerator SwitchBgmWithFade(AudioClip clip, bool loop, float duration)
        {
            yield return FadeOutBgm(duration);
            if (this != null)
            {
                PlayBgm(clip, loop, duration);
            }
        }
    }
}
