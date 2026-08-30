using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using VEVE.Progression;

namespace VEVE.UI
{
    public class ProgressionUI : MonoBehaviour
    {
        [Header("Profile")]
        [SerializeField] private Text callsignText;
        [SerializeField] private Text levelText;
        [SerializeField] private Slider xpBar;
        [SerializeField] private Text xpText;
        [SerializeField] private Text tierText;
        [SerializeField] private Image tierIcon;

        [Header("Stats")]
        [SerializeField] private Text missionsCompletedText;
        [SerializeField] private Text killsText;
        [SerializeField] private Text deathsText;
        [SerializeField] private Text accuracyText;
        [SerializeField] private Text timePlayedText;
        [SerializeField] private Text kdRatioText;

        [Header("Unlock Tree")]
        [SerializeField] private Transform unlockTreeContainer;
        [SerializeField] private GameObject unlockNodePrefab;
        [SerializeField] private ScrollRect unlockTreeScrollRect;

        [Header("References")]
        [SerializeField] private UIManager uiManager;
        [SerializeField] private ProgressionManager progressionManager;

        private List<GameObject> unlockNodes = new List<GameObject>();

        private void Start()
        {
            RefreshUI();
        }

        public void RefreshUI()
        {
            if (progressionManager == null)
                return;

            PlayerProfile profile = progressionManager.GetProfile();

            if (callsignText != null)
                callsignText.text = profile.callsign ?? "OPERATOR";
            if (levelText != null)
                levelText.text = $"LEVEL {profile.level}";
            if (xpBar != null)
                xpBar.value = profile.experienceToNextLevel > 0 ? (float)profile.experience / profile.experienceToNextLevel : 0f;
            if (xpText != null)
                xpText.text = $"{profile.experience} / {profile.experienceToNextLevel} XP";
            if (tierText != null)
                tierText.text = profile.tier.ToString().ToUpperInvariant();

            if (missionsCompletedText != null)
                missionsCompletedText.text = $"Missions: {profile.missionsCompleted}";
            if (killsText != null)
                killsText.text = $"Kills: {profile.kills}";
            if (deathsText != null)
                deathsText.text = $"Deaths: {profile.deaths}";
            if (accuracyText != null)
                accuracyText.text = $"Accuracy: {profile.accuracy * 100:F1}%";
            if (timePlayedText != null)
                timePlayedText.text = $"Time: {FormatTime(profile.timePlayed)}";

            float kdRatio = profile.deaths > 0 ? (float)profile.kills / profile.deaths : profile.kills;
            if (kdRatioText != null)
                kdRatioText.text = $"K/D: {kdRatio:F2}";

            InitializeUnlockTree();
        }

        public void UnlockItem(string itemId)
        {
            if (progressionManager != null && progressionManager.UnlockItem(itemId))
            {
                RefreshUI();
            }
        }

        public void EquipItem(string itemId)
        {
            if (progressionManager != null && progressionManager.EquipItem(itemId))
            {
                RefreshUI();
            }
        }

        public void OnNodeClicked(string itemId)
        {
            UnlockableItem item = progressionManager.GetItem(itemId);
            if (string.IsNullOrEmpty(item.itemId))
                return;

            if (!item.isUnlocked)
            {
                if (progressionManager.CanUnlock(itemId))
                {
                    UnlockItem(itemId);
                }
            }
            else
            {
                EquipItem(itemId);
            }
        }

        private void InitializeUnlockTree()
        {
            if (unlockTreeContainer == null || unlockNodePrefab == null || progressionManager == null)
                return;

            foreach (GameObject node in unlockNodes)
                Destroy(node);
            unlockNodes.Clear();

            List<UnlockableItem> allItems = progressionManager.GetAvailableUnlocks();
            List<UnlockableItem> unlockedItems = progressionManager.GetUnlockedItems();

            foreach (var item in unlockedItems)
                allItems.Add(item);

            foreach (var item in allItems)
            {
                GameObject nodeObj = Instantiate(unlockNodePrefab, unlockTreeContainer);
                unlockNodes.Add(nodeObj);

                Text nodeText = nodeObj.GetComponentInChildren<Text>();
                if (nodeText != null)
                {
                    string status = item.isUnlocked ? (item.isEquipped ? "[EQUIPPED]" : "[UNLOCKED]") : "[LOCKED]";
                    nodeText.text = $"{item.displayName}\n{status}\nLvl {item.requiredLevel}";
                }

                Image nodeImage = nodeObj.GetComponent<Image>();
                if (nodeImage != null)
                {
                    if (item.isUnlocked)
                        nodeImage.color = Color.green;
                    else if (progressionManager.CanUnlock(item.itemId))
                        nodeImage.color = Color.yellow;
                    else
                        nodeImage.color = Color.gray;
                }

                Button nodeButton = nodeObj.GetComponent<Button>();
                if (nodeButton != null)
                    nodeButton.onClick.AddListener(() => OnNodeClicked(item.itemId));

                if (!string.IsNullOrEmpty(item.parentItemId))
                {
                    GameObject parentNode = unlockNodes.Find(n => n.GetComponentInChildren<Text>().text.Contains(progressionManager.GetItem(item.parentItemId).displayName));
                    if (parentNode != null)
                    {
                        Vector3 parentPos = parentNode.transform.localPosition;
                        nodeObj.transform.localPosition = new Vector3(parentPos.x + 200f, parentPos.y - 100f, 0f);
                    }
                }
            }
        }

        private string FormatTime(float seconds)
        {
            int hours = Mathf.FloorToInt(seconds / 3600f);
            int minutes = Mathf.FloorToInt((seconds % 3600f) / 60f);
            return $"{hours:D2}:{minutes:D2}";
        }
    }
}
