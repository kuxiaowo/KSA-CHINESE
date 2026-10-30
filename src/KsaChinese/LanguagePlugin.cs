// Adapted from LiX-Works/KsaUiLanguages (MIT), b504774158cc03b582a0e75bfe94330cb841f11d.
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;
using Brutal.ImGuiApi;
using Brutal.Localization;
using HarmonyLib;
using KSA;
using StarMap.API;
using NativeStrings = Brutal.Localization.LStrings;

namespace KsaChinese;

public sealed class LanguagePack
{
    public Dictionary<string, string> Display { get; set; } = new();
    public Dictionary<string, string> Content { get; set; } = new();
    public Dictionary<string, string> History { get; set; } = new();
    public string Locale { get; set; } = "en-US";
    public string DisplayName { get; set; } = "English";
    public string FontFile { get; set; } = "KsaUiNotoSC.ttf";
    public Dictionary<string, string> Native { get; set; } = new();
    public Dictionary<string, string> Literals { get; set; } = new();
    public Dictionary<string, string> Ui { get; set; } = new();
    public Dictionary<string, string> Tooltips { get; set; } = new();
    public Dictionary<string, string> Editor { get; set; } = new();
    public Dictionary<string, string> Startup { get; set; } = new();
    public Dictionary<string, string> Parts { get; set; } = new();
    public Dictionary<string, string> Controls { get; set; } = new();
    public Dictionary<string, string> Hud { get; set; } = new();
    public Dictionary<string, string> Planning { get; set; } = new();
    public Dictionary<string, string> Utility { get; set; } = new();
    public Dictionary<string, string> Gauges { get; set; } = new();
    public Dictionary<string, string> Scoped { get; set; } = new();
}

[StarMapMod]
public sealed class LanguagePlugin
{
    private Harmony? _patches;
    public static bool SupportsGameVersion(string? version)
        => version is "2026.10.25.5707";

    [StarMapBeforeMain]
    public void BeforeMain()
    {
        var gameVersion = typeof(KSA.Program).Assembly.GetName().Version?.ToString();
        if (!SupportsGameVersion(gameVersion))
        {
            Runtime.Log($"Chinese mod disabled: supported build is 2026.10.25.5707; found {gameVersion}.");
            return;
        }
        try
        {
            Compatibility.Validate(Path.GetDirectoryName(typeof(KSA.Program).Assembly.Location)!, Path.Combine(Runtime.ModRoot,"compatibility.json"));
            Runtime.Initialize();
            _patches = new Harmony("local.ksa.chinese.5707");
            _patches.PatchAll(typeof(LanguagePlugin).Assembly);
            Runtime.Log($"Initialized UI language mod for game {gameVersion}.");
        }
        catch (Exception error)
        {
            _patches?.UnpatchAll("local.ksa.chinese.5707");
            _patches = null;
            Runtime.RestoreProvider();
            Runtime.Log("Prototype disabled after initialization error: " + error);
        }
    }

    [StarMapAllModsLoaded]
    public void AllModsLoaded()
    {
        if (_patches is null) return;
        Runtime.ApplyLanguage(Runtime.SelectedLocale);
        Runtime.Log("Mods loaded; UI language is ready.");
    }

    [StarMapUnload]
    public void Unload()
    {
        if (_patches is null) return;
        Runtime.RestoreProvider();
        _patches.UnpatchAll("local.ksa.chinese.5707");
    }
}

public static class Runtime
{
    public static readonly string ModRoot = Path.GetDirectoryName(typeof(Runtime).Assembly.Location)!;
    public static readonly Dictionary<string, LanguagePack> Packs = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<LStringId, string> Translations = new();
    private static LocalizedStrings.GetLocalizedStringDelegate? _previousProvider;
    private static readonly LocalizedStrings.GetLocalizedStringDelegate Provider = Provide;
    private static string[]? _originalNavLabels;
    internal static string? OriginalFontName;
    private static readonly Dictionary<string, byte[]> Utf8Cache = new(StringComparer.Ordinal);
    public static string SelectedLocale { get; private set; } = "zh-CN";
    public static bool InSettingsCombo { get; set; }
    [ThreadStatic] public static int EditorUiDepth;
    [ThreadStatic] public static int StartupUiDepth;
    public static LanguagePack CurrentPack { get; private set; } = new();
    public static string FontPath => Path.Combine(ModRoot, "Fonts", Path.GetFileName(CurrentPack.FontFile));
    public static string FontName => Path.GetFileNameWithoutExtension(FontPath);

    public static void Initialize()
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        foreach (var file in Directory.GetFiles(Path.Combine(ModRoot, "Locales"), "*.json"))
        {
            var pack = JsonSerializer.Deserialize<LanguagePack>(File.ReadAllText(file), options)
                ?? throw new InvalidDataException($"Empty language pack: {file}");
            Packs.Add(pack.Locale, pack);
        }
        var configPath = Path.Combine(ModRoot, "language.json");
        if (File.Exists(configPath))
        {
            using var config = JsonDocument.Parse(File.ReadAllText(configPath));
            SelectedLocale = config.RootElement.GetProperty("locale").GetString() ?? "zh-CN";
        }
        SelectedLocale = LocaleSettings.Read(SettingsPath, SelectedLocale, Packs.Keys, Log);
        if (!Packs.ContainsKey(SelectedLocale)) SelectedLocale = "en-US";
        _previousProvider = NativeStrings.LocalizedStringProvider;
        ApplyLanguage(SelectedLocale);
    }

    public static string SettingsPath => Path.Combine(KSA.Constants.DocumentsFolderPath, "KsaChinese", "settings.json");

    public static void SelectLanguage(string locale)
    {
        ApplyLanguage(locale);
        LocaleSettings.Write(SettingsPath, locale, Log);
    }

    public static void ApplyLanguage(string locale)
    {
        var pack = Packs[locale];
        var next = new Dictionary<LStringId, string>();
        foreach (var entry in pack.Native)
        {
            var field = typeof(KSA.LStrings).GetField(entry.Key, BindingFlags.Public | BindingFlags.Static);
            LString? original=field?.GetValue(null) as LString;
            if(original is null) NativeStrings.TryGet(LStringId.From(entry.Key),out original);
            if (original is null)
                throw new InvalidDataException($"Unknown native resource ID: {entry.Key}");
            if (!Tokens(original.Template).SequenceEqual(Tokens(entry.Value)))
                throw new InvalidDataException($"Placeholder mismatch: {entry.Key}");
            next.Add(original.Id, entry.Value);
        }
        foreach(var section in new[]{pack.Literals,pack.Ui,pack.Tooltips,pack.Editor,pack.Startup,pack.Controls,pack.Hud,pack.Planning,pack.Utility,pack.Gauges})
            foreach(var entry in section)
                if(!Tokens(entry.Key).SequenceEqual(Tokens(entry.Value)))
                    throw new InvalidDataException($"Translation placeholder mismatch: {entry.Key}");
        foreach (var entry in pack.Editor)
            if (!Tokens(entry.Key).SequenceEqual(Tokens(entry.Value)))
                throw new InvalidDataException($"Editor placeholder mismatch: {entry.Key}");
        foreach (var entry in pack.Hud)
            if (!Tokens(entry.Key).SequenceEqual(Tokens(entry.Value)))
                throw new InvalidDataException($"HUD placeholder mismatch: {entry.Key}");
        foreach (var section in new[] { pack.Planning, pack.Utility })
            foreach (var entry in section)
                if (!Tokens(entry.Key).SequenceEqual(Tokens(entry.Value)))
                    throw new InvalidDataException($"Window placeholder mismatch: {entry.Key}");
        CurrentPack = pack;
        SelectedLocale = locale;
        InvalidateFontCache();
        Translations.Clear();
        foreach (var entry in next) Translations.Add(entry.Key, entry.Value);
        NativeStrings.SetLocalizedStringProvider(Provider);
        var navField = AccessTools.Field(typeof(GameSettings), "_navRailLabels");
        if (navField?.GetValue(null) is string[] labels)
        {
            _originalNavLabels ??= (string[])labels.Clone();
            for (int i = 0; i < labels.Length; i++) labels[i] = Literal(_originalNavLabels[i]);
        }
        Log($"Language {locale}: {Translations.Count} native, {pack.Literals.Count} literals, {pack.Ui.Count} UI labels, {pack.Tooltips.Count} tooltips, {pack.Editor.Count} editor labels, {pack.Startup.Count} startup labels, {pack.Parts.Count} part names, {pack.Controls.Count} control captions, {pack.Hud.Count} HUD captions, {pack.Planning.Count} planning captions, {pack.Utility.Count} utility captions, {pack.Gauges.Count} gauges, {pack.Display.Count} extended display labels, {pack.Content.Count} content fields, {pack.History.Count} history paragraphs.");
    }

    private static IEnumerable<string> Tokens(string text) => Regex.Matches(text, @"\{[^{}]+\}")
        .Select(match => match.Value).Order(StringComparer.Ordinal);

    private static bool Provide(LString original, [NotNullWhen(true)] out string? result)
    {
        if (Translations.TryGetValue(original.Id, out result)) return true;
        if (_previousProvider is not null && _previousProvider(original, out result)) return true;
        result = original.Template;
        return true;
    }

    public static string Literal(string original) => CurrentPack.Literals.GetValueOrDefault(original, original);
    internal static void InvalidateFontCache()
    {
        AccessTools.Field(typeof(ConsoleStyle),"_gaugeInkRatioCached").SetValue(null,false);
        AccessTools.Field(typeof(ConsoleStyle),"_fontCacheFrame").SetValue(null,-1);
    }
    public static string DisplayRateText(string original) => ScopedText("Settings.VsyncFpsPreview",original);
    public static string ScopedText(string key,string original) => CurrentPack.Scoped.GetValueOrDefault(key,original);

    public static bool IsLiteralCandidate(string text) => Packs.Values.Any(pack => pack.Literals.ContainsKey(text));

    public static string UiText(string original) => CurrentPack.Ui.GetValueOrDefault(original, ExpandedUi.Text(original));

    public static string TooltipText(string original) => CurrentPack.Tooltips.GetValueOrDefault(original, ExpandedUi.Tooltip(original));

    public static string EditorText(string original) => CurrentPack.Editor.GetValueOrDefault(original, original);

    public static string StartupText(string original) => CurrentPack.Startup.GetValueOrDefault(original, original);

    public static string ControlText(string original) => CurrentPack.Controls.GetValueOrDefault(original, original);

    public static string PlanningText(string original) => CurrentPack.Planning.GetValueOrDefault(original, original);
    public static string UtilityText(string original) => CurrentPack.Utility.GetValueOrDefault(original, original);
    public static string GaugeText(string original) => string.IsNullOrEmpty(original) ? original : CurrentPack.Gauges.GetValueOrDefault(original, original);

    public static string PlanningMenuText(string original) => ScopedMenuText(original, pack => pack.Planning);
    public static string UtilityMenuText(string original) => ScopedMenuText(original, pack => pack.Utility);

    private static string ScopedMenuText(string original, Func<LanguagePack, Dictionary<string, string>> section)
    {
        int separator = original.IndexOf("##", StringComparison.Ordinal);
        string visible = separator < 0 ? original : original[..separator];
        if (!Packs.Values.Any(pack => section(pack).ContainsKey(visible))) return original;
        int stable = original.IndexOf("###", StringComparison.Ordinal);
        return section(CurrentPack).GetValueOrDefault(visible, visible) + (stable >= 0 ? original[stable..] : "###" + original);
    }

    public static string HudText(string original) => CurrentPack.Hud.GetValueOrDefault(original, UiText(original));

    public static string HudMenuText(string original)
    {
        int separator = original.IndexOf("##", StringComparison.Ordinal);
        string visible = separator < 0 ? original : original[..separator];
        if (!Packs.Values.Any(pack => pack.Hud.ContainsKey(visible) || pack.Ui.ContainsKey(visible))) return original;
        int stable = original.IndexOf("###", StringComparison.Ordinal);
        return HudText(visible) + (stable >= 0 ? original[stable..] : "###" + original);
    }

    public static string PartName(PartTemplate template)
        => CurrentPack.Parts.GetValueOrDefault(template.Id, template.DisplayName ?? template.Id);

    public static string PartCaption(Part part)
    {
        // Preserve an instance name supplied by the player when the game exposes it.
        if (part.DisplayName != part.Template.DisplayName && part.DisplayName != part.Template.Id) return part.DisplayName;
        return CurrentPack.Parts.GetValueOrDefault(part.Template.Id, part.DisplayName);
    }

    public static string StartupMenuText(string original)
    {
        int separator = original.IndexOf("##", StringComparison.Ordinal);
        string visible = separator < 0 ? original : original[..separator];
        if (!Packs.Values.Any(pack => pack.Startup.ContainsKey(visible))) return original;
        int stable = original.IndexOf("###", StringComparison.Ordinal);
        return StartupText(visible) + (stable >= 0 ? original[stable..] : "###" + original);
    }

    public static string EditorLiteral(string original)
    {
        string direct = EditorText(original);
        if (direct != original) return direct;
        string tooltip = TooltipText(original);
        if (tooltip != original) return tooltip;
        // Part cards split on an ASCII colon; retain all layout delimiters.
        int start = original.StartsWith("\n", StringComparison.Ordinal) ? 1 : 0;
        if (original.AsSpan(start).StartsWith("├─ ") || original.AsSpan(start).StartsWith("└─ ")) start += 3;
        if (original.EndsWith(": ", StringComparison.Ordinal))
        {
            string key = original[start..^2];
            string translated = EditorText(key);
            if (translated != key) return original[..start] + translated + ": ";
        }
        return original;
    }

    public static string EditorMenuText(string original)
    {
        int separator = original.IndexOf("##", StringComparison.Ordinal);
        string visible = separator < 0 ? original : original[..separator];
        if (!Packs.Values.Any(pack => pack.Editor.ContainsKey(visible))) return original;
        string translated = EditorText(visible);
        int stable = original.IndexOf("###", StringComparison.Ordinal);
        return translated + (stable >= 0 ? original[stable..] : "###" + original);
    }

    public static string EditorNativeText(string original, int kind)
    {
        if (kind == 1) return EditorText(original);
        if (kind != 2) return original;
        const string orbitPrefix = "Orbit around ";
        if (original.StartsWith(orbitPrefix, StringComparison.Ordinal))
            return EditorText("Orbit around {0}").Replace("{0}", EditorText(original[orbitPrefix.Length..]), StringComparison.Ordinal);
        int on = original.LastIndexOf(" On ", StringComparison.Ordinal);
        if (original.StartsWith("At ", StringComparison.Ordinal) && on > 3)
            return EditorText("At {0} On {1}").Replace("{0}", original[3..on], StringComparison.Ordinal)
                .Replace("{1}", EditorText(original[(on + 4)..]), StringComparison.Ordinal);
        return original == "None" ? EditorText(original) : original;
    }

    public static string EditorNativeSelectable(string original, int kind)
    {
        int stable = original.IndexOf("###", StringComparison.Ordinal);
        string visible = stable < 0 ? original : original[..stable];
        return EditorNativeText(visible, kind) + (stable >= 0 ? original[stable..] : "###" + original);
    }

    public static string MenuText(string original)
    {
        // ImGui ### keeps the widget ID identical across language changes.
        int separator = original.IndexOf("##", StringComparison.Ordinal);
        string visible = separator < 0 ? original : original[..separator];
        string translated = UiText(visible);
        // Normalize known labels in every locale: ### includes its own hash prefix,
        // so both English and translated captions need the same explicit ID.
        bool known = Packs.Values.Any(pack => pack.Ui.ContainsKey(visible));
        if (!known) return ExpandedUi.Menu(original);
        int stableSeparator = original.IndexOf("###", StringComparison.Ordinal);
        if (stableSeparator >= 0) return translated + original[stableSeparator..];
        if (separator >= 0) return translated + "###" + original;
        return translated + "###" + original;
    }

    public static ImString ImText(string text)
    {
        if (!Utf8Cache.TryGetValue(text, out var bytes))
        {
            bytes = Encoding.UTF8.GetBytes(text + '\0');
            Utf8Cache.Add(text, bytes);
        }
        return new ImString(bytes.AsSpan(0, bytes.Length - 1));
    }

    public static void RestoreProvider()
    {
        if (ReferenceEquals(NativeStrings.LocalizedStringProvider, Provider))
        {
            if (_previousProvider is null) NativeStrings.RemoveLocalizedStringProvider();
            else NativeStrings.SetLocalizedStringProvider(_previousProvider);
        }
        var navField = AccessTools.Field(typeof(GameSettings), "_navRailLabels");
        if (_originalNavLabels is not null && navField?.GetValue(null) is string[] labels)
            _originalNavLabels.CopyTo(labels, 0);
        InSettingsCombo = false;
        EditorUiDepth = 0;
        StartupUiDepth = 0;
        if(OriginalFontName is not null) GameSettings.Current.Interface.FontName=OriginalFontName;
        InvalidateFontCache();
    }

    public static void Log(string message)
    {
        Console.WriteLine($"[KsaChinese] {message}");
        var directory = Path.Combine(KSA.Constants.DocumentsFolderPath, "logs");
        Directory.CreateDirectory(directory);
        File.AppendAllText(Path.Combine(directory, "KsaChinese.log"), $"{DateTimeOffset.UtcNow:O} {message}{Environment.NewLine}");
    }
}

[HarmonyPatch(typeof(FontManager), nameof(FontManager.RegenerateFonts))]
public static class FontRegistrationPatch
{
    [HarmonyPrefix]
    public static void RegisterLocaleFont()
    {
        var field = AccessTools.Field(typeof(FontManager), "_fontFiles");
        if (field.GetValue(null) is not string[] fonts || !File.Exists(Runtime.FontPath))
            throw new InvalidOperationException("UI font registration is not ready.");
        Runtime.OriginalFontName ??= GameSettings.Current.Interface.FontName;
        // Put the selected font first so this build also creates its tooltip font from the right file.
        // Content fonts may already be found as relative paths by KSA. Font
        // atlas keys use basenames, so absolute-path equality is insufficient.
        var rest = WithoutLocaleFont(fonts, Runtime.FontName);
        field.SetValue(null, new[] { Runtime.FontPath }.Concat(rest).ToArray());
        if(!FontManager.FontFiles.Any(font=>font.GetKey()==Runtime.FontName)) FontManager.RegisterFont(Runtime.FontPath);
        GameSettings.Current.Interface.FontName = Runtime.FontName;
        Runtime.Log("Registered UI font before atlas generation: " + Runtime.FontName);
    }

    public static IEnumerable<string> WithoutLocaleFont(IEnumerable<string> files, string fontName)
        => files.Where(file => !string.Equals(Path.GetFileNameWithoutExtension(file), fontName, StringComparison.OrdinalIgnoreCase));
}

[HarmonyPatch]
public static class SettingsLiteralPatch
{
    public static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var name in new[] { "OnDrawUi", "DrawFooter", "BuildFooterStatusText" })
        {
            var method = AccessTools.Method(typeof(GameSettings), name);
            if (method is not null) yield return method;
        }
        yield return AccessTools.Method(typeof(EscMenu), nameof(EscMenu.Draw));
    }

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Translate(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        var lookup = AccessTools.Method(typeof(Runtime), nameof(Runtime.Literal));
        bool buttonContext = __originalMethod.DeclaringType == typeof(EscMenu) || __originalMethod.Name == "DrawFooter";
        var buttonLabels = new HashSet<string> { "APPLY", "RESET", "DISCARD", "RESET KEYS", "CLOSE", "RESUME", "NEW VEHICLE", "LAUNCH VEHICLE", "ABANDON VEHICLE", "RECOVER VEHICLE", "SAVE/LOAD", "EXIT EDITOR", "SETTINGS", "QUIT" };
        foreach (var instruction in instructions)
        {
            yield return instruction;
            if (__originalMethod.DeclaringType==typeof(GameSettings) && __originalMethod.Name=="OnDrawUi"
                && instruction.opcode==OpCodes.Ldstr && Equals(instruction.operand,"DISPLAY"))
            {
                yield return new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(Runtime),nameof(Runtime.DisplayRateText)));
                continue;
            }
            if (instruction.opcode == OpCodes.Ldstr && instruction.operand is string text && Runtime.IsLiteralCandidate(text)
                && !(buttonContext && buttonLabels.Contains(text)))
                yield return new CodeInstruction(OpCodes.Call, lookup);
        }
    }
}

[HarmonyPatch]
public static class MenuTextPatch
{
    private static readonly HashSet<string> StaticLabels = new(StringComparer.Ordinal)
    {
        "File", "Universe", "View", "Editor", "Settings", "Save/Load", "Build New Vehicle", "Launch Existing Vehicle",
        "Check for Update", "Version History", "Exit Game", "Vehicle Save/Load", "Crew Assignment", "Exit Editor",
        "Speed", "Camera", "Camera Mode", "Fixed", "Free", "Orbit", "Map", "Target", "Orbit Lines",
        "Show All Orbits", "Hide All Orbits", "Show Celestial Names", "Show Location Names", "Show Map Grid",
        "Frame Statistics", "Context Assignments", "Resource Groups", "Resources", "Draw Part Axes", "Show Delta-V Debug",
        "Vehicle", "Follow", "Control", "Set Target", "Clear Target", "Move Camera To", "Celestial Info",
        "HUD", "Auto Warp", "Vessels", "Bodies", "Manifest", "Kitten Roster", "Space Weather", "Show Space Weather", "Show Magnetosphere", "Next", "Previous",
        "Transfer Planner", "Flight Plan", "Ground Track", "Target Track", "Staging", "Threads", "Profiler",
        "Orbit Camera", "Free Camera", "Map Camera", "Add Camera", "Follow Terrain", "Show Orbit Markers",
        "Flight Plans", "Celestial Names", "Debug", "Show All", "Show None", "Comets", "Planets", "Moons",
        "Kitten Tuning", "Parachute Tuning", "Statistics", "Spherical Billboarding", "Wireframe", "Show Flare Debug",
        "Show Overall Bloom Debug", "Show Terrain Debug", "Show Texture Streaming", "Show Orbit Directions",
        "Show Burn Debug", "Show Part Contact Load", "Mesh Deformation Editor", "Show Exhaust Debug",
        "Show Plume Debug", "Show Engine Designer", "Show Atmosphere Editor", "Show Clouds Editor", "Show Ocean Editor",
        "Show Particle Emitter Editor", "Show Explosion Editor", "Show Distant Glint Editor",
        "Show Device Host Shared Memory", "Lighting", "Show Light Debug", "Show Planet Shadow Debug",
        "Show Cascaded Shadow Debug", "Gauge UI", "Debug Mode", "Inspector", "Hierarchy", "Reset",
        "Show Physics Debug", "Show Engine Debug", "Show Flight Computer Debug", "Show Set Orbit Debug",
        "Show Audio Debug", "Sprite Distance", "Editor Toggle", "Show PBR Spheres", "Show experimental particles",
        "Minor Bodies", "Asteroids", "Show Portrait Camera Controls"
    };
    private static readonly HashSet<string> CameraCategoryTitles = new(StringComparer.Ordinal)
    {
        "Minor Bodies", "Asteroids", "Comets"
    };
    private static readonly HashSet<string> HudTitles = new(StringComparer.Ordinal)
    {
        "Navball", "Telemetry", "Engine Control", "Autopilot Settings", "Resources", "Game Time", "Gametime",
        "Radiation", "Sequences", "Crew Portraits", "Altitude", "Attitude Indicators", "Rendezvous Control", "Burn Control", "Kitten Flight Control"
    };

    public static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(KSA.Program), "DrawMenuBar");
        yield return AccessTools.Method(typeof(ProfilerUi), nameof(ProfilerUi.DrawMenuItems));
        yield return AccessTools.Method(typeof(KSA.Program), "DrawMoveCameraToSubMenu");
        yield return AccessTools.Method(typeof(GaugeCanvas), nameof(GaugeCanvas.OnDrawMenuBar));
    }

    public static void LocalizeMenuLabel(ref ImString label)
    {
        string original = label.ToString();
        string translated = Runtime.MenuText(original);
        if (translated != original) label = Runtime.ImText(translated);
    }

    public static ImString LocalizeStaticLabel(ImString label)
    {
        if(label.ToString()=="Speed") return Runtime.ImText(Runtime.ScopedText("Menu.SimulationSpeed","Speed")+"###Speed");
        if (label.ToString() == "Hold alt to multiselect") return Runtime.ImText(Runtime.UiText(label.ToString()));
        if (StaticLabels.Contains(label.ToString())) LocalizeMenuLabel(ref label);
        return label;
    }

    public static ImString LocalizeHudTitle(ImString label)
    {
        string original = label.ToString();
        int separator = original.IndexOf("##", StringComparison.Ordinal);
        string visible = separator < 0 ? original : original[..separator];
        if (HudTitles.Contains(visible)) LocalizeMenuLabel(ref label);
        return label;
    }

    public static int FormatCameraCategoryWithCount(ReadOnlySpan<char> label, int count, Span<byte> buffer)
    {
        string original = label.ToString();
        if (!CameraCategoryTitles.Contains(original)) return FormatLabelWithCount(label, count, buffer);

        string menuText = Runtime.MenuText(original);
        int separator = menuText.IndexOf("###", StringComparison.Ordinal);
        if (separator < 0) return FormatLabelWithCount(label, count, buffer);

        string countText = count > 0 ? $" ({count})" : string.Empty;
        string visible = menuText[..separator] + countText;
        string stableId = menuText[(separator + 3)..] + countText;
        int length = Encoding.UTF8.GetBytes(visible + "###" + stableId, buffer);
        buffer[length] = 0;
        return length;
    }

    private static int FormatLabelWithCount(ReadOnlySpan<char> label, int count, Span<byte> buffer)
    {
        int length = Encoding.UTF8.GetBytes(label, buffer);
        if (count > 0)
        {
            buffer[length++] = (byte)' ';
            buffer[length++] = (byte)'(';
            count.TryFormat(buffer[length..], out int bytesWritten);
            length += bytesWritten;
            buffer[length++] = (byte)')';
        }
        buffer[length] = 0;
        return length;
    }

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> TranslateStaticMenus(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        bool hud = __originalMethod.DeclaringType == typeof(GaugeCanvas);
        var inputType = hud ? typeof(string) : typeof(ReadOnlySpan<byte>);
        var lookup = AccessTools.Method(typeof(MenuTextPatch), hud ? nameof(LocalizeHudTitle) : nameof(LocalizeStaticLabel));
        bool cameraCategories = __originalMethod.DeclaringType == typeof(KSA.Program)
            && __originalMethod.Name == "DrawMoveCameraToSubMenu";
        var formatCameraCategory = AccessTools.Method(typeof(MenuTextPatch), nameof(FormatCameraCategoryWithCount));
        foreach (var instruction in instructions)
        {
            if (cameraCategories && instruction.operand is MethodInfo formatMethod
                && formatMethod.DeclaringType == typeof(KSA.Program)
                && formatMethod.Name == "FormatLabelWithCount"
                && formatMethod.GetParameters().Length == 3
                && formatMethod.GetParameters()[0].ParameterType == typeof(ReadOnlySpan<char>)
                && formatMethod.GetParameters()[1].ParameterType == typeof(int)
                && formatMethod.GetParameters()[2].ParameterType == typeof(Span<byte>))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = formatCameraCategory;
            }
            yield return instruction;
            if (instruction.opcode == OpCodes.Call && instruction.operand is MethodInfo method
                && method.DeclaringType == typeof(ImString) && method.Name == "op_Implicit"
                && method.GetParameters()[0].ParameterType == inputType)
                yield return new CodeInstruction(OpCodes.Call, lookup);
        }
    }
}

[HarmonyPatch(typeof(ConsoleWidgets), "DrawButtonCore")]
public static class ButtonCaptionPatch
{
    [HarmonyPrefix]
    public static void LocalizeCaption(ref ReadOnlySpan<char> label)
    {
        string original = label.ToString();
        string translated = Runtime.Literal(original);
        if (translated == original) translated = ExpandedUi.Text(original);
        if (translated != original) label = translated.AsSpan();
    }
}

[HarmonyPatch(typeof(ConsoleWidgets), nameof(ConsoleWidgets.ButtonWidth))]
public static class ButtonWidthPatch
{
    [HarmonyPrefix]
    public static void MeasureCaption(ref ReadOnlySpan<char> label) => ButtonCaptionPatch.LocalizeCaption(ref label);
}

[HarmonyPatch(typeof(ResourcesPanel), "DrawBar")]
public static class ResourceNamePatch
{
    [HarmonyPrefix]
    public static void LocalizeName(ref ReadOnlySpan<char> name)
    {
        string original = name.ToString();
        string translated = Runtime.UiText(original);
        if (translated != original) name = translated.AsSpan();
    }
}

[HarmonyPatch(typeof(ConsoleWidgets), nameof(ConsoleWidgets.BeginComboControl))]
public static class ComboPreviewPatch
{
    [HarmonyPrefix]
    public static void LocalizePreview(ReadOnlySpan<char> id, ref ReadOnlySpan<char> previewText)
    {
        if (!id.StartsWith("Settings")) return;
        string original = previewText.ToString();
        string translated = Runtime.UiText(original);
        if (translated != original) previewText = translated.AsSpan();
    }

    [HarmonyPostfix]
    public static void EnterPopup(ReadOnlySpan<char> id, bool __result) => Runtime.InSettingsCombo = __result && id.StartsWith("Settings");
}

[HarmonyPatch(typeof(ConsoleWidgets), nameof(ConsoleWidgets.EndComboControl))]
public static class ComboEndPatch
{
    [HarmonyPostfix]
    public static void LeavePopup() => Runtime.InSettingsCombo = false;
}

[HarmonyPatch]
public static class ComboOptionPatch
{
    public static IEnumerable<MethodBase> TargetMethods() => typeof(ImGui).GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Where(method => method.Name == "Selectable")
        .Where(method => method.GetParameters().Length > 0 && method.GetParameters()[0].ParameterType == typeof(ImString));

    [HarmonyPrefix]
    public static void LocalizeOption(ref ImString label)
    {
        if (!Runtime.InSettingsCombo) return;
        string original = label.ToString();
        string translated = Runtime.MenuText(original);
        if (translated != original) label = Runtime.ImText(translated);
    }
}

[HarmonyPatch(typeof(ConsoleWidgets), nameof(ConsoleWidgets.Tooltip))]
public static class ConsoleTooltipPatch
{
    [HarmonyPrefix]
    public static void LocalizeTooltip(ref ReadOnlySpan<char> text)
    {
        string original = text.ToString();
        string translated = Runtime.TooltipText(original);
        if (translated != original) text = translated.AsSpan();
    }
}

[HarmonyPatch]
public static class HelperTooltipPatch
{
    public static IEnumerable<MethodBase> TargetMethods() => typeof(ImGuiHelper).GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Where(method => method.Name == nameof(ImGuiHelper.DrawTooltip))
        .Where(method => method.GetParameters().Length > 0 && method.GetParameters()[0].ParameterType == typeof(ImString));

    [HarmonyPrefix]
    public static void LocalizeTooltip(ref ImString tooltip)
    {
        string original = tooltip.ToString();
        string translated = Runtime.TooltipText(original);
        if (translated != original) tooltip = Runtime.ImText(translated);
    }
}

[HarmonyPatch(typeof(KSA.Program), "DrawProgramMenusHook")]
public static class LanguageMenuPatch
{
    [HarmonyPostfix]
    public static void Draw()
    {
        if (!ImGui.BeginMenu(Runtime.SelectedLocale == "zh-CN" ? "语言###Language" : "Language###Language")) return;
        foreach (var pack in Runtime.Packs.Values)
        {
            if (ImGui.MenuItem(pack.DisplayName, default(ImString), Runtime.SelectedLocale == pack.Locale))
                Runtime.SelectLanguage(pack.Locale);
        }
        ImGui.EndMenu();
    }
}
