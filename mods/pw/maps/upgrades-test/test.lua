-- Upgrades (Upgrade.upi, rules/primitive/upgrades.yaml). Buildings research them like units, one level at a time.
-- 1. Level 2 is offered only after level 1, and a researched level isn't offered again.
-- 2. The same level can't be queued twice.
-- 3. Improve Ground Primitive Weapon 1: +3 attack (a Powerman hits a Buildman for 28).
-- 4. Improve Gathering Amount: 16 berries per trip.
-- 5. Reduce Level Up Exp. 1: a Buildman levels every 8 experience instead of 10.
Weapon1 = "primitive-upgrade-ground-primitive-weapon-1"
Weapon2 = "primitive-upgrade-ground-primitive-weapon-2"
Gathering = "primitive-upgrade-gathering-amount-1"
LevelExp = "primitive-upgrade-level-up-exp-1"

WorldLoaded = function()
    me = Player.GetPlayer("Multi0")
    enemy = Player.GetPlayer("Creeps")
    me.Cash = 20000
    sanctuary = Actor.Create("primitive-sanctuary", true, { Owner = me, Location = CPos.New(10, 10) })
    landincreaser = Actor.Create("primitive-landincreaser", true, { Owner = me, Location = CPos.New(18, 2) })

    Later(5, Weapon)
    Later(5, GatheringAndLevels)
    FinishWhenDone("UPGRADES", 4000)
end

Weapon = function()
    Check(PwTest.Available(me, Weapon1) and not PwTest.Available(me, Weapon2), "only level 1 of the weapon upgrade is offered")
    PwTest.Train(landincreaser, Weapon1)
    PwTest.Train(landincreaser, Weapon1)
    Check(PwTest.QueueLength(landincreaser) == 1, "a level can't be queued twice")
    WaitFor("the LandIncreaser researches the weapon upgrade", 3000, function() return PwTest.Researched(me, Weapon1) end, function(ok)
        if not ok then return end
        Check(not PwTest.Available(me, Weapon1) and PwTest.Available(me, Weapon2), "after level 1, level 2 is offered instead")
        local attacker = Actor.Create("primitive-powerman", true, { Owner = me, Location = CPos.New(3, 26) })
        local target = Actor.Create("primitive-buildman", true, { Owner = enemy, Location = CPos.New(4, 26) })
        Later(1, function()
            target.Stance = "HoldFire"
            attacker.Attack(target)
        end)
        WaitFor("an upgraded Powerman hits", 300, function() return target.Health < target.MaxHealth end, function(hit)
            if hit then Check(target.MaxHealth - target.Health == 28, "an upgraded Powerman hits for 28 (did " .. (target.MaxHealth - target.Health) .. ")") end
        end)
    end)
end

GatheringAndLevels = function()
    PwTest.Train(sanctuary, Gathering)
    PwTest.Train(sanctuary, LevelExp)
    WaitFor("the Sanctuary researches the gathering upgrade", 1500, function() return PwTest.Researched(me, Gathering) end, function(ok)
        if not ok then return end
        local worker = Actor.Create("primitive-buildman", true, { Owner = me, Location = CPos.New(12, 18) })
        Later(2, function() PwTest.Gather(worker, BushNear) end)
        WaitFor("an upgraded Buildman gathers", 600, function() return PwTest.Carried(worker) > 0 end, function(gathered)
            if gathered then Check(PwTest.Carried(worker) == 16, "an upgraded Buildman carries 16 (" .. PwTest.Carried(worker) .. ")") end
        end)
    end)
    WaitFor("the Sanctuary researches Reduce Level Up Exp.", 2500, function() return PwTest.Researched(me, LevelExp) end, function(ok)
        if not ok then return end
        local man = Actor.Create("primitive-buildman", true, { Owner = me, Location = CPos.New(28, 28) })
        PwTest.GiveExperience(man, 8)
        Check(PwTest.Level(man) == 1, "with Reduce Level Up Exp. 1, 8 experience is a level")
    end)
end
