-- Test: Pathing through a narrow ramp, on the converted (2) One VS One (written by the installer, TestMaps.cs).
-- A group of 12 Powermen below the ramp at x 25-29, y 17-21 (two cells wide between cliffs) is ordered to the top
-- left. Each unit plans its first path through the others, not around them (our engine fork, Move.PathSearchOrder),
-- so the group files over the ramp instead of splitting off on detours through another one.
-- 1. Every unit arrives, by tick 400 (about 125 ticks with the fix; detours took until 315 and later).
-- 2. No unit strays more than 10 cells from the ramp on its way (detours went 19 cells away).
local Target = CPos.New(22, 15)
local RampCentre = CPos.New(27, 19)
local Deadline = 400
local MaxStray = 10

WorldLoaded = function()
    local group = Player.GetPlayer("Group")
    local units = { }
    for i = 0, 11 do
        units[#units + 1] = Actor.Create("primitive-powerman", true, { Owner = group, Location = CPos.New(29 + i % 4, 23 + math.floor(i / 4)) })
    end

    local arrived, farthest = { }, { }
    Later(5, function()
        for _, u in ipairs(units) do u.Move(Target) end
    end)

    local ticks = 5
    local function Sample()
        ticks = ticks + 5
        for i, u in ipairs(units) do
            local c = u.Location
            farthest[i] = math.max(farthest[i] or 0, math.max(math.abs(c.X - RampCentre.X), math.abs(c.Y - RampCentre.Y)))
            if not arrived[i] and math.max(math.abs(c.X - Target.X), math.abs(c.Y - Target.Y)) <= 3 then arrived[i] = ticks end
        end

        if ticks < Deadline then
            Later(5, Sample)
            return
        end

        local count, last, stray = 0, 0, 0
        for i = 1, #units do
            if arrived[i] then
                count = count + 1
                last = math.max(last, arrived[i])
            end
            stray = math.max(stray, farthest[i])
        end

        Check(count == #units, "the whole group crosses the ramp by tick " .. Deadline .. " (" .. count .. " of " .. #units .. ", the last at " .. last .. ")")
        Check(stray <= MaxStray, "no unit takes a detour (the farthest went " .. stray .. " cells from the ramp)")
    end

    Later(10, Sample)
    FinishWhenDone("PATHING", Deadline + 20)
end
