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

    private static int GetFreeUdpPort()
    {
        using var udp = new System.Net.Sockets.UdpClient(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 0));
        return ((System.Net.IPEndPoint)udp.Client.LocalEndPoint!).Port;
    }

    private sealed class FakeFfbBackend : IFfbBackend
    {
        public DeviceInfo? SelectedDevice => null;
        public bool HasSelectedFfbDevice => false;
        public IReadOnlyList<DeviceInfo> ScanDevices() => [];
        public bool SelectDevice(DeviceInfo device, IntPtr windowHandle, int globalLimitPercent, int deviceLimitPercent) => true;
        public void UpdateForceLimits(int globalLimitPercent, int deviceLimitPercent) { }
        public void StartTestEffect(FfbEffectKind kind) { }
        public FfbApplyResult ApplyGameplayEffects(GameplayFfbOutput output) => FfbApplyResult.Applied;
        public void StopGameplayEffects(string reason) { }
        public void StopAllEffects(string reason) { }
        public void Dispose() { }
    }
}
