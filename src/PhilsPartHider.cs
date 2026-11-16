// Phil's Part Hider
//
// Middle-click a part icon in the VAB or SPH parts list to hide it. The toolbar
// button (an orange eye with a slash) opens the Hidden parts window, which shows
// every hidden part with its icon, by category; click one to bring it back.
//
// Hidden parts stay loaded. This only filters the editor's parts list, so craft
// and vessels that already use them keep working.

using System.Collections;
using System.Collections.Generic;
using KSP.UI.Screens;
using UnityEngine;
using UnityEngine.EventSystems;

[assembly: KSPAssembly("PhilsPartHider", 1, 0)]
[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersion("1.0.0")]
[assembly: System.Reflection.AssemblyTitle("Phil's Part Hider")]

namespace PhilsPartHider
{
    [KSPAddon(KSPAddon.Startup.EditorAny, false)]
    public class PhilsPartHiderAddon : MonoBehaviour
    {
        const string FilterName = "PhilsPartHider";
        const string IconPath = "PhilsPartHider/Textures/icon";

        EditorPartListFilter<AvailablePart> filter;
        ApplicationLauncherButton button;
        HiddenPartsWindow window;
        int listVersion;

        void Start()
        {
            filter = new EditorPartListFilter<AvailablePart>(FilterName, p => !HiddenList.Contains(p.name), "");
            window = new HiddenPartsWindow(() => { if (button != null) button.SetFalse(); else window.Close(); });
            listVersion = HiddenList.Version;
            StartCoroutine(AddFilterWhenReady());
            GameEvents.onGUIApplicationLauncherReady.Add(AddButton);
            GameEvents.onHideUI.Add(OnHideUI);
            GameEvents.onShowUI.Add(OnShowUI);
            if (ApplicationLauncher.Ready) AddButton();
        }

        // F2: the window hides with the rest of the UI.
        void OnHideUI() => window.SetUiHidden(true);
        void OnShowUI() => window.SetUiHidden(false);

        IEnumerator AddFilterWhenReady()
        {
            while (EditorPartList.Instance == null) yield return null;
            EditorPartList.Instance.ExcludeFilters.AddFilter(filter);
            EditorPartList.Instance.Refresh();
        }

        void AddButton()
        {
            if (button != null) return;
            Texture icon = GameDatabase.Instance.GetTexture(IconPath, false) ?? new Texture2D(38, 38);
            button = ApplicationLauncher.Instance.AddModApplication(
                () => window.Open(), () => window.Close(),
                () => { }, () => { }, () => { }, () => { },
                ApplicationLauncher.AppScenes.VAB | ApplicationLauncher.AppScenes.SPH, icon);
        }

        void Update()
        {
            // Middle-click over the window (or any menu) does nothing: see PartIconAt.
            if (Input.GetMouseButtonDown(2))
            {
                AvailablePart part = PartIconAt(Input.mousePosition);
                if (part != null && HiddenList.Add(part.name))
                    ScreenMessages.PostScreenMessage(
                        $"Hidden: {part.title}. Bring it back from the Part Hider toolbar button.", 4f, ScreenMessageStyle.UPPER_CENTER);
            }
            window.Update();

            // The one place the parts list catches up with the hidden list, whatever changed it.
            if (listVersion != HiddenList.Version)
            {
                listVersion = HiddenList.Version;
                if (EditorPartList.Instance != null) EditorPartList.Instance.Refresh();
            }
        }

        void LateUpdate() => window.LateUpdate();

        // The stock parts-list icon at a screen point, if any. Only the topmost thing
        // there counts (not an icon under a menu or dialog), and only an icon in the
        // parts list itself (not an inventory slot or another mod's window).
        internal static AvailablePart PartIconAt(Vector2 screenPoint)
        {
            EditorPartList list = EditorPartList.Instance;
            if (EventSystem.current == null || list == null || list.partGrid == null) return null;
            var pointer = new PointerEventData(EventSystem.current) { position = screenPoint };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            if (hits.Count == 0 || hits[0].gameObject == null) return null;
            EditorPartIcon icon = hits[0].gameObject.GetComponentInParent<EditorPartIcon>();
            return icon != null && icon.transform.IsChildOf(list.partGrid) ? icon.partInfo : null;
        }

        void OnDestroy()
        {
            GameEvents.onGUIApplicationLauncherReady.Remove(AddButton);
            GameEvents.onHideUI.Remove(OnHideUI);
            GameEvents.onShowUI.Remove(OnShowUI);
            window?.Close();
            if (button != null && ApplicationLauncher.Instance != null) ApplicationLauncher.Instance.RemoveModApplication(button);
            if (EditorPartList.Instance != null) EditorPartList.Instance.ExcludeFilters.RemoveFilter(filter);
        }
    }
}
