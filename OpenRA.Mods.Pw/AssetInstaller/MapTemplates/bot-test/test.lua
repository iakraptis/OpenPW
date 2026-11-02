-- Two Normal AIs on (2) One vs One at 5x speed: North Primitive (a Sanctuary and four Buildmen), South Tyrano (a
-- TyranoNest and four Dinos). Both must grow their workforce, build a base and an army, and guard it with towers (by
-- the main building, by the berries and on the edge); they must fight early, and one must destroy a building.
-- Written by the asset installer (OpenRA.Mods.Pw/AssetInstaller/MapTemplates/bot-test) on the converted map.
Races = {
    primitive = {
        Workers = { "primitive-buildman" },
        Main = "primitive-sanctuary",
        Base = { "primitive-house", "primitive-powerhouse", "primitive-soldierhouse", "primitive-skyhouse",
            "primitive-blacksmith", "primitive-landincreaser", "primitive-skyincreaser", "primitive-stonetower", "primitive-trap" },
        Army = { "primitive-powerman", "primitive-giant", "primitive-soldier", "primitive-knight", "primitive-bowmachine",
            "primitive-chief", "primitive-squadron", "primitive-flattop" },
        Towers = { "primitive-stonetower" },
    },
    tyrano = {
        Workers = { "tyrano-dinos" },
        Main = "tyrano-tyranonest",
        Base = { "tyrano-nest", "tyrano-eggnest", "tyrano-landnest", "tyrano-skynest", "tyrano-thrownest",
            "tyrano-upgradenest", "tyrano-landnisdos", "tyrano-skynisdos", "tyrano-ascomoid" },
        Army = { "tyrano-masos", "tyrano-velocis", "tyrano-twinvelocis", "tyrano-dilophos", "tyrano-trices",
            "tyrano-eggthrower", "tyrano-tyranos", "tyrano-rhamphos", "tyrano-pteras", "tyrano-mutant" },
        Towers = { "tyrano-ascomoid" },
    },
}

WorldLoaded = function()
    north = Player.GetPlayer("North")
    south = Player.GetPlayer("South")
    peak = { North = { workers = 0, base = 0, army = 0, towers = 0 }, South = { workers = 0, base = 0, army = 0, towers = 0 } }
    guard = { North = { }, South = { } }
    kills = 0
    buildingKills = 0
    Watch()
    for _, p in ipairs({ "North", "South" }) do
        WaitFor(p .. " trains workers (6+)", 6000, function() return peak[p].workers >= 6 end)
        WaitFor(p .. " builds a base (3+ buildings besides the main one)", 15000, function() return peak[p].base >= 3 end)
        WaitFor(p .. " trains an army (4+ units)", 20000, function() return peak[p].army >= 4 end)
        WaitFor(p .. " guards its base with towers: 3+, one by the main building, one by berry bushes", 25000, function()
            return peak[p].towers >= 3 and guard[p].hq and guard[p].berries
        end)
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
        local race = Races[p.Faction]
        local workers = #p.GetActorsByTypes(race.Workers)
        local base = p.GetActorsByTypes(race.Base)
        local army = p.GetActorsByTypes(race.Army)
        if workers > peak[name].workers then peak[name].workers = workers end
        if #base > peak[name].base then peak[name].base = #base end
        if #army > peak[name].army then peak[name].army = #army end
        local towers = p.GetActorsByTypes(race.Towers)
        if #towers > peak[name].towers then peak[name].towers = #towers end
        local hq = Map.NamedActor(name .. "Main")
        for _, t in ipairs(towers) do
            if hq and not hq.IsDead and (t.Location - hq.Location).Length <= 8 then guard[name].hq = true end
            for _, a in ipairs(Map.ActorsInCircle(t.CenterPosition, WDist.FromCells(6))) do
                if string.find(a.Type, "berry%-bush") then guard[name].berries = true end
            end
        end
        for _, a in ipairs(p.GetActors()) do
            local key = tostring(a)
            if not watched[key] and a.HasProperty("Health") then
                watched[key] = true
                Trigger.OnKilled(a, function(self, killer)
                    if killer and killer.Owner ~= self.Owner and (killer.Owner == north or killer.Owner == south) then
                        kills = kills + 1
                        local victim = Races[self.Owner.Faction]
                        if Contains(victim.Base, self.Type) or self.Type == victim.Main then
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
            for t, n in pairs(types) do parts[#parts + 1] = string.gsub(t, "^%a+%-", "") .. " " .. n end
            table.sort(parts)
            print("bot: tick " .. DateTime.GameTime .. " " .. name .. " workers " .. workers .. ", buildings " .. #base
                .. " (" .. table.concat(parts, ", ") .. "), army " .. #army .. ", berries " .. (p.Cash + p.Resources))
        end
    end
    if not finished then Trigger.AfterDelay(250, Watch) end
end
