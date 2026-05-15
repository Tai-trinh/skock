using Godot;

namespace Skock.Meta;

public partial class RunEndUi : Control
{
    public override void _Ready()
    {
        var run = RunState.Instance;
        var reason = run.RunEndReason;

        GetNode<Label>("MarginContainer/VBox/TitleLabel").Text = reason switch
        {
            RunEndReason.Defeat  => "DEFEAT",
            RunEndReason.Victory => "VICTORY",
            _ => "",
        };

        GetNode<Label>("MarginContainer/VBox/LoreLabel").Text = reason switch
        {
            RunEndReason.Defeat =>
                "The colony's population has been decimated beyond recovery.\n" +
                "Not enough crew remain to operate the Mothership.\n" +
                "The journey ends here, among the stars.",
            RunEndReason.Victory =>
                "The Mothership emerges from hyperspace to find a fertile, habitable world.\n" +
                "After a long voyage through contested space, the colony makes landfall.\n" +
                "A new home — hard-won.",
            _ => "",
        };

        var jumpsCompleted = reason == RunEndReason.Defeat
            ? run.JumpNumber - 1
            : run.JumpNumber;

        GetNode<Label>("MarginContainer/VBox/StatsLabel").Text =
            $"Jumps completed:  {jumpsCompleted} / 8\n" +
            $"Losses:           {run.LossCount} / 3\n" +
            $"Ships remaining:  {run.Fleet.Ships.Count}\n" +
            $"Salvage:          {run.Salvage}\n" +
            $"Tech:             {run.Tech}";

        GetNode<Button>("MarginContainer/VBox/NewRunButton").Pressed += OnNewRun;
    }

    private void OnNewRun()
    {
        GetTree().ChangeSceneToFile("res://scenes/AdmiralSelect.tscn");
    }
}
