using System.Reflection;
using HarmonyLib;
using KSA;

namespace KsaChinese;

public static class LifeSupportLabelPatch
{
    // 5901 inlines the shortfall formatter in some UI callers. Replace those
    // display calls together so the title, shortfall and duration stay in sync.
    public static void AppendShortfall(in ZoneMission mission, ref Core.SpanBuilder text)
    {
        text.Append(ExpandedUi.Text(mission.FirstName).AsSpan());
        text.Append(ExpandedUi.Text(" short by ").AsSpan());
        LifeSupportForecast.AppendSpan(ref text, mission.ShortBy);
    }

    public static string StorageCaption(Habitat habitat) => Runtime.PartCaption(habitat.FullPart.StructuralPart);
}

[HarmonyPatch]
public static class LifeSupportControlPatch
{
    public static IEnumerable<MethodBase> TargetMethods()
    {
        var habitatWindow = AccessTools.Inner(typeof(LifeSupportWindow), "Window");
        yield return AccessTools.Method(habitatWindow, "DrawAmount");
        yield return AccessTools.Method(habitatWindow, "DrawToggle");
        yield return AccessTools.Method(AccessTools.Inner(typeof(EvaCarryWindow), "Window"), "DrawAmount");
    }

    [HarmonyPrefix, HarmonyPriority(Priority.Last)]
    public static void Localize(ref ReadOnlySpan<byte> label) => label = ExpandedUi.Utf8Menu(label);
}
