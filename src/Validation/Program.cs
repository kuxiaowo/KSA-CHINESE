using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HarmonyLib;
using KsaChinese;

internal static class Program
{
    static string Root = Path.GetFullPath(Environment.GetEnvironmentVariable("KSA_PROJECT_ROOT") ?? Environment.CurrentDirectory);
    static string Game = Environment.GetEnvironmentVariable("KSA_GAME_DIR") ?? @"F:\系统应用\Kitten Space Agency";
    static string Loader = Environment.GetEnvironmentVariable("KSA_LOADER_DIR") ?? Path.Combine(Root,".tools","StarMap-0.4.7");
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
        Check(LanguagePlugin.SupportsGameVersion("2026.10.13.5602") && !LanguagePlugin.SupportsGameVersion(null) && !LanguagePlugin.SupportsGameVersion("2026.10.13.5603"),"精确版本保护及未知版本拒绝");
        var previous=Brutal.Localization.LStrings.LocalizedStringProvider;
        Compatibility.Validate(Game,Path.Combine(Root,"assets","KsaChinese","compatibility.json"));
        Check(true,"游戏及依赖指纹匹配");
        bool mismatch=false;
        try { Compatibility.Validate(Fixture,Path.Combine(Root,"assets","KsaChinese","compatibility.json")); }
        catch(IOException) { mismatch=true; }
        Check(mismatch,"缺少或不同游戏文件安全拒绝");
        Runtime.Initialize();
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(KSA.LStrings).TypeHandle);
        Check(Runtime.CurrentPack.Native.Count==212,"原生213定义中212已核对接入，TabsId暂缓");
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
        Runtime.ApplyLanguage("en-US");
        Check(GaugeFontPatch.Name()=="MilSpec33558","仪表英文恢复原字体");
        Check(Hash(HudLayoutPatches.CustomLayoutMenuLabel(longName,true),0x735A1913)==Hash(zhLong,0x735A1913),"长中文自定义名跨语言 ID 稳定");
        Runtime.ApplyLanguage("zh-CN");
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
        var report=new{schema=1,gameVersion=typeof(KSA.Program).Assembly.GetName().Version?.ToString(),ksaSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(Game,"KSA.dll")))).ToLowerInvariant(),fixture=Fixture,checks=Checks,targets=Targets,graphicalValidation="not covered by this headless test"};
        File.WriteAllText(Path.Combine(Root,"reports","5602-bootstrap-validation.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"PASS: {Checks.Count} checks, {Targets.Count} target references");
    }
    static uint Hash(string s,uint seed) { var b=Encoding.UTF8.GetBytes(s+'\0'); return ImHash(b,0,seed); }
    [DllImport("KsaCheckImGui",EntryPoint="?ImHashStr@@YAIPEBD_KI@Z",ExactSpelling=true,CallingConvention=CallingConvention.Cdecl)]
    static extern uint ImHash(byte[] data,nuint size,uint seed);
}
