using System.Reflection.Emit;
using System.Text.Json;
using HarmonyLib;
using KsaChinese;

internal static partial class Program
{
    static void Update5801Checks()
    {
        using var log = JsonDocument.Parse(File.ReadAllText(Path.Combine(Game, "Content/Versions/v2026.11.X.5801.json")));
        var lines = log.RootElement.GetProperty("commits").EnumerateArray()
            .SelectMany(commit => commit.GetProperty("lines").EnumerateArray()).Select(line => line.GetString()!).ToArray();
        Check(lines.Length == 242 && lines.All(line => Runtime.CurrentPack.History.ContainsKey(line))
            && lines.Count(line => ExpandedUi.HistoryText(line) == line) == 1, "5801 更新日志 242 行逐条匹配，原样保留一个残缺字符");
        Check(KSA.LStrings.EditorUndoAlert.Format("测试操作") == "已撤销：测试操作"
            && KSA.LStrings.EditorRedoAlert.Format("测试操作") == "已重做：测试操作", "撤销和重做保留动作占位符");
        Check(KSA.LStrings.AlertFuelTransferNoPower.Localized == "储罐转移需要电力"
            && KSA.LStrings.AlertLifeSupportLow.Localized == "生命保障物资不足", "5801 资源与生命保障警报");
        Check(Runtime.PartName(new KSA.PartTemplate { Id = "CoreLifeSupportA_Prefab_SupplyLockerA", DisplayName = "SL-M1 Supply Locker" }) == "SL-M1 物资柜",
            "物资柜使用 5801 的实际 ID，保留型号");
        var sites = JsonSerializer.Deserialize<DisplaySite[]>(File.ReadAllText(Path.Combine(Runtime.ModRoot, "display-sites.json")))!;
        var targets = ExpandedUi.Methods().ToLookup(ExpandedUi.Identity);
        foreach (var site in sites.Where(site => site.Direct.Length > 0))
        {
            string identity = site.Type + "." + site.Method;
            var constants = targets[identity].SelectMany(method => PatchProcessor.GetOriginalInstructions(method))
                .Where(instruction => instruction.opcode == OpCodes.Ldstr)
                .Select(instruction => (string)instruction.operand).ToHashSet(StringComparer.Ordinal);
            var missing = site.Direct.Where(source => !constants.Contains(source)).ToArray();
            Check(missing.Length == 0, "5801 显示常量匹配 " + identity + " " + string.Join(", ", missing));
        }

        var captions = new[] { "Life Support", "Habitat Zones", "Rules", "Cause of Death", "Meals##take", "Highlight all zones" };
        var hashes = captions.Select(c => Hash(ExpandedUi.Menu(c), 0x735A1913)).ToArray();
        Runtime.ApplyLanguage("en-US");
        Check(captions.Select(c => Hash(ExpandedUi.Menu(c), 0x735A1913)).SequenceEqual(hashes), "5801 新增控件的 ImGui ID 跨语言稳定");
        Check(ExpandedUi.HistoryText(lines[0]) == lines[0], "5801 日志英文回退");
        Runtime.ApplyLanguage("zh-CN");
        Check(!LanguagePlugin.SupportsGameVersion("2026.10.25.5707") && !LanguagePlugin.SupportsGameVersion("2026.11.10.5802"), "拒绝旧版和未经核实的相邻版本");
    }
}
