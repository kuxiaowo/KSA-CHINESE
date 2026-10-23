// Adapted from LiX-Works/KsaUiLanguages (MIT), b504774158cc03b582a0e75bfe94330cb841f11d.
using System.Reflection;
using System.Reflection.Emit;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using HarmonyLib;
using KSA;

namespace KsaChinese;

/// <summary>
/// Covers the common crew assignment window without localizing part display
/// names, crew names, seat template IDs, or any input binding values.
/// </summary>
[HarmonyPatch]
public static class CrewAssignmentCaptionPatch
{
    private static readonly HashSet<string> StatusLabels = new(StringComparer.Ordinal)
    {
        "No vehicle selected in the editor.",
        "This vehicle has no seats."
    };

    private static Type WindowType => typeof(CrewAssignmentWindow).GetNestedType(
        "Window", BindingFlags.NonPublic)
        ?? throw new MissingMemberException(typeof(CrewAssignmentWindow).FullName, "Window");

    public static MethodBase TargetMethod()
        => AccessTools.Method(WindowType, "DrawContent", new[] { typeof(IViewport) })
            ?? throw new MissingMethodException(WindowType.FullName, "DrawContent(IViewport)");

    public static ImString StatusCaption(ImString source)
    {
        string original = source.ToString();
        return StatusLabels.Contains(original)
            ? Runtime.ImText(Runtime.UtilityText(original))
            : source;
    }

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Translate(IEnumerable<CodeInstruction> instructions)
    {
        var caption = AccessTools.Method(typeof(CrewAssignmentCaptionPatch), nameof(StatusCaption))!;
        int sites = 0;
        foreach (var instruction in instructions)
        {
            yield return instruction;
            if (instruction.opcode == OpCodes.Call && instruction.operand is MethodInfo method
                && method.DeclaringType == typeof(ImString) && method.Name == "op_Implicit"
                && method.GetParameters() is [{ ParameterType: var sourceType }]
                && sourceType == typeof(ReadOnlySpan<byte>))
            {
                yield return new CodeInstruction(OpCodes.Call, caption);
                sites++;
            }
        }
        if (sites != 2)
            throw new InvalidOperationException($"Crew assignment UTF-8 caption sites changed: expected 2, found {sites}.");
    }
}

/// <summary>
/// The generic window title hook intentionally scopes itself to known windows.
/// Extend it here for Crew Assignment while keeping its existing UI key and ID.
/// </summary>
[HarmonyPatch(typeof(ImGuiWindow), nameof(ImGuiWindow.OnDrawUi))]
public static class CrewAssignmentWindowTitlePatch
{
    private static readonly FieldInfo WindowTitle = AccessTools.Field(typeof(ImGuiWindow), "_windowTitle")
        ?? throw new MissingFieldException(typeof(ImGuiWindow).FullName, "_windowTitle");

    public static string DisplayTitle(string original, ImGuiWindow window)
        => window.GetType().DeclaringType == typeof(CrewAssignmentWindow)
            ? Runtime.MenuText(original)
            : original;

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Translate(IEnumerable<CodeInstruction> instructions)
    {
        var display = AccessTools.Method(typeof(CrewAssignmentWindowTitlePatch), nameof(DisplayTitle))!;
        foreach (var instruction in instructions)
        {
            yield return instruction;
            if (instruction.opcode == OpCodes.Ldfld && Equals(instruction.operand, WindowTitle))
            {
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Call, display);
            }
        }
    }
}

/// <summary>
/// Localizes the face-camera bone combo's display captions. Selectable options
/// keep their original ImGui IDs with ###, and the enum values remain untouched.
/// </summary>
[HarmonyPatch]
public static class CrewPortraitBoneCaptionPatch
{
    public static MethodBase TargetMethod()
        => AccessTools.Method(typeof(CrewPortraitPanel), "DrawCameraControls")
            ?? throw new MissingMethodException(typeof(CrewPortraitPanel).FullName,
                "DrawCameraControls");

    public static ImString LocalizeBoneCaption(ImString source, bool selectable)
    {
        string original = source.ToString();
        string translated = selectable ? Runtime.UtilityMenuText(original) : Runtime.UtilityText(original);
        if (translated == original) return source;
        return Runtime.ImText(translated);
    }

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Translate(IEnumerable<CodeInstruction> instructions)
    {
        var code = instructions.ToList();
        var display = AccessTools.Method(typeof(CrewPortraitBoneCaptionPatch), nameof(LocalizeBoneCaption))!;
        int sites = 0;
        for (int i = 0; i < code.Count; i++)
        {
            yield return code[i];
            if (code[i].opcode != OpCodes.Call || code[i].operand is not MethodInfo method
                || method.DeclaringType != typeof(CrewPortraitPanel) || method.Name != "GetBoneImString")
                continue;

            string? nextControl = null;
            for (int j = i + 1; j < Math.Min(i + 64, code.Count); j++)
            {
                if (code[j].operand is MethodInfo next && next.DeclaringType == typeof(ImGui)
                    && (next.Name == nameof(ImGui.BeginCombo) || next.Name == nameof(ImGui.Selectable)))
                {
                    nextControl = next.Name;
                    break;
                }
            }

            if (nextControl is null)
                throw new InvalidOperationException("Crew portrait bone caption is no longer followed by its combo/selectable control.");
            yield return new CodeInstruction(nextControl == nameof(ImGui.Selectable) ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
            yield return new CodeInstruction(OpCodes.Call, display);
            sites++;
        }
        if (sites != 2)
            throw new InvalidOperationException($"Crew portrait bone caption sites changed: expected 2, found {sites}.");
    }
}

/// <summary>
/// Changes only the action words in the EVA ladder prompt. The bracketed
/// Input.GetAssignmentString value continues to come from the game's binding.
/// </summary>
[HarmonyPatch]
public static class KittenEvaActionCaptionPatch
{
    private static readonly HashSet<string> ActionSuffixes = new(StringComparer.Ordinal)
    {
        "] Let go", "] Grab", "] Board"
    };

    public static MethodBase TargetMethod()
        => AccessTools.Method(typeof(KittenEva), "DrawLadderPrompt", new[] { typeof(IViewport) })
            ?? throw new MissingMethodException(typeof(KittenEva).FullName, "DrawLadderPrompt(IViewport)");

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Translate(IEnumerable<CodeInstruction> instructions)
    {
        var lookup = AccessTools.Method(typeof(Runtime), nameof(Runtime.UtilityText), new[] { typeof(string) })!;
        int sites = 0;
        foreach (var instruction in instructions)
        {
            yield return instruction;
            if (instruction.opcode == OpCodes.Ldstr && instruction.operand is string suffix
                && ActionSuffixes.Contains(suffix))
            {
                yield return new CodeInstruction(OpCodes.Call, lookup);
                sites++;
            }
        }
        if (sites != ActionSuffixes.Count)
            throw new InvalidOperationException($"EVA action prompt sites changed: expected {ActionSuffixes.Count}, found {sites}.");
    }
}

/// <summary>
/// The celestial hover tooltip prepends an object ID at runtime. Translate only
/// its fixed instruction suffix so celestial and vehicle names remain original.
/// </summary>
[HarmonyPatch]
public static class OrbiterHoverTooltipPatch
{
    private const string Instructions = "\nDouble click to focus camera\nShift + Click to set/unset target";

    public static MethodBase TargetMethod()
        => AccessTools.Method(typeof(IOrbiter), "OnDrawUi",
            new[] { typeof(IOrbiter), typeof(IGameViewport), typeof(string), typeof(ImGuiWindowFlags) })
            ?? throw new MissingMethodException(typeof(IOrbiter).FullName,
                "OnDrawUi(IOrbiter, IGameViewport, string, ImGuiWindowFlags)");

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Translate(IEnumerable<CodeInstruction> instructions)
    {
        var lookup = AccessTools.Method(typeof(Runtime), nameof(Runtime.TooltipText), new[] { typeof(string) })!;
        int sites = 0;
        foreach (var instruction in instructions)
        {
            yield return instruction;
            if (instruction.opcode == OpCodes.Ldstr && Equals(instruction.operand, Instructions))
            {
                yield return new CodeInstruction(OpCodes.Call, lookup);
                sites++;
            }
        }
        if (sites != 1)
            throw new InvalidOperationException($"Orbiter hover tooltip suffix changed: expected 1, found {sites}.");
    }
}

/// <summary>
/// Localizes the three fixed galactic-grid captions while leaving longitude
/// ticks and astronomical object names intact.
/// </summary>
[HarmonyPatch]
public static class GalacticGridCaptionPatch
{
    private static readonly HashSet<string> Labels = new(StringComparer.Ordinal)
    {
        "galactic centre", "north galactic pole", "south galactic pole"
    };

    public static MethodBase TargetMethod()
        => AccessTools.Method(typeof(Universe), "DrawGalacticGrid", new[] { typeof(IGameViewport) })
            ?? throw new MissingMethodException(typeof(Universe).FullName, "DrawGalacticGrid(IGameViewport)");

    public static ImString Caption(ImString source)
        => Labels.Contains(source.ToString()) ? Runtime.ImText(Runtime.UtilityText(source.ToString())) : source;

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Translate(IEnumerable<CodeInstruction> instructions)
    {
        var lookup = AccessTools.Method(typeof(GalacticGridCaptionPatch), nameof(Caption))!;
        int sites = 0;
        foreach (var instruction in instructions)
        {
            yield return instruction;
            if (instruction.operand is MethodInfo conversion && conversion.DeclaringType == typeof(ImString)
                && conversion.Name == "op_Implicit" && conversion.GetParameters() is [{ ParameterType: var source }]
                && source == typeof(ReadOnlySpan<byte>))
            {
                yield return new CodeInstruction(OpCodes.Call, lookup);
                sites++;
            }
        }
        if (sites != Labels.Count)
            throw new InvalidOperationException($"Galactic grid caption sites changed: expected {Labels.Count}, found {sites}.");
    }
}
