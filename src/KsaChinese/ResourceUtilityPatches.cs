// Adapted from LiX-Works/KsaUiLanguages (MIT), b504774158cc03b582a0e75bfe94330cb841f11d.
using System.Reflection;
using System.Reflection.Emit;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using HarmonyLib;
using KSA;

namespace KsaChinese;

// Display-only sites in the resource panel and device readouts. Runtime IDs,
// drag payloads, substance names and part names remain the original inputs.
[HarmonyPatch]
public static class ResourceUtilityCaptionPatch
{
    [ThreadStatic] private static int _fieldDepth;
    public static readonly HashSet<string> Fields = new(StringComparer.Ordinal)
    { "Highlight", "Number Tanks", "Number Batteries", "Light Switch" };
    public static readonly HashSet<string> Plain = new(StringComparer.Ordinal)
    {
        "No vehicle", "Total Battery Capacity:", "Total Battery Charge:", "Battery Capacity:", "Charge Stored:",
        "Generator Active:", "Producing:", "Power Consumption Active:", "Consuming:", "Panel Active:",
        "Occluded:", "Distance to Sun:", "Sun AoA:", "Sun Efficiency:", "Produced:", "Stored:",
        "Used Volume:", "Available Volume:", "Total Storage Volume:", "Yes", "No", "X Tank '", "X Battery '",
        " to ", "Direction fuel is allowed to flow. Click to cycle."
    };
    public static readonly HashSet<string> Interactive = new(StringComparer.Ordinal)
    { "Automatic", "Recalculate", "Add New Group", "Add Above", "Add Below", "DELETE",
        "Engine Resources", "Electricity Generators", "Electricity Consumers", "Fuel Lines" };

    public static IEnumerable<MethodBase> TargetMethods()
    {
        Type panel = typeof(ResourceGroupsPanel).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
            .Single(t => typeof(ImGuiWindow).IsAssignableFrom(t));
        yield return AccessTools.DeclaredMethod(panel, "DrawContent") ?? throw new MissingMethodException(panel.FullName, "DrawContent");
        foreach (var (type, name) in new[] {
            (typeof(ResourceGroupList), "DrawResourceGroupContent"), (typeof(PartTree), "DrawResourceInfo"),
            (typeof(PartTree), "DrawResourceWindow"), (typeof(FuelLinkList), "DrawFuelLinksSection"),
            (typeof(Battery), "DrawStateInfo"), (typeof(Generator), "DrawStateInfo"),
            (typeof(SolarPanel), "DrawStateInfo"), (typeof(PowerConsumer), "DrawStateInfo"), (typeof(Tank), "DrawStateInfo") })
            yield return AccessTools.Method(type, name) ?? throw new MissingMethodException(type.FullName, name);
    }

    public static string PlainText(string source) => Plain.Contains(source) ? Runtime.UtilityText(source) : source;
    public static ImString PlainCaption(ImString source) => Runtime.ImText(PlainText(source.ToString()));
    public static string InteractiveText(string source)
    {
        int separator = source.IndexOf("##", StringComparison.Ordinal);
        string visible = separator < 0 ? source : source[..separator];
        return Interactive.Contains(visible) ? Runtime.UtilityMenuText(source) : source;
    }
    public static ImString InteractiveCaption(ImString source) => Runtime.ImText(InteractiveText(source.ToString()));
    public static bool Button(ImString label, in float2? size) => ImGui.Button(InteractiveCaption(label), in size);
    public static bool MenuItem(ImString label, ImString shortcut, bool selected, bool enabled)
        => ImGui.MenuItem(InteractiveCaption(label), shortcut, selected, enabled);
    public static bool Checkbox(ImString label, ref bool value) => ImGui.Checkbox(InteractiveCaption(label), ref value);
    public static bool Header(ImString label, ImGuiTreeNodeFlags flags) => ImGui.CollapsingHeader(InteractiveCaption(label), flags);

    // Only the group's first TreeNodeEx/Text call is redirected; the later
    // entries are actual user-defined part names, even if they say "Group 2".
    public static string GroupText(string source, bool interactive)
    {
        const string prefix = "Group ";
        if (!source.StartsWith(prefix, StringComparison.Ordinal)
            || !int.TryParse(source[prefix.Length..], out _)) return source;
        string visible = Runtime.UtilityText(prefix) + source[prefix.Length..];
        return interactive ? visible + "###" + source : visible;
    }
    public static bool GroupTree(ImString label, ImGuiTreeNodeFlags flags)
        => ImGui.TreeNodeEx(Runtime.ImText(GroupText(label.ToString(), true)), flags);
    public static void GroupReadout(ImString label) => ImGui.Text(Runtime.ImText(GroupText(label.ToString(), false)));
    public static bool DeleteButton(ReadOnlySpan<char> label)
        => ConsoleWidgets.DangerButton(Runtime.UtilityText(label.ToString()).AsSpan(), label, float2.Zero);

    public static ImString FieldCaption(ImString label)
        => _fieldDepth > 0 && Fields.Contains(label.ToString())
            ? Runtime.ImText(Runtime.UtilityText(label.ToString())) : label;
    public static bool HelperCheckbox(ImString label, ref bool value, bool changed)
    {
        int previous = _fieldDepth;
        try { _fieldDepth++; return ImGuiHelper.DrawCheckbox(label, ref value, changed); }
        finally { _fieldDepth = previous; }
    }

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Translate(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        int sites = 0, trees = 0, texts = 0;
        bool groups = __originalMethod.DeclaringType == typeof(ResourceGroupList);
        foreach (var instruction in instructions)
        {
            bool utf8 = false;
            if (instruction.operand is MethodInfo native)
            {
                var args = native.GetParameters();
                string? wrapper = null;
                if (native.DeclaringType == typeof(Part) && native.Name == "get_DisplayName")
                {
                    // These resource renderer names are captions/context tags;
                    // IDs are constructed separately from native instance IDs.
                    // PartCaption checks the actual template and preserves custom names.
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = AccessTools.Method(typeof(Runtime), nameof(Runtime.PartCaption));
                    sites++;
                }
                else if (native.DeclaringType == typeof(ImGui))
                {
                    if (native.Name == nameof(ImGui.Button) && args.Length == 2) wrapper = nameof(Button);
                    else if (native.Name == nameof(ImGui.MenuItem) && args.Length == 4 && args[2].ParameterType == typeof(bool)) wrapper = nameof(MenuItem);
                    else if (native.Name == nameof(ImGui.Checkbox) && args.Length == 2) wrapper = nameof(Checkbox);
                    else if (native.Name == nameof(ImGui.CollapsingHeader) && args.Length == 2
                        && __originalMethod.DeclaringType != typeof(Tank)) wrapper = nameof(Header);
                    else if (groups && native.Name == nameof(ImGui.TreeNodeEx) && args.Length == 2 && ++trees == 1) wrapper = nameof(GroupTree);
                    else if (groups && native.Name == nameof(ImGui.Text) && args.Length == 1 && ++texts == 1) wrapper = nameof(GroupReadout);
                }
                else if (native.DeclaringType == typeof(ImGuiHelper) && native.Name == nameof(ImGuiHelper.DrawCheckbox)
                    && args.Length == 3 && args[0].ParameterType == typeof(ImString)) wrapper = nameof(HelperCheckbox);
                else if (native.DeclaringType == typeof(ConsoleWidgets) && native.Name == nameof(ConsoleWidgets.DangerButton)
                    && args.Length == 1) wrapper = nameof(DeleteButton);
                else if (native.DeclaringType == typeof(ImString) && native.Name == "op_Implicit" && args.Length == 1
                    && args[0].ParameterType == typeof(ReadOnlySpan<byte>)) utf8 = true;
                if (wrapper is not null)
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = AccessTools.Method(typeof(ResourceUtilityCaptionPatch), wrapper);
                    sites++;
                }
            }
            yield return instruction;
            if (instruction.opcode == OpCodes.Ldstr && instruction.operand is string source && Plain.Contains(source))
            { yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ResourceUtilityCaptionPatch), nameof(PlainText))); sites++; }
            else if (utf8)
            { yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ResourceUtilityCaptionPatch), nameof(PlainCaption))); sites++; }
        }
        if (sites == 0 || (groups && (trees != 2 || texts != 2)))
            throw new InvalidOperationException($"Resource display sites changed: {__originalMethod}: {sites}, trees {trees}, texts {texts}.");
        Runtime.Log($"Resource display: {__originalMethod}: {sites} caption sites; source IDs and values retained.");
    }
}
