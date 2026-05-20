namespace FieldForce.App.Models;

public enum TestFfbEffectKind
{
    SpeedSpring,
    SpeedDamper,
    MechanicalFriction,
    EngineRpmVibration,
    SurfaceFeedback,
    SlipFeedback,
    BumpFeedback,
    SuspensionHitFeedback,
    CollisionFeedback,
    LandingFeedback,
    TerrainRumble,
    GearShiftPulse,
    DrivetrainPulse,
    EngineStartStopPulse
}

public sealed record TestFfbEffectDescriptor(string DisplayName, string Group, TestFfbEffectKind Kind);
