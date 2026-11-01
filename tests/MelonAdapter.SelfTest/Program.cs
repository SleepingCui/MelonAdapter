using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MelonAdapter.Runtime;
using MelonLoader;
using MelonAdapter.Runtime.Mods;
using MelonAdapter.Runtime.Localization;
using MelonAdapter.Runtime.Preferences;

namespace MelonAdapter.SelfTest
{
    /// <summary>
    /// Headless run of the phase 8 validation matrix. No Unity and no UnityModManager are needed:
    /// the runtime is driven directly through its tick and scene entry points.
    /// </summary>
    internal static class Program
    {
        private static readonly List<string> Failures = new List<string>();
        private static int _checks;

        private static int Main(string[] args)
        {
            string workspace = args.Length > 0
                ? Path.GetFullPath(args[0])
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "selftest-workspace");

            Console.WriteLine("MelonAdapter self test");
            Console.WriteLine("Workspace: " + workspace);
            Console.WriteLine();

            try
            {
                if (Directory.Exists(workspace))
                {
                    Directory.Delete(workspace, true);
                }

                Directory.CreateDirectory(workspace);
                Run(workspace);
            }
            catch (Exception exception)
            {
                Check("unexpected exception", false, exception.ToString());
            }

            Console.WriteLine();

            if (Failures.Count == 0)
            {
                Console.WriteLine("PASS - " + _checks + " checks");
                return 0;
            }

            Console.WriteLine("FAIL - " + Failures.Count + " of " + _checks + " checks failed");
            foreach (string failure in Failures)
            {
                Console.WriteLine("  * " + failure);
            }

            return 1;
        }

        private static void Run(string workspace)
        {
            string gameDirectory = Path.Combine(workspace, "game");
            string adapterDirectory = Path.Combine(workspace, "adapter");
            Directory.CreateDirectory(gameDirectory);
            Directory.CreateDirectory(adapterDirectory);

            // The adapter reads <adapter>/lang.json next to Config.toml. Staging a small file here covers
            // the external-file path and proves the file is merged over the copy embedded in the runtime.
            string languagePath = Path.Combine(adapterDirectory, LocalizationCatalog.FileName);
            File.WriteAllText(languagePath, "{\n  \"languages\": [\"zh\", \"en\"],\n  \"zh\": { \"custom.runtime\": \"运行时\" },\n  \"en\": { \"custom.runtime\": \"runtime\" }\n}\n");

            Section("Configuration");
            string configPath = Path.Combine(adapterDirectory, "Config.toml");
            bool createdDefault;
            AdapterConfig config = AdapterConfig.Load(configPath, out createdDefault);
            Check("default configuration written on first start", createdDefault && File.Exists(configPath), configPath);

            string configText = ReadTextShared(configPath);
            Check("configuration contains the API baseline section", configText.Contains("[api]") && configText.Contains("baseline"), Snippet(configText));
            Check("configuration contains the Harmony policy", configText.Contains("[harmony]") && configText.Contains("conflictPolicy"), Snippet(configText));
            Check("documented defaults are applied", config.Enabled && !config.DebugLogging && config.MaxConsecutiveExceptions == 5, "enabled=" + config.Enabled + ", maxExceptions=" + config.MaxConsecutiveExceptions);
            Check("configuration contains the interface language section", configText.Contains("[ui]") && configText.Contains("language = \"auto\""), Snippet(configText));
            Check("the interface follows the system language by default", string.Equals(config.Language, LocalizationCatalog.AutomaticLanguage, StringComparison.OrdinalIgnoreCase), "language=" + config.Language);

            Section("Corrupt configuration");
            string corruptDirectory = Path.Combine(workspace, "corrupt");
            Directory.CreateDirectory(corruptDirectory);
            string corruptPath = Path.Combine(corruptDirectory, "Config.toml");
            File.WriteAllText(corruptPath, "enabled = true\n[api\nbaseline = \"0.7.3.0\"\n");

            bool corruptCreatedDefault;
            AdapterConfig corruptConfig = AdapterConfig.Load(corruptPath, out corruptCreatedDefault);
            Check("corrupt configuration falls back to defaults", corruptCreatedDefault && corruptConfig.Enabled, "warnings=" + corruptConfig.Warnings.Count);
            Check("corrupt configuration is preserved", Directory.GetFiles(corruptDirectory, "Config.toml.invalid-*").Length == 1, string.Join("; ", Directory.GetFiles(corruptDirectory)));

            Section("MelonLoader preference migration");
            string legacyUserData = Path.Combine(gameDirectory, "UserData");
            Directory.CreateDirectory(legacyUserData);
            string legacyPreferences = Path.Combine(legacyUserData, PreferencesStorage.LegacyFileName);
            File.WriteAllText(legacyPreferences, "[LegacyCategory]\nLegacyValue = \"from melonloader\"\n");

            // "auto" would follow the machine locale; the language assertions below need a fixed choice.
            config.Language = "zh";

            Section("Runtime start-up");
            AdapterRuntime runtime = new AdapterRuntime(gameDirectory, adapterDirectory, config);
            Check("adapter directories created", Directory.Exists(runtime.Paths.ModsDirectory) && Directory.Exists(runtime.Paths.UserDataDirectory), runtime.Paths.AdapterDirectory);
            Check(
                "MelonLoader preferences imported once with a backup",
                File.Exists(PreferencesStorage.GetDefaultPath(runtime.Paths))
                    && Directory.GetFiles(runtime.Paths.UserDataDirectory, "MelonPreferences.melonloader-*.bak").Length == 1
                    && File.Exists(legacyPreferences),
                runtime.PreferencesOrigin);
            Check(
                "the external lang.json is loaded from the adapter folder",
                runtime.Language != null
                    && string.Equals(runtime.Language.SourcePath, languagePath, StringComparison.OrdinalIgnoreCase)
                    && runtime.Language.Language == "zh",
                runtime.Language == null ? "(no catalog)" : "source=" + runtime.Language.SourcePath + ", language=" + runtime.Language.Language);
            Check(
                "lang.json extends the built-in catalog instead of replacing it",
                runtime.Language.T("custom.runtime") == "运行时" && runtime.Language.T("tab.mods") == "模组",
                "custom=" + runtime.Language.T("custom.runtime") + ", tab=" + runtime.Language.T("tab.mods"));
            runtime.Environment.HostName = "SelfTest";
            runtime.Environment.HostVersion = "1.0";

            StageMods(runtime);

            string[] staged = Directory.GetFiles(runtime.Paths.ModsDirectory, "*.dll", SearchOption.AllDirectories);
            Check("sample assemblies staged", staged.Length == 5, string.Join("; ", staged));

            runtime.Initialize();
            Check("scanner returned records", runtime.Mods.Count > 0, "Mods=" + runtime.Mods.Count);

            Check("runtime initialised", runtime.IsInitialized);
            Check("application callbacks are delayed to the first frame", !runtime.IsApplicationStarted, "IsApplicationStarted=" + runtime.IsApplicationStarted);

            ModRecord sample = FindRecord(runtime, "SampleMod");
            ModRecord missing = FindRecord(runtime, "SampleMod.MissingDependency");
            ModRecord throwing = FindRecord(runtime, "SampleMod.Throwing");
            ModRecord duplicated = null;
            int duplicateRejections = 0;

            foreach (ModRecord record in runtime.Mods)
            {
                if (record.AssemblySimpleName == "SampleMod" && record.AssemblyPath != null && record.AssemblyPath.IndexOf("duplicate", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    duplicated = record;
                }

                if (record.State == ModState.Rejected && Contains(record.FailureReason, "Duplicate mod id"))
                {
                    duplicateRejections++;
                }
            }

            Check("sample mod discovered", sample != null);
            Check("sample mod loaded", sample != null && sample.State == ModState.Loaded, sample == null ? null : sample.State.ToString());
            Check("sample mod has metadata", sample != null && sample.Melons.Count == 1 && sample.Melons[0].Info.Name == "Sample Mod", sample == null ? null : sample.DisplayName + " " + sample.VersionText);
            Check("missing dependency rejected", missing != null && missing.State == ModState.Rejected && Contains(missing.FailureReason, "ModThatIsNotInstalled"), missing == null ? null : missing.FailureReason);
            Check("duplicate mod id rejected", duplicated != null && duplicated.State == ModState.Rejected && duplicateRejections == 1, duplicated == null ? null : duplicated.FailureReason);
            Check("support library resolved as private dependency", sample != null && sample.Assembly.GetType("SampleMod.SampleModMain") != null && FindRecord(runtime, "SupportLib") == null, "SupportLib must not appear as a mod");
            Check("mods directory only loads the configured directory", !File.Exists(Path.Combine(gameDirectory, "Mods", "SampleMod.dll")), "stray copy in the game directory");
            Check(
                "per-mod user data directory created",
                Directory.Exists(Path.Combine(runtime.Paths.UserDataDirectory, "Sample Mod")),
                Path.Combine(runtime.Paths.UserDataDirectory, "Sample Mod"));

            string logText = ReadTextShared(runtime.Paths.LogPath);
            Check("startup banner reports the API baseline", logText.Contains("MelonLoader API baseline " + config.ApiBaseline), Snippet(logText));
            Check("startup banner reports the Harmony provider", logText.Contains("Harmony: "), Snippet(logText));
            Check("startup banner reports per-mod status", logText.Contains("Sample Mod"), Snippet(logText));
            Check("startup banner reports the host loader", logText.Contains("Host: SelfTest 1.0"), Snippet(logText));

            Section("Frames, scenes and callbacks");
            for (int frame = 0; frame < 10; frame++)
            {
                runtime.Tick(0.016f);
                runtime.FixedUpdate(0.02f);
                runtime.LateUpdate(0.016f);
            }

            runtime.OnGUI();
            runtime.OnSceneWasLoaded(0, "SampleScene");
            runtime.OnSceneWasUnloaded(0, "SampleScene");

            Check("application started after the first frame", runtime.IsApplicationStarted);
            Check("throwing mod disabled after repeated exceptions", throwing != null && throwing.State == ModState.Disabled && Contains(throwing.FailureReason, "consecutive"), throwing == null ? null : throwing.FailureReason);

            int patchedValue = InvokeSampleTarget(sample);
            Check("harmony patch applied", patchedValue == 42, "SampleTarget.Value()=" + patchedValue);

            string lifecycle = ReadLifecycleLog(runtime);
            Check("initialise callback ran", Contains(lifecycle, "OnInitializeMelon"));
            Check("application start callback ran", Contains(lifecycle, "OnApplicationStart"));
            Check("update callback ran", Contains(lifecycle, "OnUpdate"));
            Check("fixed update callback ran", Contains(lifecycle, "OnFixedUpdate"));
            Check("late update callback ran", Contains(lifecycle, "OnLateUpdate"));
            Check("gui callback ran", Contains(lifecycle, "OnGUI"));
            Check("scene callbacks ran", Contains(lifecycle, "OnSceneWasInitialized:0:SampleScene"), Snippet(lifecycle));
            Check("event subscription delivered", Contains(lifecycle, "event:OnSceneWasInitialized:SampleScene"), Snippet(lifecycle));
            Check("coroutine completed", Contains(lifecycle, "coroutine-done"), Snippet(lifecycle));
            Check("private dependency code executed", Contains(lifecycle, "support=support-lib-ok"), Snippet(lifecycle));

            Section("Preferences");
            runtime.SavePreferences();
            string preferencesPath = PreferencesStorage.GetDefaultPath(runtime.Paths);
            Check("preferences file written", File.Exists(preferencesPath), preferencesPath);
            string preferences = File.Exists(preferencesPath) ? ReadTextShared(preferencesPath) : string.Empty;
            Check("preferences contain the mod category", Contains(preferences, "SampleMod") && Contains(preferences, "Greeting"), Snippet(preferences));
            Check("migrated MelonLoader values survive a save", Contains(preferences, "LegacyCategory") && Contains(preferences, "from melonloader"), Snippet(preferences));

            // The in-game settings UI groups categories by the assembly that created them, so an
            // unattributed category would show up under the wrong owner.
            MelonPreferences_Category sampleCategory = MelonPreferences.GetCategory("SampleMod");
            Check(
                "preference categories know the assembly that created them",
                sampleCategory != null && sampleCategory.CreatorAssembly != null && sampleCategory.CreatorAssembly.GetName().Name == "SampleMod",
                sampleCategory == null ? "category missing" : "creator=" + (sampleCategory.CreatorAssembly == null ? "(none)" : sampleCategory.CreatorAssembly.GetName().Name));

            Section("API fidelity (MelonLoader 0.7.3 signatures)");

            // A mod compiles against exact signatures; a shape mismatch only surfaces at run time as
            // MissingMethodException. Jipper Key Viewer 1.7.1 failed on the eight-argument CreateEntry,
            // so these checks pin the shapes real mods bind against.
            MelonPreferences_Category fidelity = MelonPreferences.CreateCategory("Fidelity");
            fidelity.CreateEntry("OldName", 5, "Old", "old description", false, false, null);
            MelonPreferences_Entry<int> renamedEntry = fidelity.CreateEntry("NewName", 0, "New", "new description", false, false, null, "OldName");
            Check(
                "the eight-argument CreateEntry overload renames an existing entry",
                renamedEntry != null && renamedEntry.Identifier == "NewName" && renamedEntry.Value == 5 && !fidelity.HasEntry("OldName"),
                "entry=" + (renamedEntry == null ? "(null)" : renamedEntry.Identifier + "=" + renamedEntry.Value) + ", old=" + fidelity.HasEntry("OldName"));

            MelonPreferences_Entry<string> staticEntry = MelonPreferences.CreateEntry("FidelityStatic", "Greeting", "hello", "Greeting", "description", false, false, null);
            Check(
                "the static CreateEntry overload keeps the full signature",
                staticEntry != null && staticEntry.Category != null && staticEntry.Category.Identifier == "FidelityStatic" && staticEntry.Value == "hello",
                staticEntry == null ? "(null)" : staticEntry.Category.Identifier + "." + staticEntry.Identifier);

            string callback = null;
            Action<ConsoleColor, ConsoleColor, string, string> callbackHandler = (color, textColor, source, text) => callback = source + ":" + text;
            MelonLogger.MsgCallbackHandler += callbackHandler;
            new MelonLogger.Instance("Fidelity").Msg("fidelity-callback");
            MelonLogger.MsgCallbackHandler -= callbackHandler;
            Check("MelonLogger raises its message callback events", callback != null && Contains(callback, "fidelity-callback"), "callback=" + (callback ?? "(none)"));

            MelonInfoAttribute fidelityInfo = new MelonInfoAttribute(typeof(MelonMod), "Fidelity", "1.2.3", "tester");
            Check(
                "MelonInfoAttribute exposes a SemVersion",
                fidelityInfo.SemanticVersion != null && fidelityInfo.SemanticVersion.ToString() == "1.2.3",
                "version=" + (fidelityInfo.SemanticVersion == null ? "(null)" : fidelityInfo.SemanticVersion.ToString()));

            Dictionary<string, Dictionary<string, ModPrefs.PrefDesc>> legacyTable = ModPrefs.GetPrefs();
            Check(
                "ModPrefs.GetPrefs returns the legacy preference table",
                legacyTable.ContainsKey("SampleMod") && legacyTable["SampleMod"].ContainsKey("Greeting") && legacyTable["SampleMod"]["Greeting"].Type == ModPrefs.PrefType.STRING,
                "categories=" + legacyTable.Count + ", sample=" + (legacyTable.ContainsKey("SampleMod") ? legacyTable["SampleMod"].Count.ToString() : "missing"));

            Check(
                "MelonEnvironment lives in MelonLoader.Utils and reports the game root",
                string.Equals(MelonLoader.Utils.MelonEnvironment.GameRootDirectory, runtime.Environment.GameDirectory, StringComparison.OrdinalIgnoreCase),
                "env=" + MelonLoader.Utils.MelonEnvironment.GameRootDirectory + ", adapter=" + runtime.Environment.GameDirectory);
            Check("MelonBase.FindMelon accepts a name and an author", MelonBase.FindMelon("Definitely Not A Mod", "Nobody") == null, "unexpected match");

            Section("Language");
            string languageDirectory = Path.Combine(workspace, "language");
            Directory.CreateDirectory(languageDirectory);

            string customLanguageFile = Path.Combine(languageDirectory, "custom.json");
            File.WriteAllText(customLanguageFile, "{\n  \"languages\": [\"zh\", \"en\"],\n  \"zh\": { \"custom.key\": \"你好\", \"tab.overview\": \"总览\" },\n  \"en\": { \"custom.key\": \"hello\", \"tab.overview\": \"Overview\" }\n}\n");

            string catalogError;
            LocalizationCatalog catalog = LocalizationCatalog.Load(customLanguageFile, "zh", out catalogError);
            Check(
                "a custom lang.json overrides single keys and keeps the rest",
                catalogError == null && catalog.T("custom.key") == "你好" && catalog.T("tab.overview") == "总览" && catalog.T("tab.mods") == "模组",
                "error=" + (catalogError ?? "none") + ", values=" + catalog.T("custom.key") + "/" + catalog.T("tab.overview") + "/" + catalog.T("tab.mods"));
            Check("the catalog switches language at runtime", catalog.SetLanguage("en") && catalog.T("custom.key") == "hello" && catalog.T("tab.overview") == "Overview", "value=" + catalog.T("custom.key"));
            Check("language codes outside the catalog are rejected", !catalog.SetLanguage("fr") && catalog.Language == "en", "language=" + catalog.Language);

            LocalizationCatalog builtIn = LocalizationCatalog.Load(null, "zh", out catalogError);
            string chinese = builtIn.T("tab.prefs");
            builtIn.SetLanguage("en");
            string english = builtIn.T("tab.prefs");
            Check("the built-in catalog translates every key in both languages", builtIn.T("unknown.key") == "unknown.key" && builtIn.MissingKeyCount == 1, "missing=" + builtIn.MissingKeyCount);
            Check(
                "Chinese and English text differ for the same key",
                !string.IsNullOrEmpty(chinese) && !string.IsNullOrEmpty(english) && chinese != english && english == "Preferences",
                "zh=" + chinese + ", en=" + english);
            Check(
                "shipped translations complete each other",
                builtIn.KeysMissingFrom("zh").Count == 0 && builtIn.KeysMissingFrom("en").Count == 0,
                "zh-missing=" + builtIn.KeysMissingFrom("zh").Count + ", en-missing=" + builtIn.KeysMissingFrom("en").Count);

            string brokenLanguageFile = Path.Combine(languageDirectory, "broken.json");
            File.WriteAllText(brokenLanguageFile, "{\"zh\": {");
            LocalizationCatalog broken = LocalizationCatalog.Load(brokenLanguageFile, "en", out catalogError);
            Check(
                "a broken lang.json is reported and the built-in catalog is used",
                !string.IsNullOrEmpty(catalogError) && broken.T("tab.mods") == "Mods" && broken.SourcePath == null,
                "error=" + (catalogError ?? "none") + ", tab=" + broken.T("tab.mods"));

            string absentLanguageFile = Path.Combine(languageDirectory, "absent.json");
            LocalizationCatalog absent = LocalizationCatalog.Load(absentLanguageFile, "en", out catalogError);
            Check("a missing lang.json is not an error", catalogError == null && absent.T("tab.mods") == "Mods" && absent.SourcePath == null, "error=" + (catalogError ?? "none"));

            LocalizationCatalog automatic = LocalizationCatalog.Load(null, LocalizationCatalog.AutomaticLanguage, out catalogError);
            Check(
                "the automatic language is always a supported one",
                automatic.Language == "zh" || automatic.Language == "en",
                "language=" + automatic.Language + ", detected=" + LocalizationCatalog.DetectLanguage());

            Section("Compatibility report");
            Check("compatibility report written", File.Exists(runtime.Paths.ReportPath), runtime.Paths.ReportPath);
            string report = File.Exists(runtime.Paths.ReportPath) ? ReadTextShared(runtime.Paths.ReportPath) : string.Empty;
            Check("report lists the loaded mod", Contains(report, "SampleMod.dll"), Snippet(report));
            Check("report notes the API gaps", Contains(report, "gap"), Snippet(report));
            Check("report names the preference source", Contains(report, "imported from"), Snippet(report));

            Section("Manual mod control");
            Check("unknown mods cannot be disabled", !runtime.DisableMod("NoSuchMod", "self-test"), "DisableMod must reject unknown ids");
            Check(
                "mods that already stopped cannot be disabled again",
                throwing != null && !runtime.DisableMod(throwing.ModId, "self-test"),
                throwing == null ? "throwing mod missing" : "state=" + throwing.State);

            Section("Toggle off");
            runtime.SetEnabled(false);
            Check("runtime disabled", !runtime.IsEnabled);
            string disabledLog = ReadTextShared(runtime.Paths.LogPath);
            Check("toggle off explains the restart requirement", Contains(disabledLog, "A game restart is required"), Snippet(disabledLog));
            runtime.Tick(0.016f);
            Check("callbacks stop while disabled", runtime.IsInitialized && !runtime.IsEnabled);

            Section("Shutdown");
            int updateCountBefore = CountOccurrences(ReadLifecycleLog(runtime), "OnUpdate");
            runtime.Dispose();
            string afterShutdown = ReadLifecycleLog(runtime);
            Check("quit callback ran", Contains(afterShutdown, "OnApplicationQuit"));
            Check("deinitialise callback ran", Contains(afterShutdown, "OnDeinitializeMelon"));
            Check("no callbacks after shutdown", CountOccurrences(afterShutdown, "OnUpdate") == updateCountBefore, "before=" + updateCountBefore + ", after=" + CountOccurrences(afterShutdown, "OnUpdate"));

            int unpatchedValue = InvokeSampleTarget(sample);
            Check("harmony patches removed on shutdown", unpatchedValue == 21, "SampleTarget.Value()=" + unpatchedValue);
        }

        private static void StageMods(AdapterRuntime runtime)
        {
            string modsDirectory = runtime.Paths.ModsDirectory;
            string source = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestMods");
            if (!Directory.Exists(source))
            {
                throw new DirectoryNotFoundException("Build output 'TestMods' not found at " + source + ". Build the sample projects first.");
            }

            // The adapter resolves shared mod dependencies from <MelonAdapter>/Dependencies.
            Directory.CreateDirectory(runtime.Paths.SupportDirectory);
            File.Copy(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "0Harmony.dll"), Path.Combine(runtime.Paths.SupportDirectory, "0Harmony.dll"), true);

            CopyMod(source, modsDirectory, "SampleMod.dll", null);
            CopyMod(source, modsDirectory, "SampleMod.MissingDependency.dll", null);
            CopyMod(source, modsDirectory, "SampleMod.Throwing.dll", null);
            CopyMod(source, modsDirectory, "SupportLib.dll", "Support");
            CopyMod(source, modsDirectory, "SampleMod.dll", "duplicate");

            string duplicatePath = Path.Combine(modsDirectory, "duplicate", "SampleMod.dll");
            if (File.Exists(duplicatePath))
            {
                File.Move(duplicatePath, Path.Combine(modsDirectory, "duplicate", "SampleMod.Copy.dll"));
            }
        }

        private static void CopyMod(string source, string modsDirectory, string fileName, string subdirectory)
        {
            string target = subdirectory == null ? modsDirectory : Path.Combine(modsDirectory, subdirectory);
            Directory.CreateDirectory(target);
            File.Copy(Path.Combine(source, fileName), Path.Combine(target, fileName), true);
        }

        // Several staged files can share one assembly name (the duplicate-copy scenario). Prefer the
        // record the loader actually accepted, then the shallowest path, so the assertions always talk
        // about the mod under test rather than its deliberately rejected copy.
        private static ModRecord FindRecord(AdapterRuntime runtime, string assemblySimpleName)
        {
            return runtime.Mods
                .Where(record => string.Equals(record.AssemblySimpleName, assemblySimpleName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(record => record.State == ModState.Loaded || record.State == ModState.Disabled ? 0 : 1)
                .ThenBy(record => record.Depth)
                .FirstOrDefault();
        }

        private static int InvokeSampleTarget(ModRecord sample)
        {
            if (sample == null || sample.Assembly == null || sample.State == ModState.Failed)
            {
                return -1;
            }

            Type type = sample.Assembly.GetType("SampleMod.SampleTarget", false);
            if (type == null)
            {
                return -1;
            }

            object instance = Activator.CreateInstance(type);
            MethodInfo method = type.GetMethod("Value");
            return method == null ? -1 : (int)method.Invoke(instance, null);
        }

        private static string ReadLifecycleLog(AdapterRuntime runtime)
        {
            string path = Path.Combine(runtime.Paths.UserDataDirectory, "SampleMod", "lifecycle.log");
            return File.Exists(path) ? ReadTextShared(path) : string.Empty;
        }

        /// <summary>
        /// The adapter keeps MelonAdapter.log open for writing for the whole session, so a plain
        /// File.ReadAllText fails with a sharing violation. Share read/write/delete instead.
        /// </summary>
        private static string ReadTextShared(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return string.Empty;
            }

            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (StreamReader reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
            catch (IOException exception)
            {
                return "<unreadable: " + exception.Message + ">";
            }
        }

        private static int CountOccurrences(string text, string value)
        {
            int count = 0;
            int index = text.IndexOf(value, StringComparison.Ordinal);
            while (index >= 0)
            {
                count++;
                index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal);
            }

            return count;
        }

        private static bool Contains(string text, string value)
        {
            return text != null && text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string Snippet(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "<empty>";
            }

            string singleLine = text.Replace("\r", " ").Replace("\n", " | ");
            return singleLine.Length <= 180 ? singleLine : singleLine.Substring(0, 180) + "...";
        }

        private static void Section(string name)
        {
            Console.WriteLine("[" + name + "]");
        }

        private static void Check(string name, bool condition, string detail = null)
        {
            _checks++;
            if (condition)
            {
                Console.WriteLine("  ok   " + name);
                return;
            }

            string message = detail == null ? name : name + " -> " + Snippet(detail);
            Failures.Add(message);
            Console.WriteLine("  FAIL " + message);
        }
    }
}
