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
            // Middle-click over the window does nothing, so its own icons are never picked here.
            if (Input.GetMouseButtonDown(2) && !window.MouseOver())
            {
                AvailablePart part = HoveredPartIcon();
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

        // The stock parts-list icon under the mouse, if any.
        static AvailablePart HoveredPartIcon()
        {
            if (EventSystem.current == null) return null;
            var pointer = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            foreach (RaycastResult hit in hits)
            {
                EditorPartIcon icon = hit.gameObject.GetComponentInParent<EditorPartIcon>();
                if (icon != null && icon.partInfo != null) return icon.partInfo;
            }
            return null;
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
