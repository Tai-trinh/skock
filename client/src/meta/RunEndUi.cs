using System.Collections.Generic;
using System.Linq;
using Godot;
using Skock.UI;

namespace Skock.Meta;

public partial class RunEndUi : Control
{
    public override void _Ready()
    {
        var run = RunState.Instance;
        var reason = run.RunEndReason;

        GetNode<Label>("MarginContainer/VBox/TitleLabel").Text = reason switch
        {
            RunEndReason.Defeat => "DEFEAT",
            RunEndReason.Victory => "VICTORY",
            _ => "",
        };

        GetNode<Label>("MarginContainer/VBox/LoreLabel").Text = reason switch
        {
            RunEndReason.Defeat => "The colony's population has been decimated beyond recovery.\n"
                + "Not enough crew remain to operate the Mothership.\n"
                + "The journey ends here, among the stars.",
            RunEndReason.Victory =>
                "The Mothership emerges from hyperspace to find a fertile, habitable world.\n"
                    + "After a long voyage through contested space, the colony makes landfall.\n"
                    + "A new home — hard-won.",
            _ => "",
        };

        var history = run.Stats.GetJumpHistory();
        var jumpsCompleted = reason == RunEndReason.Defeat ? run.JumpNumber - 1 : run.JumpNumber;
        var totalKills = history.Sum(j => j.EnemiesKilledByHullClass.Values.Sum());
        var totalLost = history.Sum(j => j.OwnShipsLostByHullClass.Values.Sum());
        var totalDealt = history.Sum(j => j.DamageDealt);
        var totalTaken = history.Sum(j => j.DamageTaken);

        // Per-run condition achievement hint.
        var untouchable = totalTaken < 1000f;

        GetNode<Label>("MarginContainer/VBox/StatsLabel").Text =
            $"Jumps completed:  {jumpsCompleted} / 8\n"
            + $"Losses:           {run.LossCount} / 3\n"
            + $"Ships remaining:  {run.Fleet.Ships.Count}\n"
            + $"Salvage:          {run.Salvage}\n"
            + $"Tech:             {run.Tech}\n"
            + $"Enemies killed:   {totalKills}\n"
            + $"Own ships lost:   {totalLost}\n"
            + $"Damage dealt:     {totalDealt:0}\n"
            + $"Damage taken:     {totalTaken:0}"
            + (untouchable ? "\n★ Untouchable  (< 1000 HP taken)" : "");

        BuildJumpTable(
            GetNode<VBoxContainer>("MarginContainer/VBox/JumpTableScroll/JumpTable"),
            history,
            run
        );

        GetNode<Button>("MarginContainer/VBox/NewRunButton").Pressed += OnNewRun;
        GetNode<Button>("MarginContainer/VBox/MenuButton").Pressed += OnReturnToMenu;
    }

    // ── Jump table with accordion ─────────────────────────────────────────────

    private static void BuildJumpTable(
        VBoxContainer table,
        IReadOnlyList<JumpRecord> history,
        RunState run
    )
    {
        if (history.Count == 0)
        {
            table.AddChild(
                new Label
                {
                    Text =
                        "(No jump records — history is in-memory only; start a new run to see data here.)",
                    Modulate = new Color(0.6f, 0.6f, 0.6f),
                }
            );
            return;
        }

        // Header row
        table.AddChild(
            new Label
            {
                Text =
                    $"  {"Jump", -6}{"W/L", -6}{"Kills", -8}{"Lost", -8}{"Dealt", -10}{"Taken", -10}{"Ticks", -8}",
            }
        );
        table.AddChild(new HSeparator());

        var currentJump = run.JumpNumber;

        foreach (var record in history)
        {
            var rowContainer = new VBoxContainer();
            table.AddChild(rowContainer);

            var kills = record.EnemiesKilledByHullClass.Values.Sum();
            var lost = record.OwnShipsLostByHullClass.Values.Sum();
            var isCurrent =
                record.JumpNumber == currentJump - 1
                || (record.JumpNumber == currentJump && run.RunEndReason == RunEndReason.Victory);

            var summaryBtn = new Button
            {
                Text =
                    $"  {record.JumpNumber, -6}{(record.Won ? "W" : "L"), -6}{kills, -8}{lost, -8}{record.DamageDealt, -10:0}{record.DamageTaken, -10:0}{record.DurationTicks, -8}",
                Flat = true,
                Modulate = isCurrent ? new Color(1f, 1f, 0.7f) : Colors.White,
            };
            rowContainer.AddChild(summaryBtn);

            // Accordion body (hidden by default)
            var accordion = BuildAccordionBody(record, run);
            accordion.Visible = false;
            rowContainer.AddChild(accordion);

            summaryBtn.Pressed += () => accordion.Visible = !accordion.Visible;

            table.AddChild(new HSeparator());
        }
    }

    private static Control BuildAccordionBody(JumpRecord record, RunState run)
    {
        var body = new VBoxContainer();
        body.AddChild(new HSeparator());

        // Kill breakdown by hull class
        if (record.EnemiesKilledByHullClass.Count > 0)
        {
            body.AddChild(new Label { Text = "  Enemies killed by hull class:" });
            foreach (var (hc, count) in record.EnemiesKilledByHullClass)
                body.AddChild(new Label { Text = $"    {hc}: {count}" });
        }
        if (record.OwnShipsLostByHullClass.Count > 0)
        {
            body.AddChild(new Label { Text = "  Own ships lost by hull class:" });
            foreach (var (hc, count) in record.OwnShipsLostByHullClass)
                body.AddChild(new Label { Text = $"    {hc}: {count}" });
        }

        body.AddChild(new HSeparator());

        // Fleet inspector: side by side
        var fleetsRow = new HBoxContainer();
        body.AddChild(fleetsRow);

        fleetsRow.AddChild(
            FleetInspector.Build(record.PlayerFleetSnapshot, record.PlayerUpgrades, true)
        );
        fleetsRow.AddChild(new VSeparator());
        fleetsRow.AddChild(FleetInspector.Build(record.OpponentFleetSnapshot, null, false));

        return body;
    }

    // ── Navigation ────────────────────────────────────────────────────────────

    private void OnNewRun()
    {
        GetTree().ChangeSceneToFile("res://scenes/AdmiralSelect.tscn");
    }

    private void OnReturnToMenu()
    {
        GetTree().ChangeSceneToFile("res://scenes/MainMenu.tscn");
    }
}
