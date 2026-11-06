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

        void Start()
        {
            filter = new EditorPartListFilter<AvailablePart>(FilterName, p => !HiddenList.Contains(p.name), "");
            window = new HiddenPartsWindow(() => { if (button != null) button.SetFalse(); else window.Close(); });
            StartCoroutine(AddFilterWhenReady());
            GameEvents.onGUIApplicationLauncherReady.Add(AddButton);
            if (ApplicationLauncher.Ready) AddButton();
        }

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
            if (Input.GetMouseButtonDown(2) && !window.MouseOver())
            {
                AvailablePart part = HoveredPartIcon();
                if (part != null && HiddenList.Add(part.name))
                {
                    RefreshPartList();
                    ScreenMessages.PostScreenMessage(
                        $"Hidden: {part.title}. Bring it back from the Part Hider toolbar button.", 4f, ScreenMessageStyle.UPPER_CENTER);
                }
            }
            window.Update();
        }

        void LateUpdate() => window.LateUpdate();

        // The stock parts-list icon under the mouse, if any. Icons in the Hidden
        // parts window are skipped (middle-click there does nothing).
        static AvailablePart HoveredPartIcon()
        {
            if (EventSystem.current == null) return null;
            var pointer = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            foreach (RaycastResult hit in hits)
            {
                EditorPartIcon icon = hit.gameObject.GetComponentInParent<EditorPartIcon>();
                if (icon != null && icon.partInfo != null && !HiddenPartsWindow.IsWindowIcon(icon)) return icon.partInfo;
            }
            return null;
        }

        public static void RefreshPartList()
        {
            if (EditorPartList.Instance != null) EditorPartList.Instance.Refresh();
        }

        void OnDestroy()
        {
            GameEvents.onGUIApplicationLauncherReady.Remove(AddButton);
            window?.Close();
            if (button != null && ApplicationLauncher.Instance != null) ApplicationLauncher.Instance.RemoveModApplication(button);
            if (EditorPartList.Instance != null) EditorPartList.Instance.ExcludeFilters.RemoveFilter(filter);
        }
    }
}
