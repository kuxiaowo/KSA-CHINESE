// Adapted from LiX-Works/KsaUiLanguages (MIT), b504774158cc03b582a0e75bfe94330cb841f11d.
using System.Reflection;
using System.Reflection.Emit;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using HarmonyLib;
using KSA;

namespace KsaChinese;

[HarmonyPatch]
public static class EditorDrawScopePatch
{
    public static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (string name in new[] { "OnDrawUi", "DrawPartUi", "DrawSubPartUi", "DrawLaunchUi",
            "DrawGizmoToolbarButtons", "DrawSymmetryToolbarButtons" })
            yield return AccessTools.Method(typeof(VehicleEditor), name)
                ?? throw new MissingMethodException(typeof(VehicleEditor).FullName, name);
        yield return AccessTools.Method(typeof(VehicleLaunchMenu), nameof(VehicleLaunchMenu.DrawLaunchUi));
        var windowType = typeof(VehicleEditor).GetNestedType("PartWindow", BindingFlags.Public | BindingFlags.NonPublic)!;
        foreach (var method in windowType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            if (method.Name == "OnDrawUi") yield return method;
    }

    [HarmonyPrefix]
    public static void Enter(out int __state)
    {
        __state = Runtime.EditorUiDepth;
        Runtime.EditorUiDepth++;
    }

    [HarmonyFinalizer]
    public static void Leave(int __state)
    {
        Runtime.EditorUiDepth = __state;
    }
}

[HarmonyPatch(typeof(ConsoleWidgets), nameof(ConsoleWidgets.Segmented))]
public static class EditorSegmentsPatch
{
    private static readonly HashSet<string> Ids = new(StringComparer.Ordinal)
    {
        "GizmoTool", "GizmoFrame", "SymmetryCycle", "InheritToggle", "SnapToggle", "CameraMode", "Projection", "AutoResourceGroups"
    };

    [HarmonyPrefix]
    public static void Localize(ReadOnlySpan<char> id, ref ReadOnlySpan<string> segments)
    {
        if (id.SequenceEqual("KittenRosterTabs"))
        { segments=segments.ToArray().Select(ExpandedUi.Text).ToArray(); return; }
        if (!Ids.Contains(id.ToString()) || Runtime.CurrentPack.Editor.Count == 0) return;
        string[] translated = segments.ToArray();
        bool changed = false;
        for (int i = 0; i < translated.Length; i++)
        {
            string text = Runtime.EditorText(translated[i]);
            changed |= text != translated[i];
            translated[i] = text;
        }
        if (changed) segments = translated;
    }
}

[HarmonyPatch(typeof(ConsoleWidgets), nameof(ConsoleWidgets.BinZone))]
public static class EditorBinPatch
{
    [HarmonyPrefix]
    public static void Localize(ReadOnlySpan<char> id, ref ReadOnlySpan<char> label)
    {
        if (id.SequenceEqual("SymmetryBin")) label = Runtime.EditorText(label.ToString()).AsSpan();
    }
}

[HarmonyPatch(typeof(VehicleEditor), "DrawCategoryRow")]
public static class EditorCategoryPatch
{
    public static int LocalizedUpper(ReadOnlySpan<char> label, Span<char> destination)
        => Runtime.EditorText(label.ToString()).AsSpan().ToUpperInvariant(destination);

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Translate(IEnumerable<CodeInstruction> instructions)
    {
        foreach (var instruction in instructions)
        {
            // InvisibleButton still receives the original category tag.
            if (instruction.operand is MethodInfo method && method.DeclaringType == typeof(System.MemoryExtensions)
                && method.Name == nameof(System.MemoryExtensions.ToUpperInvariant))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = AccessTools.Method(typeof(EditorCategoryPatch), nameof(LocalizedUpper));
            }
            yield return instruction;
        }
    }
}

[HarmonyPatch]
public static class EditorWindowTitlePatch
{
    public static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(ConsoleStyle), "BeginWindowCore");
        yield return AccessTools.Method(typeof(ConsoleStyle), "BeginModalCore");
    }

    [HarmonyPrefix]
    public static void Localize(ReadOnlySpan<char> id, ref ReadOnlySpan<char> title)
    {
        if (id.SequenceEqual("LaunchWindow") || id.SequenceEqual("VehicleLaunch")
            || id.SequenceEqual("NewVehicleConfirm"))
            title = Runtime.EditorText(title.ToString()).AsSpan();
    }
}

[HarmonyPatch]
public static class EditorFieldCaptionPatch
{
    public static IEnumerable<MethodBase> TargetMethods()
    {
        var helperMethods = typeof(ImGuiHelper).GetMethods(BindingFlags.Public | BindingFlags.Static);
        foreach (var method in helperMethods)
        {
            var parameters = method.GetParameters();
            if (parameters.Length == 0 || parameters[0].ParameterType != typeof(ImString)) continue;
            if (!method.IsGenericMethodDefinition && method.Name.StartsWith("Draw", StringComparison.Ordinal))
                yield return method;
            else if (method.IsGenericMethodDefinition && method.Name == "DrawCombo"
                && parameters.Length > 1 && parameters[1].ParameterType == method.GetGenericArguments()[0].MakeByRefType()
                && method.GetGenericArguments()[0].GetGenericParameterConstraints().Contains(typeof(IComboable)))
            {
                foreach (Type type in new[] { typeof(CelestialObject), typeof(LocationObject) })
                    yield return method.MakeGenericMethod(type);
            }
        }
    }

    public static ImString DisplayCaption(ImString label)
    {
        label = ExpandedUi.FieldCaption(label);
        if (Runtime.StartupUiDepth > 0) return Runtime.ImText(Runtime.StartupText(label.ToString()));
        if (Runtime.EditorUiDepth <= 0) return label;
        string original = label.ToString();
        string text = Runtime.EditorText(original);
        return text == original ? label : Runtime.ImText(text);
    }

    public static bool CelestialCombo(ImString id, ImString preview, ImGuiComboFlags flags)
        => ImGui.BeginCombo(id, Runtime.EditorUiDepth > 0 || Runtime.StartupUiDepth > 0 || ExpandedUi.InUi ? Runtime.ImText(Runtime.EditorNativeText(preview.ToString(), 1)) : preview, flags);

    public static bool LocationCombo(ImString id, ImString preview, ImGuiComboFlags flags)
        => ImGui.BeginCombo(id, Runtime.EditorUiDepth > 0 || Runtime.StartupUiDepth > 0 || ExpandedUi.InUi ? Runtime.ImText(Runtime.EditorNativeText(preview.ToString(), 2)) : preview, flags);

    public static bool CelestialOption(ImString label, bool selected, ImGuiSelectableFlags flags, in float2? size)
        => ImGui.Selectable(Runtime.EditorUiDepth > 0 || Runtime.StartupUiDepth > 0 || ExpandedUi.InUi ? Runtime.ImText(Runtime.EditorNativeSelectable(label.ToString(), 1)) : label, selected, flags, in size);

    public static bool LocationOption(ImString label, bool selected, ImGuiSelectableFlags flags, in float2? size)
        => ImGui.Selectable(Runtime.EditorUiDepth > 0 || Runtime.StartupUiDepth > 0 || ExpandedUi.InUi ? Runtime.ImText(Runtime.EditorNativeSelectable(label.ToString(), 2)) : label, selected, flags, in size);

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Translate(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        bool replaced = false;
        var source = instructions.ToList();
        var lookup = AccessTools.Method(typeof(EditorFieldCaptionPatch), nameof(DisplayCaption));
        bool nativeCombo = __originalMethod.IsGenericMethod && __originalMethod.Name == "DrawCombo";
        Type? nativeType = nativeCombo ? __originalMethod.GetGenericArguments()[0] : null;
        for (int i = 0; i < source.Count; i++)
        {
            var instruction = source[i];
            if ((nativeType == typeof(CelestialObject) || nativeType == typeof(LocationObject))
                && instruction.operand is MethodInfo native && native.DeclaringType == typeof(ImGui))
            {
                string? wrapper = native.Name == "BeginCombo" ? (nativeType == typeof(CelestialObject) ? nameof(CelestialCombo) : nameof(LocationCombo))
                    : native.Name == "Selectable" ? (nativeType == typeof(CelestialObject) ? nameof(CelestialOption) : nameof(LocationOption)) : null;
                if (wrapper is not null)
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = AccessTools.Method(typeof(EditorFieldCaptionPatch), wrapper);
                }
            }
            // Translate only Text(variable), never variable itself or a value/ID.
            if (!replaced && i > 0 && source[i - 1].opcode == OpCodes.Ldarg_0
                && instruction.operand is MethodInfo method && method.DeclaringType == typeof(ImGui)
                && method.Name == nameof(ImGui.Text) && method.GetParameters().Length == 1
                && method.GetParameters()[0].ParameterType == typeof(ImString))
            {
                yield return new CodeInstruction(OpCodes.Call, lookup);
                replaced = true;
            }
            yield return instruction;
        }
    }
}

[HarmonyPatch]
public static class EditorLiteralPatch
{
    private static readonly HashSet<string> Buttons = new(StringComparer.Ordinal)
    {
        "All Sizes", "Toggle Resource Display", "Make Vehicle Root", "Reroot", "Hide Fuel Flow",
        "Visualize Fuel Flow", "Connect Fuel Line", "Cancel Fuel Line",
        "Contents", "Connections", "SubParts", "Add SubParts"
    };
    private static readonly HashSet<string> TextLabels = new(StringComparer.Ordinal)
    {
        "Do you want to clear the current vehicle and create a new one?", "Crew", "Fuel Lines", "Solid Grain",
        "Thrust Curve", "Parachute", "Battery"
    };

    public static ImString StaticText(ImString label)
    {
        string original = label.ToString();
        if (Buttons.Contains(original)) return Runtime.ImText(Runtime.EditorMenuText(original));
        if (TextLabels.Contains(original))
            return Runtime.ImText(Runtime.EditorText(original));
        string tooltip = Runtime.TooltipText(original);
        if (tooltip != original) return Runtime.ImText(tooltip);
        return label;
    }

    public static bool DiameterCombo(ImString id, ImString preview, ImGuiComboFlags flags)
        => ImGui.BeginCombo(id, id.ToString() == "##DiameterFilter" ? Runtime.ImText(Runtime.EditorText(preview.ToString())) : preview, flags);

    public static bool DiameterOption(ImString label, bool selected, ImGuiSelectableFlags flags, in float2? size)
        => ImGui.Selectable(label.ToString() == "All Sizes" ? Runtime.ImText(Runtime.EditorMenuText(label.ToString())) : label, selected, flags, in size);

    public static ReadOnlySpan<char> PropellantDisplay(ReadOnlySpan<char> text)
        => Runtime.EditorText(text.ToString()).AsSpan();

    public static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (string name in new[] { "OnDrawUi", "DrawGizmoToolbarButtons", "DrawSymmetryToolbarButtons",
            "DrawPartUi", "DrawSubPartUi", "DrawLaunchUi", "DrawGrainSection", "DrawNozzleExpansionSection",
            "DrawParachuteSection", "DrawTankGroupControls", "DrawTankGroupFillSliders" })
            yield return AccessTools.Method(typeof(VehicleEditor), name);
        yield return AccessTools.Method(typeof(VehicleLaunchMenu), nameof(VehicleLaunchMenu.DrawLaunchUi));
        yield return AccessTools.Method(typeof(VehicleEditor).GetNestedType("PartWindow", BindingFlags.Public | BindingFlags.NonPublic), "OnDrawUi");
        foreach (var method in typeof(PartArchetypes).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
            if (method.Name.StartsWith("Append", StringComparison.Ordinal)) yield return method;
    }

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Translate(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        var lookup = AccessTools.Method(typeof(Runtime), nameof(Runtime.EditorLiteral));
        var staticLookup = AccessTools.Method(typeof(EditorLiteralPatch), nameof(StaticText));
        foreach (var instruction in instructions)
        {
            if (__originalMethod.DeclaringType?.Name == "PartWindow" && instruction.operand is MethodInfo native
                && native.DeclaringType == typeof(ImGui) && native.Name is "BeginCombo" or "Selectable")
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = AccessTools.Method(typeof(EditorLiteralPatch), native.Name == "BeginCombo" ? nameof(DiameterCombo) : nameof(DiameterOption));
            }
            yield return instruction;
            if (instruction.opcode == OpCodes.Ldstr && instruction.operand is string text
                && !(__originalMethod.DeclaringType?.Name == "PartWindow" && text == "All")
                && !Buttons.Contains(text)
                && Runtime.Packs.Values.Any(pack => pack.Editor.ContainsKey(text)
                    || pack.Tooltips.ContainsKey(text)
                    || (text.EndsWith(": ", StringComparison.Ordinal) && pack.Editor.Keys.Any(key => text.EndsWith(key + ": ", StringComparison.Ordinal)))))
                yield return new CodeInstruction(OpCodes.Call, lookup);
            if (instruction.opcode == OpCodes.Call && instruction.operand is MethodInfo method
                && method.DeclaringType == typeof(ImString) && method.Name == "op_Implicit"
                && method.GetParameters()[0].ParameterType == typeof(ReadOnlySpan<byte>))
                yield return new CodeInstruction(OpCodes.Call, staticLookup);
            if (__originalMethod.DeclaringType == typeof(PartArchetypes) && __originalMethod.Name == "AppendPropellantBranch"
                && instruction.operand is MethodInfo spanMethod && spanMethod.DeclaringType == typeof(System.MemoryExtensions)
                && spanMethod.Name == "AsSpan" && spanMethod.GetParameters().Length == 1
                && spanMethod.GetParameters()[0].ParameterType == typeof(string))
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(EditorLiteralPatch), nameof(PropellantDisplay)));
        }
    }
}
