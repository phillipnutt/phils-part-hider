using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using KSP.Localization;
using KSP.UI.Screens;
using UnityEngine;

namespace PhilsPartHider
{
    // One of the VAB's "Filter by Function" categories, with its icon.
    public class PartCategory
    {
        public string Name;
        public Texture Icon;
        public string Short;        // shown when there is no icon
        public Func<AvailablePart, bool> Criteria;
        public int Count;

        public bool Has(AvailablePart ap)
        {
            try { return Criteria(ap); }
            catch (Exception) { return false; }
        }
    }

    static class PartFacts
    {
        // The categories exactly as the VAB shows them, read from the game's own
        // part categorizer (so mods that change them are followed). If that fails,
        // the stock categories by each part's config, with two-letter labels.
        public static List<PartCategory> Categories()
        {
            var list = new List<PartCategory>();
            try
            {
                FieldInfo field = typeof(PartCategorizer).GetField("filterFunction", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                var function = PartCategorizer.Instance != null && field != null ? field.GetValue(PartCategorizer.Instance) as PartCategorizer.Category : null;
                if (function?.subcategories != null)
                    foreach (PartCategorizer.Category sub in function.subcategories)
                    {
                        Func<AvailablePart, bool> criteria = sub?.exclusionFilter?.FilterCriteria;
                        if (criteria == null) continue;
                        PartCategorizerButton b = sub.button;
                        string name = b != null ? (b.displayCategoryName ?? b.categorydisplayName ?? b.categoryName) : null;
                        name = string.IsNullOrEmpty(name) ? "Category" : Localizer.Format(name);
                        list.Add(new PartCategory { Name = name, Icon = b?.icon?.iconNormal, Short = Abbreviate(name), Criteria = criteria });
                    }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PhilsPartHider] Couldn't read the VAB's categories, using the stock ones: {e.Message}");
                list.Clear();
            }
            if (list.Count == 0) list = StockCategories();
            return list;
        }

        static List<PartCategory> StockCategories()
        {
            var stock = new (PartCategories cat, string name)[]
            {
                (PartCategories.Pods, "Pods"), (PartCategories.FuelTank, "Fuel Tanks"), (PartCategories.Engine, "Engines"),
                (PartCategories.Control, "Command and Control"), (PartCategories.Structural, "Structural"), (PartCategories.Robotics, "Robotics"),
                (PartCategories.Coupling, "Coupling"), (PartCategories.Payload, "Payload"), (PartCategories.Aero, "Aerodynamics"),
                (PartCategories.Ground, "Ground"), (PartCategories.Thermal, "Thermal"), (PartCategories.Electrical, "Electrical"),
                (PartCategories.Communication, "Communication"), (PartCategories.Science, "Science"), (PartCategories.Cargo, "Cargo"),
                (PartCategories.Utility, "Utility"),
            };
            return stock.Select(s =>
            {
                PartCategories c = s.cat;
                return new PartCategory { Name = s.name, Short = Abbreviate(s.name), Criteria = ap => ap.category == c };
            }).ToList();
        }

        static string Abbreviate(string name)
        {
            string letters = new string(name.Where(char.IsLetter).ToArray());
            return letters.Length >= 2 ? letters.Substring(0, 1).ToUpperInvariant() + letters.Substring(1, 1).ToLowerInvariant() : letters;
        }

        static readonly Dictionary<string, string> mods = new Dictionary<string, string>();

        // The mod a part comes from, by its folder: "Stock", "Making History",
        // "Near Future Propulsion" (from NearFuturePropulsion).
        public static string ModOf(AvailablePart ap)
        {
            string url = ap?.partUrl ?? "";
            string[] parts = url.Split('/');
            string top = parts[0] == "SquadExpansion" && parts.Length > 1 ? parts[1] : parts[0];
            if (mods.TryGetValue(top, out string name)) return name;
            switch (top)
            {
                case "": name = ""; break;
                case "Squad": name = "Stock"; break;
                case "MakingHistory": name = "Making History"; break;
                case "Serenity": name = "Breaking Ground"; break;
                case "ReStockPlus": name = "ReStock+"; break;
                default:
                    var sb = new System.Text.StringBuilder();
                    for (int i = 0; i < top.Length; i++)
                    {
                        if (i > 0 && char.IsUpper(top[i]) && char.IsLower(top[i - 1])) sb.Append(' ');
                        sb.Append(top[i] == '_' ? ' ' : top[i]);
                    }
                    name = sb.ToString().Trim();
                    break;
            }
            return mods[top] = name;
        }

        // Full mass, with the resources it starts with.
        public static float MassOf(AvailablePart ap)
        {
            Part p = ap.partPrefab;
            if (p == null) return 0;
            return p.mass + p.GetResourceMass();
        }

        // "1.25 m", "Radial", "Mk2": the first size the part attaches at.
        public static string SizeOf(AvailablePart ap)
        {
            string[] profiles = (ap.bulkheadProfiles ?? "").Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
            foreach (string p in profiles)
            {
                string size = SizeName(p);
                if (size != null) return size;
            }
            if (profiles.Contains("srf")) return "Radial";
            return profiles.Length > 0 ? profiles[0] : "";
        }

        static string SizeName(string profile)
        {
            switch (profile)
            {
                case "size0": return "0.625 m";
                case "size1": return "1.25 m";
                case "size1p5": return "1.875 m";
                case "size2": return "2.5 m";
                case "size3": return "3.75 m";
                case "size4": return "5 m";
                case "size5": return "7.5 m";
                case "mk1": return "Mk1";
                case "mk2": return "Mk2";
                case "mk3": return "Mk3";
                default: return null;
            }
        }

        public static string Mass(float t)
        {
            var inv = CultureInfo.InvariantCulture;
            if (t < 1) return (t * 1000).ToString(t < 0.01f ? "0.#" : "0", inv) + " kg";
            return t.ToString(t < 10 ? "0.00" : "0.0", inv) + " t";
        }

        public static string Funds(float cost) => cost.ToString("N0", CultureInfo.InvariantCulture);
    }
}
