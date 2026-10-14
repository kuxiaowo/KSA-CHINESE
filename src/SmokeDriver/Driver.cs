// Development-only driver. Never included in the player Mod/package.
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using HarmonyLib;
using KSA;
using StarMap.API;
using Constants = KSA.Constants;

namespace KsaChineseSmoke;

[StarMapMod]
public sealed class Driver
{
    readonly Stopwatch Timer=new();
    int Stage;
    bool Fallback => Environment.GetEnvironmentVariable("KSA_SMOKE_FALLBACK")=="1";
    bool Quick => Environment.GetEnvironmentVariable("KSA_SMOKE_QUICK")=="1";
    bool GaugeContext => Environment.GetEnvironmentVariable("KSA_SMOKE_GAUGE_CONTEXT")=="1";
    static bool ShowGaugeContext;
    [StarMapBeforeMain]
    public void Initialize()
    {
        var path=Path.GetFullPath(Constants.DocumentsFolderPath);
        if(!path.Contains("KSA-CHINESE",StringComparison.OrdinalIgnoreCase) || !path.Contains("test-profile",StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Smoke driver is restricted to workspace test-profile.");
        var patch=new Harmony("local.ksa.chinese.smoke");
        patch.Patch(AccessTools.Constructor(typeof(ConfigOnStartPopup)),postfix:new HarmonyMethod(typeof(Driver),nameof(SkipConfig)));
        patch.Patch(AccessTools.Constructor(typeof(WelcomePopup)),postfix:new HarmonyMethod(typeof(Driver),nameof(SkipWelcome)));
        if(GaugeContext) patch.Patch(AccessTools.Method(typeof(GaugeCanvas),nameof(GaugeCanvas.OnDrawUi)),transpiler:new HarmonyMethod(typeof(Driver),nameof(ContextRenderer)));
        Log("isolation="+path);
    }
    public static void SkipConfig(ConfigOnStartPopup __instance)
    {
        GameSettings.Current.System.CheckForUpdates=false;
        GameSettings.Current.System.DisplayChangelogOnChange=false;
        GameSettings.Current.WelcomePopup=false;
        __instance.Active=false;
    }
    public static void SkipWelcome(WelcomePopup __instance)=>__instance.Active=false;
    static void Locale(string locale)
    {
        var runtime=AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.GetName().Name=="KsaChinese").GetType("KsaChinese.Runtime")!;
        runtime.GetMethod("ApplyLanguage")!.Invoke(null,new object[]{locale});
    }
    static void Shot(string tag)
    {
        Log("screenshot-request="+tag);
        AccessTools.Method(typeof(KSA.Program),"Screenshot").Invoke(null,new object[]{1,"ui hud warm=3"});
    }
    static void Log(string s) { Console.WriteLine("[Smoke] "+s); File.AppendAllText(Path.Combine(Constants.DocumentsFolderPath,"smoke.log"),DateTimeOffset.UtcNow.ToString("O")+" "+s+Environment.NewLine); }
    // Open the real SequenceCanvas popup in the fixture without injecting mouse
    // clicks or changing any checked value. This driver never ships to players.
    public static bool ContextPopup(ImString id, ImGuiPopupFlags flags)
    {
        if(ShowGaugeContext && Program.SequenceCanvas is not null && id.ToString()==Program.SequenceCanvas.DebugTitle)
        {
            ImGui.SetNextWindowPos(ImGui.GetMainViewport().Pos+new float2(400,250));
            ImGui.OpenPopup(id,ImGuiPopupFlags.None);
            return ImGui.BeginPopup(id,ImGuiWindowFlags.AlwaysAutoResize);
        }
        return ImGui.BeginPopupContextWindow(id,flags);
    }
    public static IEnumerable<CodeInstruction> ContextRenderer(IEnumerable<CodeInstruction> instructions)
    {
        int count=0;
        foreach(var instruction in instructions)
        {
            if(instruction.operand is MethodInfo m && m.DeclaringType==typeof(ImGui) && m.Name==nameof(ImGui.BeginPopupContextWindow))
            { instruction.opcode=OpCodes.Call; instruction.operand=AccessTools.Method(typeof(Driver),nameof(ContextPopup)); count++; }
            yield return instruction;
        }
        if(count!=1) throw new InvalidOperationException("Fixture gauge popup site changed.");
    }
    static void Tab(string name)
    {
        GameSettings.Show=true;
        var field=AccessTools.Field(typeof(GameSettings),"_openTab");
        field.SetValue(null,Enum.Parse(field.FieldType,name));
        var order=(Array)AccessTools.Field(typeof(GameSettings),"_navRailOrder").GetValue(null)!;
        for(int i=0;i<order.Length;i++) if(order.GetValue(i)!.ToString()==name) AccessTools.Field(typeof(GameSettings),"_openTabIndex").SetValue(null,i);
        Log("settings-tab="+name);
    }
    [StarMapAfterOnFrame]
    public void Frame(double currentPlayerTime,double dtPlayer)
    {
        if(!KSA.Program.IsLoaded) return;
        if(!Timer.IsRunning) { Timer.Start(); Log("loaded; controlled="+KSA.Program.ControlledVehicle?.Id); }
        double t=Timer.Elapsed.TotalSeconds;
        try
        {
            if(GaugeContext)
            {
                ShowGaugeContext=true;
                if(Stage==0 && t>3) { Shot("gauge-context-zh"); Stage++; }
                else if(Stage==1 && t>7) { Locale("en-US"); Stage++; }
                else if(Stage==2 && t>10) { Shot("gauge-context-en"); Stage++; }
                else if(Stage==3 && t>14) { Locale("zh-CN"); Stage++; }
                else if(Stage==4 && t>17) { Shot("gauge-context-zh-roundtrip"); Stage++; }
                else if(Stage==5 && t>22) { Log("gauge-context-completed"); Program.Instance.RequestExit(0); Stage++; }
                return;
            }
            if(Quick)
            {
                if(Stage==0 && t>3) { Shot("quick-hud-zh"); Stage++; }
                else if(Stage==1 && t>6) { Tab("Graphics"); Stage++; }
                else if(Stage==2 && t>10) { Shot("quick-graphics-zh"); Stage++; }
                else if(Stage==3 && t>13) { Tab("Controls"); Stage++; }
                else if(Stage==4 && t>17) { Shot("quick-controls-zh"); Stage++; }
                else if(Stage==5 && t>20) { GameSettings.Show=false; Locale("en-US"); Stage++; }
                else if(Stage==6 && t>25) { Shot("quick-hud-en"); Stage++; }
                else if(Stage==7 && t>28) { Locale("zh-CN"); Stage++; }
                else if(Stage==8 && t>32) { Shot("quick-hud-zh-roundtrip"); Stage++; }
                else if(Stage==9 && t>37) { Log("quick-completed"); KSA.Program.Instance.RequestExit(0); Stage++; }
                return;
            }
            if(Fallback)
            {
                if(Stage==0 && t>5) { GameSaves.LoadSaveGame("ksa_chinese_5602_test"); Log("uninstalled-load-request"); Stage++; }
                else if(Stage==1 && t>15) { Log("uninstalled-loaded; controlled="+KSA.Program.ControlledVehicle?.Id); Shot("fallback-english"); Stage++; }
                else if(Stage==2 && t>23) { Log("fallback-completed"); KSA.Program.Instance.RequestExit(0); Stage++; }
                return;
            }
            if(Stage==0 && t>5) { Shot("01-hud-zh"); Stage++; }
            else if(Stage==1 && t>12) { Tab("Display"); Stage++; }
            else if(Stage==2 && t>18) { Shot("02-settings-zh"); Stage++; }
            else if(Stage==3 && t>24) { Tab("Graphics"); Stage++; }
            else if(Stage==4 && t>30) { Shot("03-graphics-zh"); Stage++; }
            else if(Stage==5 && t>36) { Tab("Controls"); Stage++; }
            else if(Stage==6 && t>42) { Shot("04-controls-zh"); Stage++; }
            else if(Stage==7 && t>48) { GameSettings.Show=false; TransferPlanner.ShowPlanWindow=true; if(KSA.Program.ControlledVehicle!=null) KSA.Program.ControlledVehicle.ShowPatchWindow=true; Stage++; }
            else if(Stage==8 && t>54) { Shot("05-planning-zh"); Stage++; }
            else if(Stage==9 && t>60) { Locale("en-US"); Stage++; }
            else if(Stage==10 && t>66) { Shot("06-planning-en"); Stage++; }
            else if(Stage==11 && t>72) { Locale("zh-CN"); TransferPlanner.ShowPlanWindow=false; if(KSA.Program.ControlledVehicle!=null) KSA.Program.ControlledVehicle.ShowPatchWindow=false; GameSaves.MakeUncompressedSave("ksa_chinese_5602_test"); Log("save-request"); Stage++; }
            else if(Stage==12 && t>78) { GameSaves.LoadSaveGame("ksa_chinese_5602_test"); Log("load-request"); Stage++; }
            else if(Stage==13 && t>88) { Log("after-load; controlled="+KSA.Program.ControlledVehicle?.Id); if(KSA.Program.ControlledVehicle!=null) { KSA.Program.ControlledVehicle.ShowGroundTrack=true; KSA.Program.ControlledVehicle.ShowTargetTrack=true; } Stage++; }
            else if(Stage==14 && t>94) { Shot("07-tracking-zh"); Stage++; }
            else if(Stage==15 && t>100) { if(KSA.Program.ControlledVehicle!=null) { KSA.Program.ControlledVehicle.ShowGroundTrack=false; KSA.Program.ControlledVehicle.ShowTargetTrack=false; } KSA.Program.EditorFlag=true; Stage++; }
            else if(Stage==16 && t>108) { Shot("08-editor-zh"); Stage++; }
            else if(Stage==17 && t>114) { var editor=KSA.Program.Editor!; var parts=editor.EditingSpace.AllParts.ToArray(); var part=parts.FirstOrDefault(p=>p.Template.Id.Contains("FuelTank",StringComparison.OrdinalIgnoreCase)) ?? parts.First(); editor.PartMenus.Add(new PartMenu{Part=part}); Log("part-menu="+part.Template.Id); Stage++; }
            else if(Stage==18 && t>120) { Shot("09-parts-zh"); Stage++; }
            else if(Stage==19 && t>128) { Log("completed"); KSA.Program.Instance.RequestExit(0); Stage++; }
        }
        catch(Exception error) { Log("ERROR="+error); KSA.Program.Instance.RequestExit(2); Stage=99; }
    }
}
