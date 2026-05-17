using Godot;

namespace Skock.Meta;

public partial class AdmiralSelectUi : Control
{
    private HBoxContainer _admiralCards = null!;

    public override void _Ready()
    {
        _admiralCards = GetNode<HBoxContainer>("MarginContainer/VBox/AdmiralCards");
        BuildCards();
    }

    private void BuildCards()
    {
        foreach (var admiral in RunState.Instance.Catalog.GetAdmirals())
        {
            var card = BuildCard(admiral);
            _admiralCards.AddChild(card);
        }
    }

    private PanelContainer BuildCard(Admiral admiral)
    {
        var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var vbox = new VBoxContainer();
        panel.AddChild(vbox);

        var nameLabel = new Label
        {
            Text = admiral.Name,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        nameLabel.AddThemeFontSizeOverride("font_size", 20);
        vbox.AddChild(nameLabel);

        vbox.AddChild(new HSeparator());

        vbox.AddChild(
            new Label { Text = admiral.BonusText, AutowrapMode = TextServer.AutowrapMode.WordSmart }
        );

        vbox.AddChild(new HSeparator());

        var fleetLabel = new Label { Text = "Starting fleet:" };
        vbox.AddChild(fleetLabel);

        foreach (var ship in admiral.StartingFleet.Ships)
        {
            vbox.AddChild(
                new Label
                {
                    Text = $"  • {ShipDisplay.NameFor(ship)}  [{ship.HullClass.Tonnage()}T]",
                }
            );
        }

        vbox.AddChild(
            new Label
            {
                Text =
                    $"\nSalvage: {admiral.StartingSalvage}   Tech: {admiral.StartingTech}   Hangar: {admiral.StartingHangarCapacity}T",
            }
        );

        var spacer = new Control { SizeFlagsVertical = SizeFlags.ExpandFill };
        vbox.AddChild(spacer);

        var selectBtn = new Button { Text = "SELECT" };
        var captured = admiral;
        selectBtn.Pressed += () => OnAdmiralSelected(captured);
        vbox.AddChild(selectBtn);

        return panel;
    }

    private async void OnAdmiralSelected(Admiral admiral)
    {
        await RunState.Instance.StartRun(admiral);
        GetTree().ChangeSceneToFile("res://scenes/Dockyard.tscn");
    }
}
