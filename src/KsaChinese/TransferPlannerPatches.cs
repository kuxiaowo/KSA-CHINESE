// Adapted from LiX-Works/KsaUiLanguages (MIT), b504774158cc03b582a0e75bfe94330cb841f11d.
using System.Reflection;
using System.Reflection.Emit;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using HarmonyLib;
using KSA;

namespace KsaChinese;

// Only the inspected 5602 transfer planner's rendering methods are rewritten. In particular,
// TransferType.GetKey(), entity names, widget IDs, numeric formats and worker code
// never pass through a translation lookup.
[HarmonyPatch]
public static class TransferPlannerCaptionPatch
{
    public static readonly string[] RenderMethods =
    {
        "DrawPlanWindow", "DrawInterstellarBurnsSummary", "DrawTransferSummary",
        "DrawInterstellarPlan", "DrawInterstellarBurnFuel", "DrawPropellantShortfalls",
        "DrawTargetPeriapsisRow", "DrawInterstellarCorrection", "DrawDeltaVShortfall",
        "DrawCalculatingSegmentBars", "DrawPorkChopToolTip", "DrawCorrectionTransfer",
        "DrawSelectedTransferFlightPlan"
    };

    public static readonly HashSet<string> PlainCaptions = new(StringComparer.Ordinal)
    {
        "TRANSFER PLANNING", "PLAN", "PLAN TYPE", "SOURCE", "DESTINATION", "NONE AVAILABLE",
        "TRANSFER WINDOW", "TIME OF FLIGHT", "TIME UNIT", "MINIMUM TIME OF FLIGHT", "THREADS",
        "MAXIMUM TIME OF FLIGHT", "SET TARGET", "SHOW PARENT/TARGET ALIGNMENT",
        "MUST HAVE A VALID ORBIT", "NO SOURCE VEHICLE", "NO VALID APSIS ON THIS ORBIT",
        "PREVIEW SELECTED TRANSFER", "PREVIEW SELECTED TRANSFER FLIGHT PLAN", "PREVIEW LAMBERT TRANSFER",
        "BEST TRANSFER", "SELECT BEST DV", "SELECT BEST DV + INTERCEPT VEL", "SELECTED TRANSFER",
        "NO SOLUTION FOUND", "DEPARTURE × FLIGHT TIME - DV", "CALCULATING…", "TRANSFER SELECTED",
        "NO SELECTION", "SPEED UP BURN IN", "SLOW DOWN BURN IN", "ARRIVES IN", "ARRIVAL PERIAPSIS",
        "PLANNED SPEED UP BURN", "PLANNED SLOW DOWN BURN", "DEPART", "TRANSIT", "INTERCEPT VELOCITY",
        "CLOSEST APPROACH", "NONE", "NO STAR NEARBY", "NO OTHER STAR", "DEPARTING", "CROSSING",
        "DISTANCE", "NO ACTIVE ANTIMATTER DRIVE", "MAX SPEED", "SPEED UP BURN ANTIMATTER",
        "SLOW DOWN BURN ANTIMATTER", "FINAL SPEED", "CIRCULAR SPEED AT PERIAPSIS", "SPEED UP BURN",
        "COAST", "COAST TIME", "HYDROGEN SCOOPED", "SLOW DOWN BURN",
        "SLOW DOWN BURN RUNS OUT OF ANTIMATTER", "NOT ENOUGH ROOM LEFT TO SLOW DOWN", "JOURNEY",
        "MUST DEPART FROM A BODY ORBITING THE STAR", "NO DEPARTURE FOUND AT THIS SPEED",
        "THE SPEED UP BURN GOES NOWHERE", "BURN TIME", "ENDS AT", "ANTIMATTER USED", "HYDROGEN USED",
        "HALFWAY", "VESSEL BURNED AWAY", "TARGET SPEED", "SPEED AFTER", "ANTIMATTER", "HYDROGEN",
        "NOT ENOUGH ", " SHORT", "TARGET PERIAPSIS", "APPROACH CORRECTION", "ARRIVING",
        "NO CORRECTION FOUND", "NOT ENOUGH DELTA-V: ", " m/s SHORT", "TRANSFER POINT", "TIME",
        "REL VELOCITY", "CLOSEST", "CALCULATING CORRECTION…", "PERIAPSIS", "LEAVING",
        "AFTER SLOWING DOWN"
    };

    public static readonly HashSet<string> ButtonCaptions = new(StringComparer.Ordinal)
    {
        "CALCULATE", "RE-CALCULATE", "CREATE", "CORRECTION"
    };

    public static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (string name in RenderMethods)
            yield return AccessTools.Method(typeof(TransferPlanner), name)
                ?? throw new MissingMethodException(typeof(TransferPlanner).FullName, name);
    }

    public static string PlainText(string source)
        => PlainCaptions.Contains(source) ? Runtime.PlanningText(source) : source;

    public static string ButtonText(string source)
        => ButtonCaptions.Contains(source) ? Runtime.PlanningText(source) : source;

    public static bool Button(ReadOnlySpan<char> label)
    {
        string source = label.ToString();
        // ConsoleWidgets draws the caption separately from its InvisibleButton ID.
        // A ### suffix in its AddText caption would be visibly printed, so retain
        // the original ID through the explicit native overload instead.
        return ConsoleWidgets.Button(ButtonText(source).AsSpan(), label, float2.Zero);
    }

    public static bool PrimaryButton(ReadOnlySpan<char> label)
    {
        string source = label.ToString();
        return ConsoleWidgets.PrimaryButton(ButtonText(source).AsSpan(), label, float2.Zero);
    }

    public static float ButtonWidth(ReadOnlySpan<char> label)
        => ConsoleWidgets.ButtonWidth(ButtonText(label.ToString()).AsSpan());

    public static string TimeUnitText(string source)
        => TransferPlannerComboPatch.TimeNames.Contains(source) ? Runtime.PlanningText(source) : source;

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Translate(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        int captions = 0, buttons = 0, units = 0;
        var plain = AccessTools.Method(typeof(TransferPlannerCaptionPatch), nameof(PlainText));
        var time = AccessTools.Method(typeof(TransferPlannerCaptionPatch), nameof(TimeUnitText));
        foreach (var instruction in instructions)
        {
            if (instruction.operand is MethodInfo native && native.DeclaringType == typeof(ConsoleWidgets)
                && native.GetParameters().Length == 1
                && native.GetParameters()[0].ParameterType == typeof(ReadOnlySpan<char>)
                && native.Name is "Button" or "PrimaryButton" or "ButtonWidth")
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = AccessTools.Method(typeof(TransferPlannerCaptionPatch), native.Name);
                buttons++;
            }
            yield return instruction;
            if (instruction.opcode == OpCodes.Ldstr && instruction.operand is string text && PlainCaptions.Contains(text))
            {
                // This whitelist excludes Hohmann/Interstellar/Circularize keys,
                // N/A comparisons and every internal control identifier.
                yield return new CodeInstruction(OpCodes.Call, plain);
                captions++;
            }
            else if (instruction.operand is MethodInfo unitName && unitName.DeclaringType == typeof(TimeObject)
                && unitName.Name == nameof(TimeObject.GetName) && unitName.ReturnType == typeof(string))
            {
                // Only the slider's visible "%u " + unit-name format fragment.
                // TimeObject.Name/GetKey and numeric multipliers remain native.
                yield return new CodeInstruction(OpCodes.Call, time);
                units++;
            }
        }
        if (captions + buttons + units == 0)
            throw new InvalidOperationException($"No transfer display call sites matched {__originalMethod.Name} on KSA 5602.");
        Runtime.Log($"Transfer display: {__originalMethod.Name}: {captions} captions, {buttons} button calls, {units} unit fragments.");
    }
}

[HarmonyPatch]
public static class TransferPlannerComboPatch
{
    public static readonly HashSet<string> PlanNames = new(StringComparer.Ordinal)
    {
        "Hohmann", "Circularize Apoapsis", "Circularize Periapsis", "Interstellar"
    };
    public static readonly HashSet<string> TimeNames = new(StringComparer.Ordinal)
    {
        "Seconds", "Minutes", "Hours", "Days", "Weeks", "Years"
    };

    public static IEnumerable<MethodBase> TargetMethods()
    {
        var definition = typeof(TransferPlanner).GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .Single(method => method.Name == "DrawComboControl" && method.IsGenericMethodDefinition);
        // Both are value types, with distinct closed native rendering bodies.
        // TransferObject (source/destination entities) is deliberately excluded.
        yield return definition.MakeGenericMethod(typeof(TransferType));
        yield return definition.MakeGenericMethod(typeof(TimeObject));
    }

    public static string PreviewText(string source, bool time)
        => (time ? TimeNames : PlanNames).Contains(source) || source == "N/A"
            ? Runtime.PlanningText(source) : source;

    public static string OptionText(string source, bool time)
        => (time ? TimeNames : PlanNames).Contains(source)
            ? Runtime.PlanningMenuText(source) : source;

    public static bool PlanCombo(ImString id, ImString preview, ImGuiComboFlags flags)
        => ImGui.BeginCombo(id, Runtime.ImText(PreviewText(preview.ToString(), false)), flags);

    public static bool TimeCombo(ImString id, ImString preview, ImGuiComboFlags flags)
        => ImGui.BeginCombo(id, Runtime.ImText(PreviewText(preview.ToString(), true)), flags);

    public static bool PlanOption(ImString label, bool selected, ImGuiSelectableFlags flags, in float2? size)
        => ImGui.Selectable(Runtime.ImText(OptionText(label.ToString(), false)), selected, flags, in size);

    public static bool TimeOption(ImString label, bool selected, ImGuiSelectableFlags flags, in float2? size)
        => ImGui.Selectable(Runtime.ImText(OptionText(label.ToString(), true)), selected, flags, in size);

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Translate(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        bool time = __originalMethod.GetGenericArguments()[0] == typeof(TimeObject);
        int previews = 0, options = 0;
        foreach (var instruction in instructions)
        {
            if (instruction.operand is MethodInfo native && native.DeclaringType == typeof(ImGui))
            {
                string? wrapper = null;
                if (native.Name == nameof(ImGui.BeginCombo) && native.GetParameters().Length == 3
                    && native.GetParameters()[0].ParameterType == typeof(ImString))
                {
                    wrapper = time ? nameof(TimeCombo) : nameof(PlanCombo);
                    previews++;
                }
                else if (native.Name == nameof(ImGui.Selectable) && native.GetParameters().Length == 4
                    && native.GetParameters()[0].ParameterType == typeof(ImString)
                    && native.GetParameters()[1].ParameterType == typeof(bool))
                {
                    wrapper = time ? nameof(TimeOption) : nameof(PlanOption);
                    options++;
                }
                if (wrapper is not null)
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = AccessTools.Method(typeof(TransferPlannerComboPatch), wrapper);
                }
            }
            yield return instruction;
        }
        if (previews != 1 || options != 1)
            throw new InvalidOperationException($"Transfer combo call sites changed: {__originalMethod}: {previews} previews, {options} options.");
        Runtime.Log($"Transfer combo: {__originalMethod}: preview and options matched.");
    }
}
