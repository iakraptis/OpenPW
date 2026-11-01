-- Combat. Damage is the original rule: attack minus the target's armour, at least 1 (docs/game/rules.md).
-- 1. A melee hit (Powerman, attack 25) takes 25 from a Buildman (armour 0).
-- 2. An arrow (Soldier, attack 20) takes 17 from a Powerman (armour 3).
-- 4. A melee unit can hit a large building (Buildman, attack 10, on a PowerHouse, armour 0).
-- 3. A weak hit still does 1 (Buildman, attack 10, on a Powerman with Shield of Peace, armour 10).
WorldLoaded = function()
    me = Player.GetPlayer("Multi0")
    enemy = Player.GetPlayer("Creeps")

    Later(5, function() FirstHit("primitive-powerman", "primitive-buildman", 25, CPos.New(4, 4), CPos.New(5, 4)) end)
    Later(5, function() FirstHit("primitive-soldier", "primitive-powerman", 17, CPos.New(4, 12), CPos.New(10, 12)) end)
    Later(5, function() FirstHit("primitive-buildman", "primitive-powerman-shield-of-peace", 1, CPos.New(4, 20), CPos.New(5, 20)) end)
    Later(5, function() FirstHit("primitive-buildman", "primitive-powerhouse", 10, CPos.New(20, 20), CPos.New(22, 14)) end)
    FinishWhenDone("COMBAT", 2000)
end

-- The attacker attacks; the first change in the target's health must be exactly the expected damage.
FirstHit = function(attackerType, targetType, damage, at, targetAt)
    local attacker = Actor.Create(attackerType, true, { Owner = me, Location = at })
    local target = Actor.Create(targetType, true, { Owner = enemy, Location = targetAt })
    local full = target.MaxHealth
    -- Created actors join the world at the end of the tick: order the attack on the next one.
    Later(1, function() attacker.Attack(target) end)
    local label = attackerType .. " hits " .. targetType
    WaitFor(label, 400, function() return target.IsDead or target.Health < full end, function(ok)
        if ok and not target.IsDead then
            Check(full - target.Health == damage, label .. " for " .. damage .. " (did " .. (full - target.Health) .. ")")
        end
    end)
end
