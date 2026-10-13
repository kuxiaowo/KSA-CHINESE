// Adapted from LiX-Works/KsaUiLanguages (MIT), b504774158cc03b582a0e75bfe94330cb841f11d.
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using HarmonyLib;
using KSA;

namespace KsaChinese;

// Welcome keeps its original token array and keybinding cards. Translated text
// is prepared only for this popup's draw call; the shared rich-text parser and
// other popups are not modified.
[HarmonyPatch(typeof(WelcomePopup), "OnDrawUi")]
public static class WelcomeCaptionPatch
{
    private static readonly FieldInfo SourceText = AccessTools.Field(typeof(WelcomePopup), "_text");
    private static readonly Regex ActionTag = new(@"<Action:(?<name>\w+)\s*/>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex AllTags = new(@"<[^>]*>", RegexOptions.CultureInvariant);
    private static readonly ConditionalWeakTable<WelcomePopup, Dictionary<string, PopupToken[]>> Cache = new();
    private const string ClosingPunctuation = "，。！？；：、）】》”’…,.!?;:)]}";
    private const string OpeningPunctuation = "（【《“‘([{\"";

    public static string TitleText(string source) => source == "WELCOME" ? Runtime.UtilityText(source) : source;

    public static PopupToken[] PrepareTokens(string source, PopupToken[] original, string translated)
    {
        if (source == translated) return original;
        // Preserve every structural/action tag in order. Bad review edits fall
        // back to the native text, rather than silently replacing a hotkey.
        if (!AllTags.Matches(source).Select(m => m.Value).SequenceEqual(AllTags.Matches(translated).Select(m => m.Value)))
            return original;
        var actions = original.OfType<ActionStringToken>().ToArray();
        var matches = ActionTag.Matches(translated);
        if (actions.Length != matches.Count) return original;
        for (int i = 0; i < actions.Length; i++)
            if (actions[i].Action.ToString() != matches[i].Groups["name"].Value) return original;

        var result = new List<PopupToken>();
        int offset = 0, action = 0;
        foreach (Match match in matches)
        {
            AddTextTokens(translated[offset..match.Index], result);
            // Reuse the actual existing binding text, including custom keys.
            result.Add(actions[action++]);
            offset = match.Index + match.Length;
        }
        AddTextTokens(translated[offset..], result);
        return result.ToArray();
    }

    private static void AddTextTokens(string markup, List<PopupToken> result)
    {
        foreach (PopupToken token in StringTokenParser.Parse(markup))
        {
            if (token.GetType() != typeof(StringToken)) { result.Add(token); continue; }
            foreach (string part in SplitChineseText(token.Text)) result.Add(new StringToken(part));
        }
    }

    public static IEnumerable<string> SplitChineseText(string source)
    {
        var parts = new List<string>();
        string pending = "";
        foreach (var rune in source.EnumerateRunes())
        {
            int code = rune.Value;
            bool cjk = code is >= 0x3400 and <= 0x9fff or >= 0xf900 and <= 0xfaff or >= 0x20000 and <= 0x3134f;
            string text = rune.ToString();
            if (cjk)
            {
                if (pending.Length > 0 && pending.All(c => OpeningPunctuation.Contains(c)))
                    parts.Add(pending + text);
                else { if (pending.Length > 0) parts.Add(pending); parts.Add(text); }
                pending = "";
            }
            else if (ClosingPunctuation.Contains(text, StringComparison.Ordinal) && pending.Length == 0 && parts.Count > 0)
                parts[^1] += text;
            else pending += text;
        }
        if (pending.Length > 0) parts.Add(pending);
        return parts;
    }

    public static PopupToken[] DisplayTokens(PopupToken[] original, WelcomePopup popup)
    {
        string source = (string)SourceText.GetValue(popup)!;
        string translated = Runtime.UtilityText(source);
        if (translated == source) return original;
        var versions = Cache.GetOrCreateValue(popup);
        if (!versions.TryGetValue(translated, out var tokens))
            versions.Add(translated, tokens = PrepareTokens(source, original, translated));
        return tokens;
    }

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Translate(IEnumerable<CodeInstruction> instructions)
    {
        int arrays = 0, titles = 0, checkboxes = 0;
        var textList = AccessTools.Field(typeof(WelcomePopup), "_textList");
        foreach (var instruction in instructions)
        {
            if (instruction.operand is MethodInfo widget && widget.DeclaringType == typeof(PopupCheckbox<WelcomePopup>)
                && widget.Name == "DrawUi")
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = AccessTools.Method(typeof(CommonPopupCaptionPatch), nameof(CommonPopupCaptionPatch.WelcomeCheckbox));
                checkboxes++;
            }
            yield return instruction;
            if (instruction.opcode == OpCodes.Ldfld && Equals(instruction.operand, textList))
            {
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(WelcomeCaptionPatch), nameof(DisplayTokens)));
                arrays++;
            }
            else if (instruction.opcode == OpCodes.Ldstr && Equals(instruction.operand, "WELCOME"))
            {
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(WelcomeCaptionPatch), nameof(TitleText)));
                titles++;
            }
        }
        if (arrays != 1 || titles != 1 || checkboxes != 1)
            throw new InvalidOperationException($"Welcome display sites changed: {arrays}/{titles}/{checkboxes}.");
    }
}
