-- The Chief (docs/game/rules.md): morale and Fall out.
-- 1. Morale: Primitive units within 5 cells of their Chief attack and defend 10% better.
-- 2. Fall out needs its upgrade; then, for 80 of his 100 mana, it wears an enemy down to 1 HP without killing it.
-- 3. Mana refills over time.
WorldLoaded = function()
    me = Player.GetPlayer("Multi0")
    enemy = Player.GetPlayer("Creeps")

    Later(5, MoraleAttack)
    Later(5, MoraleArmour)
    Later(5, FallOut)
    FinishWhenDone("CHIEF", 2500)
end

FirstHit = function(label, attacker, target, damage)
    local full = target.MaxHealth
    Later(2, function()
        target.Stance = "HoldFire"
        attacker.Attack(target)
    end)
    WaitFor(label, 400, function() return target.IsDead or target.Health < full end, function(ok)
        if ok and not target.IsDead then
            Check(full - target.Health == damage, label .. " for " .. damage .. " (did " .. (full - target.Health) .. ")")
        end
    end)
end

MoraleAttack = function()
    Actor.Create("primitive-chief", true, { Owner = me, Location = CPos.New(4, 4) })
    local attacker = Actor.Create("primitive-powerman", true, { Owner = me, Location = CPos.New(6, 4) })
    local target = Actor.Create("primitive-buildman", true, { Owner = enemy, Location = CPos.New(7, 4) })
    -- 25 attack, +10%: 27.
    FirstHit("a Powerman beside his Chief hits harder", attacker, target, 27)
end

MoraleArmour = function()
    Actor.Create("primitive-chief", true, { Owner = me, Location = CPos.New(4, 16) })
    local defender = Actor.Create("primitive-powerman-shield-of-peace", true, { Owner = me, Location = CPos.New(6, 16) })
    local attacker = Actor.Create("primitive-powerman", true, { Owner = enemy, Location = CPos.New(7, 16) })
    Later(1, function() defender.Stance = "HoldFire" end)
    -- 25 attack against armour 10 + 10% = 11: 14 (15 without the Chief).
    FirstHit("an enemy hits a Powerman beside his Chief less", attacker, defender, 14)
end

FallOut = function()
    local chief = Actor.Create("primitive-chief", true, { Owner = me, Location = CPos.New(20, 20) })
    local target = Actor.Create("primitive-knight", true, { Owner = enemy, Location = CPos.New(24, 20) })
    Later(2, function()
        target.Stance = "HoldFire"
        chief.Stance = "HoldFire"
        PwTest.OrderOn(chief, "PwFallOut", target)
    end)
    Later(60, function()
        Check(target.Health == target.MaxHealth and PwTest.Mana(chief) == 100, "Fall out does nothing before its upgrade")
        PwTest.CompleteUpgrade(me, "primitive-upgrade-fall-out-for-chief-1")
        PwTest.OrderOn(chief, "PwFallOut", target)
        WaitFor("Fall out wears the target down", 600, function() return target.IsDead or target.Health == 1 end, function(ok)
            if not ok then return end
            Check(not target.IsDead and target.Health == 1, "Fall out leaves the target at 1 HP")
            local mana = PwTest.Mana(chief)
            Check(mana < 100, "Fall out costs mana (" .. mana .. " left)")
            Later(100, function() Check(PwTest.Mana(chief) > mana, "mana refills over time") end)
        end)
    end)
end
