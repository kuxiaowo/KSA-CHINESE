using HarmonyLib;
using KSA;

namespace KsaChineseSmoke;

public sealed partial class Driver
{
    bool _radiation;
    bool _lifeSupport;

    void Update5801Frame(double elapsed)
    {
        if (Stage == 0 && elapsed > 2)
        {
            _radiation = Universe.Rules.Radiation;
            _lifeSupport = Universe.Rules.LifeSupport;
            Universe.Rules.LifeSupport = true;
            ContentSnapshot();
            HabitatZonesWindow.Toggle();
            Stage++;
        }
        else if (Stage == 1 && elapsed > 6)
        {
            Shot("5801-habitat-zh");
            Stage++;
        }
        else if (Stage == 2 && elapsed > 9)
        {
            Locale("en-US");
            Stage++;
        }
        else if (Stage == 3 && elapsed > 12)
        {
            Shot("5801-habitat-en");
            Stage++;
        }
        else if (Stage == 4 && elapsed > 15)
        {
            Locale("zh-CN");
            HabitatZonesWindow.Toggle();
            KittenRosterWindow.Toggle();
            Stage++;
        }
        else if (Stage == 5 && elapsed > 19)
        {
            Shot("5801-roster-zh");
            Stage++;
        }
        else if (Stage == 6 && elapsed > 22)
        {
            KittenRosterWindow.Toggle();
            Tab("Display");
            Stage++;
        }
        else if (Stage == 7 && elapsed > 26)
        {
            Shot("5801-display-zh");
            Stage++;
        }
        else if (Stage == 8 && elapsed > 29)
        {
            GameSettings.Show = false;
            History(true);
            Stage++;
        }
        else if (Stage == 9 && elapsed > 33)
        {
            Shot("5801-history-zh");
            Stage++;
        }
        else if (Stage == 10 && elapsed > 36)
        {
            History(false);
            Universe.Rules.Radiation = false;
            Universe.Rules.LifeSupport = false;
            GameSaves.MakeUncompressedSave("ksa_5801_localization_test");
            Log("5801-save-rules-off");
            Stage++;
        }
        else if (Stage == 11 && elapsed > 44)
        {
            Universe.Rules.Radiation = true;
            Universe.Rules.LifeSupport = true;
            GameSaves.LoadSaveGame("ksa_5801_localization_test");
            Stage++;
        }
        else if (Stage == 12 && elapsed > 54)
        {
            if (Universe.Rules.LifeSupport || Universe.Rules.Radiation)
                throw new InvalidOperationException("Saved rules did not survive reload.");
            Log("5801-reloaded-rules-off");
            Universe.Rules.Radiation = _radiation;
            Universe.Rules.LifeSupport = _lifeSupport;
            Program.EditorFlag = true;
            Stage++;
        }
        else if (Stage == 13 && elapsed > 57 && Program.Editor is { } editor)
        {
            Program.ShowUndoHistoryDebug = true;
            var candidate = editor.EditingSpace.AllParts.ToArray().FirstOrDefault(p => p.Template.HasVariants);
            if (candidate is not null)
            {
                AccessTools.Method(typeof(VehicleEditor), "RequestVariantSwap")
                    .Invoke(editor, new object[] { candidate, 1 });
                Log("5801-variant-request=" + candidate.Template.Id);
            }
            Stage++;
        }
        else if (Stage == 14 && elapsed > 63)
        {
            Shot("5801-editor-zh");
            var activeEditor = Program.Editor!;
            if (activeEditor.CanUndo)
            {
                bool undone = activeEditor.Undo();
                bool redone = activeEditor.Redo();
                Log($"5801-undo={undone};redo={redone}");
            }
            Stage++;
        }
        else if (Stage == 15 && elapsed > 69)
        {
            Log("5801-review-completed");
            Program.Instance.RequestExit(0);
            Stage++;
        }
    }
}
