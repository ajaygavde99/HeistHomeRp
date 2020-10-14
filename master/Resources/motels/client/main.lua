ESX = nil

cachedData = {
	["motels"] = {},
	["storages"] = {},
	--["storageId"] = {},
	["furnishings"] = {},
	["keys"] = {}
}

--[[ Citizen.CreateThread(function()
	while not ESX do
		--Fetching esx library, due to new to esx using this.

		TriggerEvent("esx:getSharedObject", function(library) 
			ESX = library 
		end)

		Citizen.Wait(25)
	end

	if ESX.IsPlayerLoaded() then
		Init()
	end

	AddTextEntry("furnishing_instructions", Config.HelpTextMessage)

end) ]]

Citizen.CreateThread(function()
    while ESX == nil do
        TriggerEvent('esx:getSharedObject', function(obj)
            ESX = obj
        end)
        Citizen.Wait(0)
    end

    while ESX.GetPlayerData().job == nil do
		Citizen.Wait(10)
    end

	if ESX.IsPlayerLoaded() then
		Init()
	end

	ESX.PlayerData = ESX.GetPlayerData()
	
	AddTextEntry("furnishing_instructions", Config.HelpTextMessage)
end)

RegisterNetEvent("esx:playerLoaded")
AddEventHandler("esx:playerLoaded", function(playerData)
	ESX.PlayerData = playerData

	Init()
end)

RegisterNetEvent("esx:setJob")
AddEventHandler("esx:setJob", function(newJob)
	ESX.PlayerData["job"] = newJob
end)

AddEventHandler("onResourceStop", function(resource)
	if resource ~= GetCurrentResourceName() then return end

	RemoveSpawnedProps()
end)

RegisterNetEvent("james_motels:eventHandler")
AddEventHandler("james_motels:eventHandler", function(event, eventData)
	if event == "update_motels" then
		cachedData["motels"] = eventData
	elseif event == "update_storages" then
		cachedData["storages"][eventData["storageId"]] = eventData["newTable"]

		if ESX.UI.Menu.IsOpen("default", GetCurrentResourceName(), "main_storage_menu_" .. eventData["storageId"]) then
			local openedMenu = ESX.UI.Menu.GetOpened("default", GetCurrentResourceName(), "main_storage_menu_" .. eventData["storageId"])

			if openedMenu then
				openedMenu.close()

				OpenStorage(eventData["storageId"])
			end
		end
	elseif event == "invite_player" then
		if eventData["player"]["source"] == GetPlayerServerId(PlayerId()) then
			Citizen.CreateThread(function()
				local startedInvite = GetGameTimer()

				cachedData["invited"] = true

				while GetGameTimer() - startedInvite < 7500 do
					Citizen.Wait(0)

					ESX.ShowHelpNotification("You have been invited to the room # ".. eventData ["motel"] ["room"] ..". Enter with the ~ INPUT_DETONATE ~ key.")

					if IsControlJustPressed(0, 47) then
						EnterMotel(eventData["motel"])

						break
					end
				end

				cachedData["invited"] = false
			end)
		end
	elseif event == "knock_motel" then
		local currentInstance = DecorGetInt(PlayerPedId(), "currentInstance")

		if currentInstance and currentInstance == eventData["uniqueId"] then
			TriggerEvent('mythic_notify:client:SendAlert', { type = 'inform', text = 'Someone Knocked on the Door.' } ) 
		end
	elseif event == "update_furnishing" then
		local currentInstance = DecorGetInt(PlayerPedId(), "currentInstance")

		cachedData["furnishings"][eventData["motelId"]]["furnishing"] = eventData["furnishingData"]

		if currentInstance == eventData["motelId"] then
			SpawnFurnishing(currentInstance)
		end
	elseif event == "update_owned_furnishing" then
		if not cachedData["furnishings"][eventData["id"]] then cachedData["furnishings"][eventData["id"]] = {} end

		cachedData["furnishings"][eventData["id"]]["ownedFurnishing"] = eventData["newData"]
	else
		-- print("Wrong event handler.")
	end
end)

RegisterNetEvent("james_motels:storage")
AddEventHandler("james_motel:storage",function(event, eventData)
	if event == "update_motels" then
		cachedData["motels"] = eventData
	elseif event == "update_storages" then
		cachedData["storages"][eventData["storageId"]] = eventData["newTable"]
		if ESX.UI.Menu.IsOpen("default", GetCurrentResourceName(), "main_storage_menu_" .. eventData["storageId"]) then
			local openedMenu = ESX.UI.Menu.GetOpened("default", GetCurrentResourceName(), "main_storage_menu_" .. eventData["storageId"])

			if openedMenu then
				openedMenu.close()

				OpenStorage(eventData["storageId"])
			end
		end
	end
end)

Citizen.CreateThread(function()
	while true do
		Citizen.Wait(5)

		if IsControlJustPressed(0, 344) then  
			ShowKeyMenu()
		end
	end
end)

--[[ Citizen.CreateThread(function(eventData)
	local storageId = eventData
	while true do
		Citizen.Wait(5)
		if IsControlJustPressed(0, 47) then
			--print(eventData)
			OpenStorage(storageId)
		end
	end
end) ]]

--[[ Citizen.CreateThread(function()
	cachedData["storages"][storageId] = newTable
	while true do
		Citizen.Wait(5)
		if IsControlJustPressed(0, 47) then
			OpenStorage(eventData["storageId"])
		end
	end
end) ]]

--[[ Citizen.CreateThread(function()
	while true do
		Citizen.Wait(5)

		if IsControlJustPressed(0, 47) then
			TriggerEvent("james_motels:storage", event, eventData)
		end
	end
end) ]]

Citizen.CreateThread(function()
	Citizen.Wait(50)

	cachedData["lastCheck"] = GetGameTimer() - 4750

	CreateBlip()
	CreateBlip2()
	CreateBlip3()

	while true do
		local sleepThread = 500

		local ped = PlayerPedId()
		local pedCoords = GetEntityCoords(ped)

		local yourMotel = GetPlayerMotel()

		for motelRoom, motelPos in ipairs(Config.MotelsEntrances) do
			local playerRoom = yourMotel and (yourMotel["room"] == motelRoom)

			local dstCheck = #(pedCoords - motelPos)
			local dstRange = playerRoom and 35.0 or 3.0

			if dstCheck <= dstRange then
				sleepThread = 5

				DrawScriptMarker({
					["type"] = 2,
					["pos"] = motelPos,
					["r"] = not playerRoom and 150 or 0,
					["g"] = playerRoom and 150 or 0,
					["b"] = 0,
					["sizeX"] = 0.3,
					["sizeY"] = 0.3,
					["sizeZ"] = 0.3,
					["rotate"] = true
				})

				if dstCheck <= 0.8 then
					local displayText = "[~g~E~s~] Manage #" .. motelRoom .. (playerRoom and " [~g~G~s~] Enter" or "")

					if IsControlJustPressed(0, 38) then
						OpenMotelRoomMenu(motelRoom)
					elseif IsControlJustPressed(0, 47) then
						if playerRoom then
							EnterMotel(yourMotel)
						end
					end

					DrawScriptText(motelPos + vector3(0.0, 0.0, 0.5), displayText)
				end
			end
		end

		local dstCheck = #(pedCoords - Config.LandLord["position"])
		local dstCheck2 = #(pedCoords - Config.LandLord["position2"])

		if dstCheck <= 3.0 then
			sleepThread = 5

			local displayText = "Property owner"

			if dstCheck <= 0.9 then
				displayText = "[~g~E~s~] " .. displayText

				if IsControlJustPressed(0, 38) then
					OpenLandLord()
				end
			end

			DrawScriptText(Config.LandLord["position"], displayText)
		end		
		if dstCheck2 <= 3.0 then
			sleepThread = 5

			local displayText = "Property owner"

			if dstCheck2 <= 0.9 then
				displayText = "[~g~E~s~] " .. displayText

				if IsControlJustPressed(0, 38) then
					OpenLandLord()
				end
			end

			DrawScriptText(Config.LandLord["position2"], displayText)
		end

		Citizen.Wait(sleepThread)
	end
end)