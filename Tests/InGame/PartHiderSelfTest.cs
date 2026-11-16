// In-game self-test for Phil's Part Hider. Not part of the mod: Run-SelfTest.ps1
// builds it, copies it into GameData/zz_PartHiderSelfTest for one run, then
// removes it. It does nothing unless <KSP>/PartHiderSelfTest/run exists.
//
// It makes a throwaway sandbox save (zz_PartHiderSelfTest), opens the VAB, checks
// the window and the middle-click rules against the real game, writes PASS/FAIL
// lines to <KSP>/PartHiderSelfTest/results.log (and KSP.log, tagged [PH-TEST]),
// puts the hidden list back the way it was, and quits KSP.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using KSP.UI;
using KSP.UI.Screens;
using PhilsPartHider;
using UnityEngine;
using UnityEngine.EventSystems;

[KSPAddon(KSPAddon.Startup.MainMenu, true)]
public class PartHiderSelfTest : MonoBehaviour
{
    const string SaveName = "zz_PartHiderSelfTest";
    const string PipFake = "PIP.zzSelfTest", GoneFake = "zzSelfTestGone";
    const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static string Dir => Path.Combine(KSPUtil.ApplicationRootPath, "PartHiderSelfTest");

    StreamWriter log;
    int pass, fail;
    readonly List<string> errors = new List<string>();
    bool expectReadError;
    List<string> snapshot;      // the hidden list before the test, put back afterwards

    void Start()
    {
        if (!File.Exists(Path.Combine(Dir, "run"))) { Destroy(this); return; }
        DontDestroyOnLoad(gameObject);
        log = new StreamWriter(Path.Combine(Dir, "results.log"), false) { AutoFlush = true };
        Application.logMessageReceived += OnLog;
        StartCoroutine(Guarded(Run()));
    }

    void OnLog(string message, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception) return;
        if (!(message + stack).Contains("PhilsPartHider") || message.Contains("[PH-TEST]")) return;
        if (expectReadError && message.Contains("Couldn't read")) return;
        errors.Add(type + ": " + message.Split('\n')[0]);
    }

    void W(string s) { log.WriteLine(s); Debug.Log("[PH-TEST] " + s); }

    void Check(string name, bool ok, string detail = "")
    {
        if (ok) pass++; else fail++;
        W((ok ? "PASS  " : "FAIL  ") + name + (string.IsNullOrEmpty(detail) ? "" : "   [" + detail + "]"));
    }

    // Runs the test, logging any exception; always puts the list back and quits.
    IEnumerator Guarded(IEnumerator inner)
    {
        while (true)
        {
            object cur;
            try
            {
                if (!inner.MoveNext()) break;
                cur = inner.Current;
            }
            catch (Exception e)
            {
                fail++;
                W("FAIL  test crashed: " + e);
                break;
            }
            yield return cur;
        }
        try { if (snapshot != null) RestoreList(); } catch (Exception e) { W("could not restore the hidden list: " + e.Message); }
        W($"RESULT {pass} passed, {fail} failed, {errors.Count} Part Hider errors in the log");
        foreach (string e in errors) W("  " + e);
        W("[PH-TEST-DONE]");
        log.Flush();
        yield return new WaitForSeconds(2f);
        Application.Quit();
    }

    static IEnumerator Until(Func<bool> done, float seconds)
    {
        float t0 = Time.realtimeSinceStartup;
        while (!done() && Time.realtimeSinceStartup - t0 < seconds) yield return null;
    }

    static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

    IEnumerator Run()
    {
        // ---- A throwaway sandbox save, then the VAB --------------------------------
        GameParameters gp = GameParameters.GetDefaultParameters(Game.Modes.SANDBOX, GameParameters.Preset.Normal);
        Game g = GamePersistence.CreateNewGame(SaveName, Game.Modes.SANDBOX, gp, "Squad/Flags/default", GameScenes.SPACECENTER, EditorFacility.VAB);
        HighLogic.CurrentGame = g;
        HighLogic.SaveFolder = SaveName;
        g.startScene = GameScenes.SPACECENTER;
        g.Start();
        yield return Until(() => HighLogic.LoadedScene == GameScenes.SPACECENTER, 120);
        yield return new WaitForSeconds(3f);
        EditorDriver.StartupBehaviour = EditorDriver.StartupBehaviours.START_CLEAN;
        EditorDriver.StartEditor(EditorFacility.VAB);
        yield return Until(() => HighLogic.LoadedScene == GameScenes.EDITOR && EditorPartList.Instance != null &&
                                 EditorPartList.Instance.partGrid != null && StockIcon() != null, 120);
        yield return new WaitForSeconds(3f);
        Check("reached the VAB with a parts list", StockIcon() != null);
        if (StockIcon() == null) yield break;

        Type addonType = typeof(HiddenList).Assembly.GetType("PhilsPartHider.PhilsPartHiderAddon");
        var addon = FindObjectOfType(addonType);
        var window = (HiddenPartsWindow)Get(addon, "window");
        MethodInfo partAt = addonType.GetMethod("PartIconAt", Any);
        Check("found the add-on, its window and PartIconAt", addon != null && window != null && partAt != null);
        if (addon == null || window == null || partAt == null) yield break;
        snapshot = HiddenList.Names.ToList();
        W("hidden before the test: " + snapshot.Count);

        // ---- The parts list follows the hidden list (one rule: the add-on's version watch)
        AvailablePart first = StockIcon().partInfo;
        HiddenList.Add(first.name);
        yield return Frames(3);
        Check("hiding a part takes it out of the parts list", !InPartsList(first), first.name);
        HiddenList.Remove(first.name);
        yield return Frames(3);
        Check("unhiding it puts it back", InPartsList(first), first.name);

        // ---- PH2: only the topmost thing counts, and only a parts-list icon ---------
        EditorPartIcon stock = StockIcon();
        Vector2 stockPoint = ScreenCenter((RectTransform)stock.transform);
        Check("PH2 a stock parts-list icon is picked", (AvailablePart)partAt.Invoke(null, new object[] { stockPoint }) == stock.partInfo,
              $"{stock.partInfo.name}, top hit {TopHit(stockPoint)}");

        AvailablePart variantPart = PartLoader.LoadedPartsList.FirstOrDefault(p =>
            p.Variants != null && p.Variants.Count > 1 && !p.TechHidden && p.category != PartCategories.none && p.name != first.name);
        foreach (string n in new[] { variantPart?.name, PipFake, GoneFake }) if (n != null) HiddenList.Add(n);
        window.Open();
        yield return Frames(3);
        var root = (RectTransform)Get(window, "root");
        var icons = (List<EditorPartIcon>)Get(window, "icons");
        Check("the window opens with tiles", root != null && icons.Count > 0, $"{icons.Count} tiles");
        if (root == null || icons.Count == 0) yield break;

        Vector2 tilePoint = ScreenCenter((RectTransform)icons[0].transform);
        Check("PH2 a tile in the window is never picked", partAt.Invoke(null, new object[] { tilePoint }) == null, "top hit " + TopHit(tilePoint));

        Vector2 oldPos = root.anchoredPosition;
        var parent = (RectTransform)root.parent;
        Canvas pc = parent.GetComponentInParent<Canvas>();
        Camera pcam = pc != null && pc.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? pc.rootCanvas.worldCamera : null;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, stockPoint, pcam, out Vector2 local);
        root.anchoredPosition = local - parent.rect.center;
        yield return Frames(2);
        Check("PH2 a parts-list icon under the window is not picked", partAt.Invoke(null, new object[] { stockPoint }) == null,
              "top hit " + TopHit(stockPoint));
        root.anchoredPosition = oldPos;
        yield return Frames(2);

        // ---- PH1: another save's PIP parts aren't "missing", and Forget keeps them ---
        var missing = (List<string>)Get(window, "missing");
        Check("PH1 a part from another save's PIP isn't listed as missing", !missing.Contains(PipFake), string.Join(", ", missing));
        Check("PH1 a part that really is gone is listed as missing", missing.Contains(GoneFake));
        Call(window, "Forget");
        yield return Frames(2);
        Check("PH1 Forget them keeps the other save's PIP part hidden", HiddenList.Contains(PipFake));
        Check("PH1 Forget them drops the gone part", !HiddenList.Contains(GoneFake));

        // ---- PH6: old tiles leave the layout at once when the grid is refilled ------
        Type sortKey = typeof(HiddenPartsWindow).GetNestedType("SortKey", Any);
        Call(window, "SetSort", Enum.Parse(sortKey, "Mass"));
        yield return null;      // the add-on's Update refills the grid; this runs right after it, same frame
        var content = (RectTransform)Get(window, "content");
        int tiles = ((List<GameObject>)Get(window, "tiles")).Count;
        int active = content.Cast<Transform>().Count(t => t.gameObject.activeSelf);
        Check("PH6 a refill doesn't lay new tiles out after the old ones", active == tiles,
              $"{active} active of {content.childCount} children, {tiles} tiles");
        Call(window, "SetSort", Enum.Parse(sortKey, "Name"));
        yield return Frames(2);

        // ---- PH5: the tile's corner (variant) button unhides, and leaves the paint alone
        if (variantPart == null) Check("PH5 found a part with paint variants to test", false);
        else
        {
            icons = (List<EditorPartIcon>)Get(window, "icons");
            EditorPartIcon vIcon = icons.FirstOrDefault(i => i != null && i.partInfo == variantPart);
            PartVariant before = variantPart.variant;
            Check("PH5 the variant part has a tile with a corner button", vIcon != null && vIcon.btnSwapTexture != null, variantPart.name);
            if (vIcon != null && vIcon.btnSwapTexture != null)
            {
                vIcon.btnSwapTexture.onClick.Invoke();
                yield return Frames(2);
                Check("PH5 the corner button brings the part back", !HiddenList.Contains(variantPart.name));
                Check("PH5 the corner button doesn't change the part's default paint", variantPart.variant == before,
                      $"{before?.Name} -> {variantPart.variant?.Name}");
            }
        }

        // ---- PH3: F2 hides the window, and a hidden window blocks nothing ------------
        GameEvents.onHideUI.Fire();
        yield return Frames(2);
        Check("PH3 F2 hides the window", !root.gameObject.activeInHierarchy);
        Check("PH3 a hidden window isn't under the mouse", !window.MouseOver());
        Check("PH3 a hidden window holds no input lock", InputLockManager.GetControlLock("PhilsPartHiderWindow") == ControlTypes.None);
        GameEvents.onShowUI.Fire();
        yield return Frames(2);
        Check("PH3 F2 again shows it", root.gameObject.activeInHierarchy);

        window.Close();
        yield return Frames(2);
        RestoreList();
        Check("the hidden list is back as it was", HiddenList.Names.SequenceEqual(snapshot), $"{HiddenList.Count} parts");

        // ---- PH4: an unreadable list file is kept, not saved over --------------------
        var path = (string)typeof(HiddenList).GetProperty("FilePath", Any).GetValue(null);
        string dir = Path.GetDirectoryName(path);
        string good = File.ReadAllText(path);
        const string junk = "this is not a hidden list";
        string[] copiesBefore = Directory.GetFiles(dir, "HiddenParts.cfg.unreadable-*");
        try
        {
            File.WriteAllText(path, junk);
            expectReadError = true;
            ResetList();
            int count = HiddenList.Count;
            expectReadError = false;
            string[] copies = Directory.GetFiles(dir, "HiddenParts.cfg.unreadable-*").Except(copiesBefore).ToArray();
            Check("PH4 an unreadable list starts empty", count == 0);
            Check("PH4 the unreadable file is kept as a copy", copies.Length == 1 && File.ReadAllText(copies[0]) == junk,
                  copies.Length + " copies");
            HiddenList.Add("zzSelfTestAfterBad");
            Check("PH4 the next change saves a fresh list", File.ReadAllText(path).Contains("zzSelfTestAfterBad"));
            Check("PH4 the copy is untouched by that save", copies.Length == 1 && File.ReadAllText(copies[0]) == junk);
            foreach (string c in copies) File.Delete(c);
        }
        finally
        {
            expectReadError = false;
            File.WriteAllText(path, good);
            ResetList();
        }
        Check("the list file is back as it was", HiddenList.Names.SequenceEqual(snapshot), $"{HiddenList.Count} parts");
        Check("no Part Hider errors or exceptions in the log", errors.Count == 0, errors.Count + " found");
    }

    // ---- Helpers -------------------------------------------------------------------

    static EditorPartIcon StockIcon() =>
        EditorPartList.Instance?.partGrid?.GetComponentsInChildren<EditorPartIcon>()
            .FirstOrDefault(i => i.gameObject.activeInHierarchy && i.partInfo != null);

    static bool InPartsList(AvailablePart ap) =>
        EditorPartList.Instance.partGrid.GetComponentsInChildren<EditorPartIcon>().Any(i => i.partInfo == ap);

    static Vector2 ScreenCenter(RectTransform rt)
    {
        Canvas c = rt.GetComponentInParent<Canvas>();
        Camera cam = c != null && c.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? c.rootCanvas.worldCamera : null;
        return RectTransformUtility.WorldToScreenPoint(cam, rt.TransformPoint(rt.rect.center));
    }

    static string TopHit(Vector2 point)
    {
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits);
        return hits.Count > 0 ? hits[0].gameObject.name + " under " + (hits[0].gameObject.transform.parent?.name ?? "-") : "nothing";
    }

    void RestoreList()
    {
        HiddenList.RemoveAll(HiddenList.Names.Where(n => !snapshot.Contains(n)));
        foreach (string n in snapshot) HiddenList.Add(n);
        if (!HiddenList.Names.SequenceEqual(snapshot))
        {
            HiddenList.RemoveAll(HiddenList.Names);
            foreach (string n in snapshot) HiddenList.Add(n);
        }
    }

    // Makes HiddenList read its file again on next use.
    static void ResetList() => typeof(HiddenList).GetField("order", Any).SetValue(null, null);

    static object Get(object o, string name)
    {
        Type t = o.GetType();
        FieldInfo f = t.GetField(name, Any);
        return f != null ? f.GetValue(o) : t.GetProperty(name, Any)?.GetValue(o);
    }

    static object Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, Any).Invoke(o, args);
}
