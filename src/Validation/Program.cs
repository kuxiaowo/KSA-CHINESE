using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HarmonyLib;
using KsaChinese;

internal static partial class Program
{
    static string Root = Path.GetFullPath(Environment.GetEnvironmentVariable("KSA_PROJECT_ROOT") ?? Environment.CurrentDirectory);
    static string Game = Environment.GetEnvironmentVariable("KSA_GAME_DIR") ?? @"F:\系统应用\Kitten Space Agency";
    static string Loader = Environment.GetEnvironmentVariable("KSA_LOADER_DIR") ?? Path.Combine(Root,".tools","StarMap-0.4.8");
    static string Fixture = Path.Combine(Root,"work","validation-profile");
    static List<string> Checks = new();
    static List<object> Targets = new();
    static void Main()
    {
        var dirs = new[]{Game,Loader,Path.Combine(Root,"work","build","KsaChinese")};
        AssemblyLoadContext.Default.Resolving += (_,n) => dirs.Select(d=>Path.Combine(d,n.Name+".dll"))
            .Where(File.Exists).Select(AssemblyLoadContext.Default.LoadFromAssemblyPath).FirstOrDefault();
        NativeLibrary.SetDllImportResolver(typeof(Program).Assembly,(n,a,s)=> n=="KsaCheckImGui" ? NativeLibrary.Load(Path.Combine(Game,"imgui.dll")) : IntPtr.Zero);
        Directory.CreateDirectory(Fixture);
        Run();
    }
    static void Check(bool ok,string title) { if(!ok) throw new Exception(title); Checks.Add(title); }
    public static bool UseFixture(ref string __result) { __result=Fixture; return false; }
    [MethodImpl(MethodImplOptions.NoInlining)]
    static void Run()
    {
        var isolate = new Harmony("local.ksa.chinese.validation.isolation");
        isolate.Patch(AccessTools.PropertyGetter(typeof(KSA.Constants),nameof(KSA.Constants.DocumentsFolderPath)),prefix:new HarmonyMethod(typeof(Program),nameof(UseFixture)));
        Check(KSA.Constants.DocumentsFolderPath==Fixture,"玩家目录重定向至工作区");
        Check(LanguagePlugin.SupportsGameVersion("2026.10.19.5638") && !LanguagePlugin.SupportsGameVersion(null) && !LanguagePlugin.SupportsGameVersion("2026.10.19.5639"),"精确版本保护及未知版本拒绝");
        var previous=Brutal.Localization.LStrings.LocalizedStringProvider;
        Compatibility.Validate(Game,Path.Combine(Root,"assets","KsaChinese","compatibility.json"));
        Check(true,"游戏及依赖指纹匹配");
        bool mismatch=false;
        try { Compatibility.Validate(Fixture,Path.Combine(Root,"assets","KsaChinese","compatibility.json")); }
        catch(IOException) { mismatch=true; }
        Check(mismatch,"缺少或不同游戏文件安全拒绝");
        Runtime.Initialize();
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(KSA.LStrings).TypeHandle);
        Check(Runtime.CurrentPack.Native.Count==216,"原生217定义中216已核对接入，TabsId暂缓");
        Check(Brutal.Localization.LStrings.TryGet(Brutal.Localization.LStringId.From("ShadowFilterTooltip_Hard"),out var shadow)
            && shadow.Localized.StartsWith("不作滤波",StringComparison.Ordinal),"数组内无同名字段的原生ID接入");
        Check(KSA.LStrings.SettingsDisplayResolution.Localized=="分辨率","实际原生 LString 提供器");
        Check(Runtime.Literal("SettingsDisplayModeCombo")=="SettingsDisplayModeCombo","技术 ID 保留");
        var labels=new[]{"File","File##main","File###main","Resources###hud-resources","High##quality"};
        var zh=labels.ToDictionary(x=>x,Runtime.MenuText);
        Runtime.ApplyLanguage("en-US");
        Check(!(bool)AccessTools.Field(typeof(KSA.ConsoleStyle),"_gaugeInkRatioCached").GetValue(null)!,"语言切换清除旧仪表字形比例缓存");
        foreach(var label in labels) foreach(uint seed in new uint[]{0,0x735A1913})
            Check(Hash(Runtime.MenuText(label),seed)==Hash(zh[label],seed),"原生 ImGui 中英 ID: "+label+" seed="+seed);
        Check(KSA.LStrings.SettingsDisplayResolution.Localized=="Resolution","原生文本回退英文");
        Runtime.ApplyLanguage("zh-CN");
        Check(Runtime.MenuText("File###main")=="文件###main","已有 ### ID 保留");
        Check(KSA.LStrings.AlertTargetSet.Format("玩家目标").Contains("玩家目标",StringComparison.Ordinal),"实际LString格式化保留动态名称");
        Check(Runtime.DisplayRateText("DISPLAY")=="显示器刷新率" && Runtime.Literal("DISPLAY")=="显示","显示导航与垂直同步值的上下文区分");
        var longName=new string('测',240)+" (default)";
        var zhLong=HudLayoutPatches.CustomLayoutMenuLabel(longName,true);
        Check(zhLong.StartsWith(new string('测',240),StringComparison.Ordinal),"长自定义布局名保留");
        HudLayoutMenuBufferPatch.RequiredCharacters=zhLong.Length+3;
        Check(HudLayoutMenuBufferPatch.BufferCharacters()>128 && HudLayoutMenuBufferPatch.BufferBytes()==HudLayoutMenuBufferPatch.BufferCharacters()*2,"中文长布局的字符容量与原始分配同步扩展");
        HudLayoutMenuBufferPatch.RequiredCharacters=0;
        Check(EditorLiteralPatch.StaticText("玩家自定义部件").ToString()=="玩家自定义部件","未知玩家名称回退");
        Check(PartContextCaptionPatch.InteractiveText("Control 用户飞船").Contains("用户飞船"),"动态对象名称后缀保留");
        Check(GaugeFontPatch.Name()==Runtime.FontName,"仪表选择中文字体");
        var fontCandidates=new[]{"Content/KsaChinese/Fonts/KsaUiNotoSC.ttf",Runtime.FontPath,"Content/Core/Fonts/OtherFont.ttf"};
        Check(FontRegistrationPatch.WithoutLocaleFont(fontCandidates,Runtime.FontName).SequenceEqual(new[]{"Content/Core/Fonts/OtherFont.ttf"}),"Content 相对路径与绝对路径的中文字体按图集键去重，其他字体保留");
        var startupLabels=new[]{"Interstellar (Dense)","Earth and Moon"};
        var startupIds=startupLabels.ToDictionary(x=>x,Runtime.StartupMenuText);
        Check(Runtime.StartupText(startupLabels[0])=="太阳系及邻近恒星（扩充版）" && Runtime.StartupText(startupLabels[1])=="地球与月球","当前版本 XML 系统显示名翻译");
        Check(Runtime.EditorNativeText("At Apollo11 On Luna",2)=="月球：Apollo11","地点天体名称翻译，任务型号保留");
        Check(Runtime.EditorNativeText("At 玩家地点 On Luna",2)=="月球：玩家地点","地点玩家名称保留");
        var locationId=Runtime.EditorNativeSelectable("At Apollo11 On Luna###location-key",2);
        Check(locationId=="月球：Apollo11###location-key","地点隐藏 ID 保留");
        Check(StagingCaptionPatch.Text("Isp ")+"436s"=="比冲 436s" && StagingCaptionPatch.Text("TWR ")+"2.6"=="推重比 2.6","分级显示保留数值和单位");
        Check(StagingCaptionPatch.Text("VAC")=="真空" && StagingCaptionPatch.Text("ATM")=="大气" && StagingCaptionPatch.Text("TOTAL dV")=="总Δv" && Runtime.GaugeText("RES")=="资源","分级工况、速度增量与资源按钮翻译");
        Check(StagingCaptionPatch.Text("###seqenv")=="###seqenv" && StagingCaptionPatch.Text("玩家名称")=="玩家名称","分级控件 ID 及未知文本不改写");
        var gaugeLabels=new[]{"Enabled","Always on top","Reset","Detached","Lock"};
        var gaugeMenuIds=gaugeLabels.ToDictionary(x=>x,x=>GaugeContextMenuPatch.Caption(x,false));
        Check(GaugeContextMenuPatch.Caption("Lock",false)=="锁定      ###Lock" && GaugeContextMenuPatch.Caption("Detached",false)=="独立窗口      ###Detached","仪表右键显示层翻译及勾选区域留白");
        Check(GaugeContextMenuPatch.Caption("Always on top",true).TrimEnd()=="始终置顶" && !GaugeContextMenuPatch.Caption("Always on top",true).Contains("###"),"禁用仪表菜单不显示隐藏 ID");
        Check(GaugeContextMenuPatch.Caption("玩家仪表名",false)=="玩家仪表名" && GaugeContextMenuPatch.Caption("NeedsResize",true).TrimEnd()=="需要调整大小" && !GaugeContextMenuPatch.Caption("NeedsResize",true).Contains("###"),"仪表未知名字保留，禁用调试字段翻译且不显示隐藏 ID");
        Runtime.ApplyLanguage("en-US");
        foreach(var label in gaugeLabels) foreach(uint seed in new uint[]{0,0x735A1913})
            Check(Hash(GaugeContextMenuPatch.Caption(label,false)+"   ",seed)==Hash(gaugeMenuIds[label]+"   ",seed),"仪表右键中英 ID 与 helper 尾部空格: "+label+" seed="+seed);
        Check(GaugeContextMenuPatch.Caption("Always on top",true).TrimEnd()=="Always on top","仪表禁用菜单英文回退");
        foreach(var label in startupLabels) foreach(uint seed in new uint[]{0,0x735A1913})
            Check(Hash(Runtime.StartupMenuText(label),seed)==Hash(startupIds[label],seed),"启动系统中英 ID: "+label+" seed="+seed);
        Check(Runtime.EditorNativeText("At Apollo11 On Luna",2)=="At Apollo11 On Luna" && StagingCaptionPatch.Text("Isp ")=="Isp " && Runtime.GaugeText("RES")=="RES","新增地点与分级文本可恢复英文");
        Check(Hash(Runtime.EditorNativeSelectable("At Apollo11 On Luna###location-key",2),0)==Hash(locationId,0),"地点跨语言 ID 稳定");
        Check(GaugeFontPatch.Name()=="MilSpec33558","仪表英文恢复原字体");
        Check(Hash(HudLayoutPatches.CustomLayoutMenuLabel(longName,true),0x735A1913)==Hash(zhLong,0x735A1913),"长中文自定义名跨语言 ID 稳定");
        Runtime.ApplyLanguage("zh-CN");
        // Inventory the actual inspected XML. Display names can be overridden
        // by GameData; do not infer a friendly English label from a resource ID.
        var xml = Directory.GetFiles(Path.Combine(Game,"Content","Core"),"*.xml")
            .Select(System.Xml.Linq.XDocument.Load).ToArray();
        var definitions = xml.SelectMany(d=>d.Root!.Elements("Part")).ToArray();
        Check(definitions.Length==180,"5602 Core 顶层部件定义数（包含隐藏和测试资源）");
        foreach(var definition in definitions)
        {
            string key=(string)definition.Attribute("Id")!;
            string name=xml.SelectMany(d=>d.Root!.Elements("PartGameData"))
                .Where(n=>(string?)n.Attribute("Id")==key)
                .Select(n=>(string?)n.Attribute("DisplayName")).LastOrDefault(n=>!string.IsNullOrEmpty(n))
                ?? (string?)definition.Attribute("DisplayName") ?? key;
            var template=new KSA.PartTemplate { Id=key, DisplayName=name };
            string caption=Runtime.PartName(template);
            Check(key=="CorePropulsionA_Prefab_EngineA4" ? caption=="VTR-10" : caption.Any(c=>c>='\u4e00' && c<='\u9fff'),"主部件类型翻译或纯型号保留: "+key);
            Check(template.Id==key && template.DisplayName==name,"模板原始资源 ID 与显示字段不变: "+key);
        }
        var engineTemplate=new KSA.PartTemplate { Id="CorePropulsionA_Prefab_EngineA5", DisplayName="LR91 Vac + Verniers" };
        var engine=(KSA.Part)RuntimeHelpers.GetUninitializedObject(typeof(KSA.Part));
        engine.Template=engineTemplate;
        var nameField=AccessTools.Field(typeof(KSA.Part),"<DisplayName>k__BackingField");
        nameField.SetValue(engine,engineTemplate.DisplayName);
        string engineMenu=FlightPartNamePatch.MenuCaption(engine);
        Check(Runtime.PartCaption(engine)=="LR91 真空型发动机（带游动发动机）A5","LR91 默认名称及型号、变体保留");
        foreach(uint seed in new uint[]{0,0x735A1913})
            Check(Hash(engineMenu,seed)==Hash("###"+engineTemplate.DisplayName,seed),"飞行部件菜单使用固定规范 ID seed="+seed);
        foreach(string custom in new[]{"玩家自定义发动机","Fuel Port","玩家部件###custom-id"})
        {
            nameField.SetValue(engine,custom);
            Check(Runtime.PartCaption(engine)==custom && FlightPartNamePatch.MenuCaption(engine)==custom,"玩家自定义名称不因命中词典而被翻译: "+custom);
        }
        nameField.SetValue(engine,engineTemplate.DisplayName);
        Runtime.ApplyLanguage("en-US");
        Check(Runtime.PartName(engineTemplate)==engineTemplate.DisplayName && Runtime.PartCaption(engine)==engineTemplate.DisplayName,
            "部件名称切换英文恢复原始显示");
        foreach(uint seed in new uint[]{0,0x735A1913})
            Check(Hash(FlightPartNamePatch.MenuCaption(engine),seed)==Hash(engineMenu,seed),"飞行部件菜单中英文 ID 一致 seed="+seed);
        Runtime.ApplyLanguage("zh-CN");
        Check(FlightPartNamePatch.MenuCaption(engine)==engineMenu,"部件名称中英往返一致");
        ExpandedChecks();
        var patcher=new Harmony("local.ksa.chinese.validation.patches");
        foreach(var type in typeof(LanguagePlugin).Assembly.GetTypes().Where(t=>t.GetCustomAttributes<HarmonyPatch>().Any()).OrderBy(t=>t.Name))
        {
            var selector=type.GetMethod("TargetMethods",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static)
                ?? type.GetMethod("TargetMethod",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static);
            IEnumerable<MethodBase> originals;
            if(selector!=null) { var value=selector.Invoke(null,null); originals=value is MethodBase single ? new[]{single} : (IEnumerable<MethodBase>)value!; }
            else {
                var info=HarmonyMethod.Merge(type.GetCustomAttributes<HarmonyPatch>().Select(a=>a.info).ToList());
                originals=new[]{AccessTools.Method(info.declaringType,info.methodName,info.argumentTypes)};
            }
            foreach(var method in originals)
            {
                if(method==null) throw new MissingMethodException(type.Name);
                var before=PatchProcessor.GetOriginalInstructions(method);
                var transpiler=type.GetMethods().SingleOrDefault(m=>m.GetCustomAttribute<HarmonyTranspiler>()!=null);
                List<CodeInstruction>? after=null;
                if(transpiler!=null) {
                    var args=transpiler.GetParameters().Select(p=>p.Name=="__originalMethod" ? (object)method : p.ParameterType==typeof(System.Reflection.Emit.ILGenerator) ? throw new NotSupportedException(type.Name+" requires generator") : before.Select(i=>new CodeInstruction(i)).ToList()).ToArray();
                    after=((IEnumerable<CodeInstruction>)transpiler.Invoke(null,args)!).ToList();
                }
                var body=method.GetMethodBody()?.GetILAsByteArray() ?? Array.Empty<byte>();
                var addedCalls=after?.Count(i=>i.operand is MethodInfo mi && mi.DeclaringType?.Assembly==typeof(LanguagePlugin).Assembly) ?? 0;
                Targets.Add(new{group=type.Name,signature=method.ToString(),declaringType=method.DeclaringType?.FullName,ilSha256=Convert.ToHexString(SHA256.HashData(body)).ToLowerInvariant(),originalInstructions=before.Count,transformedInstructions=after?.Count,pluginCalls=addedCalls});
            }
            var patched=patcher.CreateClassProcessor(type).Patch();
            Check(patched.Count>0,"补丁实际安装: "+type.Name);
        }
        patcher.UnpatchAll(patcher.Id);
        Runtime.RestoreProvider();
        Check(ReferenceEquals(Brutal.Localization.LStrings.LocalizedStringProvider,previous),"卸载恢复原提供器");
        Check(KSA.LStrings.SettingsDisplayResolution.Localized=="Resolution","卸载回退原生英文");
        var report=new{schema=1,gameVersion=typeof(KSA.Program).Assembly.GetName().Version?.ToString(),ksaSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(Game,"KSA.dll")))).ToLowerInvariant(),fixture=Path.GetRelativePath(Root,Fixture),checks=Checks,targets=Targets,graphicalValidation="not covered by this headless test"};
        File.WriteAllText(Path.Combine(Root,"reports","5638-bootstrap-validation.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"PASS: {Checks.Count} checks, {Targets.Count} target references");
    }
    static uint Hash(string s,uint seed) { var b=Encoding.UTF8.GetBytes(s+'\0'); return ImHash(b,0,seed); }
    [DllImport("KsaCheckImGui",EntryPoint="?ImHashStr@@YAIPEBD_KI@Z",ExactSpelling=true,CallingConvention=CallingConvention.Cdecl)]
    static extern uint ImHash(byte[] data,nuint size,uint seed);
}
