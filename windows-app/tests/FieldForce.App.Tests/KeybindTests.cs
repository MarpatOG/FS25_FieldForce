using System.Text.Json;
using FieldForce.App.Models;
using FieldForce.App.Services;
using FieldForce.App.ViewModels;

namespace FieldForce.App.Tests;

public sealed class KeybindTests
{
    [Fact]
    public void AppConfig_defaults_to_empty_keybinds()
    {
        var config = new AppConfig();

        Assert.All(KeybindsConfig.Actions, action => Assert.True(config.Keybinds.Get(action).IsNone));
    }

    [Fact]
    public void Keybinds_round_trip_through_json()
    {
        var config = new AppConfig();
        config.Keybinds.ToggleFfb = InputBinding.Keyboard(0x46, KeyboardModifiers.Control | KeyboardModifiers.Alt);
        config.Keybinds.EmergencyStop = InputBinding.DirectInputButton("stable-device", "Test Wheel", 3);

        var json = JsonSerializer.Serialize(config);
        var decoded = JsonSerializer.Deserialize<AppConfig>(json);

        Assert.NotNull(decoded);
        Assert.Equal(config.Keybinds.ToggleFfb, decoded!.Keybinds.ToggleFfb);
        Assert.Equal(config.Keybinds.EmergencyStop, decoded.Keybinds.EmergencyStop);
    }

    [Fact]
    public void Duplicate_binding_moves_to_new_action_and_clears_old_action()
    {
        using var fixture = ViewModelFixture.Create();
        var binding = InputBinding.Keyboard(0x46, KeyboardModifiers.Control);

        fixture.ViewModel.AssignKeybind(KeybindAction.ToggleFfb, binding);
        fixture.ViewModel.AssignKeybind(KeybindAction.Reload, binding);

        Assert.True(fixture.ViewModel.KeybindRows.Single(row => row.Action == KeybindAction.ToggleFfb).Binding == "None");
        Assert.Equal("Ctrl+F", fixture.ViewModel.KeybindRows.Single(row => row.Action == KeybindAction.Reload).Binding);
    }

    [Fact]
    public void Backspace_clears_active_recording_action()
    {
        using var fixture = ViewModelFixture.Create();
        fixture.ViewModel.AssignKeybind(KeybindAction.ToggleOverlay, InputBinding.Keyboard(0x4F, KeyboardModifiers.Control));

        fixture.ViewModel.StartKeybindRecordingCommand.Execute(KeybindAction.ToggleOverlay);
        var handled = fixture.ViewModel.HandleKeybindRecordingKeyboard(0x08, KeyboardModifiers.None);

        Assert.True(handled);
        Assert.Equal("None", fixture.ViewModel.KeybindRows.Single(row => row.Action == KeybindAction.ToggleOverlay).Binding);
    }

    [Fact]
    public void Fake_input_source_dispatches_once_on_rising_edge()
    {
        using var log = new AppLogService();
        using var source = new FakeJoystickButtonPoller();
        using var dispatcher = new KeybindDispatcherService(log, source);
        var count = 0;
        dispatcher.Pressed += action =>
        {
            if (action == KeybindAction.EmergencyStop)
            {
                count++;
            }
        };

        dispatcher.Apply(new KeybindsConfig
        {
            EmergencyStop = InputBinding.DirectInputButton("dev", "Wheel", 0)
        });

        source.SetButton("dev", 0, true);
        source.SetButton("dev", 0, true);
        Assert.Equal(1, count);

        source.SetButton("dev", 0, false);
        source.SetButton("dev", 0, true);

        Assert.Equal(2, count);
    }

    [Fact]
    public void Keybind_actions_update_expected_view_model_state()
    {
        using var fixture = ViewModelFixture.Create();

        fixture.ViewModel.ExecuteKeybindAction(KeybindAction.ToggleFfb);
        Assert.False(fixture.ViewModel.GameplayFfbEnabled);

        fixture.ViewModel.ExecuteKeybindAction(KeybindAction.EmergencyStop);
        Assert.Equal("Paused by emergency stop", fixture.ViewModel.GameplayFfbRuntimeStatus);

        fixture.ViewModel.ExecuteKeybindAction(KeybindAction.ToggleFfb);
        fixture.ViewModel.ExecuteKeybindAction(KeybindAction.Reload);
        Assert.Equal("Reloaded", fixture.ViewModel.GameplayFfbRuntimeStatus);

        var overlay = fixture.ViewModel.EffectOverlayEnabled;
        fixture.ViewModel.ExecuteKeybindAction(KeybindAction.ToggleOverlay);
        Assert.Equal(!overlay, fixture.ViewModel.EffectOverlayEnabled);

        var clickThrough = fixture.ViewModel.EffectOverlayClickThrough;
        fixture.ViewModel.ExecuteKeybindAction(KeybindAction.ToggleOverlayClickThrough);
        Assert.Equal(!clickThrough, fixture.ViewModel.EffectOverlayClickThrough);
    }

    private sealed class ViewModelFixture : IDisposable
    {
        private ViewModelFixture(string directory, MainWindowViewModel viewModel)
        {
            Directory = directory;
            ViewModel = viewModel;
        }

        public string Directory { get; }
        public MainWindowViewModel ViewModel { get; }

        public static ViewModelFixture Create()
        {
            var directory = Path.Combine(Path.GetTempPath(), "FieldForce.Tests", Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(directory);
            var store = new ConfigStore(Path.Combine(directory, "config.json"));
            store.Save(new AppConfig { TelemetryPort = GetFreeUdpPort() });
            var log = new AppLogService();
            var telemetry = new TelemetryReceiverService(log);
            return new ViewModelFixture(directory, new MainWindowViewModel(store, new FakeFfbBackend(), telemetry, log));
        }

        public void Dispose()
        {
            ViewModel.Dispose();
            try
            {
                System.IO.Directory.Delete(Directory, recursive: true);
            }
            catch
            {
                // Temp cleanup best effort.
            }
        }
    }

    private sealed class FakeJoystickButtonPoller : IJoystickButtonPoller
    {
        private readonly Dictionary<string, bool[]> _previous = [];
        private Dictionary<KeybindAction, InputBinding> _bindings = [];

        public IReadOnlyDictionary<KeybindAction, InputBinding> Bindings => _bindings;
        public event Action<KeybindAction>? ButtonPressed;

        public void Apply(IReadOnlyDictionary<KeybindAction, InputBinding> bindings)
        {
            _bindings = bindings.ToDictionary();
        }

        public void SetButton(string stableId, int buttonIndex, bool pressed)
        {
            if (!_previous.TryGetValue(stableId, out var buttons))
            {
                buttons = new bool[Math.Max(8, buttonIndex + 1)];
                _previous[stableId] = buttons;
            }

            var wasPressed = buttons[buttonIndex];
            buttons[buttonIndex] = pressed;
            if (!pressed || wasPressed)
            {
                return;
            }

            foreach (var (action, binding) in _bindings)
            {
                if (binding.Kind == InputBindingKind.DirectInputButton &&
                    binding.DeviceStableId == stableId &&
                    binding.ButtonIndex == buttonIndex)
                {
                    ButtonPressed?.Invoke(action);
                }
            }
        }

        public void Dispose()
        {
        }
    }

    private sealed class FakeFfbBackend : IFfbBackend
    {
        public DeviceInfo? SelectedDevice => null;
        public bool HasSelectedFfbDevice => false;
        public IReadOnlyList<DeviceInfo> ScanDevices() => [];
        public bool SelectDevice(DeviceInfo device, IntPtr windowHandle, int globalLimitPercent, int deviceLimitPercent, int? primaryFfbAxisOffset) => true;
        public void UpdateForceLimits(int globalLimitPercent, int deviceLimitPercent) { }
        public void StartTestEffect(FfbEffectKind kind) { }
        public FfbApplyResult ApplyGameplayEffects(GameplayFfbOutput output) => FfbApplyResult.Applied;
        public void StopGameplayEffects(string reason) { }
        public void StopAllEffects(string reason) { }
        public bool TryGetSelectedDeviceButtons(out bool[] buttons)
        {
            buttons = [];
            return false;
        }
        public void Dispose() { }
    }

    private static int GetFreeUdpPort()
    {
        using var udp = new System.Net.Sockets.UdpClient(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 0));
        return ((System.Net.IPEndPoint)udp.Client.LocalEndPoint!).Port;
    }
}
