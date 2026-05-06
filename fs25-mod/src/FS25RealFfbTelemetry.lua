FS25RealFfbTelemetry = {}

local FS25RealFfbTelemetry_mt = Class(FS25RealFfbTelemetry)

function FS25RealFfbTelemetry.new()
    local self = setmetatable({}, FS25RealFfbTelemetry_mt)
    self.config = FS25RealFfbTelemetryConfig or {}
    self.host = self.config.host or "127.0.0.1"
    self.port = tonumber(self.config.port) or 34325
    self.updateRateHz = math.max(1, tonumber(self.config.updateRateHz) or 30)
    self.intervalMs = 1000 / self.updateRateHz
    self.debug = self.config.debug == true
    self.elapsedMs = 0
    self.socket = nil
    self.udpEnabled = false
    self.fileEnabled = false
    self.filePath = nil
    self.fileWarningLogged = false
    self.transportWarningLogged = false
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

    if self.udpEnabled then
        local ok, result = pcall(function()
            return self.socket:send(payload)
        end)

        if not ok or result == nil then
            self:logTransportWarning("UDP send failed: " .. tostring(result))
        elseif self.debug then
            print("[FS25 Real FFB] Sent UDP telemetry: " .. payload)
        end
    end

    if self.fileEnabled and self.filePath ~= nil then
        local ok, writeError = self:writeFile(self.filePath, payload)
        if not ok then
            self:logFileWarning("File telemetry write failed: " .. tostring(writeError))
        elseif self.debug then
            print("[FS25 Real FFB] Wrote file telemetry: " .. payload)
        end
    end
end

function FS25RealFfbTelemetry:collectTelemetry()
    local vehicle = self:getActiveVehicle()
    local inVehicle = vehicle ~= nil

    return {
        timestamp = self:getTimestamp(),
        gameState = self:getGameState(),
        isPlayerInVehicle = inVehicle,
        vehicleName = inVehicle and self:getVehicleName(vehicle) or nil,
        vehicleType = inVehicle and self:getVehicleType(vehicle) or nil,
        speedKmh = inVehicle and self:getSpeedKmh(vehicle) or nil,
        steeringAngle = inVehicle and self:getSteeringAngle(vehicle) or nil,
        rpm = inVehicle and self:getRpm(vehicle) or nil,
        engineStarted = inVehicle and self:getEngineStarted(vehicle) or nil,
        mass = inVehicle and self:getMass(vehicle) or nil,
        totalMass = inVehicle and self:getTotalMass(vehicle) or nil,
        isOnField = inVehicle and self:getIsOnField(vehicle) or nil
    }
end

function FS25RealFfbTelemetry:getActiveVehicle()
    local mission = g_currentMission
    if mission == nil then
        return nil
    end

    if mission.controlledVehicle ~= nil then
        return mission.controlledVehicle
    end

    if mission.player ~= nil and mission.player.getCurrentVehicle ~= nil then
        local ok, vehicle = pcall(function()
            return mission.player:getCurrentVehicle()
        end)
        if ok then
            return vehicle
        end
    end

    return nil
end

function FS25RealFfbTelemetry:getTimestamp()
    if g_time ~= nil then
        return g_time
    end

    return os.clock()
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

function FS25RealFfbTelemetry:getSpeedKmh(vehicle)
    if vehicle.getLastSpeed ~= nil then
        local ok, speed = pcall(function()
            return vehicle:getLastSpeed()
        end)
        if ok and speed ~= nil then
            return speed * 3600
        end
    end

    if vehicle.lastSpeedReal ~= nil then
        return vehicle.lastSpeedReal * 3600
    end

    return nil
end

function FS25RealFfbTelemetry:getSteeringAngle(vehicle)
    if vehicle.rotatedTime ~= nil then
        return vehicle.rotatedTime
    end

    if vehicle.spec_drivable ~= nil and vehicle.spec_drivable.axisSide ~= nil then
        return vehicle.spec_drivable.axisSide
    end

    return nil
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

function FS25RealFfbTelemetry:encodeJson(packet)
    local fields = {
        "timestamp",
        "gameState",
        "isPlayerInVehicle",
        "vehicleName",
        "vehicleType",
        "speedKmh",
        "steeringAngle",
        "rpm",
        "engineStarted",
        "mass",
        "totalMass",
        "isOnField"
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

    if os == nil then
        return "os library is unavailable"
    end

    if type(os.remove) ~= "function" then
        return "os.remove is unavailable"
    end

    if type(os.rename) ~= "function" then
        return "os.rename is unavailable"
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

addModEventListener(FS25RealFfbTelemetry.new())
