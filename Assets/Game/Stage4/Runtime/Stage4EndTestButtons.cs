using UnityEngine;
using UnityEngine.UI;

namespace Game.Stage4
{
    [AddComponentMenu("Game/Stage 4/End Test Buttons")]
    [DisallowMultipleComponent]
    public sealed class Stage4EndTestButtons : MonoBehaviour
    {
        [SerializeField] private bool showButtons = true;
        [SerializeField] private Vector2 winButtonPosition = new Vector2(-140f, 420f);
        [SerializeField] private Vector2 overButtonPosition = new Vector2(140f, 420f);
        [SerializeField] private Vector2 buttonSize = new Vector2(220f, 64f);

        private void Start()
        {
            if (!showButtons) return;
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null) return;
            StageOutcomeController outcome = FindObjectOfType<StageOutcomeController>();
            if (outcome == null) return;
            CreateButton(canvas.transform, "Win", winButtonPosition, outcome.TestWin, new Color(0.1f, 0.5f, 0.2f, 0.9f));
            CreateButton(canvas.transform, "Over", overButtonPosition, outcome.TestOver, new Color(0.55f, 0.12f, 0.12f, 0.9f));
        }

        private void CreateButton(Transform parent, string label, Vector2 position, UnityEngine.Events.UnityAction action, Color color)
        {
            GameObject buttonObject = new GameObject("End Test " + label, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = buttonSize;
            Image image = buttonObject.GetComponent<Image>();
            image.color = color;
            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);

            GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(buttonObject.transform, false);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            Text text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = label;
            text.fontSize = 26;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
        }
    }
}
