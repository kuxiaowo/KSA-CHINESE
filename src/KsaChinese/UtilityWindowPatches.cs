// Adapted from LiX-Works/KsaUiLanguages (MIT), b504774158cc03b582a0e75bfe94330cb841f11d.
using System.Reflection;
using System.Reflection.Emit;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using HarmonyLib;
using KSA;

namespace KsaChinese;

/// <summary>
/// Translates fixed captions in the save lists and Universe Manifest. Save names,
/// object IDs, popup IDs, and table sort IDs continue to use the game's originals.
/// </summary>
[HarmonyPatch]
public static class UtilityWindowPatches
{
    private static readonly HashSet<string> MenuLabels = new(StringComparer.Ordinal)
    {
        "Refresh", "New", "Load", "Delete", "Overwrite", "Filter", "Columns", "Unfiltered",
        "Show All", "Show None", "Clear Debris", "Name", "Size", "Version", "Date", "Id",
        "Type", "SOI", "Apoapsis", "Periapsis", "Velocity", "Distance", "Vehicles", "KittenEva",
        "Debris", "MinorBodies", "Planets", "Asteroids", "Comets", "PeriodicComets",
        "InterstellarComets", "StellarBody"
    };

    private static readonly HashSet<string> UtilityTextLabels = new(StringComparer.Ordinal)
    {
        "Hold alt to multiselect", "You must not be in the Vehicle Editor", "Vehicle", "MinorBody",
        "Planet", "Asteroid", "Comet", "PeriodicComet", "InterstellarComet", "StellarBody", "Barycenter"
    };

    private static readonly HashSet<string> TooltipLabels = new(StringComparer.Ordinal)
    {
        "Reload all saves from the save folder", "Reload all vehicles from the save folder",
        "Will create a new save of the current state with the given name",
        "You must be in the Vehicle Editor to save a vehicle",
        "Double click a save to load it, and right click to gain access to that saves context menu. Clicking on a header item will toggle sorting for that column.",
        "Delete every piece of debris in this system", "There is no debris in this system",
        "Double-click an item to focus on it or Shift-Click to target it with the currently controlled vessel. You can also filter objects in this window or toggle columns.",
        "Recover Vehicle", "Delete vehicle", "Currently Following", "Target of Controlled Vehicle",
        "Controlled Vehicle", "Shift-Click to set as target", "Shift-Click to unset as target",
        "Double-click to follow and control", "Double-click to follow"
    };

    private static readonly HashSet<string> LiteralKeys = new(StringComparer.Ordinal)
    {
        " SAVE", " SAVES", " VEHICLE", " VEHICLES"
    };

    public static IEnumerable<MethodBase> TargetMethods()
    {
        Type gameWindow = Nested(typeof(GameSaves), "GameSavesWindow");
        Type vehicleWindow = Nested(typeof(VehicleSaves), "VehicleSavesWindow");
        Type manifestWindow = Nested(typeof(UniverseManifest), "Window");

        foreach (string name in new[] { "DrawViewMenu", "DrawMenuBar", "DrawConsoleFooter" })
            yield return Require(gameWindow, name);
        yield return RequireArity(gameWindow, "DrawContent", 1);
        foreach (string name in new[] { "DrawViewMenu", "DrawMenuBar", "DrawConsoleFooter" })
            yield return Require(vehicleWindow, name);
        yield return RequireArity(vehicleWindow, "DrawContent", 1);

        yield return Require(typeof(GameSave), "LoadOnConfirm");
        yield return Require(typeof(GameSaves), nameof(GameSaves.MakeSave), typeof(string));
        yield return Require(typeof(UncompressedSave), "DrawInTable");
        yield return RequireArity(typeof(UncompressedVehicleSave), "DrawInTable", 1);

        // The overwrite confirmation callbacks in UncompressedVehicleSave are
        // emitted as methods on compiler-generated closure types.
        foreach (Type closure in typeof(UncompressedVehicleSave).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
        {
            foreach (MethodInfo method in closure.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (method.Name.Contains("DrawInTable", StringComparison.Ordinal))
                    yield return method;
            }
        }

        yield return Require(manifestWindow, "DrawMenuBar");
        yield return RequireArity(manifestWindow, "DrawContent", 1);
        yield return Require(manifestWindow, "Draw", typeof(Astronomical));
    }

    private static Type Nested(Type owner, string name)
        => owner.GetNestedType(name, BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new MissingMemberException(owner.FullName, name);

    private static MethodBase Require(Type type, string name, params Type[] parameters)
        => (parameters.Length == 0 ? AccessTools.Method(type, name) : AccessTools.Method(type, name, parameters))
            ?? throw new MissingMethodException(type.FullName, name);

    private static MethodBase RequireArity(Type type, string name, int parameterCount)
        => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .SingleOrDefault(method => method.Name == name && method.GetParameters().Length == parameterCount)
            ?? throw new MissingMethodException(type.FullName, name);

    private static string VisiblePart(string source)
    {
        int separator = source.IndexOf("##", StringComparison.Ordinal);
        return separator < 0 ? source : source[..separator];
    }

    public static ImString StaticUtf8Caption(ImString source)
    {
        string original = source.ToString();
        string visible = VisiblePart(original);
        if (MenuLabels.Contains(visible))
            return Runtime.ImText(Runtime.UtilityMenuText(original));
        if (UtilityTextLabels.Contains(original))
            return Runtime.ImText(Runtime.UtilityText(original));
        if (TooltipLabels.Contains(original))
            return Runtime.ImText(Runtime.TooltipText(original));
        return source;
    }

    public static ImString MenuCaption(ImString source)
        => Runtime.ImText(Runtime.UtilityMenuText(source.ToString()));

    public static ImString PlainCaption(ImString source)
        => Runtime.ImText(Runtime.UtilityText(source.ToString()));

    public static ImString TooltipCaption(ImString source)
        => Runtime.ImText(Runtime.TooltipText(source.ToString()));

    public static bool MenuItem(ImString label, ImString shortcut, bool selected, bool enabled)
        => ImGui.MenuItem(MenuCaption(label), shortcut, selected, enabled);

    public static bool BeginMenu(ImString label, bool enabled)
        => ImGui.BeginMenu(MenuCaption(label), enabled);

    public static bool DrawMenuItem(ImString label, bool selected, bool disabled)
        => ImGuiHelper.DrawMenuItem(MenuCaption(label), selected, disabled);

    public static bool DrawMenuItem(ImString label, ref bool selected, bool disabled)
        => ImGuiHelper.DrawMenuItem(MenuCaption(label), ref selected, disabled);

    public static void TableSetupColumn(ImString label, ImGuiTableColumnFlags flags, float width, ImGuiID userId)
        => ImGui.TableSetupColumn(MenuCaption(label), flags, width, userId);

    public static void TextDisabled(ImString text)
        => ImGui.TextDisabled(PlainCaption(text));

    public static void TextColored(in float4 color, ImString text)
        => ImGui.TextColored(in color, PlainCaption(text));

    public static void ManifestText(ImString text)
        => ImGui.Text(PlainCaption(text));

    public static void DrawTooltip(ImString text, Func<bool>? isHovered)
        => ImGuiHelper.DrawTooltip(TooltipCaption(text), isHovered);

    public static void SetTooltip(ImString text)
        => ImGui.SetTooltip(TooltipCaption(text));

    public static ConfirmActionPopup CreateConfirmPopup(Action? action, string title, string message)
        => ConfirmActionPopup.Create(action, Runtime.UtilityText(title), TranslateConfirmMessage(title, message));

    private static string TranslateConfirmMessage(string title, string source)
    {
        if (title == "LOAD SAVE" && TrySurrounding(source,
                "You are currently in a game, are you sure you want to load '", "'?", out string loadName))
            return Format("You are currently in a game, are you sure you want to load '{0}'?", loadName);

        if (title == "DELETE SAVE" && TrySurrounding(source, "Are you sure you want to delete save '", "'?", out string saveName))
            return Format("Are you sure you want to delete save '{0}'?", saveName);
        if (title == "OVERWRITE SAVE" && TrySurrounding(source, "Are you sure you want to overwrite save '", "'?", out string overwriteSaveName))
            return Format("Are you sure you want to overwrite save '{0}'?", overwriteSaveName);

        if (title is "DELETE" or "OVERWRITE")
        {
            if (TrySurrounding(source, "Are you sure you want to delete saved vehicle '", "'?", out string deleteVehicleName))
                return Format("Are you sure you want to delete saved vehicle '{0}'?", deleteVehicleName);
            if (TrySurrounding(source, "Are you sure you want to overwrite saved vehicle '", "'?", out string overwriteVehicleName))
                return Format("Are you sure you want to overwrite saved vehicle '{0}'?", overwriteVehicleName);
            if (TrySurrounding(source, "Are you sure you want to overwrite saved vehicle", "'?", out string legacyVehicleName))
                return Format("Are you sure you want to overwrite saved vehicle'{0}'?", legacyVehicleName);
        }

        if (title == "CLEAR DEBRIS" && TrySurrounding(source,
                "Are you sure you want to delete all ", " debris objects in this system?", out string debrisCount))
            return Format("Are you sure you want to delete all {0} debris objects in this system?", debrisCount);

        if (title == "RECOVER" && TrySurrounding(source, "Are you sure you want to recover ", "?", out string recoverId))
            return Format("Are you sure you want to recover {0}?", recoverId);
        if (title == "DESTROY" && TrySurrounding(source, "Are you sure you want to destroy ", "?", out string destroyId))
            return Format("Are you sure you want to destroy {0}?", destroyId);

        return source;
    }

    private static string Format(string template, string value)
        => Runtime.UtilityText(template).Replace("{0}", value, StringComparison.Ordinal);

    private static bool TrySurrounding(string source, string prefix, string suffix, out string value)
    {
        if (source.StartsWith(prefix, StringComparison.Ordinal)
            && source.EndsWith(suffix, StringComparison.Ordinal)
            && source.Length >= prefix.Length + suffix.Length)
        {
            value = source[prefix.Length..^suffix.Length];
            return true;
        }
        value = string.Empty;
        return false;
    }

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Translate(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        MethodInfo utf8Caption = AccessTools.Method(typeof(UtilityWindowPatches), nameof(StaticUtf8Caption));
        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.opcode == OpCodes.Call && instruction.operand is MethodInfo native)
            {
                MethodInfo? replacement = ReplacementFor(native, __originalMethod);
                if (replacement is not null)
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = replacement;
                }
            }

            bool staticUtf8 = instruction.opcode == OpCodes.Call && instruction.operand is MethodInfo conversion
                && conversion.DeclaringType == typeof(ImString) && conversion.Name == "op_Implicit"
                && conversion.GetParameters() is [{ ParameterType: var argumentType }]
                && argumentType == typeof(ReadOnlySpan<byte>);
            bool footerFragment = instruction.opcode == OpCodes.Ldstr && instruction.operand is string literal && LiteralKeys.Contains(literal);
            yield return instruction;
            if (staticUtf8)
                yield return new CodeInstruction(OpCodes.Call, utf8Caption);
            else if (footerFragment)
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Runtime), nameof(Runtime.UtilityText)));
        }
    }

    private static MethodInfo? ReplacementFor(MethodInfo method, MethodBase original)
    {
        ParameterInfo[] args = method.GetParameters();
        if (method.DeclaringType == typeof(ImGui) && method.Name == nameof(ImGui.MenuItem)
            && args.Length == 4 && args[0].ParameterType == typeof(ImString)
            && args[1].ParameterType == typeof(ImString) && args[2].ParameterType == typeof(bool)
            && args[3].ParameterType == typeof(bool))
            return AccessTools.Method(typeof(UtilityWindowPatches), nameof(MenuItem));
        if (method.DeclaringType == typeof(ImGui) && method.Name == nameof(ImGui.BeginMenu)
            && args.Length == 2 && args[0].ParameterType == typeof(ImString) && args[1].ParameterType == typeof(bool))
            return AccessTools.Method(typeof(UtilityWindowPatches), nameof(BeginMenu));
        if (method.DeclaringType == typeof(ImGuiHelper) && method.Name == nameof(ImGuiHelper.DrawMenuItem)
            && args.Length == 3 && args[0].ParameterType == typeof(ImString) && args[1].ParameterType == typeof(bool))
            return AccessTools.Method(typeof(UtilityWindowPatches), nameof(DrawMenuItem), args.Select(p => p.ParameterType).ToArray());
        if (method.DeclaringType == typeof(ImGui) && method.Name == nameof(ImGui.TableSetupColumn)
            && args.Length == 4 && args[0].ParameterType == typeof(ImString))
            return AccessTools.Method(typeof(UtilityWindowPatches), nameof(TableSetupColumn));
        if (method.DeclaringType == typeof(ImGui) && method.Name == nameof(ImGui.TextDisabled)
            && args is [{ ParameterType: var disabledText }] && disabledText == typeof(ImString))
            return AccessTools.Method(typeof(UtilityWindowPatches), nameof(TextDisabled));
        if (method.DeclaringType == typeof(ImGui) && method.Name == nameof(ImGui.TextColored)
            && args.Length == 2 && args[1].ParameterType == typeof(ImString))
            return AccessTools.Method(typeof(UtilityWindowPatches), nameof(TextColored));
        if (method.DeclaringType == typeof(ImGuiHelper) && method.Name == nameof(ImGuiHelper.DrawTooltip)
            && args.Length == 2 && args[0].ParameterType == typeof(ImString)
            && args[1].ParameterType == typeof(Func<bool>))
            return AccessTools.Method(typeof(UtilityWindowPatches), nameof(DrawTooltip), args.Select(p => p.ParameterType).ToArray());
        if (method.DeclaringType == typeof(ImGui) && method.Name == nameof(ImGui.SetTooltip)
            && args is [{ ParameterType: var tooltipText }] && tooltipText == typeof(ImString))
            return AccessTools.Method(typeof(UtilityWindowPatches), nameof(SetTooltip));
        if (method.DeclaringType == typeof(ImGui) && method.Name == nameof(ImGui.Text)
            && args is [{ ParameterType: var plainText }] && plainText == typeof(ImString)
            && original.DeclaringType == Nested(typeof(UniverseManifest), "Window") && original.Name == "Draw")
            return AccessTools.Method(typeof(UtilityWindowPatches), nameof(ManifestText));
        if (method.DeclaringType == typeof(ConfirmActionPopup) && method.Name == nameof(ConfirmActionPopup.Create)
            && args.Length == 3 && args[0].ParameterType == typeof(Action)
            && args[1].ParameterType == typeof(string) && args[2].ParameterType == typeof(string))
            return AccessTools.Method(typeof(UtilityWindowPatches), nameof(CreateConfirmPopup));
        return null;
    }
}
