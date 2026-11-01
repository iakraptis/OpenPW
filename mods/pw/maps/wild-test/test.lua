-- Wild creatures. The local player (Multi0) is Primitive; the creatures belong to Creeps.
-- 1. A plant-eater wanders around where it was placed and never fights.
-- 2. A predator and the units beside it leave each other alone until ordered; attacked, it fights back.
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
    local bystander = Actor.Create("primitive-buildman", true, { Owner = me, Location = CPos.New(17, 16) })
    Later(300, function()
        Check(not bystander.IsDead and bystander.Health == bystander.MaxHealth and raptor.Health == raptor.MaxHealth, "a predator and a unit beside it leave each other alone")
        local fighter = Actor.Create("primitive-powerman", true, { Owner = me, Location = CPos.New(15, 16) })
        fighter.Attack(raptor)
        WaitFor("a predator fights back when attacked", 600, function()
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
