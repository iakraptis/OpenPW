-- The Demon race (rules/demon, docs/game/demon.md).
-- 1. The DemonDen trains Insects; an Insect builds a Tomb, which adds 10 population.
-- 2. Kill points: a Demon's health is its KP. Killing a living unit gives back a quarter of the victim's full health;
--    a machine gives nothing.
-- 3. Stone curse (Kelpa, level 1): costs 85 KP; the enemy turns to stone and can't move, until it wears off.
-- 4. Noxious gas (Warlock): costs 120 KP; hurts every living thing in the cloud, friends too, but no buildings.
-- 5. Quake (Warlock, level 1): costs 150 KP; hurts enemy buildings.
-- 6. Suicide (Warlock): the Warlock dies, and the enemy beside it takes 150.
-- 7. Rise death (Devil): costs 100 KP; three zombies rise.
WorldLoaded = function()
    me = Player.GetPlayer("Multi0")
    enemy = Player.GetPlayer("Enemy")
    me.Cash = 20000
    -- Room for every unit of the test (the DemonDen provides 9).
    for x = 2, 14, 3 do Actor.Create("demon-tomb", true, { Owner = me, Location = CPos.New(x, 30) }) end

    Later(5, Production)
    Later(5, KillPoints)
    Later(5, StoneCurse)
    Later(5, NoxiousGas)
    Later(5, Quake)
    Later(5, Suicide)
    Later(5, RiseDeath)
    FinishWhenDone("DEMON", 4000)
end

Production = function()
    local den = Actor.Create("demon-demonden", true, { Owner = me, Location = CPos.New(2, 2) })
    PwTest.Train(den, "demon-insect")
    WaitFor("the DemonDen trains an Insect", 400, function()
        return #me.GetActorsByType("demon-insect") == 1
    end, function(ok)
        if not ok then return end
        local insect = me.GetActorsByType("demon-insect")[1]
        local limit = PwTest.PopulationLimit(me)
        local before = #me.GetActorsByType("demon-tomb")
        PwTest.Construct(insect, "demon-tomb", CPos.New(9, 2))
        WaitFor("the Insect builds a Tomb", 800, function()
            local tombs = me.GetActorsByType("demon-tomb")
            return #tombs == before + 1 and PwTest.Finished(tombs[#tombs])
        end, function(built)
            if built then
                Check(PwTest.PopulationLimit(me) == limit + 10, "the Tomb adds 10 population (" .. limit .. " -> " .. PwTest.PopulationLimit(me) .. ")")
            end
        end)
    end)
end

KillPoints = function()
    local skeleton = Actor.Create("demon-skeleton", true, { Owner = me, Location = CPos.New(4, 12) })
    local victim = Actor.Create("primitive-buildman", true, { Owner = enemy, Location = CPos.New(6, 12) })
    skeleton.Stance = "HoldFire"
    victim.Stance = "HoldFire"
    Later(2, function()
        skeleton.Health = 30
        victim.Health = 1
        skeleton.Attack(victim)
    end)
    WaitFor("a Skeleton kills a BuildMan", 300, function() return victim.IsDead end, function(ok)
        if not ok then return end
        -- A quarter of the BuildMan's 100.
        Check(skeleton.Health == 55, "it gains a quarter of the BuildMan's health as KP (" .. skeleton.Health .. ")")
        local machine = Actor.Create("primitive-bowmachine", true, { Owner = enemy, Location = CPos.New(6, 14) })
        machine.Stance = "HoldFire"
        Later(2, function()
            machine.Health = 1
            skeleton.Attack(machine)
        end)
        WaitFor("the Skeleton destroys a BowMachine", 300, function() return machine.IsDead end, function(destroyed)
            if destroyed then
                Check(skeleton.Health == 55, "a machine gives no KP (" .. skeleton.Health .. ")")
            end
        end)
    end)
end

StoneCurse = function()
    local kelpa = Actor.Create("demon-kelpa", true, { Owner = me, Location = CPos.New(14, 6) })
    local target = Actor.Create("primitive-powerman", true, { Owner = enemy, Location = CPos.New(17, 6) })
    kelpa.Stance = "HoldFire"
    target.Stance = "HoldFire"
    PwTest.CastOn(kelpa, "stone-curse", target)
    Later(30, function()
        Check(kelpa.Health == 250, "no Stone curse below level 1 (" .. kelpa.Health .. " KP)")
        PwTest.GiveExperience(kelpa, 14)
        PwTest.CastOn(kelpa, "stone-curse", target)
        Later(30, function()
            Check(kelpa.Health == 165, "Stone curse costs 85 KP (" .. kelpa.Health .. ")")
            local at = target.Location
            target.Move(CPos.New(17, 14))
            Later(60, function()
                Check(target.Location == at, "the cursed PowerMan can't move")
                WaitFor("the curse wears off and it walks", 500, function() return target.Location ~= at end)
            end)
        end)
    end)
end

NoxiousGas = function()
    local warlock = Actor.Create("demon-warlock", true, { Owner = me, Location = CPos.New(22, 4) })
    local foe = Actor.Create("primitive-powerman", true, { Owner = enemy, Location = CPos.New(27, 7) })
    local friend = Actor.Create("demon-skeleton", true, { Owner = me, Location = CPos.New(28, 7) })
    local house = Actor.Create("primitive-house", true, { Owner = enemy, Location = CPos.New(26, 9) })
    foe.Stance = "HoldFire"
    friend.Stance = "HoldFire"
    Later(10, function() PwTest.CastAt(warlock, "noxious-gas", CPos.New(27, 8)) end)
    WaitFor("the cloud hurts the enemy PowerMan", 300, function() return foe.Health < foe.MaxHealth end, function(ok)
        if not ok then return end
        Check(warlock.Health == 80, "Noxious gas costs 120 KP (" .. warlock.Health .. ")")
        Later(60, function()
            Check(friend.Health < friend.MaxHealth, "it hurts the Warlock's own Skeleton too")
            Check(house.Health == house.MaxHealth, "it doesn't hurt buildings")
        end)
    end)
end

Quake = function()
    local warlock = Actor.Create("demon-warlock", true, { Owner = me, Location = CPos.New(22, 16) })
    local house = Actor.Create("primitive-house", true, { Owner = enemy, Location = CPos.New(27, 18) })
    Later(10, function() PwTest.CastAt(warlock, "quake", CPos.New(28, 18)) end)
    Later(60, function()
        Check(warlock.Health == 200 and house.Health == house.MaxHealth, "no Quake below level 1")
        PwTest.GiveExperience(warlock, 18)
        PwTest.CastAt(warlock, "quake", CPos.New(28, 18))
        WaitFor("Quake hurts an enemy House", 300, function() return house.Health < house.MaxHealth end, function(ok)
            if ok then
                Check(warlock.Health == 50, "Quake costs 150 KP (" .. warlock.Health .. ")")
            end
        end)
    end)
end

Suicide = function()
    local warlock = Actor.Create("demon-warlock", true, { Owner = me, Location = CPos.New(14, 24) })
    local foe = Actor.Create("primitive-powerman", true, { Owner = enemy, Location = CPos.New(16, 24) })
    foe.Stance = "HoldFire"
    Later(10, function() PwTest.CastSelf(warlock, "suicide") end)
    WaitFor("a Warlock's Suicide kills it", 100, function() return warlock.IsDead end, function(ok)
        if ok then
            Check(foe.Health == foe.MaxHealth - 150, "the PowerMan beside it takes 150 (" .. foe.Health .. " of " .. foe.MaxHealth .. ")")
        end
    end)
end

RiseDeath = function()
    local devil = Actor.Create("demon-devil", true, { Owner = me, Location = CPos.New(4, 22) })
    devil.Stance = "HoldFire"
    PwTest.CastSelf(devil, "rise-death")
    WaitFor("a Devil raises three zombies", 100, function()
        return #me.GetActorsByTypes({ "demon-risezombie1", "demon-risezombie2" }) == 3
    end, function(ok)
        if ok then
            Check(devil.Health == 800, "Rise death costs 100 KP (" .. devil.Health .. ")")
        end
    end)
end
