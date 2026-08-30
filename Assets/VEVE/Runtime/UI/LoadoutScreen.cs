using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using VEVE.Customization;
using VEVE.Progression;

namespace VEVE.UI
{
    public class LoadoutScreen : MonoBehaviour
    {
        [Header("Operator Selection")]
        [SerializeField] private Dropdown operatorDropdown;
        [SerializeField] private Image operatorPortrait;
        [SerializeField] private Text operatorNameText;
        [SerializeField] private Text operatorDescriptionText;
        [SerializeField] private Text operatorStatsText;

        [Header("Weapon Customization")]
        [SerializeField] private Dropdown weaponDropdown;
        [SerializeField] private Text weaponNameText;
        [SerializeField] private Transform attachmentSlotContainer;
        [SerializeField] private GameObject attachmentSlotPrefab;
        [SerializeField] private Transform availableAttachmentsContainer;
        [SerializeField] private GameObject availableAttachmentPrefab;

        [Header("Gear Loadout")]
        [SerializeField] private ToggleGroup gearToggleGroup;
        [SerializeField] private GameObject gearItemPrefab;

        [Header("Loadout Stats")]
        [SerializeField] private Text totalWeightText;
        [SerializeField] private Text totalAccuracyText;
        [SerializeField] private Text totalRecoilText;
        [SerializeField] private Text totalErgonomicsText;

        [Header("References")]
        [SerializeField] private UIManager uiManager;
        [SerializeField] private ProgressionManager progressionManager;
        [SerializeField] private WeaponCustomizationManager customizationManager;

        private string selectedWeaponId;
        private string selectedOperatorId;
        private List<AttachmentDefinition> availableAttachments = new List<AttachmentDefinition>();

        private void Start()
        {
            InitializeOperatorDropdown();
            InitializeWeaponDropdown();
            InitializeGearLoadout();
            UpdateLoadoutStats();
        }

        public void OnOperatorSelected(int index)
        {
            if (operatorDropdown == null || progressionManager == null)
                return;

            List<UnlockableItem> operators = progressionManager.GetUnlockedItems();
            operators.RemoveAll(o => o.type != UnlockableType.Operator);

            if (index >= 0 && index < operators.Count)
            {
                selectedOperatorId = operators[index].itemId;
                UnlockableItem op = progressionManager.GetItem(selectedOperatorId);
                if (operatorNameText != null)
                    operatorNameText.text = op.displayName;
                if (operatorDescriptionText != null)
                    operatorDescriptionText.text = op.description;
                if (operatorStatsText != null)
                    operatorStatsText.text = $"Level Required: {op.requiredLevel}\nType: Operator";
            }
        }

        public void OnWeaponSelected(int index)
        {
            if (weaponDropdown == null || customizationManager == null)
                return;

            List<UnlockableItem> weapons = progressionManager.GetUnlockedItems();
            weapons.RemoveAll(w => w.type != UnlockableType.Weapon);

            if (index >= 0 && index < weapons.Count)
            {
                selectedWeaponId = weapons[index].itemId;
                UnlockableItem weapon = progressionManager.GetItem(selectedWeaponId);
                if (weaponNameText != null)
                    weaponNameText.text = weapon.displayName;

                InitializeAttachmentSlots();
                LoadAvailableAttachments();
                UpdateLoadoutStats();
            }
        }

        public void OnAttachmentSlotSelected(string slotType)
        {
            LoadAvailableAttachmentsForSlot(slotType);
        }

        public void OnAttachmentSelected(string attachmentId)
        {
            if (customizationManager == null || string.IsNullOrEmpty(selectedWeaponId))
                return;

            customizationManager.Attach(selectedWeaponId, attachmentId);
            InitializeAttachmentSlots();
            UpdateLoadoutStats();
        }

        public void OnAttachmentDetach(string slotType)
        {
            if (customizationManager == null || string.IsNullOrEmpty(selectedWeaponId))
                return;

            if (System.Enum.TryParse<AttachmentSlot>(slotType, out AttachmentSlot slot))
            {
                customizationManager.Detach(selectedWeaponId, slot);
                InitializeAttachmentSlots();
                UpdateLoadoutStats();
            }
        }

        public void OnGearSelected(string gearId)
        {
            if (progressionManager == null)
                return;

            progressionManager.EquipItem(gearId);
            UpdateLoadoutStats();
        }

        public void SaveLoadout()
        {
            if (progressionManager == null)
                return;

            PlayerPrefs.SetString("VEVE_SelectedOperator", selectedOperatorId ?? "");
            PlayerPrefs.SetString("VEVE_SelectedWeapon", selectedWeaponId ?? "");
            PlayerPrefs.Save();
        }

        public void ResetLoadout()
        {
            if (customizationManager != null && !string.IsNullOrEmpty(selectedWeaponId))
            {
                WeaponCustomizationState state = customizationManager.GetState(selectedWeaponId);
                customizationManager.Detach(selectedWeaponId, AttachmentSlot.Optic);
                customizationManager.Detach(selectedWeaponId, AttachmentSlot.Muzzle);
                customizationManager.Detach(selectedWeaponId, AttachmentSlot.Grip);
                customizationManager.Detach(selectedWeaponId, AttachmentSlot.Stock);
                customizationManager.Detach(selectedWeaponId, AttachmentSlot.Magazine);
                customizationManager.Detach(selectedWeaponId, AttachmentSlot.Barrel);
                customizationManager.Detach(selectedWeaponId, AttachmentSlot.Laser);
                InitializeAttachmentSlots();
                UpdateLoadoutStats();
            }
        }

        private void InitializeOperatorDropdown()
        {
            if (operatorDropdown == null || progressionManager == null)
                return;

            List<UnlockableItem> operators = progressionManager.GetUnlockedItems();
            operators.RemoveAll(o => o.type != UnlockableType.Operator);

            operatorDropdown.ClearOptions();
            List<string> options = new List<string>();
            foreach (var op in operators)
                options.Add(op.displayName);
            operatorDropdown.AddOptions(options);

            if (PlayerPrefs.HasKey("VEVE_SelectedOperator"))
            {
                string savedOperator = PlayerPrefs.GetString("VEVE_SelectedOperator");
                for (int i = 0; i < operators.Count; i++)
                {
                    if (operators[i].itemId == savedOperator)
                    {
                        operatorDropdown.value = i;
                        OnOperatorSelected(i);
                        break;
                    }
                }
            }
        }

        private void InitializeWeaponDropdown()
        {
            if (weaponDropdown == null || progressionManager == null)
                return;

            List<UnlockableItem> weapons = progressionManager.GetUnlockedItems();
            weapons.RemoveAll(w => w.type != UnlockableType.Weapon);

            weaponDropdown.ClearOptions();
            List<string> options = new List<string>();
            foreach (var weapon in weapons)
                options.Add(weapon.displayName);
            weaponDropdown.AddOptions(options);

            if (weapons.Count > 0)
            {
                selectedWeaponId = weapons[0].itemId;
                OnWeaponSelected(0);
            }
        }

        private void InitializeAttachmentSlots()
        {
            if (attachmentSlotContainer == null || attachmentSlotPrefab == null || customizationManager == null)
                return;

            foreach (Transform child in attachmentSlotContainer)
                Destroy(child.gameObject);

            if (string.IsNullOrEmpty(selectedWeaponId))
                return;

            WeaponCustomizationState state = customizationManager.GetState(selectedWeaponId);
            AttachmentSlot[] slots = (AttachmentSlot[])System.Enum.GetValues(typeof(AttachmentSlot));

            foreach (AttachmentSlot slot in slots)
            {
                GameObject slotObj = Instantiate(attachmentSlotPrefab, attachmentSlotContainer);
                Text slotText = slotObj.GetComponentInChildren<Text>();
                if (slotText != null)
                    slotText.text = $"{slot}: Empty";

                Button slotButton = slotObj.GetComponentInChildren<Button>();
                if (slotButton != null)
                    slotButton.onClick.AddListener(() => OnAttachmentSlotSelected(slot.ToString()));
            }
        }

        private void LoadAvailableAttachments()
        {
            if (customizationManager == null || progressionManager == null || string.IsNullOrEmpty(selectedWeaponId))
                return;

            int playerLevel = progressionManager.GetProfile().level;
            availableAttachments = customizationManager.GetAvailableAttachments(playerLevel);
        }

        private void LoadAvailableAttachmentsForSlot(string slotType)
        {
            if (availableAttachmentsContainer == null || availableAttachmentPrefab == null || customizationManager == null)
                return;

            foreach (Transform child in availableAttachmentsContainer)
                Destroy(child.gameObject);

            if (string.IsNullOrEmpty(selectedWeaponId))
                return;

            if (System.Enum.TryParse<AttachmentSlot>(slotType, out AttachmentSlot slot))
            {
                List<AttachmentDefinition> slotAttachments = customizationManager.GetAttachmentsForSlot(slot, progressionManager.GetProfile().level);

                foreach (var attachment in slotAttachments)
                {
                    GameObject attachmentObj = Instantiate(availableAttachmentPrefab, availableAttachmentsContainer);
                    Text attachmentText = attachmentObj.GetComponentInChildren<Text>();
                    if (attachmentText != null)
                        attachmentText.text = $"{attachment.displayName}";

                    Button attachmentButton = attachmentObj.GetComponentInChildren<Button>();
                    if (attachmentButton != null)
                        attachmentButton.onClick.AddListener(() => OnAttachmentSelected(attachment.attachmentId));
                }
            }
        }

        private void InitializeGearLoadout()
        {
            if (gearToggleGroup == null || progressionManager == null)
                return;

            List<UnlockableItem> gear = progressionManager.GetUnlockedItems();
            gear.RemoveAll(g => g.type != UnlockableType.Gear);

            foreach (Transform child in gearToggleGroup.transform)
                Destroy(child.gameObject);

            foreach (var gearItem in gear)
            {
                GameObject gearObj = Instantiate(gearItemPrefab, gearToggleGroup.transform);
                Toggle toggle = gearObj.GetComponent<Toggle>();
                if (toggle != null)
                {
                    toggle.group = gearToggleGroup;
                    toggle.isOn = gearItem.isEquipped;
                    toggle.onValueChanged.AddListener((isOn) => { if (isOn) OnGearSelected(gearItem.itemId); });
                }

                Text gearText = gearObj.GetComponentInChildren<Text>();
                if (gearText != null)
                    gearText.text = gearItem.displayName;
            }
        }

        private void UpdateLoadoutStats()
        {
            if (customizationManager == null || string.IsNullOrEmpty(selectedWeaponId))
                return;

            float baseWeight = 3.0f;
            float totalWeight = customizationManager.CalculateTotalWeight(selectedWeaponId, baseWeight);
            float modifiedAccuracy = customizationManager.CalculateModifiedAccuracy(selectedWeaponId, 1.0f);
            float modifiedRecoil = customizationManager.CalculateModifiedRecoil(selectedWeaponId, 1.0f);
            float totalErgonomics = 1.0f;

            foreach (var attachment in availableAttachments)
            {
                if (!string.IsNullOrEmpty(GetEquippedAttachmentId(attachment.slot)))
                    totalErgonomics *= attachment.ergonomicsModifier;
            }

            if (totalWeightText != null)
                totalWeightText.text = $"Weight: {totalWeight:F1} kg";
            if (totalAccuracyText != null)
                totalAccuracyText.text = $"Accuracy: {modifiedAccuracy * 100:F0}%";
            if (totalRecoilText != null)
                totalRecoilText.text = $"Recoil: {modifiedRecoil * 100:F0}%";
            if (totalErgonomicsText != null)
                totalErgonomicsText.text = $"Ergonomics: {totalErgonomics * 100:F0}%";
        }

        private string GetEquippedAttachmentId(AttachmentSlot slot)
        {
            if (customizationManager == null || string.IsNullOrEmpty(selectedWeaponId))
                return null;

            WeaponCustomizationState state = customizationManager.GetState(selectedWeaponId);
            switch (slot)
            {
                case AttachmentSlot.Optic: return state.equippedOptic;
                case AttachmentSlot.Muzzle: return state.equippedMuzzle;
                case AttachmentSlot.Grip: return state.equippedGrip;
                case AttachmentSlot.Stock: return state.equippedStock;
                case AttachmentSlot.Magazine: return state.equippedMagazine;
                case AttachmentSlot.Barrel: return state.equippedBarrel;
                case AttachmentSlot.Laser: return state.equippedLaser;
                default: return null;
            }
        }
    }
}
