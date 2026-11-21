-- The Elf race (rules/elf, docs/game/elf.md).
-- 1. The Heaven trains Wizards; a Wizard conjures an ElfTree, which adds 10 population.
-- 2. Thunder bolt (RedElf, level 1, 40 mana): 80 damage to one enemy.
-- 3. Shield (WhiteElf, level 1, 60 mana): a friendly unit's shield soaks up 480 damage before it is hurt.
-- 4. Teleport (Ranger, 65 mana): the Ranger is at the place at once.
-- 5. Drop stone (BlueElf, level 1, 65 mana): 200 damage around the place.
-- 6. Cure (Unicorn): automatically heals a nearby friendly unit for its mana, as the original casts it: every 16 ticks,
--    each cast 2 HP a tick for 15 ticks (so 62 to 90 HP in 48 ticks, not the 96 of 2 HP every tick). Fake (after
--    Study Fake): a harmless copy.
-- 7. Meteor (ManaSpread, after Study Meteor): it dies, meteors hurt the enemies there.
-- 8. Resurrect (AngelElf, 80 mana): a unit that fell there comes back, the caster's own, at half health.
-- 9. Study Mana Potion Secret raises an Elf's mana by 50.
-- 10. Mass temper (WhiteElf, level 3): the friendly units around the place get a 160 shield.
-- 11. Jump portal (Ranger, level 3): an enemy standing in the portal is thrown away.
-- 12. Berry fly: a Wizard stays at the bush, and the berries fly to the Heaven by themselves (here the Enemy player's,
--     whose cash nothing else touches).
-- 13. The Bubble tower deals 2 damage and traps the target in its bubble for 36 ticks.
-- 14. Cast on a unit, Cure heals that one first, though another is more hurt.
WorldLoaded = function()
    me = Player.GetPlayer("Multi0")
    enemy = Player.GetPlayer("Enemy")
    me.Cash = 20000
    -- Room for every unit of the test (the Heaven provides 9).
    for x = 2, 14, 3 do Actor.Create("elf-elftree", true, { Owner = me, Location = CPos.New(x, 31) }) end

    Later(5, Production)
    Later(5, ThunderBolt)
    Later(5, Shield)
    Later(5, Teleport)
    Later(5, DropStone)
    Later(5, Unicorn)
    Later(5, Meteor)
    Later(5, Resurrect)
    Later(5, ManaPotion)
    Later(5, MassTemper)
    Later(5, JumpPortal)
    Later(5, BerryFly)
    Later(5, Bubble)
    Later(5, CurePreferred)
    FinishWhenDone("ELF", 4000)
end

CurePreferred = function()
    local unicorn = Actor.Create("elf-unicorn", true, { Owner = me, Location = CPos.New(10, 31) })
    local worse = Actor.Create("elf-redelf", true, { Owner = me, Location = CPos.New(9, 31) })
    local chosen = Actor.Create("elf-redelf", true, { Owner = me, Location = CPos.New(11, 31) })
    worse.Stance = "HoldFire"
    chosen.Stance = "HoldFire"
    Later(2, function()
        worse.Health = 20
        chosen.Health = 60
        PwTest.CastOn(unicorn, "cure", chosen)
        WaitFor("Cure heals the unit it was cast on first", 100, function() return chosen.Health == chosen.MaxHealth end,
            function(ok)
                if ok then Check(worse.Health < worse.MaxHealth, "the more hurt one waits its turn") end
                WaitFor("then the other", 200, function() return worse.Health == worse.MaxHealth end)
            end)
    end)
end

Bubble = function()
    local tower = Actor.Create("elf-bubble", true, { Owner = me, Location = CPos.New(24, 27) })
    local target = Actor.Create("primitive-buildman", true, { Owner = enemy, Location = CPos.New(29, 27) })
    target.Stance = "HoldFire"
    local full = target.Health

    WaitFor("the Bubble traps its target", 300, function()
        return not target.IsMobile
    end, function(ok)
        if not ok then return end
        Check(target.Health == full - 2, "the Bubble deals 2 damage")
        tower.Kill()
        WaitFor("the target can move again after the bubble expires", 100, function()
            return target.IsMobile
        end)
    end)
end

Production = function()
    local heaven = Actor.Create("elf-heaven", true, { Owner = me, Location = CPos.New(2, 2) })
    PwTest.Train(heaven, "elf-wizard")
    WaitFor("the Heaven trains a Wizard", 400, function()
        return #me.GetActorsByType("elf-wizard") == 1
    end, function(ok)
        if not ok then return end
        local wizard = me.GetActorsByType("elf-wizard")[1]
        local limit = PwTest.PopulationLimit(me)
        local before = #me.GetActorsByType("elf-elftree")
        PwTest.Construct(wizard, "elf-elftree", CPos.New(9, 2))
        WaitFor("the Wizard casts beside the unfinished ElfTree", 100, function()
            local trees = me.GetActorsByType("elf-elftree")
            return #trees == before + 1 and PwTest.ConstructionWorkerIsOutside(trees[#trees], wizard)
        end, function(ok)
            if ok then
                Check(PwTest.LightningActive(wizard), "the Wizard channels lightning into the unfinished ElfTree")
                Check(PwTest.ConstructionWorkerIsOutside(me.GetActorsByType("elf-elftree")[#me.GetActorsByType("elf-elftree")], wizard),
                    "the Wizard remains outside while building")
            end
        end)

        WaitFor("the Wizard conjures an ElfTree", 800, function()
            local trees = me.GetActorsByType("elf-elftree")
            return #trees == before + 1 and PwTest.Finished(trees[#trees])
        end, function(built)
            if built then
                Check(PwTest.PopulationLimit(me) == limit + 10, "the ElfTree adds 10 population (" .. limit .. " -> " .. PwTest.PopulationLimit(me) .. ")")
            end
        end)
    end)
end

ThunderBolt = function()
    local red = Actor.Create("elf-redelf", true, { Owner = me, Location = CPos.New(14, 4) })
    local foe = Actor.Create("primitive-knight", true, { Owner = enemy, Location = CPos.New(18, 4) })
    red.Stance = "HoldFire"
    foe.Stance = "HoldFire"
    Later(2, function()
        PwTest.CastOn(red, "thunder-bolt", foe)
        Later(30, function()
            Check(PwTest.Mana(red) >= 95 and foe.Health == foe.MaxHealth, "no Thunder bolt below level 1")
            PwTest.GiveExperience(red, 12)
            PwTest.CastOn(red, "thunder-bolt", foe)
            WaitFor("Thunder bolt strikes the Knight", 200, function() return foe.Health < foe.MaxHealth end, function(ok)
                if ok then
                    Check(foe.Health == foe.MaxHealth - 80, "it takes 80 (" .. foe.MaxHealth - foe.Health .. ")")
                end
            end)
        end)
    end)
end

Shield = function()
    local white = Actor.Create("elf-whiteelf", true, { Owner = me, Location = CPos.New(22, 4) })
    local friend = Actor.Create("elf-blueelf", true, { Owner = me, Location = CPos.New(24, 4) })
    white.Stance = "HoldFire"
    friend.Stance = "HoldFire"
    Later(2, function()
        PwTest.GiveExperience(white, 16)
        PwTest.CastOn(white, "shield", friend)
        WaitFor("a WhiteElf shields a BlueElf", 100, function() return PwTest.Shield(friend) > 0 end, function(ok)
            if not ok then return end
            Check(PwTest.Shield(friend) == 480, "the shield holds 480 (" .. PwTest.Shield(friend) .. ")")
            local attacker = Actor.Create("primitive-powerman", true, { Owner = enemy, Location = CPos.New(25, 5) })
            Later(2, function() attacker.Attack(friend) end)
            WaitFor("the PowerMan's blows wear the shield down", 300, function() return PwTest.Shield(friend) < 480 end, function(worn)
                if worn then
                    Check(friend.Health == friend.MaxHealth, "the BlueElf isn't hurt meanwhile")
                end
            end)
        end)
    end)
end

Teleport = function()
    local ranger = Actor.Create("elf-ranger", true, { Owner = me, Location = CPos.New(4, 12) })
    Later(2, function()
        PwTest.CastAt(ranger, "teleport", CPos.New(28, 12))
        WaitFor("the Ranger teleports across the map", 60, function() return (ranger.Location - CPos.New(28, 12)).Length <= 2 end, function(ok)
            if ok then
                Check(PwTest.Mana(ranger) < 40, "Teleport costs 65 mana (" .. PwTest.Mana(ranger) .. " left)")
            end
        end)
    end)
end

DropStone = function()
    local blue = Actor.Create("elf-blueelf", true, { Owner = me, Location = CPos.New(14, 16) })
    local foe = Actor.Create("primitive-giant", true, { Owner = enemy, Location = CPos.New(18, 16) })
    blue.Stance = "HoldFire"
    foe.Stance = "HoldFire"
    Later(2, function()
        PwTest.GiveExperience(blue, 16)
        PwTest.CastAt(blue, "drop-stone", CPos.New(18, 16))
        WaitFor("a stone falls on the Giant", 200, function() return foe.Health < foe.MaxHealth end, function(ok)
            if ok then
                Check(foe.Health == foe.MaxHealth - 200, "it takes 200 (" .. foe.MaxHealth - foe.Health .. ")")
            end
        end)
    end)
end

Unicorn = function()
    local unicorn = Actor.Create("elf-unicorn", true, { Owner = me, Location = CPos.New(22, 16) })
    local hurt = Actor.Create("elf-redelf", true, { Owner = me, Location = CPos.New(24, 16) })
    local model = Actor.Create("primitive-knight", true, { Owner = enemy, Location = CPos.New(27, 18) })
    hurt.Stance = "HoldFire"
    model.Stance = "HoldFire"
    Later(2, function()
        hurt.Health = 30
        Later(48, function()
            local healed = hurt.Health - 30
            Check(healed >= 45 and healed <= 90,
                "Cure heals in casts of 15 ticks every 16, not every tick (" .. healed .. " HP in 48 ticks)")
        end)
        WaitFor("Cure automatically heals the RedElf to full", 200, function() return hurt.Health == hurt.MaxHealth end, function(ok)
            if not ok then return end
            Check(PwTest.Mana(unicorn) < 200, "automatic Cure spends mana (" .. PwTest.Mana(unicorn) .. " left)")
            PwTest.CastOn(unicorn, "fake", model)
            Later(30, function()
                Check(#me.GetActorsByType("primitive-knight") == 0, "no Fake before Study Fake")
                PwTest.CompleteUpgrade(me, "elf-upgrade-fake-unicorn-1")
                PwTest.CastOn(unicorn, "fake", model)
                WaitFor("Fake makes a copy of the Knight, the Unicorn's own", 200, function()
                    return #me.GetActorsByType("primitive-knight") == 1
                end)
            end)
        end)
    end)
end

Meteor = function()
    local spread = Actor.Create("elf-manaspread", true, { Owner = me, Location = CPos.New(4, 24) })
    local foe = Actor.Create("primitive-powerman", true, { Owner = enemy, Location = CPos.New(9, 24) })
    foe.Stance = "HoldFire"
    PwTest.Select(spread)
    Later(2, function()
        PwTest.CastAt(spread, "meteor", CPos.New(9, 24))
        Later(30, function()
            Check(not spread.IsDead, "no Meteor before Study Meteor")
            PwTest.CompleteUpgrade(me, "elf-upgrade-meteor-manaspread-1")
            PwTest.CastAt(spread, "meteor", CPos.New(9, 24))
            WaitFor("Meteor takes the ManaSpread", 200, function() return spread.IsDead end, function(ok)
                if ok then
                    WaitFor("the meteors hurt the PowerMan", 300, function() return foe.IsDead or foe.Health < foe.MaxHealth end)
                end
            end)
        end)
    end)
end

Resurrect = function()
    local angel = Actor.Create("elf-angelelf", true, { Owner = me, Location = CPos.New(16, 26) })
    local fallen = Actor.Create("primitive-soldier", true, { Owner = enemy, Location = CPos.New(20, 27) })
    angel.Stance = "HoldFire"
    Later(2, function()
        fallen.Kill()
        Later(5, function()
            PwTest.CastAt(angel, "resurrect", CPos.New(20, 27))
            WaitFor("Resurrect raises the fallen Soldier, the AngelElf's own", 200, function()
                return #me.GetActorsByType("primitive-soldier") == 1
            end, function(ok)
                if not ok then return end
                local risen = me.GetActorsByType("primitive-soldier")[1]
                Check(risen.Health == risen.MaxHealth / 2, "at half health (" .. risen.Health .. ")")
                Check(PwTest.Mana(angel) < 30, "Resurrect costs 80 mana (" .. PwTest.Mana(angel) .. " left)")
            end)
        end)
    end)
end

ManaPotion = function()
    local red = Actor.Create("elf-redelf", true, { Owner = me, Location = CPos.New(28, 28) })
    red.Stance = "HoldFire"
    Later(2, function()
        Check(PwTest.MaxMana(red) == 100, "a RedElf holds 100 mana")
        PwTest.CompleteUpgrade(me, "elf-upgrade-mana-potion-secret-all-of-elf-1")
        Later(2, function()
            Check(PwTest.MaxMana(red) == 150, "Study Mana Potion Secret raises it to 150 (" .. PwTest.MaxMana(red) .. ")")
        end)
    end)
end

MassTemper = function()
    local white = Actor.Create("elf-whiteelf", true, { Owner = me, Location = CPos.New(28, 20) })
    local a = Actor.Create("elf-redelf", true, { Owner = me, Location = CPos.New(30, 22) })
    local b = Actor.Create("elf-ranger", true, { Owner = me, Location = CPos.New(31, 22) })
    white.Stance = "HoldFire"
    Later(2, function()
        PwTest.GiveExperience(white, 16 * 3)
        PwTest.CastAt(white, "mass-temper", CPos.New(30, 22))
        WaitFor("Mass temper shields the units there", 100, function() return PwTest.Shield(a) > 0 and PwTest.Shield(b) > 0 end, function(ok)
            if ok then
                Check(PwTest.Shield(a) >= 160, "a shield of 160 or more, with spell power (" .. PwTest.Shield(a) .. ")")
            end
        end)
    end)
end

JumpPortal = function()
    local ranger = Actor.Create("elf-ranger", true, { Owner = me, Location = CPos.New(4, 18) })
    local foe = Actor.Create("primitive-powerman", true, { Owner = enemy, Location = CPos.New(8, 20) })
    ranger.Stance = "HoldFire"
    foe.Stance = "HoldFire"
    Later(2, function()
        PwTest.GiveExperience(ranger, 12 * 3)
        PwTest.CastAt(ranger, "jump-portal", CPos.New(8, 20))
        WaitFor("the portal throws the PowerMan away", 100, function() return (foe.Location - CPos.New(8, 20)).Length > 6 end)
    end)
end

BerryFly = function()
    enemy.Cash = 0
    Actor.Create("elf-heaven", true, { Owner = enemy, Location = CPos.New(12, 8) })
    local bush = Actor.Create("berry-bush-0", true, { Owner = Player.GetPlayer("Neutral"), Location = CPos.New(21, 9) })
    local wizard = Actor.Create("elf-wizard", true, { Owner = enemy, Location = CPos.New(20, 11) })
    wizard.Stance = "HoldFire"
    local strayed = false
    Later(2, function()
        PwTest.Gather(wizard, bush)
        WaitFor("the Wizard channels lightning into the berry bush", 100, function()
            return PwTest.LightningActive(wizard)
        end)

        WaitFor("the Wizard guides the berries while they fly", 200, function()
            return PwTest.BerryFlying(wizard)
        end, function(ok)
            if ok then
                Check(PwTest.Gathering(wizard), "the Wizard keeps its casting activity during berry flight")
                Check(not PwTest.LightningActive(wizard), "the gathering beam stops when the berries fly")
                Check(PwTest.Carried(wizard) == 0, "the flying berries are not carried")
                Later(10, function()
                    Check(PwTest.BerryFlying(wizard), "the next gather cycle waits for the berry to arrive")
                end)
            end
        end)

        WaitFor("berries from the Wizard's bush reach the Heaven", 400, function()
            if (wizard.Location - CPos.New(21, 9)).Length > 2 then strayed = true end
            return enemy.Cash >= 12
        end, function(ok)
            if ok then
                Check(not strayed, "the Wizard stays at the bush meanwhile")
                Check(PwTest.Carried(wizard) == 0, "it carries nothing")
            end
        end)
    end)
end
