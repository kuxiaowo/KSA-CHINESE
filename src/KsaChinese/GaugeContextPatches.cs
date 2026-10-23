using System.Reflection;
using System.Reflection.Emit;
using Brutal.ImGuiApi;
using HarmonyLib;
using KSA;

namespace KsaChinese;

// GaugeCanvas has its own context menu; ImGuiWindow.DrawViewMenu does not cover
// it. Keep the game's checked/disabled behavior and both helper overloads.
[HarmonyPatch(typeof(GaugeCanvas), nameof(GaugeCanvas.OnDrawUi))]
public static class GaugeContextMenuPatch
{
    private static readonly HashSet<string> Labels = new(StringComparer.Ordinal)
        { "Enabled", "Always on top", "Reset", "Detached", "Lock", "Resizing", "Selected", "Interacting", "NeedsResize" };

    public static string Caption(string original, bool disabled)
    {
        if (!Labels.Contains(original)) return original;
        string translated = Runtime.ScopedText("Gauge.Context." + original, ExpandedUi.Text(original));
        // Disabled helpers use TextDisabled, which displays ### literally.
        // Active helpers use Selectable and need a stable ID in both locales.
        // The native helper's trailing spaces end up AFTER ### and no longer
        // reserve visible width. Reserve room before it for the checkmark.
        const string checkmarkGap = "      ";
        return disabled ? translated + checkmarkGap : translated + checkmarkGap + "###" + original;
    }
    public static bool Value(ImString label, bool selected, bool disabled)
        => ImGuiHelper.DrawMenuItem(Runtime.ImText(Caption(label.ToString(), disabled)), selected, disabled);
    public static bool Toggle(ImString label, ref bool selected, bool disabled)
        => ImGuiHelper.DrawMenuItem(Runtime.ImText(Caption(label.ToString(), disabled)), ref selected, disabled);

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Translate(IEnumerable<CodeInstruction> instructions)
    {
        int values = 0, toggles = 0;
        foreach (var instruction in instructions)
        {
            if (instruction.operand is MethodInfo method && method.DeclaringType == typeof(ImGuiHelper)
                && method.Name == nameof(ImGuiHelper.DrawMenuItem))
            {
                var p = method.GetParameters();
                if (p.Length != 3 || p[0].ParameterType != typeof(ImString) || p[2].ParameterType != typeof(bool))
                    throw new InvalidOperationException("5602 gauge menu helper signature changed.");
                bool toggle = p[1].ParameterType == typeof(bool).MakeByRefType();
                if (!toggle && p[1].ParameterType != typeof(bool)) throw new InvalidOperationException("Unsupported gauge menu value.");
                instruction.opcode = OpCodes.Call;
                instruction.operand = AccessTools.Method(typeof(GaugeContextMenuPatch), toggle ? nameof(Toggle) : nameof(Value));
                if (toggle) toggles++; else values++;
            }
            yield return instruction;
        }
        if (values != 7 || toggles != 2) throw new InvalidOperationException($"5602 gauge menu sites changed: {values}/7, {toggles}/2.");
    }
}
