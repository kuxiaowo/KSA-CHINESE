using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Xml.Linq;
using Brutal.ImGuiApi;
using HarmonyLib;
using KSA;
using KsaChinese;

internal static partial class Program
{
    static void ExpandedChecks()
    {
        var enumData=typeof(EnumCollections).GetFields(BindingFlags.Public|BindingFlags.Static)
            .Where(f=>f.FieldType.IsGenericType&&f.FieldType.GetGenericTypeDefinition()==typeof(StringEnumCollection<>))
            .Select(f=>{var v=f.GetValue(null)!;int length=(int)AccessTools.PropertyGetter(f.FieldType,"Length").Invoke(v,null)!;
                return new {field=f.Name,type=f.FieldType.GetGenericArguments()[0].FullName,names=Enumerable.Range(0,length).Select(i=>(string)f.FieldType.GetMethod("GetNameFromIndex")!.Invoke(v,new object[]{i})!).ToArray()};}).ToArray();
        File.WriteAllText(Path.Combine(Root,"work","full-localization-20261021","dynamic-ui-inventory.json"),JsonSerializer.Serialize(new{
            enums=enumData,
            combos=typeof(ImGui).GetMethods().Where(m=>m.Name=="Combo").Select(m=>new{signature=m.ToString(),parameters=m.GetParameters().Select(p=>new{name=p.Name,type=p.ParameterType.ToString()})}),
            particleEnums=typeof(KSA.Program).Assembly.GetTypes().Where(t=>t.IsEnum&&(t.FullName!.Contains("ParticleEmitterReference")||t.Name=="EmitterSpawnMode")).Select(t=>new{type=t.FullName,names=Enum.GetNames(t)})
        },new JsonSerializerOptions{WriteIndented=true}));
        Runtime.ApplyLanguage("zh-CN");
        Check(Runtime.CurrentPack.Parts.Count==650,"全部 180 个主部件与 470 个子部件拥有显示映射");
        var docs=Directory.GetFiles(Path.Combine(Game,"Content","Core"),"*.xml").Select(XDocument.Load).ToArray();
        var children=docs.SelectMany(x=>x.Root!.Elements("SubPart")).Where(x=>x.Attribute("Id")!=null).ToArray();
        Check(children.Length==470,"实际 XML 子部件定义数");
        foreach(var node in children)
        {
            string id=(string)node.Attribute("Id")!;
            string original=(string?)node.Attribute("DisplayName")??id;
            var value=new PartTemplate{Id=id,DisplayName=original};
            string zh=Runtime.PartName(value);
            Check(zh.Any(c=>c is >= '\u4e00' and <= '\u9fff')&&value.Id==id&&value.DisplayName==original,"子部件显示翻译且原始字段保留: "+id);
        }
        foreach(var id in new[]{"H2","O2","H2O","Aluminum.2014","CarbonFiber"})
        {
            var substance=(Substance)RuntimeHelpers.GetUninitializedObject(typeof(Substance));
            substance.Id=id; substance.ShortName=id;substance.FullName=id;substance.Description="source description";
            var before=substance.Id+"|"+substance.ShortName+"|"+substance.FullName+"|"+substance.Description;
            Check(ExpandedUi.SubstanceShort(substance)!=id&&ExpandedUi.SubstanceFull(substance)!=id,"按实际资源 ID 翻译物质或工程材料: "+id);
            foreach(var prefix in new[]{"Liquid ","Gas ","Solid "})
            {
                var phase=(SubstancePhase)RuntimeHelpers.GetUninitializedObject(typeof(SubstancePhase));
                phase.Id=prefix+id;phase.Name=prefix+id;phase.Substance=substance;
                Check(ExpandedUi.PhaseName(phase).Contains(ExpandedUi.SubstanceShort(substance))&&phase.Name==prefix+id&&phase.Id==prefix+id,"相态名称显示转换并保留相态键: "+prefix+id);
            }
            Check(before==substance.Id+"|"+substance.ShortName+"|"+substance.FullName+"|"+substance.Description,"物质对象名称和说明未被改写: "+id);
        }
        var grain=(GrainGeometry)RuntimeHelpers.GetUninitializedObject(typeof(GrainGeometry));
        grain.Id="Progressive";grain.Name="Progressive";grain.Shape="Tubular";grain.Description="source description";
        Check(ExpandedUi.GrainName(grain)=="递增推力型"&&ExpandedUi.GrainShape(grain)=="管形"&&ExpandedUi.GrainDescription(grain).Contains("推力"),"药柱名称、几何形状和性能说明分别翻译");
        Check(grain.Id=="Progressive"&&grain.Name=="Progressive"&&grain.Shape=="Tubular"&&grain.Description=="source description","药柱几何对象字段未改写");
        Check(ExpandedUi.OrbitAnomaly("EccentricAnomaly",0).ToString()=="偏近点角"&&ExpandedUi.OrbitAnomaly("EccentricAnomaly",1).ToString()=="真近点角","椭圆与非束缚轨道的异常角显示按实际物理量区分");
        const string ratio="The oxidizer-to-fuel mass ratio fed to the combustion chamber.\nTypical value for this mixture: ";
        Check(Runtime.TooltipText(ratio+"2.347").EndsWith("2.347")&&!Runtime.TooltipText(ratio+"2.347").StartsWith("The "),"发动机动态混合比说明翻译并保留实时数值");
        var labels=new[]{"Combustion Pressure","Over budget","Draw Wireframe","Frames","Always###debug-key","Frames##plot"};
        var translated=labels.ToDictionary(x=>x,ExpandedUi.Menu);
        Check(translated["Combustion Pressure"].Contains("###Combustion Pressure"),"新增交互标签具有稳定的原文标识");
        Check(ExpandedUi.Text("玩家部件 English name")=="玩家部件 English name"&&ExpandedUi.Menu("unlisted##internal")=="unlisted##internal","未审字符串和玩家命名保持原样");
        var array=new[]{"Point","VehicleSurface","PrecomputedTransform"};
        var localized=ExpandedUi.StaticOptions(array);
        Check(localized.Length==array.Length&&array.SequenceEqual(new[]{"Point","VehicleSurface","PrecomputedTransform"})&&localized[1]=="飞船表面","静态选项只转换副本，保持原数组与选项索引");
        var packed=ExpandedUi.PackedOptions("Default\0Time (ascending)\0Alloc (descending)\0").ToString();
        Check(packed.Split('\0').Length==4&&packed.Contains("耗时（升序）")&&packed.Contains("内存分配量（降序）"),"原生零分隔选项保留分隔和次序");
        var hierarchyType=typeof(KSA.Program).Assembly.GetType("KSA.GaugeHierarchy+HierarchyWindow")!;
        var hierarchy=(ImGuiWindow)RuntimeHelpers.GetUninitializedObject(hierarchyType);
        Check(HudWindowTitlePatch.DisplayTitle("Gauge Hierarchy###fixture",hierarchy)=="仪表层级###fixture"&&HudWindowTitlePatch.DisplayPlainTitle("Gauge Hierarchy",hierarchy)=="仪表层级","原生窗口标题保留固定 ID，控制台标题不暴露隐藏 ID");
        ReadOnlySpan<char> versionTitle="VERSION HISTORY";
        HudConsoleTitlePatch.Localize("KSA-VER",ref versionTitle);
        Check(versionTitle.SequenceEqual("版本历史"),"控制台风格的版本历史标题在实际绘制入口翻译");
        Check(Runtime.UiText("One Frame")=="1 帧","实际帧队列枚举选项接入设置预览");
        using(var history=JsonDocument.Parse(File.ReadAllText(Path.Combine(Game,"Content","Versions","v2026.10.X.5602.json"))))
        {
            var lines=history.RootElement.GetProperty("commits").EnumerateArray().SelectMany(c=>c.GetProperty("lines").EnumerateArray().Select(l=>l.GetString()!)).ToArray();
            Check(lines.Length==135&&lines.All(s=>ExpandedUi.HistoryText(s)!=s),"当前 5602 更新说明 135 条正文完整匹配显示译文");
            Check(ExpandedUi.HistoryText("2026-10-13")=="2026-10-13"&&ExpandedUi.HistoryText("JPLRepoRocketWerkz")=="JPLRepoRocketWerkz","更新说明日期和作者元数据保持原样");
        }
        var historyMethod=AccessTools.Method(typeof(ChangeLog),nameof(ChangeLog.DrawInTable));
        var historyBody=ExpandedUiPatch.Translate(PatchProcessor.GetOriginalInstructions(historyMethod).Select(i=>new CodeInstruction(i)),historyMethod).ToArray();
        Check(historyBody.Count(i=>i.operand is MethodInfo m&&m.DeclaringType==typeof(ExpandedUi)&&m.Name==nameof(ExpandedUi.History))==1
            &&historyBody.Count(i=>i.operand is MethodInfo m&&m.DeclaringType==typeof(ImGui)&&m.Name==nameof(ImGui.Text))==5,"历史正文只替换一处读取，五处日期、版本和作者显示调用保留");
        var sites=JsonSerializer.Deserialize<DisplaySite[]>(File.ReadAllText(Path.Combine(Runtime.ModRoot,"display-sites.json")))!;
        var entry=sites.First(s=>s.Fields.Contains("Combustion Pressure"));
        var method=typeof(KSA.Program).Assembly.GetType(entry.Type)!.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance).First(m=>m.Name==entry.Method);
        Check(ExpandedUi.FieldCaption("Combustion Pressure").ToString()=="Combustion Pressure","字段显示适配器在未登记的调用范围之外保持原样");
        int depth=ExpandedUi.Depth;
        ExpandedUiPatch.Enter(method,out int state);
        Check(ExpandedUi.FieldCaption("Combustion Pressure").ToString()==ExpandedUi.Text("Combustion Pressure"),"已登记辅助控件在最终可见标签处翻译");
        ExpandedUi.Enter(method);
        var failure=new InvalidOperationException("fixture");
        Check(ReferenceEquals(ExpandedUiPatch.Leave(failure,state),failure)&&ExpandedUi.Depth==depth,"异常退出时恢复显示上下文并保留原异常");
        var addMethod=AccessTools.Method(typeof(VehicleEditor),nameof(VehicleEditor.DrawPartUi));
        var addOriginal=PatchProcessor.GetOriginalInstructions(addMethod);
        var added=AddSubpartDisplayPatch.Translate(addOriginal.Select(i=>new CodeInstruction(i))).ToArray();
        Check(added.Count(i=>i.operand is MethodInfo m&&m.DeclaringType==typeof(Runtime)&&m.Name==nameof(Runtime.PartName))==1,"新增子部件按钮只替换一个已核对的名称读取位置");
        Check(addOriginal.Count(i=>i.opcode==OpCodes.Ldfld&&i.operand is FieldInfo f&&f.Name=="Id")-added.Count(i=>i.opcode==OpCodes.Ldfld&&i.operand is FieldInfo f&&f.Name=="Id")==1,"其余用于创建、选择子部件的资源 ID 读取保留");
        Runtime.ApplyLanguage("en-US");
        Check(ExpandedUi.StaticOptions(array).SequenceEqual(array)&&ExpandedUi.PackedOptions("Default\0Time (ascending)\0").ToString()=="Default\0Time (ascending)\0","新增数组和零分隔选项可回退英文");
        foreach(var label in labels)foreach(uint seed in new uint[]{0,0x735A1913})
            Check(Hash(ExpandedUi.Menu(label),seed)==Hash(translated[label],seed),"新增控件跨语言原生 ID: "+label+" seed="+seed);
        Check(ExpandedUi.Text("Combustion Pressure")=="Combustion Pressure"&&ExpandedUi.OrbitAnomaly("EccentricAnomaly",1).ToString()=="EccentricAnomaly","新显示适配器英文回退");
        Runtime.ApplyLanguage("zh-CN");
    }
}
