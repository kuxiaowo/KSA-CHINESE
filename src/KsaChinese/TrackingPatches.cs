// Adapted from LiX-Works/KsaUiLanguages (MIT), b504774158cc03b582a0e75bfe94330cb841f11d.
using System.Reflection;
using System.Reflection.Emit;
using Brutal.ImGuiApi;
using HarmonyLib;
using KSA;

namespace KsaChinese;

[HarmonyPatch]
public static class TrackingCaptionPatch
{
    public static readonly HashSet<string> Interactive = new(StringComparer.Ordinal)
    {
        "Map Mode", "Lighting Mode", "Locations", "Lat/Lon Lines", "Terminator", "Flight Path", "Subsolar Point",
        "Target", "Zoom", "Manual Zoom", "Reset", "View", "Closest Approach", "Sunrise / Sunset", "Focus on Positions",
        "None", "Terrain Color", "Terrain Height", "Overlay", "Realistic", "Add Burn", "Add Burn Here"
    };
    public static readonly HashSet<string> Plain = new(StringComparer.Ordinal)
    {
        "No ground track available for this vehicle.", "No target set.", "Chaser and target have different parent bodies.",
        "Altitude", "Top-Down", "Period", "LVC Rendezvous Plot", "Along: ", "Alt:   ", "Cross: ", "BURN ",
        "Closest ", "Scale: ", "Apoapsis", "Periapsis", "Sunset", "Sunrise (Post-burn)", "Sunset (Post-burn)",
        "Orbit Closest Approach", "Post-burn Closest Approach", "Sep:   "
    };

    public static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (string name in new[] { "DrawMenuBar", "DrawViewMenu", "DrawContent" })
            yield return AccessTools.Method(typeof(GroundTrackWindow), name);
        foreach (string name in new[] { "DrawMenuBar", "DrawContent", "DrawPanel", "DrawAddBurnPopup", "DrawBurnEditPopup", "TryGetValidTarget" })
            yield return AccessTools.Method(typeof(TargetTrackWindow), name);
    }

    public static string PlainText(string source) => Plain.Contains(source) ? Runtime.PlanningText(source) : source;

    public static string GroundDisplayMode(string source)
        => Interactive.Contains(source) ? Runtime.PlanningMenuText(source) : source;

    public static ImString StaticText(ImString original)
    {
        string source=original.ToString();
        int separator=source.IndexOf("##",StringComparison.Ordinal);
        string visible=separator<0?source:source[..separator];
        if(Interactive.Contains(visible))return Runtime.ImText(Runtime.PlanningMenuText(source));
        if(Plain.Contains(source))return Runtime.ImText(Runtime.PlanningText(source));
        string tooltip=Runtime.TooltipText(source);
        return tooltip==source?original:Runtime.ImText(tooltip);
    }

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Translate(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        var plain=AccessTools.Method(typeof(TrackingCaptionPatch),nameof(PlainText));
        var text=AccessTools.Method(typeof(TrackingCaptionPatch),nameof(StaticText));
        var mode=AccessTools.Method(typeof(TrackingCaptionPatch),nameof(GroundDisplayMode));
        foreach(var instruction in instructions)
        {
            yield return instruction;
            if(instruction.opcode==OpCodes.Ldstr && instruction.operand is string literal && Plain.Contains(literal))
                yield return new CodeInstruction(OpCodes.Call,plain);
            else if(instruction.operand is MethodInfo method)
            {
                if(method.DeclaringType==typeof(ImString) && method.Name=="op_Implicit" && method.GetParameters()[0].ParameterType==typeof(ReadOnlySpan<byte>))
                    yield return new CodeInstruction(OpCodes.Call,text);
                else if(__originalMethod.DeclaringType==typeof(GroundTrackWindow) && __originalMethod.Name=="DrawViewMenu"
                    && method.Name=="GetName" && method.ReturnType==typeof(string)
                    && method.GetParameters() is [{ParameterType:var type}] && type.DeclaringType==typeof(GroundTrackWindow))
                    yield return new CodeInstruction(OpCodes.Call,mode);
            }
        }
    }
}

[HarmonyPatch(typeof(ConsoleStyle), "BeginWindowCore")]
public static class TrackingWindowTitlePatch
{
    public static string DisplayTitle(string original, string signature)
    {
        string? suffix=signature=="KSA-GND"?" | Ground Track":signature=="KSA-TGT"?" | Target Track":null;
        if(suffix is null || !original.EndsWith(suffix,StringComparison.Ordinal))return original;
        return original[..^suffix.Length]+" | "+Runtime.PlanningText(suffix[3..]);
    }
    [HarmonyPrefix]
    public static void Localize(ReadOnlySpan<char> signature, ref ReadOnlySpan<char> title)
        => title=DisplayTitle(title.ToString(),signature.ToString()).AsSpan();
}
