using System;
using Godot;

namespace Skock.Meta;

// Autoload CanvasLayer — always visible above all scenes.
// Toggled by Escape; blocked during battle playback.
public partial class OptionsOverlay : CanvasLayer
{
    public static OptionsOverlay Instance { get; private set; } = null!;

    private VBoxContainer _runSection = null!;
    private ConfirmationDialog _abandonConfirm = null!;

    public override void _Ready()
    {
        Instance = this;
        Visible = false;
        BuildUi();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key)
            return;
        if (key.Keycode != Key.Escape)
            return;
        if (RunState.Instance.IsBattleActive)
            return;

        if (Visible)
            Visible = false;
        else
            Open();

        GetViewport().SetInputAsHandled();
    }

    public void Open()
    {
        _runSection.Visible = RunState.Instance.HasActiveRun;
        Visible = true;
    }

    private void BuildUi()
    {
        var root = new Control();
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);

        var bg = new ColorRect
        {
            Color = new Color(0f, 0f, 0f, 0.75f),
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(bg);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(center);

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(520, 0) };
        center.AddChild(panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 48);
        margin.AddThemeConstantOverride("margin_top", 48);
        margin.AddThemeConstantOverride("margin_right", 48);
        margin.AddThemeConstantOverride("margin_bottom", 48);
        panel.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 12);
        margin.AddChild(vbox);

        // Title
        var title = new Label
        {
            Text = "OPTIONS",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        title.AddThemeFontSizeOverride("font_size", 32);
        vbox.AddChild(title);
        vbox.AddChild(new HSeparator());

        // Audio
        var audioHeader = new Label { Text = "AUDIO" };
        audioHeader.AddThemeFontSizeOverride("font_size", 14);
        vbox.AddChild(audioHeader);

        var s = RunState.Instance.Settings;
        vbox.AddChild(
            SliderRow(
                "Master",
                s.MasterVolume,
                v =>
                {
                    RunState.Instance.Settings.MasterVolume = v;
                    RunState.Instance.Settings.Apply();
                    RunState.Instance.SaveSettings();
                }
            )
        );
        vbox.AddChild(
            SliderRow(
                "Music",
                s.MusicVolume,
                v =>
                {
                    RunState.Instance.Settings.MusicVolume = v;
                    RunState.Instance.Settings.Apply();
                    RunState.Instance.SaveSettings();
                }
            )
        );
        vbox.AddChild(
            SliderRow(
                "SFX",
                s.SfxVolume,
                v =>
                {
                    RunState.Instance.Settings.SfxVolume = v;
                    RunState.Instance.Settings.Apply();
                    RunState.Instance.SaveSettings();
                }
            )
        );

        vbox.AddChild(new HSeparator());

        // Display
        var displayHeader = new Label { Text = "DISPLAY" };
        displayHeader.AddThemeFontSizeOverride("font_size", 14);
        vbox.AddChild(displayHeader);

        var fsRow = new HBoxContainer();
        var fsLabel = new Label
        {
            Text = "Fullscreen",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        var fsCheck = new CheckBox { ButtonPressed = s.Fullscreen };
        fsCheck.Toggled += on =>
        {
            RunState.Instance.Settings.Fullscreen = on;
            RunState.Instance.Settings.Apply();
            RunState.Instance.SaveSettings();
        };
        fsRow.AddChild(fsLabel);
        fsRow.AddChild(fsCheck);
        vbox.AddChild(fsRow);

        // Run section (shown only when a run is active)
        _runSection = new VBoxContainer();
        _runSection.AddThemeConstantOverride("separation", 8);
        vbox.AddChild(_runSection);

        _runSection.AddChild(new HSeparator());

        var runHeader = new Label { Text = "RUN" };
        runHeader.AddThemeFontSizeOverride("font_size", 14);
        _runSection.AddChild(runHeader);

        var saveQuitBtn = new Button { Text = "SAVE & QUIT TO MENU" };
        saveQuitBtn.Pressed += async () =>
        {
            Visible = false;
            await RunState.Instance.SaveAndQuitToMenu();
        };
        _runSection.AddChild(saveQuitBtn);

        var abandonBtn = new Button { Text = "ABANDON RUN" };
        abandonBtn.Pressed += () => _abandonConfirm.PopupCentered();
        _runSection.AddChild(abandonBtn);

        // Close
        vbox.AddChild(new HSeparator());
        var closeBtn = new Button { Text = "CLOSE" };
        closeBtn.Pressed += () => Visible = false;
        vbox.AddChild(closeBtn);

        // Abandon confirmation dialog
        _abandonConfirm = new ConfirmationDialog
        {
            Title = "Abandon Run",
            DialogText = "Abandon this run? This cannot be undone.",
        };
        _abandonConfirm.Confirmed += async () =>
        {
            Visible = false;
            await RunState.Instance.AbandonCurrentRun();
            GetTree().ChangeSceneToFile("res://scenes/MainMenu.tscn");
        };
        AddChild(_abandonConfirm);
    }

    private static HBoxContainer SliderRow(string label, float value, Action<float> onChange)
    {
        var row = new HBoxContainer();
        var lbl = new Label { Text = label, CustomMinimumSize = new Vector2(70, 0) };
        var slider = new HSlider
        {
            MinValue = 0.0,
            MaxValue = 1.0,
            Step = 0.01,
            Value = value,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        slider.ValueChanged += v => onChange((float)v);
        row.AddChild(lbl);
        row.AddChild(slider);
        return row;
    }
}
