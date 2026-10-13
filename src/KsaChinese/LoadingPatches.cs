// Adapted from LiX-Works/KsaUiLanguages (MIT), b504774158cc03b582a0e75bfe94330cb841f11d.
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using KSA;

namespace KsaChinese;

[HarmonyPatch(typeof(Loading), nameof(Loading.DrawUi))]
public static class LoadingCaptionPatch
{
    public static readonly HashSet<string> Labels = new(StringComparer.Ordinal)
    {
        "Loading", "Asset Loading", "Job Systems", "Renderer", "Processing Gameplay Data", "Processing Assets",
        "Universe", "Spherical Billboarding", "Planet Renderer", "Loading Complete", "Build Viewports",
        "Build Render Targets", "Graphics Queue", "Populate Sounds", "Global Shader Bindings", "Device Vector Shared Memory",
        "Cloud Shadows Renderer", "Mesh render system", "Character render resources", "Star Instances", "Star Binaries",
        "Milky Way", "Substances", "Gauges", "Sun Flare", "Transparencies MSAA", "Gizmos", "Composite Renderer",
        "Compiling Shaders", "This can take 10+ minutes on 5000/6000 series AMD GPUs on Windows.",
        "Planet Transparencies", "Particle Resources", "Volumetric Plume Trails", "Volumetric Exhausts", "Ocean renderer",
        "Sun, Sun Bloom and Lens Flare", "Bloom Renderers", "Line Renderer", "Part Validation", "Raytrace Renderer"
    };
    private static readonly string[] Prefixes = { "Pre-Loading ", "Loading editor tags from ", "Loading " };
    public static string DisplayText(string source)
    {
        if (Labels.Contains(source)) return Runtime.UtilityText(source);
        foreach (string prefix in Prefixes)
            if (source.StartsWith(prefix, StringComparison.Ordinal)) return Runtime.UtilityText(prefix) + source[prefix.Length..];
        // Shader names, mesh IDs, paths and other unknown task names stay exact.
        return source;
    }
    public static string MemoryPrefix(string source) => source == "VRAM: ~" ? Runtime.UtilityText(source) : source;

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Translate(IEnumerable<CodeInstruction> instructions)
    {
        int getters = 0, memory = 0;
        foreach (var instruction in instructions)
        {
            yield return instruction;
            if (instruction.operand is MethodInfo method
                && ((method.DeclaringType == typeof(Loading) && method.Name == "get_StateText")
                    || (method.DeclaringType == typeof(LoadTask) && method.Name == "get_Name")))
            {
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(LoadingCaptionPatch), nameof(DisplayText)));
                getters++;
            }
            else if (instruction.opcode == OpCodes.Ldstr && Equals(instruction.operand, "VRAM: ~"))
            {
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(LoadingCaptionPatch), nameof(MemoryPrefix)));
                memory++;
            }
        }
        if (getters != 2 || memory != 1) throw new InvalidOperationException($"Loading display sites changed: {getters}/{memory}.");
    }
}
