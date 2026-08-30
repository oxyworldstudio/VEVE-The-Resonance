using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using VEVE.AI;
using VEVE.Agentic;

namespace VEVE.UI
{
    /// <summary>
    /// Modular tactical HUD overlay assembled entirely at runtime. Coordinates with the legacy
    /// HUDController: any element it already serializes (compass text, ammo text, kill feed text,
    /// stamina bar, damage indicator) is delegated to its public API instead of duplicated.
    /// Remaining modules (compass strip, objective queue, squad pips, vitals pulse, damage arc,
    /// scroll kill feed, low-health vignette, stamina arc) are built on demand. All bindings are
    /// polled null-safe: every module degrades gracefully when its data source is absent.
    /// </summary>
    public sealed class AdvancedHUDLayout : MonoBehaviour
    {
        public static class Features
        {
            public const string Compass = "compass";
            public const string Objectives = "objectives";
            public const string Squad = "squad";
            public const string Ammo = "ammo";
            public const string Vitals = "vitals";
            public const string Damage = "damage";
            public const string KillFeed = "killfeed";
            public const string Vignette = "vignette";
            public const string Stamina = "stamina";
        }

        private static readonly string[] AllFeatureNames =
        {
            Features.Compass, Features.Objectives, Features.Squad, Features.Ammo,
            Features.Vitals, Features.Damage, Features.KillFeed, Features.Vignette, Features.Stamina
        };

        private static readonly string[] CardinalLabels = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
        private const float DegreesPerTick = 5f;
        private const float PixelsPerDegree = 6f;
        private const int CompassTicks = 25;
        private const int MaxKillEntries = 8;
        private const int MaxObjectiveRows = 5;
        private const int MaxSquadPips = 6;

        private HUDController legacyHud;
        private Physiology physiology;
        private Weapon weapon;
        private SquadManager squad;
        private MultiAgentSystemManager agentManager;

        private readonly Dictionary<string, GameObject> featureObjects = new Dictionary<string, GameObject>();
        private readonly HashSet<string> nativeHandled = new HashSet<string>();
        private readonly List<ObjectiveEntry> objectiveQueue = new List<ObjectiveEntry>();
        private readonly List<string> killEntries = new List<string>();

        private readonly List<Image> compassTicks = new List<Image>();
        private readonly List<Text> compassLabels = new List<Text>();
        private readonly List<Text> objectiveRows = new List<Text>();
        private readonly List<Image> squadPips = new List<Image>();
        private readonly List<Image> squadPipFills = new List<Image>();

        private Canvas canvas;
        private Text squadText;
        private Text ammoText;
        private Text ammoNameText;
        private Text malfunctionText;
        private Image heartDot;
        private Text heartRateText;
        private Text bloodText;
        private Text respirationText;
        private Image consciousnessArc;
        private Image damageArc;
        private Image staminaArc;
        private Image vignette;
        private RectTransform killFeedContent;
        private bool ownsCompass;

        private float referenceRefresh;
        private float bindRefresh;
        private float pulsePhase;
        private float damageTimer;
        private float damageAngle;
        private float externalHealth01 = 1f;
        private float staminaValue = 1f;

        private struct ObjectiveEntry
        {
            public string text;
            public bool complete;
        }

        private void Awake()
        {
            legacyHud = FindLegacyHud();
            canvas = UiFactory.CreateCanvas("AdvancedHUD", 40);

            BuildCompass();
            BuildObjectives();
            BuildSquad();
            BuildAmmo();
            BuildVitals();
            BuildDamage();
            BuildKillFeed();
            BuildVignette();
            BuildStamina();
        }

        // ------------------------------------------------------------------- public API

        /// <summary>Toggle a HUD module by key (case-insensitive). Unknown keys return false.</summary>
        public bool EnableFeature(string feature)
        {
            return SetFeature(feature, true);
        }

        public bool DisableFeature(string feature)
        {
            return SetFeature(feature, false);
        }

        public IReadOnlyList<string> FeatureNames => AllFeatureNames;

        /// <summary>True when a module is absent because HUDController already owns that element.</summary>
        public bool IsHandledByLegacyHud(string feature)
        {
            return feature != null && nativeHandled.Contains(feature.Trim().ToLowerInvariant());
        }

        /// <summary>Inject player health for the vignette when no Physiology component is bound.</summary>
        public void SetExternalHealth01(float value)
        {
            externalHealth01 = Mathf.Clamp01(value);
        }

        /// <summary>
        /// Feed stamina. Delegates to HUDController.UpdateStamina when it owns the stamina bar,
        /// otherwise drives the local arc.
        /// </summary>
        public void SetStamina(float current, float max)
        {
            if (max <= 0f)
                return;
            staminaValue = Mathf.Clamp01(current / max);
            if (Native("staminaBar") && legacyHud != null)
                legacyHud.UpdateStamina(current, max);
        }

        /// <summary>Direction FROM the damage source, in world space.</summary>
        public void ReportDamage(Vector3 worldSourceDirection)
        {
            if (Native("damageIndicator") && legacyHud != null)
            {
                legacyHud.ShowDamageIndicator(worldSourceDirection);
                return;
            }
            Vector3 local = transform.InverseTransformDirection(worldSourceDirection);
            damageAngle = Mathf.Atan2(local.x, local.z);
            damageTimer = 1.2f;
        }

        public void ReportKill(string killer, string victim, string weaponLabel)
        {
            if (Native("killFeedText") && legacyHud != null)
            {
                legacyHud.AddKillFeed(killer, victim, weaponLabel ?? string.Empty);
                return;
            }
            string entry = string.IsNullOrEmpty(weaponLabel)
                ? killer + " -> " + victim
                : killer + " [" + weaponLabel + "] " + victim;
            killEntries.Insert(0, entry);
            while (killEntries.Count > MaxKillEntries)
                killEntries.RemoveAt(killEntries.Count - 1);
            RebuildKillFeed();
        }

        public void AddObjective(string objective)
        {
            if (string.IsNullOrEmpty(objective))
                return;
            objectiveQueue.Add(new ObjectiveEntry { text = objective, complete = false });
            RebuildObjectives();
        }

        public void CompleteObjective(string objective)
        {
            for (int i = 0; i < objectiveQueue.Count; i++)
            {
                ObjectiveEntry entry = objectiveQueue[i];
                if (entry.text == objective && !entry.complete)
                {
                    entry.complete = true;
                    objectiveQueue.RemoveAt(i);
                    objectiveQueue.Add(entry);
                    break;
                }
            }
            RebuildObjectives();
        }

        public void ClearObjectives()
        {
            objectiveQueue.Clear();
            killEntries.Clear();
            RebuildObjectives();
            RebuildKillFeed();
        }

        // ------------------------------------------------------------------- lifecycle

        private void Update()
        {
            referenceRefresh -= Time.unscaledDeltaTime;
            if (referenceRefresh <= 0f)
            {
                referenceRefresh = 1.5f;
                if (physiology == null)
                    physiology = Object.FindFirstObjectByType<Physiology>();
                if (agentManager == null)
                    agentManager = MultiAgentSystemManager.Instance;
            }
            if (Time.unscaledTime >= bindRefresh)
            {
                bindRefresh = Time.unscaledTime + 0.5f;
                if (weapon == null)
                    weapon = FindActiveWeapon();
                if (squad == null)
                    squad = Object.FindFirstObjectByType<SquadManager>();
                if (physiology == null)
                    physiology = Object.FindFirstObjectByType<Physiology>();
            }

            UpdateSquad();
            if (IsLive(Features.Compass))
                UpdateCompass();
            if (IsLive(Features.Ammo))
                UpdateAmmo();
            if (IsLive(Features.Vitals))
                UpdateVitals();
            if (IsLive(Features.Damage))
                UpdateDamageArc();
            if (IsLive(Features.Vignette))
                UpdateVignette();
            if (IsLive(Features.Stamina))
                UpdateStamina();
        }

        private bool IsLive(string feature)
        {
            return featureObjects.TryGetValue(feature, out GameObject go)
                && go != null && go.activeInHierarchy;
        }

        private Weapon FindActiveWeapon()
        {
            Transform node = transform;
            int guard = 0;
            while (node != null && guard++ < 12)
            {
                Weapon found = node.GetComponent<Weapon>();
                if (found != null)
                    return found;
                node = node.parent;
            }
            Weapon[] weapons = Object.FindObjectsByType<Weapon>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (Weapon candidate in weapons)
                if (candidate != null) return candidate;
            return null;
        }

        // ------------------------------------------------------------------- registration

        private static HUDController FindLegacyHud()
        {
            HUDController[] controllers = Object.FindObjectsByType<HUDController>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (HUDController candidate in controllers)
            {
                if (candidate != null)
                    return candidate;
            }
            return null;
        }

        private bool Native(string serializedFieldName)
        {
            return HudThemeLibrary.HudControllerOwns(legacyHud, serializedFieldName);
        }

        private void RegisterFeature(string name, GameObject root, string delegatedField = null)
        {
            if (root == null)
                return;
            if (delegatedField != null && Native(delegatedField))
            {
                nativeHandled.Add(name);
                Object.Destroy(root);
                return;
            }
            root.transform.SetParent(canvas != null ? canvas.transform : null, false);
            featureObjects[name] = root;
        }

        private bool SetFeature(string feature, bool enabled)
        {
            if (string.IsNullOrEmpty(feature))
                return false;
            feature = feature.Trim().ToLowerInvariant();
            if (nativeHandled.Contains(feature))
                return true;
            if (!featureObjects.TryGetValue(feature, out GameObject root) || root == null)
                return false;
            root.SetActive(enabled);
            return true;
        }

        // ------------------------------------------------------------------- compass

        private void BuildCompass()
        {
            ownsCompass = !(legacyHud != null
                && Native("compassText"));
            if (!ownsCompass)
            {
                nativeHandled.Add(Features.Compass);
                return;
            }

            Image bg = UiFactory.CreateImage(canvas.transform, "CompassStrip",
                HudThemeLibrary.WithAlpha(HudThemeLibrary.PanelBackground, 0.55f),
                Image.Type.Simple, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(CompassTicks * DegreesPerTick * PixelsPerDegree + 40f, 44f),
                new Vector2(0f, -8f));

            RectTransform container = new GameObject("Ticks", typeof(RectTransform)).transform as RectTransform;
            container.SetParent(bg.rectTransform, false);
            UiFactory.StretchFull(container);
            Image clip = container.gameObject.AddComponent<Image>();
            clip.sprite = null;
            clip.color = new Color(1f, 1f, 1f, 0f);
            clip.raycastTarget = false;
            container.gameObject.AddComponent<RectMask2D>();

            for (int i = 0; i < CompassTicks; i++)
            {
                Image tick = UiFactory.CreateImage(container, "Tick" + i, HudThemeLibrary.TextSecondary,
                    Image.Type.Simple, new Vector2(0.5f, 0.3f), new Vector2(0.5f, 0.3f),
                    new Vector2(0.5f, 0.5f), new Vector2(2f, 12f), Vector2.zero);
                compassTicks.Add(tick);

                Text label = UiFactory.CreateText(container, "Label" + i, string.Empty,
                    HudThemeLibrary.FontCaption, HudThemeLibrary.TextPrimary,
                    TextAnchor.MiddleCenter, new Vector2(0.5f, 0.3f), new Vector2(0.5f, 0.3f),
                    new Vector2(0.5f, 1f), new Vector2(48f, 18f), new Vector2(0f, 6f));
                compassLabels.Add(label);
            }

            Image caret = UiFactory.CreateImage(bg, "Caret", HudThemeLibrary.Amber,
                Image.Type.Simple, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(3f, 44f), Vector2.zero);

            RegisterFeature(Features.Compass, bg.gameObject);
        }

        private float GetHeading()
        {
            Camera cam = Camera.main;
            return cam != null ? cam.transform.eulerAngles.y : transform.eulerAngles.y;
        }

        private static float Normalize360(float angle)
        {
            return ((angle % 360f) + 360f) % 360f;
        }

        private void UpdateCompass()
        {
            float heading = GetHeading();
            for (int i = 0; i < CompassTicks; i++)
            {
                Image tickImage = compassTicks[i];
                if (tickImage == null)
                    continue;
                RectTransform tick = tickImage.rectTransform;
                int offsetSteps = i - CompassTicks / 2;
                float offsetDeg = offsetSteps * DegreesPerTick;
                float x = offsetDeg * PixelsPerDegree;
                float ang = Normalize360(heading + offsetDeg);
                tick.anchoredPosition = new Vector2(x, tick.anchoredPosition.y);

                float mod45 = ang % 45f;
                bool major = mod45 < 0.4f || mod45 > 44.6f;
                float mod15 = ang % 15f;
                bool mid = mod15 < 0.4f || mod15 > 14.6f;
                tick.sizeDelta = new Vector2(2f, major ? 18f : mid ? 12f : 6f);
                tickImage.color = major ? HudThemeLibrary.Amber : HudThemeLibrary.TextSecondary;

                if (i < compassLabels.Count && compassLabels[i] != null)
                {
                    Text label = compassLabels[i];
                    int nearest = Mathf.RoundToInt(ang / 45f) % 8;
                    label.text = major ? CardinalLabels[nearest] : string.Empty;
                    label.rectTransform.anchoredPosition =
                        new Vector2(x, label.rectTransform.anchoredPosition.y);
                }
            }
        }

        // ------------------------------------------------------------------- objectives

        private void BuildObjectives()
        {
            RectTransform panel = new GameObject("Objectives", typeof(RectTransform)).transform as RectTransform;
            panel.anchorMin = new Vector2(1f, 1f);
            panel.anchorMax = new Vector2(1f, 1f);
            panel.pivot = new Vector2(1f, 1f);
            panel.sizeDelta = new Vector2(340f, 190f);
            panel.anchoredPosition = new Vector2(-12f, -64f);

            for (int i = 0; i < MaxObjectiveRows; i++)
            {
                Text row = UiFactory.CreateText(panel, "Objective" + i, string.Empty,
                    HudThemeLibrary.FontCaption + 1, HudThemeLibrary.TextPrimary,
                    TextAnchor.UpperRight, new Vector2(0f, 1f), new Vector2(1f, 1f),
                    new Vector2(1f, 1f), new Vector2(0f, 28f), new Vector2(0f, -i * 32f));
                row.horizontalOverflow = HorizontalWrapMode.Overflow;
                row.enabled = false;
                objectiveRows.Add(row);
            }
            RegisterFeature(Features.Objectives, panel.gameObject);
        }

        private void RebuildObjectives()
        {
            for (int i = 0; i < objectiveRows.Count; i++)
            {
                Text row = objectiveRows[i];
                if (row == null)
                    continue;
                if (i < objectiveQueue.Count)
                {
                    ObjectiveEntry entry = objectiveQueue[i];
                    row.enabled = true;
                    row.text = (entry.complete ? "- " : "> ") + entry.text;
                    row.color = entry.complete ? HudThemeLibrary.TextMuted : HudThemeLibrary.TextPrimary;
                }
                else
                {
                    row.enabled = false;
                    row.text = string.Empty;
                }
            }
        }

        // ------------------------------------------------------------------- squad

        private void BuildSquad()
        {
            Image bg = UiFactory.CreateImage(canvas.transform, "SquadStatus",
                HudThemeLibrary.WithAlpha(HudThemeLibrary.PanelBackground, 0.4f),
                Image.Type.Simple, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(330f, 70f), new Vector2(12f, -64f));

            squadText = UiFactory.CreateText(bg, "Agents", string.Empty,
                HudThemeLibrary.FontCaption, HudThemeLibrary.TextSecondary,
                TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, 1f), new Vector2(0f, 18f), new Vector2(0f, -2f));

            for (int i = 0; i < MaxSquadPips; i++)
            {
                Image pip = UiFactory.CreateImage(bg, "Pip" + i, HudThemeLibrary.OliveDim,
                    Image.Type.Simple, new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(0f, 1f), new Vector2(HudThemeLibrary.PipSize, HudThemeLibrary.PipSize),
                    new Vector2(8f + i * (HudThemeLibrary.PipSize + 8f), -24f));
                pip.sprite = UiFactory.GetRadialSprite();
                pip.enabled = false;
                squadPips.Add(pip);

                Image fill = UiFactory.CreateImage(bg, "PipFill" + i, HudThemeLibrary.OliveBright,
                    Image.Type.Simple, new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(0f, 1f), new Vector2(HudThemeLibrary.PipSize - 12f, 4f),
                    new Vector2(14f + i * (HudThemeLibrary.PipSize + 8f), -6f));
                fill.enabled = false;
                squadPipFills.Add(fill);
            }
            RegisterFeature(Features.Squad, bg.gameObject);
        }

        private void UpdateSquad()
        {
            if (!IsLive(Features.Squad))
                return;
            if (squad != null && squad.Members != null)
            {
                List<SquadMember> members = squad.Members;
                for (int i = 0; i < squadPips.Count; i++)
                {
                    bool has = i < members.Count;
                    if (squadPips[i] != null)
                    {
                        squadPips[i].enabled = has;
                        if (has)
                        {
                            float h = Mathf.Clamp01(members[i].health);
                            squadPips[i].color = members[i].isLeader
                                ? HudThemeLibrary.AmberDim
                                : Color.Lerp(HudThemeLibrary.AlertRedDim, HudThemeLibrary.Olive, h);
                        }
                    }
                    if (i < squadPipFills.Count && squadPipFills[i] != null)
                    {
                        squadPipFills[i].enabled = has;
                        if (has)
                        {
                            float h = Mathf.Clamp01(members[i].health);
                            squadPipFills[i].rectTransform.sizeDelta =
                                new Vector2((HudThemeLibrary.PipSize - 12f) * h, 4f);
                        }
                    }
                }
                if (squadText != null)
                    squadText.text = "SQUAD " + squad.AliveCount + "  "
                        + squad.CurrentTactic.ToString().ToUpperInvariant()
                        + "  " + squad.CurrentFormation.ToString().ToUpperInvariant();
            }
            else if (agentManager != null)
            {
                for (int i = 0; i < squadPips.Count; i++)
                    if (squadPips[i] != null) squadPips[i].enabled = false;
                if (squadText != null)
                    squadText.text = "NETWORK " + agentManager.RegisteredAgentCount + " AGENTS  "
                        + agentManager.ActiveTeamCount + " TEAMS";
            }
            else if (squadText != null)
            {
                squadText.text = "SQUAD // NO UP-LINK";
            }
        }

        // ------------------------------------------------------------------- ammo

        private void BuildAmmo()
        {
            RectTransform panel = new GameObject("AmmoReadout", typeof(RectTransform)).transform as RectTransform;
            panel.anchorMin = new Vector2(1f, 0f);
            panel.anchorMax = new Vector2(1f, 0f);
            panel.pivot = new Vector2(1f, 0f);
            panel.sizeDelta = new Vector2(240f, 96f);
            panel.anchoredPosition = new Vector2(-20f, 16f);

            ammoNameText = UiFactory.CreateText(panel, "Weapon", string.Empty,
                HudThemeLibrary.FontSubhead, HudThemeLibrary.TextSecondary,
                TextAnchor.UpperRight, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(0f, 24f), Vector2.zero);

            ammoText = UiFactory.CreateText(panel, "Rounds", "--", HudThemeLibrary.FontHeading + 8,
                HudThemeLibrary.TextPrimary, TextAnchor.MiddleRight,
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                Vector2.zero, new Vector2(-8f, 4f));
            ammoText.fontStyle = FontStyle.Bold;

            malfunctionText = UiFactory.CreateText(panel, "Malfunction", string.Empty,
                HudThemeLibrary.FontBody, HudThemeLibrary.AlertRed,
                TextAnchor.MiddleLeft, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(0f, 0f), new Vector2(200f, 20f), new Vector2(0f, 12f));

            RegisterFeature(Features.Ammo, panel.gameObject, "ammoText");
        }

        private void UpdateAmmo()
        {
            if (weapon == null)
            {
                if (ammoText != null && ammoText.text != "--")
                    ammoText.text = "--";
                return;
            }
            int rounds = weapon.RoundsRemaining;
            if (ammoText != null)
            {
                string label = rounds.ToString("D2");
                if (ammoText.text != label)
                    ammoText.text = label;
                Color color = rounds <= 5 ? HudThemeLibrary.AlertRed
                    : rounds <= 15 ? HudThemeLibrary.Amber : HudThemeLibrary.TextPrimary;
                ammoText.color = color;
            }
            if (ammoNameText != null)
            {
                string name = weapon.gameObject.name.ToUpperInvariant();
                if (ammoNameText.text != name)
                    ammoNameText.text = name;
            }
            if (malfunctionText != null)
                malfunctionText.text = weapon.IsMalfunctioned ? "STOPPAGE" : string.Empty;
        }

        // ------------------------------------------------------------------- vitals

        private void BuildVitals()
        {
            RectTransform panel = new GameObject("Vitals", typeof(RectTransform)).transform as RectTransform;
            panel.anchorMin = new Vector2(0f, 0f);
            panel.anchorMax = new Vector2(0f, 0f);
            panel.pivot = new Vector2(0f, 0f);
            panel.sizeDelta = new Vector2(210f, 110f);
            panel.anchoredPosition = new Vector2(20f, 18f);

            consciousnessArc = UiFactory.CreateRadialArc(panel, "Consciousness",
                HudThemeLibrary.OliveBright, 1f);
            RectTransform arcRect = consciousnessArc.rectTransform;
            arcRect.anchorMin = arcRect.anchorMax = new Vector2(0f, 0.5f);
            arcRect.pivot = new Vector2(0f, 0.5f);
            arcRect.anchoredPosition = new Vector2(55f, 0f);
            arcRect.sizeDelta = new Vector2(96f, 96f);

            heartDot = UiFactory.CreateImage(consciousnessArc, "Heart", HudThemeLibrary.AlertRed,
                Image.Type.Simple, new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.62f),
                new Vector2(0.5f, 0.5f), new Vector2(16f, 16f), Vector2.zero);
            heartDot.sprite = UiFactory.GetRadialSprite();

            heartRateText = CreateVitalRow(panel, "BPM", 44f);
            bloodText = CreateVitalRow(panel, "BLOOD", 24f);
            respirationText = CreateVitalRow(panel, "RESP", 4f);

            RegisterFeature(Features.Vitals, panel.gameObject);
        }

        private Text CreateVitalRow(RectTransform panel, string label, float bottom)
        {
            return UiFactory.CreateText(panel, label, label + " --",
                HudThemeLibrary.FontCaption, HudThemeLibrary.TextSecondary,
                TextAnchor.MiddleLeft, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, 0f), new Vector2(-120f, 18f), new Vector2(116f, bottom));
        }

        private void UpdateVitals()
        {
            float heartRate = 65f;
            float consciousness = 1f;
            float bloodLoss = 0f;
            float respiration = 15f;
            if (physiology != null)
            {
                PhysiologyState state = physiology.State;
                heartRate = Mathf.Max(20f, state.heartRate);
                consciousness = Mathf.Clamp01(state.consciousness / 100f);
                bloodLoss = state.bloodLossVolume;
                respiration = state.respiration;
            }

            pulsePhase = Mathf.Repeat(pulsePhase + Time.deltaTime * (heartRate / 60f), 1f);
            float pulse = Mathf.Exp(-6f * pulsePhase);
            if (heartDot != null)
            {
                float scale = 1f + 0.45f * pulse;
                heartDot.rectTransform.localScale = new Vector3(scale, scale, 1f);
                heartDot.color = Color.Lerp(HudThemeLibrary.AlertRedDim, HudThemeLibrary.AlertRed, pulse);
            }
            if (consciousnessArc != null)
            {
                consciousnessArc.fillAmount = consciousness;
                consciousnessArc.color = Color.Lerp(HudThemeLibrary.AlertRedDim,
                    HudThemeLibrary.OliveBright, consciousness);
            }
            if (heartRateText != null)
                heartRateText.text = "BPM " + Mathf.RoundToInt(heartRate);
            if (bloodText != null)
                bloodText.text = "BLOOD " + bloodLoss.ToString("F1") + " L";
            if (respirationText != null)
                respirationText.text = "RESP " + Mathf.RoundToInt(respiration);
        }

        // ------------------------------------------------------------------- damage

        private void BuildDamage()
        {
            GameObject root = new GameObject("DamageArc", typeof(RectTransform));
            damageArc = root.AddComponent<Image>();
            damageArc.sprite = UiFactory.GetRadialSprite();
            damageArc.type = Image.Type.Filled;
            damageArc.fillMethod = Image.FillMethod.Radial360;
            damageArc.fillOrigin = (int)Image.Origin360.Top;
            damageArc.fillClockwise = true;
            damageArc.fillAmount = 0.08f;
            damageArc.color = HudThemeLibrary.WithAlpha(HudThemeLibrary.AlertRed, 0f);
            damageArc.raycastTarget = false;
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(320f, 320f);
            RegisterFeature(Features.Damage, root, "damageIndicator");
        }

        private void UpdateDamageArc()
        {
            if (damageArc == null)
                return;
            if (damageTimer > 0f)
            {
                damageTimer -= Time.deltaTime;
                float t = Mathf.Clamp01(damageTimer / 1.2f);
                damageArc.color = Color.Lerp(Color.clear, HudThemeLibrary.AlertRed, t);
                damageArc.fillAmount = 0.05f + 0.12f * t;
                damageArc.rectTransform.localEulerAngles =
                    new Vector3(0f, 0f, -damageAngle * Mathf.Rad2Deg);
            }
            else
            {
                damageArc.color = Color.clear;
            }
        }

        // ------------------------------------------------------------------- kill feed

        private void BuildKillFeed()
        {
            RectTransform panel = new GameObject("KillFeed", typeof(RectTransform)).transform as RectTransform;
            panel.anchorMin = new Vector2(1f, 1f);
            panel.anchorMax = new Vector2(1f, 1f);
            panel.pivot = new Vector2(1f, 1f);
            panel.sizeDelta = new Vector2(330f, 150f);
            panel.anchoredPosition = new Vector2(-12f, -268f);

            ScrollRect scroll = UiFactory.CreateScrollRect(panel, out killFeedContent);
            UiFactory.StretchFull(scroll.viewport);

            VerticalLayoutGroup layout = killFeedContent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 2f;
            layout.childAlignment = TextAnchor.UpperRight;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            ContentSizeFitter fitter = killFeedContent.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            RegisterFeature(Features.KillFeed, panel.gameObject, "killFeedText");
        }

        private void RebuildKillFeed()
        {
            if (killFeedContent == null)
                return;
            for (int i = killFeedContent.childCount - 1; i >= 0; i--)
            {
                GameObject child = killFeedContent.GetChild(i).gameObject;
                if (child != null)
                    Object.Destroy(child);
            }

            for (int i = 0; i < killEntries.Count; i++)
            {
                Text entry = UiFactory.CreateText(killFeedContent, "Entry" + i, killEntries[i],
                    HudThemeLibrary.FontCaption,
                    i == 0 ? HudThemeLibrary.Amber : HudThemeLibrary.TextSecondary,
                    TextAnchor.UpperRight, new Vector2(0f, 1f), new Vector2(1f, 1f),
                    new Vector2(0.5f, 1f), new Vector2(0f, 16f), Vector2.zero);
                entry.horizontalOverflow = HorizontalWrapMode.Overflow;
                entry.enabled = i < 5;
            }
        }

        // ------------------------------------------------------------------- vignette

        private void BuildVignette()
        {
            GameObject root = new GameObject("Vignette", typeof(RectTransform));
            vignette = root.AddComponent<Image>();
            vignette.sprite = UiFactory.GetVignetteSprite();
            vignette.color = Color.clear;
            vignette.raycastTarget = false;
            UiFactory.StretchFull(root.GetComponent<RectTransform>());
            RegisterFeature(Features.Vignette, root);
        }

        private void UpdateVignette()
        {
            if (vignette == null)
                return;
            float health = externalHealth01;
            if (physiology != null)
                health = Mathf.Min(health, Mathf.Clamp01(physiology.State.consciousness / 100f));
            float danger = Mathf.InverseLerp(0.55f, 0.15f, health);
            float pulse = danger > 0f ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * (3f + danger * 5f)) : 0f;
            float alpha = danger * (0.35f + 0.65f * pulse) * 0.85f;
            Color tint = danger > 0.6f
                ? new Color(1f, 0.75f, 0.7f, alpha)
                : new Color(1f, 1f, 1f, alpha);
            vignette.color = tint;
        }

        // ------------------------------------------------------------------- stamina

        private void BuildStamina()
        {
            staminaArc = UiFactory.CreateRadialArc(canvas.transform, "StaminaArc",
                HudThemeLibrary.WithAlpha(HudThemeLibrary.OliveBright, 0.9f), 1f);
            staminaArc.fillOrigin = (int)Image.Origin360.Bottom;
            staminaArc.raycastTarget = false;
            RectTransform rect = staminaArc.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(74f, 74f);
            rect.anchoredPosition = new Vector2(0f, 24f);
            RegisterFeature(Features.Stamina, staminaArc.gameObject, "staminaBar");
        }

        private void UpdateStamina()
        {
            if (staminaArc == null)
                return;
            float value = staminaValue;
            if (physiology != null)
                value = Mathf.Min(value, physiology.StaminaFactor);
            staminaArc.fillAmount = Mathf.Lerp(staminaArc.fillAmount, value, Time.deltaTime * 6f);
            staminaArc.color = Color.Lerp(HudThemeLibrary.AlertRedDim,
                HudThemeLibrary.WithAlpha(HudThemeLibrary.OliveBright, 0.9f), value);
        }
    }
}
