using KsaChinese;

internal static partial class Program
{
    static void ProfileSettingsChecks()
    {
        var fixture = Path.Combine(Fixture, "locale-" + Guid.NewGuid().ToString("N"));
        var first = Path.Combine(fixture, "first", "settings.json");
        var second = Path.Combine(fixture, "second", "settings.json");
        string[] available = ["zh-CN", "en-US"];
        var diagnostics = new List<string>();
        Check(Runtime.SettingsPath == Path.Combine(Fixture, "KsaChinese", "settings.json"), "语言设置使用当前档案路径");
        Check(LocaleSettings.Read(first, "zh-CN", available, diagnostics.Add) == "zh-CN", "无个人语言设置使用包默认值");
        LocaleSettings.Write(first, "en-US", diagnostics.Add);
        Check(LocaleSettings.Read(first, "zh-CN", available, diagnostics.Add) == "en-US", "重读语言选择可恢复英文");
        Check(LocaleSettings.Read(second, "zh-CN", available, diagnostics.Add) == "zh-CN", "另一个实例保持独立默认值");
        LocaleSettings.Write(first, "zh-CN", diagnostics.Add);
        Check(LocaleSettings.Read(first, "en-US", available, diagnostics.Add) == "zh-CN", "替换旧语言选择并恢复中文");
        File.WriteAllText(first, "{broken");
        Check(LocaleSettings.Read(first, "zh-CN", available, diagnostics.Add) == "zh-CN", "损坏设置回退且不禁用汉化");
        File.WriteAllText(first, "{\"locale\":\"unknown\"}");
        Check(LocaleSettings.Read(first, "zh-CN", available, diagnostics.Add) == "zh-CN", "未知个人语言回退");
        File.WriteAllText(first, "{\"locale\":42}");
        Check(LocaleSettings.Read(first, "zh-CN", available, diagnostics.Add) == "zh-CN", "错误设置类型回退");
        var fileAsFolder = Path.Combine(fixture, "blocked");
        File.WriteAllText(fileAsFolder, "fixture");
        LocaleSettings.Write(Path.Combine(fileAsFolder, "settings.json"), "en-US", diagnostics.Add);
        Check(diagnostics.Count == 4, "不可写设置不抛异常并记录原因");
        Check(!Directory.EnumerateFiles(fixture, "*.tmp", SearchOption.AllDirectories).Any(), "原子写入不遗留临时文件");
    }
}
