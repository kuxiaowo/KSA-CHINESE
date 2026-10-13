// Adapted from LiX-Works/KsaUiLanguages (MIT), b504774158cc03b582a0e75bfe94330cb841f11d.
using System.Reflection;
using System.Reflection.Emit;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using HarmonyLib;
using KSA;

namespace KsaChinese;

// Each target is a native part/resource readout. Only the fixed first-column
// caption is translated; numeric values, units, substance/template names and
// the calculation/state code stay native. No shared helper is patched here.
[HarmonyPatch]
public static class DeeperResourceCaptionPatch
{
    public static readonly HashSet<string> Labels = new(StringComparer.Ordinal)
    {
        "Mass:", "Storage Temperature:", "Density:", "Volume:", "Charge", "Throttle",
        "Mass Flow Rate", "Exit Pressure", "Exit Temperature", "Chamber Pressure", "Chamber Temperature",
        "Thrust", "Specific Impulse", "Thrust (Sea Level)", "Thrust (Vacuum)",
        "Specific Impulse (Sea Level)", "Specific Impulse (Vacuum)",
        "Thrust Profile:", "Grain Shape:", "Propellant:", "Grain Mass:", "Grain Burned:", "Burning Area:"
    };

    public static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var (type, name) in new[] {
            (typeof(Mole), "DrawStateInfo"), (typeof(BatteryState), "DrawStateInfo"),
            (typeof(RocketCoreState), "DrawStateInfo"), (typeof(RocketNozzleState), "DrawStateInfo"),
            (typeof(RocketCoreConditions), "DrawDesignInfo"), (typeof(RocketNozzle), "DrawDesignInfo"),
            (typeof(SolidGrainSegment), "DrawStateInfo") })
            yield return AccessTools.DeclaredMethod(type, name) ?? throw new MissingMethodException(type.FullName, name);
    }

    public static string LabelText(string source) => Labels.Contains(source) ? Runtime.UtilityText(source) : source;
    public static ImString LabelCaption(ImString source)
    {
        string original = source.ToString();
        string translated = LabelText(original);
        return translated == original ? source : Runtime.ImText(translated);
    }
    public static void TextWidget(ImString variable, ImString text, int columnWidth)
        => ImGuiHelper.DrawTextWidget(LabelCaption(variable), text, columnWidth);
    public static void StringWidget(ImString variable, string text, int columnWidth)
        => ImGuiHelper.DrawTextWidget(LabelCaption(variable), text, columnWidth);
    public static void TwoColumnWidget(ImString variable, ImString text)
        => ImGuiHelper.Draw2ColumnTextWidget(LabelCaption(variable), text);

    // This is injected at the exact fallback ldstr, never at a dynamic
    // Propellant.Name or Geometry.Name value that happens to equal the key.
    public static string NoMotorText(string source) => Runtime.UtilityText(source);

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Translate(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        int labels = 0, fallbacks = 0;
        foreach (var instruction in instructions)
        {
            if (instruction.operand is MethodInfo native && native.DeclaringType == typeof(ImGuiHelper))
            {
                var args = native.GetParameters();
                string? wrapper = null;
                if (native.Name == nameof(ImGuiHelper.DrawTextWidget) && args.Length == 3
                    && args[0].ParameterType == typeof(ImString) && args[2].ParameterType == typeof(int))
                {
                    if (args[1].ParameterType == typeof(ImString)) wrapper = nameof(TextWidget);
                    else if (args[1].ParameterType == typeof(string)) wrapper = nameof(StringWidget);
                }
                else if (native.Name == nameof(ImGuiHelper.Draw2ColumnTextWidget) && args.Length == 2
                    && args.All(a => a.ParameterType == typeof(ImString))) wrapper = nameof(TwoColumnWidget);
                if (wrapper is not null)
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = AccessTools.Method(typeof(DeeperResourceCaptionPatch), wrapper);
                    labels++;
                }
            }
            yield return instruction;
            if (__originalMethod.DeclaringType == typeof(SolidGrainSegment)
                && instruction.opcode == OpCodes.Ldstr && instruction.operand is string literal && literal == "None (no motor)")
            {
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(DeeperResourceCaptionPatch), nameof(NoMotorText)));
                fallbacks++;
            }
        }
        int expected = __originalMethod.DeclaringType == typeof(Mole) ? 4
            : __originalMethod.DeclaringType == typeof(BatteryState) ? 1
            : __originalMethod.DeclaringType == typeof(RocketCoreState) ? 6
            : __originalMethod.DeclaringType == typeof(RocketNozzleState) ? 3
            : __originalMethod.DeclaringType == typeof(RocketCoreConditions) ? 4
            : __originalMethod.DeclaringType == typeof(RocketNozzle) ? 5 : 6;
        int expectedFallbacks = __originalMethod.DeclaringType == typeof(SolidGrainSegment) ? 1 : 0;
        if (labels != expected || fallbacks != expectedFallbacks)
            throw new InvalidOperationException($"Deep resource display sites changed: {__originalMethod}: {labels}/{expected} labels, {fallbacks}/{expectedFallbacks} fallbacks.");
        Runtime.Log($"Deep resource display: {__originalMethod}: {labels} fixed labels; values/names unchanged.");
    }

    // Compose this into the already-installed PartContextFieldCaptionPatch
    // CombinedCaption at its existing helper Text site. The native helper must
    // receive 'Draw Graph' unchanged to retain ###Draw Graph{_widgetId++}.
    public static ImString GraphFieldCaption(ImString source)
        => DeeperResourceGraphScopePatch.Depth > 0 && source.ToString() == "Draw Graph"
            ? Runtime.ImText(Runtime.UtilityText("Draw Graph")) : source;
}

// Only the two actual storage-manager specializations in this game build.
// The prefix/finalizer changes no argument, ref value, result or native ID.
[HarmonyPatch]
public static class DeeperResourceGraphScopePatch
{
    [ThreadStatic] public static int Depth;
    public static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (Type type in new[] { typeof(ResourceManager), typeof(PowerManager) })
        {
            Type manager = type.BaseType ?? throw new InvalidOperationException($"Missing resource manager base: {type}.");
            if (!manager.IsConstructedGenericType || manager.GetGenericTypeDefinition() != typeof(ResourceManagerBase<,>))
                throw new InvalidOperationException($"Resource manager base changed: {manager}.");
            yield return AccessTools.DeclaredMethod(manager, "DrawResourceInfo")
                ?? throw new MissingMethodException(manager.FullName, "DrawResourceInfo");
        }
    }
    [HarmonyPrefix]
    public static void Enter(out int __state) { __state = Depth; Depth++; }
    [HarmonyFinalizer]
    public static Exception? Leave(Exception? __exception, int __state) { Depth = __state; return __exception; }
}

// Manager-generated entry captions contain only native numeric instance IDs.
// Translate their literal display fragments before the existing ### tail;
// substance headers in Tank.DrawStateInfo remain untouched.
[HarmonyPatch]
public static class DeeperResourceEntryCaptionPatch
{
    public static readonly HashSet<string> Fragments = new(StringComparer.Ordinal) { "Tank '", "Battery '", "' Index '" };
    public static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.DeclaredMethod(typeof(ResourceManager), "DrawEntryInfo")
            ?? throw new MissingMethodException(typeof(ResourceManager).FullName, "DrawEntryInfo");
        yield return AccessTools.DeclaredMethod(typeof(PowerManager), "DrawEntryInfo")
            ?? throw new MissingMethodException(typeof(PowerManager).FullName, "DrawEntryInfo");
    }
    public static string FragmentText(string source) => Fragments.Contains(source) ? Runtime.UtilityText(source) : source;
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Translate(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        int count = 0;
        foreach (var instruction in instructions)
        {
            yield return instruction;
            if (instruction.opcode == OpCodes.Ldstr && instruction.operand is string literal && Fragments.Contains(literal))
            {
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(DeeperResourceEntryCaptionPatch), nameof(FragmentText)));
                count++;
            }
        }
        if (count != 2) throw new InvalidOperationException($"Resource entry display changed: {__originalMethod}: {count}/2 fragments.");
        Runtime.Log($"Resource entry display: {__originalMethod}: {count} fragments; original ### tail and numeric IDs retained.");
    }
}

// The parachute's native menu is a common player-facing part menu. Keep its
// tuning range, gating, grouping, returned action bool and control callbacks.
[HarmonyPatch]
public static class ParachutePartCaptionPatch
{
    public static readonly HashSet<string> Controls = new(StringComparer.Ordinal)
    { "Canopy Diameter", "Deploy Altitude", "Arm", "Disarm", "Force Deploy", "Un-reef", "Cut" };
    public static readonly HashSet<string> States = new(StringComparer.Ordinal)
    { "Stowed", "Armed", "LineStretch", "Reefed", "Disreefing", "Full", "Cut", "Ripped", "SeveredLines", "Unknown" };
    public static readonly HashSet<string> Tooltips = new(StringComparer.Ordinal)
    {
        "Canopy diameter, which sets both drag area and packed mass. Applies to every chute in this part, and is only adjustable before they deploy.",
        "Altitude gate for an armed chute, shared by every chute in this part. 0 = no altitude gate: an armed chute deploys as soon as deployment is safe (g-limit + dynamic pressure). With an altitude set it waits for both."
    };
    public static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (string name in new[] { "ShowContextMenu", "DrawGatedButton", "AppendClusterState" })
            yield return AccessTools.DeclaredMethod(typeof(Parachute), name)
                ?? throw new MissingMethodException(typeof(Parachute).FullName, name);
    }
    public static string ControlText(string source) => Controls.Contains(source) ? Runtime.UtilityMenuText(source) : source;
    public static ImString ControlCaption(ImString source)
    {
        string original = source.ToString();
        string translated = ControlText(original);
        return translated == original ? source : Runtime.ImText(translated);
    }
    public static string TooltipText(string source) => Tooltips.Contains(source) ? Runtime.UtilityText(source) : source;
    public static void Tooltip(ImString text, Func<bool>? hovered)
    {
        string original = text.ToString();
        string translated = TooltipText(original);
        ImGuiHelper.DrawTooltip(translated == original ? text : Runtime.ImText(translated), hovered);
    }
    public static bool Slider(ImString label, ref float value, float min, float max, ImString format, ImGuiSliderFlags flags)
    {
        ImString displayFormat = format.ToString() == "off" ? Runtime.ImText(Runtime.UtilityText("off")) : format;
        return ImGui.SliderFloat(ControlCaption(label), ref value, min, max, displayFormat, flags);
    }
    public static bool Button(ImString label, in float2? size) => ImGui.Button(ControlCaption(label), in size);
    public static string StatePrefix(string source) => Runtime.UtilityText(source);
    public static string StateText(string source) => States.Contains(source) ? Runtime.UtilityText(source) : source;
    public static ReadOnlySpan<char> ClusterStateLabel(ChuteState state)
        => StateText(Parachute.StateLabel(state).ToString()).AsSpan();

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Translate(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        int sliders = 0, tips = 0, buttons = 0, states = 0, prefixes = 0;
        foreach (var instruction in instructions)
        {
            if (instruction.operand is MethodInfo native)
            {
                var args = native.GetParameters();
                string? wrapper = null;
                if (__originalMethod.Name == "ShowContextMenu" && native.DeclaringType == typeof(ImGui)
                    && native.Name == nameof(ImGui.SliderFloat) && args.Length == 6
                    && args[0].ParameterType == typeof(ImString) && args[1].ParameterType == typeof(float).MakeByRefType()
                    && args[4].ParameterType == typeof(ImString) && args[5].ParameterType == typeof(ImGuiSliderFlags))
                { wrapper = nameof(Slider); sliders++; }
                else if (__originalMethod.Name == "ShowContextMenu" && native.DeclaringType == typeof(ImGuiHelper)
                    && native.Name == nameof(ImGuiHelper.DrawTooltip) && args.Length == 2
                    && args[0].ParameterType == typeof(ImString) && args[1].ParameterType == typeof(Func<bool>))
                { wrapper = nameof(Tooltip); tips++; }
                else if (__originalMethod.Name == "DrawGatedButton" && native.DeclaringType == typeof(ImGui)
                    && native.Name == nameof(ImGui.Button) && args.Length == 2
                    && args[0].ParameterType == typeof(ImString) && args[1].ParameterType == typeof(float2?).MakeByRefType())
                { wrapper = nameof(Button); buttons++; }
                else if (__originalMethod.Name == "AppendClusterState" && native.DeclaringType == typeof(Parachute)
                    && native.Name == nameof(Parachute.StateLabel) && args.Length == 1
                    && args[0].ParameterType == typeof(ChuteState) && native.ReturnType == typeof(ReadOnlySpan<char>))
                { wrapper = nameof(ClusterStateLabel); states++; }
                if (wrapper is not null)
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = AccessTools.Method(typeof(ParachutePartCaptionPatch), wrapper);
                }
            }
            yield return instruction;
            if (__originalMethod.Name == "ShowContextMenu" && instruction.opcode == OpCodes.Ldstr
                && instruction.operand is string literal && literal == "State: ")
            {
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ParachutePartCaptionPatch), nameof(StatePrefix)));
                prefixes++;
            }
        }
        bool valid = __originalMethod.Name == "ShowContextMenu" ? sliders == 2 && tips == 2 && prefixes == 1
            : __originalMethod.Name == "DrawGatedButton" ? buttons == 1
            : states == 2;
        if (!valid) throw new InvalidOperationException($"Parachute part display changed: {__originalMethod}: {sliders} sliders, {tips} tooltips, {prefixes} prefixes, {buttons} buttons, {states} states.");
        Runtime.Log($"Parachute part display: {__originalMethod}: captions translated, IDs/ref values/callbacks/gating retained.");
    }
}
