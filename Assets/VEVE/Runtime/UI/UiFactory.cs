using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VEVE.UI
{
    /// <summary>
    /// Runtime factory for legacy uGUI (UnityEngine.UI) elements. Produces RectTransform-driven
    /// panels, text, images, sliders, buttons and layout groups; no prefabs or scene assets.
    /// Font sizes respect the scene <see cref="AccessibilitySettings"/> (TextScale) and are always
    /// clamped to the readable range. High-DPI safe: canvases use ScaleWithScreenSize against the
    /// same 1920x1080 baseline UIManager configures.
    /// </summary>
    public static class UiFactory
    {
        public const int MinReadableFont = 12;
        public const int MaxReadableFont = 72;

        private static Font cachedFont;
        private static AccessibilitySettings cachedAccessibility;
        private static Sprite cachedSolid;
        private static Sprite cachedRadial;
        private static Sprite cachedVignette;

        public static readonly Color SolidWhite = new Color(1f, 1f, 1f, 1f);

        // ---------------------------------------------------------------- fonts

        public static Font DefaultFont
        {
            get
            {
                if (cachedFont != null)
                    return cachedFont;
                cachedFont = TryLoadFont("LegacyRuntime.ttf") ?? TryLoadFont("Arial.ttf");
                return cachedFont;
            }
        }

        private static Font TryLoadFont(string resourceName)
        {
            try
            {
                return Resources.GetBuiltinResource<Font>(resourceName);
            }
            catch (System.Exception)
            {
                return null;
            }
        }

        public static AccessibilitySettings ActiveAccessibility
        {
            get
            {
                if (cachedAccessibility == null)
                    cachedAccessibility = Object.FindFirstObjectByType<AccessibilitySettings>();
                return cachedAccessibility;
            }
        }

        /// <summary>Drops the scene AccessibilitySettings lookup (used by tests / hot reload).</summary>
        public static void ClearAccessibilityCache()
        {
            cachedAccessibility = null;
        }

        /// <summary>
        /// Scales a theme font size by the scene AccessibilitySettings.TextScale and clamps the
        /// result into the accessible readability window [12, 72] (same ceiling as
        /// AccessibilitySettings.SubtitleSize).
        /// </summary>
        public static int ScaleFontSize(int baseSize)
        {
            AccessibilitySettings settings = ActiveAccessibility;
            float scale = settings != null ? settings.TextScale : 1f;
            int size = Mathf.RoundToInt(Mathf.Max(0, baseSize) * Mathf.Clamp(scale, 0.25f, 4f));
            return Mathf.Clamp(size, MinReadableFont, MaxReadableFont);
        }

        public static int ScaleFontSize(AccessibilitySettings settings, int baseSize)
        {
            float scale = settings != null ? settings.TextScale : 1f;
            int size = Mathf.RoundToInt(Mathf.Max(0, baseSize) * Mathf.Clamp(scale, 0.25f, 4f));
            return Mathf.Clamp(size, MinReadableFont, MaxReadableFont);
        }

        // ---------------------------------------------------------------- canvas

        /// <summary>
        /// Creates a screen-space overlay canvas with a high-DPI CanvasScaler matching the
        /// UIManager reference resolution. Also ensures an EventSystem exists for clicks.
        /// </summary>
        public static Canvas CreateCanvas(string name, int sortOrder = 0,
            CanvasScaler.ScaleMode scaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize)
        {
            GameObject root = new GameObject(string.IsNullOrEmpty(name) ? "UiCanvas" : name);
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;

            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = scaleMode;
            scaler.referenceResolution = new Vector2(
                HudThemeLibrary.ReferenceWidth, HudThemeLibrary.ReferenceHeight);
            scaler.matchWidthOrHeight = HudThemeLibrary.MatchWidthOrHeight;

            root.AddComponent<GraphicRaycaster>();
            EnsureEventSystem(root);
            return canvas;
        }

        /// <summary>
        /// Creates an EventSystem under <paramref name="hostRoot"/> when the scene lacks one.
        /// Returns the active instance. Host root is required so test/tooling scenes can clean up.
        /// </summary>
        public static EventSystem EnsureEventSystem(GameObject hostRoot)
        {
            if (hostRoot == null)
                return EventSystem.current;
            if (EventSystem.current != null)
                return EventSystem.current;
            EventSystem created = hostRoot.AddComponent<EventSystem>();
            created.firstSelectedGameObject = null;
            if (created.GetComponent<StandaloneInputModule>() == null)
                created.gameObject.AddComponent<StandaloneInputModule>();
            return EventSystem.current != null ? EventSystem.current : created;
        }

        // ---------------------------------------------------------------- primitives

        /// <summary>
        /// Full-bleed tinted panel anchored to its parent. Parent may be null (creates a root rect).
        /// </summary>
        public static Image CreatePanel(Component parent, string name, Color color)
        {
            Image image = CreateImage(parent, name, color, Image.Type.Simple,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            return image;
        }

        public static Image CreatePanel(Component parent, string name, Vector2 sizeDelta, Color color)
        {
            return CreateImage(parent, name, color, Image.Type.Simple,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                sizeDelta, Vector2.zero);
        }

        public static Image CreateImage(Component parent, string name, Color color,
            Image.Type type = Image.Type.Simple,
            Vector2 anchorMin = default, Vector2 anchorMax = default,
            Vector2 pivot = default, Vector2 sizeDelta = default, Vector2 anchoredPos = default)
        {
            GameObject go = NewUiObject(name, ResolveParent(parent));
            Image image = go.AddComponent<Image>();
            image.sprite = GetSolidSprite();
            image.color = Sanitize(color);
            image.type = type;
            RectTransform rect = image.rectTransform;
            if (sizeDelta == Vector2.zero && anchorMin == Vector2.zero && anchorMax == Vector2.zero)
            {
                StretchFull(rect);
            }
            else
            {
                rect.anchorMin = anchorMin;
                rect.anchorMax = anchorMax;
                rect.pivot = pivot == Vector2.zero ? new Vector2(0.5f, 0.5f) : pivot;
                rect.sizeDelta = sizeDelta;
                rect.anchoredPosition = anchoredPos;
            }
            return image;
        }

        /// <summary>
        /// Legacy <see cref="Text"/> element. baseFontSize is accessibility-scaled and clamped.
        /// </summary>
        public static Text CreateText(Component parent, string name, string content, int baseFontSize,
            Color color, TextAnchor alignment = TextAnchor.MiddleCenter,
            Vector2 anchorMin = default, Vector2 anchorMax = default,
            Vector2 pivot = default, Vector2 sizeDelta = default, Vector2 anchoredPos = default)
        {
            RectTransform container = ResolveParent(parent);
            GameObject go = NewUiObject(name, container);
            Text text = go.AddComponent<Text>();
            text.font = DefaultFont;
            text.fontSize = ScaleFontSize(baseFontSize);
            text.text = content ?? string.Empty;
            text.color = Sanitize(color);
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            RectTransform rect = text.rectTransform;
            if (sizeDelta == Vector2.zero && anchorMin == Vector2.zero && anchorMax == Vector2.zero)
                StretchFull(rect);
            else
            {
                rect.anchorMin = anchorMin;
                rect.anchorMax = anchorMax;
                rect.pivot = pivot == Vector2.zero ? new Vector2(0.5f, 0.5f) : pivot;
                rect.sizeDelta = sizeDelta;
                rect.anchoredPosition = anchoredPos;
            }
            return text;
        }

        public static Slider CreateSlider(Component parent, string name, Color trackColor,
            Color fillColor, Vector2 sizeDelta, Vector2 anchoredPos, float value = 0f)
        {
            Image track = CreateImage(parent, name, trackColor, Image.Type.Simple,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                sizeDelta, anchoredPos);

            Image fillArea = CreateImage(track, "Fill Area", SolidWhite, Image.Type.Simple);
            fillArea.sprite = null;
            fillArea.color = new Color(1f, 1f, 1f, 0f);
            StretchFull(fillArea.rectTransform);
            RectTransform fillRect = fillArea.rectTransform;
            fillRect.offsetMin = new Vector2(2f, 2f);
            fillRect.offsetMax = new Vector2(-2f, -2f);

            Image fill = CreateImage(fillArea, "Fill", fillColor, Image.Type.Simple);
            fill.sprite = GetSolidSprite();
            RectTransform fillRectT = fill.rectTransform;
            fillRectT.anchorMin = new Vector2(0f, 0f);
            fillRectT.anchorMax = new Vector2(0f, 1f);
            fillRectT.pivot = new Vector2(0f, 0.5f);
            fillRectT.offsetMin = new Vector2(0f, 0f);
            fillRectT.sizeDelta = new Vector2(sizeDelta.x > 0f ? sizeDelta.x : 100f, 0f);

            Slider slider = track.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.interactable = false;
            slider.value = Mathf.Clamp01(value);
            return slider;
        }

        /// <summary>
        /// Themed button: solid background + centered legacy label. Click handling is left to the
        /// caller via <c>onClick</c> (inventory slots reuse this as select events).
        /// </summary>
        public static Button CreateTableButton(Component parent, string name, string label,
            Color background, Color labelColor, int baseFontSize = HudThemeLibrary.FontSubhead,
            Vector2 sizeDelta = default)
        {
            Image bg = CreateImage(parent, name, background, Image.Type.Simple,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                sizeDelta, Vector2.zero);

            ColorBlock colors = new ColorBlock
            {
                normalColor = SolidWhite,
                highlightedColor = new Color(1.18f, 1.24f, 1.08f, 1f),
                pressedColor = new Color(0.62f, 0.58f, 0.47f, 1f),
                selectedColor = SolidWhite,
                disabledColor = new Color(0.58f, 0.61f, 0.52f, 0.62f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };

            Button button = bg.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            button.colors = colors;

            Text child = CreateText(bg, "Label", label, baseFontSize, labelColor);
            child.raycastTarget = false;
            return button;
        }

        // ---------------------------------------------------------------- layout

        public static HorizontalLayoutGroup CreateHLayout(Component container, float spacing,
            RectOffset padding = null, bool controlChildSize = true,
            TextAnchor childAlignment = TextAnchor.MiddleLeft)
        {
            return ApplyLayout(RequireParent(container).gameObject.AddComponent<HorizontalLayoutGroup>(),
                spacing, padding, controlChildSize, childAlignment);
        }

        public static VerticalLayoutGroup CreateVLayout(Component container, float spacing,
            RectOffset padding = null, bool controlChildSize = true,
            TextAnchor childAlignment = TextAnchor.UpperLeft)
        {
            return ApplyLayout(RequireParent(container).gameObject.AddComponent<VerticalLayoutGroup>(),
                spacing, padding, controlChildSize, childAlignment);
        }

        private static RectTransform RequireParent(Component container)
        {
            return ResolveParent(container)
                ?? new GameObject("DetachedLayoutRoot", typeof(RectTransform)).GetComponent<RectTransform>();
        }

        private static T ApplyLayout<T>(T group, float spacing, RectOffset padding,
            bool controlChildSize, TextAnchor alignment) where T : HorizontalOrVerticalLayoutGroup
        {
            if (group == null || (group as Component) == null)
                throw new System.InvalidOperationException(
                    $"{typeof(T).Name} could not be attached: the container already has another " +
                    "Horizontal/Vertical layout group (uGUI forbids multiple HorizontalOrVerticalLayoutGroup " +
                    "components on one GameObject). Use separate child containers instead.");

            group.spacing = Mathf.Max(0f, spacing);
            group.padding = padding ?? new RectOffset(
                (int)HudThemeLibrary.PaddingSm, (int)HudThemeLibrary.PaddingSm,
                (int)HudThemeLibrary.PaddingSm, (int)HudThemeLibrary.PaddingSm);
            group.childControlWidth = controlChildSize;
            group.childControlHeight = controlChildSize;
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = false;
            group.childAlignment = alignment;
            return group;
        }

        public static GridLayoutGroup CreateGrid(Component container, Vector2 cellSize, Vector2 spacing,
            int columns = 6)
        {
            GridLayoutGroup grid = RequireParent(container).gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = cellSize;
            grid.spacing = spacing;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = Mathf.Max(1, columns);
            return grid;
        }

        /// <summary>
        /// Builds a vertical scroll area (viewport mask + content + ScrollRect) under the given
        /// parent; the viewport stretches to fill the parent rect.
        /// </summary>
        public static ScrollRect CreateScrollRect(Component parent, out RectTransform content)
        {
            RectTransform parentRect = ResolveParent(parent)
                ?? new GameObject("ScrollArea", typeof(RectTransform)).GetComponent<RectTransform>();

            Image mask = CreateImage(parentRect, "Viewport", new Color(1f, 1f, 1f, 0f));
            mask.sprite = null;
            StretchFull(mask.rectTransform);
            mask.raycastTarget = true;
            mask.gameObject.AddComponent<RectMask2D>();

            content = new GameObject("Content", typeof(RectTransform)).transform as RectTransform;
            content.SetParent(mask.rectTransform, false);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = new Vector2(0f, 0f);

            ScrollRect scroll = mask.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = mask.rectTransform;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;
            return scroll;
        }

        public static LayoutElement SetMinSize(GameObject target, float x, float y)
        {
            LayoutElement element = target.GetComponent<LayoutElement>();
            if (element == null)
                element = target.AddComponent<LayoutElement>();
            element.minWidth = x;
            element.minHeight = y;
            element.preferredWidth = x;
            element.preferredHeight = y;
            return element;
        }

        // ---------------------------------------------------------------- sprites

        public static void StretchFull(RectTransform rect)
        {
            if (rect == null) return;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public static Sprite GetSolidSprite()
        {
            if (cachedSolid == null)
            {
                Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                tex.name = "VEVE_UiSolid";
                Color32[] pixels = new Color32[16];
                for (int i = 0; i < pixels.Length; i++)
                    pixels[i] = new Color32(255, 255, 255, 255);
                tex.SetPixels32(pixels);
                tex.Apply();
                cachedSolid = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 32f);
            }
            return cachedSolid;
        }

        /// <summary>Soft disc for radial filled gauges (stamina / vitals arcs, damage ring).</summary>
        public static Sprite GetRadialSprite()
        {
            if (cachedRadial == null)
            {
                const int size = 128;
                Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                tex.name = "VEVE_UiRadial";
                float half = size * 0.5f;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x + 0.5f - half) / half;
                        float dy = (y + 0.5f - half) / half;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        byte a = (byte)(Mathf.Clamp01(1.04f - d) * 255f);
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, a / 255f));
                    }
                }
                tex.Apply();
                cachedRadial = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f);
            }
            return cachedRadial;
        }

        /// <summary>Edge-darkening ramp for the low-health vignette (transparent center).</summary>
        public static Sprite GetVignetteSprite()
        {
            if (cachedVignette == null)
            {
                const int size = 64;
                Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                tex.name = "VEVE_UiVignette";
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        Vector2 uv = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
                        float d = Vector2.Distance(uv, new Vector2(0.5f, 0.5f)) * 2f;
                        float a = Mathf.Clamp01((d - 0.55f) / 0.45f);
                        a = a * a;
                        tex.SetPixel(x, y, new Color(0.35f, 0.02f, 0.02f, a));
                    }
                }
                tex.Apply();
                cachedVignette = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f);
            }
            return cachedVignette;
        }

        /// <summary>Radial filled gauge helper (uses Image.Type.Filled with a generated disc).</summary>
        public static Image CreateRadialArc(Component parent, string name, Color color, float fillAmount)
        {
            Image arc = CreateImage(parent, name, Sanitize(color), Image.Type.Filled,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(90f, 90f), Vector2.zero);
            arc.sprite = GetRadialSprite();
            arc.fillMethod = Image.FillMethod.Radial360;
            arc.fillOrigin = (int)Image.Origin360.Top;
            arc.fillClockwise = false;
            arc.fillAmount = Mathf.Clamp01(fillAmount);
            return arc;
        }

        // ---------------------------------------------------------------- reflection

        /// <summary>
        /// Names of serialized/public instance fields on a component (used to coordinate with the
        /// existing HUDController and read snapshot accessors without editing source files).
        /// </summary>
        public static HashSet<string> GetSerializedFieldNames(Object target)
        {
            HashSet<string> names = new HashSet<string>();
            if (target == null)
                return names;
            FieldInfo[] fields = target.GetType().GetFields(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (FieldInfo field in fields)
            {
                if (field.IsStatic || field.Name.StartsWith("<"))
                    continue;
                bool serialized = field.IsPublic
                    || field.GetCustomAttribute<SerializeField>() != null;
                if (serialized)
                    names.Add(field.Name);
            }
            return names;
        }

        // ---------------------------------------------------------------- helpers

        private static RectTransform ResolveParent(Component parent)
        {
            if (parent == null)
                return null;
            if (parent is RectTransform rect)
                return rect;
            if (parent is Graphic graphic)
                return graphic.rectTransform;
            return parent.GetComponent<RectTransform>();
        }

        private static GameObject NewUiObject(string name, Transform parent)
        {
            GameObject go = new GameObject(
                string.IsNullOrEmpty(name) ? "UiElement" : name, typeof(RectTransform));
            RectTransform rect = go.GetComponent<RectTransform>();
            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0)
                go.layer = uiLayer;
            rect.SetParent(parent, false);
            return go;
        }

        private static Color Sanitize(Color color)
        {
            return new Color(
                Mathf.Clamp01(color.r),
                Mathf.Clamp01(color.g),
                Mathf.Clamp01(color.b),
                Mathf.Clamp01(color.a));
        }
    }
}
