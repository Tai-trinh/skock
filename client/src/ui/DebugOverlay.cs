using Godot;
using Skock.Sim;

namespace Skock.UI;

/// <summary>
/// Debug overlay toggled with F1. Shows raw tick state for the selected ship.
/// Press Tab to cycle through ships. Reads directly from the in-memory battle log.
/// Implemented as a CanvasLayer child of the Battle scene.
/// </summary>
public partial class DebugOverlay : CanvasLayer
{
    private Label _label = null!;
    private bool _visible;
    private uint _selectedShipId;

    public override void _Ready()
    {
        _label = GetNode<Label>("Label");
        Visible = false;
    }

    public override void _Input(InputEvent @event)
    {
        if (@event.IsActionPressed("toggle_debug"))
        {
            _visible = !_visible;
            Visible = _visible;
        }
    }

    public void UpdateFromSnapshot(ShipSnapshot[] ships, uint selectedId)
    {
        if (!_visible || ships.Length == 0)
            return;

        var ship = Array.Find(ships, s => s.Id == selectedId) ?? ships[0];
        _selectedShipId = ship.Id;

        _label.Text =
            $"[Ship {ship.Id}]  fleet={'A' + ship.Fleet}  mothership={ship.IsMothership}\n"
            + $"pos  ({ship.WorldX:F1}, {ship.WorldY:F1})\n"
            + $"heading  {Mathf.RadToDeg(ship.HeadingRad):F0}°\n"
            + $"hp  {ship.Hp} / {ship.MaxHp}  ({ship.HpFraction:P0})\n"
            + $"shield  {ship.ShieldHp} / {ship.ShieldMaxHp}";
    }
}
