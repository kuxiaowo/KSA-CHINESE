// Adapted from LiX-Works/KsaUiLanguages (MIT), b504774158cc03b582a0e75bfe94330cb841f11d.
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using HarmonyLib;
using KSA;

namespace KsaChinese;

[HarmonyPatch]
public static class FlightPlanUiScopePatch
{
    [ThreadStatic] private static int _depth;

    internal static bool Active => _depth > 0;

    public static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(PatchedConic), nameof(PatchedConic.DrawPatchInfo))
            ?? throw new MissingMethodException(typeof(PatchedConic).FullName, nameof(PatchedConic.DrawPatchInfo));
    }

    [HarmonyPrefix]
    public static void Enter(out int __state)
    {
        __state = _depth;
        _depth++;
    }

    [HarmonyFinalizer]
    public static Exception? Leave(Exception? __exception, int __state)
    {
        _depth = __state;
        return __exception;
    }
}

public static class FlightPlanCheckboxCaptionPatch
{
    public static ImString DisplayCaption(ImString label)
        => FlightPlanUiScopePatch.Active
            ? Runtime.ImText(Runtime.PlanningText(label.ToString()))
            : label;
}

[HarmonyPatch]
public static class FlightPlanCallsitePatch
{
    private static readonly HashSet<string> BurnActionTypes = new(StringComparer.Ordinal)
    {
        "Manual", "Circularize", "Match Target Plane", "Prograde", "Normal", "Outward"
    };

    private static readonly HashSet<string> TimeValueLabels = new(StringComparer.Ordinal)
    {
        "ALL PLANS EXPIRE", "ALL PLANS UPDATE IN", "MAIN FPL EXPIRES",
        "StartTime", "EndTime", "Orbital Period", "Time SOI Encountered",
        "Time To Encounter", "Time To Closest Approach", "Time Of Approach", "Time To",
        "Burn FlightPlan Expires at:", "Game Time"
    };

    private static readonly Regex NumericTimeValue = new(
        @"^(?<leading>\s*)(?<number>[+-]?(?:(?:\d{1,3}(?:,\d{3})+)|\d+)(?:\.\d+)?)(?<separator>\s+)(?<unit>seconds?|minutes?|hours?|days?|weeks?|years?)(?<trailing>\s*)$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static MethodBase Method(Type type, string name)
        => AccessTools.Method(type, name) ?? throw new MissingMethodException(type.FullName, name);

    public static IEnumerable<MethodBase> TargetMethods()
    {
        yield return Method(typeof(Vehicle), nameof(Vehicle.DrawPatchWindow));
        yield return Method(typeof(FlightPlan), nameof(FlightPlan.DrawPatchInfo));
        yield return Method(typeof(PatchedConic), nameof(PatchedConic.DrawPatchInfo));
        yield return Method(typeof(Encounter), nameof(Encounter.DrawEncounterInfo));
        yield return Method(typeof(Burn), nameof(Burn.DrawBurnPatchWindow));
        yield return Method(typeof(Burn), nameof(Burn.DrawBurnInfo));
        yield return Method(typeof(Burn), nameof(Burn.DrawBurnEditorWindowContent));
        yield return Method(typeof(BurnContextMenu), "Draw");
        yield return Method(typeof(BurnContextMenu), "DrawParentDepartureAction");
        yield return Method(typeof(BurnContextMenu), "DrawBurnActions");
        yield return Method(typeof(BurnContextMenu), "BurnActionMenuItem");
    }

    public static string PlanningLiteral(string text)
        => text.Contains("##", StringComparison.Ordinal)
            ? Runtime.PlanningMenuText(text)
            : Runtime.PlanningText(text);

    public static ImString PlanningTextElement(ImString text)
        => Runtime.ImText(Runtime.PlanningText(text.ToString()));

    public static ImString PlanningMenuElement(ImString text)
        => Runtime.ImText(Runtime.PlanningMenuText(text.ToString()));

    public static bool PlanningCollapsingHeader(ImString label, ImGuiTreeNodeFlags flags)
    {
        return ImGui.CollapsingHeader(Runtime.ImText(PlanningCollapsingHeaderText(label.ToString())), flags);
    }

    public static string PlanningCollapsingHeaderText(string original)
    {
        int stable = original.IndexOf("###", StringComparison.Ordinal);
        if (stable < 0) return Runtime.PlanningMenuText(original);

        string visible = original[..stable];
        const string patchPrefix = "Patch '";
        if (visible.StartsWith(patchPrefix, StringComparison.Ordinal))
        {
            string translatedVisible = Runtime.PlanningText(patchPrefix) + visible[patchPrefix.Length..];
            // Keep the original complete label as a canonical hidden suffix in both
            // locales; the source's existing ### hash/index tail remains intact.
            return translatedVisible + "###" + original;
        }

        const string encounterPrefix = "Closest Approach to ";
        if (visible.StartsWith(encounterPrefix, StringComparison.Ordinal))
        {
            string translatedVisible = Runtime.PlanningText(encounterPrefix) + visible[encounterPrefix.Length..];
            // The body name and time-derived original suffix are dynamic identifiers.
            return translatedVisible + "###" + original;
        }

        return Runtime.PlanningMenuText(original);
    }

    public static void PlanningReadout(ReadOnlySpan<char> key, ReadOnlySpan<char> value)
    {
        string originalKey = key.ToString();
        string originalValue = value.ToString();
        string translatedValue = originalValue == "Never"
            ? Runtime.PlanningText("Never")
            : TimeValueLabels.Contains(originalKey) ? PlanningTimeValue(originalValue) : originalValue;
        ConsoleWidgets.Readout(Runtime.PlanningText(originalKey).AsSpan(), translatedValue.AsSpan());
    }

    public static string PlanningTimeValue(string original)
    {
        Match match = NumericTimeValue.Match(original);
        if (!match.Success) return original;

        string unit = match.Groups["unit"].Value.ToLowerInvariant();
        string localizedUnit = Runtime.PlanningText(unit);
        if (localizedUnit == unit) return original;
        return match.Groups["leading"].Value + match.Groups["number"].Value
            + match.Groups["separator"].Value + localizedUnit + match.Groups["trailing"].Value;
    }

    public static void PlanningTimeText(ReadOnlySpan<char> value)
        => ImGui.Text(Runtime.ImText(PlanningTimeValue(value.ToString())));

    public static bool PlanningBeginFlightPlanWindow(ReadOnlySpan<char> id, ReadOnlySpan<char> title,
        ReadOnlySpan<char> signature, ReadOnlySpan<char> contextTag, ref bool open, float2 initialSize,
        ImGuiWindowFlags extraFlags, ImGuiCond sizeCond, bool pinToMainViewport)
    {
        string translatedTitle = Runtime.PlanningText(title.ToString());
        return ConsoleStyle.BeginWindow(id, translatedTitle.AsSpan(), signature, contextTag, ref open,
            initialSize, extraFlags, sizeCond, pinToMainViewport);
    }

    public static void PlanningText(ImString text)
        => ImGui.Text(Runtime.ImText(Runtime.PlanningText(text.ToString())));

    public static void PlanningTooltip(ImString text)
        => ImGui.SetTooltip(Runtime.ImText(Runtime.PlanningText(text.ToString())));

    public static bool PlanningBeginMenu(ImString label, bool enabled)
        => ImGui.BeginMenu(Runtime.ImText(Runtime.PlanningMenuText(label.ToString())), enabled);

    public static bool PlanningMenuItem(ImString label, ImString shortcut, bool selected, bool enabled)
        => ImGui.MenuItem(Runtime.ImText(Runtime.PlanningMenuText(label.ToString())), shortcut, selected, enabled);

    public static bool PlanningBurnActionMenuItem(ImString label, ImString shortcut, bool selected, bool enabled)
    {
        string original = label.ToString();
        return ImGui.MenuItem(Runtime.ImText(PlanningBurnActionMenuText(original)),
            shortcut, selected, enabled);
    }

    public static string PlanningBurnActionMenuText(string original)
    {
        if (!TryBurnActionLabel(original, out string display)) return original;
        // Use an identical hidden source label in English and Chinese so changing
        // locale cannot change the menu item's ImGui ID.
        return display + "###" + original;
    }

    public static string BurnActionLabel(string original)
    {
        return TryBurnActionLabel(original, out string display) ? display : original;
    }

    private static bool TryBurnActionLabel(string original, out string display)
    {
        display = original;
        const string createPrefix = "Create Burn [";
        const string addPrefix = "Add ";
        const string addMiddle = " Burn [";
        if (original.StartsWith(createPrefix, StringComparison.Ordinal) && original.EndsWith("]", StringComparison.Ordinal))
        {
            string type = original[createPrefix.Length..^1];
            if (!BurnActionTypes.Contains(type)) return false;
            string translatedType = Runtime.PlanningText(type);
            display = Runtime.PlanningText(createPrefix) + translatedType + Runtime.PlanningText("]");
            return true;
        }
        if (!original.StartsWith(addPrefix, StringComparison.Ordinal)) return false;
        int middle = original.IndexOf(addMiddle, addPrefix.Length, StringComparison.Ordinal);
        if (middle < 0 || !original.EndsWith("]", StringComparison.Ordinal)) return false;
        string ordinal = original[addPrefix.Length..middle];
        int digits = 0;
        while (digits < ordinal.Length && char.IsAsciiDigit(ordinal[digits])) digits++;
        if (digits == 0 || !IsEnglishOrdinal(ordinal, digits)) return false;
        string typeName = original[(middle + addMiddle.Length)..^1];
        if (!BurnActionTypes.Contains(typeName)) return false;
        string translatedTypeName = Runtime.PlanningText(typeName);
        display = Runtime.PlanningText(addPrefix) + ordinal[..digits]
            + Runtime.PlanningText(addMiddle) + translatedTypeName + Runtime.PlanningText("]");
        return true;
    }

    private static bool IsEnglishOrdinal(string ordinal, int digits)
    {
        if (!int.TryParse(ordinal.AsSpan(0, digits), out int value) || value <= 0) return false;
        int lastTwo = value % 100;
        string expected = lastTwo is >= 11 and <= 13 ? "th" : (value % 10) switch
        {
            1 => "st",
            2 => "nd",
            3 => "rd",
            _ => "th"
        };
        return ordinal.AsSpan(digits).SequenceEqual(expected);
    }

    public static bool PlanningParentDepartureMenuItem(ImString label, ImString shortcut, bool selected, bool enabled)
    {
        string original = label.ToString();
        return ImGui.MenuItem(Runtime.ImText(PlanningParentDepartureMenuText(original)),
            shortcut, selected, enabled);
    }

    public static string PlanningParentDepartureMenuText(string original)
    {
        const string prefix = "Create Burn [";
        const string suffix = " Departure]";
        if (!original.StartsWith(prefix, StringComparison.Ordinal)
            || !original.EndsWith(suffix, StringComparison.Ordinal)
            || original.Length <= prefix.Length + suffix.Length)
            return original;

        string bodyId = original[prefix.Length..^suffix.Length];
        // The celestial ID is user/game data. Only translate the fixed shell and
        // preserve one canonical hidden ID in both locales.
        string visible = Runtime.PlanningText(prefix) + bodyId + Runtime.PlanningText(suffix);
        return visible + "###" + original;
    }

    public static void PlanningDrawTextWidget(ImString label, string value, int columnWidth)
    {
        string originalLabel = label.ToString();
        string localizedValue = originalLabel == "EndTime" && value == "Never"
            ? Runtime.PlanningText("Never")
            : TimeValueLabels.Contains(PlanningFieldKey(originalLabel)) ? PlanningTimeValue(value) : value;
        ImGuiHelper.DrawTextWidget(Runtime.ImText(PlanningFieldText(originalLabel)), localizedValue, columnWidth);
    }

    public static void PlanningDrawTextWidget(ImString label, ImString value, int columnWidth)
    {
        string originalLabel = label.ToString();
        string originalValue = value.ToString();
        string localizedValue = originalLabel == "EndTime" && originalValue == "Never"
            ? Runtime.PlanningText("Never")
            : TimeValueLabels.Contains(PlanningFieldKey(originalLabel)) ? PlanningTimeValue(originalValue) : originalValue;
        ImGuiHelper.DrawTextWidget(Runtime.ImText(PlanningFieldText(originalLabel)), Runtime.ImText(localizedValue), columnWidth);
    }

    public static void PlanningDrawTextWidget(string label, ImString value, byte4 colorLabel, byte4 colorValue, int columnWidth)
    {
        string originalValue = value.ToString();
        string localizedValue = TimeValueLabels.Contains(PlanningFieldKey(label))
            ? PlanningTimeValue(originalValue) : originalValue;
        ImGuiHelper.DrawTextWidget(PlanningFieldText(label),
            localizedValue == originalValue ? value : Runtime.ImText(localizedValue), colorLabel, colorValue, columnWidth);
    }

    private static string PlanningFieldKey(string original) => original switch
    {
        "Period" => "Orbital Period",
        "Periapsis" => "Orbit Periapsis",
        "Apoapsis" => "Orbit Apoapsis",
        _ => original
    };

    private static string PlanningFieldText(string original) => original switch
    {
        // TrackingPatches already uses Period for a display multiplier. Use field
        // aliases here so the orbit readout cannot inherit that unrelated caption.
        "Period" => Runtime.PlanningText("Orbital Period"),
        "Periapsis" => Runtime.PlanningText("Orbit Periapsis"),
        "Apoapsis" => Runtime.PlanningText("Orbit Apoapsis"),
        _ => Runtime.PlanningText(original)
    };

    public static bool PlanningDrawButton(ImString label, byte4? button, byte4? hovered, byte4? active)
        => ImGuiHelper.DrawButton(Runtime.ImText(Runtime.PlanningMenuText(label.ToString())), button, hovered, active);

    public static bool PlanningDrawButton(ImString label, float2 size, byte4? button, byte4? hovered, byte4? active)
        => ImGuiHelper.DrawButton(Runtime.ImText(Runtime.PlanningMenuText(label.ToString())), size, button, hovered, active);

    public static bool PlanningConsoleButton(ReadOnlySpan<char> label)
    {
        string source = label.ToString();
        string translated = PlanningButtonCaption(source);
        // DrawButtonCore prints label directly. Keep the original key/id separate so
        // a canonical ### suffix from PlanningMenuText is never rendered as text.
        return ConsoleWidgets.Button(translated.AsSpan(), label, float2.Zero);
    }

    public static bool PlanningDangerButton(ReadOnlySpan<char> label)
    {
        string source = label.ToString();
        string translated = PlanningButtonCaption(source);
        return ConsoleWidgets.DangerButton(translated.AsSpan(), label, float2.Zero);
    }

    private static string PlanningButtonCaption(string source)
    {
        string scoped = Runtime.PlanningMenuText(source);
        int stable = scoped.IndexOf("##", StringComparison.Ordinal);
        return stable < 0 ? scoped : scoped[..stable];
    }

    public static string PlanningEncounterLabel(string original)
    {
        const string prefix = "Closest Approach to ";
        int stable = original.IndexOf("###", StringComparison.Ordinal);
        if (stable < 0 || !original.AsSpan(0, stable).StartsWith(prefix, StringComparison.Ordinal)) return original;
        return Runtime.PlanningText(prefix) + original[prefix.Length..stable] + original[stable..];
    }

    public static string PlanningTransitionText(ref PatchTransition transition)
        => Runtime.PlanningText(transition.ToString());

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Translate(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        var code = instructions.ToList();
        bool vehicleWindow = __originalMethod.DeclaringType == typeof(Vehicle)
            && __originalMethod.Name == nameof(Vehicle.DrawPatchWindow);
        bool flightPlanPatchInfo = __originalMethod.DeclaringType == typeof(FlightPlan)
            && __originalMethod.Name == nameof(FlightPlan.DrawPatchInfo);
        bool patchedConicInfo = __originalMethod.DeclaringType == typeof(PatchedConic)
            && __originalMethod.Name == nameof(PatchedConic.DrawPatchInfo);
        bool encounterInfo = __originalMethod.DeclaringType == typeof(Encounter)
            && __originalMethod.Name == nameof(Encounter.DrawEncounterInfo);
        bool burnPatchWindow = __originalMethod.DeclaringType == typeof(Burn)
            && __originalMethod.Name == nameof(Burn.DrawBurnPatchWindow);
        bool burnInfo = __originalMethod.DeclaringType == typeof(Burn)
            && __originalMethod.Name == nameof(Burn.DrawBurnInfo);
        bool burnEditor = __originalMethod.DeclaringType == typeof(Burn)
            && __originalMethod.Name == nameof(Burn.DrawBurnEditorWindowContent);
        bool burnMenu = __originalMethod.DeclaringType == typeof(BurnContextMenu)
            && __originalMethod.Name == "Draw";
        bool parentDepartureMenu = __originalMethod.DeclaringType == typeof(BurnContextMenu)
            && __originalMethod.Name == "DrawParentDepartureAction";
        bool burnActionsMenu = __originalMethod.DeclaringType == typeof(BurnContextMenu)
            && __originalMethod.Name == "DrawBurnActions";
        bool burnActionItem = __originalMethod.DeclaringType == typeof(BurnContextMenu)
            && __originalMethod.Name == "BurnActionMenuItem";
        // ImGui.Text returns void. PlanningTextElement returns an ImString and is only
        // suitable for value-producing ImString operands such as DrawTextWidget labels.
        var staticText = AccessTools.Method(typeof(FlightPlanCallsitePatch), nameof(PlanningText));
        var plannedCollapsing = AccessTools.Method(typeof(FlightPlanCallsitePatch), nameof(PlanningCollapsingHeader));
        var readout = AccessTools.Method(typeof(FlightPlanCallsitePatch), nameof(PlanningReadout));
        var beginWindow = AccessTools.Method(typeof(FlightPlanCallsitePatch), nameof(PlanningBeginFlightPlanWindow));
        var menuItem = AccessTools.Method(typeof(FlightPlanCallsitePatch), nameof(PlanningMenuItem));
        var beginMenu = AccessTools.Method(typeof(FlightPlanCallsitePatch), nameof(PlanningBeginMenu));
        var tooltip = AccessTools.Method(typeof(FlightPlanCallsitePatch), nameof(PlanningTooltip));
        var textWidgetImStringString = AccessTools.Method(typeof(FlightPlanCallsitePatch), nameof(PlanningDrawTextWidget),
            new[] { typeof(ImString), typeof(string), typeof(int) });
        var textWidgetImStringImString = AccessTools.Method(typeof(FlightPlanCallsitePatch), nameof(PlanningDrawTextWidget),
            new[] { typeof(ImString), typeof(ImString), typeof(int) });
        var textWidgetStringImString = AccessTools.Method(typeof(FlightPlanCallsitePatch), nameof(PlanningDrawTextWidget),
            new[] { typeof(string), typeof(ImString), typeof(byte4), typeof(byte4), typeof(int) });
        var drawButtonDefault = AccessTools.Method(typeof(FlightPlanCallsitePatch), nameof(PlanningDrawButton),
            new[] { typeof(ImString), typeof(byte4?), typeof(byte4?), typeof(byte4?) });
        var drawButtonSize = AccessTools.Method(typeof(FlightPlanCallsitePatch), nameof(PlanningDrawButton),
            new[] { typeof(ImString), typeof(float2), typeof(byte4?), typeof(byte4?), typeof(byte4?) });
        var consoleButton = AccessTools.Method(typeof(FlightPlanCallsitePatch), nameof(PlanningConsoleButton));
        var dangerButton = AccessTools.Method(typeof(FlightPlanCallsitePatch), nameof(PlanningDangerButton));
        var burnMenuItem = AccessTools.Method(typeof(FlightPlanCallsitePatch), nameof(PlanningBurnActionMenuItem));
        var departureMenuItem = AccessTools.Method(typeof(FlightPlanCallsitePatch), nameof(PlanningParentDepartureMenuItem));
        var transitionText = AccessTools.Method(typeof(FlightPlanCallsitePatch), nameof(PlanningTransitionText));
        var planningTimeText = AccessTools.Method(typeof(FlightPlanCallsitePatch), nameof(PlanningTimeText));

        for (int i = 0; i < code.Count; i++)
        {
            var instruction = code[i];

            if (instruction.opcode == OpCodes.Ldstr && instruction.operand is string literal)
            {
                bool burnHeader = burnPatchWindow && literal == "Burn###Burn";
                bool burnEditHeader = burnInfo && literal is "Edit###Burn" or "Delete###Burn";
                yield return instruction;

                if (burnHeader || burnEditHeader)
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Runtime), nameof(Runtime.PlanningMenuText)));
                continue;
            }

            if (instruction.operand is MethodInfo method)
            {
                if (vehicleWindow && method.DeclaringType == typeof(ConsoleStyle) && method.Name == nameof(ConsoleStyle.BeginWindow))
                {
                    var p = method.GetParameters();
                    if (p.Length == 9 && p[0].ParameterType == typeof(ReadOnlySpan<char>)
                        && p[1].ParameterType == typeof(ReadOnlySpan<char>)
                        && p[2].ParameterType == typeof(ReadOnlySpan<char>)
                        && p[3].ParameterType == typeof(ReadOnlySpan<char>))
                    {
                        instruction.opcode = OpCodes.Call;
                        instruction.operand = beginWindow;
                    }
                }
                else if (vehicleWindow && method.DeclaringType == typeof(ConsoleWidgets) && method.Name == nameof(ConsoleWidgets.Readout)
                    && method.GetParameters().Length == 2)
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = readout;
                }
                else if ((flightPlanPatchInfo || patchedConicInfo || burnPatchWindow || burnInfo || burnEditor)
                    && method.DeclaringType == typeof(ImGuiHelper) && method.Name == nameof(ImGuiHelper.DrawTextWidget))
                {
                    var p = method.GetParameters();
                    if (p.Length == 3 && p[0].ParameterType == typeof(ImString) && p[1].ParameterType == typeof(string))
                    {
                        instruction.opcode = OpCodes.Call;
                        instruction.operand = textWidgetImStringString;
                    }
                    else if (p.Length == 3 && p[0].ParameterType == typeof(ImString) && p[1].ParameterType == typeof(ImString))
                    {
                        instruction.opcode = OpCodes.Call;
                        instruction.operand = textWidgetImStringImString;
                    }
                    else if (p.Length == 5 && p[0].ParameterType == typeof(string) && p[1].ParameterType == typeof(ImString))
                    {
                        instruction.opcode = OpCodes.Call;
                        instruction.operand = textWidgetStringImString;
                    }
                }
                else if ((flightPlanPatchInfo || burnPatchWindow || patchedConicInfo || encounterInfo)
                    && method.DeclaringType == typeof(ImGui) && method.Name == nameof(ImGui.CollapsingHeader)
                    && method.GetParameters().Length == 2
                    && method.GetParameters()[0].ParameterType == typeof(ImString)
                    && method.GetParameters()[1].ParameterType == typeof(ImGuiTreeNodeFlags))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = plannedCollapsing;
                }
                else if (burnEditor && method.DeclaringType == typeof(ImGui) && method.Name == nameof(ImGui.Text)
                    && method.GetParameters().Length == 1 && method.GetParameters()[0].ParameterType == typeof(ImString)
                    && i > 0 && code[i - 1].operand is MethodInfo conversion && conversion.DeclaringType == typeof(ImString)
                    && conversion.Name == "op_Implicit" && conversion.GetParameters().Length == 1
                    && conversion.GetParameters()[0].ParameterType == typeof(ReadOnlySpan<byte>))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = staticText;
                }
                else if (burnEditor && method.DeclaringType == typeof(ImGui) && method.Name == nameof(ImGui.Text)
                    && method.GetParameters().Length == 1
                    && method.GetParameters()[0].ParameterType == typeof(ReadOnlySpan<char>)
                    && i > 0 && code[i - 1].operand is MethodInfo timeFormat
                    && timeFormat.DeclaringType == typeof(TimeSpanReference)
                    && timeFormat.Name == nameof(TimeSpanReference.ToNearest)
                    && timeFormat.ReturnType == typeof(ReadOnlySpan<char>))
                {
                    // This editor method's adjacent ToNearest -> Text sites are its
                    // Game Time / Time To values; other raw spans stay untouched.
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = planningTimeText;
                }
                else if (burnEditor && method.DeclaringType == typeof(ConsoleWidgets) && method.Name == nameof(ConsoleWidgets.DangerButton)
                    && method.GetParameters().Length == 1 && method.GetParameters()[0].ParameterType == typeof(ReadOnlySpan<char>))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = dangerButton;
                }
                else if (burnEditor && method.DeclaringType == typeof(ConsoleWidgets) && method.Name == nameof(ConsoleWidgets.Button)
                    && method.GetParameters().Length == 1 && method.GetParameters()[0].ParameterType == typeof(ReadOnlySpan<char>))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = consoleButton;
                }
                else if (burnInfo && method.DeclaringType == typeof(ImGuiHelper) && method.Name == nameof(ImGuiHelper.DrawButton))
                {
                    var p = method.GetParameters();
                    if (p.Length == 4 && p[0].ParameterType == typeof(ImString))
                    {
                        instruction.opcode = OpCodes.Call;
                        instruction.operand = drawButtonDefault;
                    }
                    else if (p.Length == 5 && p[0].ParameterType == typeof(ImString)
                        && p[1].ParameterType == typeof(float2))
                    {
                        instruction.opcode = OpCodes.Call;
                        instruction.operand = drawButtonSize;
                    }
                }
                else if ((burnMenu || burnActionsMenu || parentDepartureMenu || burnActionItem)
                    && method.DeclaringType == typeof(ImGui) && method.Name == nameof(ImGui.BeginMenu)
                    && method.GetParameters().Length == 2)
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = beginMenu;
                }
                else if ((burnMenu || burnActionsMenu || parentDepartureMenu || burnActionItem)
                    && method.DeclaringType == typeof(ImGui) && method.Name == nameof(ImGui.MenuItem)
                    && method.GetParameters().Length == 4
                    && method.GetParameters()[0].ParameterType == typeof(ImString)
                    && method.GetParameters()[1].ParameterType == typeof(ImString)
                    && method.GetParameters()[2].ParameterType == typeof(bool))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = burnActionItem ? burnMenuItem
                        : parentDepartureMenu ? departureMenuItem : menuItem;
                }
                else if ((burnMenu || burnActionsMenu || parentDepartureMenu || burnActionItem)
                    && method.DeclaringType == typeof(ImGui) && method.Name == nameof(ImGui.SetTooltip)
                    && method.GetParameters().Length == 1 && method.GetParameters()[0].ParameterType == typeof(ImString))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = tooltip;
                }
                else if (patchedConicInfo && method.Name == "ToString"
                    && method.DeclaringType == typeof(object) && method.GetParameters().Length == 0
                    && i > 0 && code[i - 1].opcode == OpCodes.Constrained
                    && Equals(code[i - 1].operand, typeof(PatchTransition)))
                {
                    code[i - 1].opcode = OpCodes.Nop;
                    code[i - 1].operand = null;
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = transitionText;
                }
            }

            yield return instruction;
        }
    }
}
