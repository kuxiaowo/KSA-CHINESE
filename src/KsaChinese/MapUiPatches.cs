using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using KSA;

namespace KsaChinese;

// 5638 uses the row's original label again as its button ID. Translate at
// display calls inside these methods, keeping that original argument intact.
[HarmonyPatch]
public static class MapUiPatch
{
    public static readonly HashSet<string> FixedLabels = new(StringComparer.Ordinal)
    {
        "Vehicle", "Map", "None", "VEHICLE CONTROL", "MAP CONTROL",
        "BODY GRID PLANES", "ALIGN PLANES", "MAGNETOSPHERES",
        "Draw the equatorial plane of the followed body and its target alongside the ecliptic.",
        "Hold every plane parallel to the ecliptic instead of orienting it by its body's axial tilt.",
        "Draw each magnetised body's radiation belts, field lines, magnetopause and bow shock.",
        "Release", "Take", "Focus Vehicle", "Recenter"
    };
    public static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var name in new[] { "OnDrawUi", "DrawButtonRow" })
            yield return AccessTools.Method(typeof(MapController), name)
                ?? throw new MissingMethodException(typeof(MapController).FullName, name);
    }

    public static string FixedValue(string value) => ExpandedUi.Text(value);
    public static void Header(ReadOnlySpan<char> label)
        => ConsoleWidgets.RegionHeader(ExpandedUi.Text(label.ToString()));
    public static void Readout(ReadOnlySpan<char> key, ReadOnlySpan<char> value)
        => ConsoleWidgets.Readout(ExpandedUi.Text(key.ToString()), value);
    public static string RowText(string original)
    {
        int shortcut = original.IndexOf(" [", StringComparison.Ordinal);
        return shortcut < 0 ? ExpandedUi.Text(original)
            : ExpandedUi.Text(original[..shortcut]) + original[shortcut..];
    }
    public static void Row(ReadOnlySpan<char> label) => ConsoleWidgets.BeginRow(RowText(label.ToString()));

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Translate(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        int readouts = 0, headers = 0, rows = 0;
        foreach (var instruction in instructions)
        {
            if (instruction.operand is MethodInfo method && method.DeclaringType == typeof(ConsoleWidgets))
            {
                string? replacement = method.Name switch
                {
                    "RegionHeader" => nameof(Header), "Readout" => nameof(Readout), "BeginRow" => nameof(Row), _ => null
                };
                if (replacement != null)
                {
                    if (replacement == nameof(Header)) headers++;
                    else if (replacement == nameof(Readout)) readouts++;
                    else rows++;
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = AccessTools.Method(typeof(MapUiPatch), replacement);
                }
            }
            yield return instruction;
            // Only literal mode/fallback values, never the dynamic vehicle ID.
            if (instruction.opcode == OpCodes.Ldstr && instruction.operand is string text
                && FixedLabels.Contains(text))
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MapUiPatch), nameof(FixedValue)));
        }
        if (__originalMethod.Name == "OnDrawUi" && (readouts != 9 || headers != 3 || rows != 0))
            throw new InvalidOperationException($"Map display contract changed: readouts={readouts}, headers={headers}, rows={rows}.");
        if (__originalMethod.Name == "DrawButtonRow" && (readouts != 0 || headers != 0 || rows != 1))
            throw new InvalidOperationException("Map button row display contract changed.");
    }
}
