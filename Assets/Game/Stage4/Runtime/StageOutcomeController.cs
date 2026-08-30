using Game.BaseSystem;
using Game.Combat;
using Game.DayNight;
using Game.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Stage4
{
    [DisallowMultipleComponent]
    public sealed class StageOutcomeController : MonoBehaviour
    {
        [SerializeField] private MainTower mainTower;
        [SerializeField] private DayNightSystem dayNightSystem;
        [SerializeField] private string endSceneName = "End";
        private bool requested;
        private bool subscribed;
        private void Awake() { if (mainTower == null) mainTower = FindObjectOfType<MainTower>(); if (dayNightSystem == null) dayNightSystem = FindObjectOfType<DayNightSystem>(); }
        private void OnEnable()
        {
            Subscribe();
        }

        private void Start()
        {
            Subscribe();
        }

        private void Subscribe()
        {
            if (subscribed) return;
            if (mainTower == null) mainTower = FindObjectOfType<MainTower>();
            if (mainTower == null || mainTower.Health == null)
            {
                return;
            }

            mainTower.Health.Died += OnTowerDied;
            EventBus.Instance.Subscribe<DayNightCompleted>(OnCompleted);
            subscribed = true;
        }

        private void OnDisable()
        {
            if (!subscribed) return;
            mainTower.Health.Died -= OnTowerDied;
            EventBus.Instance.UnSubscribe<DayNightCompleted>(OnCompleted);
            subscribed = false;
        }
        private void OnTowerDied(Health _) { Request(EndOutcome.GameOver); }
        private void OnCompleted(DayNightCompleted _) { Request(EndOutcome.Victory); }
        public void TestWin() { Request(EndOutcome.Victory); }
        public void TestOver() { Request(EndOutcome.GameOver); }

        private void Request(EndOutcome outcome)
        {
            if (requested) return;
            requested = true;
            EndSceneFlow.SetOutcome(outcome);
            if (SceneManagerSystem.HasInstance && SceneManagerSystem.Instance.LoadScene(endSceneName) == SceneLoadRequestStatus.Accepted) return;
            SceneManager.LoadScene(endSceneName);
        }
    }
}
