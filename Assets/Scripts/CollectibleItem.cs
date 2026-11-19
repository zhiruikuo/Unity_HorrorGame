using UnityEngine;

namespace AhMa
{
    public enum QuestItem { Antidote, FamilyPhoto }

    public sealed class CollectibleItem : MonoBehaviour
    {
        [SerializeField] private QuestItem item;
        public QuestItem Item => item;
        public string DisplayName => item == QuestItem.Antidote ? "解藥" : "阿嬤的老照片";
        public bool IsCollected { get; private set; }

        public bool TryCollect(RescueQuest quest)
        {
            if (quest == null || IsCollected || !quest.RecordItem(item))
                return false;
            IsCollected = true;
            gameObject.SetActive(false);
            return true;
        }
    }
}
