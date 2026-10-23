// Local integration runner against the unmodified Borea v0.5.1 source libraries.
// Candidate mode covers runtime checks; --public-catalog checks the live catalogue install path.
using System.Net;
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

var project = Path.GetFullPath(args[0]);
var root = Path.GetFullPath(args[1]);
var game = Path.GetFullPath(args[2]);
var published = args.Contains("--published");
var publicCatalog = args.Contains("--public-catalog");
if (!root.StartsWith(Path.Combine(project, "work") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
    || !root.Contains("test-profile", StringComparison.OrdinalIgnoreCase)
    || Directory.Exists(Path.Combine(root, "Instances")))
    throw new Exception("Use a fresh workspace test-profile root.");
Directory.CreateDirectory(root);
var snapshot = Path.Combine(project, "work/ecosystem-candidate/index.json");
var records = new List<object>();
var checks = new List<string>();
var paths = new GamePathProvider(null, boreaRoot: root);
await new FileBoreaSettingsRepository(paths).SaveAsync(new BoreaSettings(game));

async Task<BoreaServices> Graph()
{
    var reader = new ContentIndexReader(new GamePathProvider(null, boreaRoot: root));
    var fallback = new ContentIndexModRepository(new ReaderSnapshotProvider(reader));
    HttpMessageHandler transport = publicCatalog ? new HttpClientHandler() : new CandidateTransport(project, snapshot, published);
    return await BoreaServices.BuildAsync(root, transport, fallback,
        new NoCandidates(), sharedProfileRoot: Path.Combine(root, "unused-shared-profile"));
}
async Task Command(params string[] command)
{
    using var output = new StringWriter();
    using var error = new StringWriter();
    int code = await BoreaCli.RunAsync(command, async _ => CliServices.From(await Graph()), output, error);
    records.Add(new { command, code, output = Clean(output.ToString()), error = Clean(error.ToString()) });
    Console.WriteLine(string.Join(' ', command) + ": " + code);
    if (code != 0) throw new Exception(error + "\n" + output);
}
void Check(bool ok, string message)
{
    if (!ok) throw new Exception(message);
    checks.Add(message);
}
string Clean(string value) => value.Replace(project, "<project>", StringComparison.OrdinalIgnoreCase);
string Digest(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
await Command("index", "refresh");
await Command("index", "validate", "--json");
await Command("search", "KsaChinese", "--json");
await Command("instance", "create", "Chinese A", "--json");
await Command("instance", "create", "Chinese B", "--json");
await Command("loader", "install", "StarMap", "--version", "0.4.8");
await Command("install", "KsaChinese", "--instance", "Chinese A", "--version", "0.1.6");
await Command("install", "KsaChinese", "--instance", "Chinese B", "--version", "0.1.6");

using var graph = await Graph();
var instances = await graph.Instances.GetAllAsync();
var first = instances.Single(i => i.Name == "Chinese A");
var second = instances.Single(i => i.Name == "Chinese B");
var release = (await graph.OfflineContentIndex.GetReleaseAsync("KsaChinese", ModVersion.Parse("0.1.6")))!;
var cleanPlanTarget = new Instance("Compatibility probe", InstanceSource.Custom.Value);
foreach (var build in new[] { "2026.10.13.5602", "2026.10.19.5639" })
{
    var plan = await graph.InstallPlanner.PlanAsync(new InstallPlanningRequest(cleanPlanTarget,
        [new RequestedMod(release, InstallReason.Manual, true)], graph.OfflineContentIndex,
        GameVersion.Parse(build), OsPlatform.Windows));
    Check(!plan.IsReady, "Borea blocks unsupported game build " + build);
}
foreach (var platform in new[] { OsPlatform.Linux, OsPlatform.MacOs })
{
    var plan = await graph.InstallPlanner.PlanAsync(new InstallPlanningRequest(cleanPlanTarget,
        [new RequestedMod(release, InstallReason.Manual, true)], graph.OfflineContentIndex,
        GameVersion.Parse("2026.10.19.5638"), platform));
    Check(plan.Warnings.Any(warning => warning.Kind == PlanningMessageKind.Platform), "Borea warns about unverified platform " + platform);
}
string Profile(Instance instance) => graph.Paths.GetInstanceRoot(instance.InstanceId);
string Settings(Instance instance) => Path.Combine(Profile(instance), "KsaChinese/settings.json");
string Dll(Instance instance) => Path.Combine(Profile(instance), "mods/KsaChinese/KsaChinese.dll");
Check(Digest(Dll(first)) == Digest(Path.Combine(project, "work/build/KsaChinese/KsaChinese.dll")), "Installed plugin equals the built release payload");
Check(Digest(Dll(first)) == Digest(Dll(second)), "Both instances use the same release bytes");
if (publicCatalog)
{
    Check(!Directory.Exists(Path.Combine(root, "unused-shared-profile")), "Public catalogue installation did not create a shared/personal game profile");
    var publicReport = new { boreaVersion = "0.5.1", sourceCommit = "284d5cf929c8ede99320f8afc451fa44f12aeadd",
        verifiedAtUtc = DateTime.UtcNow, transport = "public index and published archives; default HTTP handler",
        scope = "CLI search and isolated-instance installation through upstream source components; no game launch or desktop GUI acceptance in this run",
        checks, records };
    File.WriteAllText(Path.Combine(project, "reports/ecosystem-public-catalog-validation.json"), JsonSerializer.Serialize(publicReport, new JsonSerializerOptions { WriteIndented = true }) + "\n");
    Console.WriteLine("PASS: " + checks.Count + " public catalogue checks");
    return;
}
var immutable = Digest(Path.Combine(Profile(first), "mods/KsaChinese/language.json"));

foreach (var instance in instances)
{
    var driver = Path.Combine(Profile(instance), "mods/SmokeDriver");
    Directory.CreateDirectory(driver);
    File.Copy(Path.Combine(project, "work/build/SmokeDriver/SmokeDriver.dll"), Path.Combine(driver, "SmokeDriver.dll"));
    File.WriteAllText(Path.Combine(driver, "mod.toml"), "name = \"Workspace ecosystem test driver\"\nassets = []\n[StarMap]\nEntryAssembly = \"SmokeDriver\"\n");
    await graph.ModState.AddEntryAsync(instance.InstanceId, "SmokeDriver", true);
    await graph.ModState.PutGameContentFirstAsync(instance.InstanceId, game);
}
var loader = (await graph.OfflineContentIndex.GetListingAsync("StarMap"))!;
async Task Launch(Instance instance, string mode)
{
    string oldLog = Path.Combine(Profile(instance), "smoke.log");
    long start = File.Exists(oldLog) ? new FileInfo(oldLog).Length : 0;
    Environment.SetEnvironmentVariable("KSA_SMOKE_ECOSYSTEM", mode);
    var launch = graph.Launcher.Launch((await graph.Instances.GetByIdAsync(instance.InstanceId))!, loader);
    using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
    launch = await graph.Launcher.WatchStartAsync(instance, launch, timeout.Token);
    if (!launch.Started) throw new Exception(launch.Message + "\n" + string.Join('\n', launch.Output));
    var exit = await graph.Launcher.WatchExitAsync(instance, launch, timeout.Token);
    Environment.SetEnvironmentVariable("KSA_SMOKE_ECOSYSTEM", null);
    if (exit.ExitCode != 0) throw new Exception("Borea runtime exit " + exit.ExitCode + "\n" + string.Join('\n', exit.Output));
    Check(true, "Actual Borea launch exited zero: " + instance.Name + " / " + mode);
    using var logFile = new FileStream(oldLog, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
    logFile.Seek(start, SeekOrigin.Begin);
    using var logReader = new StreamReader(logFile);
    string log = logReader.ReadToEnd();
    Check(log.Contains("ecosystem-completed=" + mode), "Runtime driver completed: " + mode);
    if (mode != "disabled")
        Check(log.Contains(Path.GetFullPath(Path.Combine(Profile(instance), "mods/KsaChinese/KsaChinese.dll")), StringComparison.OrdinalIgnoreCase), "Game loaded plugin from chosen Borea instance: " + instance.Name);
    records.Add(new { runtime = mode, instance = instance.Name, exitCode = exit.ExitCode,
        output = Clean(string.Join('\n', exit.Output)) });
    Console.WriteLine("Runtime " + instance.Name + " / " + mode + ": " + exit.ExitCode);
}
await Launch(first, "zh-to-en");
Check(File.ReadAllText(Settings(first)).Contains("en-US"), "First instance saved English outside mod files");
Check(!File.Exists(Settings(second)), "First instance did not create preferences in second instance");
await Launch(second, "verify-zh");
Check(immutable == Digest(Path.Combine(Profile(second), "mods/KsaChinese/language.json")), "Shared mod defaults were not mutated");
await Launch(first, "en-to-zh");
Check(File.ReadAllText(Settings(first)).Contains("zh-CN"), "Restart restored English and saved Chinese again");
var saves = Directory.EnumerateFiles(Path.Combine(Profile(first), "saves"), "*", SearchOption.AllDirectories).ToDictionary(p => p, Digest);
Check(saves.Count > 0, "Test save exists after save/load roundtrip");
await Command("disable", "KsaChinese", "--instance", "Chinese A");
await Launch(first, "disabled");
await Command("enable", "KsaChinese", "--instance", "Chinese A");
var current = (await graph.Instances.GetByIdAsync(first.InstanceId))!.Mods.Single(m => m.ModId == "KsaChinese");
var replacement = await graph.Replacer.ReplaceAsync(first.InstanceId, current, current.Metadata);
Check(File.ReadAllText(Settings(first)).Contains("zh-CN"), "Borea replacement preserves per-instance preferences");
await Command("update", "KsaChinese", "--instance", "Chinese A", "--dry-run");
await Launch(first, "verify-zh");
await Command("remove", "KsaChinese", "--instance", "Chinese A");
Check(!Directory.Exists(Path.Combine(Profile(first), "mods/KsaChinese")), "Borea removed owned mod directory");
Check(File.Exists(Settings(first)), "Uninstall preserves profile preference");
Check(saves.All(pair => File.Exists(pair.Key) && Digest(pair.Key) == pair.Value), "Disable, replacement and uninstall preserved test save bytes");
Check(File.Exists(Dll(second)), "Removing first instance mod preserves second instance");
await Launch(first, "disabled");
Check(!Directory.Exists(Path.Combine(root, "unused-shared-profile")), "No shared/personal game profile was created");
var report = new { boreaVersion = "0.5.1", sourceCommit = "284d5cf929c8ede99320f8afc451fa44f12aeadd",
    transport = published ? "published archives; local candidate index" : "local archives and candidate index",
    publicIndexSubmitted = false, forumPublished = false, checks, records };
File.WriteAllText(Path.Combine(project, "reports/ecosystem-validation.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + "\n");
Console.WriteLine("PASS: " + checks.Count + " ecosystem checks");

sealed class NoCandidates : IInstallCandidateSource
{
    public IReadOnlyList<string> GetGameDirectories() => [];
    public IReadOnlyList<string> GetLoaderDirectories() => [];
}
sealed class ReaderSnapshotProvider(IContentIndexReader reader) : IContentIndexSnapshotProvider
{
    public Task<ContentIndexSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
        => reader.ReadAsync(cancellationToken);
}
sealed class CandidateTransport(string project, string snapshot, bool published) : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var url = request.RequestUri!.AbsoluteUri;
        string? file = url switch
        {
            "https://ksamodding.github.io/content-index-releases/v1/index.json" => snapshot,
            "https://github.com/kuxiaowo/KSA-CHINESE/releases/download/v0.1.6/KsaChinese-0.1.6.zip" when !published => Path.Combine(project, "dist/KsaChinese-0.1.6.zip"),
            "https://github.com/StarMapLoader/StarMap/releases/download/0.4.8/StarMap-0.4.8.zip" when !published => Path.Combine(project, ".tools/StarMap-0.4.8.zip"),
            _ => null
        };
        if (file is not null)
        {
            var content = new ByteArrayContent(await File.ReadAllBytesAsync(file, ct));
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.EndsWith(".json") ? "application/json" : "application/zip");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        }
        if (published && request.Method == HttpMethod.Get && url.StartsWith("https://github.com/", StringComparison.Ordinal))
        {
            using var client = new HttpClient();
            return await client.GetAsync(url, ct);
        }
        throw new HttpRequestException("Unexpected integration-test request: " + request.Method + " " + url);
    }
}
