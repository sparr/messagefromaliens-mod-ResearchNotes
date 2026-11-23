using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ResearchNotes
{
    // A freeform notes field drawn over a block of cells: columns FirstColumn to LastColumn, on
    // rows FirstRow down to at most MaxRows rows. Rows whose columns hold a glyph of Sector are
    // left out, from the first such row down. The area is outlined on its left and bottom sides
    // in the color of the lines that divide the screen, and the field types in the label color at
    // the labels' font size, shrinking to fit.
    //
    // Each subclass picks the save key of the note for what the screen shows. Notes are stored in
    // Global.savedata.dict under that key.
    internal abstract class NoteArea
    {
        // World units between the cells' edges and the outline, and between the outline and the text.
        private const float Inset = 0.12f;
        private const float Padding = 0.1f;
        // The labels' font size, per world unit, as AlienCharacter.UpdatePos sets it; and the
        // smallest the text shrinks to. The game's labels shrink with TMP auto sizing too.
        private const float FontSize = 0.3f;
        private const float MinFontSize = 0.02f;

        protected abstract string Name { get; }
        protected abstract int FirstColumn { get; }
        protected abstract int LastColumn { get; }
        protected abstract int FirstRow { get; }
        protected abstract int MaxRows { get; }
        // The sector of the glyphs that take rows away from the area.
        protected abstract string Sector { get; }
        protected virtual int CharacterLimit => 4000;
        // Whether the area's left and bottom sides are drawn. Without them, the area is bounded
        // only by the game's dividers and the screen's edge.
        protected virtual bool Outlined => true;
        // Whether the area's setting is on.
        internal virtual bool Enabled => Plugin.Enabled.Value;

        // Whether the game state the note depends on changed since the last call, apart from a
        // redraw. Records the new state. Called every frame.
        protected abstract bool StateChanged(Draw m);

        // The save key of the note for what the screen shows, or null when it shows none.
        protected abstract string Key(Draw m);

        private TMP_InputField field;
        private LineRenderer outline;
        private TMP_SelectionCaret caret;
        // The save key the field edits, and the number of rows the area spans.
        private string key;
        private int rows;
        private bool dirty = true;
        private bool lastVisible;
        // The note as last written to disk, so an edit that ends unchanged does not save.
        private string saved;
        private bool loggedError;

        internal void MarkDirty() => dirty = true;

        internal void Tick(Draw m, bool visible)
        {
            try
            {
                // StateChanged runs every frame so that it records the state.
                var changed = m != null && StateChanged(m);
                if (dirty || visible != lastVisible || changed)
                {
                    dirty = false;
                    lastVisible = visible;
                    var k = visible && m != null ? Key(m) : null;
                    var r = k != null ? FreeRows(m) : 0;
                    Show(r > 0 ? k : null, r);
                }
                Position();
            }
            catch (Exception e)
            {
                if (!loggedError)
                {
                    Plugin.Log.LogError($"{Name}: {e}");
                    loggedError = true;
                }
            }
        }

        // The rows from FirstRow down, at most MaxRows, whose columns hold no glyph of Sector.
        // alien_slots counts rows from the bottom of the screen.
        private int FreeRows(Draw m)
        {
            var slots = m.alien_slots;
            if (slots == null)
            {
                return 0;
            }
            var bottom = slots.GetLength(1) - 1;
            var n = 0;
            for (var y = FirstRow; y < FirstRow + MaxRows; y++)
            {
                for (var x = FirstColumn; x <= LastColumn; x++)
                {
                    var ac = slots[x, bottom - y];
                    if (ac != null && ac.gameObject.activeInHierarchy && ac.sector == Sector && !string.IsNullOrEmpty(ac.alien_txt))
                    {
                        return n;
                    }
                }
                n++;
            }
            return n;
        }

        // The words of the left panel's title as drawn, or null when it shows none. Another mod,
        // such as Calculator, may draw over the left panel without a history entry of its own,
        // so a note for the panel's entry is shown only while its title is drawn.
        protected static string DrawnTitle(Draw m)
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

        private static string Get(string k)
        {
            var dict = Global.savedata?.dict;
            return dict != null && dict.TryGetValue(k, out var t) ? t : "";
        }

        private void Show(string k, int r)
        {
            var m = Global.manager;
            if (k == null || m == null)
            {
                if (outline != null)
                {
                    outline.gameObject.SetActive(false);
                }
                if (field != null && field.gameObject.activeSelf)
                {
                    // Deactivating ends the edit, which saves it.
                    field.gameObject.SetActive(false);
                    ReleaseSelection();
                }
                return;
            }
            if (field == null)
            {
                field = CreateInput(m);
                outline = Outlined ? CreateOutline(m) : null;
            }
            var editing = field.isFocused && k == key;
            if (!editing)
            {
                // Deactivating ends the edit, which saves it.
                if (field.isFocused)
                {
                    field.DeactivateInputField();
                }
                ReleaseSelection();
                key = k;
                saved = Get(k);
                field.SetTextWithoutNotify(saved);
            }
            rows = r;
            PlaceOutline(m);
            field.textComponent.color = m.DefaultColor(0);
            field.gameObject.SetActive(true);
        }

        // Writes each change to the save data, so the note survives whatever ends the edit.
        private void OnValueChanged(string s)
        {
            var dict = Global.savedata?.dict;
            if (dict == null || key == null)
            {
                return;
            }
            if (string.IsNullOrWhiteSpace(s))
            {
                dict.Remove(key);
            }
            else
            {
                dict[key] = s;
            }
        }

        private void Save()
        {
            if (key == null)
            {
                return;
            }
            var now = Get(key);
            if (now != saved)
            {
                saved = now;
                Global.SaveGame();
            }
        }

        // An edit that ends without a click, such as on Escape, leaves the field selected, so the
        // next Escape, Enter, or arrow key would act on it. The game's labels let go the same way
        // in AlienCharacter.EndEdit.
        private void ReleaseSelection()
        {
            var es = EventSystem.current;
            if (field != null && es != null && !es.alreadySelecting && !field.isFocused && es.currentSelectedGameObject == field.gameObject)
            {
                es.SetSelectedGameObject(null);
            }
        }

        // TMP_InputField.UpdateLabel shows the text with a zero-width space after it, where the
        // caret goes past the last character. Auto sizing measures only lines that hold a
        // character it treats as visible, and it skips that space, so an empty last line would
        // not count. A word joiner, which TMP gives every font as a zero-width glyph, counts.
        // It replaces the space one for one, so the caret's character indices are unchanged.
        // TMP runs this once each time it lays out the text.
        private sealed class TrailingJoiner : ITextPreprocessor
        {
            public string PreprocessText(string text) =>
                text != null && text.EndsWith("\u200b", StringComparison.Ordinal)
                    ? text.Substring(0, text.Length - 1) + "\u2060"
                    : text;
        }

        private TMP_InputField CreateInput(Draw m)
        {
            var go = UnityEngine.Object.Instantiate(m.text_prefab);
            go.name = $"ResearchNotes.{Name}.Field";
            go.transform.SetParent(m.world_canva.transform);
            var input = go.GetComponent<TMP_InputField>();
            // The labels' font, which differs from the prefab's for some languages.
            input.fontAsset = m.txt_slots[0, 0].fontAsset;
            input.characterLimit = CharacterLimit;
            input.richText = false;
            // A click places the caret instead of selecting the whole note.
            input.onFocusSelectAll = false;
            // Deactivating the field ends the edit, so onEndEdit is the one place that saves.
            input.resetOnDeActivation = true;
            // Enter inserts a newline, and the up and down arrows move between lines. The text wraps,
            // and the prefab's text already shrinks to fit.
            input.lineType = TMP_InputField.LineType.MultiLineNewline;
            // Arrow keys stay in the field instead of moving the selection to a nearby label.
            var nav = input.navigation;
            nav.mode = Navigation.Mode.None;
            input.navigation = nav;
            input.onValueChanged.AddListener(OnValueChanged);
            input.onEndEdit.AddListener(_ =>
            {
                Save();
                ReleaseSelection();
            });
            var t = input.textComponent;
            // Backslashes stay as typed instead of starting escape sequences such as \n.
            t.parseCtrlCharacters = false;
            t.textPreprocessor = new TrailingJoiner();
            t.alignment = TextAlignmentOptions.TopLeft;
            input.scrollSensitivity = 0f;
            // Clips text that does not fit even at the smallest size.
            go.AddComponent<RectMask2D>();
            go.SetActive(false);
            return input;
        }

        // The area's corners in world space: columns FirstColumn to LastColumn, from the top of
        // FirstRow's glyphs to the bottom of the last row's labels, which are a third of a row
        // below the glyph, as Draw.ResetTextPosition places them.
        private void Area(Draw m, out Vector2 min, out Vector2 max)
        {
            // Cell corners: the top left of the first row's cell, and the bottom right of the last
            // column's cell a row lower, where that row's labels end.
            min = m.PosToScreenPos(FirstColumn, FirstRow + rows, false);
            max = m.PosToScreenPos(LastColumn + 1, FirstRow, false);
        }

        // The area's top and right edges are the game's dividers, or the screen's right edge, so
        // only its left and bottom sides are drawn, running into them.
        private LineRenderer CreateOutline(Draw m)
        {
            var line = m.DrawLineRenderer(Vector2.zero, Vector2.zero, 0);
            line.gameObject.name = $"ResearchNotes.{Name}.Outline";
            line.positionCount = 3;
            return line;
        }

        private void PlaceOutline(Draw m)
        {
            if (outline == null)
            {
                return;
            }
            Area(m, out var min, out var max);
            min += Vector2.one * Inset;
            outline.SetPosition(0, new Vector3(min.x, max.y));
            outline.SetPosition(1, new Vector3(min.x, min.y));
            outline.SetPosition(2, new Vector3(max.x, min.y));
            outline.startColor = outline.endColor = m.DefaultColor(0);
            outline.gameObject.SetActive(true);
        }

        // Places the field inside the outline. The field's pivot is its bottom-left corner, as
        // for the game's labels.
        private void Position()
        {
            if (field == null || !field.gameObject.activeSelf)
            {
                return;
            }
            var cam = Camera.main;
            var m = Global.manager;
            if (cam == null || m == null)
            {
                return;
            }
            Area(m, out var min, out var max);
            Vector2 a = cam.WorldToScreenPoint(min + Vector2.one * (Inset + Padding));
            Vector2 b = cam.WorldToScreenPoint(max - Vector2.one * Padding);
            field.transform.position = a;
            ((RectTransform)field.transform).sizeDelta = b - a;
            var perUnit = cam.pixelHeight / cam.orthographicSize / 2f;
            var t = field.textComponent;
            t.fontSizeMax = FontSize * perUnit;
            t.fontSizeMin = MinFontSize * perUnit;
            // The field scrolls to show the caret, which can reach a little past the text that
            // auto sizing fitted. The text, and the caret drawn over it, stay at the top instead.
            if (t.rectTransform.anchoredPosition != Vector2.zero)
            {
                t.rectTransform.anchoredPosition = Vector2.zero;
            }
            // TMP creates the caret when the field is first enabled, and keeps it.
            if (caret == null)
            {
                caret = field.GetComponentInChildren<TMP_SelectionCaret>();
            }
            if (caret != null && caret.rectTransform.anchoredPosition != Vector2.zero)
            {
                caret.rectTransform.anchoredPosition = Vector2.zero;
            }
        }
    }
}
