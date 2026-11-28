using System.Diagnostics;
using System.Text.Json;
using HarmonyLib;
using KSA;
using KsaChinese;

internal static partial class Program
{
    static void Update5949Checks()
    {
        var original = Runtime.SelectedLocale;
        using var changelog = JsonDocument.Parse(File.ReadAllText(Path.Combine(Game, "Content/Versions/v2026.11.X.5949.json")));
        var lines = changelog.RootElement.GetProperty("commits").EnumerateArray()
            .SelectMany(commit => commit.GetProperty("lines").EnumerateArray()).Select(line => line.GetString()!).ToArray();
        Runtime.ApplyLanguage("zh-CN");
        Check(lines.Length == 119 && lines.All(Runtime.CurrentPack.History.ContainsKey), "5949 更新日志逐行匹配");
        Check(!LanguagePlugin.SupportsGameVersion("2026.11.16.5901")
            && !LanguagePlugin.SupportsGameVersion("2026.11.25.5950"), "5949 拒绝旧版和未检查的相邻版本");
        Check(LStrings.AlertSaveSystemNotInstalled.Format("Save-{42}", "System-77") ==
              "存档 Save-{42} 创建于尚未安装的星系 System-77。"
            && LStrings.AlertRestartedForVram.Format("Save-{42}").Contains("Save-{42}")
            && LStrings.AlertJoinNotEnoughVram.Format("System-77").Contains("System-77"), "5949 存档和星系占位符");
        Check(LStrings.LifeSupportTooltipTimingAsPlanned.Localized.Contains("返航")
            && LStrings.LifeSupportTooltipTimingFlightPlan.Localized.Contains("实际时间"), "5949 任务计时使用实际到达、离开与返航");
        Check(SaveSystemCaptionPatch.DisplayName("Unknown System-77") == "Unknown System-77"
            && DestinationValuePatch.SituationNames("Landed, InSoi") == "已着陆, 在引力影响范围内", "5949 未知星系原样回退及到访状态标志组合");
        Check(Runtime.EditorText("Thermal") == "热防护" && Runtime.EditorText("MASS") == "质心"
            && Runtime.CurrentPack.Parts["CoreThermalA_Prefab_Heatshield2HalfWA"] == "2.5 m 防热盾", "5949 热防护类别、部件和质心按钮");
        Check(Runtime.EditorNativeText("At Baikonur Site 1 On Earth", 2).Contains("拜科努尔 1 号发射台")
            && Runtime.EditorNativeSelectable("At Baikonur Site 1 On Earth", 2).EndsWith("###At Baikonur Site 1 On Earth")
            && Runtime.EditorNativeText("At Prototype Custom Pad On Earth", 2).Contains("Prototype Custom Pad"),
            "5949 拜科努尔发射台显示名及地点 ID");
        var draw = AccessTools.Method(typeof(DestinationRegistryWindow), nameof(DestinationRegistryWindow.Draw));
        ExpandedUi.Enter(draw);
        var translated = ExpandedUi.Menu("Visits");
        ExpandedUi.Leave();
        Check(translated == "到访记录###Visits" && Hash(translated, 123) == Hash("###Visits", 123), "5949 到访选项卡保留 ImGui ID");
        foreach (var locale in Runtime.Packs.Keys.Where(locale => locale != "en-US"))
        {
            Runtime.ApplyLanguage(locale);
            Check(lines.All(Runtime.CurrentPack.History.ContainsKey)
                && LStrings.AlertRestartedForVram.Format("Save-{42}").Contains("Save-{42}")
                && Runtime.EditorText("Thermal") != "Thermal"
                && DestinationValuePatch.SituationNames("Landed, InSoi") != "Landed, InSoi",
                "5949 更新正文、热防护、到访状态和重启提示覆盖 " + locale);
        }
        Runtime.ApplyLanguage("en-US");
        ExpandedUi.Enter(draw);
        var fallback = ExpandedUi.Menu("Visits");
        ExpandedUi.Leave();
        Check(fallback == "Visits###Visits" && Hash(fallback, 123) == Hash(translated, 123), "5949 语言切换后到访选项卡 ID 一致");
        Check(ExpandedUi.HistoryText(lines[0]) == lines[0] && DestinationValuePatch.SituationNames("Landed, InSoi") == "Landed, InSoi"
            && Runtime.EditorText("Thermal") == "Thermal", "5949 英文回退");
        Runtime.ApplyLanguage(original);

        var relaunch = AccessTools.Method(typeof(SaveSystemRestart), "RelaunchArguments");
        var nextArguments = (List<string>)relaunch.Invoke(null, new object[]
        {
            new[] { "StarMap.exe", "-InstancePath", "isolated profile", "-load", "old save" },
            "StarMap.exe", "new save"
        })!;
        Check(nextArguments.SequenceEqual(new[] { "-InstancePath", "isolated profile", "-load", "new save" }),
            "5949 重启保留档案参数并替换旧存档参数");
        var loader = Path.Combine(Fixture, "5949-restart-loader");
        Directory.CreateDirectory(loader);
        File.WriteAllText(Path.Combine(loader, "StarMapConfig.json"), "{}");
        var starMap = new ProcessStartInfo(Path.Combine(loader, "StarMap.exe")) { WorkingDirectory = Game };
        foreach (string argument in nextArguments) starMap.ArgumentList.Add(argument);
        StarMapRestartPatch.Configure(starMap);
        var native = new ProcessStartInfo(Path.Combine(Game, "KSA.exe")) { WorkingDirectory = Game };
        StarMapRestartPatch.Configure(native);
        Check(starMap.WorkingDirectory == loader && starMap.ArgumentList.SequenceEqual(nextArguments)
            && native.WorkingDirectory == Game, "5949 StarMap 重启从加载器目录启动并保留参数");
    }
}
