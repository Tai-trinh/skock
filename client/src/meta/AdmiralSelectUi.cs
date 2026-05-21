using System.Threading.Tasks;
using Godot;

namespace Skock.Meta;

public partial class AdmiralSelectUi : Control
{
    private HBoxContainer _admiralCards = null!;

    public override void _Ready()
    {
        _admiralCards = GetNode<HBoxContainer>("MarginContainer/VBox/AdmiralCards");
        _ = LoadOffersAsync();
    }

    private async Task LoadOffersAsync()
    {
        var result = await RunState.Instance.AdmiralSelection.GetOffersAsync(
            RunState.Instance.PlayerId
        );
        foreach (var offer in result.Offers)
            _admiralCards.AddChild(BuildCard(offer, result.RunSeed));
    }

    private PanelContainer BuildCard(AdmiralOfferDto offer, ulong runSeed)
    {
        var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var vbox = new VBoxContainer();
        panel.AddChild(vbox);

        var nameLabel = new Label
        {
            Text = offer.AdmiralName,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        nameLabel.AddThemeFontSizeOverride("font_size", 20);
        vbox.AddChild(nameLabel);

        vbox.AddChild(new HSeparator());

        vbox.AddChild(
            new Label { Text = offer.BonusText, AutowrapMode = TextServer.AutowrapMode.WordSmart }
        );

        vbox.AddChild(new HSeparator());

        vbox.AddChild(new Label { Text = "Starting fleet:" });

        foreach (var ship in offer.StartingFleet.Ships)
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
                    $"\nSalvage: {offer.StartingSalvage}   Tech: {offer.StartingTech}   Hangar: {offer.StartingHangarCapacity}T",
            }
        );

        var spacer = new Control { SizeFlagsVertical = SizeFlags.ExpandFill };
        vbox.AddChild(spacer);

        var selectBtn = new Button { Text = "SELECT" };
        selectBtn.Pressed += () => OnAdmiralSelected(offer, runSeed);
        vbox.AddChild(selectBtn);

        return panel;
    }

    private async void OnAdmiralSelected(AdmiralOfferDto offer, ulong runSeed)
    {
        offer.StartingFleet.Mothership.IsMothership = true;
        var admiral = new Admiral
        {
            Id = offer.AdmiralId,
            Name = offer.AdmiralName,
            FactionId = offer.FactionId,
            BonusText = offer.BonusText,
            StartingSalvage = offer.StartingSalvage,
            StartingTech = offer.StartingTech,
            StartingHangarCapacity = offer.StartingHangarCapacity,
            StartingFleet = offer.StartingFleet,
        };
        await RunState.Instance.StartRun(admiral, runSeed);
        GetTree().ChangeSceneToFile("res://scenes/Dockyard.tscn");
    }
}
