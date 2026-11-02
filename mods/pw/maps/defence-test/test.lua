-- Defences. Enemies here belong to Creeps.
-- 1. A StoneTower stones an enemy in range on its own: 40 damage minus armour.
-- 2. A Trap snaps at an enemy beside it: 80 damage.
-- 3. A StoneTower still under construction doesn't shoot, and doesn't count as a prerequisite yet.
-- 4. Stealth: a Trap is hidden from the other players (not from its owner). Their StoneTowers reveal it within 5
--    cells, 8 with Improve StoneTower Detection; their Traps within 5.
WorldLoaded = function()
    me = Player.GetPlayer("Multi0")
    enemy = Player.GetPlayer("Creeps")
    me.Cash = 20000

    Later(5, Tower)
    Later(5, Trap)
    Later(5, Unfinished)
    Later(5, Stealth)
    FinishWhenDone("DEFENCE", 2000)
end

Stealth = function()
    local hidden = Actor.Create("primitive-trap", true, { Owner = me, Location = CPos.New(14, 28) })
    -- 7 cells away: beyond a tower's 5, within the upgraded 8.
    Actor.Create("primitive-stonetower", true, { Owner = enemy, Location = CPos.New(21, 28) })
    local spotted = Actor.Create("primitive-trap", true, { Owner = me, Location = CPos.New(4, 24) })
    Later(5, function()
        Check(not PwTest.CanSee(enemy, hidden), "a Trap is hidden from the other players")
        Check(PwTest.CanSee(me, hidden), "its owner sees it")
        Actor.Create("primitive-trap", true, { Owner = enemy, Location = CPos.New(8, 24) })
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
    Actor.Create("primitive-stonetower", true, { Owner = me, Location = CPos.New(4, 4) })
    -- Powerman: armour 3, so a stone does 37. 6 cells away, inside the 9-cell range.
    local target = Actor.Create("primitive-powerman", true, { Owner = enemy, Location = CPos.New(11, 5) })
    target.Stance = "HoldFire"
    FirstHit("a StoneTower stones an enemy in range", target, 37, 300)
end

Trap = function()
    Actor.Create("primitive-trap", true, { Owner = me, Location = CPos.New(4, 16) })
    local target = Actor.Create("primitive-giant", true, { Owner = enemy, Location = CPos.New(6, 16) })
    target.Stance = "HoldFire"
    -- Giant: armour 5, so a snap does 75.
    FirstHit("a Trap snaps at an enemy beside it", target, 75, 300)
end

Unfinished = function()
    Check(not PwTest.Available(me, "primitive-trap"), "the Trap needs a LandIncreaser")
    local builder = Actor.Create("primitive-buildman", true, { Owner = me, Location = CPos.New(20, 24) })
    -- Available here only once the PowerHouse is finished: built by a worker, it starts unfinished.
    Actor.Create("primitive-powerhouse", true, { Owner = me, Location = CPos.New(24, 2) })
    Later(2, function() PwTest.Construct(builder, "primitive-stonetower", CPos.New(20, 20)) end)
    local builder2 = Actor.Create("primitive-buildman", true, { Owner = me, Location = CPos.New(14, 24) })
    Later(2, function() PwTest.Construct(builder2, "primitive-landincreaser", CPos.New(10, 22)) end)
    Later(10, function()
        Check(#me.GetActorsByType("primitive-landincreaser") == 1, "a Buildman starts a LandIncreaser")
        Check(not PwTest.Available(me, "primitive-trap"), "a LandIncreaser under construction doesn't unlock the Trap")
    end)
    local passer = Actor.Create("primitive-buildman", true, { Owner = enemy, Location = CPos.New(24, 26) })
    passer.Stance = "HoldFire"
    Later(10, function()
        local tower = me.GetActorsByType("primitive-stonetower")
        local site
        for _, t in ipairs(tower) do if t.Location.X == 20 then site = t end end
        Check(site ~= nil and not PwTest.Finished(site), "a Buildman starts a StoneTower")
        if site == nil then return end
        Later(100, function()
            Check(PwTest.Finished(site) or passer.Health == passer.MaxHealth, "an unfinished StoneTower doesn't shoot")
        end)
    end)
end
