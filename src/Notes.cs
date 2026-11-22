using System.Collections.Generic;
using HarmonyLib;

namespace ResearchNotes
{
    // The notes areas, ticked once a frame and rebuilt after every redraw.
    internal static class Notes
    {
        private static readonly NoteArea[] Areas = { new WordNotes(), new StageNotes() };

        // The fields stay while the game hides translations; they hold the player's own text.
        private static bool Visible => Plugin.Enabled.Value;

        internal static void MarkDirty()
        {
            foreach (var a in Areas)
            {
                a.MarkDirty();
            }
        }

        internal static void Tick()
        {
            var m = Global.manager;
            var visible = Visible;
            foreach (var a in Areas)
            {
                a.Tick(m, visible);
            }
        }
    }

    // A note for each word, in the top right of the word's information panel on the left:
    // columns 7 to the panel's last column, on rows 1 to 3, which the type and composition lines
    // never reach. Rows that hold glyphs, such as a token's definition starting on row 3, are
    // left out.
    internal sealed class WordNotes : NoteArea
    {
        internal const string KeyPrefix = "researchnotes.note:";

        protected override string Name => "Word";
        protected override int FirstColumn => 7;
        protected override int LastColumn => 11;
        protected override int FirstRow => 1;
        protected override int MaxRows => 3;
        protected override string Sector => "lefttext";

        // What the left panel showed last. The panel changes only on a redraw, which marks the
        // area dirty, or when its history entry or page changes.
        private int lastIndex = -1;
        private string lastEntry;
        private int lastPage;

        protected override bool StateChanged(Draw m)
        {
            var index = m.currenttext;
            var entry = Entry(m, index);
            var page = entry != null && index < m.currentleftpage.Count ? m.currentleftpage[index] : 0;
            if (index == lastIndex && ReferenceEquals(entry, lastEntry) && page == lastPage)
            {
                return false;
            }
            lastIndex = index;
            lastEntry = entry;
            lastPage = page;
            return true;
        }

        private static string Entry(Draw m, int index) =>
            m.text_history != null && index >= 0 && index < m.text_history.Count ? m.text_history[index] : null;

        protected override string Key(Draw m)
        {
            if (lastEntry == null || lastPage != 0)
            {
                return null;
            }
            var w = Word(m, lastEntry);
            return w != null ? KeyPrefix + w : null;
        }

        // The panel's word when the entry is a word's information panel. Otherwise null.
        private static string Word(Draw m, string entry)
        {
            // PushLeftText separates title and text with "&", or "&&" for text it does not parse.
            var sep = entry.IndexOf('&');
            if (sep < 0 || Global.alien_text == null)
            {
                return null;
            }
            var title = entry.Substring(0, sep);
            var info = sep + 1 < entry.Length && entry[sep + 1] == '&' ? sep + 2 : sep + 1;
            if (!Global.alien_text.ContainsKey(title)
                || string.CompareOrdinal(entry, info, title, 0, title.Length) != 0
                || info + title.Length >= entry.Length || entry[info + title.Length] != ' ')
            {
                return null;
            }
            // Another mod, such as Calculator, may draw over the left panel without a history
            // entry of its own, so the drawn title must be the entry's.
            return DrawnTitle(m) == title ? title : null;
        }

        // The words of the left panel's title as drawn, or null when it shows none.
        private static string DrawnTitle(Draw m)
        {
            if (m.alien_left_title == null)
            {
                return null;
            }
            var words = new List<string>();
            foreach (var ac in m.alien_left_title)
            {
                if (ac != null && ac.gameObject.activeInHierarchy && ac.sector == "lefttitle")
                {
                    words.Add(ac.alien_txt);
                }
            }
            return words.Count > 0 ? string.Join(" ", words) : null;
        }
    }

    // A note for each stage of the understand rule game, in the top right of the right panel's
    // lower part, which holds the stage's examples and tests: columns 18 to 23 on rows 2 to 5.
    // The example and test lines are at most six glyphs wide, so only wrapped lines, such as the
    // rule shown after a win, take rows away.
    internal sealed class StageNotes : NoteArea
    {
        internal const string KeyPrefix = "researchnotes.level:";
        private const string Page = "understand rule";

        private static readonly AccessTools.FieldRef<Draw, int> Level = AccessTools.FieldRefAccess<Draw, int>("understand_lv");

        protected override string Name => "Stage";
        protected override int FirstColumn => 18;
        protected override int LastColumn => 23;
        // The game puts one line of buttons above the divider on a stage's page.
        protected override int FirstRow => 2;
        protected override int MaxRows => 4;
        protected override string Sector => "righttextdown";

        private string lastPage;
        private int lastLevel;

        protected override bool StateChanged(Draw m)
        {
            var page = m.current_page;
            var level = Level(m);
            if (page == lastPage && level == lastLevel)
            {
                return false;
            }
            lastPage = page;
            lastLevel = level;
            return true;
        }

        // Level 0 is the list of stages.
        protected override string Key(Draw m) =>
            lastPage == Page && lastLevel != 0 ? KeyPrefix + lastLevel : null;
    }
}
