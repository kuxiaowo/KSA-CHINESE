// Adapted from LiX-Works/KsaUiLanguages (MIT), b504774158cc03b582a0e75bfe94330cb841f11d.
using System.Reflection;
using System.Reflection.Emit;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using HarmonyLib;
using KSA;

namespace KsaChinese;

[HarmonyPatch]
public static class PartContextCaptionPatch
{
    public static readonly HashSet<string> PlainKeys = new(StringComparer.Ordinal)
    {
        "Chutes on ", "Decoupler '", "Decoupler", "Inert Mass", "Reaction", "Max Pressure",
        "Stack Invalid", "Area Ratio (sized)", "Expansion", "Electricity", "Docked to ",
        "Targeted Port", "Nearby Vehicle", "Dist:", "C/Vel:", "Y Axis:", "Z Axis:",
        "Viewing: ", "Following: None", "Scope: ", "Offset: ", "Mode: Vehicle (", "Mode: Map",
        "When off, this decoupler is inert and hidden from the sequence window.",
        "When off, nothing can draw propellant from this tank.",
        "Drain this tank into every tank set to In on another open part window. Uses electrical power.",
        "Fill this tank from every tank set to Out on another open part window. Uses electrical power.",
        "Draw the equatorial plane of the followed body and its target alongside the ecliptic.",
        "Hold every plane parallel to the ecliptic instead of orienting it by its body's axial tilt."
    };
    public static readonly HashSet<string> InteractiveKeys = new(StringComparer.Ordinal)
    {
        "UnFocus Camera", "Focus Camera", "Open Details", "Decouple", "Control From Here",
        "Toggle Docking Camera", "Undock", "Clear Target", "Set as Target", "Dock", "Out", "In",
        "Map View", "Body Grid Planes", "Align Planes", "Release Control", "Control Vehicle", "Recenter"
    };
    public static readonly string[] InteractivePrefixes =
    {
        "Enter ", "Decouple '", "Control From Here: ", "Set Target: ",
        "Tank '", "Grain Segment '", "Engine Core '", "Nozzle '", "Control ", "Focus "
    };

    public static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (string name in new[] { "ShowContextMenu", "DrawTransferButton", "OnDrawUi", "ShowControlFromHereItem", "DrawPartInfo" })
            yield return AccessTools.Method(typeof(Part), name)
                ?? throw new MissingMethodException(typeof(Part).FullName, name);
        foreach (string name in new[] { "ShowContextMenu", "OnDrawUi" })
            yield return AccessTools.Method(typeof(DockingPort), name)
                ?? throw new MissingMethodException(typeof(DockingPort).FullName, name);
    }

    public static string PlainText(string source)
        => PlainKeys.Contains(source) ? Runtime.UtilityText(source) : source;

    public static ImString PlainCaption(ImString source)
    {
        string text = source.ToString();
        string translated = PlainText(text);
        return translated == text ? source : Runtime.ImText(translated);
    }

    // Translate the final assembled caption; never translate the entity suffix
    // or change the original full ImGui ID. Existing ### tails are copied intact.
    public static string InteractiveText(string source)
    {
        int separator = source.IndexOf("##", StringComparison.Ordinal);
        string visible = separator < 0 ? source : source[..separator];
        string translated = visible;
        bool known = false;
        if (InteractiveKeys.Contains(visible)
            && Runtime.Packs.Values.Any(pack => pack.Utility.ContainsKey(visible)))
        {
            known = true;
            translated = Runtime.UtilityText(visible);
        }
        else
        {
            foreach (string prefix in InteractivePrefixes)
            {
                if (!visible.StartsWith(prefix, StringComparison.Ordinal)) continue;
                if (!Runtime.Packs.Values.Any(pack => pack.Utility.ContainsKey(prefix))) break;
                known = true;
                string next = Runtime.UtilityText(prefix);
                translated = next + visible[prefix.Length..];
                break;
            }
        }
        // English and Chinese must use the same canonical ID even when the
        // current pack is empty. Unknown/custom labels still remain byte-exact.
        if (!known) return source;
        int stable = source.IndexOf("###", StringComparison.Ordinal);
        return translated + (stable >= 0 ? source[stable..] : "###" + source);
    }

    public static ImString InteractiveCaption(ImString source)
    {
        string original = source.ToString();
        string translated = InteractiveText(original);
        return translated == original ? source : Runtime.ImText(translated);
    }

    public static bool MenuItem(ImString label, ImString shortcut, bool selected, bool enabled)
        => ImGui.MenuItem(InteractiveCaption(label), shortcut, selected, enabled);
    public static bool Button(ImString label, in float2? size)
        => ImGui.Button(InteractiveCaption(label), in size);
    public static bool Checkbox(ImString label, ref bool value)
        => ImGui.Checkbox(InteractiveCaption(label), ref value);
    public static bool CollapsingHeader(ImString label, ImGuiTreeNodeFlags flags)
        => ImGui.CollapsingHeader(InteractiveCaption(label), flags);
    public static bool Begin(ImString label, ImGuiWindowFlags flags)
        => ImGui.Begin(InteractiveCaption(label), flags);
    public static bool HelperButton(ImString label, byte4? button, byte4? hovered, byte4? active)
        => ImGuiHelper.DrawButton(InteractiveCaption(label), button, hovered, active);

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Translate(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        int plain = 0, interactive = 0, menus = 0;
        var literal = AccessTools.Method(typeof(PartContextCaptionPatch), nameof(PlainText));
        var fixedCaption = AccessTools.Method(typeof(PartContextCaptionPatch), nameof(PlainCaption));
        bool partWindow = __originalMethod.DeclaringType == typeof(Part) && __originalMethod.Name == "OnDrawUi";
        foreach (var instruction in instructions)
        {
            bool fixedUtf8 = false;
            if (instruction.operand is MethodInfo native)
            {
                var args = native.GetParameters();
                string? wrapper = null;
                if (native.DeclaringType == typeof(ImGui))
                {
                    if (native.Name == nameof(ImGui.MenuItem) && args.Length == 4 && args[2].ParameterType == typeof(bool))
                    {
                        menus++;
                        // Part.OnDrawUi's first menu entry is the actual dynamic
                        // Part.DisplayName, even when it equals a dictionary key.
                        if (!partWindow || menus > 1) wrapper = nameof(MenuItem);
                    }
                    else if (native.Name == nameof(ImGui.Button) && args.Length == 2) wrapper = nameof(Button);
                    else if (native.Name == nameof(ImGui.Checkbox) && args.Length == 2) wrapper = nameof(Checkbox);
                    else if (native.Name == nameof(ImGui.CollapsingHeader) && args.Length == 2) wrapper = nameof(CollapsingHeader);
                    else if (native.Name == nameof(ImGui.Begin) && args.Length == 2) wrapper = nameof(Begin);
                }
                else if (native.DeclaringType == typeof(ImGuiHelper) && native.Name == nameof(ImGuiHelper.DrawButton)
                    && args.Length == 4 && args[1].ParameterType == typeof(byte4?)) wrapper = nameof(HelperButton);
                else if (native.DeclaringType == typeof(ImString) && native.Name == "op_Implicit"
                    && args.Length == 1 && args[0].ParameterType == typeof(ReadOnlySpan<byte>)) fixedUtf8 = true;

                if (wrapper is not null)
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = AccessTools.Method(typeof(PartContextCaptionPatch), wrapper);
                    interactive++;
                }
            }
            yield return instruction;
            if (instruction.opcode == OpCodes.Ldstr && instruction.operand is string text && PlainKeys.Contains(text))
            {
                yield return new CodeInstruction(OpCodes.Call, literal);
                plain++;
            }
            else if (fixedUtf8)
            {
                // This conversion handles constant UTF-8 readout/tooltip text.
                // ID literals survive because PlainText is an exact whitelist.
                yield return new CodeInstruction(OpCodes.Call, fixedCaption);
                plain++;
            }
        }
        if (plain + interactive == 0 || (partWindow && menus != 2))
            throw new InvalidOperationException($"Part/context display sites changed: {__originalMethod}: {plain}/{interactive}, menus {menus}.");
        Runtime.Log($"Part/context display: {__originalMethod.DeclaringType?.Name}.{__originalMethod.Name}: {plain} caption sites, {interactive} controls.");
    }
}

// These native helpers use their original label to construct a separate input
// ID. Translate only Text(variable) inside their bodies, under this Part scope.
[HarmonyPatch(typeof(Part), nameof(Part.ShowContextMenu))]
public static class PartContextFieldScopePatch
{
    [ThreadStatic] public static int Depth;
    [HarmonyPrefix]
    public static void Enter(out int __state) { __state = Depth; Depth++; }
    [HarmonyFinalizer]
    public static Exception? Leave(Exception? __exception, int __state) { Depth = __state; return __exception; }
}

[HarmonyPatch]
public static class PartContextFieldCaptionPatch
{
    public static readonly HashSet<string> FieldKeys = new(StringComparer.Ordinal) { "Active", "Fuel Flow", "Propellant Use" };
    public static readonly HashSet<string> FlowOptionKeys = new(StringComparer.Ordinal)
    {
        "FurtherestToNearest", "NearestToFurtherest",
        "FurtherestToNearestSameStage", "NearestToFurtherestSameStage"
    };
    public static IEnumerable<MethodBase> TargetMethods()
    {
        yield return typeof(ImGuiHelper).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m.Name == nameof(ImGuiHelper.DrawCheckbox) && m.GetParameters().Length == 3
                && m.GetParameters()[0].ParameterType == typeof(ImString));
        // Discover the actual closed FlowRule-object combo from the native call
        // site instead of guessing its nested generic type or overload.
        var part = AccessTools.Method(typeof(Part), nameof(Part.ShowContextMenu));
        var combos = PatchProcessor.GetOriginalInstructions(part)
            .Select(i => i.operand).OfType<MethodInfo>()
            .Where(m => m.DeclaringType == typeof(ImGuiHelper) && m.Name == "DrawCombo" && m.IsGenericMethod
                && !m.ContainsGenericParameters && m.GetParameters()[0].ParameterType == typeof(ImString)).Distinct().ToArray();
        if (combos.Length != 1 || combos[0].GetGenericArguments()[0] != typeof(FlowRuleObject)
            || !typeof(FlowRuleObject).IsValueType)
            throw new InvalidOperationException($"Expected one value-type Part FlowRuleObject combo, found {combos.Length}.");
        yield return combos[0];
    }
    public static ImString DisplayVariable(ImString label)
    {
        label = ResourceUtilityCaptionPatch.FieldCaption(label);
        string source = label.ToString();
        return PartContextFieldScopePatch.Depth > 0 && FieldKeys.Contains(source)
            ? Runtime.ImText(Runtime.UtilityText(source)) : label;
    }
    // Compose each scoped lookup explicitly at the final display call. This
    // does not depend on which shared-helper transpiler runs first.
    public static ImString CombinedCaption(ImString label)
        => FlightPlanCheckboxCaptionPatch.DisplayCaption(DeeperResourceCaptionPatch.GraphFieldCaption(DisplayVariable(EditorFieldCaptionPatch.DisplayCaption(label))));
    public static void NativeText(ImString label) => ImGui.Text(CombinedCaption(label));

    public static string FlowPreviewText(string source)
        => PartContextFieldScopePatch.Depth > 0 && FlowOptionKeys.Contains(source)
            ? Runtime.UtilityText(source) : source;

    public static string FlowOptionText(string source)
        => PartContextFieldScopePatch.Depth > 0 && FlowOptionKeys.Contains(source)
            ? Runtime.UtilityMenuText(source) : source;

    public static bool FlowCombo(ImString id, ImString preview, ImGuiComboFlags flags)
    {
        string source = preview.ToString();
        string translated = FlowPreviewText(source);
        return ImGui.BeginCombo(id, translated == source ? preview : Runtime.ImText(translated), flags);
    }

    public static bool FlowOption(ImString label, bool selected, ImGuiSelectableFlags flags, in float2? size)
    {
        string source = label.ToString();
        string translated = FlowOptionText(source);
        return ImGui.Selectable(translated == source ? label : Runtime.ImText(translated), selected, flags, in size);
    }

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Translate(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        var body = instructions.ToList();
        int count = 0, previews = 0, options = 0;
        bool flow = __originalMethod.IsGenericMethod
            && __originalMethod.GetGenericArguments()[0] == typeof(FlowRuleObject);
        for (int i = 0; i < body.Count; i++)
        {
            var instruction = body[i];
            // Other caption transpilers may already have inserted a lookup
            // between ldarg.0 and this helper's sole Text call. Keep that
            // transformed ImString on the stack and compose our scoped display
            // wrapper at the call itself; never rely on instruction adjacency.
            if (instruction.operand is MethodInfo native
                && native.DeclaringType == typeof(ImGui) && native.Name == nameof(ImGui.Text)
                && native.ReturnType == typeof(void)
                && native.GetParameters().Length == 1 && native.GetParameters()[0].ParameterType == typeof(ImString))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = AccessTools.Method(typeof(PartContextFieldCaptionPatch), nameof(NativeText));
                count++;
            }
            else if (flow && instruction.operand is MethodInfo control && control.DeclaringType == typeof(ImGui))
            {
                var args = control.GetParameters();
                string? wrapper = null;
                if (control.Name == nameof(ImGui.BeginCombo) && args.Length == 3
                    && args[0].ParameterType == typeof(ImString) && args[1].ParameterType == typeof(ImString)
                    && args[2].ParameterType == typeof(ImGuiComboFlags))
                {
                    wrapper = nameof(FlowCombo);
                    previews++;
                }
                else if (control.Name == nameof(ImGui.Selectable) && args.Length == 4
                    && args[0].ParameterType == typeof(ImString) && args[1].ParameterType == typeof(bool)
                    && args[2].ParameterType == typeof(ImGuiSelectableFlags)
                    && args[3].ParameterType == typeof(float2?).MakeByRefType())
                {
                    wrapper = nameof(FlowOption);
                    options++;
                }
                if (wrapper is not null)
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = AccessTools.Method(typeof(PartContextFieldCaptionPatch), wrapper);
                }
            }
            yield return instruction;
        }
        if (count != 1 || (flow && (previews != 1 || options != 1)))
            throw new InvalidOperationException($"Part field display site changed: {__originalMethod}: {count} labels, {previews} previews, {options} options.");
        Runtime.Log($"Part field display: {__originalMethod}: {count} label, {previews} previews, {options} options; input ID/value/key unchanged.");
    }
}
