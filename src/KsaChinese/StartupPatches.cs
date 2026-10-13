// Adapted from LiX-Works/KsaUiLanguages (MIT), b504774158cc03b582a0e75bfe94330cb841f11d.
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using HarmonyLib;
using KSA;

namespace KsaChinese;

[HarmonyPatch(typeof(ConfigOnStartPopup), "OnDrawUi")]
public static class StartupUiPatch
{
    [HarmonyPrefix]
    public static void Enter(out int __state)
    {
        __state = Runtime.StartupUiDepth;
        Runtime.StartupUiDepth++;
    }

    [HarmonyFinalizer]
    public static void Leave(int __state) => Runtime.StartupUiDepth = __state;

    private static readonly HashSet<string> Regions = new(StringComparer.Ordinal)
    {
        "Select System", "Game Type", "Starting Situation", "Vessels", "Terrain", "Texture Streaming",
        "Cascaded Shadows", "Light System", "Thumbnails"
    };

    public static ImString RegionText(ImString label)
        => Regions.Contains(label.ToString()) ? Runtime.ImText(Runtime.StartupMenuText(label.ToString())) : label;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool SystemCombo(ImString variable, ref SystemInfo value, List<SystemInfo> options, bool isChanged)
    {
        if (Runtime.StartupUiDepth <= 0 || isChanged) return ImGuiHelper.DrawCombo(variable, ref value, options, isChanged);
        // Reference-type generic bodies are shared by the runtime. Draw this one
        // startup field directly, retaining the helper's original ID counter.
        ImGui.Text(Runtime.ImText(Runtime.StartupText(variable.ToString())));
        ImGui.NextColumn();
        ImGui.PushItemWidth(-1f);
        var counter = AccessTools.Field(typeof(ImGuiHelper), "_widgetId");
        int index = (int)counter.GetValue(null)!;
        counter.SetValue(null, index + 1);
        bool changed = false;
        if (ImGui.BeginCombo("###" + variable.ToString() + index, Runtime.StartupText(value.GetName()), ImGuiComboFlags.HeightRegular))
        {
            foreach (var option in options)
                if (ImGui.Selectable(Runtime.StartupMenuText(option.GetName()), option.GetKey().Equals(value.GetKey(), StringComparison.OrdinalIgnoreCase)))
                {
                    value = option;
                    changed = true;
                }
            ImGui.EndCombo();
        }
        ImGui.PopItemWidth();
        ImGui.NextColumn();
        return changed;
    }

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Translate(IEnumerable<CodeInstruction> instructions)
    {
        var lookup = AccessTools.Method(typeof(Runtime), nameof(Runtime.StartupText));
        var region = AccessTools.Method(typeof(StartupUiPatch), nameof(RegionText));
        foreach (var instruction in instructions)
        {
            if (instruction.operand is MethodInfo combo && combo.DeclaringType == typeof(ImGuiHelper)
                && combo.Name == "DrawCombo" && combo.IsGenericMethod && combo.GetGenericArguments()[0] == typeof(SystemInfo))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = AccessTools.Method(typeof(StartupUiPatch), nameof(SystemCombo));
            }
            yield return instruction;
            // Plain warning/title strings are separate from IDs in this method.
            if (instruction.opcode == OpCodes.Ldstr && instruction.operand is string text
                && Runtime.Packs.Values.Any(pack => pack.Startup.ContainsKey(text)))
                yield return new CodeInstruction(OpCodes.Call, lookup);
            if (instruction.operand is MethodInfo method && method.DeclaringType == typeof(ImString)
                && method.Name == "op_Implicit" && method.GetParameters()[0].ParameterType == typeof(ReadOnlySpan<byte>))
            {
                // Only region headings get ### IDs. Field labels stay original and
                // their Text(variable) display is translated inside the helper.
                yield return new CodeInstruction(OpCodes.Call, region);
            }
        }
    }
}

[HarmonyPatch]
public static class StartupFieldPatch
{
    public static IEnumerable<MethodBase> TargetMethods()
    {
        var draw = AccessTools.Method(typeof(ConfigOnStartPopup), "OnDrawUi");
        var alreadyHandled = EditorFieldCaptionPatch.TargetMethods().ToHashSet();
        var pending = new Queue<MethodInfo>(PatchProcessor.GetOriginalInstructions(draw)
            .Select(i => i.operand).OfType<MethodInfo>()
            .Where(m => m.DeclaringType == typeof(ImGuiHelper) && m.IsGenericMethod && !m.IsGenericMethodDefinition)
            .Distinct());
        var visited = new HashSet<MethodInfo>();
        while (pending.TryDequeue(out var method))
        {
            if (!visited.Add(method) || alreadyHandled.Contains(method)) continue;
            var instructions = PatchProcessor.GetOriginalInstructions(method);
            foreach (var child in instructions.Select(i => i.operand).OfType<MethodInfo>()
                .Where(m => m.DeclaringType == typeof(ImGuiHelper) && m.IsGenericMethod
                    && m.Name.StartsWith("DrawCombo", StringComparison.Ordinal)))
            {
                var closed = child.ContainsGenericParameters ? child.GetGenericMethodDefinition().MakeGenericMethod(method.GetGenericArguments()) : child;
                pending.Enqueue(closed);
            }
            if (instructions.Any(i => i.operand is MethodInfo m && m.DeclaringType == typeof(ImGui)
                && m.Name == nameof(ImGui.Text))) yield return method;
        }
    }

    public static bool Combo(ImString id, ImString preview, ImGuiComboFlags flags)
        => ImGui.BeginCombo(id, Runtime.StartupUiDepth > 0 ? Runtime.ImText(Runtime.StartupText(preview.ToString())) : preview, flags);

    public static bool Option(ImString label, bool selected, ImGuiSelectableFlags flags, in float2? size)
        => ImGui.Selectable(Runtime.StartupUiDepth > 0 ? Runtime.ImText(Runtime.StartupMenuText(label.ToString())) : label, selected, flags, in size);

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Translate(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        var source = instructions.ToList();
        bool caption = false;
        for (int i = 0; i < source.Count; i++)
        {
            var instruction = source[i];
            if (instruction.operand is MethodInfo native && native.DeclaringType == typeof(ImGui))
            {
                if ((native.Name == "BeginCombo" || native.Name == "Selectable")
                    && __originalMethod.GetGenericArguments()[0] != typeof(ConfigOnStartPopup.VehicleObject))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = AccessTools.Method(typeof(StartupFieldPatch), native.Name == "BeginCombo" ? nameof(Combo) : nameof(Option));
                }
                else if (!caption && native.Name == "Text" && native.GetParameters().Length == 1
                    && native.GetParameters()[0].ParameterType == typeof(ImString) && i > 0 && source[i - 1].opcode == OpCodes.Ldarg_0)
                {
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(EditorFieldCaptionPatch), nameof(EditorFieldCaptionPatch.DisplayCaption)));
                    caption = true;
                }
            }
            yield return instruction;
        }
    }
}

[HarmonyPatch(typeof(PopupCheckbox<ConfigOnStartPopup>), nameof(PopupCheckbox<ConfigOnStartPopup>.DrawUi))]
public static class StartupCheckboxPatch
{
    public static ImString Label(ImString label)
        => Runtime.StartupUiDepth > 0 ? Runtime.ImText(Runtime.StartupMenuText(label.ToString())) : label;

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Translate(IEnumerable<CodeInstruction> instructions)
    {
        foreach (var instruction in instructions)
        {
            yield return instruction;
            if (instruction.operand is MethodInfo method && method.DeclaringType == typeof(ImString)
                && method.Name == "op_Implicit" && method.GetParameters()[0].ParameterType == typeof(string))
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(StartupCheckboxPatch), nameof(Label)));
        }
    }
}
