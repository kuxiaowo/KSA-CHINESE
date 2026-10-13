// Adapted from LiX-Works/KsaUiLanguages (MIT), b504774158cc03b582a0e75bfe94330cb841f11d.
using System.Reflection;
using System.Reflection.Emit;
using Brutal.ImGuiApi;
using HarmonyLib;
using KSA;

namespace KsaChinese;

[HarmonyPatch]
public static class RosterCaptionPatch
{
    private static readonly HashSet<string> Headers = new(StringComparer.Ordinal)
    {
        "Name", "Assigned Vehicle", "Missions", "Current Mission Elapsed", "Total Mission Elapsed",
        "Distance Travelled", "Fastest Speed"
    };

    private static readonly HashSet<string> PlainCaptions = new(StringComparer.Ordinal) { "Unassigned" };
    private static readonly HashSet<string> FooterFragments = new(StringComparer.Ordinal) { " LOST", " KITTEN", " KITTENS" };

    public static IEnumerable<MethodBase> TargetMethods()
    {
        var window = typeof(KittenRosterWindow).GetNestedType("Window", BindingFlags.NonPublic)!;
        foreach (string name in new[] { "DrawRoster", "DrawMemorial", "Draw", "DrawConsoleFooter" })
            yield return AccessTools.Method(window, name);
    }

    public static ImString DisplayCaption(ImString caption)
    {
        string source = caption.ToString();
        if (Headers.Contains(source)) return Runtime.ImText(Runtime.MenuText(source));
        return PlainCaptions.Contains(source) ? Runtime.ImText(Runtime.UiText(source)) : caption;
    }

    public static string FooterText(string fragment)
        => FooterFragments.Contains(fragment) ? Runtime.UiText(fragment) : fragment;

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Translate(IEnumerable<CodeInstruction> instructions)
    {
        var display = AccessTools.Method(typeof(RosterCaptionPatch), nameof(DisplayCaption));
        var footer = AccessTools.Method(typeof(RosterCaptionPatch), nameof(FooterText));
        foreach (var instruction in instructions)
        {
            yield return instruction;
            if (instruction.opcode == OpCodes.Call && instruction.operand is MethodInfo method
                && method.DeclaringType == typeof(ImString) && method.Name == "op_Implicit"
                && method.GetParameters()[0].ParameterType == typeof(ReadOnlySpan<byte>))
                yield return new CodeInstruction(OpCodes.Call, display);
            else if (instruction.opcode == OpCodes.Ldstr && instruction.operand is string text && FooterFragments.Contains(text))
                yield return new CodeInstruction(OpCodes.Call, footer);
        }
    }
}

[HarmonyPatch(typeof(ConsoleWidgets), nameof(ConsoleWidgets.Segmented))]
public static class RosterTabsPatch
{
    [HarmonyPrefix]
    public static void Localize(ReadOnlySpan<char> id, ref ReadOnlySpan<string> segments)
    {
        if (!id.SequenceEqual("KittenRosterTabs")) return;
        // Replace only this draw call's captions, retaining the original static array and tab indices.
        segments = segments.ToArray().Select(Runtime.UiText).ToArray();
    }
}

[HarmonyPatch(typeof(ConsoleStyle), "BeginWindowCore")]
public static class RosterTitlePatch
{
    [HarmonyPrefix]
    public static void Localize(ReadOnlySpan<char> signature, ref ReadOnlySpan<char> title)
    {
        if (signature.SequenceEqual("KSA-ROS") && title.SequenceEqual("Kitten Roster"))
            title = Runtime.UiText(title.ToString()).AsSpan();
    }
}

[HarmonyPatch]
public static class CelestialCaptionPatch
{
    private static readonly HashSet<string> Readouts = new(StringComparer.Ordinal)
    {
        "MASS", "SPHERE OF INFLUENCE", "AXIAL TILT", "RETROGRADE", "Yes", "No",
        "TRUE ANOMALY", "SEMI-MAJOR AXIS", "SEMI-MINOR AXIS", "ORBITAL PERIOD", "INCLINATION",
        "ECCENTRICITY", "ORBIT TYPE", "PERIAPSIS", "APOAPSIS", "ORBITAL SPEED",
        "TIME AT PERIAPSIS", "TIME SINCE PE", "PERIOD", "Unknown"
    };

    private static readonly HashSet<string> InteractiveCaptions = new(StringComparer.Ordinal)
    {
        "LocalPosition", "OrbitVelocity", "Rotation", "Draw Axes", "Show SOI"
    };

    private static readonly HashSet<string> PlainCaptions = new(StringComparer.Ordinal)
    {
        "Quadrant", "First", "Second", "Third", "Fourth"
    };

    public static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(Celestial), nameof(Celestial.DrawCelestialWindowData));
        yield return AccessTools.Method(typeof(IOrbiter), "DrawCelestialWindowData");
    }

    public static string ReadoutText(string source) => Readouts.Contains(source) ? Runtime.UiText(source) : source;

    public static ImString DisplayCaption(ImString caption)
    {
        string source = caption.ToString();
        if (InteractiveCaptions.Contains(source)) return Runtime.ImText(Runtime.MenuText(source));
        return PlainCaptions.Contains(source) ? Runtime.ImText(Runtime.UiText(source)) : caption;
    }

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Translate(IEnumerable<CodeInstruction> instructions)
    {
        var readout = AccessTools.Method(typeof(CelestialCaptionPatch), nameof(ReadoutText));
        var display = AccessTools.Method(typeof(CelestialCaptionPatch), nameof(DisplayCaption));
        foreach (var instruction in instructions)
        {
            yield return instruction;
            if (instruction.opcode == OpCodes.Ldstr && instruction.operand is string text && Readouts.Contains(text))
                yield return new CodeInstruction(OpCodes.Call, readout);
            else if (instruction.opcode == OpCodes.Call && instruction.operand is MethodInfo method
                && method.DeclaringType == typeof(ImString) && method.Name == "op_Implicit"
                && method.GetParameters()[0].ParameterType == typeof(ReadOnlySpan<byte>))
                yield return new CodeInstruction(OpCodes.Call, display);
        }
    }
}

[HarmonyPatch(typeof(ExitPopup), "OnDrawUi")]
public static class ExitPopupCaptionPatch
{
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Translate(IEnumerable<CodeInstruction> instructions)
    {
        var lookup = AccessTools.Method(typeof(Runtime), nameof(Runtime.Literal));
        foreach (var instruction in instructions)
        {
            yield return instruction;
            if (instruction.opcode == OpCodes.Ldstr && instruction.operand is string text
                && text is "EXIT" or "Are you sure you want to exit the game?")
                yield return new CodeInstruction(OpCodes.Call, lookup);
        }
    }
}
