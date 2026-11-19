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
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "sparr.messagefromaliens.researchnotes";
        public const string Name = "Research Notes";
        public const string Version = "0.1.0";

        // The mod's name and its setting's label, as game word IDs.
        internal const string NameGlyphs = "linguistics de science de write";

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> Enabled;

        private void Awake()
        {
            Log = Logger;

            Enabled = Config.Bind("Notes", "Enabled", true,
                Described("Show a notes field in the top right of a glyph's information panel.", NameGlyphs));

            Config.SettingChanged += (_, _) => Notes.MarkDirty();

            new Harmony(Guid).PatchAll(typeof(Plugin).Assembly);
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
