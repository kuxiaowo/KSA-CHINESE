using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace KsaChineseLauncher;

internal static class Program
{
    static readonly string Root = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
    static readonly string LogRoot = Path.Combine(Root, "logs");
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int MessageBoxW(IntPtr owner, string text, string caption, uint flags);

    static async Task<int> Main(string[] args)
    {
        try
        {
            // Probes exercise exactly the same guard without starting KSA.
            if (args.Length == 2 && args[0] == "--check")
            {
                ValidateGame(Path.GetFullPath(args[1]), Path.Combine(Root, "compatibility.json"));
                return 0;
            }
            string game = Directory.GetParent(Root)?.FullName ?? throw new IOException("请把 KsaChinese 文件夹放在游戏目录内。");
            ValidateGame(game, Path.Combine(game, "Content", "KsaChinese", "compatibility.json"));
            string profile = Path.Combine(Root, "Profile");
            string settings = Path.Combine(Root, "launcher-settings.json");
            if (File.Exists(settings))
            {
                using var config = JsonDocument.Parse(File.ReadAllText(settings));
                if (config.RootElement.TryGetProperty("profile", out var value))
                {
                    string configured = value.GetString() ?? throw new IOException("profile 配置无效。");
                    if (string.IsNullOrWhiteSpace(configured)) throw new IOException("profile 不能为空。");
                    profile = Path.GetFullPath(configured, Root);
                }
            }
            Directory.CreateDirectory(profile);
            string manifest = Path.Combine(profile, "manifest.toml");
            if (!File.Exists(manifest))
                File.WriteAllText(manifest, "[[mods]]\nid = \"Core\"\nenabled = true\n\n[[mods]]\nid = \"KsaChinese\"\nenabled = true\n", new UTF8Encoding(false));
            string loader = Path.Combine(Root, "Loader");
            string executable = Path.Combine(loader, "StarMap.exe");
            if (!File.Exists(executable)) throw new FileNotFoundException("缺少随包 StarMap 加载器，请完整解压。", executable);
            File.WriteAllText(Path.Combine(loader, "StarMapConfig.json"), JsonSerializer.Serialize(new { GameLocation = Path.Combine(game,"KSA.dll"), RepositoryLocation = "", GameArguments = args }), new UTF8Encoding(false));
            Directory.CreateDirectory(LogRoot);
            string tag = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
            var start = new ProcessStartInfo(executable)
            {
                WorkingDirectory = loader, UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
            };
            // Survives a loader restart; does not alter the machine's environment.
            start.Environment["STARMAP_INSTANCE_PATH"] = profile;
            using var child = Process.Start(start) ?? throw new IOException("未能创建游戏进程。");
            await Task.WhenAll(CopyLog(child.StandardOutput, Path.Combine(LogRoot,tag+".stdout.log")), CopyLog(child.StandardError,Path.Combine(LogRoot,tag+".stderr.log")), child.WaitForExitAsync());
            if (child.ExitCode != 0) throw new IOException($"游戏退出码：{child.ExitCode}。日志位于：{LogRoot}");
            return 0;
        }
        catch (Exception error)
        {
            Directory.CreateDirectory(LogRoot);
            File.WriteAllText(Path.Combine(LogRoot, "last-launch-error.txt"),error.ToString(),new UTF8Encoding(false));
            // Automated failure probes suppress the dialog, regular launches never do.
            if (!(args.Length == 2 && args[0] == "--check"))
                MessageBoxW(IntPtr.Zero,error.Message+"\n\n日志："+LogRoot,"KSA 汉化启动失败",0x10);
            return 1;
        }
    }

    static async Task CopyLog(StreamReader input, string path)
    {
        await using var output = new StreamWriter(path,false,new UTF8Encoding(false)) { AutoFlush = true };
        char[] buffer = new char[4096];
        int length;
        while ((length = await input.ReadAsync(buffer)) > 0) await output.WriteAsync(buffer.AsMemory(0,length));
    }

    internal static void ValidateGame(string game, string compatibility)
    {
        using var expected = JsonDocument.Parse(File.ReadAllText(compatibility));
        foreach (var entry in expected.RootElement.GetProperty("files").EnumerateObject())
        {
            string path = Path.GetFullPath(entry.Name,game);
            if (!path.StartsWith(game.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
                throw new IOException("适配清单中的路径无效。");
            if (!File.Exists(path)) throw new IOException("缺少游戏文件："+entry.Name);
            using var stream = File.OpenRead(path);
            if (!Convert.ToHexString(SHA256.HashData(stream)).Equals(entry.Value.GetString(),StringComparison.OrdinalIgnoreCase))
                throw new IOException("游戏已更新或文件发生变化，汉化安全停止。请等待适配包；游戏文件与存档没有被修改。变化文件："+entry.Name);
        }
    }
}
