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
public sealed partial class Driver
{
    readonly Stopwatch Timer=new();
    int Stage;
    bool Fallback => Environment.GetEnvironmentVariable("KSA_SMOKE_FALLBACK")=="1";
    bool Quick => Environment.GetEnvironmentVariable("KSA_SMOKE_QUICK")=="1";
    bool GaugeContext => Environment.GetEnvironmentVariable("KSA_SMOKE_GAUGE_CONTEXT")=="1";
    bool PartsReview => Environment.GetEnvironmentVariable("KSA_SMOKE_PARTS_REVIEW")=="1";
    bool FullReview => Environment.GetEnvironmentVariable("KSA_SMOKE_FULL_REVIEW")=="1";
    bool UpdateReview => Environment.GetEnvironmentVariable("KSA_SMOKE_5638_REVIEW")=="1";
    bool HistoryReview => Environment.GetEnvironmentVariable("KSA_SMOKE_HISTORY_REVIEW")=="1";
    static bool ScrollToAurora;
    ImGuiWindow? ChuteWindow,KittenWindow;
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
        patch.Patch(AccessTools.Constructor(typeof(UpdateAvailablePopup)),postfix:new HarmonyMethod(typeof(Driver),nameof(SkipUpdate)));
        if(UpdateReview) patch.Patch(AccessTools.Method(typeof(GameSettings),"DrawCheckboxRow"),postfix:new HarmonyMethod(typeof(Driver),nameof(AuroraScroll)));
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
    public static void SkipUpdate(UpdateAvailablePopup __instance)=>__instance.Active=false;
    public static void AuroraScroll(ReadOnlySpan<char> label)
    {
        if(ScrollToAurora && (label.SequenceEqual("极光") || label.SequenceEqual("Aurora"))) ImGui.SetScrollHereY(0.4f);
    }
    static void Position(ImGuiWindow window,float x,float y)
        =>AccessTools.Field(typeof(ImGuiWindow),"_initialPosition").SetValue(window,new float2(x,y));
    static void ContentSnapshot()
    {
        var plugin=AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.GetName().Name=="KsaChinese");
        var display=plugin.GetType("KsaChinese.ExpandedUi")!;
        var registry=AccessTools.Field(typeof(ModLibrary),"AllParts").GetValue(null)!;
        var templates=(IEnumerable<PartTemplate>)AccessTools.Method(registry.GetType(),"GetList").Invoke(registry,null)!;
        var partName=plugin.GetType("KsaChinese.Runtime")!.GetMethod("PartName")!;
        var parts=templates.Select(p=>new {key=p.Id,original=p.DisplayName,chinese=(string)partName.Invoke(null,new object[]{p})!}).ToArray();
        File.WriteAllText(Path.Combine(Constants.DocumentsFolderPath,"part-name-runtime.json"),System.Text.Json.JsonSerializer.Serialize(parts));
        if(parts.Length!=650||parts.Count(p=>p.original!=p.chinese)!=649)throw new Exception("Loaded Core part inventory does not match the reviewed 5602 set.");
        Log("part-registry="+parts.Length+" translated="+parts.Count(p=>p.original!=p.chinese));
        var controlText=plugin.GetType("KsaChinese.Runtime")!.GetMethod("ControlText")!;
        var actions=EnumCollections.InputActions.Names.Select(name=>new{original=name,chinese=(string)controlText.Invoke(null,new object[]{name})!}).ToArray();
        File.WriteAllText(Path.Combine(Constants.DocumentsFolderPath,"controls-runtime.json"),System.Text.Json.JsonSerializer.Serialize(actions));
        if(actions.Length!=103||actions.Any(a=>a.chinese==a.original))throw new Exception("A loaded input-action caption has no reviewed translation.");
        Log("input-actions="+actions.Length+" translated="+actions.Count(a=>a.original!=a.chinese));
        string Caption(string method,object value)=>(string)display.GetMethod(method)!.Invoke(null,new[]{value})!;
        var substances=((System.Collections.IDictionary)AccessTools.Field(typeof(SubstanceLibrary),"SubstancesByHash").GetValue(null)!).Values.Cast<Substance>().ToArray();
        var phases=((System.Collections.IDictionary)AccessTools.Field(typeof(SubstanceLibrary),"SubstancePhasesByHash").GetValue(null)!).Values.Cast<SubstancePhase>().ToArray();
        var reactions=SubstanceLibrary.AllReactions().ToArray();
        var before=substances.Select(s=>(s.Id,s.Hash,s.ShortName,s.FullName,s.Description)).ToArray();
        var report=new {
            plugin=plugin.Location,
            substances=substances.Select(s=>new{s.Id,original=s.ShortName,chinese=Caption("SubstanceShort",s),full=Caption("SubstanceFull",s),description=Caption("SubstanceDescription",s)}).ToArray(),
            phases=phases.Select(s=>new{s.Id,original=s.Name,chinese=Caption("PhaseName",s)}).ToArray(),
            reactions=reactions.Select(s=>new{s.Id,original=s.Name,chinese=Caption("ReactionName",s),description=Caption("ReactionDescription",s)}).ToArray()
        };
        if(!before.SequenceEqual(substances.Select(s=>(s.Id,s.Hash,s.ShortName,s.FullName,s.Description))))throw new Exception("Display adapter mutated loaded substance data.");
        File.WriteAllText(Path.Combine(Constants.DocumentsFolderPath,"content-runtime.json"),System.Text.Json.JsonSerializer.Serialize(report));
        if(report.substances.Any(s=>!s.chinese.Any(c=>c is >= '\u4e00' and <= '\u9fff'))||report.phases.Any(s=>!s.chinese.Any(c=>c is >= '\u4e00' and <= '\u9fff')))
            throw new Exception("A loaded Core substance/phase has no Chinese display name.");
        Log($"content-registry substances={substances.Length} phases={phases.Length} reactions={reactions.Length}; original-fields-unchanged");
    }
    static void Locale(string locale)
    {
        var runtime=AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.GetName().Name=="KsaChinese").GetType("KsaChinese.Runtime")!;
        runtime.GetMethod("ApplyLanguage")!.Invoke(null,new object[]{locale});
    }
    static void Shot(string tag)
    {
        Log("screenshot-request="+tag);
        AccessTools.Method(typeof(KSA.Program),"Screenshot").Invoke(null,new object[]{1,"ui hud warm=0"});
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
    static void History(bool shown)
    {
        var field=AccessTools.Field(typeof(VersionHistory),"_instance");
        if(field.GetValue(null) is null)VersionHistory.Toggle();
        ((ImGuiWindow)field.GetValue(null)!).SetShown(shown);
    }
    [StarMapAfterOnFrame]
    public void Frame(double currentPlayerTime,double dtPlayer)
    {
        if(!KSA.Program.IsLoaded) return;
        if(!Timer.IsRunning) { Timer.Start(); Log("loaded; controlled="+KSA.Program.ControlledVehicle?.Id); }
        double t=Timer.Elapsed.TotalSeconds;
        try
        {
            var ecosystem = Environment.GetEnvironmentVariable("KSA_SMOKE_ECOSYSTEM");
            if (!string.IsNullOrEmpty(ecosystem))
            {
                EcosystemFrame(ecosystem, t);
                return;
            }
            if(HistoryReview)
            {
                if(Stage==0 && t>2) { GameSettings.Show=false;History(true);Stage++; }
                else if(Stage==1 && t>6) { Shot("update-history-zh");Stage++; }
                else if(Stage==2 && t>10) { Log("history-review-completed");Program.Instance.RequestExit(0);Stage++; }
                return;
            }
            if(UpdateReview)
            {
                if(Stage==0 && t>3)
                {
                    ContentSnapshot();GameSettings.Show=false;
                    foreach(var canvas in GaugeCanvas.AllCanvases)canvas.SetEnabled(canvas.Id=="Radiation");
                    Log("radiation-canvas="+GaugeCanvas.AllCanvases.Count(c=>c.Id=="Radiation"));
                    Stage++;
                }
                else if(Stage==1 && t>7) { Shot("update-radiation-zh");Stage++; }
                else if(Stage==2 && t>10) { Locale("en-US");Stage++; }
                else if(Stage==3 && t>14) { Shot("update-radiation-en");Stage++; }
                else if(Stage==4 && t>17) { Locale("zh-CN");Program.MainViewport.SetCameraMode(CameraMode.Map);MagnetosphereOverlay.Show=true;Stage++; }
                else if(Stage==5 && t>22) { Shot("update-map-zh");Stage++; }
                else if(Stage==6 && t>25) { Locale("en-US");Stage++; }
                else if(Stage==7 && t>30) { Shot("update-map-en");Stage++; }
                else if(Stage==8 && t>33) { Locale("zh-CN");Program.MainViewport.SetCameraMode(CameraMode.Orbit);ScrollToAurora=true;Tab("Graphics");Stage++; }
                else if(Stage==9 && t>38) { Shot("update-aurora-zh");Stage++; }
                else if(Stage==10 && t>41) { ScrollToAurora=false;GameSettings.Show=false;History(true);Stage++; }
                else if(Stage==11 && t>46) { Shot("update-history-zh");Stage++; }
                else if(Stage==12 && t>49) { History(false);Program.ShowEngineDesignDebug=true;AccessTools.Field(typeof(EngineDesigner),"_combustionProcess").SetValue(null,SubstanceLibrary.AllReactions().ToArray().First(r=>r.Id=="Kerolox"));Stage++; }
                else if(Stage==13 && t>54) { Shot("update-engine-zh");Stage++; }
                else if(Stage==14 && t>57) { Program.ShowEngineDesignDebug=false;CrewPortraitPanel.ShowCameraControls=true;GameSaves.MakeUncompressedSave("ksa_chinese_5638_test");Log("update-save-request");Stage++; }
                else if(Stage==15 && t>64) { GameSaves.LoadSaveGame("ksa_chinese_5638_test");Log("update-load-request");Stage++; }
                else if(Stage==16 && t>74) { ContentSnapshot();Log("update-after-load; controlled="+Program.ControlledVehicle?.Id);Stage++; }
                else if(Stage==17 && t>79) { Log("update-review-completed");Program.Instance.RequestExit(0);Stage++; }
                return;
            }
            if(FullReview)
            {
                if(Stage==0 && t>3)
                {
                    ContentSnapshot();
                    GameSettings.Show=false;
                    Program.ShowEngineDesignDebug=true;
                    AccessTools.Field(typeof(EngineDesigner),"_combustionProcess").SetValue(null,SubstanceLibrary.AllReactions().ToArray().First(r=>r.Id=="Kerolox"));
                    Stage++;
                }
                else if(Stage==1 && t>7) { Shot("full-engine-zh"); Stage++; }
                else if(Stage==2 && t>10) { Locale("en-US"); Stage++; }
                else if(Stage==3 && t>14) { Shot("full-engine-en"); Stage++; }
                else if(Stage==4 && t>17)
                {
                    Locale("zh-CN");Program.ShowEngineDesignDebug=false;
                    foreach(var canvas in GaugeCanvas.AllCanvases)AccessTools.Field(typeof(GaugeCanvas),"_enabled").SetValue(canvas,false);
                    ChuteWindow=new ChuteTuningWindow();Position(ChuteWindow,30,80);ChuteWindow.SetShown(true);
                    KittenWindow=new KittenTuningWindow();Position(KittenWindow,570,80);KittenWindow.SetShown(true);
                    Stage++;
                }
                else if(Stage==5 && t>22) { Shot("full-tuning-zh"); Stage++; }
                else if(Stage==6 && t>25) { ChuteWindow!.SetShown(false);KittenWindow!.SetShown(false);Tab("Display");Stage++; }
                else if(Stage==7 && t>29) { Shot("full-display-zh"); Stage++; }
                else if(Stage==8 && t>32) { GameSettings.Show=false;Program.EditorFlag=true;Stage++; }
                else if(Stage==9 && t>37)
                {
                    var editor=Program.Editor!;
                    var part=editor.EditingSpace.AllParts.ToArray().First(p=>p.Template.Id.Contains("FuelTank"));
                    editor.PartMenus.Add(new PartMenu{Part=part});Stage++;
                }
                else if(Stage==10 && t>41) { Shot("full-editor-zh");Stage++; }
                else if(Stage==11 && t>45) { Program.EditorFlag=false;VersionHistory.Toggle();Stage++; }
                else if(Stage==12 && t>50) { Shot("full-history-zh");Stage++; }
                else if(Stage==13 && t>55) { Log("full-review-completed");Program.Instance.RequestExit(0);Stage++; }
                return;
            }
            if(PartsReview)
            {
                if(Stage==0 && t>3)
                {
                    var registry=AccessTools.Field(typeof(ModLibrary),"AllParts").GetValue(null)!;
                    var templates=(IEnumerable<PartTemplate>)AccessTools.Method(registry.GetType(),"GetList").Invoke(registry,null)!;
                    var runtime=AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.GetName().Name=="KsaChinese").GetType("KsaChinese.Runtime")!;
                    var translate=runtime.GetMethod("PartName")!;
                    Log("tested-plugin="+runtime.Assembly.Location);
                    var rows=templates.Select(p=>new {key=p.Id,original=p.DisplayName,chinese=(string)translate.Invoke(null,new object[]{p})!}).ToArray();
                    File.WriteAllText(Path.Combine(Constants.DocumentsFolderPath,"part-name-runtime.json"),System.Text.Json.JsonSerializer.Serialize(rows));
                    Log("part-registry="+rows.Length+" translated="+rows.Count(r=>r.original!=r.chinese));
                    var part=Program.ControlledVehicle!.Parts.Parts.ToArray().FirstOrDefault(p=>p.Template.Id=="CorePropulsionA_Prefab_EngineA5")
                        ?? Program.ControlledVehicle.Parts.Parts.ToArray().First(p=>p.Template.Id.Contains("FuelTank"));
                    AccessTools.Field(typeof(Part),"ShowPartWindow").SetValue(part,true);
                    Log("flight-part="+part.Template.Id+" original="+part.DisplayName);
                    Stage++;
                }
                else if(Stage==1 && t>7) { Shot("parts-flight-zh"); Stage++; }
                else if(Stage==2 && t>10) { Locale("en-US"); Stage++; }
                else if(Stage==3 && t>14) { Shot("parts-flight-en"); Stage++; }
                else if(Stage==4 && t>17) { Locale("zh-CN"); Stage++; }
                else if(Stage==5 && t>21) { Shot("parts-flight-zh-roundtrip"); Stage++; }
                else if(Stage==6 && t>24) { Program.EditorFlag=true; Stage++; }
                else if(Stage==7 && t>28)
                {
                    var editor=Program.Editor!;
                    var part=editor.EditingSpace.AllParts.ToArray().First(p=>p.Template.Id.Contains("FuelTank"));
                    editor.PartMenus.Add(new PartMenu{Part=part});
                    Log("editor-part="+part.Template.Id);
                    Stage++;
                }
                else if(Stage==8 && t>33) { Locale("en-US"); Stage++; }
                else if(Stage==9 && t>37) { Locale("zh-CN"); Stage++; }
                else if(Stage==10 && t>42) { Log("parts-review-completed"); Program.Instance.RequestExit(0); Stage++; }
                return;
            }
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
