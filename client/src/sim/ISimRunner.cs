namespace Skock.Sim;

public interface ISimRunner
{
    (BattleLog Log, BattleResult Result) Run(ulong seed, string fleetAPath, string fleetBPath);
}
