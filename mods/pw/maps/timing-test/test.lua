-- Timings in original ticks (tickrate-plan.md): how long things take, measured in world ticks and divided by the world
-- ticks in one original tick (PwTime.TicksPerOriginal). Each is checked against the value measured before the tick
-- change (Baseline), within 3% or 2 original ticks. A missing baseline only prints the value.
-- Measured on 2026-11-22, one world tick per original tick (71 ms at normal speed).
Baseline = {
    ["cure heals 60 hp"] = 32,
    ["soldier walks 10 cells"] = 51,
    ["buildman first berries"] = 54,
    ["pteras flies 8 cells"] = 37,
    ["first hit after the attack order"] = 79,
    ["soldier attack cycle"] = 29,
    ["soldier attack cycle, again"] = 29,
    ["buildman first delivery"] = 87,
    ["redelf regains 10 mana"] = 161,
    ["soldierhouse trains a soldier"] = 258,
    ["buildman builds a house"] = 321,
    ["buildman repairs 100 hp"] = 366,
    ["soldier body lies"] = 653,
    ["velociraptor respawns"] = 2255,
}

Measure = function(name, startTick)
    local ticks = (DateTime.GameTime - startTick) / PwTime.TicksPerOriginal
    local base = Baseline[name]
    if base == nil then
        print("TIMING " .. name .. " = " .. ticks)
        Check(true, name .. " measured: " .. ticks)
        return
    end
    local allowed = math.max(2, base * 0.03)
    Check(math.abs(ticks - base) <= allowed, name .. ": " .. ticks .. " original ticks (before: " .. base .. ")")
end

-- Measure from now until cond() holds (or the original-tick timeout passes: a failed check).
Until = function(name, timeout, cond)
    local start = DateTime.GameTime
    WaitFor(name .. " happens", timeout, cond, function(ok) if ok then Measure(name, start) end end)
end

WorldLoaded = function()
    me = Player.GetPlayer("Multi0")
    enemy = Player.GetPlayer("Enemy")
    creeps = Player.GetPlayer("Creeps")
    me.Cash = 20000
    -- Population room for the trained Soldier.
    for x = 2, 14, 3 do Actor.Create("primitive-house", true, { Owner = me, Location = CPos.New(x, 31) }) end

    Later(5, Walk)
    Later(5, Attack)
    Later(5, Train)
    Later(5, Construct)
    Later(5, ManaRegen)
    Later(5, Cure)
    Later(5, Body)
    Later(5, Respawn)
    Later(5, Gather)
    Later(5, Fly)
    Later(5, Repair)
    FinishWhenDone("TIMING", 3500)
end

-- A Soldier walks 10 cells.
Walk = function()
    local soldier = Actor.Create("primitive-soldier", true, { Owner = me, Location = CPos.New(2, 2) })
    soldier.Stance = "HoldFire"
    Later(2, function()
        soldier.Move(CPos.New(12, 2))
        Until("soldier walks 10 cells", 400, function() return soldier.Location == CPos.New(12, 2) end)
    end)
end

-- A Soldier shoots a Giant that doesn't fight back: the first hit after the order, then the next two.
Attack = function()
    local soldier = Actor.Create("primitive-soldier", true, { Owner = me, Location = CPos.New(2, 5) })
    local target = Actor.Create("primitive-giant", true, { Owner = enemy, Location = CPos.New(6, 5) })
    soldier.Stance = "HoldFire"
    target.Stance = "HoldFire"
    local hits = { }
    Trigger.OnDamaged(target, function() hits[#hits + 1] = DateTime.GameTime end)
    Later(2, function()
        local start = DateTime.GameTime
        soldier.Attack(target)
        WaitFor("the Soldier hits three times", 300, function() return #hits >= 3 end, function(ok)
            if not ok then return end
            Measure("first hit after the attack order", start)
            Measure("soldier attack cycle", hits[1] + (DateTime.GameTime - hits[2]))
            Measure("soldier attack cycle, again", hits[2] + (DateTime.GameTime - hits[3]))
        end)
    end)
end

-- A SoldierHouse trains a Soldier.
Train = function()
    local house = Actor.Create("primitive-soldierhouse", true, { Owner = me, Location = CPos.New(20, 2) })
    local done = false
    Trigger.OnProduction(house, function() done = true end)
    Later(2, function()
        PwTest.Train(house, "primitive-soldier")
        Until("soldierhouse trains a soldier", 600, function() return done end)
    end)
end

-- A BuildMan beside the place builds a House.
Construct = function()
    local worker = Actor.Create("primitive-buildman", true, { Owner = me, Location = CPos.New(2, 9) })
    Later(2, function()
        PwTest.Construct(worker, "primitive-house", CPos.New(4, 9))
        Until("buildman builds a house", 900, function()
            local houses = Utils.Where(me.GetActorsByType("primitive-house"), function(h) return h.Location == CPos.New(4, 9) end)
            return #houses == 1 and PwTest.Finished(houses[1])
        end)
    end)
end

-- A RedElf with no mana regains 10.
ManaRegen = function()
    local red = Actor.Create("elf-redelf", true, { Owner = me, Location = CPos.New(14, 9) })
    red.Stance = "HoldFire"
    Later(2, function()
        PwTest.SetMana(red, 0)
        Until("redelf regains 10 mana", 400, function() return PwTest.Mana(red) >= 10 end)
    end)
end

-- A Unicorn's Cure heals a hurt RedElf by 60 HP.
Cure = function()
    local unicorn = Actor.Create("elf-unicorn", true, { Owner = me, Location = CPos.New(17, 12) })
    local hurt = Actor.Create("elf-redelf", true, { Owner = me, Location = CPos.New(18, 12) })
    unicorn.Stance = "HoldFire"
    hurt.Stance = "HoldFire"
    Later(2, function()
        hurt.Health = 10
        Until("cure heals 60 hp", 400, function() return hurt.Health >= 70 end)
    end)
end

-- A Soldier's body lies until it is gone.
Body = function()
    local soldier = Actor.Create("primitive-soldier", true, { Owner = enemy, Location = CPos.New(2, 14) })
    soldier.Stance = "HoldFire"
    Later(2, function()
        soldier.Kill()
        Later(1, function()
            Until("soldier body lies", 1000, function() return PwTest.Bodies(me, CPos.New(2, 14)) == 0 end)
        end)
    end)
end

-- A dead Velociraptor comes back.
Respawn = function()
    local raptor = Actor.Create("wild-velociraptor", true, { Owner = creeps, Location = CPos.New(30, 30) })
    Later(2, function()
        raptor.Kill()
        Until("velociraptor respawns", 3000, function() return #creeps.GetActorsByType("wild-velociraptor") > 0 end)
    end)
end

-- A BuildMan gathers a bush: its first berries, then the first delivery.
Gather = function()
    Actor.Create("primitive-sanctuary", true, { Owner = me, Location = CPos.New(22, 20) })
    local bush = Actor.Create("berry-bush-1", true, { Owner = Player.GetPlayer("Neutral"), Location = CPos.New(16, 22) })
    local worker = Actor.Create("primitive-buildman", true, { Owner = me, Location = CPos.New(16, 24) })
    Later(2, function()
        local earned = PwTest.Earned(me)
        PwTest.Gather(worker, bush)
        Until("buildman first berries", 400, function() return PwTest.Carried(worker) > 0 end)
        Until("buildman first delivery", 1500, function() return PwTest.Earned(me) > earned end)
    end)
end

-- A Pteras flies 8 cells.
Fly = function()
    local flyer = Actor.Create("tyrano-pteras", true, { Owner = me, Location = CPos.New(2, 20) })
    flyer.Stance = "HoldFire"
    Later(20, function()
        flyer.Move(CPos.New(10, 20))
        Until("pteras flies 8 cells", 400, function() return flyer.Location == CPos.New(10, 20) end)
    end)
end

-- A BuildMan repairs 100 HP of a House.
Repair = function()
    local house = Actor.Create("primitive-house", true, { Owner = me, Location = CPos.New(26, 10) })
    local worker = Actor.Create("primitive-buildman", true, { Owner = me, Location = CPos.New(26, 13) })
    Later(2, function()
        house.Health = house.MaxHealth / 2
        local from = house.Health
        PwTest.OrderOn(worker, "PwRepairBuilding", house)
        Until("buildman repairs 100 hp", 1200, function() return house.Health >= from + 100 end)
    end)
end
