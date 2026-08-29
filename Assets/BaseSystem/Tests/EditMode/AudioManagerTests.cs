using NUnit.Framework;
using UnityEngine;

namespace Game.BaseSystem.Tests
{
    public sealed class AudioManagerTests
    {
        private GameObject managerObject;
        private AudioManager manager;

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey(AudioPlayerPrefsKeys.MasterVolume);
            PlayerPrefs.DeleteKey(AudioPlayerPrefsKeys.BgmVolume);
            PlayerPrefs.DeleteKey(AudioPlayerPrefsKeys.SfxVolume);
            managerObject = new GameObject("Audio Manager Test");
            manager = managerObject.AddComponent<AudioManager>();
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(AudioPlayerPrefsKeys.MasterVolume);
            PlayerPrefs.DeleteKey(AudioPlayerPrefsKeys.BgmVolume);
            PlayerPrefs.DeleteKey(AudioPlayerPrefsKeys.SfxVolume);
            if (managerObject != null)
            {
                Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void NewManager_UsesDefaultVolumesAndCreatesSfxPool()
        {
            Assert.That(manager.MasterVolume, Is.EqualTo(1f));
            Assert.That(manager.BgmVolume, Is.EqualTo(1f));
            Assert.That(manager.SfxVolume, Is.EqualTo(1f));
            Assert.That(manager.SfxSourceCount, Is.EqualTo(8));
        }

        [Test]
        public void SetVolume_ClampsAndPersistsValue()
        {
            manager.SetMasterVolume(2f);
            manager.SetBgmVolume(-1f);
            manager.SetSfxVolume(0.35f);

            Assert.That(manager.MasterVolume, Is.EqualTo(1f));
            Assert.That(manager.BgmVolume, Is.EqualTo(0f));
            Assert.That(manager.SfxVolume, Is.EqualTo(0.35f).Within(0.0001f));
            Assert.That(PlayerPrefs.GetFloat(AudioPlayerPrefsKeys.SfxVolume), Is.EqualTo(0.35f).Within(0.0001f));
        }

        [Test]
        public void PlaySfx_WithNullClip_ReturnsNull()
        {
            Assert.That(manager.PlaySfx(null), Is.Null);
        }

        [Test]
        public void PlayBgm_WithNullClip_DoesNotChangeCurrentClip()
        {
            manager.PlayBgm(null);

            Assert.That(manager.CurrentBgm, Is.Null);
            Assert.That(manager.IsBgmPlaying, Is.False);
        }
    }
}
