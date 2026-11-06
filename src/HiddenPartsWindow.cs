// The Hidden parts window: a small copy of the VAB parts list that shows only the
// hidden parts, in the look Phil's mods share with PIP. The VAB's categories down
// the left, sort and search on top, the stock part icons in a grid (same models,
// same spin on hover), and a bar at the bottom with the hovered part's details.
// A click brings a part back, and Undo hides it again.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using KSP.UI;
using KSP.UI.Screens;
using KSP.UI.Screens.Editor;
using UnityEngine;
using UnityEngine.UI;

namespace PhilsPartHider
{
    public class HiddenPartsWindow
    {
        enum SortKey { Name, Mod, Recent, Mass, Cost, Size }

        const int Columns = 7, VisibleRows = 6;
        const float TitleH = 46, HeadH = 108, FootH = 62, MissingH = 28, RailW = 62, Gap = 7, Pad = 14, ScrollW = 10;
        const float UndoSeconds = 6;
        const string LockId = "PhilsPartHiderWindow", TypingLockId = "PhilsPartHiderTyping";
        const string HelpLine = "Click a part to bring it back. Middle-click a part in the parts list to hide it.";

        // While the mouse is over the window: no picking or placing parts on the
        // craft behind it, and the scroll wheel scrolls the window, not the camera.
        // Icon hover is left unlocked so the icons still spin.
        const ControlTypes WindowLock = ControlTypes.EDITOR_PAD_PICK_PLACE | ControlTypes.EDITOR_PAD_PICK_COPY |
            ControlTypes.EDITOR_GIZMO_TOOLS | ControlTypes.EDITOR_ROOT_REFLOW | ControlTypes.CAMERACONTROLS;

        // Kept for the rest of the session, so the window opens the way it was left.
        static Vector2? savedPosition;
        static SortKey sort = SortKey.Name;
        static bool ascending = true;
        static string search = "";
        static string category;                 // the selected category's name; null for All

        static readonly HashSet<EditorPartIcon> windowIcons = new HashSet<EditorPartIcon>();
        public static bool IsWindowIcon(EditorPartIcon icon) => windowIcons.Contains(icon);

        // The stock code keeps the icon clipping rectangle in four static fields
        // shared with the parts list; they're put back after clipping ours.
        static readonly FieldInfo[] maskFields = new[] { "minX", "maxX", "minY", "maxY" }
            .Select(n => typeof(PartListTooltipController).GetField(n, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)).ToArray();

        class CatButton
        {
            public PartCategory Cat;            // null for All
            public Btn Btn;
            public RectTransform Badge;
            public Image BadgeBg;
            public Text BadgeText;
            public RectTransform Bar;
        }

        readonly Action closeRequested;
        readonly List<Btn> buttons = new List<Btn>();
        readonly List<CatButton> cats = new List<CatButton>();
        readonly Dictionary<SortKey, Btn> sortButtons = new Dictionary<SortKey, Btn>();
        readonly List<EditorPartIcon> icons = new List<EditorPartIcon>();
        readonly List<GameObject> tiles = new List<GameObject>();
        readonly Dictionary<AvailablePart, string> categoryOf = new Dictionary<AvailablePart, string>();
        List<PartCategory> categories;

        RectTransform root, gridArea, content, viewport, missingBar, tip, pill, foot;
        Text pillText, eyebrow, heading, emptyText, missingText, tipText;
        Text infoTitle, infoSub, help, confirmText;
        Image searchBg;
        InputField searchField;
        RawImage sortArrow;
        Btn undoButton, unhideAllButton, keepButton, confirmButton, forgetButton;
        DragMove drag;
        Vector2 cell;
        float winW, winH, mainW, gridH;

        int builtVersion = -1;
        bool dirty;
        AvailablePart hovered;
        CatButton hoveredCat;
        string pendingUnhide;
        bool pendingUndo, pendingUnhideAll, pendingForget, confirmAll;
        string undoName;
        int undoIndex;
        float undoUntil;
        bool windowLocked, typingLocked;
        string footKey;
        static bool loggedOnce;

        public HiddenPartsWindow(Action closeRequested)
        {
            this.closeRequested = closeRequested;
        }

        public bool IsOpen => root != null;

        // ---- Opening and closing ------------------------------------------------

        public void Open()
        {
            if (root != null || EditorPartList.Instance == null || UIMasterController.Instance == null) return;
            try
            {
                Build();
                Populate();
            }
            catch (Exception e)
            {
                Debug.LogError($"[PhilsPartHider] Couldn't open the window: {e}");
                Close();
            }
        }

        public void Close()
        {
            ClearTiles();
            ReleaseLocks();
            if (root != null)
            {
                savedPosition = root.anchoredPosition;
                UnityEngine.Object.Destroy(root.gameObject);
            }
            root = null;
            buttons.Clear();
            cats.Clear();
            sortButtons.Clear();
            searchField = null;
            hovered = null;
            hoveredCat = null;
            confirmAll = false;
            footKey = null;
        }

        void Build()
        {
            Look.Init();
            categories = PartFacts.Categories();

            // Same icon cell as the stock parts list.
            EditorPartList list = EditorPartList.Instance;
            GridLayoutGroup stockGrid = list.partGrid != null ? list.partGrid.GetComponent<GridLayoutGroup>() : null;
            cell = stockGrid != null && stockGrid.cellSize.x > 8 ? stockGrid.cellSize : new Vector2(64, 64);
            mainW = Columns * cell.x + (Columns - 1) * Gap + 2 * Pad + ScrollW;
            gridH = VisibleRows * cell.y + (VisibleRows - 1) * Gap + 2 * Pad;
            winW = RailW + mainW;
            winH = TitleH + HeadH + gridH + FootH;

            Canvas canvas = UIMasterController.Instance.dialogCanvas;
            root = Ui.Node(canvas.transform, "PhilsPartHider");
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(winW, winH);
            root.anchoredPosition = Clamped(savedPosition ?? Vector2.zero);
            Ui.Box(root, Look.Round(Look.Panel, Look.Line, 10), raycast: true);

            BuildTitle();
            BuildRail();
            BuildHead();
            BuildGrid();
            BuildMissing();
            BuildFoot();
            BuildTip();
            root.SetAsLastSibling();
        }

        // Keeps the window on screen.
        Vector2 Clamped(Vector2 p)
        {
            Rect area = ((RectTransform)root.parent).rect;
            float x = Mathf.Max(0, (area.width - winW) / 2), y = Mathf.Max(0, (area.height - winH) / 2);
            return new Vector2(Mathf.Clamp(p.x, -x, x), Mathf.Clamp(p.y, -y, y));
        }

        Btn Track(Btn b)
        {
            buttons.Add(b);
            return b;
        }

        void BuildTitle()
        {
            Ui.Slab(root, "TitleBar", 1, 1, winW - 2, TitleH - 1, Look.Panel2, 9, Ui.Corners.Top);
            Ui.Solid(Ui.Node(root, "TitleLine").At(1, TitleH - 1, winW - 2, 1), Look.Line);

            // The title bar drags the window; the buttons on it sit above.
            RectTransform handle = Ui.Node(root, "Drag").At(0, 0, winW, TitleH);
            Ui.Solid(handle, Color.clear, raycast: true);
            drag = handle.gameObject.AddComponent<DragMove>();
            drag.Target = root;

            Ui.Picture(Ui.Node(root, "Logo").At(14, 11, 24, 24), Look.EyeSlash, Look.Amber);
            Ui.Label(Ui.Node(root, "Title").At(46, 0, 260, TitleH),
                $"<color={Look.Html(Look.Ink3)}>Phil's</color> <b>Part Hider</b>", Look.Display, 19, Look.Ink);

            Btn close = Track(Btn.Make(root, "Close", winW - 38, 8, 30, 30, () => closeRequested())
                .Skin(null, Look.Round(Look.Panel3, Look.Panel3, 6)).Colors(Look.Ink2, Look.Ink, Look.Ink).WithGlyph(Look.Cross, 12));

            pill = Ui.Node(root, "Count").At(winW - 48 - 90, 11, 90, 24);
            Ui.Box(pill, Look.Round(Look.Panel3, Look.Panel3, 11));
            pillText = Ui.Label(Ui.Node(pill, "Text").Stretch(), "", Look.Display, 14, Look.Ink2, TextAnchor.MiddleCenter);
        }

        void BuildRail()
        {
            float bodyH = winH - TitleH;
            Ui.Slab(root, "Rail", 1, TitleH, RailW - 1, bodyH - 1, Look.Rail, 9, Ui.Corners.BottomLeft);
            Ui.Solid(Ui.Node(root, "RailLine").At(RailW - 1, TitleH, 1, bodyH - 1), Look.Line);

            Sprite hover = Look.Round(Look.Panel2, Look.Panel2, 6), on = Look.Round(Look.Wash(Look.Rail), Look.AmberLine, 6);
            float y = TitleH + 8;
            AddCat(null, y, 34, hover, on);
            y += 34 + 5;
            Ui.Solid(Ui.Node(root, "RailSep").At(16, y, 30, 1), Look.Line);
            y += 6;

            // Shrink the buttons if a mod adds more categories than fit.
            float room = winH - y - 8;
            float h = Mathf.Clamp(room / Mathf.Max(1, categories.Count) - 3, 16, 27);
            foreach (PartCategory c in categories)
            {
                AddCat(c, y, h, hover, on);
                y += h + 3;
            }
        }

        void AddCat(PartCategory c, float y, float h, Sprite hover, Sprite on)
        {
            var cb = new CatButton { Cat = c };
            cb.Bar = Ui.Node(root, "Bar").At(1, y + 6, 3, h - 12);
            Ui.Solid(cb.Bar, Look.Amber);
            cb.Btn = Track(Btn.Make(root, c?.Name ?? "All", 8, y, 46, h, () => SelectCategory(cb)).Skin(null, hover, on));
            if (c == null) cb.Btn.WithLabel("All", Look.Display, 14, FontStyle.Bold);
            else if (c.Icon != null) cb.Btn.WithGlyph(c.Icon, Mathf.Min(20, h - 4));
            else cb.Btn.WithLabel(c.Short, Look.Display, 12, FontStyle.Bold);
            cb.Btn.Hover.Changed = over =>
            {
                cb.Btn.Refresh();
                if (over) hoveredCat = cb;
                else if (hoveredCat == cb) hoveredCat = null;
                ShowTip();
            };

            if (c != null)
            {
                cb.Badge = Ui.Node(cb.Btn.Rt, "Badge");
                cb.Badge.anchorMin = cb.Badge.anchorMax = cb.Badge.pivot = new Vector2(1, 1);
                cb.Badge.anchoredPosition = new Vector2(4, 4);
                cb.Badge.sizeDelta = new Vector2(16, 15);
                cb.BadgeBg = Ui.Box(cb.Badge, Look.Round(Look.Panel3, Look.Line2, 6));
                cb.BadgeText = Ui.Label(Ui.Node(cb.Badge, "Text").Stretch(), "", Look.Display, 10, Look.Ink, TextAnchor.MiddleCenter);
                cb.BadgeText.horizontalOverflow = HorizontalWrapMode.Overflow;
            }
            cats.Add(cb);
        }

        void BuildHead()
        {
            float x0 = RailW, top = TitleH;
            eyebrow = Ui.Label(Ui.Node(root, "Eyebrow").At(x0 + 16, top + 14, mainW - 260, 16), "", Look.Body, 11, Look.Ink3);
            heading = Ui.Label(Ui.Node(root, "Heading").At(x0 + 16, top + 29, mainW - 260, 28), "", Look.Display, 21, Look.Ink, style: FontStyle.Bold);

            // Search box.
            RectTransform box = Ui.Node(root, "Search").At(x0 + mainW - 16 - 210, top + 14, 210, 34);
            box.gameObject.SetActive(false);    // so the field finds its text when it wakes
            searchBg = Ui.Box(box, Look.Round(Look.Void, Look.Line2, 8), raycast: true);
            Ui.Picture(Ui.Node(box, "Glyph").At(11, 10, 14, 14), Look.Search, Look.Ink3);
            Text text = Ui.Label(Ui.Node(box, "Text").Stretch(32, 0, 10, 0), "", Look.Body, 14, Look.Ink);
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            Text placeholder = Ui.Label(Ui.Node(box, "Placeholder").Stretch(32, 0, 10, 0), "Name, mod or maker", Look.Body, 14, Look.Ink3);
            searchField = box.gameObject.AddComponent<InputField>();
            searchField.textComponent = text;
            searchField.placeholder = placeholder;
            searchField.targetGraphic = searchBg;
            searchField.transition = Selectable.Transition.None;
            searchField.lineType = InputField.LineType.SingleLine;
            searchField.characterLimit = 60;
            searchField.customCaretColor = true;
            searchField.caretColor = Look.Amber;
            searchField.selectionColor = new Color(1f, 0.65f, 0.17f, 0.35f);
            var nav = searchField.navigation;
            nav.mode = Navigation.Mode.None;
            searchField.navigation = nav;
            searchField.text = search;
            searchField.onValueChanged.AddListener(OnSearchChanged);
            box.gameObject.SetActive(true);

            // Sort buttons, as one segmented control.
            float rowY = top + 64;
            Ui.Label(Ui.Node(root, "SortLabel").At(x0 + 16, rowY, 40, 34), "SORT", Look.Body, 11, Look.Ink3);
            float segX = x0 + 16 + 40, segW = mainW - 16 - 40 - 16;
            RectTransform seg = Ui.Node(root, "Sort").At(segX, rowY, segW, 34);
            Ui.Box(seg, Look.Round(Look.Void, Look.Line, 8));
            var keys = (SortKey[])Enum.GetValues(typeof(SortKey));
            float bw = (segW - 6 - 2 * (keys.Length - 1)) / keys.Length;
            Sprite on = Look.Round(Look.Panel3, Look.Line2, 5);
            for (int i = 0; i < keys.Length; i++)
            {
                SortKey k = keys[i];
                Btn b = Track(Btn.Make(seg, k.ToString(), 3 + i * (bw + 2), 4, bw, 26, () => SetSort(k))
                    .Skin(null, null, on).Colors(Look.Ink2, Look.Ink, Look.Ink).WithLabel(k.ToString(), Look.Body, 13));
                sortButtons[k] = b;
            }
            sortArrow = Ui.Picture(Ui.Node(root, "SortArrow"), Look.Up, Look.Amber);
            sortArrow.rectTransform.sizeDelta = new Vector2(8, 8);

            Ui.Solid(Ui.Node(root, "HeadLine").At(x0, top + HeadH - 1, mainW - 1, 1), Look.Line);
        }

        void BuildGrid()
        {
            gridArea = Ui.Node(root, "Grid").At(RailW, TitleH + HeadH, mainW - 1, gridH);
            ScrollRect scroll = gridArea.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = false;
            scroll.scrollSensitivity = 40;

            viewport = Ui.Node(gridArea, "Viewport").Stretch(0, 0, ScrollW - 1, 0);
            Ui.Solid(viewport, Color.clear, raycast: true);    // so the wheel scrolls over gaps too
            viewport.gameObject.AddComponent<RectMask2D>();

            content = Ui.Node(viewport, "Content");
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            GridLayoutGroup grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.padding = new RectOffset((int)Pad, (int)Pad, (int)Pad, (int)Pad);
            grid.cellSize = cell;
            grid.spacing = new Vector2(Gap, Gap);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = Columns;
            ContentSizeFitter fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            RectTransform bar = Ui.Node(gridArea, "Scrollbar");
            bar.anchorMin = new Vector2(1, 0);
            bar.anchorMax = new Vector2(1, 1);
            bar.pivot = new Vector2(1, 0.5f);
            bar.offsetMin = new Vector2(-8, 10);
            bar.offsetMax = new Vector2(-2, -10);
            Ui.Box(bar, Look.Round(Look.Panel2, Look.Panel2, 2), raycast: true);
            RectTransform handle = Ui.Node(bar, "Handle");
            handle.offsetMin = handle.offsetMax = Vector2.zero;
            Image handleImg = Ui.Box(handle, Look.Round(Look.Line2, Look.Line2, 2), raycast: true);
            Scrollbar sb = bar.gameObject.AddComponent<Scrollbar>();
            sb.transition = Selectable.Transition.None;
            sb.targetGraphic = handleImg;
            sb.handleRect = handle;
            sb.direction = Scrollbar.Direction.BottomToTop;
            var nav = sb.navigation;
            nav.mode = Navigation.Mode.None;
            sb.navigation = nav;

            scroll.viewport = viewport;
            scroll.content = content;
            scroll.verticalScrollbar = sb;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

            emptyText = Ui.Label(Ui.Node(gridArea, "Empty").Stretch(30, 40, 30, 40), "", Look.Body, 14, Look.Ink3, TextAnchor.MiddleCenter);
        }

        void BuildMissing()
        {
            missingBar = Ui.Node(root, "Missing").At(RailW, 0, mainW - 1, MissingH);
            Ui.Solid(missingBar, Look.Panel);
            Ui.Solid(Ui.Node(missingBar, "Line").At(0, 0, mainW - 1, 1), Look.Line);
            missingText = Ui.Label(Ui.Node(missingBar, "Text").At(16, 1, mainW - 16 - 110, MissingH - 1), "", Look.Body, 12, Look.Warn);
            forgetButton = Track(Btn.Make(missingBar, "Forget", mainW - 100, 3, 86, MissingH - 6, () => pendingForget = true)
                .Colors(Look.Ink2, Look.Ink, Look.Ink).WithLabel("Forget them", Look.Body, 12, align: TextAnchor.MiddleRight));
        }

        void BuildFoot()
        {
            float x0 = RailW, top = winH - FootH;
            foot = Ui.Slab(root, "Foot", x0, top, mainW - 1, FootH - 1, Look.Panel2, 9, Ui.Corners.BottomRight);
            Ui.Solid(Ui.Node(root, "FootLine").At(x0, top, mainW - 1, 1), Look.Line);

            infoTitle = Ui.Label(Ui.Node(foot, "InfoTitle").At(16, 11, 300, 20), "", Look.Body, 14, Look.Ink, style: FontStyle.Bold);
            infoSub = Ui.Label(Ui.Node(foot, "InfoSub").At(16, 32, 300, 18), "", Look.Body, 12, Look.Ink3);
            help = Ui.Label(Ui.Node(foot, "Help").At(16, 0, 300, FootH - 1), HelpLine, Look.Body, 13, Look.Ink2);
            undoButton = Track(Btn.Make(foot, "Undo", 0, 15, 48, 30, () => pendingUndo = true)
                .Colors(Look.Amber, Look.AmberHover, Look.Amber).WithLabel("Undo", Look.Body, 13, FontStyle.Bold));

            Sprite ghost = Look.Round(Look.Panel2, Look.Line2, 7), ghostHover = Look.Round(Look.Panel2, Look.AmberLine, 7);
            float right = mainW - 1 - 14;
            unhideAllButton = Track(Btn.Make(foot, "UnhideAll", right - 106, 14, 106, 34, () => confirmAll = true)
                .Skin(ghost, ghostHover).Colors(Look.Ink2, Look.Ink, Look.Ink).WithLabel("Unhide all", Look.Body, 13));
            confirmButton = Track(Btn.Make(foot, "ConfirmAll", right - 96, 14, 96, 34, () => pendingUnhideAll = true)
                .Skin(Look.Round(Look.Amber, Look.Amber, 7), Look.Round(Look.AmberHover, Look.AmberHover, 7))
                .Colors(Look.AmberInk, Look.AmberInk, Look.AmberInk).WithLabel("Unhide all", Look.Display, 14, FontStyle.Bold));
            keepButton = Track(Btn.Make(foot, "Keep", right - 96 - 8 - 100, 14, 100, 34, () => confirmAll = false)
                .Skin(ghost, ghostHover).Colors(Look.Ink2, Look.Ink, Look.Ink).WithLabel("Keep hidden", Look.Body, 13));
            confirmText = Ui.Label(Ui.Node(foot, "ConfirmText").At(right - 96 - 8 - 100 - 8 - 150, 0, 150, FootH - 1), "",
                Look.Body, 13, Look.Ink2, TextAnchor.MiddleRight);
        }

        void BuildTip()
        {
            tip = Ui.Node(root, "Tip").At(RailW + 4, 0, 100, 26);
            Ui.Box(tip, Look.Round(Look.Panel3, Look.Line2, 6));
            tipText = Ui.Label(Ui.Node(tip, "Text").Stretch(10, 0, 10, 0), "", Look.Body, 12, Look.Ink);
            tipText.horizontalOverflow = HorizontalWrapMode.Overflow;
            tip.gameObject.SetActive(false);
        }

        // ---- Per frame -------------------------------------------------------------

        public void Update()
        {
            if (pendingUnhide != null)
            {
                string name = pendingUnhide;
                pendingUnhide = null;
                int index = HiddenList.IndexOf(name);
                if (HiddenList.Remove(name))
                {
                    PhilsPartHiderAddon.RefreshPartList();
                    undoName = name;
                    undoIndex = index;
                    undoUntil = Time.unscaledTime + UndoSeconds;
                }
            }
            if (pendingUndo)
            {
                pendingUndo = false;
                if (undoName != null && HiddenList.Insert(undoIndex, undoName)) PhilsPartHiderAddon.RefreshPartList();
                undoName = null;
            }
            if (pendingUnhideAll)
            {
                pendingUnhideAll = false;
                confirmAll = false;
                int n = HiddenList.Count;
                HiddenList.RemoveAll(HiddenList.Names);
                undoName = null;
                PhilsPartHiderAddon.RefreshPartList();
                ScreenMessages.PostScreenMessage($"All {n} hidden parts are back in the parts list.", 4f, ScreenMessageStyle.UPPER_CENTER);
            }
            if (pendingForget)
            {
                pendingForget = false;
                HiddenList.RemoveAll(MissingNames());
            }

            if (root == null) return;
            if (builtVersion != HiddenList.Version || dirty) Populate();
        }

        public void LateUpdate()
        {
            if (root == null) { ReleaseLocks(); return; }
            try
            {
                ClipIcons();
                RefreshFoot();
                foreach (Btn b in buttons) b.Refresh();
                Ui.SetSprite(searchBg, Look.Round(Look.Void, searchField != null && searchField.isFocused ? Look.AmberLine : Look.Line2, 8));
            }
            catch (Exception e)
            {
                Debug.LogError($"[PhilsPartHider] Window update failed, closing it: {e}");
                closeRequested();
                return;
            }
            SetLock(ref windowLocked, LockId, WindowLock, MouseOver() || drag != null && drag.Dragging);
            SetLock(ref typingLocked, TypingLockId, ControlTypes.KEYBOARDINPUT, searchField != null && searchField.isFocused);
        }

        // Clip the 3D icons to the scroll area, the way the stock list clips its own,
        // then put the stock list's clipping back.
        void ClipIcons()
        {
            if (icons.Count == 0 || viewport == null) return;
            bool canRestore = maskFields.All(f => f != null);
            float[] saved = canRestore ? maskFields.Select(f => (float)f.GetValue(null)).ToArray() : null;
            PartListTooltipController.SetupScreenSpaceMask(viewport);
            foreach (EditorPartIcon icon in icons)
                if (icon != null && icon.materials != null) PartListTooltipController.SetScreenSpaceMaskMaterials(icon.materials);
            if (canRestore)
                for (int i = 0; i < maskFields.Length; i++) maskFields[i].SetValue(null, saved[i]);
        }

        static void SetLock(ref bool isSet, string id, ControlTypes types, bool want)
        {
            if (want == isSet) return;
            if (want) InputLockManager.SetControlLock(types, id);
            else InputLockManager.RemoveControlLock(id);
            isSet = want;
        }

        void ReleaseLocks()
        {
            SetLock(ref windowLocked, LockId, WindowLock, false);
            SetLock(ref typingLocked, TypingLockId, ControlTypes.KEYBOARDINPUT, false);
        }

        public bool MouseOver()
        {
            if (root == null) return false;
            Canvas canvas = root.GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.rootCanvas.worldCamera : null;
            return RectTransformUtility.RectangleContainsScreenPoint(root, Input.mousePosition, cam);
        }

        // ---- Filling the grid -------------------------------------------------------

        void Populate()
        {
            dirty = false;
            builtVersion = HiddenList.Version;
            ClearTiles();
            if (root == null) return;

            List<AvailablePart> installed = HiddenList.Names.Select(n => PartLoader.getPartInfoByName(n)).Where(ap => ap != null).ToList();
            categoryOf.Clear();
            foreach (PartCategory c in categories) c.Count = 0;
            foreach (AvailablePart ap in installed)
                foreach (PartCategory c in categories)
                    if (c.Has(ap))
                    {
                        c.Count++;
                        if (!categoryOf.ContainsKey(ap)) categoryOf[ap] = c.Name;
                    }
            if (category != null && !categories.Any(c => c.Name == category && c.Count > 0)) category = null;

            List<AvailablePart> shown = Sorted(Filtered(installed));
            EditorPartList list = EditorPartList.Instance;
            Sprite tileNormal = Look.Round(Look.Hero, Look.Line, 7), tileHover = Look.Round(Look.Hero, Look.Amber, 7);
            foreach (AvailablePart ap in shown)
            {
                RectTransform tile = Ui.Node(content, ap.name);
                Image bg = Ui.Box(tile, tileNormal);

                EditorPartIcon icon = UnityEngine.Object.Instantiate(list.partPrefab);
                icon.Create(list, ap, list.iconSize, list.iconOverScale, list.iconOverSpin);
                icon.DisableAddButton();
                icon.DisableDeleteButton();
                icon.UnsetGrey();
                // Our tile is the background: the stock square goes clear but still takes clicks.
                foreach (Image img in icon.GetComponentsInChildren<Image>(true))
                    if (img.GetComponent<Mask>() == null) img.color = Color.clear;
                foreach (Selectable s in icon.GetComponentsInChildren<Selectable>(true)) s.transition = Selectable.Transition.None;
                // A click brings the part back instead of picking it up.
                if (icon.btnSpawnPart != null)
                {
                    string name = ap.name;
                    icon.btnSpawnPart.onClick = new Button.ButtonClickedEvent();
                    icon.btnSpawnPart.onClick.AddListener(() => pendingUnhide = name);
                }
                var irt = (RectTransform)icon.transform;
                irt.SetParent(tile, false);
                // Fill the tile, keeping the icon's own pivot (its 3D model hangs off it).
                irt.anchorMin = Vector2.zero;
                irt.anchorMax = Vector2.one;
                irt.offsetMin = irt.offsetMax = Vector2.zero;
                icon.gameObject.SetActive(true);

                RectTransform badge = Ui.Node(tile, "Unhide").At(cell.x - 21, 3, 18, 18);
                Ui.Box(badge, Look.Round(Look.Amber, Look.Amber, 4));
                Ui.Picture(Ui.Node(badge, "Eye").Stretch(3, 3, 3, 3), Look.Eye, Look.AmberInk);
                badge.gameObject.SetActive(false);

                HoverRelay relay = tile.gameObject.AddComponent<HoverRelay>();
                AvailablePart part = ap;
                relay.Changed = over =>
                {
                    if (bg != null) Ui.SetSprite(bg, over ? tileHover : tileNormal);
                    if (badge != null) badge.gameObject.SetActive(over);
                    if (over) hovered = part;
                    else if (hovered == part) hovered = null;
                };

                icons.Add(icon);
                windowIcons.Add(icon);
                tiles.Add(tile.gameObject);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);

            RefreshRail(installed.Count);
            RefreshHead(shown.Count, installed.Count);
            RefreshMissing();
            RefreshSort();
            emptyText.gameObject.SetActive(shown.Count == 0);

            if (!loggedOnce)
            {
                loggedOnce = true;
                Canvas c = root.GetComponentInParent<Canvas>();
                Debug.Log($"[PhilsPartHider] Window open: {icons.Count} icons, {categories.Count} categories " +
                          $"({(categories.Any(k => k.Icon != null) ? "with" : "without")} icons), cell {cell}, canvas '{(c != null ? c.rootCanvas.name : "none")}'");
            }
        }

        void ClearTiles()
        {
            foreach (EditorPartIcon icon in icons) if (icon != null) windowIcons.Remove(icon);
            foreach (GameObject t in tiles) if (t != null) UnityEngine.Object.Destroy(t);
            icons.Clear();
            tiles.Clear();
            hovered = null;
        }

        List<AvailablePart> Filtered(List<AvailablePart> parts)
        {
            IEnumerable<AvailablePart> result = parts;
            if (category != null)
            {
                PartCategory c = categories.First(k => k.Name == category);
                result = result.Where(c.Has);
            }
            string q = search.Trim();
            if (q.Length > 0)
                result = result.Where(ap => Has(ap.title, q) || Has(ap.name, q) || Has(PartFacts.ModOf(ap), q) ||
                                            Has(ap.manufacturer, q) || Has(CategoryName(ap), q));
            return result.ToList();
        }

        List<AvailablePart> Sorted(List<AvailablePart> parts)
        {
            Comparison<AvailablePart> byTitle = (a, b) => string.Compare(a.title, b.title, StringComparison.CurrentCultureIgnoreCase);
            Comparison<AvailablePart> primary;
            switch (sort)
            {
                case SortKey.Mod: primary = (a, b) => string.Compare(PartFacts.ModOf(a), PartFacts.ModOf(b), StringComparison.OrdinalIgnoreCase); break;
                // Newest first on the first click, like a recent-files list.
                case SortKey.Recent: primary = (a, b) => HiddenList.IndexOf(b.name).CompareTo(HiddenList.IndexOf(a.name)); break;
                case SortKey.Mass: primary = (a, b) => PartFacts.MassOf(a).CompareTo(PartFacts.MassOf(b)); break;
                case SortKey.Cost: primary = (a, b) => a.cost.CompareTo(b.cost); break;
                case SortKey.Size: primary = (a, b) => a.partSize.CompareTo(b.partSize); break;
                default: primary = byTitle; break;
            }
            parts.Sort((a, b) =>
            {
                int c = primary(a, b);
                if (!ascending) c = -c;
                if (c != 0) return c;
                c = byTitle(a, b);
                return sort == SortKey.Name && !ascending ? -c : c;
            });
            return parts;
        }

        // ---- Refreshing the rest ----------------------------------------------------

        void RefreshRail(int total)
        {
            foreach (CatButton cb in cats)
            {
                bool selected = cb.Cat == null ? category == null : cb.Cat.Name == category;
                int n = cb.Cat?.Count ?? total;
                cb.Btn.On = selected;
                cb.Btn.Enabled = cb.Cat == null || n > 0;
                cb.Btn.InkOff = Look.Fade(Look.Ink3, 0.4f);
                cb.Bar.gameObject.SetActive(selected);
                if (cb.Badge != null)
                {
                    cb.Badge.gameObject.SetActive(n > 0);
                    cb.BadgeText.text = n.ToString();
                    cb.BadgeText.color = selected ? Look.AmberInk : Look.Ink;
                    cb.BadgeText.fontStyle = selected ? FontStyle.Bold : FontStyle.Normal;
                    Ui.SetSprite(cb.BadgeBg, selected ? Look.Round(Look.Amber, Look.Amber, 6) : Look.Round(Look.Panel3, Look.Line2, 6));
                    cb.Badge.sizeDelta = new Vector2(Mathf.Max(16, cb.BadgeText.preferredWidth + 8), 15);
                }
                cb.Btn.Refresh();
            }
            ShowTip();
        }

        void RefreshHead(int shown, int total)
        {
            heading.text = category ?? "All hidden parts";
            string q = search.Trim();
            eyebrow.text = q.Length > 0 ? $"{shown} MATCH “{q.ToUpperInvariant()}”" : $"{shown} PART{(shown == 1 ? "" : "S")}";
            pillText.text = $"<b><color={Look.Html(Look.Ink)}>{HiddenList.Count}</color></b> hidden";
            float w = pillText.preferredWidth + 24;
            pill.At(winW - 48 - w, 11, w, 24);

            if (shown == 0)
                emptyText.text = total == 0
                    ? $"<size=18><b><color={Look.Html(Look.Ink2)}>Nothing is hidden</color></b></size>\nMiddle-click a part in the parts list to hide it."
                    : $"<size=18><b><color={Look.Html(Look.Ink2)}>Nothing here</color></b></size>\nNo hidden parts {(q.Length > 0 ? "match that search" : "in this category")}.";
        }

        void RefreshMissing()
        {
            List<string> missing = MissingNames();
            bool any = missing.Count > 0;
            missingBar.gameObject.SetActive(any);
            float h = gridH - (any ? MissingH : 0);
            gridArea.At(RailW, TitleH + HeadH, mainW - 1, h);
            missingBar.At(RailW, TitleH + HeadH + h, mainW - 1, MissingH);
            if (any)
                missingText.text = $"{missing.Count} hidden part{(missing.Count == 1 ? " isn't" : "s aren't")} installed any more: " +
                                   string.Join(", ", missing.Take(4)) + (missing.Count > 4 ? $" and {missing.Count - 4} more" : "");
        }

        void RefreshSort()
        {
            if (!sortButtons.TryGetValue(sort, out Btn b)) return;
            foreach (KeyValuePair<SortKey, Btn> kv in sortButtons)
            {
                kv.Value.On = kv.Key == sort;
                kv.Value.Label.fontStyle = kv.Key == sort ? FontStyle.Bold : FontStyle.Normal;
            }
            // The arrow sits just right of the selected label, the pair centered.
            float textW = b.Label.preferredWidth;
            b.Label.rectTransform.Stretch(0, 0, 12, 0);
            foreach (KeyValuePair<SortKey, Btn> kv in sortButtons)
                if (kv.Key != sort) kv.Value.Label.rectTransform.Stretch();
            sortArrow.texture = ascending ? Look.Up : Look.Down;
            RectTransform a = sortArrow.rectTransform;
            a.SetParent(b.Rt, false);
            a.anchorMin = a.anchorMax = a.pivot = new Vector2(0.5f, 0.5f);
            a.anchoredPosition = new Vector2(textW / 2 - 6 + 3 + 4, 0);
        }

        // The bottom bar: the hovered part, or the last part brought back with Undo,
        // or how-to; Unhide all on the right, which asks first.
        void RefreshFoot()
        {
            if (undoName != null && Time.unscaledTime >= undoUntil) undoName = null;
            if (confirmAll && HiddenList.Count == 0) confirmAll = false;
            bool undo = undoName != null && hovered == null;
            // Lay the bar out again only when what it shows changes.
            string key = $"{hovered?.name}|{(undo ? undoName : "")}|{confirmAll}|{HiddenList.Count}";
            if (key == footKey) return;
            footKey = key;

            if (confirmAll) confirmText.text = $"Bring back all {HiddenList.Count}?";
            float right = mainW - 1 - 14;
            float rightW = confirmAll ? 96 + 8 + 100 + 8 + Mathf.Min(150, confirmText.preferredWidth) : 106;
            float infoW = right - rightW - 16 - 14;

            if (hovered != null)
            {
                infoTitle.text = hovered.title;
                infoTitle.color = Look.Ink;
                string v(string s) => $"<b><color={Look.Html(Look.Ink2)}>{s}</color></b>";
                infoSub.text = $"{PartFacts.ModOf(hovered)}    {CategoryName(hovered)}    {v(PartFacts.Mass(PartFacts.MassOf(hovered)))}    " +
                               $"{v(PartFacts.Funds(hovered.cost))} √    {PartFacts.SizeOf(hovered)}";
            }
            else if (undo)
            {
                AvailablePart ap = PartLoader.getPartInfoByName(undoName);
                infoTitle.text = $"Back in the parts list: {ap?.title ?? undoName}";
                infoTitle.color = Look.Good;
            }
            infoTitle.gameObject.SetActive(hovered != null || undo);
            infoSub.gameObject.SetActive(hovered != null);
            help.gameObject.SetActive(hovered == null && !undo);
            undoButton.Rt.gameObject.SetActive(undo);

            float titleW = undo ? Mathf.Min(infoTitle.preferredWidth, infoW - 58) : infoW;
            infoTitle.rectTransform.At(16, undo ? 0 : 11, titleW, undo ? FootH - 1 : 20);
            infoSub.rectTransform.At(16, 32, infoW, 18);
            help.rectTransform.At(16, 0, infoW, FootH - 1);
            undoButton.Rt.At(16 + titleW + 8, 15, 48, 30);

            unhideAllButton.Enabled = HiddenList.Count > 0;
            unhideAllButton.Rt.gameObject.SetActive(!confirmAll);
            confirmButton.Rt.gameObject.SetActive(confirmAll);
            keepButton.Rt.gameObject.SetActive(confirmAll);
            confirmText.gameObject.SetActive(confirmAll);
        }

        // The category name next to the rail button under the mouse.
        void ShowTip()
        {
            if (tip == null) return;
            CatButton cb = hoveredCat;
            if (cb == null) { tip.gameObject.SetActive(false); return; }
            int n = cb.Cat?.Count ?? InstalledCount();
            string name = cb.Cat?.Name ?? "All hidden parts";
            tipText.text = n > 0 ? $"{name}  <color={Look.Html(Look.Ink3)}>{n} hidden</color>" : $"{name}  <color={Look.Html(Look.Ink3)}>nothing hidden</color>";
            float w = tipText.preferredWidth + 20;
            float y = -cb.Btn.Rt.anchoredPosition.y + (cb.Btn.Rt.sizeDelta.y - 26) / 2;
            tip.At(RailW + 4, y, w, 26);
            tip.SetAsLastSibling();
            tip.gameObject.SetActive(true);
        }

        static int InstalledCount() => HiddenList.Names.Count(n => PartLoader.getPartInfoByName(n) != null);

        // ---- Actions -------------------------------------------------------------

        void SelectCategory(CatButton cb)
        {
            category = cb.Cat?.Name;
            confirmAll = false;
            dirty = true;
        }

        void SetSort(SortKey k)
        {
            if (k == sort) ascending = !ascending;
            else { sort = k; ascending = true; }
            dirty = true;
        }

        void OnSearchChanged(string text)
        {
            text = text ?? "";
            if (text == search) return;
            search = text;
            dirty = true;
        }

        string CategoryName(AvailablePart ap) =>
            categoryOf.TryGetValue(ap, out string name) ? name : ap.category.ToString();

        static List<string> MissingNames() => HiddenList.Names.Where(n => PartLoader.getPartInfoByName(n) == null).ToList();

        static bool Has(string text, string q) => !string.IsNullOrEmpty(text) && text.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
