namespace FieldForce.App.Models;

public sealed class EngineVibrationSettings : FfbEffectSettings
{
    public int MinRpm { get; set; } = 500;
    public int MaxRpm { get; set; } = 2400;
    public int MinFrequencyHz { get; set; } = 16;
    public int MaxFrequencyHz { get; set; } = 34;
    public int IdleStrengthPercent { get; set; } = 6;
    public int LoadStrengthPercent { get; set; } = 18;
    public int LuggingBoostPercent { get; set; } = 5;
}

public sealed class GearShiftPulseSettings : FfbEffectSettings
{
    public int DurationMs { get; set; } = 55;
    public int CooldownMs { get; set; } = 300;
}

public sealed class EngineStartStopPulseSettings : FfbEffectSettings
{
    public int StartDurationMs { get; set; } = 3000;
    public int StopDurationMs { get; set; } = 120;
    public int DurationMs
    {
        get => StopDurationMs;
        set => StopDurationMs = value;
    }

    public int StartFrequencyHz { get; set; } = 10;
    public int StopFrequencyHz { get; set; } = 12;
}

public sealed class DrivetrainPulseSettings : FfbEffectSettings
{
    public int DurationMs { get; set; } = 45;
    public int CooldownMs { get; set; } = 160;
}
