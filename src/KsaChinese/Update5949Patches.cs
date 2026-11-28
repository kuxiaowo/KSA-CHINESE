using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using Brutal.ImGuiApi;
using HarmonyLib;
using KSA;

namespace KsaChinese;

[HarmonyPatch(typeof(UncompressedSave), "DrawSystemCell")]
public static class SaveSystemCaptionPatch
{
    public static string DisplayName(string id)
        => SystemLibrary.Find(id) is { } system ? Runtime.StartupText(system.GetName()) : id;

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Translate(IEnumerable<CodeInstruction> instructions)
    {
        var original = AccessTools.Method(typeof(SaveSystemCheck), nameof(SaveSystemCheck.DisplayName));
        var localized = AccessTools.Method(typeof(SaveSystemCaptionPatch), nameof(DisplayName));
        return instructions.MethodReplacer(original, localized);
    }
}

[HarmonyPatch]
public static class DestinationValuePatch
{
    public static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(DestinationRegistryWindow), "DrawRegistry");
        yield return AccessTools.Method(typeof(DestinationRegistryWindow), "DrawTable");
        yield return AccessTools.Method(typeof(DestinationRegistryWindow), "DrawDestinationVisits");
    }

    public static string SituationNames(string source)
        => string.Join(", ", source.Split(", ", StringSplitOptions.None)
            .Select(name => Runtime.CurrentPack.Display.GetValueOrDefault("DestinationSituations." + name, name)));

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Translate(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        var source = AccessTools.Method(typeof(DestinationRegistryWindow), "SourceName");
        var shape = AccessTools.Method(typeof(DestinationRegistryWindow), "ShapeName");
        var adapter = AccessTools.Method(typeof(ExpandedUi), __originalMethod.Name == "DrawRegistry"
            ? nameof(ExpandedUi.Utf8Menu) : nameof(ExpandedUi.Utf8Plain));
        var situations = AccessTools.Method(typeof(DestinationValuePatch), nameof(SituationNames));
        bool situationReceiver = false;
        foreach (var instruction in instructions)
        {
            yield return instruction;
            if (instruction.Calls(source) || instruction.Calls(shape))
                yield return new CodeInstruction(OpCodes.Call, adapter);

            // The enum's formatting call is constrained to DestinationSituations.
            // The destination, vehicle and crew strings in the same tables stay exact.
            if (situationReceiver && instruction.operand is MethodInfo { Name: nameof(ToString) })
                yield return new CodeInstruction(OpCodes.Call, situations);
            situationReceiver = (instruction.opcode == OpCodes.Constrained || instruction.opcode == OpCodes.Box)
                && Equals(instruction.operand, typeof(DestinationSituations));
        }
    }
}

[HarmonyPatch]
public static class EngineCycleCaptionPatch
{
    public static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(EngineDesigner), "DrawEngineDesignDebugWindow");
        yield return AccessTools.Method(typeof(EngineDesigner), "DrawCycleOption");
    }

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Translate(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        var cycleName = AccessTools.Method(typeof(EngineCycleExtensions), nameof(EngineCycleExtensions.GetName));
        var adapter = AccessTools.Method(typeof(ExpandedUi), __originalMethod.Name == "DrawCycleOption"
            ? nameof(ExpandedUi.MenuCaption) : nameof(ExpandedUi.PlainCaption));
        foreach (var instruction in instructions)
        {
            yield return instruction;
            if (instruction.Calls(cycleName))
                yield return new CodeInstruction(OpCodes.Call, adapter);
        }
    }
}


[HarmonyPatch(typeof(SaveSystemRestart), nameof(SaveSystemRestart.RestartInto))]
public static class StarMapRestartPatch
{
    public static void Configure(ProcessStartInfo start)
    {
        string name = Path.GetFileNameWithoutExtension(start.FileName);
        string? executable = string.Equals(name, "StarMap", StringComparison.OrdinalIgnoreCase) ? start.FileName
            : string.Equals(name, "dotnet", StringComparison.OrdinalIgnoreCase) && start.ArgumentList.Count > 0
                && string.Equals(Path.GetFileName(start.ArgumentList[0]), "StarMap.dll", StringComparison.OrdinalIgnoreCase)
                ? start.ArgumentList[0] : null;
        if (executable is null || !Path.IsPathFullyQualified(executable)) return;

        // StarMap 0.5.4 enters the game directory before loading the mod.
        // Its next process must resolve the config and loader files from StarMap's directory.
        string directory = Path.GetDirectoryName(executable)!;
        if (File.Exists(Path.Combine(directory, "StarMapConfig.json")))
            start.WorkingDirectory = directory;
    }

    public static Process? Start(ProcessStartInfo start)
    {
        Configure(start);
        return Process.Start(start);
    }

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Translate(IEnumerable<CodeInstruction> instructions)
    {
        var start = AccessTools.Method(typeof(Process), nameof(Process.Start), new[] { typeof(ProcessStartInfo) });
        var adapter = AccessTools.Method(typeof(StarMapRestartPatch), nameof(Start));
        int replacements = 0;
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(start))
            {
                instruction.operand = adapter;
                replacements++;
            }
            yield return instruction;
        }
        if (replacements != 1) throw new InvalidOperationException("KSA restart process entry point changed.");
    }
}
