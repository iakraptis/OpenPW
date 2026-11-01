-- Gathering berries. The local player (Multi0) is Primitive; BushNear lies below the Sanctuary, BushFar far to the right.
-- 1. A Buildman fresh out of the Sanctuary takes a gather order as its first order and delivers berries.
-- 2. An emptied bush stays on the map and grows its berries back.
-- 3. Trained units walk out to different cells around the building, the front row first.
-- 4. Workers deliver from a cell right beside the Sanctuary, on the side nearest to them.
-- 5. Units with an item (Shield of Peace, ...) can't be trained; they come from equipping at the Blacksmith.
WorldLoaded = function()
    me = Player.GetPlayer("Multi0")
    me.Cash = 5000
    sanctuary = Actor.Create("primitive-sanctuary", true, { Owner = me, Location = CPos.New(10, 10) })
    powerhouse = Actor.Create("primitive-powerhouse", true, { Owner = me, Location = CPos.New(18, 2) })
    soldierhouse = Actor.Create("primitive-soldierhouse", true, { Owner = me, Location = CPos.New(2, 2) })

    Later(5, TrainAndGather)
    Later(5, EmptyAndRegrow)
    Later(5, ItemUnitsNotTrainable)
    Later(5, SpreadOverExits)
    FinishWhenDone("GATHERING", 3000)
end

TrainAndGather = function()
    sanctuary.Build({ "primitive-buildman" }, Expect("Sanctuary trains a Buildman", function(units)
        local worker = units[1]
        -- Ordered after it has stood a while where it walked out to (it used to stop on a building cell, where an
        -- order given once it had settled found no path).
        WaitFor("trained Buildman walks out and waits", 200, function() return worker.IsIdle end, function()
            Later(300, function()
                PwTest.Gather(worker, BushNear)
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
    local worker = Actor.Create("primitive-buildman", true, { Owner = me, Location = CPos.New(24, 12) })
    PwTest.SetBerries(BushFar, 12)
    Later(2, function()
        PwTest.Gather(worker, BushFar)
        WaitFor("bush is gathered empty", 600, function() return PwTest.Berries(BushFar) == 0 end, function()
            Check(BushFar.IsInWorld and not BushFar.IsDead, "empty bush stays on the map")
            WaitFor("empty bush grows berries back", 200, function() return PwTest.Berries(BushFar) > 0 end)
        end)
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
