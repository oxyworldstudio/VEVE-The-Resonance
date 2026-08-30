using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using VEVE.Catalog;

namespace VEVE.UI.Personalization
{
    /// <summary>
    /// Personalization UI orchestrator: owns the tabbed workspace canvas
    /// (Operator | Weapon | Gear | Finishes | Zeroing), routes visibility from
    /// MainMenuFlowController (shown only in the Loadout flow state, ESC defers to that
    /// controller), and exposes Bind*/Set*/Refresh APIs plus a live
    /// <see cref="UserLoadoutSelection"/> fed by the child panels. Compiles and presents
    /// standalone today: every data inlet is a <see cref="PersonalizationSeams.cs"/> seam.
    /// </summary>
    public sealed class PersonalizationWorkspace : MonoBehaviour
    {
        public const string TabOperator = "Operator";
        public const string TabWeapon = "Weapon";
        public const string TabGear = "Gear";
        public const string TabFinishes = "Finishes";
        public const string TabZeroing = "Zeroing";

        public static readonly string[] TabNames =
            { TabOperator, TabWeapon, TabGear, TabFinishes, TabZeroing };

        [Header("Flow routing (auto-discovered when empty)")]
        [SerializeField] private MainMenuFlowController flowController;
        [SerializeField] private int canvasSortOrder = 230;
        [SerializeField] private bool autoSave = false;

        private Canvas _canvas;
        private string _currentTab = TabWeapon;
        private readonly Dictionary<string, RectTransform> _tabHosts = new Dictionary<string, RectTransform>();
        private readonly Dictionary<string, Image> _tabButtonBGs = new Dictionary<string, Image>();

        private OperatorPanel _operatorPanel;
        private WeaponCustomizationPanel _weaponPanel;
        private GearPanel _gearPanel;
        private FinishesPanel _finishesPanel;
        private ZeroingPanel _zeroingPanel;
        private PersonalizationStateStore _store;

        public UserLoadoutSelection Selection { get; private set; } = new UserLoadoutSelection();
        public string CurrentTab => _currentTab;
        public PersonalizationStateStore Store => _store;
        public WeaponCustomizationPanel Weapon => _weaponPanel;
        public OperatorPanel Operator => _operatorPanel;
        public GearPanel Gear => _gearPanel;
        public FinishesPanel Finishes => _finishesPanel;
        public ZeroingPanel Zeroing => _zeroingPanel;

        /// <summary>True = writes through <see cref="Store"/> on every selection change.</summary>
        public bool AutoSave
        {
            get => autoSave;
            set => autoSave = value;
        }

        public MainMenuFlowController FlowController
        {
            get => flowController;
            set => flowController = value;
        }

        // ------------------------------------------------------------------ lifecycle

        private void Awake()
        {
            BuildChrome();
        }

        private void OnEnable()
        {
            if (flowController == null)
                flowController = UnityEngine.Object.FindFirstObjectByType<MainMenuFlowController>();
            if (flowController != null)
            {
                flowController.OnFlowChanged += HandleFlowChanged;
                ApplyFlowState(flowController.State);
            }
            else
            {
                // No flow controller in the scene (test/tooling scene): stay visible.
                gameObject.SetActive(true);
            }
        }

        private void OnDisable()
        {
            if (flowController != null)
                flowController.OnFlowChanged -= HandleFlowChanged;
        }

        private void HandleFlowChanged(MainMenuFlowController.MenuFlowState state)
        {
            ApplyFlowState(state);
        }

        private void ApplyFlowState(MainMenuFlowController.MenuFlowState state)
        {
            bool visible = state == MainMenuFlowController.MenuFlowState.Loadout;
            if (gameObject.activeSelf != visible)
                gameObject.SetActive(visible);
            if (visible)
                Refresh();
        }

        private void Update()
        {
            if (!Input.GetKeyDown(KeyCode.Escape))
                return;
            if (CloseOpenDetail())
                return;
            // ESC defers to MainMenuFlowController: it processes Escape itself and routes
            // Loadout -> Main (we hide via OnFlowChanged). Only act when no controller exists.
            if (flowController == null)
                gameObject.SetActive(false);
        }

        private bool CloseOpenDetail()
        {
            if (_currentTab == TabWeapon && _weaponPanel != null && _weaponPanel.HasDetailOpen)
            {
                _weaponPanel.ClearDetail();
                return true;
            }
            if (_currentTab == TabOperator && _operatorPanel != null && _operatorPanel.HasDetailOpen)
            {
                _operatorPanel.ClearDetail();
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ chrome

        private void BuildChrome()
        {
            if (_canvas != null)
                return;

            _canvas = UiFactory.CreateCanvas("PersonalizationWorkspace", canvasSortOrder);
            RectTransform root = new GameObject("Root", typeof(RectTransform)).transform as RectTransform;
            root.SetParent(_canvas.transform, false);
            UiFactory.StretchFull(root);

            Image bg = UiFactory.CreatePanel(root, "Backdrop", HudThemeLibrary.PanelBackground);
            UiFactory.StretchFull(bg.rectTransform);

            // Header strip: one H layout on the strip; title/buttons are fixed-size cells.
            RectTransform header = new GameObject("Header", typeof(RectTransform)).transform as RectTransform;
            header.SetParent(bg.transform, false);
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.pivot = new Vector2(0.5f, 1f);
            header.sizeDelta = new Vector2(0f, 64f);
            header.anchoredPosition = Vector2.zero;
            UiFactory.CreateHLayout(header, 8f,
                new RectOffset((int)HudThemeLibrary.PaddingMd, (int)HudThemeLibrary.PaddingMd,
                    (int)HudThemeLibrary.PaddingSm, (int)HudThemeLibrary.PaddingSm),
                false, TextAnchor.MiddleLeft);

            Text title = UiFactory.CreateText(header, "Title", "VEVE  //  PERSONALIZATION",
                HudThemeLibrary.FontSubhead, HudThemeLibrary.OliveBright, TextAnchor.MiddleLeft,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0.5f),
                new Vector2(280f, 36f), Vector2.zero);
            _ = title;

            foreach (string tab in TabNames)
            {
                string captured = tab;
                Button b = UiFactory.CreateTableButton(header, "Tab_" + tab, tab.ToUpperInvariant(),
                    HudThemeLibrary.ButtonNormal, HudThemeLibrary.TextOnDark,
                    HudThemeLibrary.FontBody, new Vector2(150f, 36f));
                b.onClick.AddListener(() => SetActiveTab(captured));
                _tabButtonBGs[tab] = b.GetComponent<Image>();
            }

            Button back = UiFactory.CreateTableButton(header, "Back", "< BACK (ESC)",
                HudThemeLibrary.OliveDim, HudThemeLibrary.TextOnDark,
                HudThemeLibrary.FontBody, new Vector2(170f, 36f));
            back.onClick.AddListener(GoBack);

            // Body: hosts stacked full-size, one per tab; layouts live on children of hosts.
            RectTransform body = new GameObject("Body", typeof(RectTransform)).transform as RectTransform;
            body.SetParent(bg.transform, false);
            body.anchorMin = new Vector2(0f, 0f);
            body.anchorMax = new Vector2(1f, 1f);
            body.offsetMin = new Vector2(4f, 8f);
            body.offsetMax = new Vector2(-4f, -70f);

            _operatorPanel = AddPanel<OperatorPanel>(body, TabOperator, out RectTransform opHost);
            _weaponPanel = AddPanel<WeaponCustomizationPanel>(body, TabWeapon, out RectTransform wpHost);
            _gearPanel = AddPanel<GearPanel>(body, TabGear, out RectTransform gearHost);
            _finishesPanel = AddPanel<FinishesPanel>(body, TabFinishes, out RectTransform finHost);
            _zeroingPanel = AddPanel<ZeroingPanel>(body, TabZeroing, out RectTransform zeroHost);

            _operatorPanel.Build(opHost);
            _weaponPanel.Build(wpHost);
            _gearPanel.Build(gearHost);
            _finishesPanel.Build(finHost);
            _zeroingPanel.Build(zeroHost);

            _weaponPanel.OnAttachmentChanged += HandleAttachmentChanged;
            _weaponPanel.OnWeaponSelected += HandleWeaponSelected;
            _gearPanel.OnSlotChanged += HandleGearSlotChanged;
            _operatorPanel.OnOperatorApplied += HandleOperatorApplied;
            _finishesPanel.OnFinishApplied += HandleFinishApplied;

            _tabHosts[TabOperator] = opHost;
            _tabHosts[TabWeapon] = wpHost;
            _tabHosts[TabGear] = gearHost;
            _tabHosts[TabFinishes] = finHost;
            _tabHosts[TabZeroing] = zeroHost;

            SetActiveTab(_currentTab);
        }

        private static T AddPanel<T>(RectTransform body, string tab, out RectTransform host) where T : MonoBehaviour
        {
            host = new GameObject("Tab_" + tab, typeof(RectTransform)).transform as RectTransform;
            host.SetParent(body, false);
            UiFactory.StretchFull(host);
            host.gameObject.SetActive(false);
            return host.gameObject.AddComponent<T>();
        }

        // ------------------------------------------------------------------ public API

        /// <summary>Case-insensitive tab switch. Returns false for unknown tab names.</summary>
        public bool SetActiveTab(string tab)
        {
            if (string.IsNullOrEmpty(tab))
                return false;
            string canonical = null;
            foreach (string candidate in TabNames)
            {
                if (string.Equals(candidate, tab, StringComparison.OrdinalIgnoreCase))
                {
                    canonical = candidate;
                    break;
                }
            }
            if (canonical == null)
                return false;

            _currentTab = canonical;
            foreach (KeyValuePair<string, RectTransform> pair in _tabHosts)
            {
                if (pair.Value != null)
                    pair.Value.gameObject.SetActive(pair.Key == canonical);
                if (_tabButtonBGs.TryGetValue(pair.Key, out Image img) && img != null)
                {
                    img.color = pair.Key == canonical
                        ? HudThemeLibrary.SlotSelected
                        : HudThemeLibrary.ButtonNormal;
                }
            }
            Refresh();
            return true;
        }

        /// <summary>Refreshes the active tab from the current bound data sources.</summary>
        public void Refresh()
        {
            switch (_currentTab)
            {
                case TabWeapon:
                    _weaponPanel?.Refresh();
                    break;
                case TabOperator:
                    _operatorPanel?.Refresh();
                    break;
                case TabGear:
                    _gearPanel?.Refresh();
                    break;
                case TabFinishes:
                    _finishesPanel?.Refresh();
                    break;
                case TabZeroing:
                    _zeroingPanel?.Update();
                    break;
            }
        }

        /// <summary>Header back action; routes through the flow controller when present.</summary>
        public void GoBack()
        {
            if (CloseOpenDetail())
                return;
            if (flowController != null)
                flowController.Transition(MainMenuFlowController.MenuFlowState.Main);
            else
                gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ data binding seams

        /// <summary>Wires the operator roster bridge (VEVE.Operator side implements the seam).</summary>
        public void BindOperators(IOperatorRosterSource roster)
        {
            _operatorPanel?.Bind(roster);
        }

        /// <summary>Wires gear catalogue + load totals (VEVE.Gear side implements the seams).</summary>
        public void BindGear(IGearRosterSource roster, IGearLoadoutPresenter presenter)
        {
            _gearPanel?.SetSources(roster, presenter);
        }

        /// <summary>Shares the live WeaponCustomizationManager instance (nil = panel-local default).</summary>
        public void BindWeaponManager(VEVE.Customization.WeaponCustomizationManager manager)
        {
            if (_weaponPanel != null)
                _weaponPanel.Manager = manager;
        }

        public void BindFinishes(IFinishApplyTarget target)
        {
            _finishesPanel?.Bind(target);
        }

        public void BindZeroing(IZeroingProvider provider)
        {
            if (_zeroingPanel != null)
                _zeroingPanel.Provider = provider;
        }

        /// <summary>Attaches (and immediately loads from) a state store; null detaches.</summary>
        public void BindStateStore(PersonalizationStateStore store)
        {
            _store = store;
            if (_store == null)
                return;
            if (_store.Load())
            {
                Selection = _store.Selection;
                if (Selection.weaponId != null
                    && IconicWeaponCatalog.TryGet(Selection.weaponId, out WeaponSpec spec))
                {
                    _zeroingPanel?.BindWeapon(spec);
                }
            }
            else
            {
                _store.Adopt(Selection);
            }
        }

        /// <summary>Persists the current selection when a store is attached.</summary>
        public bool SaveSelection()
        {
            if (_store == null)
                Selection.Migrate();
            else
                _store.Adopt(Selection);
            return _store != null && _store.Save();
        }

        // ------------------------------------------------------------------ selection events

        private void HandleWeaponSelected(string weaponId)
        {
            Selection.weaponId = weaponId;
            if (IconicWeaponCatalog.TryGet(weaponId, out WeaponSpec spec))
                _zeroingPanel?.BindWeapon(spec);
            Persist();
        }

        private void HandleAttachmentChanged(string slotKey, string attachmentId)
        {
            Selection.SetAttachment(slotKey, attachmentId);
            Persist();
        }

        private void HandleGearSlotChanged(string slotKey, string gearId)
        {
            Selection.SetGear(slotKey, gearId);
            Persist();
        }

        private void HandleOperatorApplied(OperatorCardData op)
        {
            Selection.operatorId = op.Id ?? string.Empty;
            Persist();
        }

        private void HandleFinishApplied(FinishDefinition finish)
        {
            Selection.finishId = finish.id ?? string.Empty;
            Persist();
        }

        private void Persist()
        {
            if (autoSave)
                SaveSelection();
        }
    }
}
