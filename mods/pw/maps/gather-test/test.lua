-- Test: Gathering rate. Four bases, one per race, each a main building, a population building, four bushes and
-- Workers workers that only gather. The berries each base earned are printed at Halfway and at End, with the berries
-- left in its bushes; the second half is the steady rate.
-- 1. Every base gathers.

local Workers = 25
local Halfway, End = 2500, 5000

local Races = {
    { name = "Primitive", hq = "primitive-sanctuary", house = "primitive-house", worker = "primitive-buildman" },
    { name = "Tyrano", hq = "tyrano-tyranonest", house = "tyrano-nest", worker = "tyrano-dinos" },
    { name = "Elf", hq = "elf-heaven", house = "elf-elftree", worker = "elf-wizard" },
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
        bushes[#bushes + 1] = Actor.Create("berry-bush-" .. i, true, { Owner = neutral, Location = At(4 + 4 * i, 12) })
    end

    local count = 0
    local function Gather(unit)
        count = count + 1
        PwTest.Gather(unit, bushes[(count - 1) % #bushes + 1])
    end

    -- The workers in a 5 x 5 block below the bushes.
    for i = 0, Workers - 1 do
        local unit = Actor.Create(worker, true, { Owner = player, Location = At(4 + 4 * (i % 5), 15 + 2 * math.floor(i / 5)) })
        Later(1, function() Gather(unit) end)
    end

    local function Left()
        local left = 0
        for _, bush in ipairs(bushes) do left = left + math.max(0, PwTest.Berries(bush)) end
        return left
    end
    return { player = player, hq = hq, count = function() return count end, left = Left }
end

WorldLoaded = function()
    local results = { }
    for i, race in ipairs(Races) do
        -- Two by two, each in the middle of a quarter of the map, clear of its edges and of the others.
        local col, row = (i - 1) % 2, math.floor((i - 1) / 2)
        results[race.name] = Base(Player.GetPlayer(race.name), race, race.worker, 12 + 44 * col, 2 + 44 * row)
    end

    local half = { }
    Later(Halfway, function()
        for name, r in pairs(results) do half[name] = PwTest.Earned(r.player) end
    end)

    Later(End, function()
        print("Gathering rate: berries earned by tick " .. Halfway .. " / by tick " .. End .. " / in between")
        for _, race in ipairs(Races) do
            local r = results[race.name]
            local earned = PwTest.Earned(r.player)
            print(string.format("Gathering rate: %-9s %5d / %5d / %5d (%d workers, %d berries left)",
                race.name, half[race.name], earned, earned - half[race.name], r.count(), r.left()))
            Check(earned > 0, race.name .. " gathers")
        end
    end)

    FinishWhenDone("GATHER", End + 10)
end
