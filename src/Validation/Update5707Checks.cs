using System.Reflection;
using System.Reflection.Emit;
using System.Text.Json;
using HarmonyLib;
using KsaChinese;

internal static partial class Program
{
    static void Update5707Checks()
    {
        Runtime.ApplyLanguage("zh-CN");
        Check(KSA.LStrings.SettingsDisplayHdr.Localized == "HDR（实验性）"
            && KSA.LStrings.SettingsDisplayHdrPaperWhite.Localized == "HDR 基准白亮度", "5707 HDR 原生资源与基准白语义");
        const string tooltip = "Radiation dose that heals over time, with a half-life of 30 days in flight and 7 days resting at home.\nAt 1 Sv a kitten has radiation sickness and is grounded until it heals below 1 Sv.\nAt 4 Sv it dies.";
        string translated = Update5707Text.Tooltip(tooltip);
        Check(translated.StartsWith("会随时间恢复") && translated.Contains("30 天") && translated.Contains("7 天")
            && translated.Contains("1 Sv") && translated.Contains("4 Sv"), "名册动态剂量提示保留实际半衰期和阈值");
        Check(Update5707Text.Tooltip(tooltip.Replace("30 days", "25.5 days")).Contains("25.5 天"), "剂量提示提取实际数值而非硬编码");
        const string lifetime = "Every radiation dose a kitten has ever received. It never heals.\nAt 0.6 Sv a kitten finishes its current flight and then retires.";
        const string recovery = "How long a kitten needs resting at home for its acute dose to heal away.\nA kitten with radiation sickness can fly again sooner, once its acute dose is below 1 Sv.";
        Check(Update5707Text.Tooltip(lifetime).Contains("0.6 Sv") && Update5707Text.Tooltip(lifetime).StartsWith("喵航员"), "终生剂量提示保持退休阈值");
        Check(Update5707Text.Tooltip(recovery).StartsWith("喵航员") && Update5707Text.Tooltip(recovery).Contains("1 Sv"), "恢复时间提示与停飞阈值");
        Check(Update5707Text.Tooltip("玩家名 " + tooltip) == "玩家名 " + tooltip, "动态提示只匹配完整已审核模板");
        Check(Update5707Text.Text("CME shock reached ") + "玩家目标" == "CME 激波已到达 玩家目标", "CME 警报保留动态天体身份");
        Check(Runtime.GaugeText("DOSE") == "剂量率" && Runtime.GaugeText("DOSE PER HOUR") == "每小时剂量", "5707 点火与辐射仪表新标签");
        foreach (var method in Update5707FragmentPatch.TargetMethods())
        {
            var before = PatchProcessor.GetOriginalInstructions(method);
            var after = Update5707FragmentPatch.Translate(before.Select(i => new CodeInstruction(i))).ToArray();
            Check(after.Count(i => i.operand is MethodInfo m && m.DeclaringType == typeof(Update5707Text)) > 0,
                "5707 动态显示片段实际 IL 接入: " + method.DeclaringType?.Name + "." + method.Name);
            Check(before.Where(i => i.opcode == OpCodes.Ldstr).Select(i => i.operand)
                .SequenceEqual(after.Where(i => i.opcode == OpCodes.Ldstr).Select(i => i.operand)), "动态显示补丁保留原始常量及格式参数: " + method.Name);
        }
        using var history = JsonDocument.Parse(File.ReadAllText(Path.Combine(Game,"Content/Versions/v2026.10.X.5707.json")));
        var lines = history.RootElement.GetProperty("commits").EnumerateArray().SelectMany(c=>c.GetProperty("lines").EnumerateArray().Select(l=>l.GetString()!)).ToArray();
        Check(lines.Length == 156 && lines.All(s => ExpandedUi.HistoryText(s) != s), "5707 当前更新日志 156 条正文完整接入中文");
        var labels = new[] { "Space Weather", "Show Space Weather", "Show Magnetosphere", "Acute Dose", "Lifetime Dose", "Recovery", "Compute Tessellation" };
        var localized = labels.Select(ExpandedUi.Menu).ToArray();
        Runtime.ApplyLanguage("en-US");
        Check(Update5707Text.Tooltip(tooltip) == tooltip && Update5707Text.Text("CME shock reached ") == "CME shock reached ", "5707 动态提示及警报英文回退");
        for(int i=0;i<labels.Length;i++) foreach(uint seed in new uint[]{0,0x735A1913})
            Check(Hash(ExpandedUi.Menu(labels[i]),seed)==Hash(localized[i],seed),"5707 新增控件跨语言 ID: "+labels[i]);
        Check(!LanguagePlugin.SupportsGameVersion("2026.10.19.5638") && !LanguagePlugin.SupportsGameVersion("2026.10.25.5708"), "5707 发布拒绝旧版本和未知相邻版本");
        Runtime.ApplyLanguage("zh-CN");
    }
}
