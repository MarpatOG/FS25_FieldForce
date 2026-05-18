using FieldForce.App.Models;

namespace FieldForce.App.Services;

public sealed partial class GameplayFfbCalculator
{
    public static class SurfaceTractionLayer
    {
        public static LayerContribution<ContinuousHaptics> CalculateContinuous(TelemetryFeatures features, GameplayFfbEffectProfile profile, FfbFrameContext context)
        {
            var slip = CalculateSlipFeedback(features.Slip, features.SpeedKmh, profile.SlipFeedback, context.TelemetryFade, out var slipHz);
            if (!IsOffRoadSurface(features) || !profile.SurfaceFeedback.Enabled || features.SpeedKmh < Math.Max(0, profile.SurfaceFeedback.MinSpeedKmh))
            {
                return new(new ContinuousHaptics(0, 0, slip, slipHz, 0, 0, 0, 0), Math.Max(features.SurfaceConfidence, slip > 0 ? 1.0 : 0.0));
            }

            var surface = CalculateMaxCapped(profile.SurfaceFeedback, context.TelemetryFade) * Math.Clamp(features.TireSurfaceMultiplier, 0, 2);
            var wetnessEffect = CalculateWetnessEffect(profile.WetnessFeedback, features.Wetness, context.TelemetryFade);
            if (wetnessEffect > 0)
            {
                surface *= 1 + (wetnessEffect * profile.WetnessFeedback.SurfaceVibrationModifierPercent / 100.0);
            }

            var hz = CalculateSurfaceFrequency(profile.SurfaceFeedback, features.SpeedKmh, profile.SpeedDamper.SpeedReferenceKmh);
            return new(new ContinuousHaptics(surface, hz, slip, slipHz, 0, 0, 0, 0), features.SurfaceConfidence);
        }

        private static double CalculateSlipFeedback(double slip, double speedKmh, SlipFeedbackSettings settings, double fade, out int hz)
        {
            hz = 0;
            if (!settings.Enabled || speedKmh < Math.Max(0, settings.MinSpeedKmh))
            {
                return 0;
            }

            var minSlip = Math.Clamp(settings.MinSlip, 0, 1);
            var fullSlip = Math.Max(minSlip + 0.01, settings.FullSlip);
            if (slip <= minSlip)
            {
                return 0;
            }

            var ratio = Math.Clamp((slip - minSlip) / (fullSlip - minSlip), 0, 1);
            var curve = ApplyCurve(ratio, settings.Curve);
            var minHz = Math.Clamp(settings.MinFrequencyHz, 4, 60);
            var maxHz = Math.Clamp(settings.MaxFrequencyHz, minHz, 60);
            hz = Quantize((int)Math.Round(minHz + ((maxHz - minHz) * curve)), 2);
            return CalculateMaxCapped(settings, fade) * curve;
        }

        private static int CalculateSurfaceFrequency(SurfaceFeedbackSettings settings, double speedKmh, double speedReferenceKmh)
        {
            var minHz = Math.Clamp(settings.FieldFrequencyMinHz, 4, 45);
            var maxHz = Math.Clamp(settings.FieldFrequencyMaxHz, minHz, 45);
            var ratio = Math.Clamp(speedKmh / Math.Max(1, speedReferenceKmh), 0, 1);
            return Quantize((int)Math.Round(minHz + ((maxHz - minHz) * ApplyCurve(ratio, settings.Curve))), 2);
        }
    }

    public static class SuspensionTerrainLayer
    {
        public static LayerContribution<ContinuousHaptics> CalculateContinuous(TelemetryFeatures features, GameplayFfbEffectProfile profile, FfbFrameContext context)
        {
            var settings = profile.TerrainRumble;
            if (!settings.Enabled)
            {
                return new(new ContinuousHaptics(0, 0, 0, 0, 0, 0, 0, 0), features.SuspensionConfidence);
            }

            var minImpulse = CalculateTerrainMinImpulse(features, settings);
            var fullImpulse = CalculateTerrainFullImpulse(features, settings, minImpulse);
            var impulse = Math.Min(features.SuspensionImpulse, fullImpulse);
            if (impulse <= minImpulse)
            {
                return new(new ContinuousHaptics(0, 0, 0, 0, 0, 0, 0, 0), features.SuspensionConfidence);
            }

            var ratio = Math.Clamp((impulse - minImpulse) / (fullImpulse - minImpulse), 0, 1);
            var curve = ApplyCurve(ratio, settings.Curve);
            var minHz = Math.Clamp(settings.MinFrequencyHz, 4, 60);
            var maxHz = Math.Clamp(settings.MaxFrequencyHz, minHz, 60);
            var hz = Quantize((int)Math.Round(minHz + ((maxHz - minHz) * curve)), 2);
            var surfaceScale = CalculateTerrainSurfaceScale(features) * Math.Clamp(features.TireSurfaceMultiplier, 0, 2);
            var rumble = CalculateMaxCapped(settings, context.TelemetryFade) * curve * surfaceScale * CalculateHapticLoadScale(features);
            return new(new ContinuousHaptics(0, 0, 0, 0, 0, 0, rumble, hz), features.SuspensionConfidence);
        }

        public static IReadOnlyList<EventPulse> CalculatePulses(TelemetryPacketV1 packet, TelemetryFeatures features, GameplayFfbEffectProfile profile, FfbFrameContext context)
        {
            return CalculatePulseCandidates(packet, features, profile, context)
                .Where(candidate => candidate.Valid)
                .Select(candidate => candidate.Pulse)
                .ToArray();
        }

        public static IReadOnlyList<EventPulseCandidate> CalculatePulseCandidates(TelemetryPacketV1 packet, TelemetryFeatures features, GameplayFfbEffectProfile profile, FfbFrameContext context)
        {
            var collisionCandidate = CreateImpulseCandidate(FfbPulseKind.Collision, features.CollisionImpulse, profile.CollisionFeedback, context, features, DirectionFromHorizontalImpact(packet), CalculateCollisionSurfaceScale(features), CalculateCollisionMinImpulse(features, profile.CollisionFeedback));
            if (collisionCandidate.Valid && !ShouldAllowCollisionPulse(features))
            {
                collisionCandidate = collisionCandidate with { Valid = false, SuppressReason = "LongitudinalJerk" };
            }

            var candidates = new List<EventPulseCandidate>
                {
                    collisionCandidate,
                    CreateImpulseCandidate(FfbPulseKind.Landing, features.LandingImpulse, profile.LandingFeedback, context, features, 1)
                };
            var sideKind = SelectSidePulseKind(features);
            if (sideKind is not FfbPulseKind.Bump)
            {
                var sideImpulse = Math.Max(features.LeftSuspensionImpulse, features.RightSuspensionImpulse);
                candidates.Add(CreateImpulseCandidate(sideKind, sideImpulse, profile.SuspensionHitFeedback, context, features, sideKind == FfbPulseKind.LeftSuspensionHit ? -1 : 1, CalculatePulseSurfaceScale(features), CalculatePulseMinImpulse(features, profile.SuspensionHitFeedback)));
            }

            if (ShouldAllowRoadBump(features))
            {
                var direction = Math.Sign(packet.LocalAccelerationX ?? packet.SteeringAngle ?? 1);
                var bump = CreateImpulseCandidate(FfbPulseKind.Bump, features.VerticalImpactImpulse, profile.BumpFeedback, context, features, direction == 0 ? 1 : direction, CalculatePulseSurfaceScale(features), CalculatePulseMinImpulse(features, profile.BumpFeedback));
                if (HasSuppressingEvent(candidates, FfbPulseKind.Collision))
                {
                    bump = bump with { Valid = false, SuppressReason = "CollisionCandidateSelected" };
                }
                else if (HasSuppressingEvent(candidates, FfbPulseKind.Landing))
                {
                    bump = bump with { Valid = false, SuppressReason = "LandingCandidateSelected" };
                }

                candidates.Add(bump);
            }

            return candidates;
        }

        private static EventPulseCandidate CreateImpulseCandidate(
            FfbPulseKind kind,
            double impulse,
            ImpulsePulseFeedbackSettings settings,
            FfbFrameContext context,
            TelemetryFeatures features,
            double direction,
            double outputScale = 1,
            double? minImpulseOverride = null)
        {
            if (!settings.Enabled)
            {
                return EventPulseCandidate.Invalid(kind, impulse, "DisabledBySettings");
            }

            var minImpulse = Math.Clamp(minImpulseOverride ?? settings.MinImpulse, 0, 10);
            var fullImpulse = Math.Max(minImpulse + 0.01, settings.FullImpulse);
            var magnitude = Math.Abs(impulse);
            if (magnitude <= minImpulse)
            {
                return EventPulseCandidate.Invalid(kind, impulse, "BelowMinImpulse");
            }

            var ratio = Math.Clamp((magnitude - minImpulse) / (fullImpulse - minImpulse), 0, 1);
            if (direction == 0)
            {
                direction = 1;
            }

            var percent = CalculateMaxCapped(settings, context.TelemetryFade) * ApplyCurve(ratio, settings.Curve) * Math.Max(0, outputScale) * ImpactPulseGain;
            if (percent < MinPulsePercentToEmit)
            {
                return EventPulseCandidate.Invalid(kind, impulse, "BelowMinPercent", ratio, percent);
            }

            return new EventPulseCandidate(
                kind,
                impulse,
                ratio,
                percent,
                true,
                "None",
                new EventPulse(
                kind,
                percent,
                Math.Clamp(Math.Max(settings.DurationMs, context.DeviceProfile.Name.Contains("MOMO", StringComparison.OrdinalIgnoreCase) ? 120 : settings.DurationMs), 20, 250),
                CalculatePulseCooldown(kind, settings, features),
                direction,
                1.0));
        }

        private static int CalculatePulseCooldown(FfbPulseKind kind, ImpulsePulseFeedbackSettings settings, TelemetryFeatures features)
        {
            var configured = Math.Clamp(settings.CooldownMs, 20, 500);
            return kind switch
            {
                FfbPulseKind.Bump => Math.Min(configured, IsOffRoadSurface(features) || IsUnknownMixedSurface(features) ? 90 : 105),
                FfbPulseKind.LeftSuspensionHit or FfbPulseKind.RightSuspensionHit => Math.Min(configured, 85),
                FfbPulseKind.Landing => Math.Min(configured, 140),
                _ => configured
            };
        }

        private static bool HasSuppressingEvent(IEnumerable<EventPulseCandidate> candidates, FfbPulseKind kind)
        {
            return candidates.Any(candidate => candidate.Kind == kind && candidate.Valid && candidate.Percent >= MinEventPercentForSuppression);
        }

        private static double CalculatePulseSurfaceScale(TelemetryFeatures features)
        {
            var surfaceScale = IsOffRoadSurface(features) ? 1.05 : IsUnknownMixedSurface(features) ? 0.58 : 0.18;
            return surfaceScale * CalculateHapticLoadScale(features);
        }

        private static double CalculatePulseMinImpulse(TelemetryFeatures features, ImpulsePulseFeedbackSettings settings)
        {
            if (IsOffRoadSurface(features))
            {
                return Math.Max(Math.Clamp(settings.MinImpulse, 0, 10), settings is SuspensionHitFeedbackSettings ? 0.20 : 0.18);
            }

            if (IsUnknownMixedSurface(features))
            {
                return Math.Max(Math.Clamp(settings.MinImpulse, 0, 10), settings is SuspensionHitFeedbackSettings ? 0.30 : 0.24);
            }

            return Math.Max(Math.Clamp(settings.MinImpulse, 0, 10), settings is SuspensionHitFeedbackSettings ? 0.42 : 0.58);
        }

        private static double CalculateTerrainMinImpulse(TelemetryFeatures features, TerrainRumbleSettings settings)
        {
            var configured = Math.Clamp(settings.MinImpulse, 0, 10);
            if (IsOffRoadSurface(features))
            {
                return Math.Max(configured, 0.10);
            }

            return IsUnknownMixedSurface(features)
                ? Math.Max(configured, 0.18)
                : Math.Max(configured, 0.24);
        }

        private static double CalculateTerrainFullImpulse(TelemetryFeatures features, TerrainRumbleSettings settings, double minImpulse)
        {
            var configured = settings.FullImpulse;
            var target = IsOffRoadSurface(features) ? configured : IsUnknownMixedSurface(features) ? Math.Max(configured, 0.85) : Math.Max(configured, 0.95);
            return Math.Max(minImpulse + 0.01, target);
        }

        private static double CalculateTerrainSurfaceScale(TelemetryFeatures features)
        {
            return IsOffRoadSurface(features) ? 1.10 : IsUnknownMixedSurface(features) ? 0.60 : 0.35;
        }

        private static double CalculateCollisionSurfaceScale(TelemetryFeatures features)
        {
            return IsOffRoadSurface(features) ? 0.55 : 0.32;
        }

        private static double CalculateCollisionMinImpulse(TelemetryFeatures features, ImpulsePulseFeedbackSettings settings)
        {
            return Math.Max(Math.Clamp(settings.MinImpulse, 0, 10), 1.95);
        }

        private static bool ShouldAllowCollisionPulse(TelemetryFeatures features)
        {
            if (features.CollisionImpulse <= 0)
            {
                return false;
            }

            const double minCollision = 1.95;

            if (features.CollisionImpulse < minCollision)
            {
                return false;
            }

            if (features.VerticalImpactImpulse > features.CollisionImpulse * 0.55)
            {
                return false;
            }

            var sideImpulse = Math.Max(features.LeftSuspensionImpulse, features.RightSuspensionImpulse);
            var contactKnown = features.ContactConfidence > 0;
            var fullContact = !contactKnown || features.ContactRatio > 0.85;
            var hasImpactConfirmation =
                sideImpulse >= 0.30 ||
                features.VerticalImpactImpulse >= 0.30 ||
                (contactKnown && features.ContactRatio < 0.85);

            if (fullContact &&
                IsOffRoadSurface(features) &&
                features.VerticalImpactImpulse >= 0.30 &&
                sideImpulse < 0.75)
            {
                return false;
            }

            if (fullContact && !hasImpactConfirmation)
            {
                if (Math.Abs(features.SteeringAngle) > 0.05 || features.YawRateRatio > 0.10)
                {
                    return false;
                }

                if (features.LongitudinalJerkImpulse >= 0.35)
                {
                    return false;
                }
            }

            if (!IsOffRoadSurface(features) &&
                contactKnown &&
                features.ContactRatio > 0.75 &&
                features.CollisionImpulse < 2.0)
            {
                return false;
            }

            return true;
        }

        private static bool ShouldAllowRoadBump(TelemetryFeatures features)
        {
            if (features.ContactConfidence > 0 && features.ContactRatio < 0.15)
            {
                return false;
            }

            var sideConfirmation = Math.Max(features.LeftSuspensionImpulse, features.RightSuspensionImpulse) >= 0.10;
            if (features.LongitudinalJerkImpulse >= 0.35 && !sideConfirmation)
            {
                return false;
            }

            return true;
        }

        private static double DirectionFromHorizontalImpact(TelemetryPacketV1 packet)
        {
            var accel = packet.LocalAccelerationZ ?? packet.LocalAccelerationX ?? 1;
            var direction = Math.Sign(accel);
            return direction == 0 ? 1 : direction;
        }

        private static FfbPulseKind SelectSidePulseKind(TelemetryFeatures features)
        {
            if (features.IsArticulatedVehicle)
            {
                return FfbPulseKind.Bump;
            }

            var minSideImpulse = IsOffRoadSurface(features) ? 0.20 : IsUnknownMixedSurface(features) ? 0.30 : 0.35;
            var dominance = IsOffRoadSurface(features) ? 1.25 : IsUnknownMixedSurface(features) ? 1.40 : 1.80;
            var minSideDelta = IsOffRoadSurface(features) ? 0.12 : IsUnknownMixedSurface(features) ? 0.15 : 0.24;
            var left = features.LeftSuspensionImpulse;
            var right = features.RightSuspensionImpulse;
            var sideDelta = Math.Abs(left - right);
            if (left >= minSideImpulse &&
                (left > right * dominance || sideDelta >= minSideDelta) &&
                left > right)
            {
                return FfbPulseKind.LeftSuspensionHit;
            }

            if (right >= minSideImpulse &&
                (right > left * dominance || sideDelta >= minSideDelta) &&
                right > left)
            {
                return FfbPulseKind.RightSuspensionHit;
            }

            return FfbPulseKind.Bump;
        }
    }

    private static double CalculateWetnessEffect(WetnessFeedbackSettings settings, double? wetness, double fade)
    {
        if (!settings.Enabled || wetness is null)
        {
            return 0;
        }

        var minWetness = Math.Clamp(settings.MinWetness, 0, 1);
        var value = Math.Clamp(wetness.Value, 0, 1);
        if (value <= minWetness)
        {
            return 0;
        }

        var ratio = Math.Clamp((value - minWetness) / Math.Max(0.01, 1 - minWetness), 0, 1);
        return Math.Clamp(CalculateMaxCapped(settings, fade) / 100.0, 0, 1) * ApplyCurve(ratio, settings.Curve);
    }

    public sealed record EventPulseCandidate(
            FfbPulseKind Kind,
            double RawImpulse,
            double Ratio,
            double Percent,
            bool Valid,
            string SuppressReason,
            EventPulse Pulse)
    {
        public static EventPulseCandidate Invalid(FfbPulseKind kind, double rawImpulse, string reason, double ratio = 0, double percent = 0)
        {
            return new(kind, rawImpulse, ratio, percent, false, reason, new EventPulse(kind, 0, 0, 0, 1, 0));
        }
    }
}
