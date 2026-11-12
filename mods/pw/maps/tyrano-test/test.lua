-- The Tyrano race (rules/tyrano, docs/game/tyrano.md).
-- 1. The TyranoNest trains Dinos; a Dinos builds a Nest, which adds 10 population.
-- 2. Merging (PwMerge): two Velocis become a TwinVelocis once there is a LandNest, with their mean health; the
--    TwinVelocis splits back into the two, each with its health share.
-- 3. Dilophos, Pteras and Trices mutate into a Mutant only after Mutant Morphosis.
-- 4. Disguise (PwDisguise): only once researched. A disguised Velocis passes for a wild creature (the Creeps'): an
--    enemy StoneTower leaves it alone while it stones a Masos beside it, and it doesn't attack an enemy next to it.
--    Turned back, it is the owner's again and the tower stones it.
-- 5. The Dilophos's poison spit stuns: an enemy Powerman it hits is frozen for a moment, then free again.
-- 6. Hovering (.chr 0x17c = 2: Rhamphos, Insect): a ground unit that crosses water. Ground weapons reach a Rhamphos,
--    the Phoenix's air-only fire doesn't; an enemy Trap snaps at a Masos beside it but not at an Insect.
WorldLoaded = function()
    me = Player.GetPlayer("Multi0")
    enemy = Player.GetPlayer("Enemy")
    me.Cash = 20000
    -- Room for every unit of the test (the TyranoNest provides 9).
    for x = 2, 11, 3 do Actor.Create("tyrano-nest", true, { Owner = me, Location = CPos.New(x, 30) }) end

    Later(5, Production)
    Later(5, Merging)
    Later(5, Mutation)
    Later(5, Disguise)
    Later(5, Stun)
    Later(5, Hovering)
    FinishWhenDone("TYRANO", 4000)
end

Production = function()
    local nest = Actor.Create("tyrano-tyranonest", true, { Owner = me, Location = CPos.New(2, 2) })
    PwTest.Train(nest, "tyrano-dinos")
    WaitFor("the TyranoNest trains a Dinos", 400, function()
        return #me.GetActorsByType("tyrano-dinos") == 1
    end, function(ok)
        if not ok then return end
        local dinos = me.GetActorsByType("tyrano-dinos")[1]
        local limit = PwTest.PopulationLimit(me)
        local before = #me.GetActorsByType("tyrano-nest")
        PwTest.Construct(dinos, "tyrano-nest", CPos.New(10, 2))
        WaitFor("the Dinos builds a Nest", 800, function()
            local nests = me.GetActorsByType("tyrano-nest")
            return #nests == before + 1 and PwTest.Finished(nests[#nests])
        end, function(built)
            if built then
                Check(PwTest.PopulationLimit(me) == limit + 10, "the Nest adds 10 population (" .. limit .. " -> " .. PwTest.PopulationLimit(me) .. ")")
            end
        end)
    end)
end

Merging = function()
    local a = Actor.Create("tyrano-velocis", true, { Owner = me, Location = CPos.New(4, 12) })
    local b = Actor.Create("tyrano-velocis", true, { Owner = me, Location = CPos.New(7, 12) })
    a.Health = 100
    PwTest.Merge({ a, b })
    Later(60, function()
        Check(#me.GetActorsByType("tyrano-twinvelocis") == 0 and not a.IsDead and a.IsInWorld,
            "two Velocis don't merge without a LandNest")
        Actor.Create("tyrano-landnest", true, { Owner = me, Location = CPos.New(14, 2) })
        Later(2, function() PwTest.Merge({ a, b }) end)
        WaitFor("two Velocis merge into a TwinVelocis once there is a LandNest", 400, function()
            return #me.GetActorsByType("tyrano-twinvelocis") == 1
        end, function(ok)
            if not ok then return end
            local twin = me.GetActorsByType("tyrano-twinvelocis")[1]
            Check(not a.IsInWorld and not b.IsInWorld, "the Velocis are inside it")
            Check(PwTest.MergedParts(twin) == 2, "it holds two units (" .. PwTest.MergedParts(twin) .. ")")
            -- Half and full health: 75% of 400.
            Check(twin.Health == 300, "it has their mean health (" .. twin.Health .. " of 400)")
            Later(5, function() SplitTwin(twin, a, b) end)
        end)
    end)
end

SplitTwin = function(twin, a, b)
    PwTest.Split(twin)
    Later(5, function()
        Check(twin.IsDead or not twin.IsInWorld, "the split TwinVelocis is gone")
        Check(a.IsInWorld and b.IsInWorld and not a.IsDead and not b.IsDead, "the two Velocis are back")
        -- 300 of 400 is 75%: 150 of 200 each.
        Check(a.Health == 150 and b.Health == 150, "each has its health share (" .. a.Health .. ", " .. b.Health .. ")")
    end)
end

Mutation = function()
    local parts = {
        Actor.Create("tyrano-dilophos", true, { Owner = me, Location = CPos.New(4, 20) }),
        Actor.Create("tyrano-pteras", true, { Owner = me, Location = CPos.New(6, 20) }),
        Actor.Create("tyrano-trices", true, { Owner = me, Location = CPos.New(8, 20) }),
    }
    PwTest.Merge(parts)
    Later(60, function()
        Check(#me.GetActorsByType("tyrano-mutant") == 0, "no Mutant before Mutant Morphosis")
        PwTest.CompleteUpgrade(me, "tyrano-upgrade-mutant-morphosis-1")
        Later(2, function() PwTest.Merge(parts) end)
        WaitFor("Dilophos, Pteras and Trices mutate into a Mutant after Mutant Morphosis", 500, function()
            return #me.GetActorsByType("tyrano-mutant") == 1
        end, function(ok)
            if ok then
                Check(PwTest.MergedParts(me.GetActorsByType("tyrano-mutant")[1]) == 3, "the Mutant holds the three")
            end
        end)
    end)
end

Disguise = function()
    -- An enemy StoneTower (range 9) with a Velocis and a Masos 6 cells from it, and an enemy Powerman beside the
    -- Velocis that doesn't fight back.
    Actor.Create("primitive-stonetower", true, { Owner = enemy, Location = CPos.New(24, 26) })
    local velocis = Actor.Create("tyrano-velocis", true, { Owner = me, Location = CPos.New(26, 20) })
    local masos = Actor.Create("tyrano-masos", true, { Owner = me, Location = CPos.New(22, 20) })
    local powerman = Actor.Create("primitive-powerman", true, { Owner = enemy, Location = CPos.New(27, 20) })
    velocis.Stance = "HoldFire"
    masos.Stance = "HoldFire"
    powerman.Stance = "HoldFire"
    PwTest.Disguise(velocis, true)
    Check(not PwTest.Disguised(velocis), "no Disguise before it is researched")
    PwTest.CompleteUpgrade(me, "tyrano-upgrade-disguise-1")
    Later(2, function()
        PwTest.Disguise(velocis, true)
        velocis.Stance = "AttackAnything"
        Check(PwTest.Disguised(velocis), "a Velocis disguises once Disguise is researched")
        Check(PwTest.AppearsOwnedBy(velocis) == "Creeps", "it passes for a wild creature's (" .. PwTest.AppearsOwnedBy(velocis) .. ")")
    end)
    WaitFor("the enemy tower stones the Masos", 300, function() return masos.IsDead or masos.Health < masos.MaxHealth end,
        function(ok)
            if not ok then return end
            Check(velocis.Health == velocis.MaxHealth, "it leaves the disguised Velocis alone")
            Check(powerman.Health == powerman.MaxHealth, "the disguised Velocis doesn't attack the enemy beside it")
            PwTest.Disguise(velocis, false)
            Later(2, function()
                Check(not PwTest.Disguised(velocis) and PwTest.AppearsOwnedBy(velocis) == "Multi0", "it turns back")
            end)
            WaitFor("turned back, the tower stones it", 300, function()
                return velocis.IsDead or velocis.Health < velocis.MaxHealth
            end)
        end)
end

Stun = function()
    -- A Dilophos and an enemy Powerman 4 cells apart, the Powerman holding fire.
    local dilophos = Actor.Create("tyrano-dilophos", true, { Owner = me, Location = CPos.New(22, 6) })
    local powerman = Actor.Create("primitive-powerman", true, { Owner = enemy, Location = CPos.New(26, 6) })
    powerman.Stance = "HoldFire"
    dilophos.Attack(powerman)
    WaitFor("the Dilophos's spit stuns the Powerman", 300, function() return PwTest.Tinted(powerman, "stunned") end,
        function(ok)
            if not ok then return end
            Check(powerman.Health < powerman.MaxHealth, "the spit hurt it")
            dilophos.Stop()
            dilophos.Stance = "HoldFire"
            WaitFor("the stun wears off", 60, function() return powerman.IsDead or not PwTest.Tinted(powerman, "stunned") end)
        end)
end

Hovering = function()
    local rhamphos = Actor.Create("tyrano-rhamphos", true, { Owner = me, Location = CPos.New(14, 13) })
    rhamphos.Stance = "HoldFire"
    local soldier = Actor.Create("primitive-soldier", true, { Owner = enemy, Location = CPos.New(17, 13) })
    local phoenix = Actor.Create("elf-phoenix", true, { Owner = enemy, Location = CPos.New(12, 15) })
    soldier.Stance = "HoldFire"
    phoenix.Stance = "HoldFire"
    local trap = Actor.Create("primitive-trap", true, { Owner = enemy, Location = CPos.New(16, 16) })
    local insect = Actor.Create("demon-insect", true, { Owner = me, Location = CPos.New(17, 16) })
    local masos = Actor.Create("tyrano-masos", true, { Owner = me, Location = CPos.New(15, 16) })
    insect.Stance = "HoldFire"
    masos.Stance = "HoldFire"
    Later(2, function()
        Check(PwTest.CanOrder(soldier, "Attack", rhamphos), "a Soldier's arrows reach a Rhamphos")
        Check(not PwTest.CanOrder(phoenix, "Attack", rhamphos), "the Phoenix's air-only fire doesn't reach a Rhamphos")
    end)
    WaitFor("an enemy Trap snaps at the Masos beside it", 300, function()
        return masos.IsDead or masos.Health < masos.MaxHealth
    end, function(ok)
        if ok then Check(insect.Health == insect.MaxHealth, "the Trap leaves the hovering Insect alone") end
    end)
end
