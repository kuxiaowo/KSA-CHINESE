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
