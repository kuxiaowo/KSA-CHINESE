using System.Security.Cryptography;
using System.Text.Json;

namespace KsaChinese;

public static class Compatibility
{
    public static void Validate(string gameDirectory,string manifest)
    {
        using var document=JsonDocument.Parse(File.ReadAllText(manifest));
        var files=document.RootElement.GetProperty("files");
        foreach(var file in files.EnumerateObject())
        {
            var path=Path.GetFullPath(Path.Combine(gameDirectory,file.Name));
            if(!path.StartsWith(Path.GetFullPath(gameDirectory).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Compatibility manifest path escapes game directory.");
            var hash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
            if(hash!=file.Value.GetString()) throw new InvalidDataException("Uninspected game/dependency fingerprint: "+file.Name);
        }
    }
}
