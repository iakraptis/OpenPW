-- Test: Gathering rate. Eight bases, two per race, each a main building, a population building, four bushes and four
-- workers; each trains more workers up to Workers and every worker only gathers. One base of each race has the
-- race's workers (cells shared, so they pass each other), the other the same workers on the plain locomotors
-- ("-solo" in map.yaml, one unit a cell, as before the change). The berries each base earned are printed at
-- Halfway and at End; the second half, every worker out, is the steady rate.
-- 1. Every base gathers.
-- 2. The race's workers gather at least as much as the solo ones (Elf aside: its Wizard gathers from range).

local Workers = 12
local Halfway, End = 2500, 5000

local Races = {
    { name = "Primitive", hq = "primitive-sanctuary", house = "primitive-house", worker = "primitive-buildman" },
    { name = "Tyrano", hq = "tyrano-tyranonest", house = "tyrano-nest", worker = "tyrano-dinos" },
    { name = "Elf", hq = "elf-heaven", house = "elf-elftree", worker = "elf-wizard", fromRange = true },
    { name = "Demon", hq = "demon-demonden", house = "demon-tomb", worker = "demon-insect" },
}

-- A base in the 22 x 44 cell block at (x0, y0): the main building at the top, the bushes six rows below it.
local function Base(player, race, worker, x0, y0)
    local function At(x, y) return CPos.New(x0 + x, y0 + y) end
    local neutral = Player.GetPlayer("Neutral")
    player.Cash = 20000

    local hq = Actor.Create(race.hq, true, { Owner = player, Location = At(8, 3) })
    Actor.Create(race.house, true, { Owner = player, Location = At(1, 1) })
    local bushes = { }
    for i = 0, 3 do
        bushes[#bushes + 1] = Actor.Create("berry-bush-" .. i, true, { Owner = neutral, Location = At(4 + 4 * i, 13) })
    end

    local count = 0
    local function Gather(unit)
        count = count + 1
        PwTest.Gather(unit, bushes[(count - 1) % #bushes + 1])
    end

    for i = 0, 3 do
        local unit = Actor.Create(worker, true, { Owner = player, Location = At(8 + 2 * i, 9) })
        Later(1, function() Gather(unit) end)
    end

    -- One at a time (Build won't queue on a building with an OnProduction trigger): each sent to gather as it comes
    -- out, then the next one ordered.
    local trained = 4
    local function TrainNext()
        if trained >= Workers then return end
        trained = trained + 1
        hq.Build({ worker }, function(units)
            Later(5, function() Gather(units[1]) end)
            TrainNext()
        end)
    end
    Later(5, TrainNext)
    return { player = player, hq = hq, count = function() return count end }
end

WorldLoaded = function()
    local results = { }
    for i, race in ipairs(Races) do
        local x0 = 1 + 22 * (i - 1)
        results[race.name] = {
            race = race,
            shared = Base(Player.GetPlayer(race.name .. "Shared"), race, race.worker, x0, 1),
            solo = Base(Player.GetPlayer(race.name .. "Solo"), race, race.worker .. "-solo", x0, 45),
        }
    end

    local half = { }
    Later(Halfway, function()
        for name, r in pairs(results) do
            half[name] = { shared = PwTest.Earned(r.shared.player), solo = PwTest.Earned(r.solo.player) }
        end
    end)

    Later(End, function()
        print("Gathering rate: berries earned by tick " .. Halfway .. " / by tick " .. End .. " / in between")
        for _, race in ipairs(Races) do
            local r = results[race.name]
            local shared, solo = PwTest.Earned(r.shared.player), PwTest.Earned(r.solo.player)
            local sharedLate, soloLate = shared - half[race.name].shared, solo - half[race.name].solo
            print(string.format("Gathering rate: %-9s shared %5d / %5d / %5d (%d workers)   solo %5d / %5d / %5d (%d workers)",
                race.name, half[race.name].shared, shared, sharedLate, r.shared.count(),
                half[race.name].solo, solo, soloLate, r.solo.count()))

            Check(shared > 0 and solo > 0, race.name .. " bases both gather")
            Check(r.shared.count() == Workers and r.solo.count() == Workers, race.name .. " bases both train up to " .. Workers .. " workers")
            if not race.fromRange then
                Check(sharedLate >= soloLate, race.name .. " workers that share cells gather at least as fast as solo ones")
            end
        end
    end)

    FinishWhenDone("GATHER", End + 10)
end
