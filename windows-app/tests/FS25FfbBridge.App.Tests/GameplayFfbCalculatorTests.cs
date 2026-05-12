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

        Assert.InRange(output.SpringPercent, 12, 14);
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
    public void Speed_spring_matches_low_speed_centering_targets()
    {
        var settings = new GameplayFfbSettings();
        var stopped = new GameplayFfbCalculator().Calculate(State(Packet(speedKmh: 0)), settings);
        var creeping = new GameplayFfbCalculator().Calculate(State(Packet(speedKmh: 5)), settings);
        var light = new GameplayFfbCalculator().Calculate(State(Packet(speedKmh: 10)), settings);
        var moderate = new GameplayFfbCalculator().Calculate(State(Packet(speedKmh: 20)), settings);
        var stable = new GameplayFfbCalculator().Calculate(State(Packet(speedKmh: 35)), settings);
        var capped = new GameplayFfbCalculator().Calculate(State(Packet(speedKmh: 45)), settings);

        Assert.InRange(stopped.SpringPercent, 12, 14);
        Assert.InRange(creeping.SpringPercent, 27, 30);
        Assert.InRange(light.SpringPercent, 36, 39);
        Assert.InRange(moderate.SpringPercent, 50, 53);
        Assert.InRange(stable.SpringPercent, 59, 61);
        Assert.InRange(capped.SpringPercent, 59, 61);
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
    public void No_vehicle_v1_packet_produces_zero_ffb()
    {
        var output = _calculator.Calculate(State(NoVehiclePacket()), new GameplayFfbSettings());

        Assert.False(output.IsActive);
        Assert.Equal(0, output.SpringPercent);
        Assert.Equal(0, output.BumpImpulsePercent);
    }

    [Fact]
    public void Vehicle_category_profile_applies_multipliers()
    {
        var settings = new GameplayFfbSettings();
        var wheeled = new GameplayFfbCalculator().Calculate(State(Packet(speedKmh: 25, vehicleCategory: VehicleCategoryFfbProfile.TractorWheeled)), settings);
        var tracked = new GameplayFfbCalculator().Calculate(State(Packet(speedKmh: 25, vehicleCategory: VehicleCategoryFfbProfile.TractorTracked)), settings);

        Assert.True(tracked.DamperPercent > wheeled.DamperPercent);
        Assert.True(tracked.FrictionPercent > wheeled.FrictionPercent);
        Assert.Equal(wheeled.SpringPercent, tracked.SpringPercent);
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
    public void Heavy_tractor_categories_use_normal_tractor_effect_profiles()
    {
        var settings = new GameplayFfbSettings();
        settings.VehicleCategoryEffectProfiles[VehicleCategoryFfbProfile.TractorWheeled].SpeedSpring.StrengthPercent = 20;
        settings.VehicleCategoryEffectProfiles[VehicleCategoryFfbProfile.TractorTracked].SpeedSpring.StrengthPercent = 80;

        var heavyWheeled = _calculator.Calculate(State(Packet(speedKmh: 25, vehicleCategory: VehicleCategoryFfbProfile.HeavyTractorWheeled)), settings);
        var heavyTracked = _calculator.Calculate(State(Packet(speedKmh: 25, vehicleCategory: VehicleCategoryFfbProfile.HeavyTractorTracked)), settings);

        Assert.Equal(VehicleCategoryFfbProfile.TractorWheeled, heavyWheeled.ActiveCategory);
        Assert.Equal(VehicleCategoryFfbProfile.TractorTracked, heavyTracked.ActiveCategory);
        Assert.True(heavyTracked.SpringPercent > heavyWheeled.SpringPercent);
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
        var light = new GameplayFfbCalculator().Calculate(State(Packet(speedKmh: 25, mass: 6000, totalMass: 6000)), settings);
        var heavy = new GameplayFfbCalculator().Calculate(State(Packet(speedKmh: 25, mass: 6000, totalMass: 12000)), settings);

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
        var road = new GameplayFfbCalculator().Calculate(State(Packet(speedKmh: 25, isOnField: false)), settings);
        var field = new GameplayFfbCalculator().Calculate(State(Packet(speedKmh: 25, isOnField: true, surfaceType: "field")), settings);

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
    public void Feature_extractor_uses_fallback_contact_with_lower_confidence()
    {
        var aggregate = GameplayFfbCalculator.TelemetryFeatureExtractor.Extract(
            Packet(speedKmh: 10, groundContactRatio: 0.5),
            new GameplayFfbSettings());
        var steering = GameplayFfbCalculator.TelemetryFeatureExtractor.Extract(
            Packet(speedKmh: 10, groundContactRatio: 0.5, steeringGroundContactRatio: 0.25),
            new GameplayFfbSettings());

        Assert.Equal(0.5, aggregate.ContactRatio);
        Assert.InRange(aggregate.ContactConfidence, 0.5, 0.6);
        Assert.Equal(0.25, steering.ContactRatio);
        Assert.Equal(1.0, steering.ContactConfidence);
    }

    [Fact]
    public void Speed_stability_adds_damping_without_changing_base_centering()
    {
        var context = new FfbFrameContext(
            TimeSpan.FromMilliseconds(8),
            TimeSpan.FromMilliseconds(50),
            1,
            VehicleCategoryFfbProfile.TractorWheeled,
            DeviceHapticProfile.Generic);
        var features = new TelemetryFeatures(40, 0.8, 0, 1.0, 0, 0, 1, 1, "road", 1, null, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, false, 0, 0);
        var baseSteering = new SteeringModel(25, 10, 8);

        var stability = GameplayFfbCalculator.SpeedStabilityLayer.Calculate(features, new GameplayFfbSettings(), context);
        var stabilized = GameplayFfbCalculator.SteeringContributionMixer.Combine(
            new LayerContribution<SteeringContribution>(
                new SteeringContribution("base", baseSteering.Spring, baseSteering.Damper, baseSteering.Friction, 0, 0, 1),
                1),
            stability);

        Assert.Equal(baseSteering.Spring, stabilized.Spring);
        Assert.True(stabilized.Damper > baseSteering.Damper);
        Assert.Equal(baseSteering.Friction, stabilized.Friction);
    }

    [Fact]
    public void Steering_load_speed_scale_reaches_full_effect_at_ten_kmh_and_caps_above_forty()
    {
        Assert.Equal(0, GameplayFfbCalculator.CalculateSteeringLoadSpeedScale(0));
        Assert.Equal(0.5, GameplayFfbCalculator.CalculateSteeringLoadSpeedScale(5));
        Assert.Equal(1, GameplayFfbCalculator.CalculateSteeringLoadSpeedScale(10));
        Assert.Equal(1, GameplayFfbCalculator.CalculateSteeringLoadSpeedScale(40));
        Assert.Equal(1, GameplayFfbCalculator.CalculateSteeringLoadSpeedScale(80));
    }

    [Fact]
    public void Speed_stability_reaches_full_effect_at_ten_kmh()
    {
        var context = new FfbFrameContext(
            TimeSpan.FromMilliseconds(8),
            TimeSpan.FromMilliseconds(50),
            1,
            VehicleCategoryFfbProfile.TractorWheeled,
            DeviceHapticProfile.Generic);
        var settings = new GameplayFfbSettings();
        var slow = GameplayFfbCalculator.SpeedStabilityLayer.Calculate(
            new TelemetryFeatures(5, 0, 0, 1.0, 0, 0, 1, 1, "road", 1, null, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, false, 0, 0),
            settings,
            context).Value;
        var full = GameplayFfbCalculator.SpeedStabilityLayer.Calculate(
            new TelemetryFeatures(10, 0, 0, 1.0, 0, 0, 1, 1, "road", 1, null, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, false, 0, 0),
            settings,
            context).Value;
        var capped = GameplayFfbCalculator.SpeedStabilityLayer.Calculate(
            new TelemetryFeatures(40, 0, 0, 1.0, 0, 0, 1, 1, "road", 1, null, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, false, 0, 0),
            settings,
            context).Value;

        Assert.True(slow.DamperAdd < full.DamperAdd);
        Assert.Equal(full.DamperAdd, capped.DamperAdd);
    }

    [Fact]
    public void Speed_stability_respects_road_damping_enabled_setting()
    {
        var settings = new GameplayFfbSettings();
        settings.SpeedDamper.Enabled = false;
        var context = new FfbFrameContext(
            TimeSpan.FromMilliseconds(8),
            TimeSpan.FromMilliseconds(50),
            1,
            VehicleCategoryFfbProfile.TractorWheeled,
            DeviceHapticProfile.Generic);
        var features = new TelemetryFeatures(40, 0.8, 0, 1.0, 0, 0, 1, 1, "road", 1, null, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, false, 0, 0);

        var stabilized = GameplayFfbCalculator.SpeedStabilityLayer.Calculate(features, settings, context);

        Assert.Equal(0, stabilized.Confidence);
        Assert.Equal(0, stabilized.Value.DamperAdd);
    }

    [Fact]
    public void Road_damping_disabled_keeps_speed_stability_lamps_off()
    {
        var settings = new GameplayFfbSettings();
        foreach (var profile in settings.VehicleCategoryEffectProfiles.Values)
        {
            profile.SpeedDamper.Enabled = false;
            profile.SpeedSpring.Enabled = false;
            profile.MechanicalFriction.Enabled = false;
            profile.EngineVibration.Enabled = false;
        }

        var output = _calculator.Calculate(State(Packet(speedKmh: 40, steeringAngle: 0, steeringRate: 1.0, groundContactRatio: 1)), settings);

        Assert.Equal(0, output.DamperPercent);
        Assert.False(output.AntiOscillationActive);
    }

    [Fact]
    public void Surface_and_load_layers_return_isolated_steering_contributions()
    {
        var profile = new GameplayFfbSettings();
        var context = new FfbFrameContext(TimeSpan.FromMilliseconds(8), TimeSpan.FromMilliseconds(50), 1, VehicleCategoryFfbProfile.TractorWheeled, DeviceHapticProfile.Generic);
        var features = GameplayFfbCalculator.TelemetryFeatureExtractor.Extract(
            Packet(speedKmh: 20, isOnField: true, surfaceType: "field", mass: 6000, totalMass: 12000),
            profile);

        var surface = GameplayFfbCalculator.SurfaceSteeringLayer.Calculate(features, profile, context).Value;
        var load = GameplayFfbCalculator.LoadResistanceLayer.Calculate(features, profile, context).Value;

        Assert.True(surface.DamperAdd > 0 || surface.FrictionAdd > 0 || surface.SpringAdd != 0);
        Assert.True(load.DamperAdd > 0 || load.FrictionAdd > 0 || load.SpringAdd > 0);

        Assert.Equal(0, surface.SpringRelief);
        Assert.Equal(0, load.SpringRelief);
    }

    [Fact]
    public void Steering_load_reaches_full_effect_at_ten_kmh()
    {
        var profile = new GameplayFfbSettings();
        profile.SpeedDamper.StandstillFloor = 1;
        var context = new FfbFrameContext(TimeSpan.FromMilliseconds(8), TimeSpan.FromMilliseconds(50), 1, VehicleCategoryFfbProfile.TractorWheeled, DeviceHapticProfile.Generic);
        var slow = GameplayFfbCalculator.LoadResistanceLayer.Calculate(
            GameplayFfbCalculator.TelemetryFeatureExtractor.Extract(Packet(speedKmh: 5, mass: 6000, totalMass: 12000), profile),
            profile,
            context).Value;
        var full = GameplayFfbCalculator.LoadResistanceLayer.Calculate(
            GameplayFfbCalculator.TelemetryFeatureExtractor.Extract(Packet(speedKmh: 10, mass: 6000, totalMass: 12000), profile),
            profile,
            context).Value;
        var capped = GameplayFfbCalculator.LoadResistanceLayer.Calculate(
            GameplayFfbCalculator.TelemetryFeatureExtractor.Extract(Packet(speedKmh: 40, mass: 6000, totalMass: 12000), profile),
            profile,
            context).Value;

        Assert.True(slow.DamperAdd < full.DamperAdd);
        Assert.Equal(full.DamperAdd, capped.DamperAdd);
    }

    [Fact]
    public void Continuous_haptics_and_event_pulses_stay_separate()
    {
        var output = _calculator.Calculate(State(Packet(speedKmh: 15, maxWheelSlip: 0.5, bumpImpulse: 0.8)), new GameplayFfbSettings(), DeviceHapticProfile.Generic);

        Assert.True(output.SlipVibrationPercent > 0);
        Assert.NotEqual(0, output.BumpImpulsePercent);
        Assert.True(output.EventPulseActive);
    }

    [Fact]
    public void Device_caps_limit_momo_haptics_more_than_generic()
    {
        var settings = new GameplayFfbSettings();
        var packet = Packet(speedKmh: 25, isOnField: true, surfaceType: "field", maxWheelSlip: 0.8, rpm: 2200, bumpImpulse: 1.5);
        var generic = _calculator.Calculate(State(packet), settings, DeviceHapticProfile.Generic);
        var momo = _calculator.Calculate(State(packet), settings, DeviceHapticProfile.LogitechMomo);

        Assert.True(momo.EngineVibrationPercent <= generic.EngineVibrationPercent);
        Assert.True(momo.SurfaceVibrationPercent <= generic.SurfaceVibrationPercent);
        Assert.True(momo.SlipVibrationPercent <= generic.SlipVibrationPercent);
        Assert.True(Math.Abs(momo.BumpImpulsePercent) >= Math.Abs(generic.BumpImpulsePercent));
        Assert.True(momo.BumpDurationMs >= generic.BumpDurationMs);
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
    public void Wetness_below_minimum_does_not_change_field_output()
    {
        var settings = new GameplayFfbSettings();
        settings.WetnessFeedback.MinWetness = 0.2;
        var dry = _calculator.Calculate(State(Packet(speedKmh: 25, isOnField: true, surfaceType: "field")), settings);
        var barelyWet = _calculator.Calculate(State(Packet(speedKmh: 25, isOnField: true, surfaceType: "field", groundWetness: 0.1, rainScale: 0.05)), settings);

        Assert.Equal(dry.DamperPercent, barelyWet.DamperPercent);
        Assert.Equal(dry.SurfaceVibrationPercent, barelyWet.SurfaceVibrationPercent);
    }

    [Fact]
    public void Rain_scale_can_drive_wetness_feedback()
    {
        var settings = new GameplayFfbSettings();
        var dry = _calculator.Calculate(State(Packet(speedKmh: 25, isOnField: true, surfaceType: "field")), settings);
        var raining = _calculator.Calculate(State(Packet(speedKmh: 25, isOnField: true, surfaceType: "field", rainScale: 0.85)), settings);

        Assert.True(raining.DamperPercent >= dry.DamperPercent);
        Assert.True(raining.SurfaceVibrationPercent >= dry.SurfaceVibrationPercent);
    }

    [Fact]
    public void Wet_field_without_numeric_wetness_uses_fallback()
    {
        var settings = new GameplayFfbSettings();
        var field = _calculator.Calculate(State(Packet(speedKmh: 25, isOnField: true, surfaceType: "field")), settings);
        var wetField = _calculator.Calculate(State(Packet(speedKmh: 25, isOnField: false, surfaceType: "wetField")), settings);

        Assert.True(wetField.SurfaceVibrationPercent >= field.SurfaceVibrationPercent);
        Assert.True(wetField.DamperPercent >= field.DamperPercent);
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
    public void Motion_feedback_disabled_matches_flat_packet()
    {
        var settings = new GameplayFfbSettings();
        settings.MotionFeedback.Enabled = false;

        var flat = _calculator.Calculate(State(Packet(speedKmh: 25)), settings);
        var motion = _calculator.Calculate(State(Packet(speedKmh: 25, pitchDeg: 8, yawRateDegPerSec: 30)), settings);

        Assert.Equal(flat.SpringPercent, motion.SpringPercent);
        Assert.Equal(flat.DamperPercent, motion.DamperPercent);
    }

    [Fact]
    public void Motion_feedback_zero_strength_matches_flat_packet()
    {
        var settings = new GameplayFfbSettings();
        settings.MotionFeedback.StrengthPercent = 0;

        var flat = _calculator.Calculate(State(Packet(speedKmh: 25)), settings);
        var motion = _calculator.Calculate(State(Packet(speedKmh: 25, pitchDeg: 8, yawRateDegPerSec: 30)), settings);

        Assert.Equal(flat.SpringPercent, motion.SpringPercent);
        Assert.Equal(flat.DamperPercent, motion.DamperPercent);
    }

    [Fact]
    public void Terrain_rumble_is_exposed_as_output_channel()
    {
        var output = _calculator.Calculate(State(Packet(speedKmh: 15, isOnField: true, surfaceType: "field", bumpImpulse: 0.6)), new GameplayFfbSettings(), DeviceHapticProfile.Generic);

        Assert.True(output.TerrainRumblePercent > 0);
        Assert.True(output.TerrainRumbleHz > 0);
        Assert.True(output.TerrainRumbleActive);
        Assert.Contains("Terrain", output.ActiveEffectsText);
    }

    [Fact]
    public void Asphalt_small_and_medium_suspension_impulses_do_not_create_terrain_rumble()
    {
        var settings = new GameplayFfbSettings();
        var small = _calculator.Calculate(State(Packet(speedKmh: 18, surfaceType: "asphalt", suspensionImpulse: 0.16)), settings, DeviceHapticProfile.Generic);
        var medium = _calculator.Calculate(State(Packet(speedKmh: 18, surfaceType: "asphalt", suspensionImpulse: 0.32)), settings, DeviceHapticProfile.Generic);

        Assert.Equal(0, small.TerrainRumblePercent);
        Assert.Equal(0, medium.TerrainRumblePercent);
    }

    [Fact]
    public void Asphalt_v1_micro_vertical_impulse_does_not_create_road_haptics()
    {
        var output = _calculator.Calculate(
            State(Packet(speedKmh: 18, surfaceType: "asphalt", verticalImpactImpulse: 0.48, suspensionImpulse: 0.48, groundContactRatio: 1)),
            new GameplayFfbSettings(),
            DeviceHapticProfile.Generic);

        Assert.False(output.EventPulseActive);
        Assert.NotEqual(FfbPulseKind.Bump, output.EventPulseKind);
        Assert.NotEqual(FfbPulseKind.LeftSuspensionHit, output.EventPulseKind);
        Assert.NotEqual(FfbPulseKind.RightSuspensionHit, output.EventPulseKind);
        Assert.InRange(output.TerrainRumblePercent, 0, 1);
    }

    [Fact]
    public void Asphalt_side_suspension_impulse_can_emit_when_delta_is_clear()
    {
        var output = _calculator.Calculate(
            State(Packet(speedKmh: 18, surfaceType: "asphalt", verticalImpactImpulse: 0.35, leftSuspensionImpulse: 0.8, rightSuspensionImpulse: 0.15, groundContactRatio: 1)),
            new GameplayFfbSettings(),
            DeviceHapticProfile.Generic);

        Assert.Equal(FfbPulseKind.LeftSuspensionHit, output.EventPulseKind);
        Assert.True(Math.Abs(output.BumpImpulsePercent) >= 4);
    }

    [Fact]
    public void Captured_asphalt_side_hits_emit_for_logged_unimog_element()
    {
        var settings = new GameplayFfbSettings();
        var left = _calculator.Calculate(
            State(Packet(speedKmh: 19.94, surfaceType: "asphalt", verticalImpactImpulse: 1.08, leftSuspensionImpulse: 0.542, rightSuspensionImpulse: null, groundContactRatio: 1)),
            settings,
            DeviceHapticProfile.Generic);
        var right = _calculator.Calculate(
            State(Packet(speedKmh: 21.40, surfaceType: "asphalt", verticalImpactImpulse: 0.533, leftSuspensionImpulse: 0.267, rightSuspensionImpulse: 0.533, groundContactRatio: 1)),
            settings,
            DeviceHapticProfile.Generic);

        Assert.Equal(FfbPulseKind.LeftSuspensionHit, left.EventPulseKind);
        Assert.Equal(FfbPulseKind.RightSuspensionHit, right.EventPulseKind);
        Assert.True(Math.Abs(left.BumpImpulsePercent) >= 4);
        Assert.True(Math.Abs(right.BumpImpulsePercent) >= 3);
    }

    [Theory]
    [InlineData("field")]
    [InlineData("wetField")]
    [InlineData("dirt")]
    [InlineData("gravel")]
    public void Offroad_surface_gives_stronger_suspension_output_than_asphalt(string surfaceType)
    {
        var settings = new GameplayFfbSettings();
        var asphalt = _calculator.Calculate(State(Packet(speedKmh: 18, surfaceType: "asphalt", suspensionImpulse: 0.5, verticalImpactImpulse: 0.55, leftSuspensionImpulse: 0.55, rightSuspensionImpulse: 0.25, groundContactRatio: 1)), settings, DeviceHapticProfile.Generic);
        var offroad = _calculator.Calculate(State(Packet(speedKmh: 18, isOnField: surfaceType == "field", surfaceType: surfaceType, suspensionImpulse: 0.5, verticalImpactImpulse: 0.55, leftSuspensionImpulse: 0.55, rightSuspensionImpulse: 0.25, groundContactRatio: 1)), settings, DeviceHapticProfile.Generic);

        Assert.True(offroad.TerrainRumblePercent > asphalt.TerrainRumblePercent);
        Assert.True(Math.Abs(offroad.BumpImpulsePercent) > Math.Abs(asphalt.BumpImpulsePercent));
    }

    [Fact]
    public void Extra_total_mass_amplifies_offroad_suspension_haptics_softly()
    {
        var settings = new GameplayFfbSettings();
        var empty = _calculator.Calculate(State(Packet(speedKmh: 18, isOnField: true, surfaceType: "field", mass: 6000, totalMass: 6000, suspensionImpulse: 0.42, verticalImpactImpulse: 0.52, groundContactRatio: 1)), settings, DeviceHapticProfile.Generic);
        var loaded = _calculator.Calculate(State(Packet(speedKmh: 18, isOnField: true, surfaceType: "field", mass: 6000, totalMass: 12000, suspensionImpulse: 0.42, verticalImpactImpulse: 0.52, groundContactRatio: 1)), settings, DeviceHapticProfile.Generic);

        Assert.True(loaded.TerrainRumblePercent > empty.TerrainRumblePercent);
        Assert.True(Math.Abs(loaded.BumpImpulsePercent) > Math.Abs(empty.BumpImpulsePercent));
        Assert.True(loaded.TerrainRumblePercent <= empty.TerrainRumblePercent * 1.5);
    }

    [Fact]
    public void Unknown_surface_without_field_flag_uses_mixed_suspension_haptics()
    {
        var settings = new GameplayFfbSettings();
        var road = _calculator.Calculate(State(Packet(speedKmh: 18, isOnField: false, surfaceType: "asphalt", suspensionImpulse: 0.32, verticalImpactImpulse: 0.35, groundContactRatio: 1)), settings, DeviceHapticProfile.Generic);
        var unknown = _calculator.Calculate(State(Packet(speedKmh: 18, isOnField: false, surfaceType: "unknown", suspensionImpulse: 0.32, verticalImpactImpulse: 0.35, groundContactRatio: 1)), settings, DeviceHapticProfile.Generic);
        var field = _calculator.Calculate(State(Packet(speedKmh: 18, isOnField: true, surfaceType: "unknown", suspensionImpulse: 0.32, verticalImpactImpulse: 0.35, groundContactRatio: 1)), settings, DeviceHapticProfile.Generic);

        Assert.True(unknown.TerrainRumblePercent > road.TerrainRumblePercent);
        Assert.True(field.TerrainRumblePercent > unknown.TerrainRumblePercent);
    }

    [Fact]
    public void Drivetrain_pulse_fires_on_gear_change_but_not_steady_state()
    {
        var settings = new GameplayFfbSettings();
        var first = _calculator.Calculate(State(Packet(speedKmh: 8, throttle: 0.4, gear: 2)), settings);
        var shifted = _calculator.Calculate(State(Packet(speedKmh: 8, throttle: 0.4, gear: 3)), settings);
        var repeated = _calculator.Calculate(State(Packet(speedKmh: 8, throttle: 0.4, gear: 3)), settings);

        Assert.False(first.EventPulseActive);
        Assert.True(shifted.EventPulseActive);
        Assert.Equal(FfbPulseKind.GearShift, shifted.EventPulseKind);
        Assert.False(repeated.EventPulseActive);
    }

    [Fact]
    public void Drivetrain_pulse_fires_on_large_throttle_delta()
    {
        var settings = new GameplayFfbSettings();
        _calculator.Calculate(State(Packet(speedKmh: 8, throttle: 0.1, gear: 2)), settings);
        var output = _calculator.Calculate(State(Packet(speedKmh: 8, throttle: 0.8, gear: 2)), settings);

        Assert.True(output.EventPulseActive);
        Assert.NotEqual(0, output.BumpImpulsePercent);
        Assert.Equal(FfbPulseKind.DrivetrainJerk, output.EventPulseKind);
    }

    [Fact]
    public void Longitudinal_jerk_produces_drivetrain_pulse_not_bump()
    {
        var settings = new GameplayFfbSettings();
        _calculator.Calculate(State(Packet(speedKmh: 8, throttle: 0.4, gear: 2, longitudinalJerkImpulse: 0.1)), settings);
        var output = _calculator.Calculate(State(Packet(speedKmh: 8, throttle: 0.4, gear: 2, longitudinalJerkImpulse: 0.8, verticalImpactImpulse: 0.05)), settings);

        Assert.True(output.EventPulseActive);
        Assert.Equal(FfbPulseKind.DrivetrainJerk, output.EventPulseKind);
    }

    [Fact]
    public void Vertical_road_impact_produces_bump()
    {
        var output = _calculator.Calculate(State(Packet(speedKmh: 15, verticalImpactImpulse: 0.7, groundContactRatio: 1)), new GameplayFfbSettings());

        Assert.True(output.EventPulseActive);
        Assert.Equal(FfbPulseKind.Bump, output.EventPulseKind);
    }

    [Fact]
    public void Feature_extractor_uses_max_valid_impulse_fallback()
    {
        var features = GameplayFfbCalculator.TelemetryFeatureExtractor.Extract(
            Packet(speedKmh: 15, suspensionImpulse: 0.02, verticalImpactImpulse: 0.70, bumpImpulse: 0.65),
            new GameplayFfbSettings());

        Assert.Equal(0.70, features.SuspensionImpulse);
        Assert.Equal(0.70, features.VerticalImpactImpulse);
    }

    [Fact]
    public void Collision_noise_does_not_suppress_bump()
    {
        var output = _calculator.Calculate(State(Packet(speedKmh: 15, verticalImpactImpulse: 0.7, collisionImpulse: 0.03, groundContactRatio: 1)), new GameplayFfbSettings());

        Assert.True(output.EventPulseActive);
        Assert.Equal(FfbPulseKind.Bump, output.EventPulseKind);
    }

    [Fact]
    public void Landing_noise_does_not_suppress_bump()
    {
        var output = _calculator.Calculate(State(Packet(speedKmh: 15, verticalImpactImpulse: 0.7, landingImpulse: 0.04, groundContactRatio: 1)), new GameplayFfbSettings());

        Assert.True(output.EventPulseActive);
        Assert.Equal(FfbPulseKind.Bump, output.EventPulseKind);
    }

    [Fact]
    public void Strong_collision_has_priority_over_bump()
    {
        var settings = new GameplayFfbSettings();
        foreach (var profile in settings.VehicleCategoryEffectProfiles.Values)
        {
            profile.CollisionFeedback.StrengthPercent = 100;
            profile.CollisionFeedback.MaxOutputPercent = 100;
            profile.CollisionFeedback.FullImpulse = 2.0;
        }

        var output = _calculator.Calculate(State(Packet(speedKmh: 15, verticalImpactImpulse: 0.5, collisionImpulse: 2.0, groundContactRatio: 1)), settings);

        Assert.Equal(FfbPulseKind.Collision, output.EventPulseKind);
    }

    [Fact]
    public void Steering_motion_on_full_contact_does_not_create_collision_pulse()
    {
        var output = _calculator.Calculate(State(Packet(
            speedKmh: 25,
            surfaceType: "grass",
            steeringAngle: 0.16,
            yawRateDegPerSec: 9,
            groundContactRatio: 1,
            collisionImpulse: 2.0,
            verticalImpactImpulse: 0,
            leftSuspensionImpulse: 0,
            rightSuspensionImpulse: 0)), new GameplayFfbSettings());

        Assert.NotEqual(FfbPulseKind.Collision, output.EventPulseKind);
    }

    [Fact]
    public void Road_bump_passes_for_noticeable_impact()
    {
        var output = _calculator.Calculate(State(Packet(speedKmh: 18, surfaceType: "asphalt", verticalImpactImpulse: 0.75, groundContactRatio: 1)), new GameplayFfbSettings(), DeviceHapticProfile.Generic);

        Assert.Equal(FfbPulseKind.Bump, output.EventPulseKind);
        Assert.True(Math.Abs(output.BumpImpulsePercent) >= 4);
    }

    [Fact]
    public void Side_impulse_dominance_selects_left_or_right_suspension_hit()
    {
        var left = _calculator.Calculate(State(Packet(speedKmh: 15, verticalImpactImpulse: 0.3, leftSuspensionImpulse: 0.8, rightSuspensionImpulse: 0.2, groundContactRatio: 1)), new GameplayFfbSettings());
        var right = _calculator.Calculate(State(Packet(speedKmh: 15, verticalImpactImpulse: 0.3, leftSuspensionImpulse: 0.2, rightSuspensionImpulse: 0.8, groundContactRatio: 1)), new GameplayFfbSettings());

        Assert.Equal(FfbPulseKind.LeftSuspensionHit, left.EventPulseKind);
        Assert.Equal(FfbPulseKind.RightSuspensionHit, right.EventPulseKind);
    }

    [Fact]
    public void Articulated_vehicle_side_impulse_does_not_select_suspension_hit()
    {
        var output = _calculator.Calculate(State(Packet(
            speedKmh: 15,
            verticalImpactImpulse: 0.3,
            leftSuspensionImpulse: 0.9,
            rightSuspensionImpulse: 0.1,
            groundContactRatio: 1,
            isArticulated: true)), new GameplayFfbSettings());

        Assert.NotEqual(FfbPulseKind.LeftSuspensionHit, output.EventPulseKind);
        Assert.NotEqual(FfbPulseKind.RightSuspensionHit, output.EventPulseKind);
    }

    [Fact]
    public void Side_impulse_absolute_delta_selects_suspension_hit()
    {
        var output = _calculator.Calculate(State(Packet(speedKmh: 15, surfaceType: "asphalt", verticalImpactImpulse: 0.35, leftSuspensionImpulse: 0.48, rightSuspensionImpulse: 0.30, groundContactRatio: 1)), new GameplayFfbSettings());

        Assert.NotEqual(FfbPulseKind.LeftSuspensionHit, output.EventPulseKind);
        Assert.NotEqual(FfbPulseKind.RightSuspensionHit, output.EventPulseKind);
    }

    [Fact]
    public void Symmetric_side_impulse_falls_back_to_bump()
    {
        var output = _calculator.Calculate(State(Packet(speedKmh: 15, surfaceType: "asphalt", verticalImpactImpulse: 0.75, leftSuspensionImpulse: 0.42, rightSuspensionImpulse: 0.39, groundContactRatio: 1)), new GameplayFfbSettings());

        Assert.Equal(FfbPulseKind.Bump, output.EventPulseKind);
    }

    [Fact]
    public void Unknown_contact_does_not_suppress_bump()
    {
        var output = _calculator.Calculate(State(Packet(speedKmh: 15, verticalImpactImpulse: 0.7)), new GameplayFfbSettings());

        Assert.Equal(FfbPulseKind.Bump, output.EventPulseKind);
    }

    [Fact]
    public void Known_low_contact_suppresses_bump()
    {
        var output = _calculator.Calculate(State(Packet(speedKmh: 15, verticalImpactImpulse: 0.7, groundContactRatio: 0.1)), new GameplayFfbSettings());

        Assert.NotEqual(FfbPulseKind.Bump, output.EventPulseKind);
    }

    [Fact]
    public void Landing_and_collision_have_priority_over_normal_bump()
    {
        var settings = new GameplayFfbSettings();
        foreach (var profile in settings.VehicleCategoryEffectProfiles.Values)
        {
            profile.CollisionFeedback.StrengthPercent = 100;
            profile.CollisionFeedback.MaxOutputPercent = 100;
            profile.CollisionFeedback.FullImpulse = 2.0;
        }
        var landing = _calculator.Calculate(State(Packet(speedKmh: 15, verticalImpactImpulse: 0.8, landingImpulse: 0.8, groundContactRatio: 1)), settings);
        var collision = _calculator.Calculate(State(Packet(speedKmh: 15, verticalImpactImpulse: 0.1, collisionImpulse: 2.0, groundContactRatio: 1)), settings);

        Assert.Equal(FfbPulseKind.Landing, landing.EventPulseKind);
        Assert.Equal(FfbPulseKind.Collision, collision.EventPulseKind);
    }

    [Fact]
    public void Road_horizontal_jerk_does_not_create_collision_pulse()
    {
        var output = _calculator.Calculate(State(Packet(speedKmh: 45, surfaceType: "asphalt", collisionImpulse: 0.55, longitudinalJerkImpulse: 0.55, verticalImpactImpulse: 0.05, groundContactRatio: 1)), new GameplayFfbSettings());

        Assert.False(output.EventPulseActive);
    }

    [Fact]
    public void Offroad_moderate_horizontal_jerk_does_not_create_collision_pulse()
    {
        var output = _calculator.Calculate(State(Packet(speedKmh: 18, isOnField: true, surfaceType: "field", collisionImpulse: 0.65, longitudinalJerkImpulse: 0.65, verticalImpactImpulse: 0.05, groundContactRatio: 1)), new GameplayFfbSettings());

        Assert.False(output.EventPulseActive);
    }

    [Fact]
    public void Offroad_full_contact_bump_spike_does_not_create_collision_pulse()
    {
        var output = _calculator.Calculate(State(Packet(
            speedKmh: 42,
            isOnField: true,
            surfaceType: "field",
            collisionImpulse: 2.0,
            verticalImpactImpulse: 0.45,
            leftSuspensionImpulse: 0.42,
            rightSuspensionImpulse: 0.36,
            groundContactRatio: 1)), new GameplayFfbSettings());

        Assert.NotEqual(FfbPulseKind.Collision, output.EventPulseKind);
    }

    [Fact]
    public void Terrain_rumble_does_not_create_event_pulse_by_itself()
    {
        var output = _calculator.Calculate(State(Packet(speedKmh: 15, isOnField: true, surfaceType: "field", suspensionImpulse: 0.20, verticalImpactImpulse: 0.05, groundContactRatio: 1)), new GameplayFfbSettings());

        Assert.True(output.TerrainRumblePercent > 0);
        Assert.False(output.EventPulseActive);
    }

    [Fact]
    public void Device_limit_is_independent_cap_on_directinput_magnitude()
    {
        var method = typeof(DirectInputFfbBackend).GetMethod("ScaleMagnitudeForLimits", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(method);

        var globalLimited = (int)method!.Invoke(null, [10_000, 40, 80])!;
        var deviceLimited = (int)method.Invoke(null, [10_000, 80, 35])!;

        Assert.Equal(4000, globalLimited);
        Assert.Equal(3500, deviceLimited);
    }

    [Fact]
    public void Finite_pulse_floor_survives_force_limits_without_exceeding_them()
    {
        var method = typeof(DirectInputFfbBackend).GetMethod("ScaleMagnitudeForLimitsWithFloor", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(method);

        var raised = (int)method!.Invoke(null, [600, 70, 100, 5000])!;
        var cappedByLimit = (int)method.Invoke(null, [600, 5, 100, 5000])!;

        Assert.Equal(5000, raised);
        Assert.Equal(500, cappedByLimit);
    }

    [Fact]
    public void Drivetrain_pulses_do_not_use_finite_impact_floor()
    {
        var method = typeof(DirectInputFfbBackend).GetMethod("CalculateMinimumGameplayPulseMagnitude", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(method);

        Assert.Equal(0, (int)method!.Invoke(null, [FfbPulseKind.DrivetrainJerk])!);
        Assert.Equal(0, (int)method.Invoke(null, [FfbPulseKind.GearShift])!);
        Assert.Equal(0, (int)method.Invoke(null, [FfbPulseKind.EngineStartStop])!);
        Assert.True((int)method.Invoke(null, [FfbPulseKind.Bump])! > 0);
        Assert.True((int)method.Invoke(null, [FfbPulseKind.LeftSuspensionHit])! > 0);
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
    public void Bump_and_side_hit_cooldowns_are_limited_for_terrain_pulses()
    {
        var bump = _calculator.Calculate(State(Packet(speedKmh: 15, surfaceType: "asphalt", verticalImpactImpulse: 0.8, groundContactRatio: 1)), new GameplayFfbSettings());
        var side = _calculator.Calculate(State(Packet(speedKmh: 15, surfaceType: "asphalt", verticalImpactImpulse: 0.35, leftSuspensionImpulse: 0.8, rightSuspensionImpulse: 0.15, groundContactRatio: 1)), new GameplayFfbSettings());

        Assert.Equal(FfbPulseKind.Bump, bump.EventPulseKind);
        Assert.InRange(bump.BumpCooldownMs, 20, 105);
        Assert.Equal(FfbPulseKind.LeftSuspensionHit, side.EventPulseKind);
        Assert.InRange(side.BumpCooldownMs, 20, 85);
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

    private static TelemetryReceiverState State(TelemetryPacketV1 packet, TimeSpan? age = null)
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

    private static TelemetryPacketV1 Packet(
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
        double? groundContactRatio = null,
        double? steeringGroundContactRatio = null,
        double? steeringAngle = null,
        double? steeringRate = null,
        double? pitchDeg = null,
        double? rollDeg = null,
        double? yawRateDegPerSec = null,
        double? localAccelerationX = null,
        double? localAccelerationY = null,
        double? localAccelerationZ = null,
        double? bumpImpulse = null,
        double? suspensionImpulse = null,
        double? verticalImpactImpulse = null,
        double? landingImpulse = null,
        double? collisionImpulse = null,
        double? longitudinalJerkImpulse = null,
        double? leftSuspensionImpulse = null,
        double? rightSuspensionImpulse = null,
        double? throttle = null,
        double? brake = null,
        double? clutch = null,
        int? gear = null,
        bool? isArticulated = null,
        string? vehicleCategory = VehicleCategoryFfbProfile.TractorWheeled)
    {
        return new TelemetryPacketV1
        {
            Protocol = new TelemetryProtocolV1 { Name = TelemetryPacketV1.ExpectedProtocolName, Version = TelemetryPacketV1.ExpectedProtocolVersion },
            Frame = new TelemetryFrameV1 { TimestampMs = 1, DtMs = 8, TelemetryRateHz = 125, Sequence = 1, IsDuplicate = false, IsInterpolated = false },
            Game = new TelemetryGameV1 { State = "mission" },
            Player = new TelemetryPlayerV1 { IsInVehicle = true },
            Vehicle = new TelemetryVehicleV1
            {
                Name = "Tractor",
                Type = "tractor",
                Category = vehicleCategory,
                WheelTireTypes = "street",
                WheelTireProfile = "street",
                IsArticulated = isArticulated,
                MassT = mass / 1000.0,
                TotalMassT = totalMass / 1000.0
            },
            Controls = new TelemetryControlsV1 { Throttle = throttle, Brake = brake, Clutch = clutch },
            Motion = new TelemetryMotionV1
            {
                SpeedMps = speedKmh / 3.6,
                SpeedKmh = speedKmh,
                PitchDeg = pitchDeg,
                RollDeg = rollDeg,
                YawRateRadPerSec = yawRateDegPerSec is null ? null : yawRateDegPerSec * Math.PI / 180.0,
                LocalAccelerationMps2 = new TelemetryVector3V1 { X = localAccelerationX, Y = localAccelerationY, Z = localAccelerationZ }
            },
            Steering = new TelemetrySteeringV1 { Angle = steeringAngle ?? 0, Rate = steeringRate },
            Engine = new TelemetryEngineV1 { Rpm = rpm, Started = engineStarted },
            Transmission = new TelemetryTransmissionV1 { Gear = gear },
            Wheels = CreateWheels(maxWheelSlip, groundContactRatio, steeringGroundContactRatio),
            Suspension = new TelemetrySuspensionV1
            {
                Impulse = suspensionImpulse ?? bumpImpulse,
                VerticalImpactImpulse = verticalImpactImpulse ?? bumpImpulse,
                LandingImpulse = landingImpulse,
                LeftImpulse = leftSuspensionImpulse,
                RightImpulse = rightSuspensionImpulse
            },
            Surface = new TelemetrySurfaceV1 { IsOnField = isOnField, Type = surfaceType, Attribute = surfaceAttribute },
            Environment = new TelemetryEnvironmentV1 { GroundWetness = groundWetness, RainScale = rainScale },
            Attachments = [],
            Collisions = new TelemetryCollisionsV1 { CollisionImpulse = collisionImpulse, LongitudinalJerkImpulse = longitudinalJerkImpulse },
            Diagnostics = new TelemetryDiagnosticsV1()
        };
    }

    private static TelemetryPacketV1 NoVehiclePacket()
    {
        return new TelemetryPacketV1
        {
            Protocol = new TelemetryProtocolV1 { Name = TelemetryPacketV1.ExpectedProtocolName, Version = TelemetryPacketV1.ExpectedProtocolVersion },
            Frame = new TelemetryFrameV1 { TimestampMs = 1, DtMs = 8, TelemetryRateHz = 125, Sequence = 1, IsDuplicate = false, IsInterpolated = false },
            Game = new TelemetryGameV1 { State = "mission" },
            Player = new TelemetryPlayerV1 { IsInVehicle = false },
            Vehicle = null,
            Controls = null,
            Motion = null,
            Steering = null,
            Engine = null,
            Transmission = null,
            Wheels = [],
            Suspension = null,
            Surface = null,
            Environment = new TelemetryEnvironmentV1(),
            Attachments = [],
            Collisions = null,
            Diagnostics = new TelemetryDiagnosticsV1()
        };
    }

    private static List<TelemetryWheelV1> CreateWheels(double? slip, double? contactRatio, double? steeringContactRatio)
    {
        var contactCount = contactRatio is null ? 4 : (int)Math.Round(Math.Clamp(contactRatio.Value, 0, 1) * 4);
        var steeringContactCount = steeringContactRatio is null ? 0 : (int)Math.Ceiling(Math.Clamp(steeringContactRatio.Value, 0, 1) * 4);
        return Enumerable.Range(0, 4)
            .Select(index => new TelemetryWheelV1
            {
                Index = index,
                Side = index % 2 == 0 ? "left" : "right",
                IsSteering = steeringContactRatio is not null,
                Slip = slip,
                HasGroundContact = steeringContactRatio is null ? index < contactCount : index < steeringContactCount
            })
            .ToList();
    }
}
