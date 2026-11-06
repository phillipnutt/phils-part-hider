using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PhilsPartHider
{
    // The list of hidden part names, one file shared by every save:
    // GameData/PhilsPartHider/PluginData/HiddenParts.cfg. Module Manager ignores
    // PluginData, so changing the list never triggers a re-patch.
    public static class HiddenList
    {
        // In the order the parts were hidden, oldest first (the "Recent" sort).
        static List<string> order;
        static HashSet<string> set;

        // Goes up by one on every change, so the window knows to refill.
        public static int Version { get; private set; }

        static string FilePath => Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(typeof(HiddenList).Assembly.Location), "..", "PluginData", "HiddenParts.cfg"));

        public static IList<string> Names { get { EnsureLoaded(); return order; } }
        public static int Count { get { EnsureLoaded(); return order.Count; } }
        public static bool Contains(string name) { EnsureLoaded(); return set.Contains(name); }
        public static int IndexOf(string name) { EnsureLoaded(); return order.IndexOf(name); }

        public static bool Add(string name) => Insert(int.MaxValue, name);

        // Puts a part back at its old place in the list (Undo keeps the Recent order).
        public static bool Insert(int index, string name)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(name) || !set.Add(name)) return false;
            order.Insert(Mathf.Clamp(index, 0, order.Count), name);
            Changed();
            return true;
        }

        public static bool Remove(string name)
        {
            EnsureLoaded();
            if (!set.Remove(name)) return false;
            order.Remove(name);
            Changed();
            return true;
        }

        public static void RemoveAll(IEnumerable<string> names)
        {
            EnsureLoaded();
            bool any = false;
            foreach (string n in new List<string>(names))
                if (set.Remove(n)) { order.Remove(n); any = true; }
            if (any) Changed();
        }

        static void Changed()
        {
            Version++;
            Save();
        }

        static void EnsureLoaded()
        {
            if (order != null) return;
            order = new List<string>();
            set = new HashSet<string>();
            try
            {
                if (!File.Exists(FilePath)) return;
                ConfigNode list = ConfigNode.Load(FilePath)?.GetNode("HIDDEN_PARTS");
                if (list == null) return;
                foreach (string name in list.GetValues("part"))
                    if (set.Add(name)) order.Add(name);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[PhilsPartHider] Couldn't read {FilePath}: {e.Message}");
            }
        }

        static void Save()
        {
            try
            {
                var root = new ConfigNode();
                ConfigNode list = root.AddNode("HIDDEN_PARTS");
                foreach (string name in order) list.AddValue("part", name);
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                root.Save(FilePath, "Parts hidden from the VAB/SPH parts list by Phil's Part Hider, oldest first. Delete a line to unhide.");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[PhilsPartHider] Couldn't save {FilePath}: {e.Message}");
            }
        }
    }
}
