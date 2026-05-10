using System.Text.Json;
using FS25FfbBridge.App.Models;
using FS25FfbBridge.App.Services;
using FS25FfbBridge.App.ViewModels;

namespace FS25FfbBridge.App.Tests;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public void Stop_all_does_not_persistently_disable_gameplay_ffb()
    {
        var directory = Path.Combine(Path.GetTempPath(), "FS25FfbBridge.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var configPath = Path.Combine(directory, "config.json");
        var store = new ConfigStore(configPath);
        store.Save(new AppConfig { GameplayFfb = { Enabled = true }, TelemetryPort = GetFreeUdpPort() });

        using var log = new AppLogService();
        using var telemetry = new TelemetryReceiverService(log);
        using var viewModel = new MainWindowViewModel(store, new FakeFfbBackend(), telemetry, log);

        viewModel.StopAllEffectsCommand.Execute(null);

        var saved = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(configPath), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(saved);
        Assert.True(saved!.GameplayFfb.Enabled);
        Assert.True(viewModel.GameplayFfbEnabled);
    }

    [Fact]
    public void Dashboard_ffb_status_prefers_user_setting_over_runtime_text()
    {
        var directory = Path.Combine(Path.GetTempPath(), "FS25FfbBridge.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var configPath = Path.Combine(directory, "config.json");
        var store = new ConfigStore(configPath);
        store.Save(new AppConfig { GameplayFfb = { Enabled = false }, TelemetryPort = GetFreeUdpPort() });

        using var log = new AppLogService();
        using var telemetry = new TelemetryReceiverService(log);
        using var viewModel = new MainWindowViewModel(store, new FakeFfbBackend(), telemetry, log);

        Assert.False(viewModel.GameplayFfbEnabled);
        Assert.Equal("off", viewModel.FfbStatus);
        Assert.Equal("FFB: off", viewModel.FfbReadyText);
        Assert.Contains("disabled", viewModel.FfbReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Vehicle_category_display_names_are_player_readable()
    {
        Assert.Equal("Loader / telehandler", MainWindowViewModel.GetVehicleCategoryDisplayName(VehicleCategoryFfbProfile.LoaderTelehandler));
        Assert.Equal("Heavy tracked tractor", MainWindowViewModel.GetVehicleCategoryDisplayName(VehicleCategoryFfbProfile.HeavyTractorTracked));
    }

    [Fact]
    public void Global_force_limit_is_the_only_saved_safety_cap()
    {
        var directory = Path.Combine(Path.GetTempPath(), "FS25FfbBridge.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var configPath = Path.Combine(directory, "config.json");
        var store = new ConfigStore(configPath);
        store.Save(new AppConfig { GlobalForceLimitPercent = 40, DeviceForceLimitPercent = 35, TelemetryPort = GetFreeUdpPort() });
        var backend = new FakeFfbBackend();

        using var log = new AppLogService();
        using var telemetry = new TelemetryReceiverService(log);
        using var viewModel = new MainWindowViewModel(store, backend, telemetry, log);

        viewModel.GlobalForceLimitPercent = 55;

        var saved = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(configPath), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(saved);
        Assert.Equal(55, saved!.GlobalForceLimitPercent);
        Assert.Equal(100, saved.DeviceForceLimitPercent);
        Assert.Equal(100, saved.GameplayFfb.SpeedSpring.MaxOutputPercent);
        Assert.All(saved.GameplayFfb.VehicleCategoryEffectProfiles.Values, profile =>
            Assert.Equal(100, profile.SpeedSpring.MaxOutputPercent));
        Assert.Equal(55, backend.LastGlobalLimitPercent);
        Assert.Equal(100, backend.LastDeviceLimitPercent);
    }

    [Fact]
    public void Effect_editor_follows_active_vehicle_category_until_user_selects_profile()
    {
        var directory = Path.Combine(Path.GetTempPath(), "FS25FfbBridge.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var configPath = Path.Combine(directory, "config.json");
        var store = new ConfigStore(configPath);
        store.Save(new AppConfig { TelemetryPort = GetFreeUdpPort() });

        using var log = new AppLogService();
        using var telemetry = new TelemetryReceiverService(log);
        using var viewModel = new MainWindowViewModel(store, new FakeFfbBackend(), telemetry, log);

        ApplyTelemetryState(viewModel, VehicleCategoryFfbProfile.Truck);
        viewModel.TerrainRumbleEnabled = false;

        var saved = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(configPath), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.Equal(VehicleCategoryFfbProfile.Truck, viewModel.SelectedEffectCategory);
        Assert.NotNull(saved);
        Assert.False(saved!.GameplayFfb.VehicleCategoryEffectProfiles[VehicleCategoryFfbProfile.Truck].TerrainRumble.Enabled);
        Assert.True(saved.GameplayFfb.VehicleCategoryEffectProfiles[VehicleCategoryFfbProfile.TractorWheeled].TerrainRumble.Enabled);
    }

    [Fact]
    public void Manual_effect_category_selection_is_not_overridden_by_active_vehicle()
    {
        var directory = Path.Combine(Path.GetTempPath(), "FS25FfbBridge.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var configPath = Path.Combine(directory, "config.json");
        var store = new ConfigStore(configPath);
        store.Save(new AppConfig { TelemetryPort = GetFreeUdpPort() });

        using var log = new AppLogService();
        using var telemetry = new TelemetryReceiverService(log);
        using var viewModel = new MainWindowViewModel(store, new FakeFfbBackend(), telemetry, log);

        viewModel.SelectedEffectCategory = VehicleCategoryFfbProfile.Harvester;
        ApplyTelemetryState(viewModel, VehicleCategoryFfbProfile.Truck);

        Assert.Equal(VehicleCategoryFfbProfile.Harvester, viewModel.SelectedEffectCategory);
    }

    private static int GetFreeUdpPort()
    {
        using var udp = new System.Net.Sockets.UdpClient(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 0));
        return ((System.Net.IPEndPoint)udp.Client.LocalEndPoint!).Port;
    }

    private static void ApplyTelemetryState(MainWindowViewModel viewModel, string vehicleCategory)
    {
        var packet = new TelemetryPacketV1
        {
            Protocol = new TelemetryProtocolV1 { Name = TelemetryPacketV1.ExpectedProtocolName, Version = TelemetryPacketV1.ExpectedProtocolVersion },
            Frame = new TelemetryFrameV1 { TimestampMs = 1, DtMs = 33, TelemetryRateHz = 30, Sequence = 1, IsDuplicate = false, IsInterpolated = false },
            Game = new TelemetryGameV1 { State = "mission" },
            Player = new TelemetryPlayerV1 { IsInVehicle = true },
            Vehicle = new TelemetryVehicleV1 { Name = "Truck", Type = "truck", Category = vehicleCategory },
            Motion = new TelemetryMotionV1 { SpeedKmh = 20, SpeedMps = 20 / 3.6 },
            Wheels = [],
            Attachments = [],
            Diagnostics = new TelemetryDiagnosticsV1()
        };
        var state = new TelemetryReceiverState(
            TelemetryStatus.Connected,
            packet,
            "{}",
            30,
            TimeSpan.FromMilliseconds(50),
            null,
            "127.0.0.1:34325",
            "udp",
            "file",
            "udp",
            null);
        var method = typeof(MainWindowViewModel).GetMethod("ApplyTelemetryState", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(viewModel, [state]);
    }

    private sealed class FakeFfbBackend : IFfbBackend
    {
        public DeviceInfo? SelectedDevice => null;
        public bool HasSelectedFfbDevice => false;
        public int LastGlobalLimitPercent { get; private set; }
        public int LastDeviceLimitPercent { get; private set; }
        public IReadOnlyList<DeviceInfo> ScanDevices() => [];
        public bool SelectDevice(DeviceInfo device, IntPtr windowHandle, int globalLimitPercent, int deviceLimitPercent) => true;
        public void UpdateForceLimits(int globalLimitPercent, int deviceLimitPercent)
        {
            LastGlobalLimitPercent = globalLimitPercent;
            LastDeviceLimitPercent = deviceLimitPercent;
        }
        public void StartTestEffect(FfbEffectKind kind) { }
        public FfbApplyResult ApplyGameplayEffects(GameplayFfbOutput output) => FfbApplyResult.Applied;
        public void StopGameplayEffects(string reason) { }
        public void StopAllEffects(string reason) { }
        public void Dispose() { }
    }
}
