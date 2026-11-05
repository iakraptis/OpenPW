-- Four Normal AIs, one of each race, all against all on (4) The X Cross at 5x speed, each from its main building
-- and four workers; each is named after its race. Every one must grow its workforce, build a base and an army, and
-- guard it early with towers (by the main building and by the berries); they must fight early, and some bot must
-- destroy a building of another. The battle then goes on until one bot is left or MaxTicks (a hard cap), and the
-- standings are printed: what each bot has left, its kills and its losses.
MaxTicks = 40000
Bots = { "Primitive", "Tyrano", "Demon", "Elf" }
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
    demon = {
        Workers = { "demon-insect" },
        Main = "demon-demonden",
        Base = { "demon-tomb", "demon-deathden", "demon-flyden", "demon-magicden", "demon-hell", "demon-dirtyportal",
            "demon-highportal", "demon-volcano" },
        Army = { "demon-skeleton", "demon-mudman", "demon-kelpa", "demon-kingdemon", "demon-devil", "demon-darkbat",
            "demon-gargoyle" },
        Towers = { "demon-volcano" },
    },
    elf = {
        Workers = { "elf-wizard" },
        Main = "elf-heaven",
        Base = { "elf-elftree", "elf-redhall", "elf-bluehall", "elf-manahall", "elf-skyhall", "elf-elflibrary",
            "elf-skylibrary", "elf-manalibrary", "elf-bubble" },
        Army = { "elf-redelf", "elf-whiteelf", "elf-ranger", "elf-blueelf", "elf-phoenix", "elf-dragon", "elf-angelelf" },
        Towers = { "elf-bubble" },
    },
}

WorldLoaded = function()
    players, isBot, peak, guard, nextReport = { }, { }, { }, { }, { }
    for _, name in ipairs(Bots) do
        local p = Player.GetPlayer(name)
        players[#players + 1] = p
        isBot[name] = true
        peak[name] = { workers = 0, base = 0, army = 0, towers = 0 }
        guard[name] = { }
        nextReport[name] = 0
    end
    kills = 0
    buildingKills = 0
    score = { }
    for _, name in ipairs(Bots) do score[name] = { kills = 0, losses = 0 } end
    Watch()
    for _, p in ipairs(Bots) do
        WaitFor(p .. " trains workers (6+)", 6000, function() return peak[p].workers >= 6 end)
        WaitFor(p .. " builds a base (3+ buildings besides the main one)", 15000, function() return peak[p].base >= 3 end)
        WaitFor(p .. " trains an army (4+ units)", 20000, function() return peak[p].army >= 4 end)
        -- Early, while every bot still stands (in a four-way battle the weakest may be wiped out later).
        WaitFor(p .. " guards its base with towers: 2+, one by the main building, one by berry bushes", 15000, function()
            return peak[p].towers >= 2 and guard[p].hq and guard[p].berries
        end)
    end
    WaitFor("the bots fight (a kill within 15000 ticks)", 15000, function() return kills > 0 end)
    WaitFor("a bot destroys a building of another", MaxTicks, function() return buildingKills > 0 end)
    Battle()
    FinishWhenDone("BOT", MaxTicks)
end

-- What a bot has left: its main buildings and base, and its army.
Standing = function(p)
    local race = Races[p.Faction]
    return #p.GetActorsByTypes({ race.Main }) + #p.GetActorsByTypes(race.Base), p.GetActorsByTypes(race.Army)
end

-- Goes on while two bots or more still have something, then prints the standings.
Battle = function()
    local alive = 0
    for _, p in ipairs(players) do
        local buildings, army = Standing(p)
        if buildings + #army > 0 then alive = alive + 1 end
    end
    if alive > 1 and DateTime.GameTime < MaxTicks - 300 then
        Later(250, Battle)
        return
    end
    for _, p in ipairs(players) do
        local name = p.InternalName
        local buildings, army = Standing(p)
        print("bot: standing at tick " .. DateTime.GameTime .. ": " .. name .. " buildings " .. buildings .. ", army "
            .. #army .. " (" .. Census(army) .. "), kills " .. score[name].kills .. ", losses " .. score[name].losses)
    end
end

-- "type n, type n" of these actors, without the race prefix.
Census = function(actors)
    local types = { }
    for _, a in ipairs(actors) do types[a.Type] = (types[a.Type] or 0) + 1 end
    local parts = { }
    for t, n in pairs(types) do parts[#parts + 1] = string.gsub(t, "^%a+%-", "") .. " " .. n end
    table.sort(parts)
    return table.concat(parts, ", ")
end

watched = { }
Watch = function()
    for _, p in ipairs(players) do
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
                    if killer and killer.Owner ~= self.Owner and isBot[killer.Owner.InternalName] then
                        kills = kills + 1
                        score[killer.Owner.InternalName].kills = score[killer.Owner.InternalName].kills + 1
                        if score[self.Owner.InternalName] then
                            score[self.Owner.InternalName].losses = score[self.Owner.InternalName].losses + 1
                        end
                        local victim = Races[self.Owner.Faction]
                        if Contains(victim.Base, self.Type) or self.Type == victim.Main then
                            buildingKills = buildingKills + 1
                        end
                    end
                end)
            end
        end
        if DateTime.GameTime >= nextReport[name] then
            nextReport[name] = nextReport[name] + 5000
            print("bot: tick " .. DateTime.GameTime .. " " .. name .. " workers " .. workers .. ", buildings " .. #base
                .. " (" .. Census(base) .. "), army " .. #army .. " (" .. Census(army) .. "), berries "
                .. (p.Cash + p.Resources) .. ", kills " .. score[name].kills .. ", losses " .. score[name].losses)
        end
    end
    if not finished then Trigger.AfterDelay(250, Watch) end
end
