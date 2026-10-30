using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace KsaChinese;

// 5707 display-only fragments. No IDs, numeric formatting or physics values.
public static class Update5707Text
{
    public static readonly HashSet<string> Fragments = new(StringComparer.Ordinal)
    {
        "HDR (Experimental)",
        "HDR Format",
        "HDR Paper White",
        "HDR Peak Brightness",
        "Presents in HDR on this screen. The scene always uses the GT7 tonemapper in HDR, the Tonemapper setting only applies to SDR.",
        "Brightness of white UI and of an exposed scene value of 1. Match it to the system's SDR content brightness so the game sits alongside other windows.",
        "Brightest the display can show. Highlights roll off towards this, so set it to the display's rated peak.",
        "How HDR frames are sent to the screen. Auto uses scRGB where the screen offers it, otherwise HDR10. A format the screen doesn't offer falls back to the other.",
        "How HDR frames are sent to the screen. Auto uses scRGB where the screen offers it, otherwise HDR10. A format the screen doesn't offer falls back to the other. This screen offers scRGB and HDR10.",
        "How HDR frames are sent to the screen. Auto uses scRGB where the screen offers it, otherwise HDR10. A format the screen doesn't offer falls back to the other. This screen offers scRGB only.",
        "How HDR frames are sent to the screen. Auto uses scRGB where the screen offers it, otherwise HDR10. A format the screen doesn't offer falls back to the other. This screen offers HDR10 only.",
        "Double-click an assigned vehicle to follow it. Click a column header to sort the roster. Recovery is how long a kitten needs resting at home for its acute dose to heal.",
        "Acute Dose",
        "Lifetime Dose",
        "Recovery",
        "RETIRED",
        " RETIRED",
        "Acute ",
        "Acute dose ",
        "\nReceiving ",
        "\nHealing ",
        "\nNet ",
        "\nRadiation sickness in ",
        "\nLethal in ",
        "Lifetime ",
        "Lifetime dose ",
        "\nRising ",
        "\nNever heals",
        "Radiation sickness\nFit to fly in ",
        "Pressure:",
        "Rated Pressure:",
        "Radiation Shield",
        "Shield Mass",
        "Tank Cover",
        "Unshielded",
        "Solar Wind",
        "Distance to Star:",
        "Orbit Dose: ",
        "Radiation dose rate here, unshielded, in millisieverts per hour.",
        "Radiation dose rate here, unshielded, in sieverts per hour.",
        "Radiation dose rate averaged over the current orbit, unshielded, in millisieverts per hour.",
        "Radiation dose rate averaged over the current orbit, unshielded, in sieverts per hour.",
        "Radiation dose rate averaged over the orbit after this burn, unshielded, in millisieverts per hour.",
        "Radiation dose rate averaged over the orbit after this burn, unshielded, in sieverts per hour.",
        "Show Magnetosphere",
        "Show Space Weather",
        "Space Weather",
        "SPACE WEATHER",
        "Draw each star's active regions, the magnetic field line from the star to what you are viewing, and CMEs in flight. A CME's front is drawn where its forecast puts it, between its earliest and latest arrival.",
        "Compute Tessellation",
        "Terrain Pre-Pass",
        "Space weather at every body with vehicles in its sphere of influence, and at the star and body picked above. A flare's light is the first sign of it. CME arrival forecasts start about half a day out either way and close in as the CME flies. Nothing tells which way a CME's field points until it arrives.",
        "Bodies with vehicles",
        "No vehicles in flight.",
        "Body",
        "No eruptions.",
        "No fast CMEs in the last 3 days.",
        "No CMEs on the way.",
        "Activity: ",
        ", rising, maximum in ",
        ", falling, minimum in ",
        "Wind at 1 AU: ",
        "CMEs: ",
        " a day",
        "Active regions: ",
        "CME at ",
        " ago",
        "Solar wind: ",
        "Geomagnetic storm: ",
        "Radiation storm: ",
        "CME from ",
        ", arriving in ",
        "CME launched from ",
        "Major flare on ",
        ". Radiation storm likely near ",
        " CME heading for ",
        " radiation storm near ",
        "CME shock reached ",
        ", field ",
        " nT south",
        " nT north",
        " died of radiation sickness!",
        " has radiation sickness!",
        " has reached their lifetime radiation limit and will retire after this flight.",
        "HELIOSPHERE",
        "HELIOPAUSE",
        "WIND SPEED",
        "WIND PRESSURE AT 1 AU",
        "QUIET DOSE AT 1 AU",
        "ROTATION PERIOD",
        "POLE TILT",
        "ACTIVITY CYCLE",
        "CYCLE PHASE",
        "Rising",
        "Falling",
        "TIME TO MAXIMUM",
        "TIME TO MINIMUM",
        "ACTIVITY",
        "COSMIC RAYS",
        " per day",
        "CME RATE",
    };
    public static string Text(string source) => Runtime.CurrentPack.Display.GetValueOrDefault(source, source);
    static readonly string[] Templates =
    {
        "Radiation dose that heals over time, with a half-life of {0} days in flight and {1} days resting at home.\nAt {2} Sv a kitten has radiation sickness and is grounded until it heals below {3} Sv.\nAt {4} Sv it dies.",
        "Every radiation dose a kitten has ever received. It never heals.\nAt {0} Sv a kitten finishes its current flight and then retires.",
        "How long a kitten needs resting at home for its acute dose to heal away.\nA kitten with radiation sickness can fly again sooner, once its acute dose is below {0} Sv.",
    };
    static readonly Regex[] Patterns = Templates.Select(template => new Regex("\\A" +
        Regex.Replace(Regex.Escape(template), @"\\\{\d+}", "([0-9]+(?:[.,][0-9]+)?)") + "\\z",
        RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50))).ToArray();
    public static string Tooltip(string source)
    {
        for (int i = 0; i < Templates.Length; i++)
        {
            if (!Runtime.CurrentPack.Display.TryGetValue(Templates[i], out var translated)) continue;
            var match = Patterns[i].Match(source);
            if (!match.Success) continue;
            return string.Format(System.Globalization.CultureInfo.InvariantCulture, translated,
                match.Groups.Cast<Group>().Skip(1).Select(g => (object)g.Value).ToArray());
        }
        return source;
    }
}

[HarmonyPatch]
public static class Update5707FragmentPatch
{
    public static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var (type, method) in new[]
        {
            ("KSA.CrewPortraitPanel", "DrawAcuteDose"),
            ("KSA.CrewPortraitPanel", "DrawLifetimeDose"),
            ("KSA.KittenComboOption", "DrawTooltip"),
            ("KSA.SpaceWeatherAlerts", "RaiseAlerts"),
            ("KSA.SpaceWeatherAlerts", "AppendSolarWind"),
            ("KSA.Vehicle", "CreditCrewStats"),
            ("KSA.Vehicle", "KillLethallyDosedCrew"),
            ("KSA.OrbitHoverReadout", "DrawVehicleReadout"),
        })
        {
            var matches = typeof(KSA.Program).Assembly.GetType(type, throwOnError: true)!
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
                .Where(m => m.Name == method && PatchProcessor.GetOriginalInstructions(m)
                    .Any(i => i.opcode == OpCodes.Ldstr && i.operand is string s && Update5707Text.Fragments.Contains(s))).ToArray();
            if (matches.Length != 1) throw new InvalidOperationException($"5707 display fragment target changed: {type}.{method}, {matches.Length} matches.");
            yield return matches[0];
        }
    }
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Translate(IEnumerable<CodeInstruction> instructions)
    {
        foreach (var instruction in instructions)
        {
            yield return instruction;
            if (instruction.opcode == OpCodes.Ldstr && instruction.operand is string text && Update5707Text.Fragments.Contains(text))
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Update5707Text), nameof(Update5707Text.Text)));
        }
    }
}
