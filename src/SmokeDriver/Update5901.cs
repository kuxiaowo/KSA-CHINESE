using HarmonyLib;
using KSA;

namespace KsaChineseSmoke;

public sealed partial class Driver
{
    double _phaseTime;

    void Update5901Frame(double elapsed)
    {
        if (elapsed < 3 + Stage * 4) return;
        switch (Stage)
        {
            case 0:
                Locale("zh-CN");
                ContentSnapshot();
                Universe.Rules.LifeSupport = true;
                LifeSupportWindow.Open(Program.ControlledVehicle);
                Log("5901-flight=" + Program.ControlledVehicle?.Id);
                break;
            case 1:
                Shot("5901-life-support-zh");
                break;
            case 2:
                Locale("en-US");
                break;
            case 3:
                Shot("5901-life-support-en");
                break;
            case 4:
                Locale("zh-CN");
                var vessel = Universe.CurrentSystem!.All.AsSpan().ToArray().OfType<Vehicle>()
                    .First(v => v != Program.ControlledVehicle && v.LifeSupportForecast.Kittens > 0);
                CrewClock.SwitchTo(vessel);
                Log("5901-switch-target=" + vessel.Id);
                break;
            case 5:
                Log("5901-switched=" + Program.ControlledVehicle?.Id);
                var current = Program.ControlledVehicle!;
                LifeSupportWindow.Open(current);
                var zone = current.Parts.EnsureHabitatZones()[0];
                var from = zone.Habitats.ToArray().First(h => h.StorageVolume > 0);
                var transfer = AccessTools.Field(typeof(LifeSupportWindow), "_instance").GetValue(null)!;
                AccessTools.Method(transfer.GetType(), "OpenTransfer").Invoke(transfer, [current.Parts, zone, from]);
                Log("5901-transfer-open=" + AccessTools.Field(transfer.GetType(), "_transferOpen").GetValue(transfer));
                break;
            case 6:
                Shot("5901-transfer");
                break;
            case 7:
                LifeSupportWindow.Close();
                Program.EditorFlag = true;
                break;
            case 8:
                if (Program.Editor?.EditingSpace.Parts is not { } tree)
                    throw new InvalidOperationException("5901 editor has no test craft");
                tree.Mission = new MissionPlan { Custom = true, CustomLength = new UniverseTime(864000) };
                LifeSupportWindow.Open(null);
                Log($"5901-editor-zones={tree.EnsureHabitatZones().Length}");
                break;
            case 9:
                var window = AccessTools.Field(typeof(LifeSupportWindow), "_instance").GetValue(null)!;
                AccessTools.Field(window.GetType(), "_missionOpen").SetValue(window, true);
                break;
            case 10:
                Shot("5901-editor-crew-plan");
                break;
            case 11:
                if (Environment.GetEnvironmentVariable("KSA_SMOKE_VISUAL") == "1" && elapsed < 135) return;
                var craft = Program.Editor!.EditingSpace.Parts!;
                var legs = MissionPicker.ComputeLegs(craft.Mission);
                Log("5901-before-stock=" + LaunchCheck.FindShortZone(craft, in legs).Status);
                LaunchCheck.Run(craft, () => Log("5901-launch-allowed"),
                    new LaunchFixes(() => LaunchCheck.StockAll(craft, in legs), () => {}));
                Log("5901-launch-popup=" + Popup.AnyOpen);
                _phaseTime = elapsed;
                break;
            case 12:
                if (elapsed - _phaseTime < (Environment.GetEnvironmentVariable("KSA_SMOKE_VISUAL") == "1" ? 75 : 3)) return;
                var stocked = Program.Editor!.EditingSpace.Parts!;
                var planned = MissionPicker.ComputeLegs(stocked.Mission);
                LaunchCheck.StockAll(stocked, in planned);
                Log("5901-after-stock=" + LaunchCheck.FindShortZone(stocked, in planned).Status);
                Log("5901-review-completed");
                Program.Instance.RequestExit(0);
                break;
        }
        Stage++;
    }
}
