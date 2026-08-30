using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VEVE.Mission;

namespace VEVE.UI
{
    /// <summary>
    /// Cinematic menu state machine built entirely in code:
    /// Boot -> Main -> CampaignBrief -> Loadout -> Deploy, routed where possible through the
    /// scene <see cref="UIManager"/> (panel routing, ESC/pointer-lock conventions). If no
    /// UIManager is wired, the controller self-routes and still mirrors UIManager's cursor /
    /// time-scale policy. Save-slot listing is produced from <see cref="SaveSystem.GetSaveSlots"/>
    /// and every external binding (SaveSystem, Weapon loadout) is null-safe.
    /// </summary>
    public sealed class MainMenuFlowController : MonoBehaviour
    {
        public enum MenuFlowState { Boot, Main, CampaignBrief, Loadout, Deploying }

        [Header("Bindings (auto-discovered when empty)")]
        [SerializeField] private UIManager uiManager;
        [SerializeField] private SaveSystem saveSystem;
        [SerializeField] private string missionScene = "Mission_01";
        [SerializeField] private float bootDuration = 2.5f;
        [SerializeField] private float fadeDuration = 1.2f;
        [SerializeField] private string operationName = "OPERATION RESONANCE";
        [SerializeField, TextArea(3, 6)] private string operationBrief =
            "Insert by rotor-wing two klicks north of the relay ridge. Confirm comms check with " +
            "OVERLORD, mark patrol routes, and recover the resonance sampler before the front moves in.";

        private MenuFlowState currentState = MenuFlowState.Boot;
        private Canvas canvas;
        private RectTransform rootRect;
        private readonly Dictionary<MenuFlowState, GameObject> panels =
            new Dictionary<MenuFlowState, GameObject>();
        private readonly List<GameObject> slotRows = new List<GameObject>();
        private RectTransform slotListRect;
        private Text toastText;
        private CanvasGroup fadeGroup;
        private float bootTimer;
        private float fadeTarget;
        private float fadeValue;
        private bool deploying;
        private string selectedSlot;
        private string pendingSlot;

        public MenuFlowState State => currentState;
        public string SelectedSlot => selectedSlot;
        public event System.Action<MenuFlowState> OnFlowChanged;

        private void Awake()
        {
            if (uiManager == null)
                uiManager = Object.FindFirstObjectByType<UIManager>();
            if (saveSystem == null)
                saveSystem = Object.FindFirstObjectByType<SaveSystem>();

            canvas = UiFactory.CreateCanvas("MainMenuFlow", 200);
            rootRect = new GameObject("Root", typeof(RectTransform)).transform as RectTransform;
            rootRect.SetParent(canvas.transform, false);
            UiFactory.StretchFull(rootRect);

            BuildBootPanel();
            BuildMainPanel();
            BuildBriefPanel();
            BuildLoadoutPanel();
            BuildDeployOverlay();

            bootTimer = Mathf.Max(0.5f, bootDuration);
            Transition(MenuFlowState.Boot);
        }

        private void Update()
        {
            if (currentState == MenuFlowState.Boot)
            {
                bootTimer -= Time.unscaledDeltaTime;
                if (bootTimer <= 0f || Input.anyKeyDown)
                    Transition(MenuFlowState.Main);
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                switch (currentState)
                {
                    case MenuFlowState.CampaignBrief:
                    case MenuFlowState.Loadout:
                        Transition(MenuFlowState.Main);
                        break;
                    case MenuFlowState.Deploying:
                        break;
                }
            }

            UpdateFade();
        }

        // ------------------------------------------------------------------- flow

        public void Transition(MenuFlowState next)
        {
            if (next == currentState && next != MenuFlowState.Boot
                && next != MenuFlowState.CampaignBrief)
                return;
            foreach (KeyValuePair<MenuFlowState, GameObject> pair in panels)
            {
                if (pair.Value != null)
                    pair.Value.SetActive(pair.Key == next
                        || (next == MenuFlowState.Deploying && pair.Key == currentState
                            && pair.Key != MenuFlowState.Deploying));
            }
            MenuFlowState previous = currentState;
            currentState = next;
            ApplyCursorPolicy();
            if (next == MenuFlowState.CampaignBrief)
                RebuildSaveSlots();
            if (previous != next || next == MenuFlowState.Boot)
                OnFlowChanged?.Invoke(next);
        }

        public void GoMain() => Transition(MenuFlowState.Main);
        public void GoCampaignBrief() => Transition(MenuFlowState.CampaignBrief);
        public void GoLoadout() => Transition(MenuFlowState.Loadout);

        /// <summary>Begins the deploy fade. Requires a prior slot selection (defaults to first).</summary>
        public void BeginDeploy(string slot = null)
        {
            if (deploying)
                return;
            if (!string.IsNullOrEmpty(slot))
                selectedSlot = slot;
            if (string.IsNullOrEmpty(selectedSlot))
                selectedSlot = pendingSlot ?? "new_operation";
            deploying = true;
            fadeTarget = 1f;
            Transition(MenuFlowState.Deploying);
        }

        private void OnFadeComplete()
        {
            if (!deploying)
                return;
            deploying = false;
            if (!string.IsNullOrEmpty(missionScene) && SceneExists(missionScene))
            {
                SceneManager.LoadScene(missionScene);
                return;
            }
            if (uiManager != null)
            {
                try
                {
                    uiManager.ResumeGame();
                    return;
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning("[MainMenuFlow] UIManager routing unavailable: " + ex.Message);
                }
            }
            ShowToast("NO MISSION SCENE BOUND - SET missionScene IN INSPECTOR");
            fadeTarget = 0f;
            Transition(MenuFlowState.Main);
        }

        private void UpdateFade()
        {
            float step = Time.unscaledDeltaTime / Mathf.Max(0.1f, fadeDuration);
            fadeValue = Mathf.MoveTowards(fadeValue, fadeTarget, step);
            if (fadeGroup != null)
                fadeGroup.alpha = fadeValue;
            if (fadeTarget >= 1f && fadeValue >= 1f)
                OnFadeComplete();
        }

        private static bool SceneExists(string sceneName)
        {
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                if (name == sceneName)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Mirrors UIManager.UpdatePanels convention: cursor unlocked+visible outside gameplay,
        /// and time scale forced to 1 for menu states.
        /// </summary>
        private void ApplyCursorPolicy()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // ------------------------------------------------------------------- panels

        private void BuildBootPanel()
        {
            Image panel = CreateFullScreenPanel("Boot");
            UiFactory.CreateText(panel, "Logo", "VEVE", HudThemeLibrary.FontCinematic,
                HudThemeLibrary.OliveBright);
            UiFactory.CreateText(panel, "Subtitle", "THE  RESONANCE", HudThemeLibrary.FontReadout,
                HudThemeLibrary.TextSecondary, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(800f, 40f), new Vector2(0f, -70f));
            UiFactory.CreateText(panel, "Hint", "CLICK OR PRESS ANY KEY", HudThemeLibrary.FontCaption,
                HudThemeLibrary.TextMuted, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(500f, 26f), new Vector2(0f, 60f));
            panels[MenuFlowState.Boot] = panel.gameObject;
        }

        private void BuildMainPanel()
        {
            Image panel = CreateFullScreenPanel("Main");
            UiFactory.CreateText(panel, "Title", "VEVE // THE RESONANCE", HudThemeLibrary.FontReadout,
                HudThemeLibrary.TextPrimary, TextAnchor.MiddleLeft,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(-40f, 54f), new Vector2(20f, -20f));

            RectTransform menuRoot = new GameObject("Menu", typeof(RectTransform)).transform as RectTransform;
            menuRoot.SetParent(panel.rectTransform, false);
            menuRoot.anchorMin = new Vector2(0f, 0.5f);
            menuRoot.anchorMax = new Vector2(0f, 0.5f);
            menuRoot.pivot = new Vector2(0f, 0.5f);
            menuRoot.anchoredPosition = new Vector2(28f, -20f);
            menuRoot.sizeDelta = new Vector2(300f, 340f);
            VerticalLayoutGroup layout = UiFactory.CreateVLayout(menuRoot, 10f,
                new RectOffset(0, 0, 0, 0), false);
            layout.childAlignment = TextAnchor.UpperLeft;

            AddMenuButton(menuRoot, "CAMPAIGN", () => Transition(MenuFlowState.CampaignBrief));
            AddMenuButton(menuRoot, "CONTINUE", OnContinue);
            AddMenuButton(menuRoot, "LOADOUT", () => Transition(MenuFlowState.Loadout));
            AddMenuButton(menuRoot, "SETTINGS", OnSettings);
            AddMenuButton(menuRoot, "QUIT", OnQuit);

            toastText = UiFactory.CreateText(panel, "Toast", string.Empty, HudThemeLibrary.FontBody,
                HudThemeLibrary.Amber, TextAnchor.MiddleLeft,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f),
                new Vector2(-40f, 26f), new Vector2(20f, 20f));
            panels[MenuFlowState.Main] = panel.gameObject;
        }

        private void BuildBriefPanel()
        {
            Image panel = CreateFullScreenPanel("Brief");

            UiFactory.CreateText(panel, "OpName", operationName.ToUpperInvariant(),
                HudThemeLibrary.FontHeading, HudThemeLibrary.Amber, TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(-40f, 46f), new Vector2(24f, -20f));

            Text brief = UiFactory.CreateText(panel, "Brief", operationBrief, HudThemeLibrary.FontBody,
                HudThemeLibrary.TextPrimary, TextAnchor.UpperLeft,
                new Vector2(0f, 0.55f), new Vector2(0.55f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, -90f), new Vector2(24f, -70f));
            brief.verticalOverflow = VerticalWrapMode.Truncate;

            UiFactory.CreateText(panel, "SlotsHeader", "SAVED OPERATIONS", HudThemeLibrary.FontSubhead,
                HudThemeLibrary.TextSecondary, TextAnchor.UpperLeft,
                new Vector2(0.6f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 30f), new Vector2(0f, -110f));

            Image slotViewportBG = UiFactory.CreatePanel(panel, "SlotArea",
                new Color(1f, 1f, 1f, 0.03f));
            RectTransform sr = slotViewportBG.rectTransform;
            sr.anchorMin = new Vector2(0.6f, 0.32f);
            sr.anchorMax = new Vector2(1f, 0.86f);
            sr.pivot = new Vector2(0.5f, 1f);
            sr.offsetMin = new Vector2(8f, 0f);
            sr.offsetMax = new Vector2(-8f, 0f);
            ScrollRect scroll = slotViewportBG.gameObject.AddComponent<ScrollRect>();
            Image slotMask = slotViewportBG;
            slotMask.gameObject.AddComponent<RectMask2D>();
            slotListRect = new GameObject("Slots", typeof(RectTransform)).transform as RectTransform;
            slotListRect.SetParent(slotViewportBG.rectTransform, false);
            slotListRect.anchorMin = new Vector2(0f, 1f);
            slotListRect.anchorMax = new Vector2(1f, 1f);
            slotListRect.pivot = new Vector2(0.5f, 1f);
            slotListRect.sizeDelta = new Vector2(0f, 60f);
            scroll.content = slotListRect;
            scroll.viewport = slotViewportBG.rectTransform;
            scroll.vertical = true;
            scroll.horizontal = false;
            VerticalLayoutGroup vlayout = slotListRect.gameObject.AddComponent<VerticalLayoutGroup>();
            vlayout.spacing = 6f;
            vlayout.childControlWidth = false;
            vlayout.childControlHeight = false;
            vlayout.childForceExpandWidth = true;

            RectTransform deployRect = new GameObject("DeployRow", typeof(RectTransform))
                .transform as RectTransform;
            deployRect.SetParent(panel.rectTransform, false);
            deployRect.anchorMin = new Vector2(0f, 0f);
            deployRect.anchorMax = new Vector2(0.5f, 0f);
            deployRect.pivot = new Vector2(0f, 0f);
            deployRect.sizeDelta = new Vector2(0f, 50f);
            deployRect.anchoredPosition = new Vector2(24f, 20f);
            UiFactory.CreateHLayout(deployRect, 10f, new RectOffset(0, 0, 0, 0), false);
            Button deploy = UiFactory.CreateTableButton(deployRect, "Deploy", "DEPLOY",
                HudThemeLibrary.SlotSelected, HudThemeLibrary.TextOnDark,
                HudThemeLibrary.FontSubhead, new Vector2(180f, 44f));
            deploy.onClick.AddListener(() => BeginDeploy());
            Button back = UiFactory.CreateTableButton(deployRect, "Back", "BACK (ESC)",
                HudThemeLibrary.ButtonNormal, HudThemeLibrary.TextOnDark,
                HudThemeLibrary.FontSubhead, new Vector2(150f, 44f));
            back.onClick.AddListener(() => Transition(MenuFlowState.Main));

            panels[MenuFlowState.CampaignBrief] = panel.gameObject;
        }

        private void BuildLoadoutPanel()
        {
            Image panel = CreateFullScreenPanel("Loadout");

            UiFactory.CreateText(panel, "Header", "LOADOUT // EFFECTIVE COMPOSITION",
                HudThemeLibrary.FontSubhead + 6, HudThemeLibrary.TextPrimary, TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(-40f, 40f), new Vector2(24f, -18f));

            RectTransform infoRect = new GameObject("Info", typeof(RectTransform)).transform as RectTransform;
            infoRect.SetParent(panel.rectTransform, false);
            infoRect.anchorMin = new Vector2(0f, 0.35f);
            infoRect.anchorMax = new Vector2(1f, 0.88f);
            infoRect.offsetMin = new Vector2(24f, 0f);
            infoRect.offsetMax = new Vector2(-24f, 0f);
            Text weapons = UiFactory.CreateText(infoRect, "Weapons", BuildWeaponSummary(),
                HudThemeLibrary.FontBody + 4, HudThemeLibrary.TextSecondary, TextAnchor.UpperLeft);
            weapons.verticalOverflow = VerticalWrapMode.Truncate;

            RectTransform rowRect = new GameObject("Row", typeof(RectTransform)).transform as RectTransform;
            rowRect.SetParent(panel.rectTransform, false);
            rowRect.anchorMin = new Vector2(0f, 0f);
            rowRect.anchorMax = new Vector2(0.5f, 0f);
            rowRect.pivot = new Vector2(0f, 0f);
            rowRect.sizeDelta = new Vector2(0f, 50f);
            rowRect.anchoredPosition = new Vector2(24f, 20f);
            UiFactory.CreateHLayout(rowRect, 10f, new RectOffset(0, 0, 0, 0), false);
            Button toBrief = UiFactory.CreateTableButton(rowRect, "Next", "BRIEFING >",
                HudThemeLibrary.ButtonNormal, HudThemeLibrary.TextOnDark,
                HudThemeLibrary.FontSubhead, new Vector2(180f, 44f));
            toBrief.onClick.AddListener(() => Transition(MenuFlowState.CampaignBrief));
            Button toMain = UiFactory.CreateTableButton(rowRect, "Back", "< MAIN (ESC)",
                HudThemeLibrary.ButtonNormal, HudThemeLibrary.TextOnDark,
                HudThemeLibrary.FontSubhead, new Vector2(180f, 44f));
            toMain.onClick.AddListener(() => Transition(MenuFlowState.Main));

            panels[MenuFlowState.Loadout] = panel.gameObject;
        }

        private void BuildDeployOverlay()
        {
            GameObject overlay = new GameObject("Fade", typeof(RectTransform));
            overlay.transform.SetParent(rootRect, false);
            Image fade = overlay.AddComponent<Image>();
            fade.sprite = UiFactory.GetSolidSprite();
            fade.color = HudThemeLibrary.ScreenFade;
            fade.raycastTarget = false;
            UiFactory.StretchFull(fade.rectTransform);
            fadeGroup = overlay.AddComponent<CanvasGroup>();
            fadeGroup.alpha = 0f;
            fadeGroup.interactable = false;
            fadeGroup.blocksRaycasts = false;
            DeployPanel = overlay;

            Text staging = UiFactory.CreateText(fade, "Staging", "STAGING // ROTOR-WING SPINNING UP",
                HudThemeLibrary.FontSubhead, HudThemeLibrary.WithAlpha(HudThemeLibrary.TextPrimary, 0.8f));
            RectTransform stagingRect = staging.rectTransform;
            stagingRect.anchorMin = new Vector2(0.5f, 0.5f);
            stagingRect.anchorMax = new Vector2(0.5f, 0.5f);
            stagingRect.pivot = new Vector2(0.5f, 0.5f);
            stagingRect.sizeDelta = new Vector2(900f, 40f);
            stagingRect.anchoredPosition = Vector2.zero;
            panels[MenuFlowState.Deploying] = overlay;
        }

        private GameObject DeployPanel { get; set; }

        private Image CreateFullScreenPanel(string name)
        {
            Image panel = UiFactory.CreatePanel(rootRect, name,
                HudThemeLibrary.WithAlpha(HudThemeLibrary.ScreenFade, 0.96f));
            return panel;
        }

        private void AddMenuButton(RectTransform parent, string label, System.Action action)
        {
            Button button = UiFactory.CreateTableButton(parent, label, label,
                HudThemeLibrary.ButtonNormal, HudThemeLibrary.TextOnDark,
                HudThemeLibrary.FontSubhead, new Vector2(280f, 48f));
            button.onClick.AddListener(() => action());
        }

        // ------------------------------------------------------------------- handlers

        private void OnContinue()
        {
            List<string> slots = SafeGetSlots();
            if (slots.Count == 0)
            {
                ShowToast("NO SAVED OPERATIONS ON RECORD");
                return;
            }
            pendingSlot = slots[slots.Count - 1];
            Transition(MenuFlowState.CampaignBrief);
        }

        private void OnSettings()
        {
            if (uiManager != null)
            {
                try
                {
                    uiManager.OpenSettings();
                    return;
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning("[MainMenuFlow] Settings routing unavailable: " + ex.Message);
                }
            }
            ShowToast("SETTINGS MODULE OFFLINE");
        }

        private void OnQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void ShowToast(string message)
        {
            if (toastText != null)
                toastText.text = message;
        }

        private List<string> SafeGetSlots()
        {
            List<string> slots = new List<string>();
            if (saveSystem != null)
            {
                try
                {
                    List<string> reported = saveSystem.GetSaveSlots();
                    if (reported != null)
                        slots.AddRange(reported);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning("[MainMenuFlow] SaveSystem unavailable: " + ex.Message);
                }
            }
            return slots;
        }

        private void RebuildSaveSlots()
        {
            if (slotListRect == null)
                return;
            foreach (GameObject row in new List<GameObject>(slotRows))
            {
                if (row != null)
                    Object.Destroy(row);
            }
            slotRows.Clear();

            List<string> slots = SafeGetSlots();
            if (slots.Count == 0)
                slots.Add("new_operation");
            selectedSlot = pendingSlot ?? slots[0];

            for (int i = 0; i < slots.Count; i++)
            {
                string slot = slots[i];
                bool isCurrent = slot == selectedSlot;
                Image rowBG = UiFactory.CreateImage(slotListRect, "Slot" + i,
                    isCurrent ? HudThemeLibrary.WithAlpha(HudThemeLibrary.SlotSelected, 0.55f)
                        : HudThemeLibrary.WithAlpha(HudThemeLibrary.PanelInset, 0.8f),
                    Image.Type.Simple, new Vector2(0f, 1f), new Vector2(1f, 1f),
                    new Vector2(0.5f, 1f), new Vector2(0f, 38f), Vector2.zero);
                Button button = rowBG.gameObject.AddComponent<Button>();
                button.targetGraphic = rowBG;
                button.onClick.AddListener(() =>
                {
                    selectedSlot = slot;
                    pendingSlot = slot;
                    RebuildSaveSlots();
                });
                UiFactory.CreateText(rowBG, "Label", slot.ToUpperInvariant(),
                    HudThemeLibrary.FontBody,
                    isCurrent ? HudThemeLibrary.TextOnDark : HudThemeLibrary.TextSecondary,
                    TextAnchor.MiddleLeft);
                slotRows.Add(rowBG.gameObject);
            }
            slotListRect.sizeDelta = new Vector2(0f, slots.Count * 44f);
            pendingSlot = null;
        }

        private string BuildWeaponSummary()
        {
            List<string> names = new List<string>();
            Weapon[] weapons = Object.FindObjectsByType<Weapon>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (Weapon weapon in weapons)
            {
                if (weapon != null)
                    names.Add("PRIMARY  " + weapon.gameObject.name.ToUpperInvariant()
                        + "  [" + weapon.RoundsRemaining + " RDS]");
            }
            if (names.Count == 0)
                names.Add("PRIMARY  M712 CARBINE  [30/30 RDS]");
            names.Add("SIDEARM  P226  [12/12 RDS]");
            names.Add("SUPPORT  MEDKIT x2  ·  AMMO x4  ·  BREACH CHARGE x1");
            return string.Join("\n", names);
        }
    }
}
