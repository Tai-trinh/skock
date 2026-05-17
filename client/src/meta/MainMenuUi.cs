using Godot;

namespace Skock.Meta;

public partial class MainMenuUi : Control
{
    private ConfirmationDialog _newRunConfirm = null!;

    public override void _Ready()
    {
        var startBtn = GetNode<Button>("MarginContainer/VBox/StartButton");
        var continueBtn = GetNode<Button>("MarginContainer/VBox/ContinueButton");
        var optionsBtn = GetNode<Button>("MarginContainer/VBox/OptionsButton");

        continueBtn.Disabled = !RunState.Instance.HasActiveRun;

        startBtn.Pressed += OnStartPressed;
        continueBtn.Pressed += () => GetTree().ChangeSceneToFile("res://scenes/Dockyard.tscn");
        optionsBtn.Pressed += OptionsOverlay.Instance.Open;
        GetNode<Button>("MarginContainer/VBox/QuitButton").Pressed += () => GetTree().Quit();

        _newRunConfirm = new ConfirmationDialog
        {
            Title = "New Run",
            DialogText = "Abandon current run and start a new one?",
        };
        _newRunConfirm.Confirmed += StartNewRun;
        AddChild(_newRunConfirm);
    }

    private void OnStartPressed()
    {
        if (RunState.Instance.HasActiveRun)
            _newRunConfirm.PopupCentered();
        else
            StartNewRun();
    }

    private async void StartNewRun()
    {
        await RunState.Instance.AbandonCurrentRun();
        GetTree().ChangeSceneToFile("res://scenes/AdmiralSelect.tscn");
    }
}
