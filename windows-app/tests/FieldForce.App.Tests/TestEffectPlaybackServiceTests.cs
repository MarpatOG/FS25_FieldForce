using FieldForce.App.Models;
using FieldForce.App.Services;

namespace FieldForce.App.Tests;

public sealed class TestEffectPlaybackServiceTests
{
    [Fact]
    public async Task Mod_effect_test_uses_selected_category_profile_strength()
    {
        var profile = new GameplayFfbEffectProfile();
        profile.SpeedSpring.StrengthPercent = 75;
        profile.SpeedSpring.Enabled = true;
        var backend = new CapturingFfbBackend();
        using var log = new AppLogService();
        using var playback = new TestEffectPlaybackService(
            backend,
            log,
            () => profile,
            () => VehicleCategoryFfbProfile.Truck);

        var run = playback.StartModAsync(TestFfbEffectKind.SpeedSpring);
        await Task.Delay(120);
        playback.StopAll("test complete");
        await run;

        Assert.Contains(backend.GameplayOutputs, output =>
            output.ActiveCategory == VehicleCategoryFfbProfile.Truck &&
            output.SpringPercent > 0 &&
            output.SpringPercent <= 75);
    }

    [Fact]
    public void Basic_test_duration_is_seven_seconds()
    {
        Assert.Equal(TimeSpan.FromSeconds(7), TestEffectPlaybackService.TestDuration);
    }

    private sealed class CapturingFfbBackend : IFfbBackend
    {
        public DeviceInfo? SelectedDevice => null;
        public bool HasSelectedFfbDevice => true;
        public List<GameplayFfbOutput> GameplayOutputs { get; } = [];
        public IReadOnlyList<DeviceInfo> ScanDevices() => [];
        public bool SelectDevice(DeviceInfo device, IntPtr windowHandle, int globalLimitPercent, int deviceLimitPercent, int? primaryFfbAxisOffset) => true;
        public void UpdateForceLimits(int globalLimitPercent, int deviceLimitPercent) { }
        public void StartTestEffect(FfbEffectKind kind) { }
        public FfbApplyResult ApplyGameplayEffects(GameplayFfbOutput output)
        {
            GameplayOutputs.Add(output);
            return FfbApplyResult.Applied;
        }
        public void StopGameplayEffects(string reason) { }
        public void StopAllEffects(string reason) { }
        public bool TryGetSelectedDeviceButtons(out bool[] buttons)
        {
            buttons = [];
            return false;
        }
        public void Dispose() { }
    }
}
