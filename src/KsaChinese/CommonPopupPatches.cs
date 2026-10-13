// Adapted from LiX-Works/KsaUiLanguages (MIT), b504774158cc03b582a0e75bfe94330cb841f11d.
using System.Reflection;
using System.Reflection.Emit;
using Brutal.ImGuiApi;
using HarmonyLib;
using KSA;

namespace KsaChinese;

[HarmonyPatch]
public static class CommonPopupCaptionPatch
{
    private static readonly HashSet<string> Labels = new(StringComparer.Ordinal)
    {
        "ABANDON VEHICLE", "Are you sure you want to abandon this vehicle?", "REQUIRES RESTART",
        "The game needs to be restarted for the changes to take effect.", "UPDATE AVAILABLE", "YOUR VERSION",
        "AVAILABLE VERSION", "MOD FOUND", "SELECT PART TREE",
        "A new update for the game is available. Select an option below if you want to update the game, otherwise select cancel."
    };

    public static IEnumerable<MethodBase> TargetMethods()
    {
        foreach(var type in new[]{typeof(AbandonPopup),typeof(RequiresRestartPopup),typeof(UpdateAvailablePopup),typeof(ConfirmModPopup),typeof(PartTreeSelectionPopup)})
            yield return AccessTools.Method(type,"OnDrawUi");
    }

    public static string PlainText(string source)=>Labels.Contains(source)?Runtime.UtilityText(source):source;
    public static ImString StaticText(ImString source)=>Runtime.ImText(PlainText(source.ToString()));

    public static string ModMessage(string source)
    {
        const string prefix="A new mod with id '";
        const string suffix="' has been found, do you want it enabled?";
        if(!source.StartsWith(prefix,StringComparison.Ordinal)||!source.EndsWith(suffix,StringComparison.Ordinal))return source;
        string template=prefix+"{0}"+suffix;
        return Runtime.UtilityText(template).Replace("{0}",source[prefix.Length..^suffix.Length],StringComparison.Ordinal);
    }

    public static string PartTreeDescription(string source)
        =>source is "Select a part tree to save." or "Select a part tree to make the active vehicle."
            ?Runtime.UtilityText(source):source;

    public static string CheckboxCaption(string source)
        =>source is "Always Check" or "Always Show" ? Runtime.UtilityMenuText(source) : source;

    public static void UpdateCheckbox(PopupCheckbox<UpdateAvailablePopup> widget, UpdateAvailablePopup parent, float width)
        =>DrawCheckbox(widget,parent,width);
    public static void WelcomeCheckbox(PopupCheckbox<WelcomePopup> widget, WelcomePopup parent, float width)
        =>DrawCheckbox(widget,parent,width);

    private static void DrawCheckbox<T>(PopupCheckbox<T> widget,T parent,float width) where T:Popup
    {
        var type=typeof(PopupCheckbox<T>);
        string source=(string)AccessTools.Field(type,"<text>P").GetValue(widget)!;
        var value=(BoolRef)AccessTools.Field(type,"<value>P").GetValue(widget)!;
        var callback=(Action<T>?)AccessTools.Field(type,"<callback>P").GetValue(widget);
        if(ImGui.Checkbox(Runtime.ImText(CheckboxCaption(source)),ref value.Value))callback?.Invoke(parent);
    }

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Translate(IEnumerable<CodeInstruction> instructions,MethodBase __originalMethod)
    {
        var plain=AccessTools.Method(typeof(CommonPopupCaptionPatch),nameof(PlainText));
        var text=AccessTools.Method(typeof(CommonPopupCaptionPatch),nameof(StaticText));
        foreach(var instruction in instructions)
        {
            if(__originalMethod.DeclaringType==typeof(UpdateAvailablePopup) && instruction.operand is MethodInfo checkbox
                && checkbox.DeclaringType==typeof(PopupCheckbox<UpdateAvailablePopup>) && checkbox.Name==nameof(PopupCheckbox<UpdateAvailablePopup>.DrawUi))
            {
                instruction.opcode=OpCodes.Call;
                instruction.operand=AccessTools.Method(typeof(CommonPopupCaptionPatch),nameof(UpdateCheckbox));
            }
            yield return instruction;
            if(instruction.opcode==OpCodes.Ldstr && instruction.operand is string source && Labels.Contains(source))
                yield return new CodeInstruction(OpCodes.Call,plain);
            else if(instruction.operand is MethodInfo native && native.DeclaringType==typeof(ImString) && native.Name=="op_Implicit"
                && native.GetParameters()[0].ParameterType==typeof(ReadOnlySpan<byte>))
                yield return new CodeInstruction(OpCodes.Call,text);
            else if(instruction.opcode==OpCodes.Ldfld && instruction.operand is FieldInfo field)
            {
                if(__originalMethod.DeclaringType==typeof(ConfirmModPopup) && field.Name=="_message")
                    yield return new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(CommonPopupCaptionPatch),nameof(ModMessage)));
                else if(__originalMethod.DeclaringType==typeof(PartTreeSelectionPopup) && field.Name=="_description")
                    yield return new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(CommonPopupCaptionPatch),nameof(PartTreeDescription)));
            }
        }
    }
}
