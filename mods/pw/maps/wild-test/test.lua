-- Wild creatures. The local player (Multi0) is Primitive; the creatures belong to Creeps.
-- 1. A plant-eater wanders around where it was placed and never fights.
-- 2. A predator attacks player units within 3 cells, leaves the others alone, and fights back when attacked.
-- 3. A flyer wanders too, and melee can't reach it.
WorldLoaded = function()
    me = Player.GetPlayer("Multi0")
    creeps = Player.GetPlayer("Creeps")

    Later(5, Grazer)
    Later(5, Predator)
    Later(5, Flyer)
    FinishWhenDone("WILD", 3000)
end

Grazer = function()
    local start = CPos.New(6, 26)
    local grazer = Actor.Create("wild-galimimus", true, { Owner = creeps, Location = start })
    local worker = Actor.Create("primitive-buildman", true, { Owner = me, Location = CPos.New(7, 26) })
    WaitFor("a plant-eater wanders", 1500, function() return grazer.Location ~= start end, function()
        Check((grazer.Location - start).Length <= 6, "a plant-eater stays near where it was placed")
    end)
    Later(600, function() Check(worker.Health == worker.MaxHealth, "a plant-eater doesn't attack") end)
end

Predator = function()
    local raptor = Actor.Create("wild-velociraptor", true, { Owner = creeps, Location = CPos.New(16, 16) })
    local near = Actor.Create("primitive-buildman", true, { Owner = me, Location = CPos.New(18, 16) })
    local far = Actor.Create("primitive-buildman", true, { Owner = me, Location = CPos.New(16, 28) })
    WaitFor("a predator attacks a unit that comes close", 300, function() return near.IsDead or near.Health < near.MaxHealth end)
    Later(300, function() Check(not far.IsDead and far.Health == far.MaxHealth, "a predator leaves units further away alone") end)

    local tough = Actor.Create("wild-triceratops", true, { Owner = creeps, Location = CPos.New(27, 20) })
    Later(5, function()
        local fighter = Actor.Create("primitive-soldier", true, { Owner = me, Location = CPos.New(27, 27) })
        fighter.Attack(tough)
        WaitFor("a predator fights back when attacked from afar", 900, function()
            return fighter.IsDead or fighter.Health < fighter.MaxHealth
        end)
    end)
end

Flyer = function()
    local start = CPos.New(26, 6)
    local flyer = Actor.Create("wild-pteranodon", true, { Owner = creeps, Location = start })
    WaitFor("a flyer wanders", 1500, function() return flyer.Location ~= start end, function()
        local fighter = Actor.Create("primitive-powerman", true, { Owner = me, Location = flyer.Location })
        fighter.Attack(flyer)
        Later(300, function() Check(flyer.Health == flyer.MaxHealth, "melee can't reach a flyer") end)
    end)
end
