using FieldForce.App.Models;

namespace FieldForce.App.Services;

internal sealed class RawTelemetryImpactDeriver
{
    private readonly Dictionary<string, VehicleState> _vehicles = new(StringComparer.Ordinal);
    private readonly Dictionary<string, WheelState> _wheels = new(StringComparer.Ordinal);

    public DerivedImpactFeatures Derive(TelemetryPacketV1 packet, TimeSpan deltaTime)
    {
        var dt = Math.Clamp(deltaTime.TotalSeconds, 0.001, 0.25);
        var vehicleKey = GetVehicleKey(packet);
        var position = ToVector(packet.Motion?.WorldPositionM);
        var rotation = ToVector(packet.Motion?.RotationRad);
        var localAcceleration = DeriveLocalAcceleration(vehicleKey, position, rotation, dt);
        var verticalImpact = CalculateVerticalImpact(localAcceleration.Y);
        var horizontal = Math.Clamp(Math.Sqrt((localAcceleration.X * localAcceleration.X) + (localAcceleration.Z * localAcceleration.Z)) / 9.81, 0, 2);
        var previousContactRatio = _vehicles.TryGetValue(vehicleKey, out var previousVehicle) ? previousVehicle.ContactRatio : 1;
        var currentContactRatio = CalculateContactRatio(packet);

        var leftSuspension = 0.0;
        var rightSuspension = 0.0;
        var bottomOut = 0.0;
        var leftBottomOut = 0.0;
        var rightBottomOut = 0.0;
        var hasWheelSignal = false;

        foreach (var wheel in packet.Wheels)
        {
            var wheelImpulse = DeriveWheelImpulse(vehicleKey, wheel, dt, out var wheelBottomOut);
            if (wheelImpulse > 0 || wheelBottomOut > 0)
            {
                hasWheelSignal = true;
            }

            bottomOut = Math.Max(bottomOut, wheelBottomOut);
            if (string.Equals(wheel.Side, "left", StringComparison.OrdinalIgnoreCase))
            {
                leftSuspension = Math.Max(leftSuspension, wheelImpulse);
                leftBottomOut = Math.Max(leftBottomOut, wheelBottomOut);
            }
            else if (string.Equals(wheel.Side, "right", StringComparison.OrdinalIgnoreCase))
            {
                rightSuspension = Math.Max(rightSuspension, wheelImpulse);
                rightBottomOut = Math.Max(rightBottomOut, wheelBottomOut);
            }
        }

        var suspension = Math.Max(Math.Max(leftSuspension, rightSuspension), verticalImpact);
        var landing = previousContactRatio < 0.20 && currentContactRatio > 0.60 && verticalImpact >= 0.35 ? verticalImpact : 0;
        var offroad = IsOffRoad(packet);
        var side = Math.Max(leftSuspension, rightSuspension);
        var speedKmh = Math.Max(0, packet.SpeedKmh ?? 0);
        var collision = speedKmh >= 6 &&
            horizontal >= (offroad ? 1.98 : 1.75) &&
            horizontal > verticalImpact * (offroad ? 2.15 : 1.65) &&
            (!offroad || side >= 0.75 || currentContactRatio < 0.70)
                ? horizontal
                : 0;

        _vehicles[vehicleKey] = new VehicleState(
            position,
            position is null || previousVehicle?.Position is null ? null : Divide(Subtract(position.Value, previousVehicle.Position.Value), dt),
            currentContactRatio);

        var legacy = TryLegacyFallback(packet);
        if (legacy is not null && position is null && !hasWheelSignal)
        {
            return legacy;
        }

        return new DerivedImpactFeatures(
            localAcceleration.X,
            localAcceleration.Y,
            localAcceleration.Z,
            verticalImpact,
            horizontal,
            collision,
            landing,
            suspension,
            leftSuspension,
            rightSuspension,
            bottomOut,
            leftBottomOut,
            rightBottomOut,
            hasWheelSignal || verticalImpact > 0 ? 1.0 : 0.0);
    }

    public void Reset()
    {
        _vehicles.Clear();
        _wheels.Clear();
    }

    private Vector3 DeriveLocalAcceleration(string vehicleKey, Vector3? position, Vector3? rotation, double dt)
    {
        if (position is null ||
            !_vehicles.TryGetValue(vehicleKey, out var previous) ||
            previous.Position is null ||
            previous.Velocity is null)
        {
            return new Vector3(0, 0, 0);
        }

        var velocity = Divide(Subtract(position.Value, previous.Position.Value), dt);
        var accelerationWorld = Divide(Subtract(velocity, previous.Velocity.Value), dt);
        return TransformWorldAccelerationToVehicleLocal(accelerationWorld, rotation);
    }

    private double DeriveWheelImpulse(string vehicleKey, TelemetryWheelV1 wheel, double dt, out double bottomOut)
    {
        bottomOut = 0;
        if (!IsValidFinite(wheel.RawSuspensionLength))
        {
            return 0;
        }

        var key = $"{vehicleKey}:{wheel.Index ?? -1}";
        var length = wheel.RawSuspensionLength!.Value;
        var previous = _wheels.TryGetValue(key, out var state) ? state : new WheelState(length, length, null);
        var min = Math.Min(previous.MinLength, length);
        var max = Math.Max(previous.MaxLength, length);
        var velocity = previous.Length is null ? 0 : (length - previous.Length.Value) / dt;
        _wheels[key] = new WheelState(min, max, length);

        if ((wheel.HasContact ?? wheel.HasGroundContact) != true)
        {
            return 0;
        }

        var load = FirstValid(wheel.TireLoad, wheel.SuspensionLoad, wheel.ContactForce) ?? 0;
        var loadRatio = Math.Clamp(Math.Abs(load) / 12000.0, 0, 1);
        var impulse = Math.Clamp(Math.Abs(velocity) / 1.8 + loadRatio * 0.35, 0, 2);
        var observedRange = max - min;
        if (observedRange >= 0.03)
        {
            var compressionRatio = Math.Clamp((max - length) / observedRange, 0, 1);
            if (compressionRatio >= 0.92 && velocity <= -0.25 && load > 0)
            {
                var compression = Math.Clamp((compressionRatio - 0.92) / 0.08, 0, 1);
                var velocityRatio = Math.Clamp(Math.Abs(velocity) / 1.5, 0, 1);
                bottomOut = Math.Clamp((compression * 0.75) + (velocityRatio * 0.85) + (loadRatio * 0.40), 0, 2);
            }
        }

        return impulse;
    }

    private DerivedImpactFeatures? TryLegacyFallback(TelemetryPacketV1 packet)
    {
        if (packet.Suspension is null && packet.Collisions is null)
        {
            return null;
        }

        var bottomOut = CalculateLegacyBottomOut(packet);
        var suspension = Math.Max(
            Math.Max(NormalizeImpulse(packet.SuspensionImpulse), NormalizeImpulse(packet.SuspensionHitImpulse)),
            Math.Max(NormalizeImpulse(packet.VerticalImpactImpulse), Math.Max(NormalizeImpulse(packet.LeftSuspensionImpulse), NormalizeImpulse(packet.RightSuspensionImpulse))));
        var confidence = packet.SuspensionConfidence;
        if (confidence <= 0 && (suspension > 0 || bottomOut.Total > 0))
        {
            confidence = 1.0;
        }

        return new DerivedImpactFeatures(
            packet.LocalAccelerationX ?? 0,
            packet.LocalAccelerationY ?? 0,
            packet.LocalAccelerationZ ?? 0,
            NormalizeImpulse(packet.VerticalImpactImpulse),
            Math.Clamp(Math.Abs(packet.LongitudinalJerkImpulse ?? 0), 0, 2),
            NormalizeImpulse(packet.CollisionImpulse),
            NormalizeImpulse(packet.LandingImpulse),
            suspension,
            NormalizeImpulse(packet.LeftSuspensionImpulse),
            NormalizeImpulse(packet.RightSuspensionImpulse),
            Math.Max(NormalizeImpulse(packet.BottomOutImpulse), bottomOut.Total),
            Math.Max(NormalizeImpulse(packet.LeftBottomOutImpulse), bottomOut.Left),
            Math.Max(NormalizeImpulse(packet.RightBottomOutImpulse), bottomOut.Right),
            confidence);
    }

    private static (double Total, double Left, double Right) CalculateLegacyBottomOut(TelemetryPacketV1 packet)
    {
        var total = 0.0;
        var left = 0.0;
        var right = 0.0;
        foreach (var wheel in packet.Wheels)
        {
            if (!IsValidFinite(wheel.CompressionRatio) ||
                !IsValidFinite(wheel.SuspensionVelocity) ||
                wheel.CompressionRatio!.Value < 0.92 ||
                wheel.SuspensionVelocity!.Value >= -0.25 ||
                (wheel.HasContact ?? wheel.HasGroundContact) != true)
            {
                continue;
            }

            var load = FirstValid(wheel.TireLoad, wheel.SuspensionLoad, wheel.ContactForce);
            if (!IsValidFinite(load) || load!.Value <= 0)
            {
                continue;
            }

            var compression = Math.Clamp((wheel.CompressionRatio.Value - 0.92) / 0.08, 0, 1);
            var velocity = Math.Clamp(Math.Abs(wheel.SuspensionVelocity.Value) / 1.5, 0, 1);
            var loadRatio = Math.Clamp(load.Value / 12000.0, 0, 1);
            var impulse = Math.Clamp((compression * 0.75) + (velocity * 0.85) + (loadRatio * 0.40), 0, 2);
            total = Math.Max(total, impulse);
            if (string.Equals(wheel.Side, "left", StringComparison.OrdinalIgnoreCase))
            {
                left = Math.Max(left, impulse);
            }
            else if (string.Equals(wheel.Side, "right", StringComparison.OrdinalIgnoreCase))
            {
                right = Math.Max(right, impulse);
            }
        }

        return (total, left, right);
    }

    private static double CalculateVerticalImpact(double localY)
    {
        var rawG = Math.Clamp(Math.Abs(localY) / 9.81, 0, 2);
        return rawG <= 0.22 ? 0 : rawG - 0.22;
    }

    private static double CalculateContactRatio(TelemetryPacketV1 packet)
    {
        if (packet.Wheels.Count == 0)
        {
            return 1;
        }

        var contact = packet.Wheels.Count(w => (w.HasContact ?? w.HasGroundContact) == true);
        return contact / (double)packet.Wheels.Count;
    }

    private static bool IsOffRoad(TelemetryPacketV1 packet)
    {
        var surface = packet.SurfaceType ?? packet.Wheels.Select(w => w.SurfaceType).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        return packet.IsOnField == true ||
               surface is "field" or "wetField" or "plowedField" or "cultivatedField" or "grass" or "dirt" or "gravel" or "mud" or "snow" or "shallowWater";
    }

    private static string GetVehicleKey(TelemetryPacketV1 packet)
    {
        return string.Join("|", packet.VehicleName ?? "unknown", packet.VehicleType ?? "", packet.VehicleCategory ?? "");
    }

    private static Vector3? ToVector(TelemetryVector3V1? value)
    {
        return IsValidFinite(value?.X) && IsValidFinite(value?.Y) && IsValidFinite(value?.Z)
            ? new Vector3(value!.X!.Value, value.Y!.Value, value.Z!.Value)
            : null;
    }

    private static Vector3 TransformWorldAccelerationToVehicleLocal(Vector3 value, Vector3? rotation)
    {
        var yaw = rotation?.Y ?? 0;
        var cos = Math.Cos(-yaw);
        var sin = Math.Sin(-yaw);
        return new Vector3(
            (value.X * cos) - (value.Z * sin),
            value.Y,
            (value.X * sin) + (value.Z * cos));
    }

    private static Vector3 Subtract(Vector3 left, Vector3 right) => new(left.X - right.X, left.Y - right.Y, left.Z - right.Z);

    private static Vector3 Divide(Vector3 value, double divisor) => new(value.X / divisor, value.Y / divisor, value.Z / divisor);

    private static double? FirstValid(params double?[] values)
    {
        foreach (var value in values)
        {
            if (IsValidFinite(value))
            {
                return value;
            }
        }

        return null;
    }

    private static double NormalizeImpulse(double? value)
    {
        return IsValidFinite(value) ? Math.Clamp(Math.Abs(value!.Value), 0, 2) : 0;
    }

    private static bool IsValidFinite(double? value)
    {
        return value is not null && !double.IsNaN(value.Value) && !double.IsInfinity(value.Value);
    }

    private sealed record VehicleState(Vector3? Position, Vector3? Velocity, double ContactRatio);

    private sealed record WheelState(double MinLength, double MaxLength, double? Length);

    private readonly record struct Vector3(double X, double Y, double Z);
}
