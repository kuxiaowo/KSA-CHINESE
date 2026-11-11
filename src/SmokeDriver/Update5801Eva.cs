using KSA;

namespace KsaChineseSmoke;

public sealed partial class Driver
{
    ReachableStorage _evaStorage;

    void Update5801EvaFrame(double elapsed)
    {
        if (Stage == 0 && elapsed > 2)
        {
            Universe.Rules.LifeSupport = true;
            var vehicle = Program.ControlledVehicle ?? throw new InvalidOperationException("No fixture vehicle.");
            var seat = vehicle.Crew.ToArray().First();
            if (EVADoor.PerformEvaForSeat(vehicle, seat) != EvaRequest.Queued)
                throw new InvalidOperationException("Could not queue fixture EVA.");
            Stage++;
        }
        else if (Stage == 1 && elapsed > 6)
        {
            var kitten = Program.ControlledVehicle as KittenEva
                ?? throw new InvalidOperationException("The queued EVA did not become the controlled vehicle.");
            var cargo = EvaCarry.FindCargo(kitten.Parts)
                ?? throw new InvalidOperationException("No cargo in the 5801 EVA pack.");
            if (EvaCarry.Count(cargo, kitten.Parts.Moles.States).Count != 0)
                throw new InvalidOperationException("Expected a new EVA pack with no cargo.");
            EvaCarryWindow.Toggle(kitten);
            var reachable = new List<ReachableStorage>();
            kitten.FindReachableStorage(reachable);
            Log("5801-eva-reachable-storage=" + reachable.Count);
            _evaStorage = reachable.FirstOrDefault(storage =>
                kitten.CheckCarry(storage.Host, storage.Habitat) == CarryCheck.Ready
                && EvaCarry.Count(storage.Habitat, storage.Host.Parts.Moles.States).Meals > 0);
            Stage++;
        }
        else if (Stage == 2 && elapsed > 9)
        {
            Shot("5801-eva-supplies-empty");
            if (_evaStorage.Host is not null)
                QueueEvaSupply(CarryDirection.Take);
            Stage++;
        }
        else if (Stage == 3 && elapsed > 13)
        {
            if (_evaStorage.Host is not null)
            {
                var kitten = (KittenEva)Program.ControlledVehicle!;
                if (EvaCarry.Count(EvaCarry.FindCargo(kitten.Parts)!, kitten.Parts.Moles.States).Meals != 1)
                    throw new InvalidOperationException("The queued take did not transfer one meal.");
                Log("5801-eva-take-one-meal");
                Shot("5801-eva-supplies-carried");
            }
            Stage++;
        }
        else if (Stage == 4 && elapsed > 17)
        {
            if (_evaStorage.Host is not null)
                QueueEvaSupply(CarryDirection.Stow);
            Stage++;
        }
        else if (Stage == 5 && elapsed > 21)
        {
            if (_evaStorage.Host is not null)
            {
                var kitten = (KittenEva)Program.ControlledVehicle!;
                if (EvaCarry.Count(EvaCarry.FindCargo(kitten.Parts)!, kitten.Parts.Moles.States).Meals != 0)
                    throw new InvalidOperationException("The queued stow did not remove the meal from the pack.");
                Log("5801-eva-stow-one-meal");
            }
            Log("5801-eva-review-completed");
            Program.Instance.RequestExit(0);
            Stage++;
        }
    }

    void QueueEvaSupply(CarryDirection direction)
    {
        InputEvents.KittenCarryBuffer.Add(new InputEvents.KittenCarryData
        {
            Kitten = (KittenEva)Program.ControlledVehicle!,
            Host = _evaStorage.Host,
            Habitat = _evaStorage.Habitat,
            Direction = direction,
            Load = new CarryLoad(1, 0)
        });
    }
}
