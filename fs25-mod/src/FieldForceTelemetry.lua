FieldForceTelemetry = {}

local FieldForceTelemetry_mt = Class(FieldForceTelemetry)

FieldForceTelemetry.SETTINGS_FILE_NAME = "settings.json"
FieldForceTelemetry.SETTING_OVERLAY_ENABLED = "overlayEnabled"

function FieldForceTelemetry.new()
    local self = setmetatable({}, FieldForceTelemetry_mt)
    self.config = FieldForceTelemetryConfig or {}
    self.transport = self:normalizeTransport(self.config.transport)
    self.host = self.config.host or "127.0.0.1"
    self.port = tonumber(self.config.port) or 34325
    self.fileTelemetryRateHz = self:normalizeTelemetryRate(self.config.fileTelemetryRateHz, self.config.fileFallbackRateHz, "fileTelemetryRateHz")
    self.udpTelemetryRateHz = self:normalizeTelemetryRate(self.config.udpTelemetryRateHz, self.config.updateRateHz, "udpTelemetryRateHz")
    self.updateRateHz = self.transport == "udp" and self.udpTelemetryRateHz or self.fileTelemetryRateHz
    self.intervalMs = 1000 / self.updateRateHz
    self.debug = self.config.debug == true
    self.elapsedMs = 0
    self.socket = nil
    self.udpEnabled = false
    self.fileEnabled = false
    self.filePath = nil
    self.lastPacket = nil
    self.lastPayload = nil
    self.lastPacketTime = nil
    self.lastSendTime = nil
    self.sendTimes = {}
    self.actualSendRate = 0
    self.lastFileWriteMs = nil
    self.lastPacketSource = "none"
    self.lastWriteError = nil
    self.overlayConfig = self.config.overlay or {}
    self.overlayEnabled = self.overlayConfig.enabled ~= false
    self.uiRegistered = false
    self.uiControls = {}
    self.fileWarningLogged = false
    self.transportWarningLogged = false
    self.motionAccelerationSmoothingSec = 0.08
    self.motionVerticalImpactDeadbandG = 0.22
    self.lastVehicleMotion = {}
    self.lastImpactState = {}
    self.lastSteeringSample = nil
    self.engineEventState = nil
    self.frameSequence = 0
    self.lastFrameMonotonicSeconds = nil
    self:loadOverlayUserSettings()
    return self
end

function FieldForceTelemetry:loadMap()
    FieldForceTelemetry.instance = self
    self:loadOverlayUserSettings()
    self:installMenuHook()
    FieldForceTelemetry.installVehicleTypeEventHook()
    print(string.format("[FieldForce] Telemetry mod loaded. Transport %s @ %d Hz", self.transport, self.updateRateHz))
    self:initTransport()
end

function FieldForceTelemetry:deleteMap()
    if self.socket ~= nil then
        pcall(function()
            self.socket:close()
        end)
    end

    self.socket = nil
    self.udpEnabled = false
    self.fileEnabled = false
    print("[FieldForce] Telemetry mod unloaded")
end

function FieldForceTelemetry:update(dt)
    if not self.udpEnabled and not self.fileEnabled then
        return
    end

    self.elapsedMs = self.elapsedMs + (dt or 0)
    if self.elapsedMs < self.intervalMs then
        return
    end

    self.elapsedMs = 0
    self:sendTelemetry()
end

function FieldForceTelemetry:normalizeTransport(value)
    if value == "udp" or value == "file+udp" or value == "file" then
        return value
    end

    if value ~= nil then
        self:logTransportWarning("Unsupported telemetry transport '" .. tostring(value) .. "'; using file")
    end

    return "file"
end

function FieldForceTelemetry:normalizeTelemetryRate(primaryValue, legacyValue, label)
    local value = tonumber(primaryValue)
    if value == nil then
        value = tonumber(legacyValue)
    end

    if value == 1 or value == 10 or value == 30 or value == 60 then
        return value
    end

    if value ~= nil then
        self:logTransportWarning(tostring(label) .. " must be one of 1, 10, 30, 60 Hz; using 60 Hz")
    end

    return 60
end

function FieldForceTelemetry:initTransport()
    print(string.format("[FieldForce] Initializing telemetry transport: %s", tostring(self.transport)))

    if self.transport == "file" or self.transport == "file+udp" then
        self:initFileTelemetry()
    end

    if self.transport == "file" then
        return
    end

    local okSocket, socketLib = pcall(require, "socket")
    if not okSocket or socketLib == nil then
        self:logTransportWarning(string.format(
            "Lua socket library is not available; UDP telemetry disabled. require('socket') error=%s; package.path=%s; package.cpath=%s",
            tostring(socketLib),
            self:getPackagePath("path"),
            self:getPackagePath("cpath")))
        return
    end

    local okUdp, udpOrError = pcall(function()
        local udp = socketLib.udp()
        if udp == nil then
            error("socket.udp() returned nil")
        end

        udp:settimeout(0)

        local okPeer, peerResult, peerError = pcall(function()
            return udp:setpeername(self.host, self.port)
        end)
        if not okPeer then
            error("udp:setpeername threw: " .. tostring(peerResult))
        end
        if peerResult == nil then
            error("udp:setpeername failed: " .. tostring(peerError))
        end

        return udp
    end)

    if not okUdp or udpOrError == nil then
        self:logTransportWarning(string.format(
            "Could not create UDP socket for udp://%s:%d: %s",
            tostring(self.host),
            self.port,
            tostring(udpOrError)))
        return
    end

    self.socket = udpOrError
    self.udpEnabled = true
    print("[FieldForce] UDP telemetry enabled")
end

function FieldForceTelemetry:initFileTelemetry()
    if self.config.fileTelemetry == false or self.config.fileFallback == false then
        self:logFileWarning("File telemetry disabled by config")
        return
    end

    local ioError = self:getFileApiError()
    if ioError ~= nil then
        self:logFileWarning("File telemetry unavailable: " .. ioError)
        return
    end

    if type(createFolder) ~= "function" then
        self:logFileWarning("createFolder is unavailable; file telemetry will only work if the folder already exists")
    end

    local basePath, pathError = self:getModSettingsPath()
    if basePath == nil then
        self:logFileWarning("File telemetry unavailable: " .. tostring(pathError or "could not resolve modSettings path"))
        return
    end

    self:createFolderIfPossible(basePath)
    self.filePath = basePath .. "/" .. tostring(self.config.fileName or "telemetry.json")

    local ok, writeError = self:writeFile(self.filePath, self:encodeJson(self:collectTelemetry()))
    if ok then
        self.fileEnabled = true
        print("[FieldForce] File telemetry enabled: " .. self.filePath)
    else
        self:logFileWarning("File telemetry unavailable: " .. tostring(writeError))
    end
end

function FieldForceTelemetry:sendTelemetry()
    local buildStartSeconds = self:getMonotonicSeconds()
    local packet = self:collectTelemetry()
    local buildTimeMs = math.max(0, (self:getMonotonicSeconds() - buildStartSeconds) * 1000)
    self:addBuildDiagnostics(packet, buildTimeMs)
    local payload = self:encodeJson(packet)
    self:addPayloadDiagnostics(packet, string.len(payload))
    payload = self:encodeJson(packet)
    if packet.diagnostics ~= nil then
        packet.diagnostics.payloadBytes = string.len(payload)
        payload = self:encodeJson(packet)
    end
    local sent = false

    if self.udpEnabled then
        local ok, result = pcall(function()
            return self.socket:send(payload)
        end)

        if not ok or result == nil then
            self:logTransportWarning("UDP send failed: " .. tostring(result))
        elseif self.debug then
            sent = true
            print("[FieldForce] Sent UDP telemetry: " .. payload)
        else
            sent = true
        end
    end

    if self.fileEnabled and self.filePath ~= nil and self:shouldWriteFileTelemetry(packet) then
        local ok, writeError = self:writeFile(self.filePath, payload)
        if not ok then
            self.lastWriteError = tostring(writeError)
            self:logFileWarning("File telemetry write failed: " .. tostring(writeError))
        elseif self.debug then
            sent = true
            self.lastWriteError = nil
            print("[FieldForce] Wrote file telemetry: " .. payload)
        else
            sent = true
            self.lastWriteError = nil
        end
    end

    self.lastPacket = packet
    self.lastPayload = payload
    self.lastPacketTime = packet.frame ~= nil and packet.frame.timestampMs or nil
    self.lastSendTime = packet._monotonicSeconds
    self.lastPacketSource = self:getTransportLabel(sent)
    if sent then
        self:recordSend(packet._monotonicSeconds)
    end
end

function FieldForceTelemetry:collectTelemetry()
    local vehicle = self:getActiveVehicle()
    local inVehicle = vehicle ~= nil
    local isDriver = inVehicle and self:getIsDriver(vehicle) or false
    local isPassenger = inVehicle and not isDriver or false
    local aiWorkerActive = inVehicle and self:getIsAiWorkerActive(vehicle) or false
    local surface = inVehicle and self:getSurfaceTelemetry(vehicle) or {}
    local wheel = inVehicle and self:getWheelTelemetry(vehicle) or {}
    local stableSpeedKmh = inVehicle and self:getSpeedKmh(vehicle) or nil
    local motion = inVehicle and self:getMotionTelemetry(vehicle, stableSpeedKmh) or {}
    motion.speedKmh = stableSpeedKmh or motion.speedKmh
    self.lastMotionDeltaSpeedKmh = inVehicle and motion.deltaSpeedKmh or nil
    local impact = inVehicle and self:calculateImpactImpulses(vehicle, motion, wheel) or {}
    local weather = inVehicle and self:getWeatherTelemetry() or {}

    local steeringAngle = inVehicle and self:getSteeringAngle(vehicle) or nil
    local nowSeconds = self:getMonotonicSeconds()
    self.frameSequence = (self.frameSequence or 0) + 1
    local dtMs = self.lastFrameMonotonicSeconds ~= nil and math.max(0, (nowSeconds - self.lastFrameMonotonicSeconds) * 1000) or nil
    self.lastFrameMonotonicSeconds = nowSeconds

    local packet = {
        protocol = {
            name = "FIELDFORCE_TELEMETRY",
            version = "1.5.0"
        },
        frame = {
            sequence = self.frameSequence,
            dtMs = dtMs,
            telemetryRateHz = self.updateRateHz,
            timestampMs = self:getTimestamp(),
            isDuplicate = false,
            isInterpolated = false
        },
        game = {
            state = self:getGameState()
        },
        player = {
            isInVehicle = inVehicle,
            isDriver = isDriver,
            isPassenger = isPassenger
        },
        vehicle = nil,
        controls = nil,
        motion = nil,
        steering = nil,
        engine = nil,
        transmission = nil,
        events = nil,
        wheels = self:jsonArray({}),
        suspension = nil,
        surface = nil,
        environment = {
            groundWetness = weather.groundWetness,
            rainScale = weather.rainScale
        },
        attachments = self:jsonArray({}),
        collisions = nil,
        diagnostics = {
            payloadBytes = nil,
            buildTimeMs = nil,
            warnings = self:jsonArray({})
        },
        _monotonicSeconds = nowSeconds
    }

    if not inVehicle then
        return packet
    end

    local speedKmh = stableSpeedKmh or motion.speedKmh
    packet.vehicle = {
        name = self:getVehicleName(vehicle),
        type = self:getVehicleType(vehicle),
        category = self:getVehicleCategory(vehicle, wheel.wheelTireProfile),
        wheelTireTypes = wheel.wheelTireTypes,
        wheelTireProfile = wheel.wheelTireProfile,
        isArticulated = self:getIsArticulatedVehicle(vehicle),
        massT = self:kgToTons(self:getMass(vehicle)),
        totalMassT = self:kgToTons(self:getTotalMass(vehicle)),
        aiWorkerActive = aiWorkerActive
    }
    packet.attachments = self:jsonArray(self:getAttachmentTelemetry(vehicle))
    packet.controls = {
        throttle = self:getFirstNumber(vehicle.axisForward, vehicle.spec_drivable ~= nil and vehicle.spec_drivable.axisForward or nil),
        brake = self:getFirstNumber(vehicle.axisBrake, vehicle.brakePedal, vehicle.spec_drivable ~= nil and vehicle.spec_drivable.axisBrake or nil),
        clutch = self:getFirstNumber(vehicle.axisClutch, vehicle.clutchPedal)
    }
    packet.motion = {
        speedMps = type(speedKmh) == "number" and (speedKmh / 3.6) or nil,
        speedKmh = speedKmh,
        pitchDeg = motion.pitchDeg,
        rollDeg = motion.rollDeg,
        yawRateRadPerSec = self:degToRad(motion.yawRateDegPerSec),
        slopeDeg = motion.slopeDeg,
        localAccelerationMps2 = {
            x = motion.localAccelerationX,
            y = motion.localAccelerationY,
            z = motion.localAccelerationZ
        }
    }
    packet.steering = {
        angle = steeringAngle,
        rate = self:getSteeringRate(vehicle, steeringAngle)
    }
    local rpm = self:getRpm(vehicle)
    local minRpm = self:getMinRpm(vehicle)
    local maxRpm = self:getMaxRpm(vehicle)
    local motorState = self:getMotorState(vehicle)
    local engineState = self:getEngineStateText(motorState)
    local engineIsStarting = self:isMotorState(motorState, "STARTING")
    local startDurationMs = self:getMotorStartDurationMs(vehicle)
    local startRemainingMs = self:getMotorStartRemainingMs(vehicle, motorState)
    local engineRunning = self:getEngineStarted(vehicle)
    local gear = self:getGear(vehicle)
    local eventState = self:updateEngineEventState(vehicle, engineRunning, motorState, gear, nowSeconds, startDurationMs)
    local motorType = self:getMotorType(vehicle)
    local energySources = self:getEngineEnergySources(vehicle, motorType)
    packet.engine = {
        isRunning = engineRunning,
        started = engineRunning,
        state = engineState,
        isStarting = engineIsStarting,
        startDurationMs = startDurationMs,
        startRemainingMs = startRemainingMs,
        rpm = rpm,
        rpm01 = self:normalizeRatio(rpm, minRpm, maxRpm),
        minRpm = minRpm,
        maxRpm = maxRpm,
        load01 = self:getEngineLoad01(vehicle),
        torque = self:getMotorTorque(vehicle),
        maxTorque = self:getMotorMaxTorque(vehicle),
        motorType = motorType,
        powertrainType = self:getPowertrainType(energySources),
        energySources = self:jsonArray(energySources)
    }
    packet.transmission = {
        gear = gear,
        previousGear = eventState.previousGear,
        targetGear = self:getTargetGear(vehicle),
        gearGroup = self:getGearGroup(vehicle),
        clutch01 = packet.controls.clutch,
        brake01 = packet.controls.brake,
        throttle01 = packet.controls.throttle
    }
    packet.events = {
        engineStartSeq = eventState.engineStartSeq,
        engineStopSeq = eventState.engineStopSeq,
        gearChangeSeq = eventState.gearChangeSeq,
        gearChangeKind = eventState.gearChangeKind,
        gearChangeTimeMs = eventState.gearChangeTimeMs
    }
    packet.wheels = self:jsonArray(wheel.wheels or {})
    packet.suspension = {
        impulse = impact.suspensionImpulse,
        verticalImpactImpulse = impact.verticalImpactImpulse,
        landingImpulse = impact.landingImpulse,
        leftImpulse = self:calculateSideSuspensionImpulse(impact.verticalImpactImpulse, wheel.leftSuspensionImpulse, wheel.leftContactRatio),
        rightImpulse = self:calculateSideSuspensionImpulse(impact.verticalImpactImpulse, wheel.rightSuspensionImpulse, wheel.rightContactRatio)
    }
    packet.surface = {
        isOnField = self:getIsOnField(vehicle),
        type = surface.surfaceType,
        attribute = surface.surfaceAttribute
    }
    packet.environment = {
        groundWetness = surface.groundWetness or weather.groundWetness,
        rainScale = weather.rainScale
    }
    packet.collisions = {
        collisionImpulse = impact.collisionImpulse,
        longitudinalJerkImpulse = impact.longitudinalJerkImpulse
    }

    return packet
end

function FieldForceTelemetry:kgToTons(value)
    if type(value) ~= "number" then
        return nil
    end

    return value / 1000
end

function FieldForceTelemetry:degToRad(value)
    if type(value) ~= "number" then
        return nil
    end

    return value * math.pi / 180
end

function FieldForceTelemetry:jsonArray(values)
    values = values or {}
    values.__jsonArray = true
    return values
end

function FieldForceTelemetry:addBuildDiagnostics(packet, buildTimeMs)
    if packet == nil or packet.diagnostics == nil then
        return
    end

    packet.diagnostics.buildTimeMs = buildTimeMs
    if buildTimeMs > 2 then
        table.insert(packet.diagnostics.warnings, string.format("packet_build_time_ms %.3f exceeds 2 ms", buildTimeMs))
    end
end

function FieldForceTelemetry:addPayloadDiagnostics(packet, payloadBytes)
    if packet == nil or packet.diagnostics == nil then
        return
    end

    packet.diagnostics.payloadBytes = payloadBytes
    if payloadBytes > 49152 then
        table.insert(packet.diagnostics.warnings, string.format("payload_bytes %d exceeds hard warning budget 49152", payloadBytes))
    elseif payloadBytes > 24576 then
        table.insert(packet.diagnostics.warnings, string.format("payload_bytes %d exceeds warning budget 24576", payloadBytes))
    end
end

function FieldForceTelemetry:getActiveVehicle()
    if g_localPlayer ~= nil and g_localPlayer.getCurrentVehicle ~= nil then
        local ok, vehicle = pcall(function()
            return g_localPlayer:getCurrentVehicle()
        end)
        if ok and vehicle ~= nil then
            return self:getForceFeedbackVehicle(vehicle)
        end
    end

    local mission = g_currentMission
    if mission == nil then
        return nil
    end

    if mission.controlledVehicle ~= nil then
        return self:getForceFeedbackVehicle(mission.controlledVehicle)
    end

    if mission.controlledVehicles ~= nil then
        for _, vehicle in pairs(mission.controlledVehicles) do
            if vehicle ~= nil then
                return self:getForceFeedbackVehicle(vehicle)
            end
        end
    end

    if mission.player ~= nil and mission.player.getCurrentVehicle ~= nil then
        local ok, vehicle = pcall(function()
            return mission.player:getCurrentVehicle()
        end)
        if ok and vehicle ~= nil then
            return self:getForceFeedbackVehicle(vehicle)
        end
    end

    return nil
end

function FieldForceTelemetry:getIsDriver(vehicle)
    if vehicle == nil then
        return false
    end

    if vehicle.getIsVehicleControlledByPlayer ~= nil then
        local ok, controlled = pcall(function()
            return vehicle:getIsVehicleControlledByPlayer()
        end)
        if ok and controlled ~= nil then
            return controlled == true
        end
    end

    local mission = g_currentMission
    if mission == nil then
        return false
    end

    if self:isSameForceFeedbackVehicle(vehicle, mission.controlledVehicle) then
        return true
    end

    if mission.controlledVehicles ~= nil then
        for _, controlledVehicle in pairs(mission.controlledVehicles) do
            if self:isSameForceFeedbackVehicle(vehicle, controlledVehicle) then
                return true
            end
        end
    end

    return false
end

function FieldForceTelemetry:isSameForceFeedbackVehicle(vehicle, candidate)
    if vehicle == nil or candidate == nil then
        return false
    end

    if vehicle == candidate then
        return true
    end

    return self:getForceFeedbackVehicle(candidate) == vehicle
end

function FieldForceTelemetry:getIsAiWorkerActive(vehicle)
    if vehicle == nil then
        return false
    end

    if vehicle.getIsAIActive ~= nil then
        local ok, active = pcall(function()
            return vehicle:getIsAIActive()
        end)
        if ok and active ~= nil then
            return active == true
        end
    end

    return vehicle.spec_aiJobVehicle ~= nil and vehicle.spec_aiJobVehicle.job ~= nil
end

function FieldForceTelemetry:getForceFeedbackVehicle(vehicle)
    if vehicle == nil then
        return nil
    end

    if self:isDriveableTelemetrySource(vehicle) then
        return vehicle
    end

    local selectedVehicle = nil
    if vehicle.getSelectedVehicle ~= nil then
        local ok, result = pcall(function()
            return vehicle:getSelectedVehicle()
        end)
        if ok then
            selectedVehicle = result
        end
    end

    if self:isDriveableTelemetrySource(selectedVehicle) then
        return selectedVehicle
    end

    return vehicle
end

function FieldForceTelemetry:isDriveableTelemetrySource(vehicle)
    return vehicle ~= nil and
        (vehicle.spec_drivable ~= nil or
            vehicle.spec_motorized ~= nil or
            vehicle.getMotorRpm ~= nil or
            vehicle.getIsMotorStarted ~= nil)
end

function FieldForceTelemetry:getTimestamp()
    if g_time ~= nil then
        return g_time
    end

    if os ~= nil and type(os.clock) == "function" then
        return os.clock()
    end

    return 0
end

function FieldForceTelemetry:getMonotonicSeconds()
    if g_time ~= nil and type(g_time) == "number" then
        return g_time / 1000
    end

    if os ~= nil and type(os.clock) == "function" then
        return os.clock()
    end

    return 0
end

function FieldForceTelemetry:getGameState()
    if g_currentMission == nil then
        return "noMission"
    end

    return "mission"
end

function FieldForceTelemetry:getVehicleName(vehicle)
    if vehicle.getName ~= nil then
        local ok, name = pcall(function()
            return vehicle:getName()
        end)
        if ok and name ~= nil then
            return tostring(name)
        end
    end

    return tostring(vehicle.name or vehicle.configFileName or "Unknown")
end

function FieldForceTelemetry:getVehicleType(vehicle)
    if vehicle.typeName ~= nil then
        return tostring(vehicle.typeName)
    end

    if vehicle.typeDesc ~= nil then
        return tostring(vehicle.typeDesc)
    end

    return "Unknown"
end

function FieldForceTelemetry:getVehicleCategory(vehicle, wheelTireProfile)
    if vehicle == nil then
        return "Unknown"
    end

    local rawType = self:getVehicleTypeText(vehicle)
    if rawType == nil then
        return "Unknown"
    end

    if self:isHarvesterType(rawType) then
        return "Harvester"
    end

    if self:isTruckType(rawType) then
        if wheelTireProfile == "tracked" or self:hasActiveCrawlers(vehicle) then
            return "TractorTracked"
        elseif wheelTireProfile == "agricultural" then
            return "TractorWheeled"
        end

        return "Truck"
    end

    if self:isLoaderTelehandlerType(rawType) then
        return "LoaderTelehandler"
    end

    if self:isLightVehicleType(rawType) then
        return "LightVehicle"
    end

    if self:isTractorType(rawType) then
        local tracked = self:hasActiveCrawlers(vehicle)

        if wheelTireProfile == "street" and not tracked then
            return "Truck"
        end

        if tracked then
            return "TractorTracked"
        end

        return "TractorWheeled"
    end

    return "Unknown"
end

function FieldForceTelemetry:getIsArticulatedVehicle(vehicle)
    if vehicle == nil then
        return nil
    end

    return vehicle.spec_articulatedAxis ~= nil
end

function FieldForceTelemetry:getVehicleTypeText(vehicle)
    local parts = {}
    if vehicle.typeName ~= nil then
        table.insert(parts, tostring(vehicle.typeName))
    end
    if vehicle.typeDesc ~= nil then
        table.insert(parts, tostring(vehicle.typeDesc))
    end

    if #parts == 0 then
        return nil
    end

    return table.concat(parts, " ")
end

function FieldForceTelemetry:isTractorType(value)
    return self:textHasAnyAlias(value, {
        "tractor",
        "tractors",
        "traktor",
        "traktoren"
    })
end

function FieldForceTelemetry:isHeavyTractorType(value)
    return self:textHasAnyAlias(value, {
        "tractorlarge",
        "tractor large",
        "large tractor",
        "heavytractor",
        "heavy tractor",
        "bigtractor",
        "big tractor",
        "traktor gross",
        "gross traktor"
    })
end

function FieldForceTelemetry:isHarvesterType(value)
    return self:textHasAnyAlias(value, {
        "combine",
        "combineharvester",
        "combine harvester",
        "harvester",
        "forageharvester",
        "forage harvester",
        "woodharvester",
        "wood harvester"
    })
end

function FieldForceTelemetry:isTruckType(value)
    return self:textHasAnyAlias(value, {
        "truck",
        "trucks",
        "semiTruck",
        "semi truck",
        "roadTractor",
        "road tractor",
        "lkw",
        "lastkraftwagen"
    })
end

function FieldForceTelemetry:isLoaderTelehandlerType(value)
    return self:textHasAnyAlias(value, {
        "telehandler",
        "wheel loader",
        "wheelloader",
        "front loader",
        "frontloader",
        "skidsteer",
        "skid steer",
        "loader"
    })
end

function FieldForceTelemetry:isLightVehicleType(value)
    return self:textHasAnyAlias(value, {
        "car",
        "cars",
        "pickup",
        "pick up",
        "utv",
        "atv",
        "motorbike",
        "motorcycle",
        "supportvehicle",
        "support vehicle"
    })
end

function FieldForceTelemetry:textHasAnyAlias(value, patterns)
    if value == nil then
        return false
    end

    local normalized = " " .. self:normalizeAliasText(value) .. " "
    for _, pattern in ipairs(patterns) do
        local needle = " " .. self:normalizeAliasText(pattern) .. " "
        if string.find(normalized, needle, 1, true) ~= nil then
            return true
        end
    end

    return false
end

function FieldForceTelemetry:normalizeAliasText(value)
    value = tostring(value or "")
    value = string.gsub(value, "([a-z])([A-Z])", "%1 %2")
    value = string.lower(value)
    value = string.gsub(value, "[^%w]+", " ")
    value = string.gsub(value, "%s+", " ")
    value = string.gsub(value, "^%s+", "")
    value = string.gsub(value, "%s+$", "")
    return value
end

function FieldForceTelemetry:hasActiveCrawlers(vehicle)
    if vehicle == nil then
        return false
    end

    local crawlerSpec = vehicle.spec_crawlers
    if crawlerSpec == nil then
        return false
    end

    if self:tableHasEntries(crawlerSpec.crawlers) then
        return true
    end

    local wheelConfigCandidates = {
        crawlerSpec.wheelConfigurationCrawlers,
        crawlerSpec.wheelConfigCrawlers,
        crawlerSpec.crawlersByWheelConfiguration,
        crawlerSpec.configurationCrawlers
    }

    for _, candidate in ipairs(wheelConfigCandidates) do
        if self:tableHasEntries(candidate) then
            return true
        end
    end

    return false
end

function FieldForceTelemetry:tableHasEntries(value)
    if type(value) ~= "table" then
        return false
    end

    for _, entry in pairs(value) do
        if entry ~= nil then
            return true
        end
    end

    return false
end

function FieldForceTelemetry:getSpeedKmh(vehicle)
    local speed = self:getRawSpeedKmh(vehicle)
    if speed ~= nil and speed >= 0 and speed < 300 then
        return speed
    end

    return nil
end

function FieldForceTelemetry:getRawSpeedKmh(vehicle)
    if vehicle.getLastSpeed ~= nil then
        local ok, speed = pcall(function()
            return vehicle:getLastSpeed()
        end)
        if ok and speed ~= nil then
            return speed
        end
    end

    if vehicle.lastSpeedReal ~= nil then
        return vehicle.lastSpeedReal
    end

    return nil
end

function FieldForceTelemetry:getSteeringAngle(vehicle)
    return self:getFirstNumber(
        vehicle.rotatedTime,
        vehicle.steeringAngle,
        vehicle.steeringInput,
        vehicle.axisSide,
        vehicle.spec_drivable ~= nil and vehicle.spec_drivable.axisSide or nil,
        vehicle.spec_drivable ~= nil and vehicle.spec_drivable.lastInputValues ~= nil and vehicle.spec_drivable.lastInputValues.axisSide or nil,
        vehicle.spec_drivable ~= nil and vehicle.spec_drivable.steeringAngle or nil,
        vehicle.spec_drivable ~= nil and vehicle.spec_drivable.steeringInput or nil,
        self:getAverageWheelSteeringAngle(vehicle)
    )
end

function FieldForceTelemetry:getSteeringRate(vehicle, steeringAngle)
    if steeringAngle == nil then
        return nil
    end

    local now = self:getMonotonicSeconds()
    local vehicleId = tostring(vehicle.rootNode or vehicle)
    local previous = self.lastSteeringSample
    self.lastSteeringSample = {
        vehicleId = vehicleId,
        time = now,
        angle = steeringAngle
    }

    if previous == nil or previous.vehicleId ~= vehicleId or previous.time == nil or previous.angle == nil then
        return nil
    end

    local dt = now - previous.time
    if dt <= 0 or dt > 1 then
        return nil
    end

    return (steeringAngle - previous.angle) / dt
end

function FieldForceTelemetry:getAverageWheelSteeringAngle(vehicle)
    local total = 0
    local count = 0

    for _, wheel in ipairs(self:getVehicleWheels(vehicle)) do
        local value = self:getFirstNumber(
            wheel.steeringAngle,
            wheel.rotatedTime,
            wheel.steeringInput,
            wheel.steeringAxis
        )

        if value ~= nil and math.abs(value) > 0.0001 then
            total = total + value
            count = count + 1
        end
    end

    if count > 0 then
        return total / count
    end

    return nil
end

function FieldForceTelemetry:getFirstNumber(...)
    local fallbackZero = nil
    local count = select("#", ...)

    for index = 1, count do
        local value = select(index, ...)
        if type(value) == "number" and value == value and value ~= math.huge and value ~= -math.huge then
            if math.abs(value) > 0.0001 then
                return value
            end

            fallbackZero = value
        end
    end

    return fallbackZero
end

function FieldForceTelemetry:getRpm(vehicle)
    if vehicle.getMotorRpm ~= nil then
        local ok, rpm = pcall(function()
            return vehicle:getMotorRpm()
        end)
        if ok then
            return rpm
        end
    end

    local motor = vehicle.spec_motorized ~= nil and vehicle.spec_motorized.motor or nil
    if motor ~= nil then
        if motor.getLastMotorRpm ~= nil then
            local ok, rpm = pcall(function()
                return motor:getLastMotorRpm()
            end)
            if ok then
                return rpm
            end
        end

        if motor.lastMotorRpm ~= nil then
            return motor.lastMotorRpm
        end
    end

    return nil
end

function FieldForceTelemetry:getMinRpm(vehicle)
    local motor = vehicle ~= nil and vehicle.spec_motorized ~= nil and vehicle.spec_motorized.motor or nil
    return self:getFirstNumber(
        motor ~= nil and motor.minRpm or nil,
        motor ~= nil and motor.idleRpm or nil,
        vehicle ~= nil and vehicle.minRpm or nil,
        500)
end

function FieldForceTelemetry:getMaxRpm(vehicle)
    local motor = vehicle ~= nil and vehicle.spec_motorized ~= nil and vehicle.spec_motorized.motor or nil
    return self:getFirstNumber(
        motor ~= nil and motor.maxRpm or nil,
        motor ~= nil and motor.maxForwardSpeedRpm or nil,
        vehicle ~= nil and vehicle.maxRpm or nil,
        2400)
end

function FieldForceTelemetry:normalizeRatio(value, minValue, maxValue)
    if type(value) ~= "number" or type(minValue) ~= "number" or type(maxValue) ~= "number" or maxValue <= minValue then
        return nil
    end

    return math.max(0, math.min(1, (value - minValue) / (maxValue - minValue)))
end

function FieldForceTelemetry:getEngineLoad01(vehicle)
    local motor = vehicle ~= nil and vehicle.spec_motorized ~= nil and vehicle.spec_motorized.motor or nil
    local value = self:getFirstNumber(
        motor ~= nil and motor.loadPercentage or nil,
        motor ~= nil and motor.motorLoadPercentage or nil,
        motor ~= nil and motor.lastMotorLoadPercentage or nil,
        vehicle ~= nil and vehicle.motorLoadPercentage or nil)
    if type(value) ~= "number" then
        local throttle = self:getFirstNumber(vehicle ~= nil and vehicle.axisForward or nil, vehicle ~= nil and vehicle.spec_drivable ~= nil and vehicle.spec_drivable.axisForward or nil)
        return type(throttle) == "number" and math.max(0, math.min(1, math.abs(throttle))) or nil
    end

    if value > 1.5 then
        value = value / 100
    end

    return math.max(0, math.min(1, value))
end

function FieldForceTelemetry:getMotorTorque(vehicle)
    local motor = vehicle ~= nil and vehicle.spec_motorized ~= nil and vehicle.spec_motorized.motor or nil
    return self:getFirstNumber(
        motor ~= nil and motor.torque or nil,
        motor ~= nil and motor.lastMotorTorque or nil,
        motor ~= nil and motor.motorTorque or nil)
end

function FieldForceTelemetry:getMotorMaxTorque(vehicle)
    local motor = vehicle ~= nil and vehicle.spec_motorized ~= nil and vehicle.spec_motorized.motor or nil
    return self:getFirstNumber(
        motor ~= nil and motor.maxTorque or nil,
        motor ~= nil and motor.peakTorque or nil)
end

function FieldForceTelemetry:getMotorType(vehicle)
    local motor = vehicle ~= nil and vehicle.spec_motorized ~= nil and vehicle.spec_motorized.motor or nil
    return tostring(
        motor ~= nil and (motor.motorType or motor.typeName or motor.type) or
        vehicle ~= nil and vehicle.motorType or
        vehicle ~= nil and vehicle.typeName or
        "unknown")
end

function FieldForceTelemetry:getEngineEnergySources(vehicle, motorType)
    local sources = {}
    local seen = {}

    self:addEnergySourcesFromMotorizedSpec(vehicle ~= nil and vehicle.spec_motorized or nil, sources, seen)
    self:addEnergySourcesFromFillUnits(vehicle, sources, seen)
    self:addEnergySourcesFromText(motorType, sources, seen)
    self:addEnergySourcesFromText(vehicle ~= nil and vehicle.typeName or nil, sources, seen)
    self:addEnergySourcesFromText(vehicle ~= nil and vehicle.configFileName or nil, sources, seen)
    self:addEnergySourcesFromText(vehicle ~= nil and vehicle.xmlFilename or nil, sources, seen)

    return sources
end

function FieldForceTelemetry:addEnergySourcesFromMotorizedSpec(spec, sources, seen)
    if spec == nil then
        return
    end

    if type(spec.consumersByFillTypeName) == "table" then
        for fillTypeName, _ in pairs(spec.consumersByFillTypeName) do
            self:addEnergySourceFromFillTypeName(fillTypeName, sources, seen)
        end
    end

    if type(spec.consumers) == "table" then
        for _, consumer in pairs(spec.consumers) do
            self:addEnergySourceFromConsumer(consumer, sources, seen)
        end
    end

    if type(spec.consumerConfigurations) == "table" then
        for _, configuration in pairs(spec.consumerConfigurations) do
            if type(configuration) == "table" then
                for _, consumer in pairs(configuration) do
                    self:addEnergySourceFromConsumer(consumer, sources, seen)
                end
            end
        end
    end
end

function FieldForceTelemetry:addEnergySourcesFromFillUnits(vehicle, sources, seen)
    local fillUnits = vehicle ~= nil and vehicle.spec_fillUnit ~= nil and vehicle.spec_fillUnit.fillUnits or nil
    if type(fillUnits) ~= "table" then
        return
    end

    for _, fillUnit in pairs(fillUnits) do
        if type(fillUnit) == "table" and type(fillUnit.supportedFillTypes) == "table" then
            for fillType, enabled in pairs(fillUnit.supportedFillTypes) do
                if enabled then
                    self:addEnergySourceFromFillType(fillType, sources, seen)
                end
            end
        end
    end
end

function FieldForceTelemetry:addEnergySourceFromConsumer(consumer, sources, seen)
    if type(consumer) ~= "table" then
        return
    end

    self:addEnergySourceFromFillType(consumer.fillType, sources, seen)
    self:addEnergySourceFromFillTypeName(consumer.fillTypeName or consumer.fillTypeTitle, sources, seen)
end

function FieldForceTelemetry:addEnergySourceFromFillType(fillType, sources, seen)
    if fillType == nil then
        return
    end

    if FillType ~= nil then
        if fillType == FillType.DIESEL then
            self:addEnergySource("diesel", sources, seen)
            return
        elseif fillType == FillType.ELECTRICCHARGE then
            self:addEnergySource("electricCharge", sources, seen)
            return
        elseif fillType == FillType.METHANE then
            self:addEnergySource("methane", sources, seen)
            return
        end
    end

    self:addEnergySourceFromFillTypeName(tostring(fillType), sources, seen)
end

function FieldForceTelemetry:addEnergySourceFromFillTypeName(fillTypeName, sources, seen)
    if fillTypeName == nil then
        return
    end

    local text = string.lower(tostring(fillTypeName))
    if string.find(text, "electric", 1, true) or string.find(text, "charge", 1, true) or string.find(text, "battery", 1, true) then
        self:addEnergySource("electricCharge", sources, seen)
    elseif string.find(text, "diesel", 1, true) or string.find(text, "fuel", 1, true) then
        self:addEnergySource("diesel", sources, seen)
    elseif string.find(text, "methane", 1, true) or string.find(text, "gas", 1, true) then
        self:addEnergySource("methane", sources, seen)
    end
end

function FieldForceTelemetry:addEnergySourcesFromText(value, sources, seen)
    if value == nil then
        return
    end

    local text = string.lower(tostring(value))
    if string.find(text, "hybrid", 1, true) then
        self:addEnergySource("electricCharge", sources, seen)
        self:addEnergySource("diesel", sources, seen)
        return
    end

    self:addEnergySourceFromFillTypeName(text, sources, seen)
end

function FieldForceTelemetry:addEnergySource(source, sources, seen)
    if source == nil or seen[source] == true then
        return
    end

    table.insert(sources, source)
    seen[source] = true
end

function FieldForceTelemetry:getPowertrainType(energySources)
    local hasElectric = false
    local hasCombustion = false

    if type(energySources) == "table" then
        for _, source in ipairs(energySources) do
            if source == "electricCharge" then
                hasElectric = true
            elseif source == "diesel" or source == "methane" then
                hasCombustion = true
            end
        end
    end

    if hasElectric and hasCombustion then
        return "hybrid"
    elseif hasElectric then
        return "electric"
    elseif hasCombustion then
        return "combustion"
    end

    return "unknown"
end

function FieldForceTelemetry:getEngineStarted(vehicle)
    if vehicle.getIsMotorStarted ~= nil then
        local ok, started = pcall(function()
            return vehicle:getIsMotorStarted()
        end)
        if ok then
            return started == true
        end
    end

    if vehicle.spec_motorized ~= nil and vehicle.spec_motorized.isMotorStarted ~= nil then
        return vehicle.spec_motorized.isMotorStarted == true
    end

    return nil
end

function FieldForceTelemetry:getMotorState(vehicle)
    if vehicle ~= nil and vehicle.getMotorState ~= nil then
        local ok, state = pcall(function()
            return vehicle:getMotorState()
        end)
        if ok then
            return state
        end
    end

    if vehicle ~= nil and vehicle.spec_motorized ~= nil then
        return vehicle.spec_motorized.motorState
    end

    return nil
end

function FieldForceTelemetry:isMotorState(state, name)
    if state == nil or MotorState == nil then
        return false
    end

    return state == MotorState[name]
end

function FieldForceTelemetry:getEngineStateText(motorState)
    if self:isMotorState(motorState, "OFF") then
        return "off"
    elseif self:isMotorState(motorState, "IGNITION") then
        return "ignition"
    elseif self:isMotorState(motorState, "STARTING") then
        return "starting"
    elseif self:isMotorState(motorState, "ON") then
        return "running"
    end

    return "unknown"
end

function FieldForceTelemetry:getMotorStartDurationMs(vehicle)
    local spec = vehicle ~= nil and vehicle.spec_motorized or nil
    return self:getFirstNumber(spec ~= nil and spec.motorStartDuration or nil)
end

function FieldForceTelemetry:getMotorStartRemainingMs(vehicle, motorState)
    if not self:isMotorState(motorState, "STARTING") then
        return nil
    end

    local spec = vehicle ~= nil and vehicle.spec_motorized or nil
    if spec == nil or type(spec.motorStartTime) ~= "number" or g_currentMission == nil or type(g_currentMission.time) ~= "number" then
        return nil
    end

    return math.max(0, spec.motorStartTime - g_currentMission.time)
end

function FieldForceTelemetry:getVehicleEventKey(vehicle)
    return self:getVehicleName(vehicle) .. ":" .. tostring(vehicle)
end

function FieldForceTelemetry:getVehicleEnginePulse(vehicle, name)
    if vehicle == nil or vehicle.fieldForce == nil then
        return nil
    end

    local pulse = vehicle.fieldForce[name]
    if type(pulse) ~= "table" or pulse.active ~= true then
        return nil
    end

    return pulse
end

function FieldForceTelemetry:markEngineStartCrank(vehicle, nowSeconds, durationMs)
    if vehicle == nil then
        return nil
    end

    vehicle.fieldForce = vehicle.fieldForce or {}
    vehicle.fieldForce.engineStartPulse = {
        active = true,
        startedAt = nowSeconds,
        durationMs = durationMs or 650
    }
    return vehicle.fieldForce.engineStartPulse
end

function FieldForceTelemetry:updateEngineEventState(vehicle, engineRunning, motorState, gear, nowSeconds, startDurationMs)
    local key = self:getVehicleEventKey(vehicle)
    local previousState = self.engineEventState
    local engineStartPulse = self:getVehicleEnginePulse(vehicle, "engineStartPulse")
    local engineStopPulse = self:getVehicleEnginePulse(vehicle, "engineStopPulse")
    local engineStartPulseStartedAt = engineStartPulse ~= nil and engineStartPulse.startedAt or nil
    local engineStopPulseStartedAt = engineStopPulse ~= nil and engineStopPulse.startedAt or nil
    if previousState == nil or previousState.key ~= key then
        self.engineEventState = {
            key = key,
            running = engineRunning,
            motorState = motorState,
            gear = gear,
            engineStartSeq = 0,
            lastEngineStartPulseStartedAt = engineStartPulseStartedAt,
            engineStopSeq = 0,
            lastEngineStopPulseStartedAt = engineStopPulseStartedAt,
            gearChangeSeq = 0,
            gearChangeKind = "none",
            gearChangeTimeMs = nil,
            lastGearChangeSeconds = nil,
            previousGear = nil
        }
        return self.engineEventState
    end

    local startingTransition = self:isMotorState(motorState, "STARTING") and not self:isMotorState(previousState.motorState, "STARTING")
    if startingTransition then
        if engineStartPulse == nil then
            engineStartPulse = self:markEngineStartCrank(vehicle, nowSeconds, startDurationMs)
            engineStartPulseStartedAt = engineStartPulse ~= nil and engineStartPulse.startedAt or nowSeconds
        end

        if engineStartPulseStartedAt ~= previousState.lastEngineStartPulseStartedAt then
            previousState.engineStartSeq = (previousState.engineStartSeq or 0) + 1
        end
        previousState.lastEngineStartPulseStartedAt = engineStartPulseStartedAt
    elseif engineStartPulseStartedAt ~= nil and engineStartPulseStartedAt ~= previousState.lastEngineStartPulseStartedAt then
        previousState.engineStartSeq = (previousState.engineStartSeq or 0) + 1
        previousState.lastEngineStartPulseStartedAt = engineStartPulseStartedAt
    end

    if engineStopPulseStartedAt ~= nil and engineStopPulseStartedAt ~= previousState.lastEngineStopPulseStartedAt then
        previousState.engineStopSeq = (previousState.engineStopSeq or 0) + 1
        previousState.lastEngineStopPulseStartedAt = engineStopPulseStartedAt
    end

    if gear ~= nil and previousState.gear ~= nil and gear ~= previousState.gear then
        previousState.previousGear = previousState.gear
        previousState.gearChangeSeq = (previousState.gearChangeSeq or 0) + 1
        previousState.gearChangeKind = gear > previousState.gear and "up" or "down"
        previousState.gearChangeTimeMs = previousState.lastGearChangeSeconds ~= nil and math.max(0, (nowSeconds - previousState.lastGearChangeSeconds) * 1000) or nil
        previousState.lastGearChangeSeconds = nowSeconds
    end

    previousState.running = engineRunning
    previousState.motorState = motorState
    previousState.gear = gear
    return previousState
end

function FieldForceTelemetry:getGear(vehicle)
    if vehicle == nil then
        return nil
    end

    local motorized = vehicle.spec_motorized
    local motor = motorized ~= nil and motorized.motor or nil
    local methodCandidates = {
        { target = vehicle, name = "getGear" },
        { target = vehicle, name = "getSelectedGear" },
        { target = vehicle, name = "getCurrentGear" },
        { target = motor, name = "getGear" },
        { target = motor, name = "getSelectedGear" },
        { target = motor, name = "getCurrentGear" }
    }

    for _, candidate in ipairs(methodCandidates) do
        if candidate.target ~= nil and type(candidate.target[candidate.name]) == "function" then
            local ok, value = pcall(function()
                return candidate.target[candidate.name](candidate.target)
            end)
            if ok and type(value) == "number" then
                return value
            end
        end
    end

    return self:getFirstNumber(
        vehicle.gear,
        vehicle.selectedGear,
        vehicle.currentGear,
        vehicle.activeGear,
        motorized ~= nil and motorized.gear or nil,
        motorized ~= nil and motorized.selectedGear or nil,
        motorized ~= nil and motorized.currentGear or nil,
        motor ~= nil and motor.gear or nil,
        motor ~= nil and motor.selectedGear or nil,
        motor ~= nil and motor.currentGear or nil,
        motor ~= nil and motor.lastGear or nil)
end

function FieldForceTelemetry:getTargetGear(vehicle)
    local motorized = vehicle ~= nil and vehicle.spec_motorized or nil
    local motor = motorized ~= nil and motorized.motor or nil
    return self:getFirstNumber(
        motorized ~= nil and motorized.targetGear or nil,
        motor ~= nil and motor.targetGear or nil,
        vehicle ~= nil and vehicle.targetGear or nil)
end

function FieldForceTelemetry:getGearGroup(vehicle)
    local motorized = vehicle ~= nil and vehicle.spec_motorized or nil
    local motor = motorized ~= nil and motorized.motor or nil
    local value = motorized ~= nil and (motorized.gearGroup or motorized.selectedGearGroup) or nil
    if value == nil and motor ~= nil then
        value = motor.gearGroup or motor.selectedGearGroup
    end

    return value ~= nil and tostring(value) or nil
end

function FieldForceTelemetry:getMass(vehicle)
    if vehicle.getMass ~= nil then
        local ok, mass = pcall(function()
            return vehicle:getMass()
        end)
        if ok then
            return mass
        end
    end

    return vehicle.mass
end

function FieldForceTelemetry:getTotalMass(vehicle)
    local ownMass = self:getMass(vehicle)
    if vehicle.getTotalMass ~= nil then
        local ok, mass = pcall(function()
            return vehicle:getTotalMass()
        end)
        if ok and type(mass) == "number" and (ownMass == nil or mass > ownMass + 0.01) then
            return mass
        end
    end

    return self:calculateAttachedTotalMass(vehicle, {}, ownMass)
end

function FieldForceTelemetry:calculateAttachedTotalMass(vehicle, visited, ownMass)
    if vehicle == nil then
        return ownMass
    end

    local key = tostring(vehicle)
    if visited[key] == true then
        return ownMass
    end
    visited[key] = true

    local total = ownMass or self:getMass(vehicle) or 0
    for _, attached in ipairs(self:getAttachedVehicles(vehicle)) do
        if attached ~= nil then
            total = total + (self:calculateAttachedTotalMass(attached, visited, self:getMass(attached)) or 0)
        end
    end

    return total > 0 and total or ownMass
end

function FieldForceTelemetry:getAttachedVehicles(vehicle)
    local result = {}
    local attacherSpec = vehicle ~= nil and vehicle.spec_attacherJoints or nil
    local attachedImplements = attacherSpec ~= nil and attacherSpec.attachedImplements or nil
    if type(attachedImplements) ~= "table" then
        return result
    end

    for _, implement in pairs(attachedImplements) do
        local object = implement ~= nil and (implement.object or implement.vehicle or implement.attachedObject) or nil
        if object ~= nil then
            table.insert(result, object)
        end
    end

    return result
end

function FieldForceTelemetry:getAttachmentTelemetry(vehicle)
    local result = {}
    self:collectAttachmentTelemetry(vehicle, vehicle, result, {}, 0)
    return result
end

function FieldForceTelemetry:collectAttachmentTelemetry(rootVehicle, vehicle, result, visited, depth)
    if vehicle == nil then
        return
    end

    local key = tostring(vehicle)
    if visited[key] == true then
        return
    end
    visited[key] = true

    for _, attached in ipairs(self:getAttachedVehicles(vehicle)) do
        if attached ~= nil then
            table.insert(result, {
                name = self:getVehicleName(attached),
                massT = self:kgToTons(self:getMass(attached)),
                totalMassT = self:kgToTons(self:getTotalMass(attached)),
                lateralOffsetM = self:getLateralOffsetM(rootVehicle, attached),
                depth = depth + 1
            })
            self:collectAttachmentTelemetry(rootVehicle, attached, result, visited, depth + 1)
        end
    end
end

function FieldForceTelemetry:getLateralOffsetM(rootVehicle, attached)
    local rootNode = self:getVehicleNode(rootVehicle)
    local attachedNode = self:getVehicleNode(attached)
    if rootNode ~= nil and attachedNode ~= nil and type(getWorldTranslation) == "function" and type(worldToLocal) == "function" then
        local okWorld, wx, wy, wz = pcall(getWorldTranslation, attachedNode)
        if okWorld then
            local okLocal, lx = pcall(worldToLocal, rootNode, wx, wy, wz)
            if okLocal and type(lx) == "number" then
                return lx
            end
        end
    end

    return 0
end

function FieldForceTelemetry:getIsOnField(vehicle)
    if vehicle.getIsOnField ~= nil then
        local ok, isOnField = pcall(function()
            return vehicle:getIsOnField()
        end)
        if ok then
            return isOnField == true
        end
    end

    return nil
end

function FieldForceTelemetry:getSurfaceTelemetry(vehicle)
    local result = {
        surfaceType = "unknown",
        surfaceAttribute = nil,
        groundWetness = nil,
        groundType = nil,
        groundDepth = nil
    }

    for _, wheel in ipairs(self:getVehicleWheels(vehicle)) do
        local physics = wheel.physics
        if physics ~= nil then
            local surfaceName, terrainAttribute, groundType, groundDepth = self:getWheelSurface(vehicle, physics)
            if result.surfaceAttribute == nil and terrainAttribute ~= nil then
                result.surfaceAttribute = terrainAttribute
            end
            if result.groundType == nil and groundType ~= nil then
                result.groundType = groundType
            end
            if result.groundDepth == nil and groundDepth ~= nil then
                result.groundDepth = groundDepth
            end

            if surfaceName ~= nil and surfaceName ~= "unknown" then
                result.surfaceType = surfaceName
                break
            end
        end
    end

    if result.surfaceType == "unknown" then
        local vehicleOnField = self:getIsOnField(vehicle)
        if vehicleOnField == true then
            result.surfaceType = "field"
        end
    end

    return result
end

function FieldForceTelemetry:getWheelSurface(vehicle, physics)
    local terrainAttribute, groundType, groundDepth = self:getWheelTerrainAttribute(physics)

    if physics.hasWaterContact == true then
        return "shallowWater", terrainAttribute, groundType, groundDepth
    end

    if physics.hasSnowContact == true then
        return "snow", terrainAttribute, groundType, groundDepth
    end

    if self:isGrassDensity(physics.densityType) then
        return "grass", terrainAttribute, groundType, groundDepth
    end

    if physics.getSurfaceSoundAttributes ~= nil then
        local ok, soundSurface, soundTerrainAttribute = pcall(function()
            return physics:getSurfaceSoundAttributes()
        end)
        if ok then
            if soundTerrainAttribute ~= nil then
                terrainAttribute = soundTerrainAttribute
            end

            local exact = self:normalizeExactSurfaceName(soundSurface)
            if exact ~= nil then
                return exact, terrainAttribute, groundType, groundDepth
            end
        end
    end

    local specSurface = self:getSurfaceFromVehicleSoundMap(vehicle, terrainAttribute)
    if specSurface ~= nil then
        return specSurface, terrainAttribute, groundType, groundDepth
    end

    if self:getWheelIsOnField(physics) == true then
        return self:normalizeGroundTypeSurface(groundType) or "field", terrainAttribute, groundType, groundDepth
    end

    return "unknown", terrainAttribute, groundType, groundDepth
end

function FieldForceTelemetry:normalizeExactSurfaceName(surfaceName)
    if surfaceName == nil then
        return nil
    end

    local value = string.lower(tostring(surfaceName))
    if value == "asphalt" then
        return "asphalt"
    elseif value == "field" then
        return "field"
    elseif value == "wetfield" then
        return "wetField"
    elseif value == "grass" then
        return "grass"
    elseif value == "shallowwater" then
        return "shallowWater"
    elseif value == "snow" then
        return "snow"
    elseif value == "dirt" or value == "gravel" or value == "mud" then
        return value
    end

    return nil
end

function FieldForceTelemetry:normalizeGroundTypeSurface(groundType)
    if groundType == nil then
        return nil
    end

    local value = string.lower(tostring(groundType))
    value = string.gsub(value, "[^%w]+", "")
    if string.find(value, "plow", 1, true) ~= nil then
        return "plowedField"
    elseif string.find(value, "cultivat", 1, true) ~= nil or string.find(value, "seedbed", 1, true) ~= nil then
        return "cultivatedField"
    elseif string.find(value, "wet", 1, true) ~= nil then
        return "wetField"
    elseif string.find(value, "field", 1, true) ~= nil then
        return "field"
    end

    return nil
end

function FieldForceTelemetry:getSurfaceFromVehicleSoundMap(vehicle, terrainAttribute)
    if terrainAttribute == nil or vehicle == nil or vehicle.spec_wheels == nil then
        return nil
    end

    local sound = vehicle.spec_wheels.surfaceIdToSound ~= nil and vehicle.spec_wheels.surfaceIdToSound[terrainAttribute] or nil
    if sound == nil then
        return nil
    end

    local candidates = {
        sound.sampleName,
        sound.name,
        sound.filename,
        sound.fileName
    }

    for _, candidate in ipairs(candidates) do
        local exact = self:normalizeExactSurfaceName(candidate)
        if exact ~= nil then
            return exact
        end
    end

    return nil
end

function FieldForceTelemetry:getWheelIsOnField(physics)
    if physics.getIsOnField ~= nil then
        local ok, isOnField = pcall(function()
            return physics:getIsOnField()
        end)
        if ok then
            return isOnField == true
        end
    end

    if physics.densityType ~= nil and FieldGroundType ~= nil then
        return physics.densityType ~= FieldGroundType.NONE and
            physics.densityType ~= FieldGroundType.GRASS and
            physics.densityType ~= FieldGroundType.GRASS_CUT
    end

    return nil
end

function FieldForceTelemetry:isGrassDensity(densityType)
    if densityType == nil or FieldGroundType == nil then
        return false
    end

    return densityType == FieldGroundType.GRASS or densityType == FieldGroundType.GRASS_CUT
end

function FieldForceTelemetry:getWheelTerrainAttribute(physics)
    if physics.lastTerrainAttribute ~= nil then
        return physics.lastTerrainAttribute, nil, nil
    end

    if physics.getGroundAttributes ~= nil then
        local ok, groundR, groundG, groundB, groundDepth, terrainAttribute, groundType = pcall(function()
            return physics:getGroundAttributes()
        end)
        if ok then
            return terrainAttribute, self:getGroundTypeName(groundType or groundR), groundDepth
        end
    end

    if WheelsUtil ~= nil and type(WheelsUtil.getGroundType) == "function" then
        local ok, groundType = pcall(function()
            return WheelsUtil.getGroundType(physics)
        end)
        if ok then
            return nil, self:getGroundTypeName(groundType), nil
        end
    end

    return nil, nil, nil
end

function FieldForceTelemetry:getGroundTypeName(value)
    if value == nil then
        return nil
    end

    if type(value) == "string" then
        return value
    end

    return tostring(value)
end

function FieldForceTelemetry:getWheelTelemetry(vehicle)
    local wheels = self:getVehicleWheels(vehicle)
    if #wheels == 0 then
        return {}
    end

    local slipTotal = 0
    local slipCount = 0
    local maxSlip = 0
    local contactCount = 0
    local steeringSlipTotal = 0
    local steeringSlipCount = 0
    local steeringContactCount = 0
    local steeringWheelCount = 0
    local leftContactCount = 0
    local rightContactCount = 0
    local leftWheelCount = 0
    local rightWheelCount = 0
    local leftSuspensionImpulse = 0
    local rightSuspensionImpulse = 0
    local leftSuspensionCount = 0
    local rightSuspensionCount = 0
    local tireTypes = {}
    local tireTypeSet = {}
    local wheelPackets = {}

    for index, wheel in ipairs(wheels) do
        local physics = wheel.physics
        local side = self:getWheelSide(vehicle, wheel)
        local isSteeringWheel = math.abs(self:getFirstNumber(wheel.steeringAngle, wheel.rotatedTime, wheel.steeringInput, wheel.steeringAxis) or 0) > 0.0001
        local slip = nil
        local hasContact = nil
        local suspensionImpulse = nil
        local wheelType = "unknown"
        local tireType = nil
        local tireProfile = "unknown"
        local surfaceType = nil
        local surfaceAttribute = nil
        local groundType = nil
        local groundDepth = nil
        local wheelIsOnField = nil
        if isSteeringWheel then
            steeringWheelCount = steeringWheelCount + 1
        end
        if side < 0 then
            leftWheelCount = leftWheelCount + 1
        elseif side > 0 then
            rightWheelCount = rightWheelCount + 1
        end

        if physics ~= nil then
            wheelType = self:getWheelType(wheel, physics)
            tireType = self:getWheelTireTypeName(physics)
            if tireType ~= nil and tireType ~= "" and tireTypeSet[tireType] ~= true then
                tireTypeSet[tireType] = true
                table.insert(tireTypes, tireType)
            end
            tireProfile = self:getWheelTireProfile({ tireType })
            surfaceType, surfaceAttribute, groundType, groundDepth = self:getWheelSurface(vehicle, physics)
            wheelIsOnField = self:getWheelIsOnField(physics)

            slip = physics.netInfo ~= nil and physics.netInfo.slip or nil
            if type(slip) == "number" then
                slip = math.max(0, slip)
                slipTotal = slipTotal + slip
                slipCount = slipCount + 1
                maxSlip = math.max(maxSlip, slip)
                if isSteeringWheel then
                    steeringSlipTotal = steeringSlipTotal + slip
                    steeringSlipCount = steeringSlipCount + 1
                end
            end

            hasContact = physics.hasGroundContact == true or physics.hasWaterContact == true or physics.hasSnowContact == true
            if hasContact then
                contactCount = contactCount + 1
                if isSteeringWheel then
                    steeringContactCount = steeringContactCount + 1
                end
                if side < 0 then
                    leftContactCount = leftContactCount + 1
                elseif side > 0 then
                    rightContactCount = rightContactCount + 1
                end
            end

            suspensionImpulse = self:getWheelSuspensionImpulse(wheel, physics)
            if suspensionImpulse ~= nil then
                if side < 0 then
                    leftSuspensionImpulse = math.max(leftSuspensionImpulse, suspensionImpulse)
                    leftSuspensionCount = leftSuspensionCount + 1
                elseif side > 0 then
                    rightSuspensionImpulse = math.max(rightSuspensionImpulse, suspensionImpulse)
                    rightSuspensionCount = rightSuspensionCount + 1
                end
            end
        end

        table.insert(wheelPackets, {
            index = index - 1,
            side = side < 0 and "left" or side > 0 and "right" or "center",
            isSteering = isSteeringWheel,
            slip = slip,
            hasGroundContact = hasContact,
            suspensionImpulse = suspensionImpulse,
            wheelType = wheelType,
            tireType = tireType,
            tireProfile = tireProfile,
            surfaceType = surfaceType,
            surfaceAttribute = surfaceAttribute,
            groundType = groundType,
            groundDepth = groundDepth,
            isOnField = wheelIsOnField
        })
    end

    return {
        wheels = wheelPackets,
        wheelSlip = slipCount > 0 and (slipTotal / slipCount) or nil,
        maxWheelSlip = slipCount > 0 and maxSlip or nil,
        groundContactRatio = #wheels > 0 and (contactCount / #wheels) or nil,
        steeringWheelSlip = steeringSlipCount > 0 and (steeringSlipTotal / steeringSlipCount) or nil,
        steeringGroundContactRatio = steeringWheelCount > 0 and (steeringContactCount / steeringWheelCount) or nil,
        leftContactRatio = leftWheelCount > 0 and (leftContactCount / leftWheelCount) or nil,
        rightContactRatio = rightWheelCount > 0 and (rightContactCount / rightWheelCount) or nil,
        leftSuspensionImpulse = leftSuspensionCount > 0 and leftSuspensionImpulse or nil,
        rightSuspensionImpulse = rightSuspensionCount > 0 and rightSuspensionImpulse or nil,
        wheelTireTypes = #tireTypes > 0 and table.concat(tireTypes, ",") or nil,
        wheelTireProfile = self:getWheelTireProfile(tireTypes)
    }
end

function FieldForceTelemetry:calculateSideSuspensionImpulse(bumpImpulse, wheelImpulse, sideContactRatio)
    if type(wheelImpulse) == "number" then
        return math.max(0, math.min(2, wheelImpulse))
    end

    if type(bumpImpulse) ~= "number" or type(sideContactRatio) ~= "number" then
        return nil
    end

    return math.max(0, math.min(2, bumpImpulse * math.max(0, math.min(1, sideContactRatio))))
end

function FieldForceTelemetry:calculateImpactImpulses(vehicle, motion, wheel)
    local verticalImpactImpulse = motion.bumpImpulse
    local wheelImpulse = math.max(wheel.leftSuspensionImpulse or 0, wheel.rightSuspensionImpulse or 0)
    local suspensionImpulse = wheelImpulse > 0 and wheelImpulse or verticalImpactImpulse
    local contactRatio = wheel.groundContactRatio
    local contact = type(contactRatio) == "number" and contactRatio > 0.20
    local localAx = motion.localAccelerationX or 0
    local localAz = motion.localAccelerationZ or 0
    local horizontalImpulse = math.min(math.sqrt((localAx * localAx) + (localAz * localAz)) / 9.81, 2)
    local lateralImpulse = math.abs(localAx) / 9.81
    local longitudinalImpulse = math.abs(localAz) / 9.81
    local speedKmh = motion.speedKmh or 0
    local hasCollisionShape = horizontalImpulse >= 1.55 or lateralImpulse >= longitudinalImpulse * 0.85
    local longitudinalJerkImpulse = horizontalImpulse
    local collisionImpulse = speedKmh >= 4 and
        horizontalImpulse >= 1.35 and
        hasCollisionShape and
        (verticalImpactImpulse == nil or horizontalImpulse > verticalImpactImpulse * 1.55) and
        horizontalImpulse or nil
    local landingImpulse = nil
    local key = tostring(vehicle)
    local previous = self.lastImpactState ~= nil and self.lastImpactState[key] or nil

    if self.lastImpactState == nil then
        self.lastImpactState = {}
    end

    if previous ~= nil and previous.contact == false and contact and verticalImpactImpulse ~= nil and verticalImpactImpulse >= 0.25 then
        landingImpulse = verticalImpactImpulse
    end

    self.lastImpactState[key] = {
        contact = contact,
        speedKmh = motion.speedKmh
    }

    if collisionImpulse ~= nil or (verticalImpactImpulse ~= nil and verticalImpactImpulse >= 0.20) or wheelImpulse >= 0.18 then
        longitudinalJerkImpulse = nil
    end

    return {
        suspensionImpulse = suspensionImpulse,
        verticalImpactImpulse = verticalImpactImpulse,
        landingImpulse = landingImpulse,
        collisionImpulse = collisionImpulse,
        longitudinalJerkImpulse = longitudinalJerkImpulse
    }
end

function FieldForceTelemetry:getWheelSide(vehicle, wheel)
    local side = self:getFirstNumber(wheel.positionX, wheel.x, wheel.restLoadX, wheel.widthOffset)
    if side ~= nil and math.abs(side) > 0.0001 then
        return side < 0 and -1 or 1
    end

    local node = wheel.node or wheel.repr or wheel.driveNode
    local vehicleNode = self:getVehicleNode(vehicle)
    if node ~= nil and vehicleNode ~= nil and type(getWorldTranslation) == "function" and type(worldToLocal) == "function" then
        local ok, wx, wy, wz = pcall(getWorldTranslation, node)
        if ok then
            local okLocal, lx = pcall(worldToLocal, vehicleNode, wx, wy, wz)
            if okLocal and type(lx) == "number" and math.abs(lx) > 0.0001 then
                return lx < 0 and -1 or 1
            end
        end
    end

    return 0
end

function FieldForceTelemetry:getWheelSuspensionImpulse(wheel, physics)
    local value = self:getFirstNumber(
        wheel.suspensionCompression,
        wheel.suspensionLoad,
        wheel.lastSuspensionCompression,
        physics.suspensionCompression,
        physics.suspensionLoad,
        physics.wheelLoad)
    if type(value) ~= "number" then
        return nil
    end

    if value < 0 then
        value = math.abs(value)
    end

    if value > 2 then
        value = value / 1000
    end

    return math.max(0, math.min(2, value))
end

function FieldForceTelemetry:getWheelType(wheel, physics)
    if wheel ~= nil and (wheel.isCrawler == true or wheel.crawlerIndex ~= nil or wheel.crawlerTrack ~= nil) then
        return "crawler"
    end

    if physics ~= nil and (physics.isCrawler == true or physics.crawlerIndex ~= nil) then
        return "crawler"
    end

    if wheel ~= nil or physics ~= nil then
        return "wheel"
    end

    return "unknown"
end

function FieldForceTelemetry:getWheelTireTypeName(physics)
    if physics == nil or physics.tireType == nil then
        return nil
    end

    local name = nil
    if WheelsUtil ~= nil and type(WheelsUtil.getTireTypeName) == "function" then
        local ok, result = pcall(function()
            return WheelsUtil.getTireTypeName(physics.tireType)
        end)
        if ok and result ~= nil then
            name = result
        end
    end

    if name == nil then
        name = physics.tireType
    end

    return self:normalizeTireTypeName(name)
end

function FieldForceTelemetry:normalizeTireTypeName(value)
    value = string.lower(tostring(value or ""))
    value = string.gsub(value, "[^%w]+", "")

    if value == "street" or value == "road" or value == "asphalt" or
        string.find(value, "street", 1, true) ~= nil or
        string.find(value, "road", 1, true) ~= nil or
        string.find(value, "asphalt", 1, true) ~= nil then
        return "street"
    elseif value == "offroad" or value == "offroadtires" or string.find(value, "offroad", 1, true) ~= nil then
        return "offRoad"
    elseif value == "mud" or value == "muds" or string.find(value, "mud", 1, true) ~= nil then
        return "mud"
    elseif value == "crawler" or value == "crawlers" or value == "tracked" or value == "track" or
        string.find(value, "crawler", 1, true) ~= nil or
        string.find(value, "track", 1, true) ~= nil then
        return "crawler"
    elseif value == "field" or value == "agricultural" or value == "agriculture" or
        string.find(value, "field", 1, true) ~= nil or
        string.find(value, "agric", 1, true) ~= nil then
        return "agricultural"
    elseif value == "" then
        return nil
    end

    return value
end

function FieldForceTelemetry:getWheelTireProfile(tireTypes)
    if tireTypes == nil or #tireTypes == 0 then
        return "unknown"
    end

    local hasStreet = false
    local hasAgricultural = false
    local hasTracked = false
    local hasUnknown = false

    for _, tireType in ipairs(tireTypes) do
        if tireType == "street" then
            hasStreet = true
        elseif tireType == "mud" or tireType == "offRoad" or tireType == "agricultural" then
            hasAgricultural = true
        elseif tireType == "crawler" then
            hasTracked = true
        else
            hasUnknown = true
        end
    end

    if hasTracked then
        return "tracked"
    elseif hasStreet and hasAgricultural then
        return "mixed"
    elseif hasAgricultural then
        return "agricultural"
    elseif hasStreet and not hasUnknown then
        return "street"
    end

    return "unknown"
end

function FieldForceTelemetry:getVehicleWheels(vehicle)
    if vehicle == nil or vehicle.spec_wheels == nil or vehicle.spec_wheels.wheels == nil then
        return {}
    end

    return vehicle.spec_wheels.wheels
end

function FieldForceTelemetry:getWeatherTelemetry()
    return {
        groundWetness = self:getFirstWeatherValue({
            "groundWetness",
            "currentGroundWetness",
            "wetness"
        }, {
            "getGroundWetness",
            "getWetness"
        }),
        rainScale = self:getFirstWeatherValue({
            "rainScale",
            "currentRainScale",
            "rainFallScale"
        }, {
            "getRainScale",
            "getRainFallScale"
        })
    }
end

function FieldForceTelemetry:getFirstWeatherValue(fieldNames, methodNames)
    local environment = g_currentMission ~= nil and g_currentMission.environment or nil
    local weather = environment ~= nil and environment.weather or nil
    local candidates = {}
    if environment ~= nil then
        table.insert(candidates, environment)
    end
    if weather ~= nil then
        table.insert(candidates, weather)
    end
    if g_currentMission ~= nil then
        table.insert(candidates, g_currentMission)
    end

    for _, candidate in ipairs(candidates) do
        for _, methodName in ipairs(methodNames) do
            if type(candidate[methodName]) == "function" then
                local ok, value = pcall(function()
                    return candidate[methodName](candidate)
                end)
                if ok and type(value) == "number" then
                    return math.max(0, math.min(1, value))
                end
            end
        end

        for _, fieldName in ipairs(fieldNames) do
            local value = candidate[fieldName]
            if type(value) == "number" then
                return math.max(0, math.min(1, value))
            end
        end
    end

    return nil
end

function FieldForceTelemetry:getMotionTelemetry(vehicle, stableSpeedKmh)
    local node = self:getVehicleNode(vehicle)
    if node == nil then
        return {}
    end

    local wx, wy, wz = self:getWorldTranslationSafe(node)
    local rx, ry, rz = self:getWorldRotationSafe(node)
    local pitchDeg = rx ~= nil and self:normalizeTiltDeg(math.deg(rx)) or nil
    local rollDeg = rz ~= nil and self:normalizeTiltDeg(math.deg(rz)) or nil
    local now = self:getMonotonicSeconds()
    local key = tostring(vehicle)
    local previous = self.lastVehicleMotion[key]
    local deltaSpeedKmh = nil
    local speedKmh = nil
    local yawRateDegPerSec = nil
    local localAccelerationX, localAccelerationY, localAccelerationZ = nil, nil, nil
    local bumpImpulse = nil

    if previous ~= nil and wx ~= nil and wy ~= nil and wz ~= nil then
        local dtSec = self:getDeltaSeconds(now, previous.time)
        if dtSec ~= nil and dtSec >= 0.004 and dtSec < 2 then
            local dx = wx - previous.x
            local dy = wy - previous.y
            local dz = wz - previous.z
            local vx = dx / dtSec
            local vy = dy / dtSec
            local vz = dz / dtSec
            local previousVx = type(previous.vx) == "number" and previous.vx or 0
            local previousVy = type(previous.vy) == "number" and previous.vy or 0
            local previousVz = type(previous.vz) == "number" and previous.vz or 0
            deltaSpeedKmh = self:speedFromDelta(dx, dz, dtSec)

            local stableMismatch = type(stableSpeedKmh) == "number" and
                type(deltaSpeedKmh) == "number" and
                math.abs(deltaSpeedKmh - stableSpeedKmh) > 20
            local deltaJump = type(previous.deltaSpeedKmh) == "number" and
                type(deltaSpeedKmh) == "number" and
                math.abs(deltaSpeedKmh - previous.deltaSpeedKmh) > 15
            local positionSpike = stableMismatch or deltaJump

            if not positionSpike then
                local ax = (vx - previousVx) / dtSec
                local ay = (vy - previousVy) / dtSec
                local az = (vz - previousVz) / dtSec
                speedKmh = deltaSpeedKmh

                localAccelerationX, localAccelerationY, localAccelerationZ = self:worldDirectionToLocalSafe(node, ax, ay, az)
                if localAccelerationX == nil then
                    localAccelerationX, localAccelerationY, localAccelerationZ = ax, ay, az
                end
                local rawLocalAccelerationY = localAccelerationY

                localAccelerationX = self:smoothMotionSample(previous.localAccelerationX, localAccelerationX, dtSec, self.motionAccelerationSmoothingSec)
                localAccelerationY = self:smoothMotionSample(previous.localAccelerationY, localAccelerationY, dtSec, self.motionAccelerationSmoothingSec)
                localAccelerationZ = self:smoothMotionSample(previous.localAccelerationZ, localAccelerationZ, dtSec, self.motionAccelerationSmoothingSec)

                if ry ~= nil and previous.yaw ~= nil then
                    yawRateDegPerSec = math.deg(self:angleDifference(ry, previous.yaw) / dtSec)
                end

                bumpImpulse = self:calculateVerticalImpactImpulse(rawLocalAccelerationY, localAccelerationY)
            end

            self.lastVehicleMotion[key] = {
                time = now,
                x = wx,
                y = wy,
                z = wz,
                yaw = ry or previous.yaw,
                vx = positionSpike and previousVx or vx,
                vy = positionSpike and previousVy or vy,
                vz = positionSpike and previousVz or vz,
                deltaSpeedKmh = positionSpike and previous.deltaSpeedKmh or deltaSpeedKmh,
                localAccelerationX = positionSpike and previous.localAccelerationX or localAccelerationX,
                localAccelerationY = positionSpike and previous.localAccelerationY or localAccelerationY,
                localAccelerationZ = positionSpike and previous.localAccelerationZ or localAccelerationZ
            }
        end
    elseif wx ~= nil and wy ~= nil and wz ~= nil then
        self.lastVehicleMotion[key] = {
            time = now,
            x = wx,
            y = wy,
            z = wz,
            yaw = ry,
            vx = 0,
            vy = 0,
            vz = 0,
            deltaSpeedKmh = nil,
            localAccelerationX = 0,
            localAccelerationY = 0,
            localAccelerationZ = 0
        }
    end

    return {
        speedKmh = speedKmh or ((wx ~= nil and wz ~= nil) and 0 or nil),
        deltaSpeedKmh = deltaSpeedKmh,
        pitchDeg = pitchDeg,
        rollDeg = rollDeg,
        yawRateDegPerSec = yawRateDegPerSec,
        slopeDeg = nil,
        localAccelerationX = localAccelerationX,
        localAccelerationY = localAccelerationY,
        localAccelerationZ = localAccelerationZ,
        bumpImpulse = bumpImpulse
    }
end

function FieldForceTelemetry:normalizeTiltDeg(deg)
    if type(deg) ~= "number" then
        return nil
    end

    local wrapped = ((deg + 180) % 360) - 180
    if wrapped > 90 then
        return wrapped - 180
    elseif wrapped < -90 then
        return wrapped + 180
    end

    return wrapped
end

function FieldForceTelemetry:smoothMotionSample(previousValue, currentValue, dtSec, smoothingSec)
    if type(currentValue) ~= "number" then
        return nil
    end

    if type(previousValue) ~= "number" or type(dtSec) ~= "number" or dtSec <= 0 then
        return currentValue
    end

    local smoothing = math.max(0, tonumber(smoothingSec) or 0)
    if smoothing <= 0 then
        return currentValue
    end

    local alpha = math.max(0, math.min(1, dtSec / (smoothing + dtSec)))
    return previousValue + ((currentValue - previousValue) * alpha)
end

function FieldForceTelemetry:calculateVerticalImpactImpulse(rawLocalAccelerationY, smoothedLocalAccelerationY)
    if type(rawLocalAccelerationY) ~= "number" then
        return nil
    end

    local rawAccelerationG = math.min(math.abs(rawLocalAccelerationY) / 9.81, 2)
    local smoothedAccelerationG = type(smoothedLocalAccelerationY) == "number" and math.min(math.abs(smoothedLocalAccelerationY) / 9.81, 2) or rawAccelerationG
    local deadband = math.max(0, math.min(1.5, tonumber(self.motionVerticalImpactDeadbandG) or 0))
    if rawAccelerationG <= deadband then
        return 0
    end

    if rawAccelerationG < 0.55 and smoothedAccelerationG <= deadband then
        return 0
    end

    return math.min(math.max(rawAccelerationG, smoothedAccelerationG) - deadband, 2)
end

function FieldForceTelemetry:speedFromDelta(dx, dz, dtSec)
    if type(dx) ~= "number" or type(dz) ~= "number" or type(dtSec) ~= "number" or dtSec <= 0 then
        return nil
    end

    local horizontalDistance = math.sqrt((dx * dx) + (dz * dz))
    local standstillDistance = (2 / 3.6) * dtSec
    if horizontalDistance < standstillDistance then
        return 0
    end

    return self:speedFromVelocity(dx / dtSec, dz / dtSec)
end

function FieldForceTelemetry:speedFromVelocity(vx, vz)
    if type(vx) ~= "number" or type(vz) ~= "number" then
        return nil
    end

    local speed = math.sqrt((vx * vx) + (vz * vz)) * 3.6
    if speed < 2 then
        return 0
    end

    return math.min(speed, 300)
end

function FieldForceTelemetry:getVehicleNode(vehicle)
    if vehicle.rootNode ~= nil then
        return vehicle.rootNode
    end

    if vehicle.components ~= nil and vehicle.components[1] ~= nil and vehicle.components[1].node ~= nil then
        return vehicle.components[1].node
    end

    if vehicle.componentNodes ~= nil and vehicle.componentNodes[1] ~= nil then
        return vehicle.componentNodes[1]
    end

    return nil
end

function FieldForceTelemetry:getWorldTranslationSafe(node)
    if type(getWorldTranslation) ~= "function" then
        return nil, nil, nil
    end

    local ok, x, y, z = pcall(getWorldTranslation, node)
    if ok then
        return x, y, z
    end

    return nil, nil, nil
end

function FieldForceTelemetry:getWorldRotationSafe(node)
    if type(getWorldRotation) ~= "function" then
        return nil, nil, nil
    end

    local ok, x, y, z = pcall(getWorldRotation, node)
    if ok then
        return x, y, z
    end

    return nil, nil, nil
end

function FieldForceTelemetry:worldDirectionToLocalSafe(node, x, y, z)
    if type(worldDirectionToLocal) ~= "function" then
        return nil, nil, nil
    end

    local ok, lx, ly, lz = pcall(worldDirectionToLocal, node, x, y, z)
    if ok then
        return lx, ly, lz
    end

    return nil, nil, nil
end

function FieldForceTelemetry:getDeltaSeconds(now, previous)
    if type(now) ~= "number" or type(previous) ~= "number" then
        return nil
    end

    local delta = now - previous
    if delta < 0 then
        return nil
    end

    if delta > 10 then
        return delta / 1000
    end

    return delta
end

function FieldForceTelemetry:shouldWriteFileTelemetry(packet)
    if not self.fileEnabled then
        return false
    end

    local nowMs = (packet ~= nil and type(packet._monotonicSeconds) == "number") and (packet._monotonicSeconds * 1000) or nil
    if nowMs == nil then
        return true
    end

    if self.lastFileWriteMs ~= nil and (nowMs - self.lastFileWriteMs) < self.intervalMs then
        return false
    end

    self.lastFileWriteMs = nowMs
    return true
end

function FieldForceTelemetry:recordSend(monotonicSeconds)
    if type(monotonicSeconds) ~= "number" then
        return
    end

    table.insert(self.sendTimes, monotonicSeconds)
    local threshold = monotonicSeconds - 1
    while #self.sendTimes > 0 and self.sendTimes[1] < threshold do
        table.remove(self.sendTimes, 1)
    end

    self.actualSendRate = #self.sendTimes
end

function FieldForceTelemetry:angleDifference(current, previous)
    local diff = current - previous
    while diff > math.pi do
        diff = diff - (math.pi * 2)
    end
    while diff < -math.pi do
        diff = diff + (math.pi * 2)
    end
    return diff
end

function FieldForceTelemetry:getOverlayEnabled()
    return self.overlayEnabled == true
end

function FieldForceTelemetry:setOverlayEnabled(enabled, doSave)
    self.overlayEnabled = enabled == true
    self:updateOverlaySettingUi()

    if doSave == true then
        self:saveOverlayUserSettings()
    end
end

function FieldForceTelemetry:loadOverlayUserSettings()
    local settingsPath = self:getOverlaySettingsPath()
    if settingsPath == nil then
        return
    end

    local ok, payloadOrError = pcall(function()
        local file = io.open(settingsPath, "r")
        if file == nil then
            return nil
        end

        local payload = file:read("*a")
        file:close()
        return payload
    end)

    if not ok then
        self:logFileWarning("Could not read overlay settings: " .. tostring(payloadOrError))
        return
    end

    local enabled = self:parseOverlayEnabledSetting(payloadOrError)
    if enabled ~= nil then
        self.overlayEnabled = enabled
    end
end

function FieldForceTelemetry:saveOverlayUserSettings()
    local basePath, pathError = self:getModSettingsPath()
    if basePath == nil then
        self:logFileWarning("Could not save overlay settings: " .. tostring(pathError or "could not resolve modSettings path"))
        return
    end

    self:createFolderIfPossible(basePath)

    local payload = string.format("{\"%s\":%s}", FieldForceTelemetry.SETTING_OVERLAY_ENABLED, self:getOverlayEnabled() and "true" or "false")
    local ok, writeError = self:writeFile(basePath .. "/" .. FieldForceTelemetry.SETTINGS_FILE_NAME, payload)
    if not ok then
        self:logFileWarning("Could not save overlay settings: " .. tostring(writeError))
    end
end

function FieldForceTelemetry:parseOverlayEnabledSetting(payload)
    if type(payload) ~= "string" then
        return nil
    end

    local key = FieldForceTelemetry.SETTING_OVERLAY_ENABLED
    if string.find(payload, "\"" .. key .. "\"%s*:%s*true") ~= nil then
        return true
    end
    if string.find(payload, "\"" .. key .. "\"%s*:%s*false") ~= nil then
        return false
    end

    return nil
end

function FieldForceTelemetry:draw()
    if not self.overlayEnabled then
        return
    end

    if type(renderText) ~= "function" then
        return
    end

    local ok, err = pcall(function()
        self:drawDebugOverlay()
    end)

    if not ok and self.debug then
        print("[FieldForce] Debug overlay draw failed: " .. tostring(err))
    end
end

function FieldForceTelemetry:drawDebugOverlay()
    local packet = self.lastPacket
    local x = tonumber(self.overlayConfig.x) or 0.015
    local y = tonumber(self.overlayConfig.y) or 0.965
    local width = tonumber(self.overlayConfig.width) or 0.32
    local padding = tonumber(self.overlayConfig.padding) or 0.008
    local fontSize = tonumber(self.overlayConfig.fontSize) or 0.014
    local lineHeight = tonumber(self.overlayConfig.lineHeight) or 0.018
    local lines = self:getOverlayLines(packet)

    self:drawOverlayContainer(x, y, width, padding, lineHeight, #lines)
    self:setOverlayTextStyle(false)

    for index, line in ipairs(lines) do
        if index == 1 then
            self:setOverlayTextStyle(true)
        elseif index == 2 then
            self:setOverlayTextStyle(false)
        end

        local textX = x + padding
        local textY = y - padding - ((index - 1) * lineHeight)

        renderText(textX, textY, fontSize, line)
    end

    self:resetOverlayTextStyle()
end

function FieldForceTelemetry:getOverlayLines(packet)
    local transport = self:getTransportLabel(false)
    local age = self:getPacketAgeText()
    local lines = {
        "FieldForce Telemetry",
        "transport: " .. transport,
        "source: " .. tostring(self.lastPacketSource or "none"),
        "age: " .. age,
        "configured: " .. tostring(self.updateRateHz) .. " Hz",
        "actual: " .. tostring(self.actualSendRate or 0) .. " pkt/s"
    }

    if self.lastWriteError ~= nil then
        table.insert(lines, "fileError: " .. self:truncateText(self.lastWriteError, 90))
    end

    if packet == nil then
        table.insert(lines, "timestamp: -")
        table.insert(lines, "gameState: -")
        table.insert(lines, "isPlayerInVehicle: -")
        table.insert(lines, "isDriver/passenger: - / -")
        table.insert(lines, "aiWorkerActive: -")
        table.insert(lines, "vehicleName: -")
        table.insert(lines, "vehicleType: -")
        table.insert(lines, "vehicleCategory: -")
        table.insert(lines, "wheelTireTypes: -")
        table.insert(lines, "wheelTireProfile: -")
        table.insert(lines, "speedKmh: -")
        if self.debug then
            table.insert(lines, "deltaSpeedKmh: -")
        end
        table.insert(lines, "steeringAngle: -")
        table.insert(lines, "rpm: -")
        table.insert(lines, "engineStarted: -")
        table.insert(lines, "mass: -")
        table.insert(lines, "totalMass: -")
        table.insert(lines, "isOnField: -")
        table.insert(lines, "surfaceType: -")
        table.insert(lines, "wet/rain: - / -")
        table.insert(lines, "slip/max: - / -")
        table.insert(lines, "pitch/roll/slope: - / - / -")
        table.insert(lines, "accel/bump: - / -")
        return lines
    end

    local vehicle = packet.vehicle or {}
    local motion = packet.motion or {}
    local steering = packet.steering or {}
    local engine = packet.engine or {}
    local surface = packet.surface or {}
    local environment = packet.environment or {}
    local suspension = packet.suspension or {}
    local accel = motion.localAccelerationMps2 or {}
    local player = packet.player or {}

    table.insert(lines, "timestamp: " .. self:formatNumber(packet.frame ~= nil and packet.frame.timestampMs or nil, "", 0))
    table.insert(lines, "gameState: " .. tostring(packet.game ~= nil and packet.game.state or "-"))
    table.insert(lines, "isPlayerInVehicle: " .. self:boolText(player.isInVehicle))
    table.insert(lines, "isDriver/passenger: " .. self:boolText(player.isDriver) .. " / " .. self:boolText(player.isPassenger))
    table.insert(lines, "aiWorkerActive: " .. self:boolText(vehicle.aiWorkerActive))
    table.insert(lines, "vehicleName: " .. tostring(vehicle.name or "-"))
    table.insert(lines, "vehicleType: " .. tostring(vehicle.type or "-"))
    table.insert(lines, "vehicleCategory: " .. tostring(vehicle.category or "-"))
    table.insert(lines, "wheelTireTypes: " .. tostring(vehicle.wheelTireTypes or "-"))
    table.insert(lines, "wheelTireProfile: " .. tostring(vehicle.wheelTireProfile or "-"))
    table.insert(lines, "speedKmh: " .. self:formatNumber(motion.speedKmh, "", 1))
    if self.debug then
        table.insert(lines, "deltaSpeedKmh: " .. self:formatNumber(self.lastMotionDeltaSpeedKmh, "", 1))
    end
    table.insert(lines, "steeringAngle: " .. self:formatNumber(steering.angle, "", 3))
    table.insert(lines, "rpm: " .. self:formatNumber(engine.rpm, "", 0))
    table.insert(lines, "engineStarted: " .. self:boolText(engine.started))
    table.insert(lines, "massT: " .. self:formatNumber(vehicle.massT, "", 2))
    table.insert(lines, "totalMassT: " .. self:formatNumber(vehicle.totalMassT, "", 2))
    table.insert(lines, "isOnField: " .. self:boolText(surface.isOnField))
    table.insert(lines, "surfaceType: " .. tostring(surface.type or "-") .. " attr " .. self:formatNumber(surface.attribute, "", 0))
    table.insert(lines, "wet/rain: " .. self:formatNumber(environment.groundWetness, "", 2) .. " / " .. self:formatNumber(environment.rainScale, "", 2))
    table.insert(lines, "wheels: " .. tostring(packet.wheels ~= nil and #packet.wheels or 0))
    table.insert(lines, "pitch/roll/slope: " .. self:formatNumber(motion.pitchDeg, "", 1) .. " / " .. self:formatNumber(motion.rollDeg, "", 1) .. " / " .. self:formatNumber(motion.slopeDeg, "", 1))
    table.insert(lines, "accelY/bump: " .. self:formatNumber(accel.y, "", 2) .. " / " .. self:formatNumber(suspension.verticalImpactImpulse, "", 2))

    return lines
end

function FieldForceTelemetry:drawOverlayContainer(x, y, width, padding, lineHeight, lineCount)
    local height = (lineCount * lineHeight) + (padding * 1.8)
    local color = self.overlayConfig.backgroundColor or { 0.02, 0.03, 0.025, 0.62 }
    local rectY = y - height + padding

    if type(drawFilledRect) == "function" then
        pcall(function()
            drawFilledRect(x, rectY, width, height, color[1], color[2], color[3], color[4])
        end)
    elseif type(renderFilledRect) == "function" then
        pcall(function()
            renderFilledRect(x, rectY, width, height, color[1], color[2], color[3], color[4])
        end)
    end
end

function FieldForceTelemetry:setOverlayTextStyle(isTitle)
    if type(setTextColor) == "function" then
        local color = isTitle and (self.overlayConfig.titleColor or { 1.0, 0.96, 0.78, 0.98 }) or
            (self.overlayConfig.textColor or { 0.88, 1.0, 0.82, 0.95 })
        setTextColor(color[1], color[2], color[3], color[4])
    end

    if type(setTextBold) == "function" then
        setTextBold(isTitle == true)
    end

    if type(RenderText) == "table" and type(setTextAlignment) == "function" and RenderText.ALIGN_LEFT ~= nil then
        setTextAlignment(RenderText.ALIGN_LEFT)
    end

    if type(RenderText) == "table" and type(setTextVerticalAlignment) == "function" and RenderText.VERTICAL_ALIGN_BASELINE ~= nil then
        setTextVerticalAlignment(RenderText.VERTICAL_ALIGN_BASELINE)
    end

    if type(setTextDepthTestEnabled) == "function" then
        setTextDepthTestEnabled(false)
    end
end

function FieldForceTelemetry:resetOverlayTextStyle()
    if type(setTextColor) == "function" then
        setTextColor(1, 1, 1, 1)
    end

    if type(setTextBold) == "function" then
        setTextBold(false)
    end
end

function FieldForceTelemetry:getPacketAgeText()
    if self.lastPacketTime == nil then
        return "none"
    end

    local now = self:getTimestamp()
    if type(now) ~= "number" or type(self.lastPacketTime) ~= "number" then
        return "unknown"
    end

    return string.format("%.0f ms", math.max(0, now - self.lastPacketTime))
end

function FieldForceTelemetry:getTransportLabel(sent)
    if self.udpEnabled and self.fileEnabled then
        return sent and "file+udp" or "file+udp ready"
    elseif self.udpEnabled then
        return sent and "udp" or "udp ready"
    elseif self.fileEnabled then
        return sent and "file" or "file ready"
    end

    return "disabled"
end

function FieldForceTelemetry:boolText(value)
    if value == nil then
        return "-"
    end

    return value and "yes" or "no"
end

function FieldForceTelemetry:formatNumber(value, suffix, decimals)
    if type(value) ~= "number" then
        return "-"
    end

    local format = "%." .. tostring(decimals or 0) .. "f"
    local text = string.format(format, value)
    if suffix ~= nil and suffix ~= "" then
        return text .. " " .. suffix
    end

    return text
end

function FieldForceTelemetry:truncateText(value, maxLength)
    value = tostring(value or "")
    maxLength = tonumber(maxLength) or 120
    if string.len(value) <= maxLength then
        return value
    end

    return string.sub(value, 1, math.max(1, maxLength - 3)) .. "..."
end

function FieldForceTelemetry:encodeJson(packet)
    return self:jsonValue(packet)
end

function FieldForceTelemetry:jsonValue(value)
    local valueType = type(value)
    if value == nil then
        return "null"
    elseif valueType == "number" then
        if value ~= value or value == math.huge or value == -math.huge then
            return "null"
        end
        return tostring(value)
    elseif valueType == "boolean" then
        return value and "true" or "false"
    elseif valueType == "table" then
        if value.__jsonArray == true then
            local items = {}
            for index = 1, #value do
                table.insert(items, self:jsonValue(value[index]))
            end
            return "[" .. table.concat(items, ",") .. "]"
        end

        local keys = {}
        for key, _ in pairs(value) do
            if type(key) == "string" and string.sub(key, 1, 1) ~= "_" then
                table.insert(keys, key)
            end
        end
        table.sort(keys)

        local parts = {}
        for _, key in ipairs(keys) do
            table.insert(parts, string.format("\"%s\":%s", self:escapeJson(key), self:jsonValue(value[key])))
        end

        return "{" .. table.concat(parts, ",") .. "}"
    end

    return "\"" .. self:escapeJson(tostring(value)) .. "\""
end

function FieldForceTelemetry:escapeJson(value)
    value = string.gsub(value, "\\", "\\\\")
    value = string.gsub(value, "\"", "\\\"")
    value = string.gsub(value, "\b", "\\b")
    value = string.gsub(value, "\f", "\\f")
    value = string.gsub(value, "\n", "\\n")
    value = string.gsub(value, "\r", "\\r")
    value = string.gsub(value, "\t", "\\t")
    value = string.gsub(value, "[%z\1-\31]", "")
    return value
end

function FieldForceTelemetry:getPackagePath(name)
    if package ~= nil and package[name] ~= nil then
        return tostring(package[name])
    end

    return "unavailable"
end

function FieldForceTelemetry:getFileApiError()
    if io == nil or type(io.open) ~= "function" then
        return "io.open is unavailable"
    end

    return nil
end

function FieldForceTelemetry:logTransportWarning(message)
    if self.debug or not self.transportWarningLogged then
        print("[FieldForce] " .. message)
    end

    self.transportWarningLogged = true
end

function FieldForceTelemetry:logFileWarning(message)
    if self.debug or not self.fileWarningLogged then
        print("[FieldForce] " .. message)
    end

    self.fileWarningLogged = true
end

function FieldForceTelemetry:getModSettingsPath()
    if type(getUserProfileAppPath) ~= "function" then
        return nil, "getUserProfileAppPath is unavailable"
    end

    local profilePath = nil
    local ok, path = pcall(getUserProfileAppPath)
    if ok then
        profilePath = path
    else
        return nil, "getUserProfileAppPath failed: " .. tostring(path)
    end

    if profilePath == nil or tostring(profilePath) == "" then
        return nil, "getUserProfileAppPath returned an empty path"
    end

    profilePath = string.gsub(tostring(profilePath), "\\", "/")
    profilePath = string.gsub(profilePath, "/$", "")
    return profilePath .. "/modSettings/FS25_FieldForceTelemetry"
end

function FieldForceTelemetry:getOverlaySettingsPath()
    if self:getFileApiError() ~= nil then
        return nil
    end

    local basePath = self:getModSettingsPath()
    if basePath == nil then
        return nil
    end

    return basePath .. "/" .. FieldForceTelemetry.SETTINGS_FILE_NAME
end

function FieldForceTelemetry:createFolderIfPossible(path)
    if type(createFolder) == "function" then
        local ok, result = pcall(createFolder, path)
        if not ok then
            self:logFileWarning("createFolder failed for " .. tostring(path) .. ": " .. tostring(result))
        end
    end
end

function FieldForceTelemetry:writeFile(path, payload)
    if os == nil or type(os.rename) ~= "function" or type(os.remove) ~= "function" then
        return self:writeFileDirect(path, payload, "atomic rename unavailable")
    end

    local tmpPath = path .. ".tmp"
    local ok, err = pcall(function()
        local file = io.open(tmpPath, "w")
        if file == nil then
            error("io.open returned nil for " .. tostring(tmpPath))
        end
        file:write(payload)
        file:flush()
        file:close()

        local removeOk, removeError = pcall(os.remove, path)
        if not removeOk and self.debug then
            print("[FieldForce] os.remove failed for " .. tostring(path) .. ": " .. tostring(removeError))
        end

        local renameOk, renameError = os.rename(tmpPath, path)
        if not renameOk then
            error("os.rename failed from " .. tostring(tmpPath) .. " to " .. tostring(path) .. ": " .. tostring(renameError))
        end
    end)

    if ok == true then
        return true, nil
    end

    return false, tostring(err)
end

function FieldForceTelemetry:writeFileDirect(path, payload, reason)
    local ok, err = pcall(function()
        local file = io.open(path, "w")
        if file == nil then
            error("io.open returned nil for " .. tostring(path))
        end

        file:write(payload)
        file:flush()
        file:close()
    end)

    if ok == true then
        if self.debug then
            print("[FieldForce] Wrote file telemetry without atomic rename: " .. tostring(reason))
        end
        return true, nil
    end

    return false, tostring(err)
end

function FieldForceTelemetry:installMenuHook()
    if FieldForceTelemetry.menuHookInstalled == true then
        return
    end

    if InGameMenu == nil or Utils == nil or type(Utils.appendedFunction) ~= "function" then
        return
    end

    InGameMenu.onMenuOpened = Utils.appendedFunction(InGameMenu.onMenuOpened, FieldForceTelemetry.initUiSettings)
    FieldForceTelemetry.menuHookInstalled = true
end

function FieldForceTelemetry.installVehicleTypeEventHook()
    if FieldForceTelemetry.vehicleTypeEventHookInstalled == true then
        return
    end

    if TypeManager == nil or TypeManager.validateTypes == nil or Utils == nil or type(Utils.appendedFunction) ~= "function" then
        return
    end

    TypeManager.validateTypes = Utils.appendedFunction(TypeManager.validateTypes, FieldForceTelemetry.registerVehicleEventListeners)
    FieldForceTelemetry.vehicleTypeEventHookInstalled = true

    if g_vehicleTypeManager ~= nil then
        FieldForceTelemetry.registerVehicleEventListeners(g_vehicleTypeManager)
    end
end

function FieldForceTelemetry.registerVehicleEventListeners(typeManager)
    if typeManager == nil or typeManager.typeName ~= "vehicle" or SpecializationUtil == nil or type(SpecializationUtil.registerEventListener) ~= "function" then
        return
    end

    local ok, err = pcall(function()
        local types = typeManager.getTypes ~= nil and typeManager:getTypes() or typeManager.types
        if types == nil then
            return
        end

        for _, typeEntry in pairs(types) do
            if typeEntry.fieldForceMotorEventsRegistered ~= true then
                SpecializationUtil.registerEventListener(typeEntry, "onStartMotor", FieldForceTelemetry)
                SpecializationUtil.registerEventListener(typeEntry, "onStopMotor", FieldForceTelemetry)
                typeEntry.fieldForceMotorEventsRegistered = true
            end
        end
    end)

    if not ok then
        print("[FieldForce] Could not register motor event listeners: " .. tostring(err))
    end
end

function FieldForceTelemetry:onStartMotor()
    self.fieldForce = self.fieldForce or {}

    self.fieldForce.engineStartPulse = {
        active = true,
        startedAt = g_currentMission ~= nil and g_currentMission.time or 0,
        durationMs = self.spec_motorized ~= nil and self.spec_motorized.motorStartDuration or 650
    }
end

function FieldForceTelemetry:onStopMotor()
    self.fieldForce = self.fieldForce or {}

    self.fieldForce.engineStopPulse = {
        active = true,
        startedAt = g_currentMission ~= nil and g_currentMission.time or 0,
        durationMs = 350
    }
end

function FieldForceTelemetry.initUiSettings()
    local instance = FieldForceTelemetry.instance
    if instance == nil then
        return
    end

    local ok, err = pcall(function()
        instance:registerUiSettings()
    end)

    if not ok then
        print("[FieldForce] Could not register settings UI: " .. tostring(err))
    end
end

function FieldForceTelemetry:registerUiSettings()
    if self.uiRegistered == true then
        return
    end

    if g_gui == nil or g_gui.screenControllers == nil or g_gui.screenControllers[InGameMenu] == nil then
        return
    end

    local settingsPage = g_gui.screenControllers[InGameMenu].pageSettings
    if settingsPage == nil or settingsPage.gameSettingsLayout == nil or settingsPage.checkWoodHarvesterAutoCutBox == nil then
        return
    end

    self.uiControls = {}
    self.uiSection = self:createSettingsSection(settingsPage, "fieldForceTelemetry_settings_title")
    if self.uiSection ~= nil then
        table.insert(self.uiControls, self.uiSection)
    end

    self.uiOverlayEnabled = self:createBoolSetting(
        settingsPage,
        "fieldForceTelemetry_overlayEnabled",
        "fieldForceTelemetry_overlayEnabled",
        "onOverlayEnabledChanged")

    if self.uiOverlayEnabled == nil then
        return
    end

    table.insert(self.uiControls, self.uiOverlayEnabled)
    self:updateOverlaySettingUi()
    self:registerSettingsFocusControls(settingsPage)

    if InGameMenuSettingsFrame ~= nil and Utils ~= nil and type(Utils.appendedFunction) == "function" then
        InGameMenuSettingsFrame.onFrameOpen = Utils.appendedFunction(InGameMenuSettingsFrame.onFrameOpen, function()
            self:updateOverlaySettingUi()
        end)
    end

    settingsPage.gameSettingsLayout:invalidateLayout()
    self.uiRegistered = true
end

function FieldForceTelemetry:createSettingsSection(settingsPage, i18nKey)
    for _, elem in ipairs(settingsPage.gameSettingsLayout.elements) do
        if elem.name == "sectionHeader" then
            local sectionTitle = elem:clone(settingsPage.gameSettingsLayout)
            sectionTitle:setText(self:getI18nText(i18nKey, "FieldForce"))
            sectionTitle.focusId = FocusManager:serveAutoFocusId()
            table.insert(settingsPage.controlsList, sectionTitle)
            return sectionTitle
        end
    end

    return nil
end

function FieldForceTelemetry:createBoolSetting(settingsPage, id, i18nKey, callbackName)
    local elementBox = settingsPage.checkWoodHarvesterAutoCutBox:clone(settingsPage.gameSettingsLayout)
    self:updateSettingsFocusIds(elementBox)

    elementBox.id = id .. "Box"

    local optionElement = elementBox.elements[1]
    optionElement.target = self
    optionElement.id = id
    optionElement:setDisabled(false)
    optionElement:setCallback("onClickCallback", callbackName)

    self.name = settingsPage.name

    local textElement = elementBox.elements[2]
    textElement:setText(self:getI18nText(i18nKey .. "_title", "Telemetry overlay"))

    local toolTip = optionElement.elements[1]
    if toolTip ~= nil then
        toolTip:setText(self:getI18nText(i18nKey .. "_info", "Shows or hides the in-game FieldForce telemetry overlay."))
    end

    table.insert(settingsPage.controlsList, elementBox)
    return elementBox
end

function FieldForceTelemetry:updateSettingsFocusIds(element)
    if element == nil then
        return
    end

    element.focusId = FocusManager:serveAutoFocusId()
    if element.elements ~= nil then
        for _, child in pairs(element.elements) do
            self:updateSettingsFocusIds(child)
        end
    end
end

function FieldForceTelemetry:registerSettingsFocusControls(settingsPage)
    if FocusManager == nil or Utils == nil or type(Utils.appendedFunction) ~= "function" then
        return
    end

    local controls = self.uiControls
    FocusManager.setGui = Utils.appendedFunction(FocusManager.setGui, function(_, gui)
        for _, control in ipairs(controls) do
            if control.focusId ~= nil and FocusManager.currentFocusData ~= nil and
                FocusManager.currentFocusData.idToElementMapping ~= nil and
                FocusManager.currentFocusData.idToElementMapping[control.focusId] == nil then
                FocusManager:loadElementFromCustomValues(control, nil, nil, false, false)
            end
        end

        if settingsPage.gameSettingsLayout ~= nil then
            settingsPage.gameSettingsLayout:invalidateLayout()
        end
    end)
end

function FieldForceTelemetry:onOverlayEnabledChanged(state)
    self:setOverlayEnabled(state == 2, true)
end

function FieldForceTelemetry:updateOverlaySettingUi()
    if self.uiOverlayEnabled == nil or self.uiOverlayEnabled.elements == nil or self.uiOverlayEnabled.elements[1] == nil then
        return
    end

    local optionElement = self.uiOverlayEnabled.elements[1]
    optionElement:setState(self:getOverlayEnabled() and 2 or 1)
end

function FieldForceTelemetry:getI18nText(key, fallback)
    if g_i18n ~= nil and type(g_i18n.getText) == "function" then
        local ok, text = pcall(function()
            return g_i18n:getText(key)
        end)
        if ok and text ~= nil and text ~= "" and text ~= key then
            return text
        end
    end

    return fallback
end

FieldForceTelemetry.installVehicleTypeEventHook()
FieldForceTelemetry.instance = FieldForceTelemetry.new()
addModEventListener(FieldForceTelemetry.instance)
