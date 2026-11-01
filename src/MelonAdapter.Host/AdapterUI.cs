using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using MelonAdapter.Runtime;
using MelonAdapter.Runtime.Localization;
using MelonAdapter.Runtime.Mods;
using MelonLoader;
using UnityEngine;

namespace MelonAdapter.Host
{
    /// <summary>
    /// In-game settings page for the adapter, drawn inside the UnityModManager mod window
    /// (ModEntry.OnGUI). It is a debugging aid: it shows what was discovered and why something failed,
    /// lets the user open the folders and files the adapter writes, and edits preference entries and a
    /// few configuration keys without leaving the game.
    ///
    /// Every host action goes through the public runtime API (AdapterRuntime.DisableMod,
    /// AdapterConfig.Save, MelonPreferences.Save, ...) so the UI never keeps state of its own that the
    /// runtime cannot see. All text comes from <c>lang.json</c> (Chinese and English ship with the
    /// adapter); a key that is missing from both languages is shown as-is so a broken translation never
    /// hides a control.
    /// </summary>
    internal static class AdapterUI
    {
        private enum Page
        {
            Overview = 0,
            Mods = 1,
            Preferences = 2,
            Settings = 3
        }

        private const float MessageSeconds = 6f;

        private static Page _page = Page.Overview;

        private static Vector2 _overviewScroll;
        private static Vector2 _modsScroll;
        private static Vector2 _detailScroll;
        private static Vector2 _prefsScroll;
        private static Vector2 _settingsScroll;
        private static Vector2 _logScroll;

        private static string _selectedModPath;
        private static Assembly _prefsFilter;

        private static string _logText;
        private static float _logReadAt = -1000f;

        private static string _message;
        private static float _messageAt = -1000f;
        private static bool _messageIsError;

        private static bool _prefsDirty;
        private static string _maxExceptionsBuffer;

        private static LocalizationCatalog _language;

        private static readonly Dictionary<string, string> Buffers = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> BufferErrors = new Dictionary<string, string>(StringComparer.Ordinal);

        private static GUIStyle _wrap;
        private static GUIStyle _bold;

        /// <summary>
        /// Height of the page viewport, in GUI units. UnityModManager draws a mod page inside its own tab
        /// scroll view, where a scroll view without an explicit height collapses to a few dozen pixels (the
        /// page then looks cut off after one or two lines). The page therefore sizes itself: a share of the
        /// screen, clamped so it stays usable on very small and very large resolutions. The scale is read
        /// from GUI.matrix because UnityModManager scales the whole window.
        /// </summary>
        private static float PageHeight
        {
            get
            {
                float scale = Mathf.Max(0.0001f, GUI.matrix.m00);
                float screen = Screen.height / scale;
                float height = screen * 0.62f - 150f;
                return Mathf.Clamp(height, 240f, 520f);
            }
        }

        // ------------------------------------------------------------------ entry point

        public static void Draw(AdapterRuntime runtime)
        {
            if (runtime == null)
            {
                GUILayout.Label(_language == null ? "MelonAdapter is not running." : T("common.notRunning"), _wrap ?? GUI.skin.label);
                return;
            }

            _language = runtime.Language;
            EnsureStyles();

            try
            {
                DrawTabs();
                GUILayout.Space(4);
                DrawMessage();

                switch (_page)
                {
                    case Page.Mods:
                        DrawMods(runtime);
                        break;
                    case Page.Preferences:
                        DrawPreferences(runtime);
                        break;
                    case Page.Settings:
                        DrawSettings(runtime);
                        break;
                    default:
                        DrawOverview(runtime);
                        break;
                }
            }
            catch (Exception exception)
            {
                GUILayout.Label(T("common.uiFailed", exception.GetType().Name + ": " + exception.Message), _wrap);
            }
        }

        private static void EnsureStyles()
        {
            if (_wrap == null)
            {
                _wrap = new GUIStyle(GUI.skin.label) { wordWrap = true };
            }

            if (_bold == null)
            {
                _bold = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, wordWrap = true };
            }
        }

        /// <summary>Localised text. The key itself is returned when no language provides it.</summary>
        private static string T(string key, params object[] args)
        {
            if (_language == null)
            {
                return key;
            }

            return _language.T(key, args);
        }

        private static string StateName(ModState state)
        {
            string key = "state." + state;
            string text = T(key);
            return string.Equals(text, key, StringComparison.Ordinal) ? state.ToString() : text;
        }

        private static void DrawTabs()
        {
            GUILayout.BeginHorizontal();
            Tab(T("tab.overview"), Page.Overview);
            Tab(T("tab.mods"), Page.Mods);
            Tab(T("tab.prefs"), Page.Preferences);
            Tab(T("tab.settings"), Page.Settings);
            GUILayout.EndHorizontal();
        }

        private static void Tab(string label, Page page)
        {
            bool active = _page == page;
            if (GUILayout.Toggle(active, label, GUI.skin.button, GUILayout.Width(110)) && !active)
            {
                _page = page;
                ResetScroll(page);
            }
        }

        private static void ResetScroll(Page page)
        {
            switch (page)
            {
                case Page.Mods:
                    _modsScroll = Vector2.zero;
                    _detailScroll = Vector2.zero;
                    break;
                case Page.Preferences:
                    _prefsScroll = Vector2.zero;
                    break;
                case Page.Settings:
                    _settingsScroll = Vector2.zero;
                    break;
                default:
                    _overviewScroll = Vector2.zero;
                    break;
            }
        }

        private static void DrawMessage()
        {
            if (_message == null || Time.realtimeSinceStartup - _messageAt > MessageSeconds)
            {
                return;
            }

            Color previous = GUI.color;
            GUI.color = _messageIsError ? new Color(1f, 0.5f, 0.4f) : new Color(0.6f, 1f, 0.6f);
            GUILayout.Label(_message, _wrap);
            GUI.color = previous;
        }

        // ------------------------------------------------------------------ overview

        private static void DrawOverview(AdapterRuntime runtime)
        {
            _overviewScroll = GUILayout.BeginScrollView(_overviewScroll, GUILayout.Height(PageHeight));
            GUILayout.Label(runtime.StatusSummary, _bold);

            Field(T("ov.adapter"), runtime.Environment.AdapterVersion + " (API baseline " + runtime.Config.ApiBaseline + ")");
            Field(T("ov.host"), runtime.Environment.HostName + " " + runtime.Environment.HostVersion);
            Field(T("ov.game"), runtime.Environment.GameName + " - " + runtime.Paths.GameDirectory);
            Field(T("ov.unity"), runtime.Environment.UnityVersion);
            Field(T("ov.harmony"), runtime.Harmony.ProviderVersion + " - " + runtime.Harmony.ProviderAssemblyLocation);
            Field(T("ov.preferences"), runtime.PreferencesOrigin ?? T("common.na"));
            Field(T("set.language"), runtime.Language.Language + " - " + runtime.Language.SourceName);

            GUILayout.Space(4);
            Field(T("ov.modsDir"), runtime.Paths.ModsDirectory);
            Field(T("ov.userData"), runtime.Paths.UserDataDirectory);
            Field(T("ov.config"), runtime.Paths.ConfigPath);
            Field(T("ov.report"), runtime.Paths.ReportPath);
            Field(T("ov.log"), runtime.Paths.LogPath);

            GUILayout.Space(6);
            GUILayout.Label(T("ov.states"), _bold);
            foreach (ModState state in new[] { ModState.Loaded, ModState.Disabled, ModState.Rejected, ModState.Failed, ModState.Discovered })
            {
                int count = runtime.LoadOrder.Count(m => m.State == state);
                if (count > 0)
                {
                    Field(StateName(state), count.ToString(CultureInfo.InvariantCulture));
                }
            }

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(T("ov.openAdapter"), GUILayout.Width(180)))
            {
                Open(runtime.Paths.AdapterDirectory);
            }

            if (GUILayout.Button(T("ov.openMods"), GUILayout.Width(160)))
            {
                Open(runtime.Paths.ModsDirectory);
            }

            if (GUILayout.Button(T("ov.openUserData"), GUILayout.Width(140)))
            {
                Open(runtime.Paths.UserDataDirectory);
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            GUILayout.Label(T("ov.logTail", 40), _bold);
            if (GUILayout.Button(T("ov.refresh"), GUILayout.Width(90)))
            {
                RefreshLog(runtime);
            }

            GUILayout.EndHorizontal();

            if (Time.realtimeSinceStartup - _logReadAt > 2f)
            {
                RefreshLog(runtime);
            }

            _logScroll = GUILayout.BeginScrollView(_logScroll, GUILayout.Height(160));
            GUILayout.Label(_logText ?? T("ov.logPending"), _wrap);
            GUILayout.EndScrollView();
            GUILayout.EndScrollView();
        }

        private static void RefreshLog(AdapterRuntime runtime)
        {
            _logReadAt = Time.realtimeSinceStartup;
            try
            {
                _logText = ReadTail(runtime.Paths.LogPath, 40);
            }
            catch (Exception exception)
            {
                _logText = T("ov.logFailed", exception.Message);
            }
        }

        /// <summary>
        /// Reads the tail of a file the adapter is still writing to. The adapter log is opened with
        /// FileShare.ReadWrite, so reading it needs the same sharing mode; only the last 128 KiB are read
        /// so a large log never stalls a frame.
        /// </summary>
        private static string ReadTail(string path, int maxLines)
        {
            if (!File.Exists(path))
            {
                return T("ov.logMissing");
            }

            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                const int window = 128 * 1024;
                long start = Math.Max(0, stream.Length - window);
                stream.Seek(start, SeekOrigin.Begin);
                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                {
                    string text = reader.ReadToEnd().Replace("\r\n", "\n");
                    string[] lines = text.Split('\n');
                    int first = start > 0 ? 1 : 0;
                    int available = lines.Length - first;
                    if (available <= 0)
                    {
                        return string.Empty;
                    }

                    int skip = Math.Max(first, lines.Length - maxLines);
                    return string.Join("\n", lines.Skip(skip).ToArray());
                }
            }
        }

        // ------------------------------------------------------------------ mods

        private static void DrawMods(AdapterRuntime runtime)
        {
            GUILayout.BeginHorizontal();

            _modsScroll = GUILayout.BeginScrollView(_modsScroll, GUILayout.Width(340), GUILayout.Height(PageHeight));
            foreach (ModRecord record in runtime.LoadOrder)
            {
                bool selected = string.Equals(_selectedModPath, record.AssemblyPath, StringComparison.OrdinalIgnoreCase);
                string label = Badge(record.State) + " " + record.DisplayName;
                bool clicked = GUILayout.Toggle(selected, label, GUI.skin.button);
                if (clicked && !selected)
                {
                    _selectedModPath = record.AssemblyPath;
                }
            }

            GUILayout.EndScrollView();
            GUILayout.Space(6);

            _detailScroll = GUILayout.BeginScrollView(_detailScroll, GUILayout.ExpandWidth(true), GUILayout.Height(PageHeight));
            ModRecord current = Selected(runtime);
            if (current == null)
            {
                GUILayout.Label(T("mods.select"), _wrap);
            }
            else
            {
                DrawModDetails(runtime, current);
            }

            GUILayout.EndScrollView();
            GUILayout.EndHorizontal();
        }

        private static ModRecord Selected(AdapterRuntime runtime)
        {
            if (string.IsNullOrEmpty(_selectedModPath))
            {
                return null;
            }

            return runtime.LoadOrder.FirstOrDefault(m => string.Equals(m.AssemblyPath, _selectedModPath, StringComparison.OrdinalIgnoreCase));
        }

        private static void DrawModDetails(AdapterRuntime runtime, ModRecord record)
        {
            GUILayout.Label(record.DisplayName + " " + record.VersionText, _bold);
            Field(T("mods.id"), record.ModId);
            Field(T("mods.author"), record.Author);
            Field(T("mods.state"), StateName(record.State) + (record.IsActive ? " " + T("mods.active") : string.Empty));
            if (!string.IsNullOrEmpty(record.FailureReason))
            {
                Field(T("mods.reason"), record.FailureReason);
            }

            Field(T("mods.assembly"), record.AssemblyPath);
            Field(T("mods.folder"), record.DirectoryPath);
            Field(T("mods.priority"), record.Priority.ToString(CultureInfo.InvariantCulture));
            Field(T("mods.hash"), record.Hash);
            if (record.Dependencies.Length > 0)
            {
                Field(T("mods.dependencies"), string.Join(", ", record.Dependencies));
            }

            if (record.OptionalDependencies.Length > 0)
            {
                Field(T("mods.optional"), string.Join(", ", record.OptionalDependencies));
            }

            Field(T("mods.melons"), record.Melons.Count == 0 ? T("common.none") : string.Join(", ", record.Melons.Select(m => m.GetType().FullName).ToArray()));

            if (record.Notes.Count > 0)
            {
                GUILayout.Label(T("mods.notes"), _bold);
                foreach (string note in record.Notes)
                {
                    Label(note);
                }
            }

            if (record.Warnings.Count > 0)
            {
                GUILayout.Label(T("mods.warnings"), _bold);
                foreach (string warning in record.Warnings)
                {
                    Label(warning);
                }
            }

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(T("mods.openFolder"), GUILayout.Width(130)))
            {
                Open(record.DirectoryPath);
            }

            if (GUILayout.Button(T("mods.openData"), GUILayout.Width(150)))
            {
                string directory = runtime.Paths.GetModUserDataDirectory(record.ModId);
                if (Directory.Exists(directory))
                {
                    Open(directory);
                }
                else
                {
                    Notify(T("mods.noUserData", directory), true);
                }
            }

            if (GUILayout.Button(T("mods.openInPrefs"), GUILayout.Width(170)))
            {
                _prefsFilter = record.Assembly;
                _page = Page.Preferences;
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            bool disabledNextLaunch = runtime.Config.DisabledMods.Any(id => string.Equals(id, record.ModId, StringComparison.OrdinalIgnoreCase));

            if (record.State == ModState.Loaded)
            {
                if (GUILayout.Button(T("mods.disableNow"), GUILayout.Width(130)))
                {
                    if (runtime.DisableMod(record.ModId, "Disabled manually from the adapter settings UI."))
                    {
                        Notify(T("mods.disabled", record.DisplayName), false);
                    }
                    else
                    {
                        Notify(T("mods.disableFailed"), true);
                    }
                }
            }

            if (!disabledNextLaunch)
            {
                if (GUILayout.Button(T("mods.skipNext"), GUILayout.Width(150)))
                {
                    runtime.Config.DisabledMods.Add(record.ModId);
                    runtime.Config.Save(runtime.Paths.ConfigPath);
                    Notify(T("mods.skipAdded", record.DisplayName), false);
                }
            }
            else
            {
                if (GUILayout.Button(T("mods.loadNext"), GUILayout.Width(150)))
                {
                    runtime.Config.DisabledMods.RemoveAll(id => string.Equals(id, record.ModId, StringComparison.OrdinalIgnoreCase));
                    runtime.Config.Save(runtime.Paths.ConfigPath);
                    Notify(T("mods.skipRemoved", record.DisplayName), false);
                }
            }

            if (GUILayout.Button(T("mods.writeDetails"), GUILayout.Width(170)))
            {
                runtime.Log.Write("ui", AdapterLogLevel.Info, "Mod details for '" + record.DisplayName + "': state=" + record.State + ", id=" + record.ModId + ", assembly=" + record.AssemblyPath + ", reason=" + (record.FailureReason ?? "none"));
                Notify(T("mods.detailsWritten"), false);
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            if (GUILayout.Button(T("mods.writeReport"), GUILayout.Width(220)))
            {
                runtime.WriteReport();
                Notify(T("mods.reportWritten", runtime.Paths.ReportPath), false);
            }
        }

        private static string Badge(ModState state)
        {
            switch (state)
            {
                case ModState.Loaded:
                    return "[OK]";
                case ModState.Disabled:
                    return "[off]";
                case ModState.Rejected:
                    return "[rej]";
                case ModState.Failed:
                    return "[err]";
                default:
                    return "[?]";
            }
        }

        // ------------------------------------------------------------------ preferences

        private static void DrawPreferences(AdapterRuntime runtime)
        {
            _prefsScroll = GUILayout.BeginScrollView(_prefsScroll, GUILayout.Height(PageHeight));

            GUILayout.Label(_prefsFilter == null ? T("pref.titleAll") : T("pref.title", _prefsFilter.GetName().Name), _bold);
            Label(T("pref.hint"));

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(T("pref.save"), GUILayout.Width(90)))
            {
                try
                {
                    MelonPreferences.Save();
                    _prefsDirty = false;
                    Notify(T("pref.saved", MelonPreferences.FilePath ?? T("common.na")), false);
                }
                catch (Exception exception)
                {
                    Notify(T("pref.saveFailed", exception.Message), true);
                }
            }

            if (GUILayout.Button(T("pref.reload"), GUILayout.Width(140)))
            {
                try
                {
                    MelonPreferences.Load();
                    Buffers.Clear();
                    BufferErrors.Clear();
                    _prefsDirty = false;
                    Notify(T("pref.reloaded"), false);
                }
                catch (Exception exception)
                {
                    Notify(T("pref.reloadFailed", exception.Message), true);
                }
            }

            if (_prefsFilter != null && GUILayout.Button(T("pref.showAll"), GUILayout.Width(170)))
            {
                _prefsFilter = null;
            }

            GUILayout.EndHorizontal();

            if (_prefsDirty)
            {
                Label(T("pref.dirty"));
            }

            List<MelonPreferences_Category> categories = MelonPreferences.Categories
                .Where(c => _prefsFilter == null || c.CreatorAssembly == _prefsFilter)
                .ToList();

            if (categories.Count == 0)
            {
                GUILayout.Space(6);
                Label(_prefsFilter == null ? T("pref.none") : T("pref.noneForMod"));
            }
            else
            {
                foreach (MelonPreferences_Category category in categories)
                {
                    GUILayout.Space(8);
                    string creator = category.CreatorAssembly == null ? T("pref.unknownOwner") : category.CreatorAssembly.GetName().Name;
                    GUILayout.Label(category.DisplayName + " (" + category.Identifier + ") - " + creator, _bold);

                    if (category.Entries.Count == 0)
                    {
                        Label(T("pref.noEntries"));
                        continue;
                    }

                    foreach (MelonPreferences_Entry entry in category.Entries)
                    {
                        DrawEntryRow(entry);
                    }
                }
            }

            GUILayout.EndScrollView();
        }

        private static void DrawEntryRow(MelonPreferences_Entry entry)
        {
            string key = EntryKey(entry);
            Type type = entry.GetReflectedType();
            object value = entry.BoxedValue;

            GUILayout.BeginHorizontal();
            GUILayout.Label((string.IsNullOrEmpty(entry.DisplayName) ? entry.Identifier : entry.DisplayName) + (entry.IsHidden ? " " + T("pref.hidden") : string.Empty), GUILayout.Width(240));

            if (type == typeof(bool))
            {
                bool current = value is bool && (bool)value;
                bool edited = GUILayout.Toggle(current, current ? T("pref.on") : T("pref.off"), GUI.skin.button, GUILayout.Width(70));
                if (edited != current)
                {
                    SetEntryValue(entry, edited);
                }
            }
            else if (type == typeof(string) || IsNumeric(type))
            {
                string buffer;
                if (!Buffers.TryGetValue(key, out buffer))
                {
                    buffer = entry.GetValueAsString() ?? string.Empty;
                    Buffers[key] = buffer;
                }

                string edited = GUILayout.TextField(buffer, GUILayout.Width(170));
                if (!string.Equals(edited, buffer, StringComparison.Ordinal))
                {
                    Buffers[key] = edited;
                }

                if (GUILayout.Button(T("pref.apply"), GUILayout.Width(60)))
                {
                    ApplyBuffer(entry, key, type);
                }
            }
            else if (type.IsEnum)
            {
                if (GUILayout.Button(value == null ? T("common.na") : value.ToString(), GUILayout.Width(140)))
                {
                    Array values = Enum.GetValues(type);
                    int index = value == null ? -1 : Array.IndexOf(values, value);
                    object next = values.GetValue((index + 1) % values.Length);
                    SetEntryValue(entry, next);
                }
            }
            else
            {
                GUILayout.Label(entry.GetValueAsString() ?? T("common.na"), _wrap);
            }

            if (GUILayout.Button(T("pref.resetButton"), GUILayout.Width(60)))
            {
                entry.ResetToDefault();
                Buffers.Remove(key);
                BufferErrors.Remove(key);
                _prefsDirty = true;
                Notify(T("pref.resetDone", entry.Identifier), false);
            }

            GUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(entry.Description))
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(24);
                GUILayout.Label(entry.Description, _wrap);
                GUILayout.EndHorizontal();
            }

            string error;
            if (BufferErrors.TryGetValue(key, out error))
            {
                Color previous = GUI.color;
                GUI.color = new Color(1f, 0.5f, 0.4f);
                GUILayout.Label(error, _wrap);
                GUI.color = previous;
            }
        }

        private static string EntryKey(MelonPreferences_Entry entry)
        {
            string category = entry.Category == null ? string.Empty : entry.Category.Identifier;
            return category + "\u0001" + entry.Identifier;
        }

        private static bool IsNumeric(Type type)
        {
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Int64:
                case TypeCode.UInt64:
                case TypeCode.Single:
                case TypeCode.Double:
                case TypeCode.Decimal:
                    return true;
                default:
                    return false;
            }
        }

        private static void ApplyBuffer(MelonPreferences_Entry entry, string key, Type type)
        {
            string text = Buffers[key];
            object converted;
            try
            {
                converted = Convert.ChangeType(text, type, CultureInfo.InvariantCulture);
            }
            catch (Exception exception)
            {
                BufferErrors[key] = T("pref.parseFailed", text, type.Name, exception.Message);
                return;
            }

            BufferErrors.Remove(key);
            SetEntryValue(entry, converted);
            Buffers[key] = entry.GetValueAsString() ?? string.Empty;
        }

        private static void SetEntryValue(MelonPreferences_Entry entry, object value)
        {
            try
            {
                entry.BoxedValue = value;
                _prefsDirty = true;
                Notify(T("pref.changed", entry.Identifier, entry.GetValueAsString()), false);
            }
            catch (Exception exception)
            {
                Notify(T("pref.changeFailed", entry.Identifier, exception.Message), true);
            }
        }

        // ------------------------------------------------------------------ settings

        private static void DrawSettings(AdapterRuntime runtime)
        {
            _settingsScroll = GUILayout.BeginScrollView(_settingsScroll, GUILayout.Height(PageHeight));

            GUILayout.Label(T("set.runtime"), _bold);

            bool debug = GUILayout.Toggle(runtime.Log.DebugEnabled, " " + T("set.debug"));
            if (debug != runtime.Log.DebugEnabled)
            {
                runtime.Log.DebugEnabled = debug;
                runtime.Environment.DebugLogging = debug;
                runtime.Config.DebugLogging = debug;
                Notify(debug ? T("set.debugOn") : T("set.debugOff"), false);
            }

            bool enabled = GUILayout.Toggle(runtime.IsEnabled, " " + T("set.enabled"));
            if (enabled != runtime.IsEnabled)
            {
                if (!enabled)
                {
                    runtime.SetEnabled(false);
                    runtime.Config.Enabled = false;
                    Notify(T("set.disabledNotice"), false);
                }
                else
                {
                    Notify(T("set.cannotEnable"), true);
                }
            }

            if (GUILayout.Button(T("set.reportNow"), GUILayout.Width(260)))
            {
                runtime.WriteReport();
                Notify(T("mods.reportWritten", runtime.Paths.ReportPath), false);
            }

            GUILayout.Space(8);
            GUILayout.Label(T("set.language"), _bold);
            Label(T("set.languageHint"));

            List<string> languages = runtime.Language.Languages.ToList();
            if (languages.Count == 0)
            {
                languages.Add(LocalizationCatalog.FallbackLanguage);
            }

            int activeIndex = languages.FindIndex(l => string.Equals(l, runtime.Language.Language, StringComparison.OrdinalIgnoreCase));
            if (activeIndex < 0)
            {
                activeIndex = 0;
            }

            int selectedLanguage = GUILayout.SelectionGrid(
                activeIndex,
                languages.Select(runtime.Language.DisplayName).ToArray(),
                languages.Count,
                GUILayout.Width(260));
            if (selectedLanguage != activeIndex)
            {
                SwitchLanguage(runtime, languages[selectedLanguage]);
            }

            GUILayout.Label(T("set.languageFile", runtime.Language.SourceName), _wrap);
            if (!string.IsNullOrEmpty(runtime.LanguageError))
            {
                Label(runtime.LanguageError);
            }

            if (runtime.Language.MissingKeyCount > 0)
            {
                Label(T("set.languageMissing", runtime.Language.MissingKeyCount));
            }

            if (GUILayout.Button(T("set.reloadLanguage"), GUILayout.Width(200)))
            {
                string error;
                bool reloaded = runtime.ReloadLanguage(out error);
                _language = runtime.Language;
                if (reloaded)
                {
                    Notify(T("set.languageReloaded"), false);
                }
                else
                {
                    Notify(error, true);
                }
            }

            GUILayout.Space(8);
            GUILayout.Label(T("set.configuration"), _bold);
            Label(T("set.configHint"));

            GUILayout.BeginHorizontal();
            GUILayout.Label(T("set.harmonyPolicy"), GUILayout.Width(200));
            string[] policies = { T("set.policyRefuse"), T("set.policyDisable"), T("set.policyReport") };
            int selected = GUILayout.SelectionGrid((int)runtime.Config.HarmonyPolicy, policies, 3, GUILayout.Width(260));
            if (selected != (int)runtime.Config.HarmonyPolicy)
            {
                runtime.Config.HarmonyPolicy = (HarmonyConflictPolicy)selected;
                Notify(T("set.policyChanged"), false);
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label(T("set.maxExceptions"), GUILayout.Width(200));
            if (_maxExceptionsBuffer == null)
            {
                _maxExceptionsBuffer = runtime.Config.MaxConsecutiveExceptions.ToString(CultureInfo.InvariantCulture);
            }

            _maxExceptionsBuffer = GUILayout.TextField(_maxExceptionsBuffer, GUILayout.Width(70));
            if (GUILayout.Button(T("pref.apply"), GUILayout.Width(60)))
            {
                int parsed;
                if (int.TryParse(_maxExceptionsBuffer, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) && parsed > 0)
                {
                    runtime.Config.MaxConsecutiveExceptions = parsed;
                    Notify(T("set.maxExceptionsApplied", parsed), false);
                }
                else
                {
                    Notify(T("set.maxExceptionsInvalid"), true);
                }
            }

            GUILayout.EndHorizontal();

            bool diagnostics = GUILayout.Toggle(runtime.Config.ResolverDiagnostics, " " + T("set.diagnostics"));
            if (diagnostics != runtime.Config.ResolverDiagnostics)
            {
                runtime.Config.ResolverDiagnostics = diagnostics;
            }

            bool allowUnannotated = GUILayout.Toggle(runtime.Config.AllowUnannotatedMods, " " + T("set.allowUnannotated"));
            if (allowUnannotated != runtime.Config.AllowUnannotatedMods)
            {
                runtime.Config.AllowUnannotatedMods = allowUnannotated;
            }

            bool migrate = GUILayout.Toggle(runtime.Config.MigrateFromMelonLoader, " " + T("set.migrate"));
            if (migrate != runtime.Config.MigrateFromMelonLoader)
            {
                runtime.Config.MigrateFromMelonLoader = migrate;
            }

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(T("set.saveConfig"), GUILayout.Width(180)))
            {
                try
                {
                    runtime.Config.Save(runtime.Paths.ConfigPath);
                    Notify(T("set.saved", runtime.Paths.ConfigPath), false);
                }
                catch (Exception exception)
                {
                    Notify(T("set.saveFailed", exception.Message), true);
                }
            }

            if (GUILayout.Button(T("set.openConfig"), GUILayout.Width(180)))
            {
                Open(runtime.Paths.ConfigPath);
            }

            if (GUILayout.Button(T("set.openLog"), GUILayout.Width(120)))
            {
                Open(runtime.Paths.LogPath);
            }

            GUILayout.EndHorizontal();

            GUILayout.EndScrollView();
        }

        /// <summary>
        /// Applies a new interface language immediately and remembers it in Config.toml, so the choice
        /// survives a restart even when the configuration says <c>auto</c>.
        /// </summary>
        private static void SwitchLanguage(AdapterRuntime runtime, string language)
        {
            if (!runtime.Language.SetLanguage(language))
            {
                Notify(T("set.languageMissing", language), true);
                return;
            }

            _language = runtime.Language;
            runtime.Config.Language = language;
            try
            {
                runtime.Config.Save(runtime.Paths.ConfigPath);
                Notify(T("set.languageChanged"), false);
            }
            catch (Exception exception)
            {
                Notify(T("set.saveFailed", exception.Message), true);
            }
        }

        // ------------------------------------------------------------------ helpers

        private static void Field(string name, string value)
        {
            GUILayout.Label(name + ": " + (value ?? T("common.na")), _wrap);
        }

        private static void Label(string text)
        {
            GUILayout.Label(text ?? string.Empty, _wrap);
        }

        private static void Notify(string message, bool isError)
        {
            _message = message;
            _messageIsError = isError;
            _messageAt = Time.realtimeSinceStartup;
        }

        private static void Open(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path))
                {
                    Notify(T("err.nothingToOpen"), true);
                    return;
                }

                if (!Directory.Exists(path) && !File.Exists(path))
                {
                    Notify(T("err.pathMissing", path), true);
                    return;
                }

                if (System.Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    if (File.Exists(path))
                    {
                        System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + path + "\"");
                    }
                    else
                    {
                        System.Diagnostics.Process.Start("explorer.exe", "\"" + path + "\"");
                    }
                }
                else
                {
                    string opener = System.Environment.OSVersion.Platform == PlatformID.MacOSX ? "open" : "xdg-open";
                    System.Diagnostics.Process.Start(opener, "\"" + path + "\"");
                }
            }
            catch (Exception exception)
            {
                Notify(T("err.openFailed", path, exception.Message), true);
            }
        }
    }
}
