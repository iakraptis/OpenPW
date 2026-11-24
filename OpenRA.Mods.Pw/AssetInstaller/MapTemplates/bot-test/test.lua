-- AIs, all against all on a converted original map at 5x speed, each from its main building and four workers; each is
-- named after its race. The installer writes one map per battle (AssetInstaller/TestMaps.cs) and sets Bots, Checks,
-- Level (the AIs', as the map's players have it: Easy for bot-test, Medium for the battles) and BattleName below. With Checks (bot-test), every bot must grow its workforce, build a base and an
-- army, and guard it early with towers (by the main building and by the berries); they must fight early, and some bot
-- must destroy a building of another. A bot that has lost its main building has lost, whatever else it still has. The
-- battle goes on until one bot is left or MaxTicks (a hard cap: 40000 for bot-test, 150000 for the battles, about 3
-- hours at normal speed). Times are original ticks (testlib, Now()). Every 5000 ticks each bot's state is printed; at
-- the end, one "BATTLE RESULT" line per bot and a "BATTLE END" line, which tools/battle_stats.py collects. The first
-- time a bot has its leader (the unit its main building trains last, once every other building stands: the Chief,
-- Tyranos, Devil, AngelElf) a "bot: ... leader" line is printed, and the results give that tick and the most it had.
-- The start positions are shuffled for every battle (some spots favour their owner): the map places each bot's start at
-- a spawn point, and the script moves them on the first ticks; the results give each bot's start.
Bots = { "Primitive", "Tyrano", "Demon", "Elf" }
Checks = true
Level = "easy"
BattleName = "bot-test"
MaxTicks = Checks and 40000 or 150000
-- Written by the asset installer (OpenRA.Mods.Pw/AssetInstaller/MapTemplates/bot-test) on the converted map.
Races = {
    primitive = {
        Leader = "primitive-chief",
        Workers = { "primitive-buildman" },
        Main = "primitive-sanctuary",
        Base = { "primitive-house", "primitive-powerhouse", "primitive-soldierhouse", "primitive-skyhouse",
            "primitive-blacksmith", "primitive-landincreaser", "primitive-skyincreaser", "primitive-stonetower", "primitive-trap" },
        Army = { "primitive-powerman", "primitive-giant", "primitive-soldier", "primitive-knight", "primitive-bowmachine",
            "primitive-chief", "primitive-squadron", "primitive-flattop" },
        Towers = { "primitive-stonetower" },
    },
    tyrano = {
        Leader = "tyrano-tyranos",
        Workers = { "tyrano-dinos" },
        Main = "tyrano-tyranonest",
        Base = { "tyrano-nest", "tyrano-eggnest", "tyrano-landnest", "tyrano-skynest", "tyrano-thrownest",
            "tyrano-upgradenest", "tyrano-landnisdos", "tyrano-skynisdos", "tyrano-ascomoid" },
        Army = { "tyrano-masos", "tyrano-velocis", "tyrano-twinvelocis", "tyrano-dilophos", "tyrano-trices",
            "tyrano-eggthrower", "tyrano-tyranos", "tyrano-rhamphos", "tyrano-pteras", "tyrano-mutant" },
        Towers = { "tyrano-ascomoid" },
    },
    demon = {
        Leader = "demon-devil",
        Workers = { "demon-insect" },
        Main = "demon-demonden",
        Base = { "demon-tomb", "demon-deathden", "demon-flyden", "demon-magicden", "demon-hell", "demon-dirtyportal",
            "demon-highportal", "demon-volcano" },
        Army = { "demon-skeleton", "demon-mudman", "demon-kelpa", "demon-kingdemon", "demon-devil", "demon-darkbat",
            "demon-gargoyle" },
        Towers = { "demon-volcano" },
    },
    elf = {
        Leader = "elf-angelelf",
        Workers = { "elf-wizard" },
        Main = "elf-heaven",
        Base = { "elf-elftree", "elf-redhall", "elf-bluehall", "elf-manahall", "elf-skyhall", "elf-elflibrary",
            "elf-skylibrary", "elf-manalibrary", "elf-bubble" },
        Army = { "elf-redelf", "elf-whiteelf", "elf-ranger", "elf-blueelf", "elf-phoenix", "elf-dragon", "elf-angelelf" },
        Towers = { "elf-bubble" },
    },
}

-- Each bot's main building as placed (by RandomStarts), for the tower checks.
Main = { }
Start = { }

-- Shuffles the bots' starts: each one's main building and four workers (what the map placed, at the spawn points) go,
-- and on the next tick a bot gets its start at the spawn point the shuffle gave it (the old buildings are gone by then);
-- the battle begins a tick later.
RandomStarts = function(andThen)
    local spots = { }
    for i, name in ipairs(Bots) do
        local main = Map.NamedActor(name .. "Main")
        spots[i] = main.Location
        main.Destroy()
        for w = 0, 3 do
            local worker = Map.NamedActor(name .. "Worker" .. w)
            if worker then worker.Destroy() end
        end
    end
    for i = #spots, 2, -1 do
        local j = Utils.RandomInteger(1, i + 1)
        spots[i], spots[j] = spots[j], spots[i]
    end
    Trigger.AfterDelay(1, function()
        for i, name in ipairs(Bots) do
            local p = Player.GetPlayer(name)
            local race = Races[p.Faction]
            local at = spots[i]
            Start[name] = at
            Main[name] = Actor.Create(race.Main, true, { Owner = p, Location = at })
            for w = 0, 3 do
                Actor.Create(race.Workers[1], true, { Owner = p, Location = CPos.New(at.X + w, at.Y + 5) })
            end
            print("bot: " .. name .. " starts at " .. at.X .. "," .. at.Y)
        end
        -- Created actors join the world at the end of the tick: the battle starts on the next one.
        Trigger.AfterDelay(1, andThen)
    end)
end

WorldLoaded = function()
    RandomStarts(Begin)
end

Begin = function()
    players, isBot, peak, guard, nextReport = { }, { }, { }, { }, { }
    for _, name in ipairs(Bots) do
        local p = Player.GetPlayer(name)
        players[#players + 1] = p
        isBot[name] = true
        peak[name] = { workers = 0, base = 0, army = 0, towers = 0, armyValue = 0 }
        guard[name] = { }
        nextReport[name] = 0
    end
    kills = 0
    buildingKills = 0
    score = { }
    for _, name in ipairs(Bots) do score[name] = { kills = 0, losses = 0, gone = nil, earned = { }, leaderAt = nil, leaders = 0 } end
    Watch()
    Battle()
    if not Checks then
        FinishWhenDone("BATTLE", MaxTicks)
        return
    end

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
    FinishWhenDone("BOT", MaxTicks)
end

-- What a bot has left: its main buildings and base, and its army.
Standing = function(p)
    local race = Races[p.Faction]
    return #p.GetActorsByTypes({ race.Main }) + #p.GetActorsByTypes(race.Base), p.GetActorsByTypes(race.Army)
end

-- Whether a bot is still in the battle: it has kept a main building all along. Once it has none it has lost, even if it
-- builds another.
InBattle = function(p)
    local s = score[p.InternalName]
    if not s.gone and #p.GetActorsByTypes({ Races[p.Faction].Main }) == 0 then s.gone = Now() end
    return not s.gone
end

-- Goes on while two bots or more are in the battle, then prints the standings. The winner is the last bot in it; at
-- MaxTicks there is none, and the leader is the one owning the most (PwTest.AssetValue).
Battle = function()
    local alive = { }
    for _, p in ipairs(players) do
        if InBattle(p) then alive[#alive + 1] = p end
    end
    if #alive > 1 and Now() < MaxTicks - 300 then
        Later(250, Battle)
        return
    end

    local winner = #alive == 1 and alive[1].InternalName or "none"
    local leader, best = "none", -1
    for _, p in ipairs(alive) do
        local value = PwTest.AssetValue(p)
        if value > best then leader, best = p.InternalName, value end
    end
    for _, p in ipairs(players) do
        local name = p.InternalName
        local buildings, army = Standing(p)
        local s = score[name]
        print("bot: standing at tick " .. Now() .. ": " .. name .. " buildings " .. buildings .. ", army "
            .. #army .. " (" .. Census(army) .. "), kills " .. s.kills .. ", losses " .. s.losses)
        print("BATTLE RESULT battle=" .. BattleName .. " bot=" .. name .. " race=" .. p.Faction
            .. " alive=" .. (s.gone and 0 or 1) .. " gone=" .. (s.gone or -1)
            .. " buildings=" .. buildings .. " army=" .. #army .. " armyValue=" .. PwTest.ArmyValue(p)
            .. " assets=" .. PwTest.AssetValue(p) .. " peakWorkers=" .. peak[name].workers .. " peakArmy=" .. peak[name].army
            .. " peakArmyValue=" .. peak[name].armyValue .. " earned=" .. PwTest.Earned(p) .. " spent=" .. PwTest.Spent(p)
            .. " earned5k=" .. (s.earned[5000] or -1) .. " earned10k=" .. (s.earned[10000] or -1)
            .. " earned20k=" .. (s.earned[20000] or -1) .. " kills=" .. s.kills .. " losses=" .. s.losses
            .. " killsValue=" .. p.KillsCost .. " lossesValue=" .. p.DeathsCost .. " level=" .. Level
            .. " leaderAt=" .. (s.leaderAt or -1) .. " peakLeaders=" .. s.leaders
            .. " start=" .. Start[name].X .. "," .. Start[name].Y)
    end
    print("BATTLE END battle=" .. BattleName .. " tick=" .. Now() .. " winner=" .. winner .. " leader=" .. leader)
    battleOver = true
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
        local armyValue = PwTest.ArmyValue(p)
        if armyValue > peak[name].armyValue then peak[name].armyValue = armyValue end
        local leaders = #p.GetActorsByTypes({ race.Leader })
        if leaders > score[name].leaders then score[name].leaders = leaders end
        if leaders > 0 and not score[name].leaderAt then
            score[name].leaderAt = Now()
            print("bot: tick " .. Now() .. " " .. name .. " has its leader (" .. race.Leader .. ")")
        end
        local towers = p.GetActorsByTypes(race.Towers)
        if #towers > peak[name].towers then peak[name].towers = #towers end
        local hq = Main[name]
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
        if Now() >= nextReport[name] then
            -- The berries earned by each 5000 ticks, for the economy comparison.
            if nextReport[name] > 0 then score[name].earned[nextReport[name]] = PwTest.Earned(p) end
            nextReport[name] = nextReport[name] + 5000
            print("bot: tick " .. Now() .. " " .. name .. " workers " .. workers .. ", buildings " .. #base
                .. " (" .. Census(base) .. "), army " .. #army .. " (" .. Census(army) .. ", value " .. armyValue
                .. "), berries " .. (p.Cash + p.Resources) .. " (earned " .. PwTest.Earned(p) .. ", spent "
                .. PwTest.Spent(p) .. "), kills " .. score[name].kills .. ", losses " .. score[name].losses)
        end
    end
    if not finished and not battleOver then Trigger.AfterDelay(PwTime.Ticks(250), Watch) end
end
