// Adapted from LiX-Works/KsaUiLanguages (MIT), b504774158cc03b582a0e75bfe94330cb841f11d.
using System.Reflection;
using System.Reflection.Emit;
using Brutal.ImGuiApi;
using HarmonyLib;
using KSA;

namespace KsaChinese;

[HarmonyPatch]
public static class HudContextAssignmentDisplayPatch
{
    // These inherited display aliases are checked against the pinned 5602 Gauges.xml.
    // The GaugeCanvas.Id itself remains the dictionary/override key.
    private static readonly Dictionary<string, string> CanvasDisplayNames = new(StringComparer.Ordinal)
    {
        ["RendezvousControl"] = "Rendezvous Control",
        ["BurnControl"] = "Burn Control",
        ["Burn"] = "Burn Editor",
        ["EngineControl"] = "Engine Control",
        ["KittenFlightControl"] = "Kitten Flight Control",
        ["Telemetry"] = "Telemetry",
        ["AutopilotSettings"] = "Autopilot Settings",
        ["Altitude"] = "Altitude",
        ["Resources"] = "Resources",
        ["GameTime"] = "Game Time",
        ["PitchYawRoll"] = "Attitude Indicators",
        ["Sequence"] = "Sequences",
        ["CrewPortraits"] = "Crew Portraits",
        ["Navball"] = "Navball",
        ["Radiation"] = "Radiation",
    };

    private static Type WindowType => typeof(GaugeContextAssignmentWindow)
        .GetNestedType("Window", BindingFlags.NonPublic)
        ?? throw new MissingMemberException(typeof(GaugeContextAssignmentWindow).FullName, "Window");

    public static MethodBase TargetMethod()
        => AccessTools.Method(WindowType, "DrawContent", new[] { typeof(IViewport) })
            ?? throw new MissingMethodException(WindowType.FullName, "DrawContent");

    public static string ColumnCaption(string original)
        => Runtime.HudMenuText(original);

    public static string CanvasCaption(string rawId)
    {
        if (!CanvasDisplayNames.TryGetValue(rawId, out string? displayName))
            return rawId;

        string translated = Runtime.HudText(displayName);
        // If neither Hud nor Ui has a translation for the XML display name,
        // retain the original table value (the raw ID).
        return translated == displayName ? rawId : translated;
    }

    public static string InstructionCaption(string original)
        => Runtime.HudText(original);

    public static void LocalizedTableColumn(ImString label, ImGuiTableColumnFlags flags, float widthOrWeight, ImGuiID userId)
    {
        string original = label.ToString();
        string translated = ColumnCaption(original);
        ImGui.TableSetupColumn(translated == original ? label : Runtime.ImText(translated), flags, widthOrWeight, userId);
    }

    public static void LocalizedCanvasName(ImString rawName)
    {
        string rawId = rawName.ToString();
        string caption = CanvasCaption(rawId);
        ImGui.Text(caption == rawId ? rawName : Runtime.ImText(caption));
    }

    public static void LocalizedInstruction(ImString original)
    {
        string text = original.ToString();
        string translated = InstructionCaption(text);
        ImGui.TextDisabled(translated == text ? original : Runtime.ImText(translated));
    }

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Translate(IEnumerable<CodeInstruction> instructions)
    {
        var tableColumn = AccessTools.Method(typeof(HudContextAssignmentDisplayPatch), nameof(LocalizedTableColumn))!;
        var canvasName = AccessTools.Method(typeof(HudContextAssignmentDisplayPatch), nameof(LocalizedCanvasName))!;
        foreach (var instruction in instructions)
        {
            if (instruction.operand is MethodInfo method && method.DeclaringType == typeof(ImGui))
            {
                if (method.Name == nameof(ImGui.TableSetupColumn)
                    && method.GetParameters().Length == 4
                    && method.GetParameters()[0].ParameterType == typeof(ImString))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = tableColumn;
                }
                else if (method.Name == nameof(ImGui.Text)
                    && method.GetParameters().Length == 1
                    && method.GetParameters()[0].ParameterType == typeof(ImString))
                {
                    // In DrawContent the sole plain Text call is the Canvas
                    // display cell. Checkbox IDs and PushID strings are not
                    // touched by this wrapper.
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = canvasName;
                }
                else if (method.Name == nameof(ImGui.TextDisabled)
                    && method.GetParameters().Length == 1
                    && method.GetParameters()[0].ParameterType == typeof(ImString))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = AccessTools.Method(typeof(HudContextAssignmentDisplayPatch), nameof(LocalizedInstruction))!;
                }
            }
            yield return instruction;
        }
    }
}
