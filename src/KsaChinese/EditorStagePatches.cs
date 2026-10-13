// Adapted from LiX-Works/KsaUiLanguages (MIT), b504774158cc03b582a0e75bfe94330cb841f11d.
using System.Reflection;
using System.Reflection.Emit;
using Brutal.Numerics;
using HarmonyLib;
using KSA;

namespace KsaChinese;

[HarmonyPatch(typeof(VehicleEditingSpace), nameof(VehicleEditingSpace.DrawStageWindow))]
public static class EditorStageCaptionPatch
{
    public static string ButtonCaption(string source) => source is "REFILL CONSUMABLES" or "RESOURCES" ? Runtime.EditorText(source) : source;
    // ConsoleWidgets prints its caption itself, so the original ID is a
    // separate argument. Do not put an ImGui ### suffix in the visible text.
    public static bool Button(ReadOnlySpan<char> caption, float2 size)
        => ConsoleWidgets.Button(ButtonCaption(caption.ToString()).AsSpan(), caption, size);
    public static float ButtonWidth(ReadOnlySpan<char> caption)
        => ConsoleWidgets.ButtonWidth(ButtonCaption(caption.ToString()).AsSpan());
    public static string TooltipText(string source) => Runtime.TooltipText(source);

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Translate(IEnumerable<CodeInstruction> instructions)
    {
        int buttons=0,widths=0,tips=0;
        foreach(var instruction in instructions)
        {
            if(instruction.operand is MethodInfo native && native.DeclaringType==typeof(ConsoleWidgets))
            {
                var parameters=native.GetParameters();
                if(native.Name==nameof(ConsoleWidgets.Button) && parameters.Length==2
                    && parameters[0].ParameterType==typeof(ReadOnlySpan<char>) && parameters[1].ParameterType==typeof(float2))
                { instruction.opcode=OpCodes.Call; instruction.operand=AccessTools.Method(typeof(EditorStageCaptionPatch),nameof(Button)); buttons++; }
                else if(native.Name==nameof(ConsoleWidgets.ButtonWidth) && parameters.Length==1)
                { instruction.opcode=OpCodes.Call; instruction.operand=AccessTools.Method(typeof(EditorStageCaptionPatch),nameof(ButtonWidth)); widths++; }
            }
            yield return instruction;
            if(instruction.opcode==OpCodes.Ldstr && instruction.operand is string source && source is
                "Every resource on this vehicle is already at max capacity." or
                "Fill every resource on this vehicle to max capacity." or
                "Automatic resource grouping. Switch off to manage resource groups manually." or
                "Open the Resource Groups window")
            { yield return new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(EditorStageCaptionPatch),nameof(TooltipText))); tips++; }
        }
        if(buttons!=2 || widths!=1 || tips!=4) throw new InvalidOperationException($"Editor stage display sites changed: {buttons}/{widths}/{tips}.");
    }
}
