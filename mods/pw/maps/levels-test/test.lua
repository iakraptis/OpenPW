-- Levels (docs/game/rules.md). A kill gives the killer the victim's population as experience: units, machines and
-- wild creatures alike. Every XpPerLevel is a level, up to 9; attack and armour grow to their caps, and Level.lvt
-- adds range (Soldier +160 px at level 9).
WorldLoaded = function()
    me = Player.GetPlayer("Multi0")
    enemy = Player.GetPlayer("Creeps")

    Later(5, MachineKill)
    Later(5, DinosaurKill)
    Later(5, LevelUp)
    Later(5, LevelNine)
    Later(5, LongRange)
    FinishWhenDone("LEVELS", 2500)
end

-- The attacker kills the victim; then its experience must be the victim's population.
Kill = function(label, attackerType, at, victimType, victimAt, owner, experience)
    local attacker = Actor.Create(attackerType, true, { Owner = me, Location = at })
    local victim = Actor.Create(victimType, true, { Owner = owner, Location = victimAt })
    Later(1, function()
        victim.Stance = "HoldFire"
        attacker.Attack(victim)
    end)
    WaitFor(label, 2000, function() return victim.IsDead end, function(ok)
        if ok then
            Check(PwTest.Experience(attacker) == experience,
                label .. " gives " .. experience .. " experience (got " .. PwTest.Experience(attacker) .. ")")
        end
    end)
end

MachineKill = function()
    Kill("killing a BowMachine", "primitive-soldier", CPos.New(3, 3), "primitive-bowmachine", CPos.New(8, 3), enemy, 4)
end

DinosaurKill = function()
    Kill("killing a Galimimus", "primitive-powerman", CPos.New(3, 12), "wild-galimimus", CPos.New(4, 12), enemy, 4)
end

LevelUp = function()
    local man = Actor.Create("primitive-buildman", true, { Owner = me, Location = CPos.New(28, 3) })
    -- 10 per level: 25 experience is level 2 with 5 over.
    PwTest.GiveExperience(man, 25)
    Check(PwTest.Level(man) == 2 and PwTest.Experience(man) == 5, "25 experience makes a Buildman level 2 with 5 over")
    PwTest.GiveExperience(man, 1000)
    Check(PwTest.Level(man) == 9, "levels stop at 9")
end

LevelNine = function()
    -- A level 9 Buildman hits for 10 + 18 = 28 on armour 0.
    local man = Actor.Create("primitive-buildman", true, { Owner = me, Location = CPos.New(3, 20) })
    local target = Actor.Create("primitive-buildman", true, { Owner = enemy, Location = CPos.New(4, 20) })
    PwTest.GiveExperience(man, 90)
    Later(1, function()
        target.Stance = "HoldFire"
        man.Attack(target)
    end)
    WaitFor("a level 9 Buildman hits", 300, function() return target.Health < target.MaxHealth end, function(ok)
        if ok then Check(target.MaxHealth - target.Health == 28, "a level 9 Buildman hits for 28 (did " .. (target.MaxHealth - target.Health) .. ")") end
    end)
end

LongRange = function()
    -- A level 9 Soldier reaches 256 + 160 = 416 px (13 cells); a new one only 8.
    local soldier = Actor.Create("primitive-soldier", true, { Owner = me, Location = CPos.New(16, 28) })
    local target = Actor.Create("primitive-buildman", true, { Owner = enemy, Location = CPos.New(16, 17) })
    PwTest.GiveExperience(soldier, 9 * 14)
    Later(1, function()
        target.Stance = "HoldFire"
        soldier.Attack(target, false)
    end)
    WaitFor("a level 9 Soldier hits from 11 cells without moving", 300, function() return target.Health < target.MaxHealth end)
end
