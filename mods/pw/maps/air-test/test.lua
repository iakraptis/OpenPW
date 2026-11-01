-- Air units (docs/game/primitive.md): Squadron and Flattop from the SkyHouse, the Revolver transport from the
-- Blacksmith. They fly; in the air only weapons that reach it (arrows, stones) hit them.
-- 1. The SkyHouse trains a Squadron, which flies.
-- 2. A Squadron shoots a ground unit for its attack (20).
-- 3. A Soldier shoots at a Squadron on its own; a Powerman can't reach it.
-- 4. The Flattop's fire splashes: two units side by side are both hurt.
-- 5. The Revolver carries 10: Buildmen (1 each) board it, the Chief (12) doesn't fit; it flies them and unloads.
--    With Improve Revolver Transportation (+8) the Chief fits.
WorldLoaded = function()
    me = Player.GetPlayer("Multi0")
    enemy = Player.GetPlayer("Creeps")
    me.Cash = 20000

    Later(5, Train)
    Later(5, Strafe)
    Later(5, Shot)
    Later(5, Splash)
    Later(5, Transport)
    FinishWhenDone("AIR", 2500)
end

Train = function()
    Actor.Create("primitive-blacksmith", true, { Owner = me, Location = CPos.New(2, 2) })
    -- Room for the Squadron, beside the Chief and the other test units.
    for x = 14, 28, 2 do Actor.Create("primitive-house", true, { Owner = me, Location = CPos.New(x, 0) }) end
    local skyhouse = Actor.Create("primitive-skyhouse", true, { Owner = me, Location = CPos.New(8, 2) })
    Later(2, function()
        skyhouse.Build({ "primitive-squadron" }, Expect("the SkyHouse trains a Squadron", function(units)
            Later(20, function() Check(units[1].CenterPosition.Z > 0, "a trained Squadron flies") end)
        end))
    end)
end

Hurt = function(label, target, damage, timeout)
    local full = target.MaxHealth
    WaitFor(label, timeout, function() return target.IsDead or target.Health < full end, function(ok)
        if ok and damage and not target.IsDead then
            Check(full - target.Health == damage, label .. " for " .. damage .. " (did " .. (full - target.Health) .. ")")
        end
    end)
end

Strafe = function()
    local squadron = Actor.Create("primitive-squadron", true, { Owner = me, Location = CPos.New(20, 4) })
    local target = Actor.Create("primitive-buildman", true, { Owner = enemy, Location = CPos.New(24, 4) })
    Later(1, function()
        target.Stance = "HoldFire"
        squadron.Attack(target)
    end)
    Hurt("a Squadron shoots a ground unit", target, 20, 400)
end

Shot = function()
    local squadron = Actor.Create("primitive-squadron", true, { Owner = enemy, Location = CPos.New(4, 16) })
    local powerman = Actor.Create("primitive-powerman", true, { Owner = me, Location = CPos.New(4, 17) })
    Later(1, function() squadron.Stance = "HoldFire" end)
    Later(150, function()
        Check(squadron.Health == squadron.MaxHealth, "a Powerman can't reach a Squadron")
        Actor.Create("primitive-soldier", true, { Owner = me, Location = CPos.New(4, 20) })
        Hurt("a Soldier shoots at a Squadron on its own", squadron, nil, 400)
    end)
end

Splash = function()
    local flattop = Actor.Create("primitive-flattop", true, { Owner = me, Location = CPos.New(14, 12) })
    local a = Actor.Create("primitive-buildman", true, { Owner = enemy, Location = CPos.New(19, 12) })
    local b = Actor.Create("primitive-buildman", true, { Owner = enemy, Location = CPos.New(19, 13) })
    Later(1, function()
        a.Stance = "HoldFire"
        b.Stance = "HoldFire"
        flattop.Attack(a)
    end)
    Hurt("the Flattop hits its target", a, nil, 400)
    Hurt("the Flattop's fire splashes onto the unit beside it", b, nil, 400)
end

Transport = function()
    local revolver = Actor.Create("primitive-revolver", true, { Owner = me, Location = CPos.New(24, 24) })
    local men = { }
    for i = 1, 3 do men[i] = Actor.Create("primitive-buildman", true, { Owner = me, Location = CPos.New(20 + i, 27) }) end
    local chief = Actor.Create("primitive-chief", true, { Owner = me, Location = CPos.New(27, 27) })
    Later(5, function()
        Check(PwTest.CanOrder(men[1], "EnterTransport", revolver), "a right-click on the Revolver loads a Buildman")
        for _, m in ipairs(men) do m.EnterTransport(revolver) end
        chief.EnterTransport(revolver)
    end)
    WaitFor("three Buildmen board the Revolver", 600, function() return revolver.PassengerCount >= 3 end, function(ok)
        if not ok then return end
        Later(100, function()
            Check(revolver.PassengerCount == 3 and chief.IsInWorld, "the Chief (12) doesn't fit in the Revolver (10)")
            revolver.Move(CPos.New(10, 26))
            revolver.UnloadPassengers()
            WaitFor("the Revolver flies them over and unloads", 900, function()
                return not revolver.HasPassengers and men[1].IsInWorld and men[1].Location.X < 16
            end, function(ok)
                if not ok then return end
                -- Improve Revolver Transportation: room for 18, so the Chief now fits.
                PwTest.CompleteUpgrade(me, "primitive-upgrade-revolover-transportation-1")
                Later(5, function() chief.EnterTransport(revolver) end)
                WaitFor("with the transport upgrade the Chief fits in the Revolver", 900, function()
                    return revolver.PassengerCount == 1 and not chief.IsInWorld
                end)
            end)
        end)
    end)
end
