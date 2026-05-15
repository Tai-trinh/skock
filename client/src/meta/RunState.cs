using System.IO;
using Godot;
using Skock.Sim;

namespace Skock.Meta;

// Autoload singleton — owns all in-run state and controls scene transitions.
// BattleRenderer reads inputs from here and reports results here.
// Full run logic (jump counter, loss count, admiral, resources) added in meta-layer step.
public partial class RunState : Node
{
    public static RunState Instance { get; private set; } = null!;

    public string ProjectDir { get; private set; } = "";
    public string SimBinaryPath { get; private set; } = "";
    public string PlayerFleetPath { get; private set; } = "";
    public string FallbackFleetPath { get; private set; } = "";

    public override void _Ready()
    {
        Instance = this;
        ProjectDir = ProjectSettings.GlobalizePath("res://");
        SimBinaryPath = Path.GetFullPath(Path.Combine(ProjectDir, "..", "target", "release", "skock-sim.exe"));
        PlayerFleetPath = Path.GetFullPath(Path.Combine(ProjectDir, "..", "player_fleet.json"));
        FallbackFleetPath = Path.GetFullPath(Path.Combine(ProjectDir, "..", "sim", "test_data", "fleet_a.json"));
    }

    public void RecordBattleResult(BattleResult result)
    {
        // TODO: update jump counter, loss count, earn salvage/tech, check run-end conditions.
        // For now, always return to the fleet builder.
        GetTree().ChangeSceneToFile("res://scenes/FleetBuilder.tscn");
    }
}
