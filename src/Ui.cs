using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PhilsPartHider
{
    // Small helpers for building the window out of plain Unity UI pieces.
    static class Ui
    {
        public static RectTransform Node(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        // Placed from the parent's top-left corner, y down, in the parent's units.
        public static RectTransform At(this RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        // Fills the parent, less the given margins.
        public static RectTransform Stretch(this RectTransform rt, float left = 0, float top = 0, float right = 0, float bottom = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static Image Box(RectTransform rt, Sprite sprite, bool raycast = false)
        {
            Image img = rt.gameObject.AddComponent<Image>();
            SetSprite(img, sprite);
            img.raycastTarget = raycast;
            return img;
        }

        public static void SetSprite(Image img, Sprite sprite)
        {
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.color = sprite != null ? Color.white : Color.clear;
        }

        public static Image Solid(RectTransform rt, Color color, bool raycast = false)
        {
            Image img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        public static Text Label(RectTransform rt, string text, Font font, int size, Color color,
            TextAnchor align = TextAnchor.MiddleLeft, FontStyle style = FontStyle.Normal)
        {
            Text t = rt.gameObject.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = align;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.raycastTarget = false;
            t.text = text;
            return t;
        }

        public static RawImage Picture(RectTransform rt, Texture texture, Color color)
        {
            RawImage r = rt.gameObject.AddComponent<RawImage>();
            r.texture = texture;
            r.color = color;
            r.raycastTarget = false;
            return r;
        }

        // A rounded box with only some corners rounded: square patches cover the others.
        [Flags] public enum Corners { None = 0, TopLeft = 1, TopRight = 2, BottomLeft = 4, BottomRight = 8, Top = 3, Bottom = 12, All = 15 }

        public static RectTransform Slab(RectTransform parent, string name, float x, float y, float w, float h, Color color, int radius, Corners round)
        {
            RectTransform rt = Node(parent, name).At(x, y, w, h);
            Box(rt, Look.Round(color, color, radius));
            float r = radius + 1;
            if ((round & Corners.TopLeft) == 0) Solid(Node(rt, "Square").At(0, 0, r, r), color);
            if ((round & Corners.TopRight) == 0) Solid(Node(rt, "Square").At(w - r, 0, r, r), color);
            if ((round & Corners.BottomLeft) == 0) Solid(Node(rt, "Square").At(0, h - r, r, r), color);
            if ((round & Corners.BottomRight) == 0) Solid(Node(rt, "Square").At(w - r, h - r, r, r), color);
            return rt;
        }
    }

    // Tells its owner when the pointer goes over it or leaves. Unity sends these
    // to every parent of the hovered object, so it also sees children's hovers.
    public class HoverRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Action<bool> Changed;
        public bool Over { get; private set; }

        public void OnPointerEnter(PointerEventData e) => Set(true);
        public void OnPointerExit(PointerEventData e) => Set(false);
        void OnDisable() => Set(false);

        void Set(bool over)
        {
            if (over == Over) return;
            Over = over;
            Changed?.Invoke(over);
        }
    }

    // Drags a window around by the object it sits on (the title bar).
    public class DragMove : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public RectTransform Target;
        public bool Dragging { get; private set; }
        Vector2 startPointer, startPosition;

        public void OnBeginDrag(PointerEventData e)
        {
            if (Target == null || !Local(e, out startPointer)) return;
            startPosition = Target.anchoredPosition;
            Dragging = true;
        }

        public void OnDrag(PointerEventData e)
        {
            if (Dragging && Local(e, out Vector2 p)) Target.anchoredPosition = startPosition + (p - startPointer);
        }

        public void OnEndDrag(PointerEventData e) => Dragging = false;

        bool Local(PointerEventData e, out Vector2 p) =>
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)Target.parent, e.position, e.pressEventCamera, out p);
    }

    // A flat button: a background that changes on hover and when selected, a
    // label and/or a symbol colored the same way. No Unity tinting.
    public class Btn
    {
        public RectTransform Rt;
        public Image Bg;
        public Text Label;
        public RawImage Glyph;
        public Button Button;
        public HoverRelay Hover;

        public Sprite Normal, Hovered, Selected, SelectedHover;
        public Color Ink = Look.Ink2, InkHover = Look.Ink, InkOn = Look.Amber, InkOff = Look.Fade(Look.Ink3, 0.6f);
        public bool On, Enabled = true;

        public static Btn Make(RectTransform parent, string name, float x, float y, float w, float h, Action onClick)
        {
            var b = new Btn { Rt = Ui.Node(parent, name).At(x, y, w, h) };
            b.Bg = Ui.Box(b.Rt, null, raycast: true);
            b.Button = b.Rt.gameObject.AddComponent<Button>();
            b.Button.transition = Selectable.Transition.None;
            b.Button.targetGraphic = b.Bg;
            var nav = b.Button.navigation;
            nav.mode = Navigation.Mode.None;
            b.Button.navigation = nav;
            b.Button.onClick.AddListener(() => { if (b.Enabled) onClick?.Invoke(); });
            b.Hover = b.Rt.gameObject.AddComponent<HoverRelay>();
            b.Hover.Changed = _ => b.Refresh();
            return b;
        }

        public Btn WithLabel(string text, Font font, int size, FontStyle style = FontStyle.Normal, TextAnchor align = TextAnchor.MiddleCenter)
        {
            Label = Ui.Label(Ui.Node(Rt, "Label").Stretch(), text, font, size, Ink, align, style);
            Refresh();
            return this;
        }

        public Btn WithGlyph(Texture texture, float size)
        {
            Glyph = Ui.Picture(Ui.Node(Rt, "Glyph"), texture, Ink);
            Glyph.rectTransform.anchorMin = Glyph.rectTransform.anchorMax = Glyph.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            Glyph.rectTransform.sizeDelta = new Vector2(size, size);
            Refresh();
            return this;
        }

        public Btn Skin(Sprite normal, Sprite hovered, Sprite selected = null, Sprite selectedHover = null)
        {
            Normal = normal;
            Hovered = hovered;
            Selected = selected ?? hovered;
            SelectedHover = selectedHover ?? Selected;
            Refresh();
            return this;
        }

        public Btn Colors(Color ink, Color inkHover, Color inkOn)
        {
            Ink = ink;
            InkHover = inkHover;
            InkOn = inkOn;
            Refresh();
            return this;
        }

        public void Refresh()
        {
            bool hover = Hover != null && Hover.Over && Enabled;
            Sprite s = On ? (hover ? SelectedHover : Selected) : (hover ? Hovered : Normal);
            if (Bg.sprite != s) Ui.SetSprite(Bg, s);
            Color ink = !Enabled ? InkOff : On ? InkOn : hover ? InkHover : Ink;
            if (Label != null) Label.color = ink;
            if (Glyph != null) Glyph.color = ink;
            Button.interactable = Enabled;
        }
    }
}
