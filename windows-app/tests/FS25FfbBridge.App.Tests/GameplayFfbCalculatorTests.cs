using FS25FfbBridge.App.Models;
using FS25FfbBridge.App.Services;

namespace FS25FfbBridge.App.Tests;

public sealed class GameplayFfbCalculatorTests
{
    private readonly GameplayFfbCalculator _calculator = new();

    [Fact]
    public void Standstill_produces_weak_spring_and_damper()
    {
        var output = _calculator.Calculate(State(Packet(speedKmh: 0)), new GameplayFfbSettings());

        Assert.InRange(output.SpringPercent, 0, 2);
        Assert.InRange(output.DamperPercent, 0, 3);
    }

    [Fact]
    public void Speed_increases_spring_and_damper()
    {
        var settings = new GameplayFfbSettings();
        var low = _calculator.Calculate(State(Packet(speedKmh: 5)), settings);
        var high = _calculator.Calculate(State(Packet(speedKmh: 30)), settings);

        Assert.True(high.SpringPercent > low.SpringPercent);
        Assert.True(high.DamperPercent > low.DamperPercent);
    }

    [Fact]
    public void Heavier_total_mass_increases_load_influence()
    {
        var settings = new GameplayFfbSettings();
        var light = _calculator.Calculate(State(Packet(speedKmh: 25, mass: 6000, totalMass: 6000)), settings);
        var heavy = _calculator.Calculate(State(Packet(speedKmh: 25, mass: 6000, totalMass: 12000)), settings);

        Assert.True(heavy.LoadFactor > light.LoadFactor);
        Assert.True(heavy.DamperPercent > light.DamperPercent);
    }

    [Fact]
    public void Engine_off_produces_zero_engine_vibration()
    {
        var packet = Packet(speedKmh: 20, rpm: 1500, engineStarted: false);
        var output = _calculator.Calculate(State(packet), new GameplayFfbSettings());

        Assert.Equal(0, output.EngineVibrationPercent);
        Assert.Equal(0, output.EngineVibrationHz);
    }

    [Fact]
    public void Field_surface_enables_surface_feedback_and_modifies_condition_effects()
    {
        var settings = new GameplayFfbSettings();
        var road = _calculator.Calculate(State(Packet(speedKmh: 25, isOnField: false)), settings);
        var field = _calculator.Calculate(State(Packet(speedKmh: 25, isOnField: true)), settings);

        Assert.True(field.SurfaceVibrationPercent > 0);
        Assert.True(field.SpringPercent < road.SpringPercent);
        Assert.True(field.DamperPercent > road.DamperPercent);
    }

    [Fact]
    public void Telemetry_loss_fades_and_stops_outputs()
    {
        var settings = new GameplayFfbSettings();
        var fresh = _calculator.Calculate(State(Packet(speedKmh: 30), TimeSpan.FromMilliseconds(50)), settings);
        var stale = _calculator.Calculate(State(Packet(speedKmh: 30), TimeSpan.FromMilliseconds(600)), settings);
        var lost = _calculator.Calculate(State(Packet(speedKmh: 30), TimeSpan.FromMilliseconds(1200)), settings);

        Assert.True(stale.SpringPercent < fresh.SpringPercent);
        Assert.False(lost.IsActive);
        Assert.Equal(0, lost.SpringPercent);
    }

    private static TelemetryReceiverState State(TelemetryPacket packet, TimeSpan? age = null)
    {
        return new TelemetryReceiverState(
            TelemetryStatus.Connected,
            packet,
            "{}",
            30,
            age ?? TimeSpan.FromMilliseconds(50),
            null,
            "test",
            "udp",
            "file",
            "test",
            null);
    }

    private static TelemetryPacket Packet(
        double speedKmh,
        double rpm = 900,
        bool engineStarted = true,
        double mass = 6000,
        double totalMass = 6000,
        bool isOnField = false)
    {
        return new TelemetryPacket
        {
            Timestamp = 1,
            GameState = "mission",
            IsPlayerInVehicle = true,
            VehicleName = "Tractor",
            VehicleType = "tractor",
            SpeedKmh = speedKmh,
            SteeringAngle = 0,
            Rpm = rpm,
            EngineStarted = engineStarted,
            Mass = mass,
            TotalMass = totalMass,
            IsOnField = isOnField
        };
    }
}
