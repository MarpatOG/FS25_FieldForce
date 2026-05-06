using System.Text.Json;
using FS25FfbBridge.App.Models;

namespace FS25FfbBridge.App.Tests;

public sealed class AppConfigTests
{
    [Fact]
    public void Defaults_use_current_gameplay_effect_profile()
    {
        var config = new AppConfig();

        Assert.Equal(4, config.EffectsProfileVersion);
        Assert.Equal("Logitech MOMO Racing Wheel", config.DeviceProfileName);
        Assert.Equal(270, config.RotationDegrees);
        Assert.Equal(40, config.GlobalForceLimitPercent);
        Assert.Equal(35, config.DeviceForceLimitPercent);
        Assert.True(config.GameplayFfb.Enabled);
        Assert.True(config.GameplayFfb.SpeedSpring.Enabled);
        Assert.True(config.GameplayFfb.SpeedDamper.Enabled);
        Assert.True(config.GameplayFfb.MechanicalFriction.Enabled);
        Assert.Equal(60, config.GameplayFfb.SpeedSpring.StrengthPercent);
        Assert.Equal(65, config.GameplayFfb.SpeedSpring.MaxOutputPercent);
        Assert.Equal(50, config.GameplayFfb.SpeedSpring.SpeedReferenceKmh);
        Assert.Equal(65, config.GameplayFfb.SpeedDamper.StrengthPercent);
        Assert.Equal(70, config.GameplayFfb.SpeedDamper.MaxOutputPercent);
        Assert.Equal(55, config.GameplayFfb.SpeedDamper.SpeedReferenceKmh);
        Assert.Equal(45, config.GameplayFfb.MechanicalFriction.StrengthPercent);
        Assert.Equal(55, config.GameplayFfb.MechanicalFriction.MaxOutputPercent);
        Assert.Equal(0.18, config.GameplayFfb.MechanicalFriction.BaseFriction);
        Assert.Equal(60, config.GameplayFfb.LoadResistance.StrengthPercent);
        Assert.True(config.GameplayFfb.LoadResistance.AffectsFriction);
        Assert.Equal(45, config.GameplayFfb.EngineVibration.StrengthPercent);
        Assert.Equal(12, config.GameplayFfb.EngineVibration.MinFrequencyHz);
        Assert.Equal(30, config.GameplayFfb.EngineVibration.MaxFrequencyHz);
        Assert.Equal(45, config.GameplayFfb.SurfaceFeedback.StrengthPercent);
        Assert.Equal(0.2, config.GameplayFfb.SurfaceFeedback.MinSpeedKmh);
        Assert.Equal(8, config.GameplayFfb.SurfaceFeedback.FieldFrequencyMinHz);
        Assert.Equal(24, config.GameplayFfb.SurfaceFeedback.FieldFrequencyMaxHz);
        Assert.True(config.GameplayFfb.SlipFeedback.Enabled);
        Assert.Equal(45, config.GameplayFfb.SlipFeedback.StrengthPercent);
        Assert.Equal(0.12, config.GameplayFfb.SlipFeedback.MinSlip);
        Assert.Equal(0.5, config.GameplayFfb.SlipFeedback.MinSpeedKmh);
        Assert.True(config.GameplayFfb.WetnessFeedback.Enabled);
        Assert.Equal(35, config.GameplayFfb.WetnessFeedback.StrengthPercent);
        Assert.True(config.GameplayFfb.MotionFeedback.Enabled);
        Assert.Equal(30, config.GameplayFfb.MotionFeedback.StrengthPercent);
        Assert.True(config.GameplayFfb.BumpFeedback.Enabled);
        Assert.Equal(55, config.GameplayFfb.BumpFeedback.StrengthPercent);
        Assert.Equal(80, config.GameplayFfb.BumpFeedback.DurationMs);
    }

    [Fact]
    public void Effect_settings_round_trip_through_json()
    {
        var config = new AppConfig();
        config.GameplayFfb.SpeedSpring.StrengthPercent = 42;
        config.GameplayFfb.MechanicalFriction.BaseFriction = 0.25;
        config.GameplayFfb.SlipFeedback.MinSlip = 0.2;
        config.GameplayFfb.BumpFeedback.DurationMs = 120;
        config.GameplayFfb.SurfaceFeedback.Enabled = false;

        var json = JsonSerializer.Serialize(config);
        var decoded = JsonSerializer.Deserialize<AppConfig>(json);

        Assert.NotNull(decoded);
        Assert.Equal(42, decoded!.GameplayFfb.SpeedSpring.StrengthPercent);
        Assert.Equal(0.25, decoded.GameplayFfb.MechanicalFriction.BaseFriction);
        Assert.Equal(0.2, decoded.GameplayFfb.SlipFeedback.MinSlip);
        Assert.Equal(120, decoded.GameplayFfb.BumpFeedback.DurationMs);
        Assert.False(decoded.GameplayFfb.SurfaceFeedback.Enabled);
    }
}
