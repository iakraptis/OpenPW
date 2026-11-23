-- Test: Combat. Merged from combat-test, defence-test, levels-test (research/tools/merge_tests.py): each is a section of
-- this map, 54 cells from the next, with its own players; At(x, y) is a cell of the section. A
-- section's checks carry its name.

-- What a hit does (SubtractArmorDamage): the weapon's damage plus the attacker's attack bonus, minus the target type's
-- armour, at least 1; the numbers come from the rules, so a balance change doesn't break the checks.
local function Hit(weapon, bonus, targetType)
    return math.max(1, PwTest.Weapon(weapon, "Damage") + bonus - PwTest.RuleOr(targetType, "PwArmor", "Value", 0))
end

local BaseCheck, BaseWaitFor, BaseFinish = Check, WaitFor, FinishWhenDone
local Sections, Deadline = { }, 0

-- combat-test
do
    local function At(x, y) return CPos.New(x + 0, y + 0) end
    local function Check(ok, message) return BaseCheck(ok, "Combat: " .. message) end
    local function WaitFor(message, timeout, cond, andThen) return BaseWaitFor("Combat: " .. message, timeout, cond, andThen) end
    local function FinishWhenDone(_, deadline) Deadline = math.max(Deadline, deadline) end
    local AttackMove, Exposure, FirstHit, MeleeAround, Patrolling, MeleeBlocked, MissileModes, Repairing, Voices, WorldLoaded, enemy, me
    -- Combat. Damage is the original rule: attack minus the target's armour, at least 1 (docs/game/rules.md).
    -- 1. A melee hit (Powerman, attack 25) takes 25 from a Buildman (armour 0).
    -- 2. An arrow (Soldier, attack 20) takes 17 from a Powerman (armour 3).
    -- 4. A melee unit can hit a large building (Buildman, attack 10, on a PowerHouse, armour 0).
    -- 3. A weak hit still does 1 (Buildman, attack 10, on a Powerman with Shield of Peace, armour 10).
    -- 5. A worker repairs a damaged friendly building for a price (Builder), in the map's spare corner; not without
    --    the berries for the next step, not a whole one, not an enemy's.
    -- 6. Missiles (PwBullet): a tracking arrow hits a target that moved, and only it; a piercing sword hits each enemy
    --    on its path once, to full range, and passes allies; a fixed-endpoint arrow ignores its path and splashes.
    -- 7. Attack (attack-move): a Soldier sent past an enemy House attacks it on the way.
    -- 8. Patrol: a Soldier goes to the place and back, attacking an enemy beside its way, and goes on.
    -- 9. Exposure (PwExposure, its delay 100 ticks here): a player without a main building is exposed after the
    --    delay; its enemy then sees a marker on its invisible MudMan, not on the Soldier it already sees; a finished
    --    main building hides the player again.
    -- 10. Melee reach (^Melee 1c512): eight Powermen in the eight cells around an enemy Giant all hit it, the
    --    diagonal ones included; a Powerman holding its ground (Defend) two cells from an enemy never reaches it.
    WorldLoaded = function()
        me = Player.GetPlayer("Combat")
        enemy = Player.GetPlayer("Creeps")

        Later(5, function() FirstHit("primitive-powerman", "primitive-buildman", Hit("PowerManHit", 0, "primitive-buildman"), At(4, 4), At(5, 4)) end)
        Later(5, function() FirstHit("primitive-soldier", "primitive-powerman", Hit("SoldierArrow", 0, "primitive-powerman"), At(4, 12), At(10, 12)) end)
        Later(5, function() FirstHit("primitive-buildman", "primitive-powerman-shield-of-peace", Hit("BuildManHit", 0, "primitive-powerman-shield-of-peace"), At(4, 20), At(5, 20)) end)
        Later(5, function() FirstHit("primitive-buildman", "primitive-powerhouse", Hit("BuildManHit", 0, "primitive-powerhouse"), At(20, 20), At(22, 14)) end)
        Later(5, MissileModes)
        Later(5, MeleeBlocked)
        Later(5, AttackMove)
        Later(5, MeleeAround)
        Later(5, Patrolling)
        Later(5, Voices)
        Exposure()
        Later(200, Repairing)
        FinishWhenDone("COMBAT", 2500)
    end

    -- The command panel's Attack: the unit walks to the place and attacks the enemies it meets, buildings included.
    AttackMove = function()
        local foe = Player.GetPlayer("CombatFoe")
        local house = Actor.Create("primitive-house", true, { Owner = foe, Location = At(14, 42) })
        local soldier = Actor.Create("primitive-soldier", true, { Owner = me, Location = At(4, 44) })
        soldier.AttackMove(At(30, 44))
        WaitFor("an attack-moving Soldier attacks the enemy House on its way", 600, function()
            return house.IsDead or house.Health < house.MaxHealth
        end)
    end

    MeleeAround = function()
        local foe = Player.GetPlayer("CombatFoe")
        local giant = Actor.Create("primitive-giant", true, { Owner = foe, Location = At(10, 36) })
        local attackers, hitBy, count = { }, { }, 0
        for dx = -1, 1 do
            for dy = -1, 1 do
                if dx ~= 0 or dy ~= 0 then
                    attackers[#attackers + 1] = Actor.Create("primitive-powerman", true, { Owner = me, Location = At(10 + dx, 36 + dy) })
                end
            end
        end
        Trigger.OnDamaged(giant, function(_, attacker)
            if attacker and not hitBy[attacker] then
                hitBy[attacker] = true
                count = count + 1
            end
        end)

        local far = Actor.Create("primitive-buildman", true, { Owner = foe, Location = At(20, 36) })
        local holder = Actor.Create("primitive-powerman", true, { Owner = me, Location = At(22, 36) })
        Later(1, function()
            giant.Stance = "HoldFire"
            far.Stance = "HoldFire"
            holder.Stance = "Defend"
            for _, a in ipairs(attackers) do a.Attack(giant) end
        end)
        WaitFor("eight Powermen around an enemy all hit it, the diagonal ones too", 120, function() return count == 8 end)
        Later(150, function()
            Check(far.Health == far.MaxHealth, "a Powerman doesn't reach an enemy 2 cells away")
        end)
    end

    -- No main building for the delay: the enemy sees the army as markers (Mod change).
    Exposure = function()
        local foe = Player.GetPlayer("CombatFoe")
        Later(50, function()
            Check(not PwTest.Exposed(me) and PwTest.ExposureTicksLeft(me) > 0, "a player without an HQ is not exposed before the delay")
        end)
        Later(110, function()
            Check(PwTest.Exposed(me) and PwTest.ExposureTicksLeft(me) == 0, "a player without an HQ is exposed after the delay")
            local mudman = Actor.Create("demon-mudman", true, { Owner = me, Location = At(46, 42) })
            local soldier = Actor.Create("primitive-soldier", true, { Owner = me, Location = At(48, 42) })
            Later(2, function()
                Check(not PwTest.CanSee(foe, mudman) and PwTest.Marked(foe, mudman), "the enemy sees an exposed player's invisible unit as a marker")
                Check(not PwTest.Marked(foe, soldier), "no marker on a unit the enemy already sees")
                Check(not PwTest.Marked(me, mudman), "no marker for its owner")
                Actor.Create("primitive-sanctuary", true, { Owner = me, Location = At(40, 46) })
                Later(2, function()
                    Check(not PwTest.Exposed(me) and not PwTest.Marked(foe, mudman), "a main building hides the player again")
                end)
            end)
        end)
    end

    -- The command panel's Patrol: back and forth between where the unit stood and the place, attack-moving.
    Patrolling = function()
        local foe = Player.GetPlayer("CombatFoe")
        local start, far = At(4, 50), At(24, 50)
        local enemy = Actor.Create("primitive-powerman", true, { Owner = foe, Location = At(14, 52) })
        local soldier = Actor.Create("primitive-soldier", true, { Owner = me, Location = start })
        enemy.Stance = "HoldFire"
        PwTest.Patrol(soldier, far)
        WaitFor("a patrolling Soldier attacks an enemy beside its way", 600, function()
            return enemy.IsDead or enemy.Health < enemy.MaxHealth
        end)
        WaitFor("it reaches the far end", 1200, function() return (soldier.Location - far).Length <= 2 end, function(ok)
            if not ok then return end
            WaitFor("it comes back to where it started", 600, function() return (soldier.Location - start).Length <= 2 end, function(back)
                if back then
                    WaitFor("and sets out again", 600, function() return (soldier.Location - start).Length > 4 end)
                end
            end)
        end)
    end

    -- A melee unit whose target has others on every cell in reach fights another enemy near it (PwMeleeAttack), and goes
    -- back to its target once there is a cell free. The other enemy is beyond its automatic targeting (5 cells), so
    -- only that search finds it.
    MeleeBlocked = function()
        local foe = Player.GetPlayer("CombatFoe")
        local target = Actor.Create("primitive-powerman-shield-of-peace", true, { Owner = foe, Location = At(40, 30) })
        local other = Actor.Create("primitive-powerman-shield-of-peace", true, { Owner = foe, Location = At(49, 26) })
        local attacker = Actor.Create("primitive-powerman", true, { Owner = me, Location = At(45, 30) })
        local blockers = { }
        -- Every cell in reach of the target: the 8 around it.
        for dx = -1, 1 do
            for dy = -1, 1 do
                if dx ~= 0 or dy ~= 0 then
                    blockers[#blockers + 1] = Actor.Create("primitive-buildman", true, { Owner = me, Location = At(40 + dx, 30 + dy) })
                end
            end
        end

        Later(2, function()
            target.Stance = "HoldFire"
            other.Stance = "HoldFire"
            for _, blocker in ipairs(blockers) do blocker.Stance = "HoldFire" end
            attacker.Attack(target)
        end)
        WaitFor("a blocked melee unit fights another enemy", 400, function() return other.Health < other.MaxHealth end, function(ok)
            if not ok then return end
            Check(target.Health == target.MaxHealth, "the surrounded target was left alone meanwhile")
            other.Kill()
            blockers[1].Kill()
            WaitFor("a blocked melee unit returns to its target", 400, function() return target.Health < target.MaxHealth end)
        end)
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

    MissileModes = function()
        local tracker = Actor.Create("primitive-soldier", true, { Owner = me, Location = At(4, 24) })
        local movingTarget = Actor.Create("primitive-powerman", true, { Owner = enemy, Location = At(10, 24) })
        movingTarget.Stance = "HoldFire"
        local healthBeforeMove
        Later(1, function()
            tracker.Attack(movingTarget)
            Later(2, function()
                healthBeforeMove = movingTarget.Health
                movingTarget.Move(At(10, 27))
            end)
        end)
        WaitFor("the first mode-2 arrow hits after its target moves after launch", 32, function()
            return healthBeforeMove ~= nil and movingTarget.Health < healthBeforeMove
        end, function(ok)
            if ok then
                Check(healthBeforeMove - movingTarget.Health == Hit("SoldierArrow", 0, "primitive-powerman"),
                    "the first tracking arrow hits the moved target exactly once")
            end
        end)

        local wildShooter = Actor.Create("wild-dilophosaurus", true, { Owner = enemy, Location = At(4, 32) })
        local wildTarget = Actor.Create("primitive-powerman", true, { Owner = me, Location = At(8, 32) })
        wildTarget.Stance = "HoldFire"
        Later(1, function() wildShooter.Attack(wildTarget) end)
        WaitFor("a wildlife missile damages its target", 300, function()
            return wildTarget.Health < wildTarget.MaxHealth
        end)

        local piercer = Actor.Create("primitive-knight", true, { Owner = me, Location = At(4, 28) })
        local aimedTarget = Actor.Create("primitive-giant", true, { Owner = enemy, Location = At(7, 28) })
        local fullRangeTarget = Actor.Create("primitive-giant", true, { Owner = enemy, Location = At(9, 28) })
        aimedTarget.Stance = "HoldFire"
        fullRangeTarget.Stance = "HoldFire"
        Later(1, function() piercer.Attack(aimedTarget) end)
        WaitFor("a mode-4 sword continues to full range after hitting its target", 300, function()
            return aimedTarget.Health < aimedTarget.MaxHealth and fullRangeTarget.Health < fullRangeTarget.MaxHealth
        end, function(ok)
            if not ok then return end
            piercer.Stop()
            local sword = Hit("ShootSword", 0, "primitive-giant")
            Check(aimedTarget.MaxHealth - aimedTarget.Health == sword, "a mode-4 sword hits its aimed target once")
            Check(fullRangeTarget.MaxHealth - fullRangeTarget.Health == sword,
                "a mode-4 sword hits an actor beyond its aimed target once")
        end)

        -- A tracking arrow without splash hurts only its target, not one standing in the same cell (the original hands
        -- the damage straight to the target).
        local archer = Actor.Create("primitive-soldier", true, { Owner = me, Location = At(12, 16) })
        local marked = Actor.Create("primitive-powerman", true, { Owner = enemy, Location = At(18, 16) })
        local bystander = Actor.Create("primitive-powerman", true, { Owner = enemy, Location = At(18, 16) })
        marked.Stance = "HoldFire"
        bystander.Stance = "HoldFire"
        Later(1, function() archer.Attack(marked) end)
        WaitFor("a mode-2 arrow hits its target", 300, function() return marked.Health < marked.MaxHealth end, function(ok)
            if not ok then return end
            archer.Stop()
            Check(bystander.Health == bystander.MaxHealth, "a mode-2 arrow without splash spares the unit beside its target")
        end)

        -- A piercing sword passes through the thrower's allies (Pw.exe FUN_0047afc0 skips them).
        local thrower = Actor.Create("primitive-knight", true, { Owner = me, Location = At(10, 8) })
        local friend = Actor.Create("primitive-powerman", true, { Owner = me, Location = At(12, 8) })
        local foe = Actor.Create("primitive-giant", true, { Owner = enemy, Location = At(14, 8) })
        friend.Stance = "HoldFire"
        foe.Stance = "HoldFire"
        Later(1, function() thrower.Attack(foe) end)
        WaitFor("a mode-4 sword thrown past an ally hits the enemy", 300, function() return foe.Health < foe.MaxHealth end,
            function(ok)
                if not ok then return end
                thrower.Stop()
                Check(friend.Health == friend.MaxHealth, "a mode-4 sword passes through an ally")
            end)

        local bowMachine = Actor.Create("primitive-bowmachine", true, { Owner = me, Location = At(20, 28) })
        local pathTarget = Actor.Create("primitive-powerman", true, { Owner = enemy, Location = At(23, 28) })
        local splashTarget = Actor.Create("primitive-powerman", true, { Owner = enemy, Location = At(26, 28) })
        local endpointTarget = Actor.Create("primitive-powerman", true, { Owner = enemy, Location = At(27, 28) })
        pathTarget.Stance = "HoldFire"
        splashTarget.Stance = "HoldFire"
        endpointTarget.Stance = "HoldFire"
        Later(1, function() bowMachine.Attack(endpointTarget) end)
        WaitFor("a mode-0 arrow ignores actors along its path and splashes at its endpoint", 400, function()
            return endpointTarget.Health < endpointTarget.MaxHealth
        end, function(ok)
            if not ok then return end
            Check(pathTarget.Health == pathTarget.MaxHealth, "a mode-0 arrow does not hit an actor along its path")
            Check(splashTarget.Health < splashTarget.MaxHealth and splashTarget.Health > endpointTarget.Health,
                "mode-0 splash damage falls with distance from the endpoint")
        end)
    end

    -- 5. Units have their original voices (voices/units.yaml): a Buildman can be selected, ordered, work, finish a
    --    building and die with its own sounds; a Knight has attack sounds.
    Voices = function()
        local man = Actor.Create("primitive-buildman", true, { Owner = me, Location = At(28, 4) })
        local knight = Actor.Create("primitive-knight", true, { Owner = me, Location = At(28, 8) })
        Later(1, function()
            local voices = { "Select", "Action", "Die", "Work", "Built" }
            local all = true
            for _, v in ipairs(voices) do if not PwTest.HasVoice(man, v) then all = false end end
            Check(all, "a Buildman has its select, order, death, work and built voices")
            Check(PwTest.HasVoice(knight, "Attack"), "a Knight has its attack sound")
        end)
    end

    Repairing = function()
        local function RepairAt(x, y) return CPos.New(x + 54, y + 54) end
        me.Cash = 20000
        local worker = Actor.Create("primitive-buildman", true, { Owner = me, Location = RepairAt(31, 10) })
        local tower = Actor.Create("primitive-stonetower", true, { Owner = me, Location = RepairAt(28, 10) })
        tower.Health = tower.MaxHealth - 10
        Later(1, function()
            Check(PwTest.CanOrder(worker, "PwRepairBuilding", tower), "a worker can target a damaged friendly building")
            PwTest.OrderOn(worker, "PwRepairBuilding", tower)
        end)

        WaitFor("a worker repairs a building", 400, function() return tower.Health == tower.MaxHealth end, function(ok)
            if ok then
                -- RepairBuildingActivity: RepairStep HP at a time, each for its share of RepairPercent of the value.
                local percent = PwTest.Rule("primitive-buildman", "Builder", "RepairPercent")
                local step = PwTest.Rule("primitive-buildman", "Builder", "RepairStep")
                local value = PwTest.Rule("primitive-stonetower", "Valued", "Cost")
                local cost, left = 0, 10
                while left > 0 do
                    local hp = math.min(step, left)
                    cost = cost + math.max(1, math.floor(hp * percent * value / (tower.MaxHealth * 100)))
                    left = left - hp
                end
                Check(me.Cash == 20000 - cost, "repairing 10 HP costs " .. cost .. " at the " .. percent .. "% repair price (paid " .. 20000 - me.Cash .. ")")
                Check(not PwTest.CanOrder(worker, "PwRepairBuilding", tower), "a worker cannot target a fully repaired building")
            end

            local brokeWorker = Actor.Create("primitive-buildman", true, { Owner = me, Location = RepairAt(31, 20) })
            local brokeTower = Actor.Create("primitive-stonetower", true, { Owner = me, Location = RepairAt(28, 20) })
            brokeTower.Health = brokeTower.MaxHealth - 7
            me.Cash = 0
            Later(1, function() PwTest.OrderOn(brokeWorker, "PwRepairBuilding", brokeTower) end)
            Later(41, function()
                Check(brokeTower.Health == brokeTower.MaxHealth - 7 and me.Cash == 0,
                    "a worker does not repair when its owner cannot afford the next step")
            end)

            local enemyTower = Actor.Create("primitive-stonetower", true, { Owner = enemy, Location = RepairAt(20, 10) })
            enemyTower.Health = enemyTower.MaxHealth - 10
            Later(1, function()
                Check(not PwTest.CanOrder(worker, "PwRepairBuilding", enemyTower), "a worker cannot repair an enemy building")
            end)
        end)
    end
    Sections[#Sections + 1] = WorldLoaded
end

-- defence-test
do
    local function At(x, y) return CPos.New(x + 54, y + 0) end
    local function Check(ok, message) return BaseCheck(ok, "Defence: " .. message) end
    local function WaitFor(message, timeout, cond, andThen) return BaseWaitFor("Defence: " .. message, timeout, cond, andThen) end
    local function FinishWhenDone(_, deadline) Deadline = math.max(Deadline, deadline) end
    local Bubble, FirstHit, Stealth, Tower, Trap, Unfinished, WorldLoaded, enemy, me
    -- Defences. Enemies here belong to Creeps.
    -- 1. A StoneTower stones an enemy in range on its own: 40 damage minus armour.
    -- 2. A Trap snaps at an enemy beside it: 80 damage.
    -- 3. A StoneTower still under construction doesn't shoot, and doesn't count as a prerequisite yet.
    -- 4. Stealth: a Trap is invisible to the other players (not to its owner). Their Traps see it (over their sight,
    --    7 cells); their StoneTowers only once Improve StoneTower Detection is researched (then over 11 cells).
    -- 5. Bubble: the Elf tower's 2 damage reaches flying units (a Death Eye, seen through a Pixie beside the tower),
    --    and outpaces a wild creature's healing (1 HP every 32 ticks): a Velociraptor loses at least 10 HP net in 400
    --    ticks (about 18; at the old 1 damage, about 3).
    WorldLoaded = function()
        me = Player.GetPlayer("Defence")
        enemy = Player.GetPlayer("Creeps")
        me.Cash = 20000

        Later(5, Tower)
        Later(5, Trap)
        Later(5, Unfinished)
        Later(5, Stealth)
        Later(5, Bubble)
        FinishWhenDone("DEFENCE", 2000)
    end

    Stealth = function()
        local hidden = Actor.Create("primitive-trap", true, { Owner = me, Location = At(14, 28) })
        -- 7 cells away: within the tower's sight, but it doesn't detect before the research.
        Actor.Create("primitive-stonetower", true, { Owner = enemy, Location = At(21, 28) })
        local spotted = Actor.Create("primitive-trap", true, { Owner = me, Location = At(4, 24) })
        Later(5, function()
            Check(not PwTest.CanSee(enemy, hidden), "a Trap is hidden from the other players")
            Check(PwTest.CanSee(me, hidden), "its owner sees it")
            Actor.Create("primitive-trap", true, { Owner = enemy, Location = At(8, 24) })
            Later(5, function()
                Check(PwTest.CanSee(enemy, spotted), "a Trap of theirs 4 cells away reveals it")
                PwTest.CompleteUpgrade(enemy, "primitive-upgrade-stonetower-detection-1")
                Later(5, function()
                    Check(PwTest.CanSee(enemy, hidden), "their StoneTower 7 cells away reveals it after Improve StoneTower Detection")
                end)
            end)
        end)
    end

    Bubble = function()
        Actor.Create("elf-bubble", true, { Owner = me, Location = At(18, 41) })
        local pixie = Actor.Create("elf-pixie", true, { Owner = me, Location = At(20, 43) })
        local eye = Actor.Create("demon-deatheye", true, { Owner = enemy, Location = At(24, 41) })
        Actor.Create("elf-bubble", true, { Owner = me, Location = At(18, 51) })
        local raptor = Actor.Create("wild-velociraptor", true, { Owner = enemy, Location = At(23, 51) })
        Later(1, function() pixie.Stance = "HoldFire" end)
        FirstHit("the Bubble hits a flying Death Eye", eye, Hit("BubbleShot", 0, "demon-deatheye"), 300)

        local start
        WaitFor("the Bubble hits a wild Velociraptor", 300, function() return raptor.Health < raptor.MaxHealth end, function(ok)
            if not ok then return end
            start = raptor.Health
            Later(400, function()
                local lost = raptor.IsDead and start or start - raptor.Health
                Check(lost >= 10, "the Bubble outpaces a wild Velociraptor's healing (lost " .. lost .. " HP in 400 ticks)")
            end)
        end)
    end

    FirstHit = function(label, target, damage, timeout)
        local full = target.MaxHealth
        WaitFor(label, timeout, function() return target.IsDead or target.Health < full end, function(ok)
            if ok and not target.IsDead then
                Check(full - target.Health == damage, label .. " for " .. damage .. " (did " .. (full - target.Health) .. ")")
            end
        end)
    end

    Tower = function()
        Actor.Create("primitive-stonetower", true, { Owner = me, Location = At(4, 4) })
        -- A Powerman 6 cells away, inside the 9-cell range.
        local target = Actor.Create("primitive-powerman", true, { Owner = enemy, Location = At(11, 5) })
        target.Stance = "HoldFire"
        FirstHit("a StoneTower stones an enemy in range", target, Hit("Stone", 0, "primitive-powerman"), 300)
    end

    Trap = function()
        Actor.Create("primitive-trap", true, { Owner = me, Location = At(4, 16) })
        local target = Actor.Create("primitive-giant", true, { Owner = enemy, Location = At(6, 16) })
        target.Stance = "HoldFire"
        FirstHit("a Trap snaps at an enemy beside it", target, Hit("TrapSnap", 0, "primitive-giant"), 300)
    end

    Unfinished = function()
        Check(not PwTest.Available(me, "primitive-trap"), "the Trap needs a LandIncreaser")
        local builder = Actor.Create("primitive-buildman", true, { Owner = me, Location = At(20, 24) })
        -- Available here only once the PowerHouse is finished: built by a worker, it starts unfinished.
        Actor.Create("primitive-powerhouse", true, { Owner = me, Location = At(24, 2) })
        Later(2, function() PwTest.Construct(builder, "primitive-stonetower", At(20, 20)) end)
        local builder2 = Actor.Create("primitive-buildman", true, { Owner = me, Location = At(14, 24) })
        Later(2, function() PwTest.Construct(builder2, "primitive-landincreaser", At(10, 22)) end)
        Later(10, function()
            Check(#me.GetActorsByType("primitive-landincreaser") == 1, "a Buildman starts a LandIncreaser")
            Check(not PwTest.Available(me, "primitive-trap"), "a LandIncreaser under construction doesn't unlock the Trap")
        end)
        local passer = Actor.Create("primitive-buildman", true, { Owner = enemy, Location = At(24, 26) })
        passer.Stance = "HoldFire"
        Later(10, function()
            local tower = me.GetActorsByType("primitive-stonetower")
            local site
            for _, t in ipairs(tower) do if t.Location.X == At(20, 20).X then site = t end end
            Check(site ~= nil and not PwTest.Finished(site), "a Buildman starts a StoneTower")
            if site == nil then return end
            Later(100, function()
                Check(PwTest.Finished(site) or passer.Health == passer.MaxHealth, "an unfinished StoneTower doesn't shoot")
            end)
        end)
    end
    Sections[#Sections + 1] = WorldLoaded
end

-- levels-test
do
    local function At(x, y) return CPos.New(x + 0, y + 54) end
    local function Check(ok, message) return BaseCheck(ok, "Levels: " .. message) end
    local function WaitFor(message, timeout, cond, andThen) return BaseWaitFor("Levels: " .. message, timeout, cond, andThen) end
    local function FinishWhenDone(_, deadline) Deadline = math.max(Deadline, deadline) end
    local DinosaurKill, Kill, LevelNine, LevelUp, LongRange, MachineKill, WorldLoaded, enemy, me
    -- Levels (docs/game/rules.md). A kill gives the killer the victim's population as experience: units, machines and
    -- wild creatures alike. Every XpPerLevel is a level, up to 9; attack and armour grow to their caps, and Level.lvt
    -- adds range (Soldier +160 px at level 9).
    WorldLoaded = function()
        me = Player.GetPlayer("Levels")
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
            -- Holding fire, it hits only its ordered victim: left free, a chaser could kill another check's victim.
            attacker.Stance = "HoldFire"
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
        Kill("killing a BowMachine", "primitive-soldier", At(3, 3), "primitive-bowmachine", At(8, 3), enemy, 4)
    end

    DinosaurKill = function()
        Kill("killing a Galimimus", "primitive-powerman", At(3, 12), "wild-galimimus", At(4, 12), enemy, 4)
    end

    LevelUp = function()
        local man = Actor.Create("primitive-buildman", true, { Owner = me, Location = At(28, 3) })
        -- XpPerLevel per level: two levels' worth and 5 more is level 2 with 5 over.
        local xp = PwTest.Rule("primitive-buildman", "PwLevels", "XpPerLevel")
        local maxLevel = PwTest.Rule("primitive-buildman", "PwLevels", "MaxLevel")
        PwTest.GiveExperience(man, 2 * xp + 5)
        Check(PwTest.Level(man) == 2 and PwTest.Experience(man) == 5, (2 * xp + 5) .. " experience makes a Buildman level 2 with 5 over")
        PwTest.GiveExperience(man, xp * maxLevel * 10)
        Check(PwTest.Level(man) == maxLevel, "levels stop at " .. maxLevel)
    end

    LevelNine = function()
        -- A Buildman at the top level has its whole AttackGain.
        local man = Actor.Create("primitive-buildman", true, { Owner = me, Location = At(3, 20) })
        local target = Actor.Create("primitive-buildman", true, { Owner = enemy, Location = At(4, 20) })
        PwTest.GiveLevels(man, PwTest.Rule("primitive-buildman", "PwLevels", "MaxLevel"))
        local hits = Hit("BuildManHit", PwTest.Rule("primitive-buildman", "PwLevels", "AttackGain"), "primitive-buildman")
        Later(1, function()
            target.Stance = "HoldFire"
            man.Attack(target)
        end)
        WaitFor("a level 9 Buildman hits", 300, function() return target.Health < target.MaxHealth end, function(ok)
            if ok then Check(target.MaxHealth - target.Health == hits, "a top-level Buildman hits for " .. hits .. " (did " .. (target.MaxHealth - target.Health) .. ")") end
        end)
    end

    LongRange = function()
        -- A level 9 Soldier reaches 256 + 160 = 416 px (13 cells); a new one only 8.
        local soldier = Actor.Create("primitive-soldier", true, { Owner = me, Location = At(16, 28) })
        local target = Actor.Create("primitive-buildman", true, { Owner = enemy, Location = At(16, 17) })
        PwTest.GiveLevels(soldier, PwTest.Rule("primitive-soldier", "PwLevels", "MaxLevel"))
        Later(1, function()
            target.Stance = "HoldFire"
            soldier.Attack(target, false)
        end)
        WaitFor("a level 9 Soldier hits from 11 cells without moving", 300, function() return target.Health < target.MaxHealth end)
    end
    Sections[#Sections + 1] = WorldLoaded
end

WorldLoaded = function()
    for _, start in ipairs(Sections) do start() end
    BaseFinish("COMBAT", Deadline)
end
