using System.Collections.Generic;
using Game.BaseSystem;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Combat
{
    [DisallowMultipleComponent]
    public sealed class DamageNumberManager : MonoBehaviour
    {
        private const int DefaultPoolSize = 64;
        private const float CanvasScale = 0.01f;

        [SerializeField, Min(1)] private int poolSize = DefaultPoolSize;
        [SerializeField, Min(0f)] private float worldOffsetY = 1.4f;
        [SerializeField, Min(0f)] private float horizontalJitter = 0.22f;

        private readonly List<DamageNumberPopup> pool = new List<DamageNumberPopup>();
        private readonly HashSet<Health> subscribedHealth = new HashSet<Health>();
        private readonly List<Health> activeHealth = new List<Health>();
        private readonly List<Health> unsubscribeBuffer = new List<Health>();
        private Transform popupRoot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateRuntimeInstance()
        {
            if (FindObjectOfType<DamageNumberManager>() != null)
            {
                return;
            }

            GameObject managerObject = new GameObject("Damage Number Manager");
            managerObject.AddComponent<DamageNumberManager>();
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            CreateCanvasAndPool();
        }

        private void OnDestroy()
        {
            UnsubscribeAll();
        }

        private void Update()
        {
            SyncHealthSubscriptions();
        }

        private void SyncHealthSubscriptions()
        {
            activeHealth.Clear();
            IReadOnlyList<EnemyController> enemies = EnemyController.ActiveEnemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyController enemy = enemies[i];
                Health health = enemy == null ? null : enemy.Health;
                if (health == null)
                {
                    continue;
                }

                activeHealth.Add(health);
                if (subscribedHealth.Add(health))
                {
                    health.Damaged += OnEnemyDamaged;
                }
            }

            unsubscribeBuffer.Clear();
            foreach (Health health in subscribedHealth)
            {
                if (!activeHealth.Contains(health))
                {
                    unsubscribeBuffer.Add(health);
                }
            }

            for (int i = 0; i < unsubscribeBuffer.Count; i++)
            {
                Health health = unsubscribeBuffer[i];
                health.Damaged -= OnEnemyDamaged;
                subscribedHealth.Remove(health);
                HidePopupsFor(health);
            }
        }

        private void OnEnemyDamaged(Health health, float appliedDamage)
        {
            if (health == null || appliedDamage <= 0f)
            {
                return;
            }

            DamageNumberPopup popup = GetAvailablePopup();
            if (popup == null)
            {
                return;
            }

            Vector3 offset = new Vector3(
                Random.Range(-horizontalJitter, horizontalJitter),
                worldOffsetY,
                0f);
            popup.Show(health.transform, appliedDamage, offset);
        }

        private DamageNumberPopup GetAvailablePopup()
        {
            for (int i = 0; i < pool.Count; i++)
            {
                if (!pool[i].IsPlaying)
                {
                    return pool[i];
                }
            }

            return null;
        }

        private void HidePopupsFor(Health health)
        {
            if (health == null)
            {
                return;
            }

            Transform owner = health.transform;
            for (int i = 0; i < pool.Count; i++)
            {
                DamageNumberPopup popup = pool[i];
                if (popup != null && popup.Owner == owner)
                {
                    popup.Hide();
                }
            }
        }

        private void CreateCanvasAndPool()
        {
            GameObject canvasObject = new GameObject("Damage Number Canvas");
            canvasObject.transform.SetParent(transform, false);
            canvasObject.transform.localScale = Vector3.one * CanvasScale;
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 30;
            canvasObject.AddComponent<GraphicRaycaster>();
            popupRoot = canvasObject.transform;
            Font font = GameUiFont.Load();

            for (int i = 0; i < Mathf.Max(1, poolSize); i++)
            {
                GameObject popupObject = new GameObject(
                    "Damage Number",
                    typeof(RectTransform),
                    typeof(CanvasGroup),
                    typeof(Text),
                    typeof(DamageNumberPopup));
                popupObject.transform.SetParent(popupRoot, false);
                popupObject.transform.localScale = Vector3.one;
                RectTransform rect = popupObject.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(180f, 64f);
                Text text = popupObject.GetComponent<Text>();
                text.font = font;
                text.fontSize = 48;
                text.alignment = TextAnchor.MiddleCenter;
                text.color = new Color(1f, 0.65f, 0.65f, 1f);
                text.raycastTarget = false;
                pool.Add(popupObject.GetComponent<DamageNumberPopup>());
            }
        }

        private void UnsubscribeAll()
        {
            foreach (Health health in subscribedHealth)
            {
                if (health != null)
                {
                    health.Damaged -= OnEnemyDamaged;
                }
            }

            subscribedHealth.Clear();
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i] != null)
                {
                    pool[i].Hide();
                }
            }
        }

        private void OnValidate()
        {
            poolSize = Mathf.Max(1, poolSize);
            worldOffsetY = Mathf.Max(0f, worldOffsetY);
            horizontalJitter = Mathf.Max(0f, horizontalJitter);
        }
    }
}
