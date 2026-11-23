using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;

namespace ResearchNotes
{
    // Interface Tweaks 0.3.0 has page notes of its own, saved under OldKeyPrefix. Research Notes
    // turns them off when that version is loaded, and moves their notes to its own keys.
    internal static class InterfaceTweaksPageNotes
    {
        internal const string Guid = "sparr.messagefromaliens.interfacetweaks";
        private const string OldKeyPrefix = "interfacetweaks.pagenote:";
        private const string TypeName = "InterfaceTweaks.PageNotes";
        // Its methods that show the field, the button, and the notes of another page, and open
        // the notes. Each has an exception handler, so none is inlined into its callers.
        private static readonly string[] Methods = { "Tick", "UpdatePrefix", "WritePrefix", "UpdateText1Prefix" };

        // Interface Tweaks loads first when present, as Research Notes declares it a soft
        // dependency.
        internal static void Disable(Harmony harmony)
        {
            try
            {
                if (!Chainloader.PluginInfos.TryGetValue(Guid, out var info) || !(info.Instance is BaseUnityPlugin plugin))
                {
                    return;
                }
                var type = plugin.GetType().Assembly.GetType(TypeName);
                if (type == null)
                {
                    return;
                }
                var skip = new HarmonyMethod(AccessTools.Method(typeof(InterfaceTweaksPageNotes), nameof(Skip)));
                foreach (var name in Methods)
                {
                    var m = AccessTools.Method(type, name);
                    if (m == null)
                    {
                        Plugin.Log.LogWarning($"Interface Tweaks' page notes have no {name}");
                        continue;
                    }
                    harmony.Patch(m, prefix: skip);
                }
                // Mod Settings lists only settings with a label, so its setting, which now does
                // nothing, is not listed.
                if (plugin.Config.TryGetEntry<bool>("Notes", "PageNotes", out var entry))
                {
                    foreach (var tags in entry.Description?.Tags?.OfType<IDictionary<string, string>>() ?? Enumerable.Empty<IDictionary<string, string>>())
                    {
                        tags.Remove("MessageFromAliens.Label");
                    }
                }
                Plugin.Log.LogInfo($"Turned off the page notes of Interface Tweaks {info.Metadata.Version}");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Turning off Interface Tweaks' page notes failed: {e}");
            }
        }

        private static bool Skip() => false;

        // Moves the loaded save's Interface Tweaks page notes to Research Notes' keys. A page
        // with a different note under both keeps both, Research Notes' first. The move is saved
        // with the game's next save.
        [HarmonyPatch(typeof(Global), nameof(Global.LoadGame))]
        private static class LoadGamePatch
        {
            // A finalizer, so that it runs also when LoadGame throws after replacing the save, as
            // its achievement calls after the load can. First, so that Interface Tweaks' undo
            // copies the dictionary after the move and does not record it as a change.
            [HarmonyFinalizer]
            [HarmonyPriority(Priority.First)]
            private static void Finalizer()
            {
                try
                {
                    var dict = Global.savedata?.dict;
                    if (dict == null)
                    {
                        return;
                    }
                    var old = dict.Keys.Where(k => k.StartsWith(OldKeyPrefix, StringComparison.Ordinal)).ToList();
                    foreach (var k in old)
                    {
                        var note = dict[k];
                        dict.Remove(k);
                        if (string.IsNullOrWhiteSpace(note))
                        {
                            continue;
                        }
                        var key = PageNotes.KeyPrefix + k.Substring(OldKeyPrefix.Length);
                        dict[key] = dict.TryGetValue(key, out var current) && !string.IsNullOrWhiteSpace(current) && current != note
                            ? current + "\n\n" + note
                            : note;
                    }
                    if (old.Count > 0)
                    {
                        Plugin.Log.LogInfo($"Moved {old.Count} page notes from Interface Tweaks");
                        Notes.MarkDirty();
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"Moving Interface Tweaks' page notes failed: {e}");
                }
            }
        }
    }
}
