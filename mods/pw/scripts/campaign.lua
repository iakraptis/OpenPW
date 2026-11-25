--[[
	Copyright (c) The OpenPW Developers and Contributors
	This file is part of OpenPW, which is free software. It is made
	available to you under the terms of the GNU General Public License
	as published by the Free Software Foundation, either version 3 of
	the License, or (at your option) any later version. For more
	information, see COPYING.
]]

-- Runs a converted campaign scenario the way Pw.exe does (docs/formats/campaign.md). The installer writes each
-- mission's script to its mission.lua (AssetInstaller/Campaign.cs): the players, teams of placed units, locations
-- (pixel rectangles of the original map) and up to 256 triggers of one condition and one action each.
--
-- Every tick, in trigger order (FUN_00404490): a trigger that is not done and whose condition holds runs its action
-- and is done, with the tick it was done at. A transmission keeps its trigger running until its text has been shown.
-- Conditions (FUN_00404650) and actions (FUN_00405170) go by their number in the original.

local Border = 1
local Now = 0
local Done, FiredAt, ShowUntil = { }, { }, { }
local TeamTimers = { }
local Counter = 0
local VictoryPending = false
local VictoryTrigger = false
local Ended = false
local Human, Objective
local Reported = { }

local function Report(kind, number)
	if not Reported[kind .. number] then
		Reported[kind .. number] = true
		print("campaign: " .. kind .. " " .. number .. " is not supported yet")
	end
end

local function GetPlayer(index)
	local name = Mission.Players[index]
	return name and Player.GetPlayer(name)
end

-- Original pixels and world positions: 32 pixels to a cell, the map moved by its one-cell border.
local function ToWorld(x, y)
	return WPos.New(math.floor(x * 32) + 1024 * Border, math.floor(y * 32) + 1024 * Border, 0)
end

local function ToCell(x, y)
	return CPos.New(math.floor(x / 32) + Border, math.floor(y / 32) + Border)
end

local function Pixel(actor)
	local p = actor.CenterPosition
	return (p.X - 1024 * Border) / 32, (p.Y - 1024 * Border) / 32
end

local function Centre(location)
	local r = Mission.Locations[location]
	if r then
		return (r[1] + r[3]) / 2, (r[2] + r[4]) / 2
	end
end

local function Inside(actor, location)
	local r = Mission.Locations[location]
	if not r or actor.IsDead then
		return false
	end

	local x, y = Pixel(actor)
	return r[1] <= x and x <= r[3] and r[2] <= y and y <= r[4]
end

-- Placed units that action 19 turned into another type: the new actor, by the placed one's name.
local Replaced = { }

local function Member(name)
	return Replaced[name] or Map.NamedActor(name)
end

-- The living members of a team (the original drops the dead ones every tick).
local function Team(index)
	local members = { }
	for _, name in ipairs(Mission.Teams[index] or { }) do
		local actor = Member(name)
		if actor and not actor.IsDead and actor.IsInWorld then
			members[#members + 1] = actor
		end
	end

	return members
end

local function TeamCentre(index)
	local members = Team(index)
	if #members == 0 then
		return nil
	end

	local x, y = 0, 0
	for _, actor in ipairs(members) do
		local px, py = Pixel(actor)
		x, y = x + px, y + py
	end

	return x / #members, y / #members
end

-- Every unit and building in a location, of any owner.
local function UnitsAt(location)
	local r = Mission.Locations[location]
	if not r then
		return { }
	end

	return Map.ActorsInBox(ToWorld(r[1], r[2]), ToWorld(r[3], r[4]), function(actor)
		return not actor.IsDead and actor.HasProperty("Health") and actor.Owner.Name ~= "Neutral"
	end)
end

local function Command(actors, location)
	local x, y = Centre(location)
	if not x then
		return
	end

	for _, actor in ipairs(actors) do
		if actor.HasProperty("AttackMove") then
			actor.Stop()
			actor.AttackMove(ToCell(x, y), 2)
		elseif actor.HasProperty("Move") then
			actor.Stop()
			actor.Move(ToCell(x, y), 2)
		end
	end
end

local function Warp(actors, dx, dy)
	for _, actor in ipairs(actors) do
		local x, y = Pixel(actor)
		actor.Teleport(ToCell(x + dx, y + dy))
	end
end

local function Look(x, y)
	if x then
		Camera.Position = ToWorld(x, y)
	end
end

-- Without a window (a headless run of the mission), the game ends with the mission: past it, the player watches as
-- a spectator, and the simulation slows to a crawl.
local function EndHeadless(won)
	if Test and Test.Headless then
		local line = "campaign: " .. (won and "won" or "lost") .. " at tick " .. Now
		if won then Test.Pass(line) else Test.Fail(line) end
	end
end

local function Win()
	if Ended then
		return
	end

	Ended = true

	-- The ending story, read out by the map's "outro" music track; the mission is won when it has been told (or after
	-- two minutes, should the music not play).
	local won = false
	local function Complete()
		if not won then
			won = true
			Human.MarkCompletedObjective(Objective)
			EndHeadless(true)
		end
	end

	if Mission.Ending ~= "" then
		Media.DisplayMessage(Mission.Ending, "")
	end

	Media.PlayMusic("outro", Complete)
	Trigger.AfterDelay(DateTime.Minutes(2), Complete)
end

local function Lose()
	if Ended then
		return
	end

	Ended = true
	Human.MarkFailedObjective(Objective)
	EndHeadless(false)
end

local Conditions = {
	[0] = function() return true end,
	[1] = function(a) return Now >= a[1] end,
	[14] = function(a)
		for _, actor in ipairs(Team(a[1])) do
			if Inside(actor, a[2]) then
				return true
			end
		end

		return false
	end,
	[15] = function(a)
		if Now - (TeamTimers[a[1]] or 0) < a[2] then
			return false
		end

		TeamTimers[a[1]] = Now
		return true
	end,
	[16] = function(a)
		local count = 0
		for _, actor in ipairs(Team(a[2])) do
			if Inside(actor, a[3]) then
				count = count + 1
			end
		end

		return count >= a[1]
	end,
	[17] = function(a) return #Team(a[1]) == 0 end,
	[18] = function(a) return #Team(a[2]) <= a[1] end,
	[19] = function() return VictoryPending end,
	[20] = function(a) return Done[a[1]] == true and Now - FiredAt[a[1]] >= a[2] end,
	[21] = function(a) return Counter == a[1] end,
}

-- Each returns true while its trigger has to keep running.
local Actions = {
	[0] = function(a) Look(Centre(a[1])) end,
	[1] = function(a, trigger, index)
		if not ShowUntil[index] then
			Media.DisplayMessage(trigger.Text, trigger.Speaker)
			ShowUntil[index] = Now + trigger.Duration
		end

		return Now < ShowUntil[index]
	end,
	-- Puts a ground object of type a2 - 1000 at location a3's centre (FUN_00462b10, the OBI pool): in the campaign, a story
	-- item (Item.itm 18 the Sword of Jurrasic, 22 the Sorcerous of the Neuully; rules/campaign.yaml). The human's
	-- units (or their allies') take it by walking onto it.
	[2] = function(a)
		local x, y = Centre(a[3])
		if a[2] < 1000 or not x then
			return
		end

		local item = Actor.Create(string.format("story-item-%02d", a[2] - 1000), true,
			{ Owner = Player.GetPlayer("Neutral"), Location = ToCell(x, y) })
		Trigger.OnEnteredProximityTrigger(item.CenterPosition, WDist.New(768), function(unit, id)
			if not item.IsDead and unit.HasProperty("Move") and (unit.Owner == Human or unit.Owner.IsAlliedWith(Human)) then
				Trigger.RemoveProximityTrigger(id)
				item.Destroy()
			end
		end)
	end,
	[3] = function()
		-- With a trigger waiting for the victory, the win only lets that trigger run; its chain ends with a victory.
		if VictoryTrigger and not VictoryPending then
			VictoryPending = true
		else
			Win()
		end
	end,
	[4] = Lose,
	[5] = Lose,
	[6] = function(a)
		local owner = GetPlayer(a[5])
		if owner then
			for _, actor in ipairs(UnitsAt(a[4])) do
				actor.Owner = owner
			end
		end
	end,
	[7] = function(a)
		for _, actor in ipairs(UnitsAt(a[4])) do
			actor.Kill()
		end
	end,
	[9] = function(a)
		local fx, fy = Centre(a[4])
		local tx, ty = Centre(a[5])
		if fx and tx then
			Warp(UnitsAt(a[4]), tx - fx, ty - fy)
		end
	end,
	[10] = function(a) Command(Team(a[2]), a[3]) end,
	[13] = function(a, trigger) Media.DisplayMessage(trigger.Text, "Objective") end,
	[15] = function(a)
		local player = GetPlayer(a[1])
		if player then
			player.Cash = a[2]
		end
	end,
	[16] = function(a) Command(Team(a[1]), a[2]) end,
	[17] = function(a) Done[a[1]] = false end,
	[18] = function(a) Done[a[1]] = true end,
	-- The units of type a1 in team a3 owned by player a4 become type a2 owned by player a5, at full health (the original
	-- copies the new type's stats over, FUN_00405170 case 0x13). A new type is a new actor in the old one's place.
	[19] = function(a)
		local from, to = Mission.Types[a[1]], Mission.Types[a[2]]
		local owner, newOwner = GetPlayer(a[4]), GetPlayer(a[5])
		if not from or not to or not owner or not newOwner then
			return
		end

		for _, name in ipairs(Mission.Teams[a[3]] or { }) do
			local actor = Member(name)
			if actor and not actor.IsDead and actor.IsInWorld and actor.Type == from and actor.Owner == owner then
				if from == to then
					actor.Owner = newOwner
					actor.Health = actor.MaxHealth
				else
					Replaced[name] = Actor.Create(to, true, { Owner = newOwner, Location = actor.Location, Facing = actor.Facing })
					actor.Destroy()
				end
			end
		end
	end,
	[20] = function(a)
		local members = Team(a[1])
		if #members > 0 then
			Look(Pixel(members[1]))
		end
	end,
	[21] = function(a)
		for _, actor in ipairs(Team(a[1])) do
			actor.Kill()
		end
	end,
	-- 22 and 23 set and clear unit flags of a team around scripted scenes (Pw.exe +0xa0 bits 0 and 7, +0x58 bit 5);
	-- what they change in play is not known yet.
	[22] = function() end,
	[23] = function() end,
	[24] = function(a)
		local fx, fy = TeamCentre(a[1])
		local tx, ty = Centre(a[2])
		if fx and tx then
			Warp(Team(a[1]), tx - fx, ty - fy)
		end
	end,
	[25] = function(a) Look(TeamCentre(a[1])) end,
	[26] = function() Counter = Counter + 1 end,
}

-- Every enemy player beaten. A mission without enemy players (P08: only wild aliens) is won by its script alone.
local function EnemiesDefeated()
	local enemies = 0
	for index, name in pairs(Mission.Players) do
		local player = Player.GetPlayer(name)
		if index < 8 and player ~= Human and not player.IsAlliedWith(Human) then
			enemies = enemies + 1
			if not player.HasNoRequiredUnits() then
				return false
			end
		end
	end

	return enemies > 0
end

local function RunTriggers()
	for index = 0, 255 do
		local trigger = Mission.Triggers[index]
		if trigger and not Done[index] then
			local condition = Conditions[trigger.Condition.Type]
			if not condition then
				Report("condition", trigger.Condition.Type)
			elseif condition(trigger.Condition) then
				local action = Actions[trigger.Action.Type]
				local running = false
				if action then
					running = action(trigger.Action, trigger, index)
				else
					Report("action", trigger.Action.Type)
				end

				if not running then
					print("campaign: tick " .. Now .. ", trigger " .. index .. " (" .. trigger.Name .. ") done")
					Done[index] = true
					FiredAt[index] = Now
					ShowUntil[index] = nil
				end
			end
		end
	end
end

function WorldLoaded()
	Human = Player.GetPlayer(Mission.Human)
	Objective = Human.AddPrimaryObjective(Mission.Objective)
	for name, cash in pairs(Mission.Cash) do
		Player.GetPlayer(name).Cash = cash
	end

	for _, trigger in pairs(Mission.Triggers) do
		if trigger.Condition.Type == 19 then
			VictoryTrigger = true
		end
	end

	-- The intro narration is the map's "intro" music track, which PwRaceMusic plays first.
	Camera.Position = Map.CenterOfCell(CPos.New(Mission.Camera[1], Mission.Camera[2]))
end

function Tick()
	if Ended then
		return
	end

	-- The triggers' times are original ticks (two world ticks each, tickrate-plan.md): they run once an original tick,
	-- as the original's did.
	if DateTime.GameTime % PwTime.TicksPerOriginal ~= 0 then
		return
	end

	Now = PwTime.Now

	-- Losing everything loses; beating every enemy wins, or, with a trigger waiting for it, lets that trigger run.
	if Human.HasNoRequiredUnits() then
		Lose()
		return
	end

	if not VictoryPending and Now % 25 == 0 and EnemiesDefeated() then
		Actions[3]()
	end

	RunTriggers()
end
