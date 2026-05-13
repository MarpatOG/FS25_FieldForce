# FS25 Suspension and Engine Data

Source: GIANTS Developer Network FS25 LuaDoc v1.15.0.0.

Legend:

- `[used]` Used by the current telemetry mod or Windows bridge.
- `[partial]` Available and used indirectly or as a derived value.
- `[unused]` Available in FS25 scripting data, but not used by the current implementation.

## Suspension / Wheels

FS25 does not expose suspension as a separate high-level specialization. Most useful suspension data is available through `WheelPhysics` / `Wheels`.

- `[used]` `spec_wheels.wheels`: wheel list used to build telemetry `wheels[]`.
- `[used]` `physics.hasGroundContact`, `physics.hasWaterContact`, `physics.hasSnowContact`: used to calculate wheel contact.
- `[used]` `physics.netInfo.slip`: used for `wheel.slip`, `WheelSlip`, `MaxWheelSlip`, and `SteeringWheelSlip`.
- `[used]` `physics.tireType`: used for tire/crawler profile and vehicle category selection.
- `[used]` `physics.lastTerrainAttribute`, `physics:getGroundAttributes()`, `physics:getIsOnField()`, `physics:getSurfaceSoundAttributes()`: used for surface telemetry.
- `[used]` `wheel.suspensionCompression`, `wheel.suspensionLoad`, `wheel.lastSuspensionCompression`, `physics.suspensionCompression`, `physics.suspensionLoad`, `physics.wheelLoad`: used as candidates for `suspensionImpulse`.
- `[used]` `suspension.impulse`, `verticalImpactImpulse`, `landingImpulse`, `leftImpulse`, `rightImpulse`: sent in telemetry and used by gameplay FFB.
- `[partial]` `netInfo.suspensionLength`: available in `WheelPhysics`, but not read directly by the mod.
- `[partial]` `WheelPhysics:getVisualInfo()`: can return suspension length relative to original `deltaY`, but the mod does not call it.
- `[unused]` `WheelPhysics:getTireLoad()`: ready-made tire load calculation.
- `[unused]` XML/physics suspension parameters: `suspTravel`, `initialCompression`, `deltaY`, `spring`, `damper`, `damperCompressionLowSpeed`, `damperCompressionHighSpeed`, `damperCompressionLowSpeedThreshold`, `damperRelaxationLowSpeed`, `damperRelaxationHighSpeed`, `damperRelaxationLowSpeedThreshold`.
- `[unused]` `WheelPhysics:setSuspensionMultipliers(springMultiplier, dampingMultiplier)`: API for changing suspension spring/damping multipliers.
- `[unused]` Additional wheel physics values that may be useful for FFB modeling: `mass`, `restLoad`, `frictionScale`, `maxLongStiffness`, `maxLatStiffness`, `maxLatStiffnessLoad`, `rotationDamping`, `sink`, `sinkTarget`, `tireGroundFrictionCoeff`.

## Engine

- `[used]` `vehicle:getMotorRpm()`: primary RPM source.
- `[used]` `spec_motorized.motor:getLastMotorRpm()` / `motor.lastMotorRpm`: RPM fallbacks.
- `[used]` `vehicle:getIsMotorStarted()` / `spec_motorized.isMotorStarted`: used as `engine.started`.
- `[used]` `vehicle:getMotorState()` / `spec_motorized.motorState`: used as `engine.state`, `engine.isStarting`, and the primary early `engineStartSeq` trigger when state enters `MotorState.STARTING`.
- `[used]` `spec_motorized.motorStartDuration` and `spec_motorized.motorStartTime`: sent as `engine.startDurationMs` and `engine.startRemainingMs` when available.
- `[used]` `spec_motorized.consumersByFillTypeName`, `spec_motorized.consumers`, `spec_motorized.consumerConfigurations`, and fill-unit supported fill types: used to derive `engine.energySources` and normalized `engine.powertrainType`.
- `[used]` `getGear`, `getSelectedGear`, `getCurrentGear`, and fallback fields `gear`, `currentGear`, `selectedGear`: used for transmission telemetry and gear-shift FFB pulses.
- `[unused]` `Motorized:getMotorRpmReal()`: available, but not used.
- `[unused]` `Motorized:getMotorRpmPercentage()`: available, but the bridge normalizes RPM from profile settings instead of game `min/max`.
- `[unused]` `VehicleMotor:getMinRpm()`, `VehicleMotor:getMaxRpm()`: not read from the game.
- `[unused]` `VehicleMotor:getTorque(acceleration)`, `getTorqueCurve()`, `getTorqueCurveValue(rpm)`, `getTorqueAndSpeedValues()`: torque curve data is available, but not used.
- `[unused]` `VehicleMotor:getMotorAppliedTorque()`, `getMotorAvailableTorque()`, `getMotorExternalTorque()`, `getPeakTorque()`: available torque/load data, not used.
- `[unused]` `VehicleMotor:getEqualizedMotorRpm()`, `getLastModulatedMotorRpm()`: alternative RPM values, not used.
- `[unused]` `VehicleMotor:getPtoMotorRpmRatio()`, `getRequiredMotorRpmRange()`: PTO and required RPM range data, not used.
- `[unused]` `spec_motorized.actualLoadPercentage`, `motorTemperature`, `lastAirUsage`, motor damping/inertia parameters: visible through `Motorized`, but not used.

`events.engineStartSeq` now means starter cranking began. The Windows bridge still keeps an RPM-rise detector, but only as a fallback for legacy or broken packets where `events.engineStartSeq` is absent.

## Current Implementation References

- `fs25-mod/src/FS25RealFfbTelemetry.lua`: builds `engine`, `transmission`, `wheels`, and `suspension` telemetry.
- `windows-app/src/App/Models/TelemetryPacketV1.cs`: receives and exposes the telemetry fields.
- `windows-app/src/App/Services/GameplayFfbCalculator.cs`: uses RPM, engine state, gear, contact, slip, and suspension impulses for gameplay FFB.

## GDN References

- `WheelPhysics`: https://gdn.giants-software.com/documentation_scripting_fs25.php?category=92&class=907&version=script
- `Wheels`: https://gdn.giants-software.com/documentation_scripting_fs25.php?category=77&class=832&version=script
- `Motorized`: https://gdn.giants-software.com/documentation_scripting_fs25.php?category=77&class=704&version=script
- `VehicleMotor`: https://gdn.giants-software.com/documentation_scripting_fs25.php?category=90&class=894&version=script
