using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace VEVE.UI
{
    /// <summary>
    /// Code-built grid inventory screen bound to <see cref="PhysicalInventory"/>. Shows weight
    /// bars per item category, a capacity meter, and item detail via click-select slots
    /// (drag semantics are realized as select events; no scene assets required).
    ///
    /// Binding note: PhysicalInventory currently exposes capacity/mass/volume through public
    /// properties and writes only through TryAdd. For read-only presentation the controller
    /// first probes for a public snapshot accessor (Items / Snapshot / GetItems) via reflection,
    /// then falls back to the serialized item list. PhysicalInventory itself is never mutated
    /// and its source is untouched. If Items is later added as a public getter, binding upgrades
    /// automatically with no UI changes.
    /// </summary>
    public sealed class InventoryUIController : MonoBehaviour
    {
        [Header("Bindings (auto-discovered when empty)")]
        [SerializeField] private PhysicalInventory inventory;
        [SerializeField] private UIManager uiManager;
        [SerializeField] private int columns = 6;
        [SerializeField] private int maxVisibleSlots = 18;
        [SerializeField] private int maxCategoryBars = 6;

        private const float RefreshInterval = 0.4f;

        private Canvas uiCanvas;
        private RectTransform rootRect;
        private RectTransform gridRect;
        private Slider capacityMeter;
        private Text capacityText;
        private Text massText;
        private Text volumeText;
        private Text detailText;
        private Text overflowText;
        private RectTransform categoryRoot;

        private readonly List<Image> slotHighlights = new List<Image>();
        private readonly List<InventoryItem> snapshot = new List<InventoryItem>();

        private int selectedIndex = -1;
        private float refreshTimer;
        private bool built;

        public PhysicalInventory BoundInventory => inventory;
        public int SelectedIndex => selectedIndex;

        private void Awake()
        {
            if (inventory == null)
                inventory = Object.FindFirstObjectByType<PhysicalInventory>();
            if (uiManager == null)
                uiManager = Object.FindFirstObjectByType<UIManager>();
            EnsureBuilt();
            Refresh();
        }

        private void Update()
        {
            if (!built)
                return;
            if (Input.GetKeyDown(KeyCode.Escape) && uiManager == null)
                ToggleScreen();
            if (Input.GetKeyDown(KeyCode.I))
                ToggleScreen();

            refreshTimer -= Time.unscaledDeltaTime;
            if (refreshTimer <= 0f)
            {
                refreshTimer = RefreshInterval;
                RefreshMeters();
            }
        }

        private void ToggleScreen()
        {
            if (uiManager != null)
            {
                if (uiManager.CurrentState == UIState.Playing)
                    uiManager.OpenInventory();
                else if (uiManager.CurrentState == UIState.Inventory)
                    uiManager.CloseInventory();
                if (uiManager.CurrentState != UIState.Inventory && gameObject.activeSelf)
                    SetVisible(false);
                else
                    SetVisible(true);
                return;
            }
            SetVisible(!gameObject.activeSelf);
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
            if (visible)
                EnsureBuilt();
        }

        private void EnsureBuilt()
        {
            if (built)
                return;
            built = true;

            uiCanvas = UiFactory.CreateCanvas("InventoryUI", 90);
            GameObject rootGO = new GameObject("Root", typeof(RectTransform));
            rootGO.transform.SetParent(uiCanvas.transform, false);
            rootGO.layer = uiCanvas.gameObject.layer;
            rootRect = rootGO.GetComponent<RectTransform>();
            UiFactory.StretchFull(rootRect);

            Image backdrop = UiFactory.CreatePanel(null, "Backdrop", new Color(0.02f, 0.024f, 0.018f, 0.78f));
            backdrop.transform.SetParent(rootRect, false);

            Image panel = UiFactory.CreatePanel(null, "LogisticsPanel", new Vector2(1060f, 680f),
                HudThemeLibrary.PanelBackground);
            panel.transform.SetParent(rootRect, false);

            Text title = UiFactory.CreateText(panel, "Title", "LOGISTICS // FIELD LOADOUT",
                HudThemeLibrary.FontSubhead + 6, HudThemeLibrary.TextPrimary);
            UiFactory.StretchFull(title.rectTransform);
            title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0f, 1f);
            title.rectTransform.pivot = new Vector2(0f, 1f);
            title.rectTransform.offsetMin = new Vector2(20f, -56f);
            title.rectTransform.offsetMax = new Vector2(-170f, -14f);
            title.alignment = TextAnchor.UpperLeft;
            title.horizontalOverflow = HorizontalWrapMode.Overflow;

            Button close = UiFactory.CreateTableButton(panel, "Close", "X  CLOSE (I)",
                HudThemeLibrary.ButtonNormal, HudThemeLibrary.TextOnDark, 20, new Vector2(140f, 36f));
            close.transform.SetParent(panel.rectTransform, false);
            RectTransform closeRect = (RectTransform)close.transform;
            closeRect.anchorMin = closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-16f, -12f);
            close.onClick.AddListener(OnClose);

            if (inventory == null)
            {
                UiFactory.CreateText(panel, "NoLink", "NO LOGISTICS LINK ESTABLISHED",
                    22, HudThemeLibrary.Amber, TextAnchor.MiddleCenter,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(520f, 80f), Vector2.zero);
                return;
            }

            // ---- Item grid (left) ----
            Image gridBG = UiFactory.CreatePanel(null, "GridBG", new Vector2(560f, 500f),
                HudThemeLibrary.PanelInset);
            gridBG.transform.SetParent(panel.rectTransform, false);
            RectTransform gridRT = gridBG.rectTransform;
            gridRT.anchorMin = gridRT.anchorMax = new Vector2(0f, 0.5f);
            gridRT.pivot = new Vector2(0f, 0.5f);
            gridRT.anchoredPosition = new Vector2(16f, -20f);
            GridLayoutGroup grid = gridBG.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(HudThemeLibrary.SlotCellSize, HudThemeLibrary.SlotCellSize);
            grid.spacing = new Vector2(HudThemeLibrary.SlotSpacing, HudThemeLibrary.SlotSpacing);
            grid.padding = new RectOffset(10, 10, 10, 10);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = Mathf.Max(3, columns);
            gridRect = gridBG.rectTransform;

            overflowText = UiFactory.CreateText(panel, "Overflow", string.Empty, 16,
                HudThemeLibrary.TextMuted, TextAnchor.UpperLeft,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(560f, 24f), new Vector2(20f, 8f));

            // ---- Meters (right) ----
            Image meterBG = UiFactory.CreatePanel(null, "MetersBG", new Vector2(430f, 500f),
                HudThemeLibrary.PanelSurface);
            meterBG.transform.SetParent(panel.rectTransform, false);
            RectTransform meterRT = meterBG.rectTransform;
            meterRT.anchorMin = meterRT.anchorMax = new Vector2(1f, 0.5f);
            meterRT.pivot = new Vector2(1f, 0.5f);
            meterRT.anchoredPosition = new Vector2(-16f, -20f);

            UiFactory.CreateText(meterBG, "CapHeader", "CAPACITY", 18, HudThemeLibrary.TextSecondary,
                TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-24f, -16f), Vector2.zero);
            capacityMeter = UiFactory.CreateSlider(meterBG, "Capacity", HudThemeLibrary.SliderTrack,
                HudThemeLibrary.OliveBright, new Vector2(390f, 14f), new Vector2(0f, -52f));
            capacityText = UiFactory.CreateText(meterBG, "CapacityValue", "0%", 16,
                HudThemeLibrary.TextPrimary, TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-24f, -22f), new Vector2(0f, -6f));
            massText = UiFactory.CreateText(meterBG, "MassValue", string.Empty, 16,
                HudThemeLibrary.TextSecondary, TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-24f, -22f), Vector2.zero);
            volumeText = UiFactory.CreateText(meterBG, "VolumeValue", string.Empty, 16,
                HudThemeLibrary.TextSecondary, TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-24f, -22f), Vector2.zero);
            SetChildRow(massText.rectTransform, -92f);
            SetChildRow(volumeText.rectTransform, -116f);
            SetChildRow(capacityText.rectTransform, -68f);

            UiFactory.CreateText(meterBG, "CatHeader", "LOAD BY CATEGORY", 18,
                HudThemeLibrary.TextSecondary, TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-24f, -30f), Vector2.zero);
            categoryRoot = new GameObject("Categories", typeof(RectTransform)).transform as RectTransform;
            categoryRoot.SetParent(meterBG.rectTransform, false);
            categoryRoot.anchorMin = categoryRoot.anchorMax = new Vector2(0f, 1f);
            categoryRoot.pivot = new Vector2(0.5f, 1f);
            categoryRoot.anchoredPosition = new Vector2(20f, -172f);
            categoryRoot.sizeDelta = new Vector2(390f, 220f);
            VerticalLayoutGroup vlayout = categoryRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            vlayout.spacing = 6f;
            vlayout.padding = new RectOffset(0, 0, 0, 0);
            vlayout.childControlWidth = false;
            vlayout.childControlHeight = false;
            vlayout.childForceExpandWidth = true;

            // ---- Detail bar (bottom) ----
            Image detailBG = UiFactory.CreatePanel(null, "DetailBG", new Vector2(1020f, 64f),
                HudThemeLibrary.PanelInset);
            detailBG.transform.SetParent(panel.rectTransform, false);
            RectTransform detailRT = detailBG.rectTransform;
            detailRT.anchorMin = new Vector2(0.5f, 0f);
            detailRT.anchorMax = new Vector2(0.5f, 0f);
            detailRT.pivot = new Vector2(0.5f, 0f);
            detailRT.anchoredPosition = new Vector2(0f, 14f);
            detailText = UiFactory.CreateText(detailBG, "Detail", "NO ITEM SELECTED",
                HudThemeLibrary.FontBody, HudThemeLibrary.TextPrimary);
            detailText.alignment = TextAnchor.MiddleLeft;
            detailText.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        private static void SetChildRow(RectTransform rect, float yOffsetFromTop)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(-24f, 22f);
            rect.anchoredPosition = new Vector2(0f, yOffsetFromTop);
        }

        private void OnClose()
        {
            if (uiManager != null)
                uiManager.CloseInventory();
            else
                SetVisible(false);
        }

        public void Refresh()
        {
            RefreshMeters();
            RebuildGrid();
        }

        private void RebuildGrid()
        {
            if (!built || gridRect == null)
                return;

            foreach (GameObject child in CollectChildren(gridRect))
                Destroy(child);
            slotHighlights.Clear();

            snapshot.Clear();
            snapshot.AddRange(GetItemsSnapshot(inventory));
            int shown = Mathf.Min(snapshot.Count, Mathf.Max(1, maxVisibleSlots));
            for (int i = 0; i < shown; i++)
            {
                int slotIndex = i;
                InventoryItem item = snapshot[i];

                Image slotBG = UiFactory.CreateImage(null, "Slot" + i, HudThemeLibrary.SlotNormal,
                    Image.Type.Simple);
                slotBG.transform.SetParent(gridRect, false);
                Button button = slotBG.gameObject.AddComponent<Button>();
                button.targetGraphic = slotBG;
                ColorBlock cb = button.colors;
                cb.normalColor = Color.white;
                cb.highlightedColor = new Color(1.2f, 1.25f, 1.1f, 1f);
                cb.pressedColor = new Color(0.7f, 0.72f, 0.65f, 1f);
                button.colors = cb;
                button.onClick.AddListener(() => SelectSlot(slotIndex));

                Text label = UiFactory.CreateText(slotBG, "Id", ShortId(item.id),
                    HudThemeLibrary.FontCaption, HudThemeLibrary.TextOnDark);
                label.alignment = TextAnchor.UpperLeft;
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                label.verticalOverflow = VerticalWrapMode.Overflow;

                Text qty = UiFactory.CreateText(slotBG, "Qty", "x" + Mathf.Max(1, item.quantity),
                    HudThemeLibrary.FontCaption, HudThemeLibrary.Amber);
                qty.alignment = TextAnchor.LowerRight;
                RectTransform qtyRect = qty.rectTransform;
                qtyRect.anchorMin = qtyRect.anchorMax = new Vector2(1f, 0f);
                qtyRect.pivot = new Vector2(1f, 0f);
                qtyRect.anchoredPosition = new Vector2(-6f, 4f);
                qtyRect.sizeDelta = new Vector2(48f, 18f);

                Image highlight = UiFactory.CreateImage(slotBG, "Highlight", HudThemeLibrary.AmberDim,
                    Image.Type.Simple);
                highlight.transform.SetParent(slotBG.rectTransform, false);
                UiFactory.StretchFull(highlight.rectTransform);
                highlight.rectTransform.offsetMin = new Vector2(2f, 2f);
                highlight.rectTransform.offsetMax = new Vector2(-2f, -2f);

                Color iconTint = item.accessible ? Color.white : HudThemeLibrary.TextMuted;
                slotBG.color = iconTint;

                slotHighlights.Add(highlight);
                highlight.enabled = false;
            }

            if (overflowText != null)
                overflowText.text = snapshot.Count > shown ? "+" + (snapshot.Count - shown) + " more carried" : string.Empty;

            selectedIndex = selectedIndex >= snapshot.Count ? -1 : selectedIndex;
            ApplySelectionVisual();
        }

        private IEnumerable<InventoryItem> GetItemsSnapshot(PhysicalInventory target)
        {
            if (target == null)
                yield break;
            System.Type type = target.GetType();

            PropertyInfo prop = type.GetProperty("Items",
                BindingFlags.Public | BindingFlags.Instance);
            if (prop != null && typeof(IEnumerable<InventoryItem>).IsAssignableFrom(prop.PropertyType))
            {
                if (prop.GetValue(target) is IEnumerable<InventoryItem> publicList)
                {
                    foreach (InventoryItem item in publicList)
                        if (item != null) yield return item;
                    yield break;
                }
            }

            MethodInfo method = type.GetMethod("GetItemsSnapshot",
                BindingFlags.Public | BindingFlags.Instance);
            if (method != null && typeof(IEnumerable<InventoryItem>).IsAssignableFrom(method.ReturnType))
            {
                if (method.Invoke(target, null) is IEnumerable<InventoryItem> result)
                {
                    foreach (InventoryItem item in result)
                        if (item != null) yield return item;
                    yield break;
                }
            }

            FieldInfo field = type.GetField("items",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null && field.FieldType == typeof(List<InventoryItem>))
            {
                if (field.GetValue(target) is List<InventoryItem> backing && backing.Count > 0)
                {
                    for (int i = 0; i < backing.Count; i++)
                        if (backing[i] != null) yield return backing[i];
                }
            }
        }

        private void RefreshMeters()
        {
            if (!built || capacityMeter == null)
                return;
            float ratio = inventory != null ? inventory.LoadRatio : 0f;
            capacityMeter.value = ratio;
            if (capacityText != null)
                capacityText.text = "CAPACITY  " + Mathf.RoundToInt(ratio * 100f) + "%";
            if (massText != null)
                massText.text = "MASS  " + (inventory != null ? inventory.TotalMassKg : 0f).ToString("F1") + " kg";
            if (volumeText != null)
                volumeText.text = "VOLUME  " + (inventory != null ? inventory.UsedVolumeLitres : 0f).ToString("F1")
                    + " / " + (inventory != null ? inventory.CapacityLitres : 0f).ToString("F1") + " L";
            RebuildCategoryBars();
        }

        private void RebuildCategoryBars()
        {
            if (categoryRoot == null)
                return;
            foreach (GameObject child in CollectChildren(categoryRoot))
                Destroy(child);
            snapshot.Clear();
            snapshot.AddRange(GetItemsSnapshot(inventory));

            Dictionary<string, float> categoryMass = new Dictionary<string, float>();
            foreach (InventoryItem item in snapshot)
            {
                string category = CategoryOf(item.id);
                categoryMass.TryGetValue(category, out float mass);
                categoryMass[category] = mass + item.massKg * Mathf.Max(1, item.quantity);
            }

            float max = 0.0001f;
            foreach (KeyValuePair<string, float> pair in categoryMass)
                if (pair.Value > max) max = pair.Value;

            List<KeyValuePair<string, float>> sorted = new List<KeyValuePair<string, float>>(categoryMass);
            sorted.Sort((a, b) => b.Value.CompareTo(a.Value));
            int count = Mathf.Min(sorted.Count, Mathf.Max(1, maxCategoryBars));
            for (int i = 0; i < count; i++)
            {
                Image rowBG = UiFactory.CreateImage(null, "Cat" + i, new Color(1f, 1f, 1f, 0.04f),
                    Image.Type.Simple);
                rowBG.transform.SetParent(categoryRoot, false);
                rowBG.rectTransform.sizeDelta = new Vector2(390f, 30f);

                Text label = UiFactory.CreateText(rowBG, "Name", sorted[i].Key.ToUpperInvariant(),
                    HudThemeLibrary.FontCaption, HudThemeLibrary.TextSecondary);
                label.alignment = TextAnchor.MiddleLeft;
                RectTransform labelRect = label.rectTransform;
                labelRect.anchorMin = labelRect.anchorMax = new Vector2(0f, 0f);
                labelRect.anchorMax = new Vector2(0f, 1f);
                labelRect.pivot = new Vector2(0f, 0.5f);
                labelRect.sizeDelta = new Vector2(96f, 0f);
                labelRect.anchoredPosition = new Vector2(4f, 0f);

                float frac = Mathf.Clamp01(sorted[i].Value / max);
                Image bar = UiFactory.CreateImage(rowBG, "Bar", HudThemeLibrary.Olive,
                    Image.Type.Simple);
                bar.sprite = UiFactory.GetSolidSprite();
                RectTransform barRect = bar.rectTransform;
                barRect.anchorMin = new Vector2(0f, 0f);
                barRect.anchorMax = new Vector2(0f, 1f);
                barRect.pivot = new Vector2(0f, 0.5f);
                barRect.sizeDelta = new Vector2(frac * 260f, -16f);
                barRect.anchoredPosition = new Vector2(110f, 0f);

                if (barRect.sizeDelta.x < 0f) barRect.sizeDelta = new Vector2(0f, 0f);
            }
        }

        public void SelectSlot(int index)
        {
            if (index < 0 || index >= snapshot.Count)
                index = -1;
            selectedIndex = index;
            ApplySelectionVisual();

            if (detailText == null)
                return;
            if (index < 0)
            {
                detailText.text = "NO ITEM SELECTED";
                return;
            }
            InventoryItem item = snapshot[index];
            detailText.text = string.Format("{0}   x{1}   {2:F1} kg   {3:F1} L   {4}",
                item.id ?? "unknown", Mathf.Max(1, item.quantity),
                item.massKg * Mathf.Max(1, item.quantity),
                item.volumeLitres * Mathf.Max(1, item.quantity),
                item.accessible ? "REACHABLE" : "BURIED");
        }

        private void ApplySelectionVisual()
        {
            for (int i = 0; i < slotHighlights.Count; i++)
            {
                if (slotHighlights[i] != null)
                {
                    Image img = slotHighlights[i];
                    img.enabled = i == selectedIndex;
                }
            }
        }

        private static IEnumerable<GameObject> CollectChildren(RectTransform parent)
        {
            List<GameObject> children = new List<GameObject>();
            for (int i = 0; i < parent.childCount; i++)
            {
                GameObject child = parent.GetChild(i).gameObject;
                if (child != null)
                    children.Add(child);
            }
            return children;
        }

        private static string ShortId(string id)
        {
            if (string.IsNullOrEmpty(id))
                return "?";
            id = id.Replace("_", " ");
            return id.Length <= 9 ? id : id.Substring(0, 8) + ".";
        }

        private static string CategoryOf(string id)
        {
            if (string.IsNullOrEmpty(id))
                return "general";
            string lower = id.ToLowerInvariant();
            if (lower.Contains("ammo") || lower.StartsWith("762") || lower.StartsWith("556")
                || lower.StartsWith("9x19") || lower.StartsWith("12g"))
                return "ammo";
            if (lower.Contains("med") || lower.Contains("band") || lower.Contains("morph")
                || lower.Contains("saline"))
                return "medical";
            if (lower.Contains("batt") || lower.Contains("cell") || lower.Contains("radio")
                || lower.Contains("drone"))
                return "power";
            if (lower.Contains("opt") || lower.Contains("sight") || lower.Contains("scope")
                || lower.Contains("thermal"))
                return "optics";
            if (lower.Contains("ration") || lower.Contains("water") || lower.Contains("food"))
                return "sustain";
            int underscore = id.IndexOf('_');
            if (underscore > 0 && underscore < 8)
                return id.Substring(0, underscore).ToLowerInvariant();
            return "general";
        }
    }
}
