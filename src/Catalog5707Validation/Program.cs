// Public-catalog integration only, using unmodified Borea v0.5.2 components.
// No candidate transport, personal profile, desktop UI or gameplay acceptance.
using System.Security.Cryptography;
using System.Text.Json;
using Borea.Cli;
using Borea.Composition;
using Borea.Core.Game;
using Borea.Core.Instances;
using Borea.Core.Index;
using Borea.Core.Mods;
using Borea.Core.Settings;
using Borea.Core.Planning;
using Borea.Network.Index;
using Borea.Storage.Index;
using Borea.Storage.Paths;
using Borea.Storage.Settings;

if(args.Length!=3) throw new ArgumentException("Usage: <project> <fresh work/test-profile-* root> <isolated game copy>");
var project=Path.GetFullPath(args[0]);
var root=Path.GetFullPath(args[1]);
var game=Path.GetFullPath(args[2]);
var prefix=Path.Combine(project,"work")+Path.DirectorySeparatorChar;
if(!root.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)
    || !root[prefix.Length..].Split(Path.DirectorySeparatorChar)[0].StartsWith("test-profile-",StringComparison.OrdinalIgnoreCase)
    || Directory.Exists(root)) throw new ArgumentException("Use a fresh workspace test-profile-* root.");
for(string? cursor=Path.GetDirectoryName(root);cursor is not null;cursor=Path.GetDirectoryName(cursor))
    if(Directory.Exists(cursor)&&(File.GetAttributes(cursor)&FileAttributes.ReparsePoint)!=0)
        throw new ArgumentException("Profile root must not traverse a reparse point.");
if(!game.StartsWith(prefix,StringComparison.OrdinalIgnoreCase) || !File.Exists(Path.Combine(game,"KSA.dll")))
    throw new ArgumentException("Use an isolated game copy in project/work.");
Directory.CreateDirectory(root);
await new FileBoreaSettingsRepository(new GamePathProvider(null,boreaRoot:root)).SaveAsync(new BoreaSettings(game));
var checks=new List<string>();
var records=new List<object>();
async Task<BoreaServices> Graph()
{
    var reader=new ContentIndexReader(new GamePathProvider(null,boreaRoot:root));
    return await BoreaServices.BuildAsync(root,new HttpClientHandler(),
        new ContentIndexModRepository(new ReaderSnapshotProvider(reader)),new NoCandidates(),
        sharedProfileRoot:Path.Combine(root,"unused-shared-profile"));
}
string Clean(string text)=>text.Replace(project,"<project>",StringComparison.OrdinalIgnoreCase);
string Digest(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
void Check(bool ok,string message) { if(!ok)throw new Exception(message); checks.Add(message); }
async Task Command(params string[] command)
{
    using var output=new StringWriter();using var error=new StringWriter();
    var code=await BoreaCli.RunAsync(command,async _=>CliServices.From(await Graph()),output,error);
    records.Add(new{command,code,output=Clean(output.ToString()),error=Clean(error.ToString())});
    Console.WriteLine(string.Join(' ',command)+": "+code);
    if(code!=0)throw new Exception(Clean(error+"\n"+output));
}
await Command("index","refresh");
await Command("index","validate","--json");
await Command("loader","install","StarMap","--version","0.4.8");
foreach(var (id,version,oldVersion) in new[]{("KsaChinese","0.1.7","0.1.6"),("KsaBabel","0.2.3","0.2.2")})
{
    await Command("search",id,"--json");
    foreach(var suffix in new[]{"A","B"})
    {
        var name=id+" "+suffix;
        await Command("instance","create",name,"--json");
        await Command("install",id,"--instance",name,"--version",version);
    }
    using var graph=await Graph();
    var instances=await graph.Instances.GetAllAsync();
    var first=instances.Single(i=>i.Name==id+" A");
    var second=instances.Single(i=>i.Name==id+" B");
    var release=(await graph.OfflineContentIndex.GetReleaseAsync(id,ModVersion.Parse(version)))!;
    var old=(await graph.OfflineContentIndex.GetReleaseAsync(id,ModVersion.Parse(oldVersion)))!;
    var empty=new Instance("Compatibility probe",InstanceSource.Custom.Value);
    foreach(var build in new[]{"2026.10.19.5638","2026.10.25.5708"})
    {
        var plan=await graph.InstallPlanner.PlanAsync(new InstallPlanningRequest(empty,
            [new RequestedMod(release,InstallReason.Manual,true)],graph.OfflineContentIndex,
            GameVersion.Parse(build),OsPlatform.Windows));
        Check(!plan.IsReady,id+" rejects unsupported game "+build);
    }
    var oldPlan=await graph.InstallPlanner.PlanAsync(new InstallPlanningRequest(empty,
        [new RequestedMod(old,InstallReason.Manual,true)],graph.OfflineContentIndex,
        GameVersion.Parse("2026.10.19.5638"),OsPlatform.Windows));
    Check(oldPlan.IsReady,id+" "+oldVersion+" retains old 5638 compatibility");
    string Profile(Instance instance)=>graph.Paths.GetInstanceRoot(instance.InstanceId);
    var folder=Path.Combine(Profile(first),"mods",id);
    var manifest=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(folder,"FILES-SHA256.json")))!;
    foreach(var (file,hash) in manifest)
        Check(Digest(Path.Combine(folder,file))==hash,id+" payload checksum: "+file);
    Check(Digest(Path.Combine(folder,id+".dll"))==Digest(Path.Combine(Profile(second),"mods",id,id+".dll")),id+" installs equal plugin bytes in two isolated instances");
    var setting=Path.Combine(Profile(first),id,"settings.json");
    Directory.CreateDirectory(Path.GetDirectoryName(setting)!);
    File.WriteAllText(setting,"{\"locale\":\"zh-CN\"}\n");
    var before=Digest(setting);
    await Command("disable",id,"--instance",first.Name);
    await Command("enable",id,"--instance",first.Name);
    await Command("update",id,"--instance",first.Name);
    Check(Digest(setting)==before,id+" enable/disable/current-version update preserves profile settings");
    Check(!File.Exists(Path.Combine(Profile(second),id,"settings.json")),id+" settings remain isolated between instances");
    await Command("remove",id,"--instance",first.Name);
    Check(Digest(setting)==before,id+" uninstall preserves profile settings");
    Check(!Directory.Exists(folder),id+" uninstall removes owned mod folder");
    Check(File.Exists(Path.Combine(Profile(second),"mods",id,id+".dll")),id+" uninstall leaves the second instance intact");
}
Check(!Directory.Exists(Path.Combine(root,"unused-shared-profile")),"No shared/personal game profile created");
var report=new {boreaVersion="0.5.2",sourceCommit="4642805bad442a2b965be5cbfe3a973907db8c1b",verifiedAtUtc=DateTime.UtcNow,
    transport="Live public index and published archives via default HTTP handler",gameVersion="2026.10.25.5707",
    scope="Upstream CLI/components: search, two instances per mod, payload checksums, compatibility, enable/disable, no-op latest update and uninstall preserving settings. No desktop UI or gameplay acceptance.",checks,records};
File.WriteAllText(Path.Combine(project,"reports/5707-public-catalog-validation.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true})+"\n");
Console.WriteLine("PASS: "+checks.Count+" public-catalog checks");

sealed class NoCandidates:IInstallCandidateSource
{
    public IReadOnlyList<string> GetGameDirectories()=>[];
    public IReadOnlyList<string> GetLoaderDirectories()=>[];
}
sealed class ReaderSnapshotProvider(IContentIndexReader reader):IContentIndexSnapshotProvider
{
    public Task<ContentIndexSnapshot> GetSnapshotAsync(CancellationToken cancellationToken=default)=>reader.ReadAsync(cancellationToken);
}
