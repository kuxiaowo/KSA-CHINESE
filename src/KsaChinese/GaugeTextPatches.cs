using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using KSA;

namespace KsaChinese;

// 5602 draws gauge label/button text through ImGui. Keep XML fields and
// component/rect IDs untouched; translate only values read for this renderer.
[HarmonyPatch(typeof(GaugeCanvas), "DrawWidgetText")]
public static class GaugeWidgetTextPatch
{
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Translate(IEnumerable<CodeInstruction> instructions)
    {
        int labels=0;
        foreach(var instruction in instructions)
        {
            yield return instruction;
            if(instruction.operand is MethodInfo method && method.Name=="get_Label"
                && (method.DeclaringType==typeof(GaugeLabelReference) || method.DeclaringType==typeof(GaugeButtonReference)))
            {
                yield return new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(Runtime),nameof(Runtime.GaugeText)));
                labels++;
            }
        }
        if(labels!=4) throw new InvalidOperationException($"5602 gauge text contract changed: {labels}/4 label reads.");
    }
}

// UI and gauges use distinct font paths. Select CJK only in the gauge font
// lookup, retaining the original MilSpec font for English and numeric assets.
[HarmonyPatch(typeof(ConsoleStyle), "RefreshFontCache")]
public static class GaugeFontPatch
{
    public static string Name() => Runtime.SelectedLocale=="zh-CN" ? Runtime.FontName : "MilSpec33558";
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Translate(IEnumerable<CodeInstruction> instructions)
    {
        int count=0;
        foreach(var instruction in instructions)
        {
            if(instruction.opcode==OpCodes.Ldstr && Equals(instruction.operand,"MilSpec33558"))
            { instruction.opcode=OpCodes.Call; instruction.operand=AccessTools.Method(typeof(GaugeFontPatch),nameof(Name)); count++; }
            yield return instruction;
        }
        if(count!=1) throw new InvalidOperationException($"5602 gauge font lookup changed: {count}/1.");
    }
}
