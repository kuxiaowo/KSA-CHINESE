using System.Reflection;
using System.Reflection.Emit;
using System.Text.Json;
using System.Text;
using Brutal.ImGuiApi;
using HarmonyLib;
using KSA;

namespace KsaChinese;

// The manifest is generated from reviewed call-site decisions for the inspected
// binary. It contains method identities and display literals, not game code.
public sealed class DisplaySite
{
    public string Type { get; set; } = "";
    public string Method { get; set; } = "";
    public string[] Fields { get; set; } = Array.Empty<string>();
    public string[] Literals { get; set; } = Array.Empty<string>();
}

public static class ExpandedUi
{
    [ThreadStatic] static Stack<MethodBase>? contexts;
    static readonly Dictionary<LanguagePack,Dictionary<string,string>> allText=new();
    static readonly Dictionary<string,byte[]> utf8Text=new(StringComparer.Ordinal);
    static Dictionary<(string Type,string Method), DisplaySite>? sites;
    static Dictionary<(string Type,string Method), DisplaySite> Sites => sites ??= JsonSerializer.Deserialize<DisplaySite[]>(
        File.ReadAllText(Path.Combine(Runtime.ModRoot,"display-sites.json")))!
        .ToDictionary(s=>(s.Type,s.Method));
    public static string Identity(MethodBase method) => (method.DeclaringType?.FullName ?? "") + "." + method.Name;
    public static IEnumerable<MethodBase> Methods()
    {
        var selected=new HashSet<MethodBase>();
        foreach(var entry in Sites.Values)
        {
            var type=typeof(KSA.Program).Assembly.GetType(entry.Type,true)!;
            var declared=type.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance|BindingFlags.DeclaredOnly)
                .Where(m=>m.Name==entry.Method && m.GetMethodBody()!=null).ToArray();
            if(declared.Length==0) throw new MissingMethodException(entry.Type,entry.Method);
            foreach(var method in declared.Where(m=>!m.ContainsGenericParameters)) selected.Add(method);
        }
        // Resolve the closed generic display helpers actually called by this
        // build rather than patching an open definition shared across types.
        foreach(var caller in selected.ToArray())
            foreach(var callee in PatchProcessor.GetOriginalInstructions(caller).Select(i=>i.operand).OfType<MethodInfo>())
                if(callee.IsGenericMethod&&!callee.ContainsGenericParameters&&Sites.ContainsKey((callee.DeclaringType!.FullName!,callee.Name)))selected.Add(callee);
        return selected;
    }
    public static void Enter(MethodBase method) => (contexts ??= new()).Push(method);
    public static void Leave() { if(contexts is {Count:>0}) contexts.Pop(); }
    public static int Depth => contexts?.Count ?? 0;
    public static void RestoreDepth(int depth) { while(Depth>depth) Leave(); }
    public static bool InUi => contexts is {Count:>0};
    public static MethodBase? Context => contexts is {Count:>0} ? contexts.Peek() : null;
    public static string[] StaticOptions(string[] original)=>original.Select(Text).ToArray();
    public static ImString PackedOptions(ImString original)
    {
        string source=Encoding.UTF8.GetString(original.AsSpan());
        var names=source.Split('\0');
        return Runtime.ImText(string.Join('\0',names.Select(Text)));
    }
    public static bool PackedCombo(ImString label,ref int current,ImString options,int height)
    {
        var owner=Context?.DeclaringType;
        if(owner?.Name=="LightDebug"||owner==typeof(ProfilerWindowBase))options=PackedOptions(options);
        return ImGui.Combo(label,ref current,options,height);
    }
    static Dictionary<string,string> DisplayDictionary(LanguagePack pack)
    {
        if(allText.TryGetValue(pack,out var cached))return cached;
        var candidates=new Dictionary<string,HashSet<string>>(StringComparer.Ordinal);
        foreach(var section in new[]{pack.Literals,pack.Ui,pack.Tooltips,pack.Editor,pack.Startup,pack.Controls,pack.Hud,pack.Planning,pack.Utility,pack.Gauges})
            foreach(var entry in section)
            {
                if(!candidates.TryGetValue(entry.Key,out var choices))candidates[entry.Key]=choices=new(StringComparer.Ordinal);
                choices.Add(entry.Value);
            }
        cached=candidates.Where(e=>e.Value.Count==1).ToDictionary(e=>e.Key,e=>e.Value.Single(),StringComparer.Ordinal);
        foreach(var entry in pack.Display)cached[entry.Key]=entry.Value;
        allText[pack]=cached;return cached;
    }
    public static string Text(string original)
    {
        if(contexts is {Count:>0} && Runtime.CurrentPack.Display.TryGetValue(Identity(contexts.Peek())+"|"+original,out var scoped)) return scoped;
        return DisplayDictionary(Runtime.CurrentPack).GetValueOrDefault(original,original);
    }
    public static string Menu(string original)
    {
        int marker=original.IndexOf("##",StringComparison.Ordinal);
        string visible=marker<0?original:original[..marker];
        bool known=Runtime.Packs.Values.Any(p=>DisplayDictionary(p).ContainsKey(visible)
            ||(Context is not null&&p.Display.ContainsKey(Identity(Context)+"|"+visible)));
        if(!known) return original;
        int stable=original.IndexOf("###",StringComparison.Ordinal);
        return Text(visible)+(stable<0?"###"+original:original[stable..]);
    }
    public static ImString Caption(ImString source,bool interactive)
    {
        string original=source.ToString();
        if(contexts is {Count:>0} && contexts.Peek().DeclaringType==typeof(GaugeCanvas)
            && contexts.Peek().Name==nameof(GaugeCanvas.OnDrawUi)
            && original is "Enabled" or "Always on top" or "Reset" or "Detached" or "Lock" or "Resizing" or "Selected" or "Interacting" or "NeedsResize")return source;
        string value=interactive?Menu(original):Text(original);
        return value==original?source:Runtime.ImText(value);
    }
    public static ImString PlainCaption(ImString source)=>Caption(source,false);
    // Used only for inventoried static UTF-8 plot/draw-list literals. Never
    // intercept user buffers, input values or an arbitrary rendering call.
    public static ReadOnlySpan<byte> Utf8Plain(ReadOnlySpan<byte> source)=>Utf8Caption(source,false);
    public static ReadOnlySpan<byte> Utf8Menu(ReadOnlySpan<byte> source)=>Utf8Caption(source,true);
    static ReadOnlySpan<byte> Utf8Caption(ReadOnlySpan<byte> source,bool interactive)
    {
        string original=Encoding.UTF8.GetString(source);
        string translated=interactive?Menu(original):Text(original);
        if(original==translated)return source;
        if(!utf8Text.TryGetValue(translated,out var bytes))utf8Text[translated]=bytes=Encoding.UTF8.GetBytes(translated);
        return bytes;
    }
    public static string Tooltip(string original)
    {
        string updated = Update5707Text.Tooltip(original);
        if (updated != original) return updated;
        const string ratio="The oxidizer-to-fuel mass ratio fed to the combustion chamber.\nTypical value for this mixture: ";
        if(Runtime.SelectedLocale=="zh-CN"&&original.StartsWith(ratio,StringComparison.Ordinal))
            return "送入燃烧室的氧化剂与燃料质量比。\n此组合的典型值："+original[ratio.Length..];
        return Text(original);
    }
    public static ImString MenuCaption(ImString source)=>Caption(source,true);
    static ImString HelperMenuCaption(ImString source,bool disabled)
    {
        string original=source.ToString();
        string menu=Menu(original);
        if(menu==original)return source;
        int marker=menu.IndexOf("###",StringComparison.Ordinal);
        return Runtime.ImText(disabled?(marker<0?menu:menu[..marker])+"      ":menu[..marker]+"      "+menu[marker..]);
    }
    public static bool HelperMenuValue(ImString label,bool selected,bool disabled)
        =>ImGuiHelper.DrawMenuItem(HelperMenuCaption(label,disabled),selected,disabled);
    public static bool HelperMenuToggle(ImString label,ref bool selected,bool disabled)
        =>ImGuiHelper.DrawMenuItem(HelperMenuCaption(label,disabled),ref selected,disabled);
    public static bool HelperButton(ImString label,Brutal.Numerics.byte4? normal,Brutal.Numerics.byte4? hovered,Brutal.Numerics.byte4? active)
        =>ImGuiHelper.DrawButton(MenuCaption(label),normal,hovered,active);
    public static ImString FieldCaption(ImString source)
    {
        if(contexts is not {Count:>0})return source;
        var method=contexts.Peek();
        if(!Sites.TryGetValue((method.DeclaringType!.FullName!,method.Name),out var site)
            || !site.Fields.Contains(source.ToString(),StringComparer.Ordinal))return source;
        string original=source.ToString();
        int marker=original.IndexOf("##",StringComparison.Ordinal);
        return PlainCaption(marker<0?source:Runtime.ImText(original[..marker]));
    }
    public static bool AllowsLiteral(MethodBase method,string value)
        => Sites.TryGetValue((method.DeclaringType!.FullName!,method.Name),out var site)
            && site.Literals.Contains(value,StringComparer.Ordinal);
    public static string Content(string kind,string id,string field,string original)
        => Runtime.CurrentPack.Content.GetValueOrDefault(kind+":"+id+":"+field,original);
    public static string SubstanceShort(Substance value)=>Content("Substance",value.Id,"ShortName",value.ShortName);
    public static string SubstanceFull(Substance value)=>Content("Substance",value.Id,"FullName",Content("Substance",value.Id,"ShortName",value.FullName));
    public static string SubstanceDescription(Substance value)=>Content("Substance",value.Id,"Description",value.Description);
    public static string PhaseName(SubstancePhase value)
    {
        string translated=SubstanceShort(value.Substance);
        if(translated==value.Substance.ShortName)return value.Name;
        if(value.Name==value.Substance.ShortName)return translated;
        if(value.Name.StartsWith("Liquid ",StringComparison.Ordinal))return "液态"+translated;
        if(value.Name==value.Substance.ShortName+" Vapor")return translated+"蒸气";
        if(value.Name==value.Substance.ShortName+" Ice")return "固态"+translated;
        if(value.Name.StartsWith("Gas ",StringComparison.Ordinal))return "气态"+translated;
        if(value.Name.StartsWith("Solid ",StringComparison.Ordinal))return "固态"+translated;
        return value.Name.Replace(value.Substance.ShortName,translated,StringComparison.Ordinal);
    }
    public static string ReactionName(Reaction value)=>Content("Reaction",value.Id,"Name",value.Name);
    public static string ReactionDescription(Reaction value)=>Content("Reaction",value.Id,"Description",value.Description);
    public static string GrainName(GrainGeometry value)=>Content("GrainGeometry",value.Id,"Name",value.Name);
    public static string GrainShape(GrainGeometry value)=>Content("GrainGeometry",value.Id,"Shape",value.Shape);
    public static string GrainDescription(GrainGeometry value)=>Content("GrainGeometry",value.Id,"Description",value.Description);
    public static string MixName(ReactantMix value)
    {
        if(Runtime.SelectedLocale!="zh-CN")return value.Name;
        foreach(var reaction in SubstanceLibrary.AllReactions())
            if(reaction.Name==value.Name)return ReactionName(reaction);
        return value.Reactants.Length==0?Text("None"):string.Join(" + ",value.Reactants.ToArray().Select(r=>PhaseName(r.SubstancePhase)));
    }
    public static void Category(ImString source)=>ImGui.TextDisabled(PlainCaption(source));
    public static string HistoryText(string source)=>Runtime.CurrentPack.History.GetValueOrDefault(source,source);
    public static void History(ImString source)
    {
        string original=source.ToString();
        ImGui.Text(Runtime.ImText(HistoryText(original)));
    }
    public static ImString OrbitAnomaly(ImString original,int branch)
        => Runtime.SelectedLocale=="zh-CN" ? Runtime.ImText(branch==0?"偏近点角":"真近点角") : original;
}

[HarmonyPatch]
public static class EnumComboDisplayPatch
{
    public static IEnumerable<MethodBase> TargetMethods()=>ExpandedUi.Methods()
        .SelectMany(m=>PatchProcessor.GetOriginalInstructions(m).Select(i=>i.operand).OfType<MethodInfo>())
        .Where(m=>m.DeclaringType==typeof(ImGuiHelper)&&m.Name is "DrawCombo" or "DrawComboWithTooltips"
            &&m.IsGenericMethod&&!m.ContainsGenericParameters&&m.GetGenericArguments().Length==1&&m.GetGenericArguments()[0].IsEnum)
        .Distinct();
    public static bool Preview(ImString id,ImString value,ImGuiComboFlags flags)
        =>ImGui.BeginCombo(id,ExpandedUi.PlainCaption(value),flags);
    public static bool Option(ImString label,bool selected,ImGuiSelectableFlags flags,in Brutal.Numerics.float2? size)
        =>ImGui.Selectable(ExpandedUi.MenuCaption(label),selected,flags,in size);
    [HarmonyTranspiler,HarmonyPriority(Priority.Last)]
    public static IEnumerable<CodeInstruction> Translate(IEnumerable<CodeInstruction> instructions)
    {
        var body=instructions.ToList();bool field=false;
        for(int index=0;index<body.Count;index++)
        {
            var i=body[index];
            if(i.operand is MethodInfo m&&m.DeclaringType==typeof(ImGui))
            {
                var args=m.GetParameters();
                if(m.Name=="BeginCombo"&&args.Length==3&&args[1].ParameterType==typeof(ImString))
                {i.opcode=OpCodes.Call;i.operand=AccessTools.Method(typeof(EnumComboDisplayPatch),nameof(Preview));}
                else if(m.Name=="Selectable"&&args.Length==4&&args[1].ParameterType==typeof(bool))
                {i.opcode=OpCodes.Call;i.operand=AccessTools.Method(typeof(EnumComboDisplayPatch),nameof(Option));}
                else if(!field&&m.Name=="Text"&&index>0&&body[index-1].opcode==OpCodes.Ldarg_0)
                {yield return new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(ExpandedUi),nameof(ExpandedUi.FieldCaption)));field=true;}
            }
            yield return i;
        }
    }
}

[HarmonyPatch(typeof(ConsoleWidgets),nameof(ConsoleWidgets.Checkbox))]
public static class CheckboxStateDisplayPatch
{
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Translate(IEnumerable<CodeInstruction> instructions)
    {
        foreach(var i in instructions)
        {
            yield return i;
            if(i.opcode==OpCodes.Ldstr&&i.operand is string value&&value is "ON" or "OFF")
                yield return new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(ExpandedUi),nameof(ExpandedUi.Text)));
        }
    }
}

[HarmonyPatch(typeof(VehicleEditor),nameof(VehicleEditor.DrawPartUi))]
public static class AddSubpartDisplayPatch
{
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Translate(IEnumerable<CodeInstruction> instructions)
    {
        bool add=false;int reads=0;
        foreach(var i in instructions)
        {
            if(i.opcode==OpCodes.Ldstr&&i.operand as string=="Add ")add=true;
            if(add&&i.opcode==OpCodes.Ldfld&&i.operand is FieldInfo field&&field.Name=="Id")
            {
                // The first ID after this exact Add-button literal is its
                // visible caption. The later ID used to create the part stays native.
                i.opcode=OpCodes.Call;i.operand=AccessTools.Method(typeof(Runtime),nameof(Runtime.PartName));add=false;reads++;
            }
            yield return i;
        }
        if(reads!=1)throw new InvalidOperationException($"Add-subpart display site changed: {reads}/1.");
    }
}

[HarmonyPatch]
public static class ExpandedUiPatch
{
    public static IEnumerable<MethodBase> TargetMethods()=>ExpandedUi.Methods();
    [HarmonyPrefix] public static void Enter(MethodBase __originalMethod,out int __state)
    { __state=ExpandedUi.Depth; ExpandedUi.Enter(__originalMethod); }
    [HarmonyFinalizer] public static Exception? Leave(Exception? __exception,int __state)
    { ExpandedUi.RestoreDepth(__state); return __exception; }
    static readonly HashSet<string> Interactive = new(StringComparer.Ordinal)
    {"Begin","BeginMenu","BeginTabItem","MenuItem","Button","SmallButton","Checkbox","Selectable","TreeNode","TreeNodeEx","CollapsingHeader","BeginCombo","Combo","ColorEdit3","ColorEdit4","ColorPicker3","ColorPicker4","RadioButton","TableSetupColumn"};
    static readonly HashSet<string> InternalIds=new(StringComparer.Ordinal)
    {"BeginTable","BeginChild","BeginTabBar","BeginPopup","BeginPopupModal","OpenPopup","IsPopupOpen","InvisibleButton","PushID","GetID","SetDragDropPayload","AcceptDragDropPayload"};
    static bool IsInternalId(MethodInfo? next)=>next?.DeclaringType==typeof(ImGui)&&InternalIds.Contains(next.Name);
    static MethodInfo? NextUi(List<CodeInstruction> body,int from)
    {
        for(int i=from+1;i<Math.Min(body.Count,from+100);i++)
            if(body[i].operand is MethodInfo m && (m.DeclaringType==typeof(ImGui)||m.DeclaringType==typeof(ImGuiHelper)||m.DeclaringType==typeof(ConsoleWidgets))) return m;
        return null;
    }
    static bool IsInteractive(MethodInfo? next)=>next?.DeclaringType==typeof(ImGui)
        && (Interactive.Contains(next.Name)||next.Name.StartsWith("Input",StringComparison.Ordinal)||next.Name.StartsWith("Slider",StringComparison.Ordinal)||next.Name.StartsWith("Drag",StringComparison.Ordinal));
    static bool IsHelperField(MethodInfo? next)=>next?.DeclaringType==typeof(ImGuiHelper)
        && next.GetParameters().Length>0 && next.GetParameters()[0].ParameterType==typeof(ImString)
        && next.Name.StartsWith("Draw",StringComparison.Ordinal)
        && next.Name is not ("DrawTextWidget" or "Draw2ColumnTextWidget" or "DrawTooltip");
    [HarmonyTranspiler,HarmonyPriority(Priority.Last)]
    public static IEnumerable<CodeInstruction> Translate(IEnumerable<CodeInstruction> instructions,MethodBase __originalMethod)
    {
        var body=instructions.ToList();int anomalies=0,historyTexts=0;
        for(int i=0;i<body.Count;i++)
        {
            var instruction=body[i];
            if(instruction.opcode==OpCodes.Ldsfld&&instruction.operand is FieldInfo options&&options.FieldType==typeof(string[])
                &&((options.DeclaringType?.Name=="ParticleEmitterDebugEditor"&&__originalMethod.Name=="DrawEmitterFields"
                    &&options.Name is "RendererNames" or "SpawnModeNames" or "EmitterShapeNames" or "SpawnTypeNames")
                ||(options.DeclaringType?.Name=="VolumetricTrailRenderer"&&options.Name=="DebugModeNames")
                ||(options.DeclaringType?.Name=="DebugWindow"&&options.DeclaringType.DeclaringType?.Name=="DistantGlintEditor"&&options.Name=="FalloffFunctionNames")))
            {
                yield return instruction;
                yield return new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(ExpandedUi),nameof(ExpandedUi.StaticOptions)));
                continue;
            }
            if(instruction.operand is MethodInfo combo&&combo.DeclaringType==typeof(ImGui)&&combo.Name=="Combo"
                &&combo.GetParameters() is [_,_,{ParameterType:var optionType},_]&&optionType==typeof(ImString)
                &&__originalMethod.DeclaringType?.Name is "LightDebug" or "ProfilerWindowBase")
            { instruction.opcode=OpCodes.Call;instruction.operand=AccessTools.Method(typeof(ExpandedUi),nameof(ExpandedUi.PackedCombo)); }
            if(instruction.operand is MethodInfo helper&&helper.DeclaringType==typeof(ImGuiHelper))
            {
                var pars=helper.GetParameters();
                if(helper.Name=="DrawMenuItem"&&pars.Length==3&&pars[0].ParameterType==typeof(ImString)&&pars[2].ParameterType==typeof(bool))
                {instruction.opcode=OpCodes.Call;instruction.operand=AccessTools.Method(typeof(ExpandedUi),pars[1].ParameterType==typeof(bool)?nameof(ExpandedUi.HelperMenuValue):nameof(ExpandedUi.HelperMenuToggle));}
                else if(helper.Name=="DrawButton"&&pars.Length==4&&pars[0].ParameterType==typeof(ImString)&&pars[1].ParameterType==typeof(Brutal.Numerics.byte4?))
                {instruction.opcode=OpCodes.Call;instruction.operand=AccessTools.Method(typeof(ExpandedUi),nameof(ExpandedUi.HelperButton));}
            }
            // These getters are replaced only in explicitly inventoried display
            // methods. Source fields, IDs, sorting, serializers and physics stay native.
            if(instruction.operand is MethodInfo getter && getter.DeclaringType==typeof(Part) && getter.Name=="get_DisplayName"
                && !(__originalMethod.DeclaringType==typeof(Part)&&__originalMethod.Name=="OnDrawUi"))
            { instruction.opcode=OpCodes.Call;instruction.operand=AccessTools.Method(typeof(Runtime),nameof(Runtime.PartCaption)); }
            else if(instruction.opcode==OpCodes.Ldfld && instruction.operand is FieldInfo field)
            {
                string? wrapper=(field.DeclaringType,field.Name) switch {
                    (var t,"Name") when t==typeof(SubstancePhase)=>nameof(ExpandedUi.PhaseName),
                    (var t,"ShortName") when t==typeof(Substance)=>nameof(ExpandedUi.SubstanceShort),
                    (var t,"FullName") when t==typeof(Substance)=>nameof(ExpandedUi.SubstanceFull),
                    (var t,"Description") when t==typeof(Substance)=>nameof(ExpandedUi.SubstanceDescription),
                    (var t,"Name") when t==typeof(Reaction)=>nameof(ExpandedUi.ReactionName),
                    (var t,"Description") when t==typeof(Reaction)=>nameof(ExpandedUi.ReactionDescription),
                    (var t,"Name") when t==typeof(GrainGeometry)=>nameof(ExpandedUi.GrainName),
                    (var t,"Shape") when t==typeof(GrainGeometry)=>nameof(ExpandedUi.GrainShape),
                    (var t,"Description") when t==typeof(GrainGeometry)=>nameof(ExpandedUi.GrainDescription),_=>null};
                if(wrapper!=null){instruction.opcode=OpCodes.Call;instruction.operand=AccessTools.Method(typeof(ExpandedUi),wrapper);}
            }
            else if(instruction.operand is MethodInfo mix && mix.DeclaringType==typeof(ReactantMix)&&mix.Name=="get_Name")
            {instruction.opcode=OpCodes.Call;instruction.operand=AccessTools.Method(typeof(ExpandedUi),nameof(ExpandedUi.MixName));}
            if(instruction.operand is MethodInfo call && call.DeclaringType==typeof(ImGui))
            {
                if(__originalMethod.DeclaringType==typeof(SubstanceLibrary)&&__originalMethod.Name=="DrawReactionOptions" && call.Name==nameof(ImGui.TextDisabled))
                {instruction.opcode=OpCodes.Call;instruction.operand=AccessTools.Method(typeof(ExpandedUi),nameof(ExpandedUi.Category));}
                if(__originalMethod.DeclaringType==typeof(ChangeLog)&&__originalMethod.Name==nameof(ChangeLog.DrawInTable)&&call.Name==nameof(ImGui.Text))
                {
                    // The second of six inspected Text sites displays Lines[j].
                    // The others are revision/date/author metadata and stay native.
                    if(++historyTexts==2){instruction.opcode=OpCodes.Call;instruction.operand=AccessTools.Method(typeof(ExpandedUi),nameof(ExpandedUi.History));}
                }
            }
            yield return instruction;
            if(instruction.opcode==OpCodes.Newobj && instruction.operand is ConstructorInfo ctor
                &&ctor.DeclaringType==typeof(ReadOnlySpan<byte>)&&i>=2&&body[i-2].opcode==OpCodes.Ldsflda
                &&body[i-2].operand is FieldInfo utf8 &&utf8.DeclaringType?.Name=="<PrivateImplementationDetails>"
                &&(__originalMethod.DeclaringType?.Name is "ProfilerPlots" or "ProfilerStackedArea" or "TargetTrackWindow" or "CascadeVisualizer")
                &&!(i+1<body.Count&&body[i+1].operand is MethodInfo im&&im.DeclaringType==typeof(ImString)))
            {
                var draw=body.Skip(i+1).Select(c=>c.operand).OfType<MethodInfo>()
                    .FirstOrDefault(m=>m.DeclaringType?.Name=="ImPlot"||m.Name=="AddText");
                bool legend=draw?.DeclaringType?.Name=="ImPlot"&&draw.Name.StartsWith("Plot",StringComparison.Ordinal);
                yield return new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(ExpandedUi),legend?nameof(ExpandedUi.Utf8Menu):nameof(ExpandedUi.Utf8Plain)));
            }
            if(instruction.opcode==OpCodes.Ldstr && instruction.operand is string text && ExpandedUi.AllowsLiteral(__originalMethod,text))
            {
                var next=NextUi(body,i);
                if(!IsHelperField(next)&&!IsInternalId(next))yield return new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(ExpandedUi),IsInteractive(next)?nameof(ExpandedUi.Menu):nameof(ExpandedUi.Text)));
            }
            // A compiler UTF-8 literal has this exact static-data sequence. Do
            // not intercept arbitrary UTF-8 buffers containing player text.
            if(instruction.operand is MethodInfo conversion && conversion.DeclaringType==typeof(ImString)&&conversion.Name=="op_Implicit"
                && conversion.GetParameters() is [{ParameterType:var p}]&&p==typeof(ReadOnlySpan<byte>)
                && i>=3 && body[i-3].opcode==OpCodes.Ldsflda && body[i-3].operand is FieldInfo data && data.DeclaringType?.Name=="<PrivateImplementationDetails>")
            {
                var next=NextUi(body,i);
                if(__originalMethod.DeclaringType==typeof(Burn)&&__originalMethod.Name=="DrawBurnInfo"
                    && data.Name=="D89D877B0277D9E35FD34F74122FE91102D62700BAE6494C1C8D72E57AB578E4")
                {
                    yield return new CodeInstruction(OpCodes.Ldc_I4,anomalies++);
                    yield return new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(ExpandedUi),nameof(ExpandedUi.OrbitAnomaly)));
                }
                else if(!IsHelperField(next)&&!IsInternalId(next))yield return new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(ExpandedUi),IsInteractive(next)?nameof(ExpandedUi.MenuCaption):nameof(ExpandedUi.PlainCaption)));
            }
        }
        if(__originalMethod.DeclaringType==typeof(Burn)&&__originalMethod.Name=="DrawBurnInfo"&&anomalies!=2)
            throw new InvalidOperationException($"Expected two inspected anomaly labels, found {anomalies}.");
        if(__originalMethod.DeclaringType==typeof(ChangeLog)&&__originalMethod.Name==nameof(ChangeLog.DrawInTable)&&historyTexts!=6)
            throw new InvalidOperationException($"Expected six inspected history Text sites, found {historyTexts}.");
    }
}

// The material combo has a separate ID and preview. Transform only its visible
// preview/options, keeping the actual PbrMaterialReference and selection intact.
[HarmonyPatch]
public static class MaterialComboDisplayPatch
{
    public static IEnumerable<MethodBase> TargetMethods()=>typeof(ImGuiHelper).GetMethods(BindingFlags.Public|BindingFlags.Static)
        .Where(m=>m.Name=="DrawCombo"&&m.IsGenericMethodDefinition&&m.GetGenericArguments().Length==1
            &&m.GetParameters().Length>1&&m.GetParameters()[1].ParameterType==m.GetGenericArguments()[0].MakeByRefType()
            &&m.GetGenericArguments()[0].GetGenericParameterConstraints().Contains(typeof(IComboable)))
        .Select(m=>m.MakeGenericMethod(typeof(PbrMaterialReference)));
    public static string MaterialText(string source)=>Runtime.CurrentPack.Display.GetValueOrDefault(source,source);
    public static bool Preview(ImString id,ImString preview,ImGuiComboFlags flags)
        =>ImGui.BeginCombo(id,Runtime.ImText(MaterialText(preview.ToString())),flags);
    public static bool Option(ImString label,bool selected,ImGuiSelectableFlags flags,in Brutal.Numerics.float2? size)
        =>ImGui.Selectable(ExpandedUi.MenuCaption(label),selected,flags,in size);
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Translate(IEnumerable<CodeInstruction> instructions)
    {
        foreach(var i in instructions)
        {
            if(i.operand is MethodInfo m&&m.DeclaringType==typeof(ImGui)&&m.Name is "BeginCombo" or "Selectable")
            {i.opcode=OpCodes.Call;i.operand=AccessTools.Method(typeof(MaterialComboDisplayPatch),m.Name=="BeginCombo"?nameof(Preview):nameof(Option));}
            yield return i;
        }
    }
}
