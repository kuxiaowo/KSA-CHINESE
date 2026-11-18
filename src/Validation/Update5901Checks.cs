using System.Text.Json;
using HarmonyLib;
using KSA;
using KsaChinese;

internal static partial class Program
{
    static void Update5901Checks()
    {
        var original = Runtime.SelectedLocale;
        Runtime.ApplyLanguage("zh-CN");
        using var changelog = JsonDocument.Parse(File.ReadAllText(Path.Combine(Game, "Content/Versions/v2026.11.X.5901.json")));
        var lines = changelog.RootElement.GetProperty("commits").EnumerateArray()
            .SelectMany(commit => commit.GetProperty("lines").EnumerateArray()).Select(line => line.GetString()!).ToArray();
        Check(lines.Length == 311 && lines.All(Runtime.CurrentPack.History.ContainsKey), "5901 更新日志逐行匹配");
        Check(LStrings.EditorSettingStandbyInStorms.Localized == "辐射风暴期间待机", "探测器风暴待机设置");
        var warning = LStrings.LaunchCheckSuppliesShort.Format("A-42", "H₂O", "4 d", "12 d");
        Check(new[] { "A-42", "H₂O", "4 d", "12 d" }.All(warning.Contains), "发射警告保留载具输入及时间");
        var labels = new[] { "As planned", "From flight plan", "Switch to", "Low orbit", "Custom" };
        var ids = labels.Select(s => Hash(ExpandedUi.Menu(s), 123)).ToArray();
        var sites = JsonSerializer.Deserialize<DisplaySite[]>(File.ReadAllText(Path.Combine(Runtime.ModRoot, "display-sites.json")))!;
        var methods = ExpandedUi.Methods().ToLookup(ExpandedUi.Identity);
        foreach (var site in sites.Where(s => s.Utf8.Length + s.Utf8Controls.Length > 0))
        {
            var targets = methods[site.Type + "." + site.Method];
            Check(targets.Any(), "5901 UTF-8 显示方法 " + site.Type + "." + site.Method);
        }
        Runtime.ApplyLanguage("en-US");
        Check(labels.Select(s => Hash(ExpandedUi.Menu(s), 123)).SequenceEqual(ids), "5901 任务及载具控件 ID 往返");
        Check(LStrings.EditorSettingStandbyInStorms.Localized == "Standby In Storms"
            && ExpandedUi.HistoryText(lines[0]) == lines[0], "5901 英文回退");
        Check(!LanguagePlugin.SupportsGameVersion("2026.11.10.5801")
            && !LanguagePlugin.SupportsGameVersion("2026.11.16.5902"), "拒绝旧版和未检查的相邻版本");
        Runtime.ApplyLanguage(original);
    }
}
