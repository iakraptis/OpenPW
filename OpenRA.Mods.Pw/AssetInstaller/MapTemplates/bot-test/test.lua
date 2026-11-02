-- Two Normal AIs (North, South: Primitive) on (2) One vs One at 5x speed, each from a Sanctuary and four Buildmen.
-- Both must grow their workforce, build a base and an army; they must fight early, and one must destroy a building.
-- Written by the asset installer (OpenRA.Mods.Pw/AssetInstaller/MapTemplates/bot-test) on the converted map.
WorkerTypes = { "primitive-buildman" }
BaseTypes = { "primitive-house", "primitive-powerhouse", "primitive-soldierhouse", "primitive-skyhouse",
    "primitive-blacksmith", "primitive-landincreaser", "primitive-skyincreaser", "primitive-stonetower", "primitive-trap" }
ArmyTypes = { "primitive-powerman", "primitive-giant", "primitive-soldier", "primitive-knight", "primitive-bowmachine",
    "primitive-chief", "primitive-squadron", "primitive-flattop" }

WorldLoaded = function()
    north = Player.GetPlayer("North")
    south = Player.GetPlayer("South")
    peak = { North = { workers = 0, base = 0, army = 0 }, South = { workers = 0, base = 0, army = 0 } }
    kills = 0
    buildingKills = 0
    Watch()
    for _, p in ipairs({ "North", "South" }) do
        WaitFor(p .. " trains workers (6+)", 6000, function() return peak[p].workers >= 6 end)
        WaitFor(p .. " builds a base (3+ buildings besides the Sanctuary)", 15000, function() return peak[p].base >= 3 end)
        WaitFor(p .. " trains an army (4+ units)", 20000, function() return peak[p].army >= 4 end)
    end
    WaitFor("the bots fight (a kill within 15000 ticks)", 15000, function() return kills > 0 end)
    WaitFor("a bot destroys a building of the other", 40000, function() return buildingKills > 0 end)
    FinishWhenDone("BOT", 40500)
end

watched = { }
nextReport = { North = 0, South = 0 }
Watch = function()
    for _, p in ipairs({ north, south }) do
        local name = p.InternalName
        local workers = #p.GetActorsByTypes(WorkerTypes)
        local base = p.GetActorsByTypes(BaseTypes)
        local army = p.GetActorsByTypes(ArmyTypes)
        if workers > peak[name].workers then peak[name].workers = workers end
        if #base > peak[name].base then peak[name].base = #base end
        if #army > peak[name].army then peak[name].army = #army end
        for _, a in ipairs(p.GetActors()) do
            local key = tostring(a)
            if not watched[key] and a.HasProperty("Health") then
                watched[key] = true
                Trigger.OnKilled(a, function(self, killer)
                    if killer and killer.Owner ~= self.Owner and (killer.Owner == north or killer.Owner == south) then
                        kills = kills + 1
                        if Contains(BaseTypes, self.Type) or self.Type == "primitive-sanctuary" then
                            buildingKills = buildingKills + 1
                        end
                        print("bot: " .. killer.Owner.InternalName .. " killed " .. self.Type .. " at tick " .. DateTime.GameTime)
                    end
                end)
            end
        end
        if DateTime.GameTime >= nextReport[name] then
            nextReport[name] = nextReport[name] + 2500
            local types = { }
            for _, b in ipairs(base) do types[b.Type] = (types[b.Type] or 0) + 1 end
            local parts = { }
            for t, n in pairs(types) do parts[#parts + 1] = string.gsub(t, "primitive%-", "") .. " " .. n end
            table.sort(parts)
            print("bot: tick " .. DateTime.GameTime .. " " .. name .. " workers " .. workers .. ", buildings " .. #base
                .. " (" .. table.concat(parts, ", ") .. "), army " .. #army .. ", berries " .. (p.Cash + p.Resources))
        end
    end
    if not finished then Trigger.AfterDelay(250, Watch) end
end
