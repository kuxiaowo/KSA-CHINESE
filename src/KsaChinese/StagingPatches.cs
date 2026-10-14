using System.Reflection;
using System.Reflection.Emit;
using Brutal.ImGuiApi;
using HarmonyLib;
using KSA;

namespace KsaChinese;

// Only caption fragments in the sequence renderer. Numeric formatting, units,
// performance calculations and invisible interaction IDs stay in the game.
[HarmonyPatch]
public static class StagingCaptionPatch
{
    public const string EnvironmentHint = "Environment for this sequence's delta-v and TWR. Click to toggle. Atmospheric uses sea-level (1 atm) engine performance.";
    private static readonly HashSet<string> Captions = new(StringComparer.Ordinal)
        { "ATM", "VAC", "Isp ", "TWR ", "TOTAL dV", EnvironmentHint };

    public static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(SequenceList), nameof(SequenceList.DrawSequenceContent));
        yield return AccessTools.Method(typeof(SequenceList), nameof(SequenceList.DrawTotalDeltaVFooter));
    }

    public static string Text(string source)
        => Captions.Contains(source) ? Runtime.ScopedText("Staging." + source, source) : source;

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Translate(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        int count = 0;
        foreach (var instruction in instructions)
        {
            yield return instruction;
            if (instruction.opcode == OpCodes.Ldstr && instruction.operand is string source && Captions.Contains(source))
            {
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(StagingCaptionPatch), nameof(Text)));
                count++;
            }
        }
        int expected = __originalMethod.Name == nameof(SequenceList.DrawSequenceContent) ? 6 : 1;
        if (count != expected) throw new InvalidOperationException($"5602 staging captions changed: {__originalMethod.Name} {count}/{expected}.");
    }
}

// The English renderer centers Isp beside a right-aligned TWR. Longer Chinese
// captions collide at the default narrow gauge width; align Isp to the left.
[HarmonyPatch(typeof(SequenceList), nameof(SequenceList.DrawSequenceContent))]
public static class StagingIspLayoutPatch
{
    [ThreadStatic] private static float _left;
    [HarmonyPrefix]
    public static void Capture(out float __state)
    {
        __state = _left;
        _left = ImGui.GetCursorScreenPos().X + ImGui.GetStyle().FramePadding.X;
    }
    [HarmonyFinalizer]
    public static Exception? Restore(Exception? __exception, float __state)
    { _left = __state; return __exception; }
    public static float Position(float original) => Runtime.SelectedLocale == "zh-CN" ? _left : original;

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Translate(IEnumerable<CodeInstruction> instructions)
    {
        bool inIsp = false, centerRead = false;
        int count = 0;
        foreach (var instruction in instructions)
        {
            if (instruction.opcode == OpCodes.Ldstr && Equals(instruction.operand, "Isp ")) inIsp = true;
            if (inIsp && instruction.opcode == OpCodes.Ldfld && instruction.operand is FieldInfo field && field.Name == "ColCenterX") centerRead = true;
            yield return instruction;
            if (centerRead && instruction.opcode == OpCodes.Sub)
            {
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(StagingIspLayoutPatch), nameof(Position)));
                inIsp = centerRead = false;
                count++;
            }
        }
        if (count != 1) throw new InvalidOperationException($"5602 staging Isp position changed: {count}/1.");
    }
}
