using System.Reflection;
using System.Text.Json;
using KSA;

namespace KsaChineseSmoke;

public sealed partial class Driver
{
    void EcosystemFrame(string mode, double t)
    {
        var plugin = AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(a => a.GetName().Name == "KsaChinese");
        var runtime = plugin?.GetType("KsaChinese.Runtime");
        if (Stage == 0 && t > 2)
        {
            if (mode == "disabled")
            {
                if (plugin is not null) throw new Exception("Disabled mod was loaded.");
                Log("ecosystem-disabled-english");
            }
            else
            {
                if (runtime is null) throw new Exception("Mod was not loaded.");
                string expected = mode == "en-to-zh" ? "en-US" : "zh-CN";
                string actual = (string)runtime.GetProperty("SelectedLocale")!.GetValue(null)!;
                if (actual != expected) throw new Exception($"Expected persisted locale {expected}, got {actual}");
                Log("ecosystem-initial=" + actual + "; plugin=" + plugin!.Location);
                if (expected == "zh-CN") ContentSnapshot();
                else if (KSA.LStrings.SettingsDisplayResolution.Localized != "Resolution")
                    throw new Exception("Persisted English provider did not restore native text.");
            }
            Shot("ecosystem-" + mode);
            Stage++;
        }
        else if (Stage == 1 && t > 6)
        {
            if (mode != "disabled")
            {
                if (mode == "zh-to-en") runtime!.GetMethod("SelectLanguage")!.Invoke(null, ["en-US"]);
                if (mode == "en-to-zh") runtime!.GetMethod("SelectLanguage")!.Invoke(null, ["zh-CN"]);
                string locale = (string)runtime!.GetProperty("SelectedLocale")!.GetValue(null)!;
                Log("ecosystem-selected=" + locale);
                if (mode == "zh-to-en") GameSaves.MakeUncompressedSave("ksa_chinese_ecosystem_test");
                if (mode == "en-to-zh") GameSaves.LoadSaveGame("ksa_chinese_ecosystem_test");
            }
            Stage++;
        }
        else if (Stage == 2 && t > 13)
        {
            if (mode == "en-to-zh") { ContentSnapshot(); Log("ecosystem-reloaded=" + Program.ControlledVehicle?.Id); }
            Log("ecosystem-completed=" + mode);
            Program.Instance.RequestExit(0);
            Stage++;
        }
    }
}
