using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace VEVE.UI
{
    public class HUDController : MonoBehaviour
    {
        [Header("Health")]
        [SerializeField] private Text healthText;
        [SerializeField] private Slider healthSlider;
        [SerializeField] private Image healthBarFill;

        [Header("Ammo")]
        [SerializeField] private Text ammoText;
        [SerializeField] private Text weaponNameText;
        [SerializeField] private Text fireModeText;

        [Header("Damage")]
        [SerializeField] private Image damageIndicator;
        [SerializeField] private float damageIndicatorDuration = 0.5f;
        [SerializeField] private RectTransform damageDirectionContainer;
        [SerializeField] private Image[] damageDirectionIndicators;
        [SerializeField] private float damageDirectionDistance = 150f;

        [Header("Kill Feed")]
        [SerializeField] private Text killFeedText;
        [SerializeField] private int maxKillFeedEntries = 5;

        [Header("Minimap")]
        [SerializeField] private GameObject minimap;
        [SerializeField] private Text compassText;

        [Header("Status")]
        [SerializeField] private Text postureText;
        [SerializeField] private Text stanceText;
        [SerializeField] private Image staminaBar;
        [SerializeField] private Text staminaText;

        private float currentHealth;
        private float maxHealth = 100f;
        private int currentAmmo;
        private int maxAmmo = 30;
        private float damageIndicatorTimer;
        private List<string> killFeedEntries = new List<string>();
        private ColorblindMode colorblindMode = ColorblindMode.None;

        private void Update()
        {
            if (damageIndicatorTimer > 0)
            {
                damageIndicatorTimer -= Time.deltaTime;
                float alpha = damageIndicatorTimer / damageIndicatorDuration;
                damageIndicator.color = new Color(1f, 0f, 0f, alpha);
            }
        }

        public void UpdateHealth(float health)
        {
            currentHealth = Mathf.Clamp(health, 0f, maxHealth);
            if (healthText != null)
                healthText.text = $"HEALTH: {Mathf.CeilToInt(currentHealth)}";
            if (healthSlider != null)
                healthSlider.value = currentHealth / maxHealth;
        }

        public void UpdateMaxHealth(float maxHealth)
        {
            this.maxHealth = maxHealth;
            UpdateHealth(currentHealth);
        }

        public void UpdateAmmo(int ammo, int reserve)
        {
            currentAmmo = ammo;
            if (ammoText != null)
                ammoText.text = $"{ammo} / {reserve}";
        }

        public void UpdateWeaponName(string name)
        {
            if (weaponNameText != null)
                weaponNameText.text = name;
        }

        public void UpdateFireMode(string fireMode)
        {
            if (fireModeText != null)
                fireModeText.text = fireMode;
        }

        public void ShowDamageIndicator(Vector3 direction)
        {
            damageIndicatorTimer = damageIndicatorDuration;
            damageIndicator.color = new Color(1f, 0f, 0f, 1f);

            if (damageDirectionContainer != null && damageDirectionIndicators != null && damageDirectionIndicators.Length > 0)
            {
                Vector3 localDirection = transform.InverseTransformDirection(direction).normalized;
                float angle = Mathf.Atan2(localDirection.x, localDirection.z) * Mathf.Rad2Deg;
                int indicatorIndex = Mathf.RoundToInt(angle / (360f / damageDirectionIndicators.Length)) % damageDirectionIndicators.Length;
                ShowDamageDirectionIndicator(indicatorIndex);
            }
        }

        private void ShowDamageDirectionIndicator(int index)
        {
            if (damageDirectionIndicators == null || index < 0 || index >= damageDirectionIndicators.Length)
                return;

            for (int i = 0; i < damageDirectionIndicators.Length; i++)
            {
                if (damageDirectionIndicators[i] != null)
                {
                    damageDirectionIndicators[i].gameObject.SetActive(i == index);
                    if (i == index)
                        damageDirectionIndicators[i].color = new Color(1f, 0f, 0f, 1f);
                }
            }
        }

        public void AddKillFeed(string killer, string victim, string weapon)
        {
            if (killFeedText == null)
                return;

            string feedEntry = $"{killer} [{weapon}] {victim}";
            killFeedEntries.Insert(0, feedEntry);
            while (killFeedEntries.Count > maxKillFeedEntries)
                killFeedEntries.RemoveAt(killFeedEntries.Count - 1);

            killFeedText.text = string.Join("\n", killFeedEntries);
        }

        public void ToggleMinimap(bool enabled)
        {
            if (minimap != null)
                minimap.SetActive(enabled);
        }

        public void UpdateCompass(float heading)
        {
            if (compassText != null)
            {
                string[] directions = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
                int index = Mathf.RoundToInt(heading / 45f) % 8;
                compassText.text = directions[index];
            }
        }

        public void UpdatePosture(string posture)
        {
            if (postureText != null)
                postureText.text = $"POSTURE: {posture.ToUpperInvariant()}";
        }

        public void UpdateStance(string stance)
        {
            if (stanceText != null)
                stanceText.text = $"STANCE: {stance.ToUpperInvariant()}";
        }

        public void UpdateStamina(float current, float max)
        {
            float ratio = Mathf.Clamp01(current / max);
            if (staminaBar != null)
                staminaBar.fillAmount = ratio;
            if (staminaText != null)
                staminaText.text = $"STAMINA: {Mathf.CeilToInt(current)}";
        }

        public void SetColorblindMode(ColorblindMode mode)
        {
            colorblindMode = mode;
            ApplyColorblindMode();
        }

        private void ApplyColorblindMode()
        {
            switch (colorblindMode)
            {
                case ColorblindMode.Protanopia:
                    ApplyColorblindPalette(new Color(0.566f, 0.616f, 0.0f), new Color(0.627f, 0.325f, 0.196f), new Color(0.835f, 0.369f, 0.0f));
                    break;
                case ColorblindMode.Deuteranopia:
                    ApplyColorblindPalette(new Color(0.627f, 0.537f, 0.0f), new Color(0.772f, 0.439f, 0.0f), new Color(0.867f, 0.627f, 0.0f));
                    break;
                case ColorblindMode.Tritanopia:
                    ApplyColorblindPalette(new Color(0.788f, 0.302f, 0.0f), new Color(0.0f, 0.467f, 0.439f), new Color(0.0f, 0.439f, 0.439f));
                    break;
                case ColorblindMode.Achromatopsia:
                    ApplyColorblindPalette(Color.gray, Color.gray, Color.gray);
                    break;
                default:
                    ApplyColorblindPalette(Color.red, Color.green, Color.blue);
                    break;
            }
        }

        private void ApplyColorblindPalette(Color friendly, Color hostile, Color neutral)
        {
            Image[] images = GetComponentsInChildren<Image>(true);
            foreach (Image img in images)
            {
                if (img == null) continue;
                if (img.color == Color.red || img.color == Color.red * 0.8f)
                    img.color = friendly;
                else if (img.color == Color.green)
                    img.color = hostile;
            }
        }

        public void SetUIScale(float scale)
        {
            Canvas[] canvases = GetComponentsInChildren<Canvas>(true);
            foreach (Canvas canvas in canvases)
            {
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler != null)
                {
                    scaler.scaleFactor = scale;
                }
            }
        }
    }
}
