using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

// The mod's name in glyphs, as game word IDs, for Mod Settings and any other mod that lists mods.
[assembly: AssemblyMetadata("MessageFromAliens.Name", ResearchNotes.Plugin.NameGlyphs)]

namespace ResearchNotes
{
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(InterfaceTweaksPageNotes.Guid, BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "sparr.messagefromaliens.researchnotes";
        public const string Name = "Research Notes";
        public const string Version = "0.1.0";

        // The mod's name and its setting's label, as game word IDs.
        internal const string NameGlyphs = "linguistics de science de write";

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<bool> PageNotes;

        private void Awake()
        {
            Log = Logger;

            Enabled = Config.Bind("Notes", "Enabled", true,
                Described("Show notes fields in a glyph's information panel and in each stage of the minigame with seven stages.", NameGlyphs));
            PageNotes = Config.Bind("Notes", "PageNotes", true,
                Described("While the left panel shows a page's explanation, the explain button opens a notes page for that page instead, with a field for English text. While a page's notes show, opening another page shows its notes.", "page draw"));

            Config.SettingChanged += (_, _) => Notes.MarkDirty();

            var harmony = new Harmony(Guid);
            harmony.PatchAll(typeof(Plugin).Assembly);
            InterfaceTweaksPageNotes.Disable(harmony);
            Log.LogInfo($"{Name} {Version} loaded");
        }

        // Mod Settings shows a setting tagged with its name in glyphs, as game word IDs.
        private static ConfigDescription Described(string description, string glyphs)
        {
            var tags = new Dictionary<string, string> { ["MessageFromAliens.Label"] = glyphs };
            return new ConfigDescription(description, null, tags);
        }

        // Plugin objects must not add components or start coroutines from Awake, which runs
        // during Application's static constructor; Unity crashes. LateUpdate is safe.
        private void LateUpdate() => Notes.Tick();
    }
}
