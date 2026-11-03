using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EngineTwin
{
    /// <summary>运行时创建 UGUI 控件的小工具（不依赖内置 UI 精灵）</summary>
    public static class UIFactory
    {
        public static TMP_FontAsset Font;

        public static readonly Color PanelColor = new Color(0.055f, 0.075f, 0.10f, 0.88f);
        public static readonly Color CardColor = new Color(0.09f, 0.12f, 0.16f, 0.95f);
        public static readonly Color ButtonColor = new Color(0.14f, 0.19f, 0.26f, 1f);
        public static readonly Color Accent = new Color(0.22f, 0.76f, 1f);
        public static readonly Color TextMain = new Color(0.90f, 0.94f, 0.98f);
        public static readonly Color TextDim = new Color(0.56f, 0.64f, 0.72f);

        const int UILayer = 5;

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = UILayer };
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        public static void Stretch(RectTransform rt, float left = 0, float right = 0, float top = 0, float bottom = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        public static Image Panel(Transform parent, string name, Color color)
        {
            var img = Rect(name, parent).gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        public static TextMeshProUGUI Label(Transform parent, string name, string text, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft, bool bold = false)
        {
            var t = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            if (Font != null) t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.richText = true;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            if (bold) t.fontStyle = FontStyles.Bold;
            return t;
        }

        public static LayoutElement Size(Component c, float width = -1, float height = -1, float flexibleWidth = -1, float flexibleHeight = -1)
        {
            var le = c.GetComponent<LayoutElement>();
            if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
            if (width >= 0) le.preferredWidth = width;
            if (height >= 0) le.preferredHeight = height;
            if (flexibleWidth >= 0) le.flexibleWidth = flexibleWidth;
            if (flexibleHeight >= 0) le.flexibleHeight = flexibleHeight;
            return le;
        }

        public static HorizontalLayoutGroup HLayout(GameObject go, float spacing, RectOffset padding, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var g = go.AddComponent<HorizontalLayoutGroup>();
            g.spacing = spacing;
            g.padding = padding;
            g.childAlignment = align;
            g.childControlWidth = true;
            g.childControlHeight = true;
            g.childForceExpandWidth = false;
            g.childForceExpandHeight = false;
            return g;
        }

        public static VerticalLayoutGroup VLayout(GameObject go, float spacing, RectOffset padding, TextAnchor align = TextAnchor.UpperLeft)
        {
            var g = go.AddComponent<VerticalLayoutGroup>();
            g.spacing = spacing;
            g.padding = padding;
            g.childAlignment = align;
            g.childControlWidth = true;
            g.childControlHeight = true;
            g.childForceExpandWidth = true;
            g.childForceExpandHeight = false;
            return g;
        }

        public static Button Button(Transform parent, string name, string label, Action onClick, float width, float height = 40f)
        {
            Image img = Panel(parent, name, ButtonColor);
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.highlightedColor = new Color(1.35f, 1.35f, 1.35f, 1f);
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            colors.selectedColor = Color.white;
            colors.colorMultiplier = 1.5f;
            btn.colors = colors;
            btn.onClick.AddListener(() => onClick?.Invoke());
            Size(img, width, height);

            var text = Label(img.transform, "Label", label, 17, TextMain, TextAlignmentOptions.Center);
            Stretch(text.rectTransform);
            return btn;
        }

        public static Slider Slider(Transform parent, string name, float width, float min, float max, Action<float> onChange)
        {
            RectTransform root = Rect(name, parent);
            Size(root, width, 32);

            Image bg = Panel(root, "Background", new Color(0.16f, 0.21f, 0.28f, 1f));
            Place(bg.rectTransform, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0, 6));

            RectTransform fillArea = Rect("Fill Area", root);
            Place(fillArea, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-16, 6));
            Image fill = Panel(fillArea, "Fill", Accent);
            fill.rectTransform.sizeDelta = Vector2.zero;

            RectTransform handleArea = Rect("Handle Slide Area", root);
            Stretch(handleArea, 8, 8);
            Image handle = Panel(handleArea, "Handle", Color.white);
            handle.rectTransform.sizeDelta = new Vector2(16, 0);

            var slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.onValueChanged.AddListener(v => onChange?.Invoke(v));
            return slider;
        }

        public static Toggle Toggle(Transform parent, string name, string label, bool isOn, Action<bool> onChange, float width)
        {
            RectTransform root = Rect(name, parent);
            Size(root, width, 40);

            Image box = Panel(root, "Box", new Color(0.16f, 0.21f, 0.28f, 1f));
            Place(box.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(22, 22));
            Image check = Panel(box.transform, "Checkmark", Accent);
            Stretch(check.rectTransform, 4, 4, 4, 4);

            var text = Label(root, "Label", label, 17, TextMain);
            Stretch(text.rectTransform, 30);

            var toggle = root.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = box;
            toggle.graphic = check;
            toggle.isOn = isOn;
            toggle.onValueChanged.AddListener(v => onChange?.Invoke(v));
            return toggle;
        }
    }
}
