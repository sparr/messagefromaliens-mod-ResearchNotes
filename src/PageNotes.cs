using System;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace ResearchNotes
{
    // A notes page in the left panel for each right panel page. While the left panel shows the
    // explanation the right panel's explain button opens, that button becomes a notes button,
    // which opens the page's notes. While the notes show, the explain button is back. While the
    // left panel shows a page's notes, opening another page on the right shows that page's notes.
    //
    // A notes page is a left panel history entry titled with the page's name and the notes
    // glyph, with no text. Its text rows hold the note, with no outline, since the panel's
    // dividers and the screen's edge bound them.
    internal sealed class PageNotes : NoteArea
    {
        internal const string KeyPrefix = "researchnotes.page:";
        // The game's explain button, and the glyph that takes its place.
        private const string ExplainButton = "tutorial";
        private const string NotesButton = "draw";
        private const string TitleSuffix = " de " + NotesButton;
        // Global.GetInfo's category for page explanations, and the suffix of an explanation's
        // name after its page's name.
        private const string InfoCategory = "alieninfo";
        private const string ExplanationSuffix = " de tutorial";
        private const string RightMenu = "rightmenu";

        protected override string Name => "Page";
        protected override int FirstColumn => 0;
        protected override int LastColumn => 11;
        protected override int FirstRow => 1;
        protected override int MaxRows => 8;
        protected override string Sector => "lefttext";
        protected override int CharacterLimit => 20000;
        protected override bool Outlined => false;
        internal override bool Enabled => Plugin.PageNotes.Value;

        // Whether the right menu drawn last has the notes button in place of the explain button.
        private static bool buttonShown;

        private int lastIndex = -1;
        private string lastEntry;

        private static string Entry(string p) => p + TitleSuffix + "&";

        // The page whose notes the history entry is, or null.
        private static string NotesPage(string entry) =>
            entry != null && entry.EndsWith(TitleSuffix + "&", StringComparison.Ordinal)
                ? entry.Substring(0, entry.Length - TitleSuffix.Length - 1)
                : null;

        private static string CurrentEntry(Draw d) =>
            d?.text_history != null && d.currenttext >= 0 && d.currenttext < d.text_history.Count ? d.text_history[d.currenttext] : null;

        protected override bool StateChanged(Draw m)
        {
            var index = m.currenttext;
            var entry = CurrentEntry(m);
            if (index == lastIndex && ReferenceEquals(entry, lastEntry))
            {
                return false;
            }
            lastIndex = index;
            lastEntry = entry;
            return true;
        }

        protected override string Key(Draw m)
        {
            var p = NotesPage(lastEntry);
            if (p == null)
            {
                return null;
            }
            var title = string.Join(" ", Global.StringToListString(p + TitleSuffix, modify: false).Where(Drawn));
            return DrawnTitle(m) == title ? KeyPrefix + p : null;
        }

        // Tokens Draw.Write draws a glyph for: not value tokens or bracketed tokens, which begin
        // the names Mod Settings opens its pages under.
        private static bool Drawn(string token) => token != "" && !token.StartsWith("(") && !token.StartsWith("[");

        // The explanation Draw.GetHelp opens for the current page, or null if it opens none.
        private static string HelpTitle(Draw d)
        {
            var p = d.current_page ?? "";
            // A page whose name starts with a token Draw.Write does not draw is no game page, such
            // as Mod Settings' pages, whose explanations it opens by this rule whatever their names.
            if (p.StartsWith("["))
            {
                return Global.GetInfo(InfoCategory, p + ExplanationSuffix) != "" ? p + ExplanationSuffix : null;
            }
            var later = Global.CountWin("geometry riddle") >= 5 && !Global.isDemo;
            if (p.Contains("riddle") && p != "bonus arithmetic riddle" && p != "last riddle")
            {
                return later ? "riddle de tutorial 2" : "riddle de tutorial";
            }
            if (d.CurrentPageIsMessage())
            {
                return later ? "message de tutorial 2" : "message de tutorial";
            }
            if (p == "hero de arena game")
            {
                return "hero de arena de tutorial";
            }
            return Global.GetInfo(InfoCategory, p + ExplanationSuffix) != "" ? p + ExplanationSuffix : null;
        }

        // Whether the left panel shows the current page's explanation.
        private static bool ShowsHelp(Draw d)
        {
            var entry = CurrentEntry(d);
            var help = HelpTitle(d);
            return entry != null && help != null && entry.StartsWith(help + "&", StringComparison.Ordinal);
        }

        private static void Open(Draw d)
        {
            d.PushLeftText(d.current_page + TitleSuffix, "");
            d.UpdateText();
        }

        // While the left panel shows another page's notes, show the current page's instead. The
        // entry is replaced rather than a new one pushed, so changing pages does not fill the
        // history with notes.
        [HarmonyPatch(typeof(Draw), nameof(Draw.UpdateText1))]
        private static class UpdateText1Patch
        {
            private static void Prefix(Draw __instance)
            {
                try
                {
                    if (!Plugin.PageNotes.Value)
                    {
                        return;
                    }
                    var d = __instance;
                    var shown = NotesPage(CurrentEntry(d));
                    if (shown == null || d.current_page == null || shown == d.current_page)
                    {
                        return;
                    }
                    d.text_history[d.currenttext] = Entry(d.current_page);
                    if (d.currenttext < d.currentleftpage.Count)
                    {
                        d.currentleftpage[d.currenttext] = 0;
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"Page notes failed: {e}");
                }
            }
        }

        // The right menu's explain button becomes the notes button while the left panel shows
        // the explanation.
        [HarmonyPatch]
        private static class WritePatch
        {
            // The overload of Draw.Write that takes the text as one string, which the game calls
            // for every section of a page.
            private static System.Reflection.MethodBase TargetMethod() =>
                AccessTools.Method(typeof(Draw), nameof(Draw.Write), new[]
                {
                    typeof(string), typeof(int), typeof(int), typeof(int), typeof(bool), typeof(bool),
                    typeof(bool), typeof(float), typeof(string), typeof(float), typeof(bool), typeof(int), typeof(int),
                });

            private static void Prefix(Draw __instance, ref string s, string sec)
            {
                try
                {
                    if (sec != RightMenu || s == null)
                    {
                        return;
                    }
                    buttonShown = false;
                    if (!Plugin.PageNotes.Value || !ShowsHelp(__instance))
                    {
                        return;
                    }
                    var tokens = s.Split(' ');
                    var i = Array.IndexOf(tokens, ExplainButton);
                    if (i >= 0)
                    {
                        tokens[i] = NotesButton;
                        s = string.Join(" ", tokens);
                        buttonShown = true;
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"Page notes button failed: {e}");
                }
            }
        }

        // A left click on the notes button opens the page's notes. The game's own click handling
        // does nothing for that glyph in the right menu.
        [HarmonyPatch(typeof(Draw), "Update")]
        private static class UpdatePatch
        {
            private static void Prefix(Draw __instance)
            {
                try
                {
                    if (!buttonShown || !Plugin.PageNotes.Value || !Input.GetMouseButtonDown(0))
                    {
                        return;
                    }
                    var cam = Camera.main;
                    if (cam == null || !Physics.Raycast(cam.ScreenPointToRay(Input.mousePosition), out var hit))
                    {
                        return;
                    }
                    var ac = hit.transform.GetComponentInParent<AlienCharacter>();
                    if (ac != null && ac.sector == RightMenu && ac.alien_txt == NotesButton)
                    {
                        Open(__instance);
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"Page notes click failed: {e}");
                }
            }
        }
    }
}
