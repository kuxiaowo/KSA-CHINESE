using System.Reflection.Emit;
using Brutal.ImGuiApi;
using HarmonyLib;
using KSA;

namespace KsaChineseSmoke;

public sealed partial class Driver
{
    const string CrossSystemSave = "5949 Cross System Fixture";
    static bool showVisitFixture;

    static bool visitFixtureOpened;

    public static bool FixtureTab(ImString label, ImGuiTabItemFlags flags)
    {
        string text = label.ToString();
        bool visits = showVisitFixture && (text == "Visits" || text.EndsWith("###Visits", StringComparison.Ordinal));
        bool open = ImGui.BeginTabItem(label, visits ? flags | ImGuiTabItemFlags.SetSelected : flags);
        if (visits && open && !visitFixtureOpened)
        {
            visitFixtureOpened = true;
            Log("5949-visits-tab-open");
        }
        return open;
    }

    public static IEnumerable<CodeInstruction> FixtureTabRenderer(IEnumerable<CodeInstruction> instructions)
    {
        var tab = AccessTools.Method(typeof(ImGui), nameof(ImGui.BeginTabItem),
            new[] { typeof(ImString), typeof(ImGuiTabItemFlags) });
        var fixture = AccessTools.Method(typeof(Driver), nameof(FixtureTab));
        return instructions.MethodReplacer(tab, fixture);
    }

    void Update5949Frame(string mode, double elapsed)
    {
        if (elapsed < 2 + Stage * 4) return;
        string system = Universe.CurrentSystem!.Id;
        if (mode == "seed")
        {
            if (system != "Sol") throw new InvalidOperationException("Fixture expected Sol.");
            GameSaves.MakeUncompressedSave(CrossSystemSave);
            if (GameSaves.Find(CrossSystemSave) is null) throw new InvalidOperationException("Fixture save was not registered.");
            Log("5949-seed-completed; system=" + system);
            Program.Instance.RequestExit(0);
            Stage++;
            return;
        }
        if (mode == "restart")
        {
            if (Stage > 0)
            {
                if (system == "Sol") Program.Instance.RequestExit(0);
                return;
            }
            string restartMarker = Path.Combine(KSA.Constants.DocumentsFolderPath, "5949-restart-requested.txt");
            if (system == "Sol")
            {
                if (!File.Exists(restartMarker) || Program.ControlledVehicle?.Id != "Rocket")
                    throw new InvalidOperationException("The requested restart did not restore the fixture.");
                Log("5949-restart-completed; system=" + system + "; vehicle=" + Program.ControlledVehicle?.Id);
                Shot("5949-restart-alert");
            }
            else
            {
                if (system != "SolLite" || SaveSystemSwap.PlanLoad("Sol") != SaveLoadPlan.Swap)
                    throw new InvalidOperationException("Fixture did not start in the source system.");
                // Exercise the estimate-driven branch without allocating VRAM.
                File.WriteAllText(restartMarker, CrossSystemSave);
                SystemVramCosts.Record("Sol", 1L << 40);
                Log("5949-restart-requested; system=" + system);
                GameSaves.LoadSaveGame(CrossSystemSave);
            }
            Stage++;
            return;
        }
        switch (Stage)
        {
            case 0:
                if (system != "SolLite" || SaveSystemSwap.PlanLoad("Sol") != SaveLoadPlan.Swap
                    || SaveSystemSwap.PlanLoad("Fixture-Missing") != SaveLoadPlan.NotInstalled
                    || GameSaves.Find(CrossSystemSave) is null)
                    throw new InvalidOperationException("Cross-system fixture is missing or inconsistent.");
                Locale("zh-CN");
                ContentSnapshot();
                GameSaves.Toggle();
                Log("5949-save-list; system=" + system);
                break;
            case 1:
                Shot("5949-save-list");
                break;
            case 2:
                GameSaves.Close();
                DestinationRegistryWindow.ShowWindow = true;
                if (DestinationRegistry.All.Count == 0) throw new InvalidOperationException("No registered destinations.");
                Log($"5949-destinations={DestinationRegistry.All.Count}; visits={Universe.CurrentSystem.Arrivals.Visits.Destinations.Count}");
                break;
            case 3:
                Shot("5949-destinations");
                break;
            case 4:
                showVisitFixture = true;
                break;
            case 5:
                if (!visitFixtureOpened) throw new InvalidOperationException("Visits tab did not open.");
                Shot("5949-visits");
                Log("5949-kitten-firsts=" + Universe.KittenRoster.Kittens.Sum(kitten => kitten.FirstVisits.Count));
                break;
            case 6:
                if (AppDomain.CurrentDomain.GetAssemblies().Any(assembly => assembly.GetName().Name == "KsaBabel"))
                {
                    foreach (string locale in new[] { "en-US", "es-ES", "fr-FR", "it-IT", "ja-JP", "pt-BR", "ru-RU", "de-DE" })
                    {
                        Locale(locale);
                        string warning = LStrings.AlertRestartedForVram.Format(CrossSystemSave);
                        if (!warning.Contains(CrossSystemSave, StringComparison.Ordinal))
                            throw new InvalidOperationException("Missing save name after locale switch: " + locale);
                        Log($"5949-locale={locale}; restart-caption={warning}");
                    }
                }
                break;
            case 7:
                Shot("5949-destinations-locale");
                break;
            case 8:
                Locale("zh-CN");
                showVisitFixture = false;
                DestinationRegistryWindow.ShowWindow = false;
                Program.EditorFlag = true;
                break;
            case 9:
                if (!Program.IsEditorOpen) throw new InvalidOperationException("Editor did not open.");
                if (!VehicleEditor.ShowPartWindow) VehicleEditor.TogglePartWindow();
                var partWindow = AccessTools.Field(typeof(VehicleEditor), "_partWindow").GetValue(null)!;
                AccessTools.Field(partWindow.GetType(), "_selectedTag").SetValue(partWindow, new EditorTag("Thermal"));
                Log("5949-thermal-category-open");
                break;
            case 10:
                // The game screenshot command is unavailable in the editor.
                // Leave the panels visible for a separate window capture.
                if (mode == "visual" && elapsed < 54) return;
                Program.EditorFlag = false;
                break;
            case 11:
                if (Program.IsEditorOpen) throw new InvalidOperationException("Editor did not close.");
                GameSaves.LoadSaveGame(CrossSystemSave);
                Log("5949-cross-system-requested; from=" + system);
                break;
            case 12:
                if (system != "Sol")
                {
                    if (elapsed > 90) throw new InvalidOperationException("Cross-system load did not finish.");
                    return;
                }
                Shot("5949-after-cross-system");
                break;
            case 13:
                Log("5949-review-completed; system=" + system + "; vehicle=" + Program.ControlledVehicle?.Id);
                Program.Instance.RequestExit(0);
                break;
        }
        Stage++;
    }
}
