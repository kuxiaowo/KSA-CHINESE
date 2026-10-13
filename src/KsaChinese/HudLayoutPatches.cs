// Adapted from LiX-Works/KsaUiLanguages (MIT), b504774158cc03b582a0e75bfe94330cb841f11d.
using System.Reflection;
using System.Reflection.Emit;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using HarmonyLib;
using KSA;

namespace KsaChinese;

[HarmonyPatch]
public static class HudLayoutPatches
{
    private static readonly Type LayoutWindowType = typeof(LayoutSaves).GetNestedType(
        "LayoutSavesWindow", BindingFlags.Public | BindingFlags.NonPublic)
        ?? throw new MissingMemberException(typeof(LayoutSaves).FullName, "LayoutSavesWindow");

    private static readonly HashSet<string> StaticLabels = new(StringComparer.Ordinal)
    {
        "New", "Refresh", "Save Current Layout", "Set Current as Default", "Manage Layouts",
        "Select", "Overwrite", "Delete"
    };

    private static readonly HashSet<string> StaticTooltips = new(StringComparer.Ordinal)
    {
        "Save the current gauge layout as a new layout",
        "Reload all layouts from the layouts folder",
        "Double click a layout to apply it, right click for its context menu. Clicking a header sorts by that column."
    };

    private static readonly HashSet<string> FooterUnits = new(StringComparer.Ordinal)
    {
        " LAYOUT", " LAYOUTS"
    };

    public static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(LayoutSaves), nameof(LayoutSaves.DrawMenu))
            ?? throw new MissingMethodException(typeof(LayoutSaves).FullName, nameof(LayoutSaves.DrawMenu));
        yield return AccessTools.Method(LayoutWindowType, "DrawMenuBar")
            ?? throw new MissingMethodException(LayoutWindowType.FullName, "DrawMenuBar");
        yield return AccessTools.Method(LayoutWindowType, "DrawViewMenu")
            ?? throw new MissingMethodException(LayoutWindowType.FullName, "DrawViewMenu");
        yield return AccessTools.Method(LayoutWindowType, "DrawConsoleFooter")
            ?? throw new MissingMethodException(LayoutWindowType.FullName, "DrawConsoleFooter");
        yield return AccessTools.Method(LayoutWindowType, "DrawContent")
            ?? throw new MissingMethodException(LayoutWindowType.FullName, "DrawContent");
        yield return AccessTools.Method(typeof(LayoutSave), nameof(LayoutSave.DrawInTable))
            ?? throw new MissingMethodException(typeof(LayoutSave).FullName, nameof(LayoutSave.DrawInTable));
    }

    public static ImString LocalizeStaticText(ImString text)
    {
        string original = text.ToString();
        if (StaticLabels.Contains(original))
            return Runtime.ImText(Runtime.HudMenuText(original));
        if (StaticTooltips.Contains(original))
            return Runtime.ImText(Runtime.TooltipText(original));
        return text;
    }

    public static bool BuiltinDefaultMenuItem(ImString label, bool selected, bool disabled)
    {
        string original = label.ToString();
        string localized = BuiltinDefaultLabel(original);
        return DrawTranslatedMenuItem(localized, selected, disabled);
    }

    public static string BuiltinDefaultLabel(string original)
    {
        bool hasDefaultSuffix = original == "Default (default)";
        if (original != "Default" && !hasDefaultSuffix) return original;

        string canonical = Runtime.HudMenuText("Default");
        int marker = canonical.IndexOf("###", StringComparison.Ordinal);
        string visible = marker >= 0 ? canonical[..marker] : "Default";
        string id = marker >= 0 ? canonical[(marker + 3)..] : "Default";
        if (hasDefaultSuffix)
        {
            visible += " " + Runtime.HudText("(default)");
            id += " (default)";
        }
        return visible + "###" + id;
    }

    public static bool CustomLayoutMenuItem(ImString label, bool selected, bool disabled, bool isDefault)
    {
        string original = label.ToString();
        string localized = CustomLayoutMenuLabel(original, isDefault);
        if (localized == original)
            return ImGuiHelper.DrawMenuItem(label, selected, disabled);

        return DrawTranslatedMenuItem(localized, selected, disabled);
    }

    private static bool DrawTranslatedMenuItem(string label, bool selected, bool disabled)
    {
        int previous = HudLayoutMenuBufferPatch.RequiredCharacters;
        HudLayoutMenuBufferPatch.RequiredCharacters = checked(label.Length + 3);
        try { return ImGuiHelper.DrawMenuItem(Runtime.ImText(label), selected, disabled); }
        finally { HudLayoutMenuBufferPatch.RequiredCharacters = previous; }
    }

    public static string CustomLayoutMenuLabel(string original, bool isDefault)
    {
        if (!isDefault) return original;
        const string suffix = " (default)";
        int marker = original.LastIndexOf(suffix, StringComparison.Ordinal);
        if (marker < 0) return original;

        // The layout name, dirty marker, and hotkey remain byte-for-byte user data.
        string visible = original[..marker] + " " + Runtime.HudText("(default)") + original[(marker + suffix.Length)..];
        return visible + "###" + original;
    }

    public static bool DefaultLayoutRow(ImString label, bool selected, ImGuiSelectableFlags flags,
        in float2? size, bool isDefault)
    {
        string original = label.ToString();
        string localized = DefaultLayoutRowLabel(original, isDefault);
        if (localized == original)
            return ImGui.Selectable(label, selected, flags, in size);
        return ImGui.Selectable(Runtime.ImText(localized), selected, flags, in size);
    }

    public static string DefaultLayoutRowLabel(string original, bool isDefault)
    {
        if (!isDefault) return original;
        const string suffix = " (default)";
        if (!original.EndsWith(suffix, StringComparison.Ordinal)) return original;

        // Keep the user's layout name intact. Only the fixed default marker changes.
        string visible = original[..^suffix.Length] + " " + Runtime.HudText("(default)");
        return visible + "###" + original;
    }

    public static unsafe void SetupTableColumn(ImString label, ImGuiTableColumnFlags flags,
        float widthOrWeight, ImGuiID userId)
    {
        ImGui.TableSetupColumn(Runtime.ImText(Runtime.HudMenuText(label.ToString())), flags, widthOrWeight, userId);
    }

    public static ReadOnlySpan<char> LayoutFooterUnit(string text)
    {
        if (!FooterUnits.Contains(text)) return text.AsSpan();
        return Runtime.HudText(text).AsSpan();
    }

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Translate(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        bool layoutMenu = __originalMethod.DeclaringType == typeof(LayoutSaves)
            && __originalMethod.Name == nameof(LayoutSaves.DrawMenu);
        bool layoutFooter = __originalMethod.DeclaringType == LayoutWindowType
            && __originalMethod.Name == "DrawConsoleFooter";
        bool layoutTable = __originalMethod.DeclaringType == LayoutWindowType
            && __originalMethod.Name == "DrawContent";
        bool layoutRow = __originalMethod.DeclaringType == typeof(LayoutSave)
            && __originalMethod.Name == nameof(LayoutSave.DrawInTable);
        var drawBuiltinDefault = AccessTools.Method(typeof(HudLayoutPatches), nameof(BuiltinDefaultMenuItem));
        var localizeStaticText = AccessTools.Method(typeof(HudLayoutPatches), nameof(LocalizeStaticText));
        var layoutFooterUnit = AccessTools.Method(typeof(HudLayoutPatches), nameof(LayoutFooterUnit));
        var setupTableColumn = AccessTools.Method(typeof(HudLayoutPatches), nameof(SetupTableColumn));
        var defaultLayoutRow = AccessTools.Method(typeof(HudLayoutPatches), nameof(DefaultLayoutRow));
        var customLayoutMenuItem = AccessTools.Method(typeof(HudLayoutPatches), nameof(CustomLayoutMenuItem));
        var isDefaultGetter = AccessTools.PropertyGetter(typeof(LayoutSave), nameof(LayoutSave.IsDefault))
            ?? throw new MissingMethodException(typeof(LayoutSave).FullName, "get_IsDefault");
        int drawMenuItemOrdinal = 0;
        bool rowSelectableReplaced = false;
        int layoutSaveLocal = -1;
        if (layoutMenu)
        {
            layoutSaveLocal = __originalMethod.GetMethodBody()?.LocalVariables
                .Where(local => local.LocalType == typeof(LayoutSave))
                .Select(local => (int?)local.LocalIndex)
                .SingleOrDefault()
                ?? throw new InvalidOperationException("LayoutSaves.DrawMenu no longer has one LayoutSave loop local.");
        }

        foreach (var instruction in instructions)
        {
            if (layoutMenu && instruction.operand is MethodInfo menuItem
                && menuItem.DeclaringType == typeof(ImGuiHelper) && menuItem.Name == nameof(ImGuiHelper.DrawMenuItem)
                && menuItem.GetParameters().Length == 3
                && menuItem.GetParameters()[0].ParameterType == typeof(ImString)
                && menuItem.GetParameters()[1].ParameterType == typeof(bool)
                && menuItem.GetParameters()[2].ParameterType == typeof(bool))
            {
                drawMenuItemOrdinal++;
                if (drawMenuItemOrdinal == 1)
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = drawBuiltinDefault;
                }
                else if (drawMenuItemOrdinal == 2)
                {
                    if (layoutSaveLocal > byte.MaxValue)
                        throw new InvalidOperationException("LayoutSaves.DrawMenu loop local exceeds short-form IL range.");
                    var loadLayoutSave = new CodeInstruction(OpCodes.Ldloc_S, (byte)layoutSaveLocal);
                    loadLayoutSave.labels.AddRange(instruction.labels);
                    instruction.labels.Clear();
                    loadLayoutSave.blocks.AddRange(instruction.blocks);
                    instruction.blocks.Clear();
                    yield return loadLayoutSave;
                    yield return new CodeInstruction(OpCodes.Callvirt, isDefaultGetter);
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = customLayoutMenuItem;
                }
            }

            if (layoutFooter && instruction.operand is MethodInfo asSpan
                && asSpan.DeclaringType == typeof(System.MemoryExtensions)
                && asSpan.Name == nameof(System.MemoryExtensions.AsSpan)
                && asSpan.GetParameters().Length == 1
                && asSpan.GetParameters()[0].ParameterType == typeof(string))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = layoutFooterUnit;
            }

            if (layoutTable && instruction.operand is MethodInfo tableColumn
                && tableColumn.DeclaringType == typeof(ImGui) && tableColumn.Name == nameof(ImGui.TableSetupColumn)
                && tableColumn.GetParameters().Length == 4
                && tableColumn.GetParameters()[0].ParameterType == typeof(ImString))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = setupTableColumn;
            }

            if (layoutRow && !rowSelectableReplaced && instruction.operand is MethodInfo selectable
                && selectable.DeclaringType == typeof(ImGui) && selectable.Name == nameof(ImGui.Selectable)
                && selectable.GetParameters().Length == 4
                && selectable.GetParameters()[0].ParameterType == typeof(ImString)
                && selectable.GetParameters()[1].ParameterType == typeof(bool))
            {
                var loadInstance = new CodeInstruction(OpCodes.Ldarg_0);
                loadInstance.labels.AddRange(instruction.labels);
                instruction.labels.Clear();
                loadInstance.blocks.AddRange(instruction.blocks);
                instruction.blocks.Clear();
                yield return loadInstance;
                yield return new CodeInstruction(OpCodes.Call, isDefaultGetter);
                instruction.opcode = OpCodes.Call;
                instruction.operand = defaultLayoutRow;
                rowSelectableReplaced = true;
            }

            yield return instruction;
            if (!layoutFooter && !layoutTable && instruction.opcode == OpCodes.Call
                && instruction.operand is MethodInfo conversion
                && conversion.DeclaringType == typeof(ImString) && conversion.Name == "op_Implicit"
                && conversion.GetParameters().Length == 1
                && conversion.GetParameters()[0].ParameterType == typeof(ReadOnlySpan<byte>))
                yield return new CodeInstruction(OpCodes.Call, localizeStaticText);
        }
    }
}

[HarmonyPatch]
public static class HudLayoutMenuBufferPatch
{
    [ThreadStatic] public static int RequiredCharacters;

    public static MethodBase TargetMethod()
        => AccessTools.Method(typeof(ImGuiHelper), nameof(ImGuiHelper.DrawMenuItem), new[] { typeof(ImString), typeof(bool), typeof(bool) });

    public static int BufferCharacters() => Math.Max(128, RequiredCharacters);
    public static int BufferBytes() => checked(BufferCharacters() * sizeof(char));

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Translate(IEnumerable<CodeInstruction> instructions)
    {
        var list = instructions.ToList();
        var characters = AccessTools.Method(typeof(HudLayoutMenuBufferPatch), nameof(BufferCharacters));
        var bytes = AccessTools.Method(typeof(HudLayoutMenuBufferPatch), nameof(BufferBytes));
        int byteReplacements = 0;
        int characterReplacements = 0;
        for (int i = 0; i < list.Count; i++)
        {
            var instruction = list[i];
            // Grow both the raw allocation and its Span<char> length together.
            // Outside the layout wrapper's scope both retain the original capacity.
            if (instruction.opcode == OpCodes.Ldc_I4 && Equals(instruction.operand, 256)
                && i + 2 < list.Count && list[i + 1].opcode == OpCodes.Conv_U && list[i + 2].opcode == OpCodes.Localloc)
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = bytes;
                byteReplacements++;
            }
            if (instruction.opcode == OpCodes.Ldc_I4 && Equals(instruction.operand, 128))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = characters;
                characterReplacements++;
            }
            yield return instruction;
        }
        if (byteReplacements != 1 || characterReplacements != 1)
            throw new InvalidOperationException("Expected exactly one menu allocation and matching character-capacity constant.");
    }
}
