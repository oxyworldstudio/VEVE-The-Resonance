using UnityEngine;
using UnityEngine.UI;

namespace VEVE.UI
{
    public class HUDController : MonoBehaviour
    {
        [SerializeField] private Text healthText;
        [SerializeField] private Slider healthSlider;
        [SerializeField] private Text ammoText;
        [SerializeField] private Text weaponNameText;
        [SerializeField] private Text killFeedText;
        [SerializeField] private Image damageIndicator;
        [SerializeField] private float damageIndicatorDuration = 0.5f;
        [SerializeField] private GameObject minimap;

        private float currentHealth;
        private float maxHealth = 100f;
        private int currentAmmo;
        private int maxAmmo = 30;
        private float damageIndicatorTimer;

        private void Update()
        {
            if (damageIndicatorTimer > 0)
            {
                damageIndicatorTimer -= Time.deltaTime;
                damageIndicator.color = new Color(1f, 0f, 0f, damageIndicatorTimer / damageIndicatorDuration);
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

        public void ShowDamageIndicator(Vector3 direction)
        {
            damageIndicatorTimer = damageIndicatorDuration;
            damageIndicator.color = new Color(1f, 0f, 0f, 1f);
        }

        public void AddKillFeed(string killer, string victim, string weapon)
        {
            if (killFeedText != null)
            {
                string feedEntry = $"{killer} [{weapon}] {victim}\n";
                killFeedText.text = feedEntry + killFeedText.text;
            }
        }

        public void ToggleMinimap(bool enabled)
        {
            if (minimap != null)
                minimap.SetActive(enabled);
        }
    }
}
