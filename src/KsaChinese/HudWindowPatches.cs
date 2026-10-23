// Adapted from LiX-Works/KsaUiLanguages (MIT), b504774158cc03b582a0e75bfe94330cb841f11d.
using System.Reflection;
using System.Reflection.Emit;
using Brutal.ImGuiApi;
using HarmonyLib;
using KSA;

namespace KsaChinese;

[HarmonyPatch(typeof(ImGuiWindow), nameof(ImGuiWindow.OnDrawUi))]
public static class HudWindowTitlePatch
{
    public static bool IsKnownWindow(ImGuiWindow window)
        => window.GetType().DeclaringType == typeof(GaugeContextAssignmentWindow)
            || window.GetType().DeclaringType == typeof(LayoutSaves)
            || window.GetType().DeclaringType == typeof(KittenRosterWindow)
            || window.GetType().DeclaringType == typeof(GameSaves)
            || window.GetType().DeclaringType == typeof(VehicleSaves)
            || window.GetType().DeclaringType == typeof(UniverseManifest)
            || window.GetType().DeclaringType == typeof(ResourceGroupsPanel)
            || window.GetType().DeclaringType == typeof(CrewAssignmentWindow)
            || window is GroundTrackWindow or TargetTrackWindow;

    public static string DisplayTitle(string original, ImGuiWindow window)
    {
        var type=window.GetType();
        if(type.Assembly==typeof(KSA.Program).Assembly && (type.Name is "ChuteTuningWindow" or "KittenTuningWindow" or "ProfilerWindow" or "WorkerProfilerWindow"
            || type.FullName is "KSA.GaugeHierarchy+HierarchyWindow" or "KSA.GaugeInspector+InspectorWindow" or "KSA.ThreadProfiler+Window"
                or "KSA.DistantGlintEditor+DebugWindow" or "KSA.ExplosionEditor+DebugWindow"
                or "KSA.ParticleEmitterDebugEditor+DebugWindow" or "KSA.VersionHistory+VersionHistoryWindow"))
        {
            if(Runtime.SelectedLocale=="zh-CN"&&type.Name=="ProfilerWindow"&&original.StartsWith("Profiler    frame ",StringComparison.Ordinal))
                return original.Replace("Profiler    frame ","性能分析    帧 ",StringComparison.Ordinal);
            if(Runtime.SelectedLocale=="zh-CN"&&type.Name=="WorkerProfilerWindow"&&original.StartsWith("CPU Profiler - ",StringComparison.Ordinal))
                return "CPU 性能分析 - "+original[15..];
            return ExpandedUi.Menu(original);
        }
        if (!IsKnownWindow(window)) return original;
        string utility=Runtime.UtilityMenuText(original);
        return utility!=original ? utility : Runtime.HudMenuText(original);
    }
    public static string DisplayPlainTitle(string original,ImGuiWindow window)
    {
        string translated=DisplayTitle(original,window);
        if(translated==original)return original;
        int marker=translated.IndexOf("###",StringComparison.Ordinal);
        return marker<0?translated:translated[..marker];
    }

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Translate(IEnumerable<CodeInstruction> instructions)
    {
        var titleField = AccessTools.Field(typeof(ImGuiWindow), "_windowTitle");
        var plainField = AccessTools.Field(typeof(ImGuiWindow), "_displayTitle");
        var display = AccessTools.Method(typeof(HudWindowTitlePatch), nameof(DisplayTitle));
        foreach (var instruction in instructions)
        {
            yield return instruction;
            if (instruction.opcode == OpCodes.Ldfld && Equals(instruction.operand, titleField))
            {
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Call, display);
            }
            else if(instruction.opcode==OpCodes.Ldfld&&Equals(instruction.operand,plainField))
            {
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(HudWindowTitlePatch),nameof(DisplayPlainTitle)));
            }
        }
    }
}

[HarmonyPatch(typeof(ConsoleStyle), "BeginWindowCore")]
public static class HudConsoleTitlePatch
{
    [HarmonyPrefix]
    public static void Localize(ReadOnlySpan<char> signature, ref ReadOnlySpan<char> title)
    {
        if (signature.SequenceEqual("KSA-LAY") && title.SequenceEqual("LAYOUTS"))
            title = Runtime.HudText(title.ToString()).AsSpan();
        else if(signature.SequenceEqual("KSA-SAV") || signature.SequenceEqual("KSA-VEH")
            || signature.SequenceEqual("KSA-MAN") || signature.SequenceEqual("KSA-RES"))
            title=Runtime.UtilityText(title.ToString()).AsSpan();
        else if(signature.SequenceEqual("KSA-VER"))
            title=ExpandedUi.Text(title.ToString()).AsSpan();
    }
}

[HarmonyPatch]
public static class HudWindowViewMenuPatch
{
    private static readonly HashSet<string> Labels = new(StringComparer.Ordinal)
    {
        "View", "Always on Top", "Detached", "Autohide"
    };

    public static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(ImGuiWindow), nameof(ImGuiWindow.DrawMenuBar));
        yield return AccessTools.Method(typeof(ImGuiWindow), nameof(ImGuiWindow.DrawViewMenu));
    }

    public static ImString DisplayCaption(ImString caption, ImGuiWindow window)
        => HudWindowTitlePatch.IsKnownWindow(window) && Labels.Contains(caption.ToString())
            ? Runtime.ImText(Runtime.HudMenuText(caption.ToString())) : caption;

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Translate(IEnumerable<CodeInstruction> instructions)
    {
        var display = AccessTools.Method(typeof(HudWindowViewMenuPatch), nameof(DisplayCaption));
        foreach (var instruction in instructions)
        {
            yield return instruction;
            if (instruction.opcode == OpCodes.Call && instruction.operand is MethodInfo method
                && method.DeclaringType == typeof(ImString) && method.Name == "op_Implicit"
                && method.GetParameters()[0].ParameterType == typeof(ReadOnlySpan<byte>))
            {
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Call, display);
            }
        }
    }
}

[HarmonyPatch(typeof(StringInputPopup), "OnDrawUi")]
public static class HudSavePopupScopePatch
{
    [ThreadStatic] public static bool IsLayout;
    [ThreadStatic] public static bool IsUtility;
    private static readonly FieldInfo Title = AccessTools.Field(typeof(StringInputPopup), "_title");

    [HarmonyPrefix]
    public static void Enter(StringInputPopup __instance, out (bool Layout, bool Utility) __state)
    {
        __state = (IsLayout, IsUtility);
        IsLayout = Equals(Title.GetValue(__instance), "SAVE LAYOUT");
        IsUtility = Title.GetValue(__instance) is "SAVE GAME" or "SAVE VEHICLE";
    }

    [HarmonyFinalizer]
    public static void Leave((bool Layout, bool Utility) __state) { IsLayout = __state.Layout; IsUtility = __state.Utility; }
}

[HarmonyPatch]
public static class HudSavePopupCaptionPatch
{
    private static readonly HashSet<string> Prompts = new(StringComparer.Ordinal)
    {
        "Please enter a filename to continue.", "A name needs at least one letter or number.", "Will be saved as \""
    };

    public static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(StringInputPopup), "OnDrawUi");
        yield return AccessTools.Method(typeof(StringInputPopup), "DrawSanitizedHint");
    }

    public static string DisplayTitle(string original) => original == "SAVE LAYOUT" ? Runtime.HudText(original)
        : original is "SAVE GAME" or "SAVE VEHICLE" ? Runtime.UtilityText(original) : original;
    public static string DisplayPrompt(string original) => HudSavePopupScopePatch.IsLayout ? Runtime.HudText(original)
        : HudSavePopupScopePatch.IsUtility ? Runtime.UtilityText(original) : original;

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Translate(IEnumerable<CodeInstruction> instructions)
    {
        var titleField = AccessTools.Field(typeof(StringInputPopup), "_title");
        var title = AccessTools.Method(typeof(HudSavePopupCaptionPatch), nameof(DisplayTitle));
        var prompt = AccessTools.Method(typeof(HudSavePopupCaptionPatch), nameof(DisplayPrompt));
        foreach (var instruction in instructions)
        {
            yield return instruction;
            if (instruction.opcode == OpCodes.Ldfld && Equals(instruction.operand, titleField))
                yield return new CodeInstruction(OpCodes.Call, title);
            else if (instruction.opcode == OpCodes.Ldstr && instruction.operand is string text && Prompts.Contains(text))
                yield return new CodeInstruction(OpCodes.Call, prompt);
        }
    }
}

[HarmonyPatch(typeof(ConfirmActionPopup), "OnDrawUi")]
public static class HudConfirmPopupCaptionPatch
{
    private static readonly FieldInfo Title = AccessTools.Field(typeof(ConfirmActionPopup), "_title");
    private const string OverwritePrefix = "Are you sure you want to overwrite layout '";
    private const string DeletePrefix = "Are you sure you want to delete layout '";

    public static string DisplayTitle(string original)
        => original is "OVERWRITE LAYOUT" or "DELETE LAYOUT" ? Runtime.HudText(original) : original;

    public static string DisplayMessage(string original, string title)
    {
        string? prefix = title == "OVERWRITE LAYOUT" ? OverwritePrefix : title == "DELETE LAYOUT" ? DeletePrefix : null;
        if (prefix is null || !original.StartsWith(prefix, StringComparison.Ordinal) || !original.EndsWith("'?", StringComparison.Ordinal))
            return original;
        string name = original[prefix.Length..^2];
        string template = prefix + "{0}'?";
        return Runtime.HudText(template).Replace("{0}", name, StringComparison.Ordinal);
    }

    public static string ReadMessage(string original, ConfirmActionPopup popup)
        => DisplayMessage(original, (string)Title.GetValue(popup)!);

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Translate(IEnumerable<CodeInstruction> instructions)
    {
        var messageField = AccessTools.Field(typeof(ConfirmActionPopup), "_message");
        var title = AccessTools.Method(typeof(HudConfirmPopupCaptionPatch), nameof(DisplayTitle));
        var message = AccessTools.Method(typeof(HudConfirmPopupCaptionPatch), nameof(ReadMessage));
        foreach (var instruction in instructions)
        {
            yield return instruction;
            if (instruction.opcode == OpCodes.Ldfld && Equals(instruction.operand, Title))
                yield return new CodeInstruction(OpCodes.Call, title);
            else if (instruction.opcode == OpCodes.Ldfld && Equals(instruction.operand, messageField))
            {
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Call, message);
            }
        }
    }
}
