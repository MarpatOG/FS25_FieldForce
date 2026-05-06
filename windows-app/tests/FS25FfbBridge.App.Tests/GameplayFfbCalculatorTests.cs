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
    public void Speed_below_two_kmh_is_treated_as_standstill()
    {
        var settings = new GameplayFfbSettings();
        var stopped = _calculator.Calculate(State(Packet(speedKmh: 0)), settings);
        var creeping = _calculator.Calculate(State(Packet(speedKmh: 1.9)), settings);

        Assert.Equal(stopped.SpringPercent, creeping.SpringPercent);
        Assert.Equal(stopped.DamperPercent, creeping.DamperPercent);
    }

    [Fact]
    public void Valid_gameplay_packet_produces_nonzero_condition_effects()
    {
        var output = _calculator.Calculate(State(Packet(speedKmh: 22, mass: 6000, totalMass: 8500)), new GameplayFfbSettings());

        Assert.True(output.IsActive);
        Assert.True(output.SpringPercent > 0);
        Assert.True(output.DamperPercent > 0);
        Assert.True(output.FrictionPercent > 0);
    }

    [Fact]
    public void Vehicle_category_profile_applies_multipliers()
    {
        var settings = new GameplayFfbSettings();
        var wheeled = _calculator.Calculate(State(Packet(speedKmh: 25, vehicleCategory: VehicleCategoryFfbProfile.TractorWheeled)), settings);
        var tracked = _calculator.Calculate(State(Packet(speedKmh: 25, vehicleCategory: VehicleCategoryFfbProfile.TractorTracked)), settings);

        Assert.True(tracked.DamperPercent > wheeled.DamperPercent);
        Assert.True(tracked.FrictionPercent > wheeled.FrictionPercent);
        Assert.True(tracked.SpringPercent < wheeled.SpringPercent);
    }

    [Fact]
    public void Truck_category_uses_truck_effect_profile()
    {
        var settings = new GameplayFfbSettings();
        settings.VehicleCategoryEffectProfiles[VehicleCategoryFfbProfile.Truck].SpeedSpring.StrengthPercent = 20;
        settings.VehicleCategoryEffectProfiles[VehicleCategoryFfbProfile.TractorWheeled].SpeedSpring.StrengthPercent = 80;

        var truck = _calculator.Calculate(State(Packet(speedKmh: 25, vehicleCategory: VehicleCategoryFfbProfile.Truck)), settings);
        var tractor = _calculator.Calculate(State(Packet(speedKmh: 25, vehicleCategory: VehicleCategoryFfbProfile.TractorWheeled)), settings);

        Assert.Equal(VehicleCategoryFfbProfile.Truck, truck.ActiveCategory);
        Assert.True(truck.SpringPercent < tractor.SpringPercent);
    }

    [Fact]
    public void Different_category_effect_profiles_change_output()
    {
        var settings = new GameplayFfbSettings();
        settings.VehicleCategoryEffectProfiles[VehicleCategoryFfbProfile.TractorWheeled].SpeedDamper.StrengthPercent = 20;
        settings.VehicleCategoryEffectProfiles[VehicleCategoryFfbProfile.Harvester].SpeedDamper.StrengthPercent = 90;

        var tractor = _calculator.Calculate(State(Packet(speedKmh: 25, vehicleCategory: VehicleCategoryFfbProfile.TractorWheeled)), settings);
        var harvester = _calculator.Calculate(State(Packet(speedKmh: 25, vehicleCategory: VehicleCategoryFfbProfile.Harvester)), settings);

        Assert.True(harvester.DamperPercent > tractor.DamperPercent);
    }

    [Fact]
    public void Unknown_vehicle_category_uses_unknown_effect_profile()
    {
        var settings = new GameplayFfbSettings();
        settings.VehicleCategoryEffectProfiles[VehicleCategoryFfbProfile.TractorWheeled].SpeedSpring.StrengthPercent = 90;
        settings.VehicleCategoryEffectProfiles[VehicleCategoryFfbProfile.Unknown].SpeedSpring.StrengthPercent = 15;
        var missing = _calculator.Calculate(State(Packet(speedKmh: 25, vehicleCategory: null)), settings);
        var unknown = _calculator.Calculate(State(Packet(speedKmh: 25, vehicleCategory: VehicleCategoryFfbProfile.Unknown)), settings);
        var tractor = _calculator.Calculate(State(Packet(speedKmh: 25, vehicleCategory: VehicleCategoryFfbProfile.TractorWheeled)), settings);

        Assert.Equal(missing.SpringPercent, unknown.SpringPercent);
        Assert.True(unknown.SpringPercent < tractor.SpringPercent);
    }

    [Fact]
    public void Heavier_total_mass_increases_load_influence()
    {
        var settings = new GameplayFfbSettings();
        var light = _calculator.Calculate(State(Packet(speedKmh: 25, mass: 6000, totalMass: 6000)), settings);
        var heavy = _calculator.Calculate(State(Packet(speedKmh: 25, mass: 6000, totalMass: 12000)), settings);

        Assert.True(heavy.LoadFactor > light.LoadFactor);
        Assert.True(heavy.DamperPercent > light.DamperPercent);
        Assert.True(heavy.FrictionPercent > light.FrictionPercent);
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
        var field = _calculator.Calculate(State(Packet(speedKmh: 25, isOnField: true, surfaceType: "field")), settings);

        Assert.True(field.SurfaceVibrationPercent > 0);
        Assert.True(field.SpringPercent < road.SpringPercent);
        Assert.True(field.DamperPercent > road.DamperPercent);
        Assert.True(field.FrictionPercent > road.FrictionPercent);
    }

    [Fact]
    public void Field_surface_waits_for_minimum_speed()
    {
        var settings = new GameplayFfbSettings();
        var output = _calculator.Calculate(State(Packet(speedKmh: 0.1, isOnField: true)), settings);

        Assert.Equal(0, output.SurfaceVibrationPercent);
        Assert.Equal(0, output.SurfaceVibrationHz);
    }

    [Fact]
    public void Exact_asphalt_surface_does_not_enable_field_surface_feedback()
    {
        var settings = new GameplayFfbSettings();
        var output = _calculator.Calculate(State(Packet(speedKmh: 25, isOnField: false, surfaceType: "asphalt")), settings);

        Assert.Equal(0, output.SurfaceVibrationPercent);
        Assert.Equal(0, output.SurfaceVibrationHz);
    }

    [Fact]
    public void Exact_wet_field_surface_enables_field_surface_feedback_without_is_on_field()
    {
        var settings = new GameplayFfbSettings();
        var output = _calculator.Calculate(State(Packet(speedKmh: 25, isOnField: false, surfaceType: "wetField", groundWetness: 0.5)), settings);

        Assert.True(output.SurfaceVibrationPercent > 0);
    }

    [Fact]
    public void Unknown_surface_attribute_does_not_become_dirt_gravel_or_mud()
    {
        var settings = new GameplayFfbSettings();
        var output = _calculator.Calculate(State(Packet(speedKmh: 25, isOnField: false, surfaceType: "unknown", surfaceAttribute: 7)), settings);

        Assert.Equal(0, output.SurfaceVibrationPercent);
        Assert.Equal(0, output.SurfaceVibrationHz);
    }

    [Fact]
    public void Slip_feedback_uses_max_wheel_slip_threshold()
    {
        var settings = new GameplayFfbSettings();
        var low = _calculator.Calculate(State(Packet(speedKmh: 15, maxWheelSlip: 0.05)), settings);
        var high = _calculator.Calculate(State(Packet(speedKmh: 15, maxWheelSlip: 0.50)), settings);

        Assert.Equal(0, low.SlipVibrationPercent);
        Assert.True(high.SlipVibrationPercent > 0);
        Assert.True(high.SlipVibrationHz > 0);
    }

    [Fact]
    public void Wetness_increases_field_surface_feel_only_when_telemetry_is_present()
    {
        var settings = new GameplayFfbSettings();
        var noWetness = _calculator.Calculate(State(Packet(speedKmh: 25, isOnField: true, surfaceType: "field")), settings);
        var wet = _calculator.Calculate(State(Packet(speedKmh: 25, isOnField: true, surfaceType: "field", groundWetness: 0.75)), settings);

        Assert.True(wet.DamperPercent >= noWetness.DamperPercent);
        Assert.True(wet.SurfaceVibrationPercent >= noWetness.SurfaceVibrationPercent);
    }

    [Fact]
    public void Motion_feedback_modifies_condition_effects_but_stays_clamped()
    {
        var settings = new GameplayFfbSettings();
        var flat = _calculator.Calculate(State(Packet(speedKmh: 25)), settings);
        var motion = _calculator.Calculate(State(Packet(
            speedKmh: 25,
            rollDeg: 10,
            pitchDeg: 8,
            yawRateDegPerSec: 30,
            localAccelerationX: 3,
            localAccelerationY: 1,
            localAccelerationZ: 2)), settings);

        Assert.True(motion.SpringPercent >= flat.SpringPercent);
        Assert.True(motion.DamperPercent >= flat.DamperPercent);
        Assert.InRange(motion.SpringPercent, 0, 100);
        Assert.InRange(motion.DamperPercent, 0, 100);
    }

    [Fact]
    public void Bump_impulse_produces_signed_short_pulse_and_fades_with_stale_telemetry()
    {
        var settings = new GameplayFfbSettings();
        var fresh = _calculator.Calculate(State(Packet(speedKmh: 15, bumpImpulse: 0.8, localAccelerationX: -2)), settings);
        var lost = _calculator.Calculate(State(Packet(speedKmh: 15, bumpImpulse: 0.8, localAccelerationX: -2), TimeSpan.FromMilliseconds(1200)), settings);

        Assert.True(fresh.BumpImpulsePercent < 0);
        Assert.InRange(fresh.BumpDurationMs, 20, 250);
        Assert.False(lost.IsActive);
        Assert.Equal(0, lost.BumpImpulsePercent);
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
        bool isOnField = false,
        string? surfaceType = null,
        double? surfaceAttribute = null,
        double? groundWetness = null,
        double? rainScale = null,
        double? maxWheelSlip = null,
        double? pitchDeg = null,
        double? rollDeg = null,
        double? yawRateDegPerSec = null,
        double? localAccelerationX = null,
        double? localAccelerationY = null,
        double? localAccelerationZ = null,
        double? bumpImpulse = null,
        string? vehicleCategory = VehicleCategoryFfbProfile.TractorWheeled)
    {
        return new TelemetryPacket
        {
            Timestamp = 1,
            GameState = "mission",
            IsPlayerInVehicle = true,
            VehicleName = "Tractor",
            VehicleType = "tractor",
            VehicleCategory = vehicleCategory,
            SpeedKmh = speedKmh,
            SteeringAngle = 0,
            Rpm = rpm,
            EngineStarted = engineStarted,
            Mass = mass,
            TotalMass = totalMass,
            IsOnField = isOnField,
            SurfaceType = surfaceType,
            SurfaceAttribute = surfaceAttribute,
            GroundWetness = groundWetness,
            RainScale = rainScale,
            MaxWheelSlip = maxWheelSlip,
            PitchDeg = pitchDeg,
            RollDeg = rollDeg,
            YawRateDegPerSec = yawRateDegPerSec,
            LocalAccelerationX = localAccelerationX,
            LocalAccelerationY = localAccelerationY,
            LocalAccelerationZ = localAccelerationZ,
            BumpImpulse = bumpImpulse
        };
    }
}
