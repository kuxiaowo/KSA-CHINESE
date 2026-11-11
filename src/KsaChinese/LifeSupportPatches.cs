using System.Reflection;
using HarmonyLib;
using KSA;

namespace KsaChinese;

[HarmonyPatch]
public static class LifeSupportLabelPatch
{
    public static string StorageCaption(Habitat habitat) => Runtime.PartCaption(habitat.FullPart.StructuralPart);

    public static MethodBase TargetMethod() => AccessTools.Method(typeof(LifeSupport), "DrawSecondsLeft");

    [HarmonyPrefix, HarmonyPriority(Priority.Last)]
    public static void Localize(ref ReadOnlySpan<byte> label) => label = ExpandedUi.Utf8Plain(label);
}

[HarmonyPatch]
public static class LifeSupportControlPatch
{
    public static IEnumerable<MethodBase> TargetMethods()
    {
        var habitatWindow = AccessTools.Inner(typeof(HabitatZonesWindow), "Window");
        yield return AccessTools.Method(habitatWindow, "DrawAmount");
        yield return AccessTools.Method(habitatWindow, "DrawToggle");
        yield return AccessTools.Method(AccessTools.Inner(typeof(EvaCarryWindow), "Window"), "DrawAmount");
    }

    [HarmonyPrefix, HarmonyPriority(Priority.Last)]
    public static void Localize(ref ReadOnlySpan<byte> label) => label = ExpandedUi.Utf8Menu(label);
}
