FS25RealFfbTelemetry = {}

local FS25RealFfbTelemetry_mt = Class(FS25RealFfbTelemetry)

function FS25RealFfbTelemetry.new()
    local self = setmetatable({}, FS25RealFfbTelemetry_mt)
    self.config = FS25RealFfbTelemetryConfig or {}
    self.host = self.config.host or "127.0.0.1"
    self.port = tonumber(self.config.port) or 34325
    self.updateRateHz = math.max(1, tonumber(self.config.updateRateHz) or 125)
    self.fileFallbackRateHz = math.max(1, tonumber(self.config.fileFallbackRateHz) or 30)
    self.intervalMs = 1000 / self.updateRateHz
    self.fileFallbackIntervalMs = 1000 / self.fileFallbackRateHz
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
    self.lastFileFallbackWriteMs = nil
    self.lastPacketSource = "none"
    self.lastWriteError = nil
    self.overlayConfig = self.config.overlay or {}
    self.overlayEnabled = self.overlayConfig.enabled ~= false
    self.fileWarningLogged = false
    self.transportWarningLogged = false
    self.lastVehicleMotion = {}
    self.lastImpactState = {}
    self.lastSteeringSample = nil
    return self
end

function FS25RealFfbTelemetry:loadMap()
    print(string.format("[FS25 Real FFB] Telemetry mod loaded. Target udp://%s:%d @ %d Hz", self.host, self.port, self.updateRateHz))
    self:initTransport()
end

function FS25RealFfbTelemetry:deleteMap()
    if self.socket ~= nil then
        pcall(function()
            self.socket:close()
        end)
    end

    self.socket = nil
    self.udpEnabled = false
    self.fileEnabled = false
    print("[FS25 Real FFB] Telemetry mod unloaded")
end

function FS25RealFfbTelemetry:update(dt)
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

function FS25RealFfbTelemetry:initTransport()
    print(string.format("[FS25 Real FFB] Initializing telemetry transport: udp://%s:%d", tostring(self.host), self.port))

    local okSocket, socketLib = pcall(require, "socket")
    if not okSocket or socketLib == nil then
        self:logTransportWarning(string.format(
            "Lua socket library is not available; UDP telemetry disabled. require('socket') error=%s; package.path=%s; package.cpath=%s",
            tostring(socketLib),
            self:getPackagePath("path"),
            self:getPackagePath("cpath")))
        self:initFileFallback()
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
        self:initFileFallback()
        return
    end

    self.socket = udpOrError
    self.udpEnabled = true
    print("[FS25 Real FFB] UDP telemetry enabled")
end

function FS25RealFfbTelemetry:initFileFallback()
    if self.config.fileFallback == false then
        self:logFileWarning("File telemetry fallback disabled by config")
        return
    end

    local ioError = self:getFileApiError()
    if ioError ~= nil then
        self:logFileWarning("File telemetry fallback unavailable: " .. ioError)
        return
    end

    if type(createFolder) ~= "function" then
        self:logFileWarning("createFolder is unavailable; file fallback will only work if the folder already exists")
    end

    local basePath, pathError = self:getModSettingsPath()
    if basePath == nil then
        self:logFileWarning("File telemetry fallback unavailable: " .. tostring(pathError or "could not resolve modSettings path"))
        return
    end

    self:createFolderIfPossible(basePath)
    self.filePath = basePath .. "/" .. tostring(self.config.fileName or "telemetry.json")

    local ok, writeError = self:writeFile(self.filePath, "{\"gameState\":\"init\",\"isPlayerInVehicle\":false}")
    if ok then
        self.fileEnabled = true
        print("[FS25 Real FFB] File telemetry fallback enabled: " .. self.filePath)
    else
        self:logFileWarning("File telemetry fallback unavailable: " .. tostring(writeError))
    end
end

function FS25RealFfbTelemetry:sendTelemetry()
    local packet = self:collectTelemetry()
    local payload = self:encodeJson(packet)
    local sent = false

    if self.udpEnabled then
        local ok, result = pcall(function()
            return self.socket:send(payload)
        end)

        if not ok or result == nil then
            self:logTransportWarning("UDP send failed: " .. tostring(result))
        elseif self.debug then
            sent = true
            print("[FS25 Real FFB] Sent UDP telemetry: " .. payload)
        else
            sent = true
        end
    end

    if self.fileEnabled and self.filePath ~= nil and self:shouldWriteFileFallback(packet) then
        local ok, writeError = self:writeFile(self.filePath, payload)
        if not ok then
            self.lastWriteError = tostring(writeError)
            self:logFileWarning("File telemetry write failed: " .. tostring(writeError))
        elseif self.debug then
            sent = true
            self.lastWriteError = nil
            print("[FS25 Real FFB] Wrote file telemetry: " .. payload)
        else
            sent = true
            self.lastWriteError = nil
        end
    end

    self.lastPacket = packet
    self.lastPayload = payload
    self.lastPacketTime = packet.timestamp
    self.lastSendTime = packet.monotonicSeconds
    self.lastPacketSource = self:getTransportLabel(sent)
    if sent then
        self:recordSend(packet.monotonicSeconds)
    end
end

function FS25RealFfbTelemetry:collectTelemetry()
    local vehicle = self:getActiveVehicle()
    local inVehicle = vehicle ~= nil
    local surface = inVehicle and self:getSurfaceTelemetry(vehicle) or {}
    local wheel = inVehicle and self:getWheelTelemetry(vehicle) or {}
    local motion = inVehicle and self:getMotionTelemetry(vehicle) or {}
    local impact = inVehicle and self:calculateImpactImpulses(vehicle, motion, wheel) or {}
    local weather = inVehicle and self:getWeatherTelemetry() or {}

    local steeringAngle = inVehicle and self:getSteeringAngle(vehicle) or nil

    return {
        timestamp = self:getTimestamp(),
        monotonicSeconds = self:getMonotonicSeconds(),
        gameState = self:getGameState(),
        isPlayerInVehicle = inVehicle,
        vehicleName = inVehicle and self:getVehicleName(vehicle) or nil,
        vehicleType = inVehicle and self:getVehicleType(vehicle) or nil,
        vehicleCategory = inVehicle and self:getVehicleCategory(vehicle, wheel.wheelTireProfile) or nil,
        wheelTireTypes = wheel.wheelTireTypes,
        wheelTireProfile = wheel.wheelTireProfile,
        speedKmh = inVehicle and (motion.speedKmh or self:getSpeedKmh(vehicle)) or nil,
        steeringAngle = steeringAngle,
        steeringRate = inVehicle and self:getSteeringRate(vehicle, steeringAngle) or nil,
        rpm = inVehicle and self:getRpm(vehicle) or nil,
        engineStarted = inVehicle and self:getEngineStarted(vehicle) or nil,
        mass = inVehicle and self:getMass(vehicle) or nil,
        totalMass = inVehicle and self:getTotalMass(vehicle) or nil,
        isOnField = inVehicle and self:getIsOnField(vehicle) or nil,
        surfaceType = surface.surfaceType,
        surfaceAttribute = surface.surfaceAttribute,
        groundWetness = surface.groundWetness or weather.groundWetness,
        rainScale = weather.rainScale,
        wheelSlip = wheel.wheelSlip,
        maxWheelSlip = wheel.maxWheelSlip,
        groundContactRatio = wheel.groundContactRatio,
        steeringGroundContactRatio = wheel.steeringGroundContactRatio,
        steeringWheelSlip = wheel.steeringWheelSlip,
        pitchDeg = motion.pitchDeg,
        rollDeg = motion.rollDeg,
        yawRateDegPerSec = motion.yawRateDegPerSec,
        slopeDeg = motion.slopeDeg,
        localAccelerationX = motion.localAccelerationX,
        localAccelerationY = motion.localAccelerationY,
        localAccelerationZ = motion.localAccelerationZ,
        bumpImpulse = impact.verticalImpactImpulse,
        suspensionImpulse = impact.suspensionImpulse,
        verticalImpactImpulse = impact.verticalImpactImpulse,
        landingImpulse = impact.landingImpulse,
        collisionImpulse = impact.collisionImpulse,
        longitudinalJerkImpulse = impact.longitudinalJerkImpulse,
        leftSuspensionImpulse = self:calculateSideSuspensionImpulse(impact.verticalImpactImpulse, wheel.leftSuspensionImpulse, wheel.leftContactRatio),
        rightSuspensionImpulse = self:calculateSideSuspensionImpulse(impact.verticalImpactImpulse, wheel.rightSuspensionImpulse, wheel.rightContactRatio),
        throttle = inVehicle and self:getFirstNumber(vehicle.axisForward, vehicle.spec_drivable ~= nil and vehicle.spec_drivable.axisForward or nil) or nil,
        brake = inVehicle and self:getFirstNumber(vehicle.axisBrake, vehicle.brakePedal, vehicle.spec_drivable ~= nil and vehicle.spec_drivable.axisBrake or nil) or nil,
        clutch = inVehicle and self:getFirstNumber(vehicle.axisClutch, vehicle.clutchPedal) or nil,
        gear = inVehicle and self:getFirstNumber(vehicle.gear, vehicle.selectedGear, vehicle.spec_motorized ~= nil and vehicle.spec_motorized.gear) or nil
    }
end

function FS25RealFfbTelemetry:getActiveVehicle()
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

function FS25RealFfbTelemetry:getForceFeedbackVehicle(vehicle)
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

function FS25RealFfbTelemetry:isDriveableTelemetrySource(vehicle)
    return vehicle ~= nil and
        (vehicle.spec_drivable ~= nil or
            vehicle.spec_motorized ~= nil or
            vehicle.getMotorRpm ~= nil or
            vehicle.getIsMotorStarted ~= nil)
end

function FS25RealFfbTelemetry:getTimestamp()
    if g_time ~= nil then
        return g_time
    end

    if os ~= nil and type(os.clock) == "function" then
        return os.clock()
    end

    return 0
end

function FS25RealFfbTelemetry:getMonotonicSeconds()
    if g_time ~= nil and type(g_time) == "number" then
        return g_time / 1000
    end

    if os ~= nil and type(os.clock) == "function" then
        return os.clock()
    end

    return 0
end

function FS25RealFfbTelemetry:getGameState()
    if g_currentMission == nil then
        return "noMission"
    end

    return "mission"
end

function FS25RealFfbTelemetry:getVehicleName(vehicle)
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

function FS25RealFfbTelemetry:getVehicleType(vehicle)
    if vehicle.typeName ~= nil then
        return tostring(vehicle.typeName)
    end

    if vehicle.typeDesc ~= nil then
        return tostring(vehicle.typeDesc)
    end

    return "Unknown"
end

function FS25RealFfbTelemetry:getVehicleCategory(vehicle, wheelTireProfile)
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
        local heavy = self:isHeavyTractorType(rawType)
        local tracked = self:hasActiveCrawlers(vehicle)

        if wheelTireProfile == "street" and not heavy and not tracked then
            return "Truck"
        end

        if heavy and tracked then
            return "HeavyTractorTracked"
        elseif heavy then
            return "HeavyTractorWheeled"
        elseif tracked then
            return "TractorTracked"
        end

        return "TractorWheeled"
    end

    return "Unknown"
end

function FS25RealFfbTelemetry:getVehicleTypeText(vehicle)
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

function FS25RealFfbTelemetry:isTractorType(value)
    return self:textHasAnyAlias(value, {
        "tractor",
        "tractors",
        "traktor",
        "traktoren"
    })
end

function FS25RealFfbTelemetry:isHeavyTractorType(value)
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

function FS25RealFfbTelemetry:isHarvesterType(value)
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

function FS25RealFfbTelemetry:isTruckType(value)
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

function FS25RealFfbTelemetry:isLoaderTelehandlerType(value)
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

function FS25RealFfbTelemetry:isLightVehicleType(value)
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

function FS25RealFfbTelemetry:textHasAnyAlias(value, patterns)
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

function FS25RealFfbTelemetry:normalizeAliasText(value)
    value = tostring(value or "")
    value = string.gsub(value, "([a-z])([A-Z])", "%1 %2")
    value = string.lower(value)
    value = string.gsub(value, "[^%w]+", " ")
    value = string.gsub(value, "%s+", " ")
    value = string.gsub(value, "^%s+", "")
    value = string.gsub(value, "%s+$", "")
    return value
end

function FS25RealFfbTelemetry:hasActiveCrawlers(vehicle)
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

function FS25RealFfbTelemetry:tableHasEntries(value)
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

function FS25RealFfbTelemetry:getSpeedKmh(vehicle)
    local speed = self:getRawSpeedKmh(vehicle)
    if speed ~= nil and speed >= 0 and speed < 300 then
        return speed
    end

    return nil
end

function FS25RealFfbTelemetry:getRawSpeedKmh(vehicle)
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

function FS25RealFfbTelemetry:getSteeringAngle(vehicle)
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

function FS25RealFfbTelemetry:getSteeringRate(vehicle, steeringAngle)
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

function FS25RealFfbTelemetry:getAverageWheelSteeringAngle(vehicle)
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

function FS25RealFfbTelemetry:getFirstNumber(...)
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

function FS25RealFfbTelemetry:getRpm(vehicle)
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

function FS25RealFfbTelemetry:getEngineStarted(vehicle)
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

function FS25RealFfbTelemetry:getMass(vehicle)
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

function FS25RealFfbTelemetry:getTotalMass(vehicle)
    if vehicle.getTotalMass ~= nil then
        local ok, mass = pcall(function()
            return vehicle:getTotalMass()
        end)
        if ok then
            return mass
        end
    end

    return self:getMass(vehicle)
end

function FS25RealFfbTelemetry:getIsOnField(vehicle)
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

function FS25RealFfbTelemetry:getSurfaceTelemetry(vehicle)
    local result = {
        surfaceType = "unknown",
        surfaceAttribute = nil,
        groundWetness = nil
    }

    for _, wheel in ipairs(self:getVehicleWheels(vehicle)) do
        local physics = wheel.physics
        if physics ~= nil then
            local surfaceName, terrainAttribute = self:getWheelSurface(vehicle, physics)
            if result.surfaceAttribute == nil and terrainAttribute ~= nil then
                result.surfaceAttribute = terrainAttribute
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

function FS25RealFfbTelemetry:getWheelSurface(vehicle, physics)
    local terrainAttribute = self:getWheelTerrainAttribute(physics)

    if physics.hasWaterContact == true then
        return "shallowWater", terrainAttribute
    end

    if physics.hasSnowContact == true then
        return "snow", terrainAttribute
    end

    if self:isGrassDensity(physics.densityType) then
        return "grass", terrainAttribute
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
                return exact, terrainAttribute
            end
        end
    end

    local specSurface = self:getSurfaceFromVehicleSoundMap(vehicle, terrainAttribute)
    if specSurface ~= nil then
        return specSurface, terrainAttribute
    end

    if self:getWheelIsOnField(physics) == true then
        return "field", terrainAttribute
    end

    return "unknown", terrainAttribute
end

function FS25RealFfbTelemetry:normalizeExactSurfaceName(surfaceName)
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

function FS25RealFfbTelemetry:getSurfaceFromVehicleSoundMap(vehicle, terrainAttribute)
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

function FS25RealFfbTelemetry:getWheelIsOnField(physics)
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

function FS25RealFfbTelemetry:isGrassDensity(densityType)
    if densityType == nil or FieldGroundType == nil then
        return false
    end

    return densityType == FieldGroundType.GRASS or densityType == FieldGroundType.GRASS_CUT
end

function FS25RealFfbTelemetry:getWheelTerrainAttribute(physics)
    if physics.lastTerrainAttribute ~= nil then
        return physics.lastTerrainAttribute
    end

    if physics.getGroundAttributes ~= nil then
        local ok, groundR, groundG, groundB, groundDepth, terrainAttribute = pcall(function()
            return physics:getGroundAttributes()
        end)
        if ok then
            return terrainAttribute
        end
    end

    return nil
end

function FS25RealFfbTelemetry:getWheelTelemetry(vehicle)
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

    for _, wheel in ipairs(wheels) do
        local physics = wheel.physics
        local side = self:getWheelSide(vehicle, wheel)
        local isSteeringWheel = math.abs(self:getFirstNumber(wheel.steeringAngle, wheel.rotatedTime, wheel.steeringInput, wheel.steeringAxis) or 0) > 0.0001
        if isSteeringWheel then
            steeringWheelCount = steeringWheelCount + 1
        end
        if side < 0 then
            leftWheelCount = leftWheelCount + 1
        elseif side > 0 then
            rightWheelCount = rightWheelCount + 1
        end

        if physics ~= nil then
            local tireType = self:getWheelTireTypeName(physics)
            if tireType ~= nil and tireType ~= "" and tireTypeSet[tireType] ~= true then
                tireTypeSet[tireType] = true
                table.insert(tireTypes, tireType)
            end

            local slip = physics.netInfo ~= nil and physics.netInfo.slip or nil
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

            local hasContact = physics.hasGroundContact == true or physics.hasWaterContact == true or physics.hasSnowContact == true
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

            local suspensionImpulse = self:getWheelSuspensionImpulse(wheel, physics)
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
    end

    return {
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

function FS25RealFfbTelemetry:calculateSideSuspensionImpulse(bumpImpulse, wheelImpulse, sideContactRatio)
    if type(wheelImpulse) == "number" then
        return math.max(0, math.min(2, wheelImpulse))
    end

    if type(bumpImpulse) ~= "number" or type(sideContactRatio) ~= "number" then
        return nil
    end

    return math.max(0, math.min(2, bumpImpulse * math.max(0, math.min(1, sideContactRatio))))
end

function FS25RealFfbTelemetry:calculateImpactImpulses(vehicle, motion, wheel)
    local verticalImpactImpulse = motion.bumpImpulse
    local wheelImpulse = math.max(wheel.leftSuspensionImpulse or 0, wheel.rightSuspensionImpulse or 0)
    local suspensionImpulse = wheelImpulse > 0 and wheelImpulse or verticalImpactImpulse
    local contactRatio = wheel.groundContactRatio
    local contact = type(contactRatio) == "number" and contactRatio > 0.20
    local localAx = motion.localAccelerationX or 0
    local localAz = motion.localAccelerationZ or 0
    local horizontalImpulse = math.min(math.sqrt((localAx * localAx) + (localAz * localAz)) / 9.81, 2)
    local longitudinalJerkImpulse = horizontalImpulse
    local collisionImpulse = horizontalImpulse >= 0.45 and (verticalImpactImpulse == nil or horizontalImpulse > verticalImpactImpulse * 1.35) and horizontalImpulse or nil
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

function FS25RealFfbTelemetry:getWheelSide(vehicle, wheel)
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

function FS25RealFfbTelemetry:getWheelSuspensionImpulse(wheel, physics)
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

function FS25RealFfbTelemetry:getWheelTireTypeName(physics)
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

function FS25RealFfbTelemetry:normalizeTireTypeName(value)
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

function FS25RealFfbTelemetry:getWheelTireProfile(tireTypes)
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

function FS25RealFfbTelemetry:getVehicleWheels(vehicle)
    if vehicle == nil or vehicle.spec_wheels == nil or vehicle.spec_wheels.wheels == nil then
        return {}
    end

    return vehicle.spec_wheels.wheels
end

function FS25RealFfbTelemetry:getWeatherTelemetry()
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

function FS25RealFfbTelemetry:getFirstWeatherValue(fieldNames, methodNames)
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

function FS25RealFfbTelemetry:getMotionTelemetry(vehicle)
    local node = self:getVehicleNode(vehicle)
    if node == nil then
        return {}
    end

    local wx, wy, wz = self:getWorldTranslationSafe(node)
    local rx, ry, rz = self:getWorldRotationSafe(node)
    local pitchDeg = rx ~= nil and math.deg(rx) or nil
    local rollDeg = rz ~= nil and math.deg(rz) or nil
    local slopeDeg = wx ~= nil and self:getSlopeDeg(wx, wy or 0, wz) or nil
    local now = self:getMonotonicSeconds()
    local key = tostring(vehicle)
    local previous = self.lastVehicleMotion[key]
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
            local ax = (vx - previous.vx) / dtSec
            local ay = (vy - previous.vy) / dtSec
            local az = (vz - previous.vz) / dtSec
            speedKmh = self:speedFromDelta(dx, dz, dtSec)

            localAccelerationX, localAccelerationY, localAccelerationZ = self:worldDirectionToLocalSafe(node, ax, ay, az)
            if localAccelerationX == nil then
                localAccelerationX, localAccelerationY, localAccelerationZ = ax, ay, az
            end

            if ry ~= nil and previous.yaw ~= nil then
                yawRateDegPerSec = math.deg(self:angleDifference(ry, previous.yaw) / dtSec)
            end

            bumpImpulse = localAccelerationY ~= nil and math.min(math.abs(localAccelerationY) / 9.81, 2) or nil

            self.lastVehicleMotion[key] = {
                time = now,
                x = wx,
                y = wy,
                z = wz,
                yaw = ry or previous.yaw,
                vx = vx,
                vy = vy,
                vz = vz
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
            vz = 0
        }
    end

    return {
        speedKmh = speedKmh or ((wx ~= nil and wz ~= nil) and 0 or nil),
        pitchDeg = pitchDeg,
        rollDeg = rollDeg,
        yawRateDegPerSec = yawRateDegPerSec,
        slopeDeg = slopeDeg,
        localAccelerationX = localAccelerationX,
        localAccelerationY = localAccelerationY,
        localAccelerationZ = localAccelerationZ,
        bumpImpulse = bumpImpulse
    }
end

function FS25RealFfbTelemetry:speedFromDelta(dx, dz, dtSec)
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

function FS25RealFfbTelemetry:speedFromVelocity(vx, vz)
    if type(vx) ~= "number" or type(vz) ~= "number" then
        return nil
    end

    local speed = math.sqrt((vx * vx) + (vz * vz)) * 3.6
    if speed < 2 then
        return 0
    end

    return math.min(speed, 300)
end

function FS25RealFfbTelemetry:getVehicleNode(vehicle)
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

function FS25RealFfbTelemetry:getWorldTranslationSafe(node)
    if type(getWorldTranslation) ~= "function" then
        return nil, nil, nil
    end

    local ok, x, y, z = pcall(getWorldTranslation, node)
    if ok then
        return x, y, z
    end

    return nil, nil, nil
end

function FS25RealFfbTelemetry:getWorldRotationSafe(node)
    if type(getWorldRotation) ~= "function" then
        return nil, nil, nil
    end

    local ok, x, y, z = pcall(getWorldRotation, node)
    if ok then
        return x, y, z
    end

    return nil, nil, nil
end

function FS25RealFfbTelemetry:worldDirectionToLocalSafe(node, x, y, z)
    if type(worldDirectionToLocal) ~= "function" then
        return nil, nil, nil
    end

    local ok, lx, ly, lz = pcall(worldDirectionToLocal, node, x, y, z)
    if ok then
        return lx, ly, lz
    end

    return nil, nil, nil
end

function FS25RealFfbTelemetry:getSlopeDeg(x, y, z)
    if type(getTerrainNormalAtWorldPos) ~= "function" then
        return nil
    end

    local terrain = g_terrainNode or (g_currentMission ~= nil and g_currentMission.terrainRootNode) or nil
    if terrain == nil then
        return nil
    end

    local ok, _, ny, _ = pcall(getTerrainNormalAtWorldPos, terrain, x, y, z)
    if ok and type(ny) == "number" then
        return math.deg(math.acos(math.max(-1, math.min(1, ny))))
    end

    return nil
end

function FS25RealFfbTelemetry:getDeltaSeconds(now, previous)
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

function FS25RealFfbTelemetry:shouldWriteFileFallback(packet)
    if not self.fileEnabled then
        return false
    end

    local nowMs = (packet ~= nil and type(packet.monotonicSeconds) == "number") and (packet.monotonicSeconds * 1000) or nil
    if nowMs == nil then
        return true
    end

    if self.lastFileFallbackWriteMs ~= nil and (nowMs - self.lastFileFallbackWriteMs) < self.fileFallbackIntervalMs then
        return false
    end

    self.lastFileFallbackWriteMs = nowMs
    return true
end

function FS25RealFfbTelemetry:recordSend(monotonicSeconds)
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

function FS25RealFfbTelemetry:angleDifference(current, previous)
    local diff = current - previous
    while diff > math.pi do
        diff = diff - (math.pi * 2)
    end
    while diff < -math.pi do
        diff = diff + (math.pi * 2)
    end
    return diff
end

function FS25RealFfbTelemetry:draw()
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
        print("[FS25 Real FFB] Debug overlay draw failed: " .. tostring(err))
    end
end

function FS25RealFfbTelemetry:drawDebugOverlay()
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

function FS25RealFfbTelemetry:getOverlayLines(packet)
    local transport = self:getTransportLabel(false)
    local age = self:getPacketAgeText()
    local lines = {
        "FS25 Real FFB Telemetry",
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
        table.insert(lines, "vehicleName: -")
        table.insert(lines, "vehicleType: -")
        table.insert(lines, "vehicleCategory: -")
        table.insert(lines, "wheelTireTypes: -")
        table.insert(lines, "wheelTireProfile: -")
        table.insert(lines, "speedKmh: -")
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

    table.insert(lines, "timestamp: " .. self:formatNumber(packet.timestamp, "", 0))
    table.insert(lines, "gameState: " .. tostring(packet.gameState or "-"))
    table.insert(lines, "isPlayerInVehicle: " .. self:boolText(packet.isPlayerInVehicle))
    table.insert(lines, "vehicleName: " .. tostring(packet.vehicleName or "-"))
    table.insert(lines, "vehicleType: " .. tostring(packet.vehicleType or "-"))
    table.insert(lines, "vehicleCategory: " .. tostring(packet.vehicleCategory or "-"))
    table.insert(lines, "wheelTireTypes: " .. tostring(packet.wheelTireTypes or "-"))
    table.insert(lines, "wheelTireProfile: " .. tostring(packet.wheelTireProfile or "-"))
    table.insert(lines, "speedKmh: " .. self:formatNumber(packet.speedKmh, "", 1))
    table.insert(lines, "steeringAngle: " .. self:formatNumber(packet.steeringAngle, "", 3))
    table.insert(lines, "rpm: " .. self:formatNumber(packet.rpm, "", 0))
    table.insert(lines, "engineStarted: " .. self:boolText(packet.engineStarted))
    table.insert(lines, "mass: " .. self:formatNumber(packet.mass, "", 0))
    table.insert(lines, "totalMass: " .. self:formatNumber(packet.totalMass, "", 0))
    table.insert(lines, "isOnField: " .. self:boolText(packet.isOnField))
    table.insert(lines, "surfaceType: " .. tostring(packet.surfaceType or "-") .. " attr " .. self:formatNumber(packet.surfaceAttribute, "", 0))
    table.insert(lines, "wet/rain: " .. self:formatNumber(packet.groundWetness, "", 2) .. " / " .. self:formatNumber(packet.rainScale, "", 2))
    table.insert(lines, "slip/max: " .. self:formatNumber(packet.wheelSlip, "", 2) .. " / " .. self:formatNumber(packet.maxWheelSlip, "", 2))
    table.insert(lines, "pitch/roll/slope: " .. self:formatNumber(packet.pitchDeg, "", 1) .. " / " .. self:formatNumber(packet.rollDeg, "", 1) .. " / " .. self:formatNumber(packet.slopeDeg, "", 1))
    table.insert(lines, "accelY/bump: " .. self:formatNumber(packet.localAccelerationY, "", 2) .. " / " .. self:formatNumber(packet.bumpImpulse, "", 2))

    return lines
end

function FS25RealFfbTelemetry:drawOverlayContainer(x, y, width, padding, lineHeight, lineCount)
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

function FS25RealFfbTelemetry:setOverlayTextStyle(isTitle)
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

function FS25RealFfbTelemetry:resetOverlayTextStyle()
    if type(setTextColor) == "function" then
        setTextColor(1, 1, 1, 1)
    end

    if type(setTextBold) == "function" then
        setTextBold(false)
    end
end

function FS25RealFfbTelemetry:getPacketAgeText()
    if self.lastPacketTime == nil then
        return "none"
    end

    local now = self:getTimestamp()
    if type(now) ~= "number" or type(self.lastPacketTime) ~= "number" then
        return "unknown"
    end

    return string.format("%.0f ms", math.max(0, now - self.lastPacketTime))
end

function FS25RealFfbTelemetry:getTransportLabel(sent)
    if self.udpEnabled and self.fileEnabled then
        return sent and "udp+file" or "udp+file ready"
    elseif self.udpEnabled then
        return sent and "udp" or "udp ready"
    elseif self.fileEnabled then
        return sent and "file" or "file ready"
    end

    return "disabled"
end

function FS25RealFfbTelemetry:boolText(value)
    if value == nil then
        return "-"
    end

    return value and "yes" or "no"
end

function FS25RealFfbTelemetry:formatNumber(value, suffix, decimals)
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

function FS25RealFfbTelemetry:truncateText(value, maxLength)
    value = tostring(value or "")
    maxLength = tonumber(maxLength) or 120
    if string.len(value) <= maxLength then
        return value
    end

    return string.sub(value, 1, math.max(1, maxLength - 3)) .. "..."
end

function FS25RealFfbTelemetry:encodeJson(packet)
    local fields = {
        "timestamp",
        "gameState",
        "isPlayerInVehicle",
        "vehicleName",
        "vehicleType",
        "vehicleCategory",
        "wheelTireTypes",
        "wheelTireProfile",
        "speedKmh",
        "steeringAngle",
        "steeringRate",
        "rpm",
        "engineStarted",
        "mass",
        "totalMass",
        "isOnField",
        "surfaceType",
        "surfaceAttribute",
        "groundWetness",
        "rainScale",
        "wheelSlip",
        "maxWheelSlip",
        "groundContactRatio",
        "steeringGroundContactRatio",
        "steeringWheelSlip",
        "pitchDeg",
        "rollDeg",
        "yawRateDegPerSec",
        "slopeDeg",
        "localAccelerationX",
        "localAccelerationY",
        "localAccelerationZ",
        "bumpImpulse",
        "suspensionImpulse",
        "verticalImpactImpulse",
        "landingImpulse",
        "collisionImpulse",
        "longitudinalJerkImpulse",
        "leftSuspensionImpulse",
        "rightSuspensionImpulse",
        "throttle",
        "brake",
        "clutch",
        "gear"
    }

    local parts = {}
    for _, key in ipairs(fields) do
        table.insert(parts, string.format("\"%s\":%s", key, self:jsonValue(packet[key])))
    end

    return "{" .. table.concat(parts, ",") .. "}"
end

function FS25RealFfbTelemetry:jsonValue(value)
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
    end

    return "\"" .. self:escapeJson(tostring(value)) .. "\""
end

function FS25RealFfbTelemetry:escapeJson(value)
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

function FS25RealFfbTelemetry:getPackagePath(name)
    if package ~= nil and package[name] ~= nil then
        return tostring(package[name])
    end

    return "unavailable"
end

function FS25RealFfbTelemetry:getFileApiError()
    if io == nil or type(io.open) ~= "function" then
        return "io.open is unavailable"
    end

    return nil
end

function FS25RealFfbTelemetry:logTransportWarning(message)
    if self.debug or not self.transportWarningLogged then
        print("[FS25 Real FFB] " .. message)
    end

    self.transportWarningLogged = true
end

function FS25RealFfbTelemetry:logFileWarning(message)
    if self.debug or not self.fileWarningLogged then
        print("[FS25 Real FFB] " .. message)
    end

    self.fileWarningLogged = true
end

function FS25RealFfbTelemetry:getModSettingsPath()
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
    return profilePath .. "/modSettings/FS25_RealFfbTelemetry"
end

function FS25RealFfbTelemetry:createFolderIfPossible(path)
    if type(createFolder) == "function" then
        local ok, result = pcall(createFolder, path)
        if not ok then
            self:logFileWarning("createFolder failed for " .. tostring(path) .. ": " .. tostring(result))
        end
    end
end

function FS25RealFfbTelemetry:writeFile(path, payload)
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
            print("[FS25 Real FFB] os.remove failed for " .. tostring(path) .. ": " .. tostring(removeError))
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

function FS25RealFfbTelemetry:writeFileDirect(path, payload, reason)
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
            print("[FS25 Real FFB] Wrote file telemetry without atomic rename: " .. tostring(reason))
        end
        return true, nil
    end

    return false, tostring(err)
end

addModEventListener(FS25RealFfbTelemetry.new())
