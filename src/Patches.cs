using HarmonyLib;

namespace ResearchNotes
{
    // A redraw may change the left panel or the colors, so the notes area is rebuilt.
    [HarmonyPatch(typeof(Draw), nameof(Draw.UpdateText))]
    internal static class UpdateTextPatch
    {
        private static void Postfix() => Notes.MarkDirty();
    }
}
