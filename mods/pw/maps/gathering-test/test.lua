-- Gathering berries. The local player (Multi0) is Primitive; BushNear lies below the Sanctuary, BushFar far to the right.
-- 1. A Buildman fresh out of the Sanctuary takes a gather order as its first order and delivers berries.
-- 2. An emptied bush stays on the map and grows its berries back.
-- 3. Units with an item (Shield of Peace, ...) can't be trained; they come from equipping at the Blacksmith.
WorldLoaded = function()
    me = Player.GetPlayer("Multi0")
    me.Cash = 1000
    sanctuary = Actor.Create("primitive-sanctuary", true, { Owner = me, Location = CPos.New(10, 10) })
    powerhouse = Actor.Create("primitive-powerhouse", true, { Owner = me, Location = CPos.New(18, 2) })
    soldierhouse = Actor.Create("primitive-soldierhouse", true, { Owner = me, Location = CPos.New(2, 2) })

    Later(5, TrainAndGather)
    Later(5, EmptyAndRegrow)
    Later(5, ItemUnitsNotTrainable)
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
                    local start = me.Cash
                    WaitFor("trained Buildman delivers them", 600, function() return PwTest.Carried(worker) == 0 and me.Cash > start end)
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

ItemUnitsNotTrainable = function()
    local trainable = Split(PwTest.Trainable(powerhouse) .. "," .. PwTest.Trainable(soldierhouse), ",")
    Check(Contains(trainable, "primitive-powerman") and Contains(trainable, "primitive-soldier"),
        "Powerhouse and Soldierhouse train their units")
    for _, unit in ipairs({ "primitive-powerman-shield-of-peace", "primitive-soldier-fire-arrow",
        "primitive-soldier-venom-arrow", "primitive-knight-spear-of-justice" }) do
        Check(not Contains(trainable, unit), unit .. " is not trainable")
    end
end
