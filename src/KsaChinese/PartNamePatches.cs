// Adapted from LiX-Works/KsaUiLanguages (MIT), b504774158cc03b582a0e75bfe94330cb841f11d.
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using KSA;

namespace KsaChinese;

[HarmonyPatch(typeof(PartArchetypes), nameof(PartArchetypes.AppendTooltip))]
public static class PartTooltipNamePatch
{
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Translate(IEnumerable<CodeInstruction> instructions)
    {
        foreach (var instruction in instructions)
        {
            // Replace the display read at this tooltip call site. Template fields,
            // save data and Part.GetKey()/Id remain the game's original values.
            if (instruction.opcode == OpCodes.Ldfld && instruction.operand is FieldInfo field
                && field.DeclaringType == typeof(PartTemplate) && field.Name == nameof(PartTemplate.DisplayName))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = AccessTools.Method(typeof(Runtime), nameof(Runtime.PartName));
            }
            yield return instruction;
        }
    }
}

[HarmonyPatch(typeof(VehicleEditor), nameof(VehicleEditor.DrawPartUi))]
public static class PartWindowNamePatch
{
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Translate(IEnumerable<CodeInstruction> instructions)
    {
        bool replaced = false;
        foreach (var instruction in instructions)
        {
            // The first display getter is the window title, whose ImGui ID is
            // independently built from InstanceId. Leave later names untouched.
            if (!replaced && instruction.operand is MethodInfo method && method.DeclaringType == typeof(Part)
                && method.Name == "get_DisplayName")
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = AccessTools.Method(typeof(Runtime), nameof(Runtime.PartCaption));
                replaced = true;
            }
            yield return instruction;
        }
    }
}

// Part.OnDrawUi reads the name three times: window ID, visible title and
// popup heading. Leave the first read byte-exact. Both languages use the same
// canonical popup heading ID, as with the other translated ImGui menus.
[HarmonyPatch(typeof(Part), nameof(Part.OnDrawUi))]
public static class FlightPartNamePatch
{
    public static string MenuCaption(Part part)
    {
        string original = part.DisplayName;
        if (original != part.Template.DisplayName && original != part.Template.Id) return original;
        if (!Runtime.Packs.Values.Any(pack => pack.Parts.TryGetValue(part.Template.Id, out string? value)
            && value != original)) return original;
        string translated = Runtime.PartCaption(part);
        int stable = original.IndexOf("###", StringComparison.Ordinal);
        return translated + (stable >= 0 ? original[stable..] : "###" + original);
    }

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Translate(IEnumerable<CodeInstruction> instructions)
    {
        int names = 0;
        foreach (var instruction in instructions)
        {
            if (instruction.operand is MethodInfo method && method.DeclaringType == typeof(Part)
                && method.Name == "get_DisplayName")
            {
                names++;
                if (names is 2 or 3)
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = names == 2
                        ? AccessTools.Method(typeof(Runtime), nameof(Runtime.PartCaption))
                        : AccessTools.Method(typeof(FlightPartNamePatch), nameof(MenuCaption));
                }
            }
            yield return instruction;
        }
        if (names != 3)
            throw new InvalidOperationException($"Flight part name display sites changed: {names}.");
    }
}
