using System.Reflection;
using System.Reflection.Emit;
using Brutal.ImGuiApi;
using Brutal.Numerics;
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
        yield return AccessTools.Method(typeof(SequenceList), nameof(SequenceList.DrawTotalDeltaVFooter),
            [typeof(float2), typeof(float2), typeof(float)]);
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
