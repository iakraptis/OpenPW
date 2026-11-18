-- Test: Economy. Merged from gathering-test, population-test, rally-test (research/tools/merge_tests.py): each is a section of
-- this map, 54 cells from the next, with its own players; At(x, y) is a cell of the section. A
-- section's checks carry its name.
local BaseCheck, BaseWaitFor, BaseFinish = Check, WaitFor, FinishWhenDone
local Sections, Deadline = { }, 0

-- gathering-test
do
    local function At(x, y) return CPos.New(x + 0, y + 0) end
    local function Check(ok, message) return BaseCheck(ok, "Gathering: " .. message) end
    local function WaitFor(message, timeout, cond, andThen) return BaseWaitFor("Gathering: " .. message, timeout, cond, andThen) end
    local function FinishWhenDone(_, deadline) Deadline = math.max(Deadline, deadline) end
    local Beside, EmptyAndRegrow, ItemUnitsNotTrainable, OneAtATime, SpreadOverExits, TrainAndGather, WorldLoaded, me, powerhouse, sanctuary, soldierhouse
    -- Gathering berries. The local player (Multi0) is Primitive; GatheringBushNear lies below the Sanctuary, GatheringBushFar far to the right.
    -- 1. A Buildman fresh out of the Sanctuary takes a gather order as its first order and delivers berries.
    -- 2. An emptied bush stays on the map and grows its berries back.
    -- 3. Trained units walk out to different cells around the building, the front row first.
    -- 4. Workers deliver from a cell right beside the Sanctuary, on the side nearest to them.
    -- 5. Units with an item (Shield of Peace, ...) can't be trained; they come from equipping at the Blacksmith.
    -- 6. One worker gathers a bush at a time (BerryBush.MaxGatherers 1, as the original): three sent to one bush,
    --    never two gathering it at once; one finding it taken goes to the free bush 4 cells away.
    WorldLoaded = function()
        me = Player.GetPlayer("Gathering")
        me.Cash = 5000
        sanctuary = Actor.Create("primitive-sanctuary", true, { Owner = me, Location = At(10, 10) })
        powerhouse = Actor.Create("primitive-powerhouse", true, { Owner = me, Location = At(18, 2) })
        soldierhouse = Actor.Create("primitive-soldierhouse", true, { Owner = me, Location = At(2, 2) })

        Later(5, TrainAndGather)
        Later(5, EmptyAndRegrow)
        Later(5, ItemUnitsNotTrainable)
        Later(5, SpreadOverExits)
        Later(5, OneAtATime)
        FinishWhenDone("GATHERING", 3000)
    end

    TrainAndGather = function()
        sanctuary.Build({ "primitive-buildman" }, Expect("Sanctuary trains a Buildman", function(units)
            local worker = units[1]
            -- Ordered after it has stood a while where it walked out to (it used to stop on a building cell, where an
            -- order given once it had settled found no path).
            WaitFor("trained Buildman walks out and waits", 200, function() return worker.IsIdle end, function()
                Later(300, function()
                    PwTest.Gather(worker, GatheringBushNear)
                    WaitFor("trained Buildman gathers berries on its first order", 600, function() return PwTest.Carried(worker) > 0 end, function(ok)
                        if not ok then return end
                        -- Unloaded means delivered (cash isn't a measure here: the Powerhouse is paying for Powermen meanwhile).
                        WaitFor("trained Buildman delivers them", 600, function() return PwTest.Carried(worker) == 0 end, function(ok)
                            if ok then Check(Beside(worker.Location, 10, 10, 6, 4), "Buildman delivers from right beside the Sanctuary") end
                        end)
                    end)
                end)
            end)
        end))
    end

    EmptyAndRegrow = function()
        local worker = Actor.Create("primitive-buildman", true, { Owner = me, Location = At(24, 12) })
        PwTest.SetBerries(GatheringBushFar, 12)
        Later(2, function()
            PwTest.Gather(worker, GatheringBushFar)
            WaitFor("bush is gathered empty", 600, function() return PwTest.Berries(GatheringBushFar) == 0 end, function()
                Check(GatheringBushFar.IsInWorld and not GatheringBushFar.IsDead, "empty bush stays on the map")
                WaitFor("empty bush grows berries back", 200, function() return PwTest.Berries(GatheringBushFar) > 0 end)
            end)
        end)
    end

    OneAtATime = function()
        local neutral = Player.GetPlayer("Neutral")
        local taken = Actor.Create("berry-bush-2", true, { Owner = neutral, Location = At(2, 26) })
        local free = Actor.Create("berry-bush-3", true, { Owner = neutral, Location = At(6, 26) })
        local workers = { }
        for i = 0, 2 do
            workers[#workers + 1] = Actor.Create("primitive-buildman", true, { Owner = me, Location = At(2 + i, 29) })
        end

        local most, ticks = 0, 0
        local function Sample()
            most = math.max(most, PwTest.Gatherers(taken))
            ticks = ticks + 1
            if ticks < 600 then
                Later(1, Sample)
                return
            end

            Check(most == 1, "never more than one worker gathers a bush at once (most at once: " .. most .. ")")
            Check(PwTest.Berries(taken) < 4000, "the bush they were sent to is gathered")
            Check(PwTest.Berries(free) < 4000, "a worker finding its bush taken gathers the free one 4 cells away")
        end

        Later(2, function()
            for _, w in ipairs(workers) do PwTest.Gather(w, taken) end
            Sample()
        end)
    end

    -- Whether a cell is next to (not on) the footprint at x, y of size w x h.
    Beside = function(cell, x, y, w, h)
        local inside = cell.X >= x and cell.X < x + w and cell.Y >= y and cell.Y < y + h
        return not inside and cell.X >= x - 1 and cell.X <= x + w and cell.Y >= y - 1 and cell.Y <= y + h
    end

    SpreadOverExits = function()
        local types = { "primitive-powerman", "primitive-powerman", "primitive-powerman" }
        powerhouse.Build(types, Expect("Powerhouse trains three Powermen", function(units)
            local men = units
            WaitFor("trained Powermen walk out", 300, function()
                for _, m in ipairs(men) do if not m.IsIdle then return false end end
                return true
            end, function()
                -- A unit walking out can nudge an earlier one aside, so most (not all) end up on the front row.
                local seen, distinct, beside, front = { }, true, true, 0
                for _, m in ipairs(men) do
                    local key = m.Location.X .. "," .. m.Location.Y
                    if seen[key] then distinct = false end
                    seen[key] = true
                    if math.abs(m.Location.X - 20) > 4 or math.abs(m.Location.Y - 3) > 3 then beside = false end
                    if m.Location.Y == 5 then front = front + 1 end
                end
                Check(distinct, "trained Powermen stand on different exit cells")
                Check(beside, "trained Powermen stand around the Powerhouse (within 2 cells)")
                Check(front >= 2, "trained Powermen mostly use the front exits below the Powerhouse")
            end)
        end))
    end

    ItemUnitsNotTrainable = function()
        local trainable = Split(PwTest.Trainable(powerhouse) .. "," .. PwTest.Trainable(soldierhouse), ",")
        Check(Contains(trainable, "primitive-powerman") and Contains(trainable, "primitive-soldier"),
            "Powerhouse and Soldierhouse train their units")
        for _, unit in ipairs({ "primitive-powerman-shield-of-peace", "primitive-soldier-fire-arrow",
            "primitive-soldier-venom-arrow", "primitive-knight-spear-of-justice" }) do
            Check(not Contains(trainable, unit), unit .. " is not trainable")
        end
    end
    Sections[#Sections + 1] = WorldLoaded
end

-- population-test
do
    local function At(x, y) return CPos.New(x + 54, y + 0) end
    local function Check(ok, message) return BaseCheck(ok, "Population: " .. message) end
    local function WaitFor(message, timeout, cond, andThen) return BaseWaitFor("Population: " .. message, timeout, cond, andThen) end
    local function FinishWhenDone(_, deadline) Deadline = math.max(Deadline, deadline) end
    local Limit, WorldLoaded, builder, me, powerhouse
    -- Population (docs/game/rules.md): units use it (Buildman 1, Powerman 2, ...), finished buildings provide it
    -- (Sanctuary 9, House 10, production buildings 1). Training can't start past the limit.
    -- 1. Units and queued units count; the limit is the finished buildings' sum.
    -- 2. A Powerman that would go past the limit isn't started.
    -- 3. A House under construction provides nothing; finished, it adds 10 and training can go on.
    WorldLoaded = function()
        me = Player.GetPlayer("Population")
        me.Cash = 20000
        Actor.Create("primitive-sanctuary", true, { Owner = me, Location = At(10, 10) })
        powerhouse = Actor.Create("primitive-powerhouse", true, { Owner = me, Location = At(18, 2) })
        builder = Actor.Create("primitive-buildman", true, { Owner = me, Location = At(8, 20) })
        Actor.Create("primitive-buildman", true, { Owner = me, Location = At(9, 20) })
        Actor.Create("primitive-buildman", true, { Owner = me, Location = At(10, 20) })

        Later(5, Limit)
        FinishWhenDone("POPULATION", 3000)
    end

    Limit = function()
        Check(PwTest.PopulationLimit(me) == 10, "Sanctuary and PowerHouse provide 10 (" .. PwTest.PopulationLimit(me) .. ")")
        Check(PwTest.PopulationUsed(me) == 3, "three Buildmen use 3 (" .. PwTest.PopulationUsed(me) .. ")")

        -- 3 + 3 Powermen x 2 = 9; a fourth (11) is over the limit.
        for i = 1, 4 do PwTest.Train(powerhouse, "primitive-powerman") end
        Check(PwTest.PopulationUsed(me) == 9, "only three of four Powermen start (" .. PwTest.PopulationUsed(me) .. " used)")

        PwTest.Construct(builder, "primitive-house", At(4, 24))
        Later(5, function()
            local house = me.GetActorsByType("primitive-house")[1]
            Check(house ~= nil, "a Buildman starts a House")
            if house == nil then return end
            Check(PwTest.PopulationLimit(me) == 10, "a House under construction provides nothing")
            WaitFor("the Buildman goes inside the House to build it", 300, function() return not builder.IsInWorld end)
            WaitFor("the House is finished", 2000, function() return PwTest.Finished(house) end, function(ok)
                if not ok then return end
                Check(PwTest.PopulationLimit(me) == 20, "a finished House adds 10 (" .. PwTest.PopulationLimit(me) .. ")")
                Later(2, function() Check(builder.IsInWorld and not builder.IsDead, "the Buildman comes out of the finished House") end)
                PwTest.Train(powerhouse, "primitive-powerman")
                Check(PwTest.PopulationUsed(me) >= 11, "with the House, the fourth Powerman starts")
            end)
        end)
    end
    Sections[#Sections + 1] = WorldLoaded
end

-- rally-test
do
    local function At(x, y) return CPos.New(x + 0, y + 54) end
    local function Check(ok, message) return BaseCheck(ok, "Rally: " .. message) end
    local function WaitFor(message, timeout, cond, andThen) return BaseWaitFor("Rally: " .. message, timeout, cond, andThen) end
    local function FinishWhenDone(_, deadline) Deadline = math.max(Deadline, deadline) end
    local WorldLoaded, bushFar, me, powerhouse, sanctuary
    -- Rally points (RallyPoint on every production building, after OpenSYW).
    -- 1. A Powerman trained at a PowerHouse with a rally point walks to it.
    -- 2. A Buildman trained at a Sanctuary whose rally point is on a berry bush starts gathering there
    --    (GatherAtRallyPoint), not at the nearer bush.
    -- 3. A main building can't be placed near a berry bush: two free cells between them (AcceptsBerries
    --    BushClearance); other buildings can be right against it.
    WorldLoaded = function()
        me = Player.GetPlayer("Rally")
        me.Cash = 5000
        sanctuary = Actor.Create("primitive-sanctuary", true, { Owner = me, Location = At(10, 10) })
        powerhouse = Actor.Create("primitive-powerhouse", true, { Owner = me, Location = At(18, 2) })
        bushFar = Map.NamedActor("RallyBushFar")

        Later(2, function()
            local spot = At(4, 28)
            powerhouse.RallyPoint = spot
            powerhouse.Build({ "primitive-powerman" }, Expect("the PowerHouse trains a Powerman", function(units)
                local man = units[1]
                WaitFor("the Powerman walks to the rally point", 1500, function()
                    return man.IsIdle and (man.Location - spot).Length <= 2
                end)
            end))

            sanctuary.RallyPoint = At(24, 9)
            sanctuary.Build({ "primitive-buildman" }, Expect("the Sanctuary trains a Buildman", function(units)
                local worker = units[1]
                local start = PwTest.Berries(bushFar)
                WaitFor("the Buildman gathers at the bush under the rally point", 1500, function()
                    return PwTest.Carried(worker) > 0 and PwTest.Berries(bushFar) < start
                end)
            end))
        end)
        Later(2, function()
            local bush = Actor.Create("berry-bush-0", true, { Owner = Player.GetPlayer("Neutral"), Location = At(28, 26) })
            local worker = Actor.Create("primitive-buildman", true, { Owner = me, Location = At(20, 30) })
            Later(1, function()
                Check(not PwTest.CanPlace(worker, "primitive-sanctuary", At(22, 24)), "a Sanctuary can't be placed right against a berry bush")
                Check(not PwTest.CanPlace(worker, "primitive-sanctuary", At(21, 24)), "nor one cell from it")
                Check(PwTest.CanPlace(worker, "primitive-sanctuary", At(20, 24)), "a Sanctuary two cells from the bush can")
                Check(PwTest.CanPlace(worker, "primitive-house", At(30, 26)), "a House can be placed right against it")
            end)
        end)
        FinishWhenDone("RALLY", 3000)
    end
    Sections[#Sections + 1] = WorldLoaded
end

WorldLoaded = function()
    for _, start in ipairs(Sections) do start() end
    BaseFinish("ECONOMY", Deadline)
end
