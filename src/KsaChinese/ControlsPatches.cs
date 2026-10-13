// Adapted from LiX-Works/KsaUiLanguages (MIT), b504774158cc03b582a0e75bfe94330cb841f11d.
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using KSA;

namespace KsaChinese;

/// <summary>
/// Localizes only the action caption used by the Controls settings row.
/// The InputAction enum, binding values, TOML names, and save path are untouched.
/// </summary>
[HarmonyPatch]
public static class ControlsKeyCaptionPatch
{
    public static MethodBase TargetMethod()
        => AccessTools.Method(typeof(GameSettings), "DrawKeyAssignmentRow")
            ?? throw new MissingMethodException(typeof(GameSettings).FullName, "DrawKeyAssignmentRow");

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Translate(IEnumerable<CodeInstruction> instructions)
    {
        var lookup = AccessTools.Method(typeof(Runtime), nameof(Runtime.ControlText), new[] { typeof(string) })!;
        foreach (var instruction in instructions)
        {
            yield return instruction;

            // GetName is called only for the row caption. The BindingValue is
            // rendered separately by AppendKeyBinding and remains unchanged.
            if (instruction.operand is MethodInfo method
                && method.Name == "GetName"
                && method.ReturnType == typeof(string)
                && method.GetParameters() is [{ ParameterType: var parameterType }]
                && parameterType == typeof(InputAction))
            {
                yield return new CodeInstruction(OpCodes.Call, lookup);
            }
        }
    }
}

/// <summary>
/// Uses the same caption dictionary in the binding-capture popup without
/// changing the InputAction value or the popup's stable window ID.
/// </summary>
[HarmonyPatch]
public static class KeyAssignmentPopupTitlePatch
{
    public static MethodBase TargetMethod()
        => AccessTools.Constructor(typeof(KeyAssignmentPopup), new[] { typeof(GameSettings), typeof(InputAction) })
            ?? throw new MissingMethodException(typeof(KeyAssignmentPopup).FullName, ".ctor(GameSettings, InputAction)");

    public static string LocalizeTitle(string original)
    {
        string translated = Runtime.ControlText(original);
        return translated == original ? original.ToUpperInvariant() : translated;
    }

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Translate(IEnumerable<CodeInstruction> instructions)
    {
        var localize = AccessTools.Method(typeof(KeyAssignmentPopupTitlePatch), nameof(LocalizeTitle))!;
        bool replaced = false;
        foreach (var instruction in instructions)
        {
            if (!replaced && instruction.operand is MethodInfo method
                && method.DeclaringType == typeof(string)
                && method.Name == nameof(string.ToUpperInvariant)
                && method.GetParameters().Length == 0)
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = localize;
                replaced = true;
            }
            yield return instruction;
        }
    }
}

[HarmonyPatch]
public static class KeyAssignmentPopupPromptPatch
{
    private static readonly HashSet<string> Text = new(StringComparer.Ordinal)
    {
        "INPUT", "PRESS A KEY OR MOUSE BUTTON", "HELD"
    };

    public static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(KeyAssignmentPopup), "OnDrawUi")
            ?? throw new MissingMethodException(typeof(KeyAssignmentPopup).FullName, "OnDrawUi");
        yield return typeof(KeyAssignmentPopup).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(method => method.Name == "DrawModifierReadout"
                && method.GetParameters().Length == 2
                && method.GetParameters()[1].ParameterType == typeof(string));
    }

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Translate(IEnumerable<CodeInstruction> instructions)
    {
        var lookup = AccessTools.Method(typeof(Runtime), nameof(Runtime.ControlText), new[] { typeof(string) })!;
        foreach (var instruction in instructions)
        {
            yield return instruction;
            if (instruction.opcode == OpCodes.Ldstr && instruction.operand is string text && Text.Contains(text))
                yield return new CodeInstruction(OpCodes.Call, lookup);
        }
    }
}
