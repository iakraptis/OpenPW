-- Test: Combat. Merged from combat-test, defence-test, levels-test (research/tools/merge_tests.py): each is a section of
-- this map, 54 cells from the next, with its own players; At(x, y) is a cell of the section. A
-- section's checks carry its name.
local BaseCheck, BaseWaitFor, BaseFinish = Check, WaitFor, FinishWhenDone
local Sections, Deadline = { }, 0

-- combat-test
do
    local function At(x, y) return CPos.New(x + 0, y + 0) end
    local function Check(ok, message) return BaseCheck(ok, "Combat: " .. message) end
    local function WaitFor(message, timeout, cond, andThen) return BaseWaitFor("Combat: " .. message, timeout, cond, andThen) end
    local function FinishWhenDone(_, deadline) Deadline = math.max(Deadline, deadline) end
    local FirstHit, MissileModes, Voices, WorldLoaded, enemy, me
    -- Combat. Damage is the original rule: attack minus the target's armour, at least 1 (docs/game/rules.md).
    -- 1. A melee hit (Powerman, attack 25) takes 25 from a Buildman (armour 0).
    -- 2. An arrow (Soldier, attack 20) takes 17 from a Powerman (armour 3).
    -- 4. A melee unit can hit a large building (Buildman, attack 10, on a PowerHouse, armour 0).
    -- 3. A weak hit still does 1 (Buildman, attack 10, on a Powerman with Shield of Peace, armour 10).
    WorldLoaded = function()
        me = Player.GetPlayer("Combat")
        enemy = Player.GetPlayer("Creeps")

        Later(5, function() FirstHit("primitive-powerman", "primitive-buildman", 25, At(4, 4), At(5, 4)) end)
        Later(5, function() FirstHit("primitive-soldier", "primitive-powerman", 17, At(4, 12), At(10, 12)) end)
        Later(5, function() FirstHit("primitive-buildman", "primitive-powerman-shield-of-peace", 1, At(4, 20), At(5, 20)) end)
        Later(5, function() FirstHit("primitive-buildman", "primitive-powerhouse", 10, At(20, 20), At(22, 14)) end)
        Later(5, MissileModes)
        Later(5, Voices)
        FinishWhenDone("COMBAT", 2500)
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
            Later(14, function()
                healthBeforeMove = movingTarget.Health
                movingTarget.Move(At(11, 24))
            end)
        end)
        WaitFor("a mode-2 arrow follows a target that moves after launch", 300, function()
            return healthBeforeMove ~= nil and movingTarget.Health < healthBeforeMove
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
            Check(aimedTarget.MaxHealth - aimedTarget.Health == 40, "a mode-4 sword hits its aimed target once")
            Check(fullRangeTarget.MaxHealth - fullRangeTarget.Health == 40,
                "a mode-4 sword hits an actor beyond its aimed target once")
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
    Sections[#Sections + 1] = WorldLoaded
end

-- defence-test
do
    local function At(x, y) return CPos.New(x + 54, y + 0) end
    local function Check(ok, message) return BaseCheck(ok, "Defence: " .. message) end
    local function WaitFor(message, timeout, cond, andThen) return BaseWaitFor("Defence: " .. message, timeout, cond, andThen) end
    local function FinishWhenDone(_, deadline) Deadline = math.max(Deadline, deadline) end
    local FirstHit, Stealth, Tower, Trap, Unfinished, WorldLoaded, enemy, me
    -- Defences. Enemies here belong to Creeps.
    -- 1. A StoneTower stones an enemy in range on its own: 40 damage minus armour.
    -- 2. A Trap snaps at an enemy beside it: 80 damage.
    -- 3. A StoneTower still under construction doesn't shoot, and doesn't count as a prerequisite yet.
    -- 4. Stealth: a Trap is invisible to the other players (not to its owner). Their Traps see it (over their sight,
    --    7 cells); their StoneTowers only once Improve StoneTower Detection is researched (then over 11 cells).
    WorldLoaded = function()
        me = Player.GetPlayer("Defence")
        enemy = Player.GetPlayer("Creeps")
        me.Cash = 20000

        Later(5, Tower)
        Later(5, Trap)
        Later(5, Unfinished)
        Later(5, Stealth)
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
        -- Powerman: armour 3, so a stone does 37. 6 cells away, inside the 9-cell range.
        local target = Actor.Create("primitive-powerman", true, { Owner = enemy, Location = At(11, 5) })
        target.Stance = "HoldFire"
        FirstHit("a StoneTower stones an enemy in range", target, 37, 300)
    end

    Trap = function()
        Actor.Create("primitive-trap", true, { Owner = me, Location = At(4, 16) })
        local target = Actor.Create("primitive-giant", true, { Owner = enemy, Location = At(6, 16) })
        target.Stance = "HoldFire"
        -- Giant: armour 5, so a snap does 75.
        FirstHit("a Trap snaps at an enemy beside it", target, 75, 300)
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
        -- 10 per level: 25 experience is level 2 with 5 over.
        PwTest.GiveExperience(man, 25)
        Check(PwTest.Level(man) == 2 and PwTest.Experience(man) == 5, "25 experience makes a Buildman level 2 with 5 over")
        PwTest.GiveExperience(man, 1000)
        Check(PwTest.Level(man) == 9, "levels stop at 9")
    end

    LevelNine = function()
        -- A level 9 Buildman hits for 10 + 18 = 28 on armour 0.
        local man = Actor.Create("primitive-buildman", true, { Owner = me, Location = At(3, 20) })
        local target = Actor.Create("primitive-buildman", true, { Owner = enemy, Location = At(4, 20) })
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
        local soldier = Actor.Create("primitive-soldier", true, { Owner = me, Location = At(16, 28) })
        local target = Actor.Create("primitive-buildman", true, { Owner = enemy, Location = At(16, 17) })
        PwTest.GiveExperience(soldier, 9 * 14)
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
