using System;
using UnityEngine;
using UnityEngine.InputSystem;


namespace AhMa
{
    public sealed class RescueQuest : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField] private GrandmaPursuit grandma;
        [SerializeField] private float interactionDistance = 3f;
        public bool HasAntidote { get; private set; }
        public bool HasPhoto { get; private set; }
        public bool IsCompleted { get; private set; }

        private UnityEngine.UI.Text objectives;
        private UnityEngine.UI.Text prompt;
        private UnityEngine.UI.Text feedback;
        private float feedbackUntil;
        private GameObject hud;
        private Font font;

        private void Start()
        {
            if (viewCamera == null)
                viewCamera = Camera.main;
            if (viewCamera == null || grandma == null)
            {
                Debug.LogError("救援任務尚未綁定攝影機或阿嬤。", this);
                enabled = false;
                return;
            }
            CreateHud();
        }

        private void Update()
        {
            if (IsCompleted)
                return;
            var target = FindTarget();
            var item = target == null ? null : target.GetComponentInParent<CollectibleItem>();
            var person = target == null ? null : target.GetComponentInParent<GrandmaPursuit>();
            prompt.text = item != null ? "[E] 撿取「" + item.DisplayName + "」" :
                person == grandma ? (HasAntidote && HasPhoto ? "[E] 使用解藥與照片，喚回阿嬤" : "還需要解藥與老照片") :
                "先找地上的解藥與老照片；靠近、低頭瞄準後按 E";
            if (Time.time > feedbackUntil)
                feedback.text = "";
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                TryInteract();
        }

        private Transform FindTarget()
        {
            var hits = Physics.RaycastAll(viewCamera.transform.position, viewCamera.transform.forward,
                interactionDistance, ~0, QueryTriggerInteraction.Collide);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (!hit.transform.IsChildOf(transform))
                    return hit.transform;
            }
            return null;
        }

        public bool TryInteract()
        {
            if (IsCompleted || viewCamera == null)
                return false;
            var target = FindTarget();
            if (target == null)
            {
                ShowFeedback("請靠近目標（3 公尺內），讓準星對準道具或阿嬤。");
                return false;
            }
            var item = target.GetComponentInParent<CollectibleItem>();
            if (item != null)
                return item.TryCollect(this);
            if (target.GetComponentInParent<GrandmaPursuit>() != grandma)
            {
                ShowFeedback("準星尚未對準可互動物件；找地上的解藥與老照片。");
                return false;
            }
            if (!HasAntidote || !HasPhoto)
            {
                ShowFeedback("先找到解藥和阿嬤珍藏的老照片。");
                return false;
            }
            if (!grandma.RestoreHuman())
                return false;
            IsCompleted = true;
            UpdateObjectives();
            prompt.text = "詛咒已解除，阿嬤恢復了記憶。";
            feedback.text = "阿嬤：我想起來了……我們回家吧。";
            return true;
        }

        public bool RecordItem(QuestItem item)
        {
            if (IsCompleted)
                return false;
            switch (item)
            {
                case QuestItem.Antidote:
                    if (HasAntidote) return false;
                    HasAntidote = true;
                    break;
                case QuestItem.FamilyPhoto:
                    if (HasPhoto) return false;
                    HasPhoto = true;
                    break;
                default:
                    return false;
            }
            UpdateObjectives();
            ShowFeedback(item == QuestItem.Antidote ? "已取得解藥。" : "已取得阿嬤的老照片。");
            return true;
        }

        private void ShowFeedback(string message)
        {
            if (feedback != null)
                feedback.text = message;
            feedbackUntil = Time.time + 4f;
        }

        private void UpdateObjectives()
        {
            if (objectives == null)
                return;
            objectives.text = "阿嬤，回家了\n" +
                (HasAntidote ? "✓ 解藥" : "□ 尋找解藥") + "    " +
                (HasPhoto ? "✓ 老照片" : "□ 尋找老照片") +
                (IsCompleted ? "\n任務完成" : HasAntidote && HasPhoto ? "\n找到阿嬤，按 E 喚回她的記憶" : "\n利用牆壁與大型物件遮住阿嬤的視線");
        }

        private void CreateHud()
        {
            hud = new GameObject("救援任務介面", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
            var canvas = hud.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = hud.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            font = Resources.Load<Font>("Fonts/NotoSansTC");
            objectives = CreateText("任務狀態", new Vector2(0, 1), new Vector2(24, -20), new Vector2(740, 130), TextAnchor.UpperLeft);
            prompt = CreateText("互動提示", new Vector2(0.5f, 0), new Vector2(0, 64), new Vector2(900, 50), TextAnchor.MiddleCenter);
            feedback = CreateText("任務訊息", new Vector2(0.5f, 0), new Vector2(0, 126), new Vector2(1000, 50), TextAnchor.MiddleCenter);
            CreateText("準星", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40, 40), TextAnchor.MiddleCenter).text = "+";
            UpdateObjectives();
        }

        private UnityEngine.UI.Text CreateText(string name, Vector2 anchor, Vector2 position, Vector2 size, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Text), typeof(UnityEngine.UI.Outline));
            go.transform.SetParent(hud.transform, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var text = go.GetComponent<UnityEngine.UI.Text>();
            text.font = font;
            text.fontSize = 24;
            text.alignment = alignment;
            text.color = new Color(1f, 0.94f, 0.77f);
            text.raycastTarget = false;
            go.GetComponent<UnityEngine.UI.Outline>().effectColor = Color.black;
            return text;
        }

        private void OnDestroy()
        {
            if (hud != null) Destroy(hud);
        }
    }
}

