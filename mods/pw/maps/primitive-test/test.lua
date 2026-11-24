-- Test: Primitive. Merged from items-test, upgrades-test, chief-test, air-test (research/tools/merge_tests.py): each is a section of
-- this map, 54 cells from the next, with its own players; At(x, y) is a cell of the section. A
-- section's checks carry its name.
local BaseCheck, BaseWaitFor, BaseFinish = Check, WaitFor, FinishWhenDone
local Sections, Deadline = { }, 0

-- items-test
do
    local function At(x, y) return CPos.New(x + 0, y + 0) end
    local function Check(ok, message) return BaseCheck(ok, "Items: " .. message) end
    local function WaitFor(message, timeout, cond, andThen) return BaseWaitFor("Items: " .. message, timeout, cond, andThen) end
    local function FinishWhenDone(_, deadline) Deadline = math.max(Deadline, deadline) end
    local Hunters, Make, PickUp, Shield, WorldLoaded, blacksmith, enemy, me, percent
    -- Items (Item.itm, rules/primitive/items.yaml). The Blacksmith makes an item, which appears on the ground beside it,
    -- even when every cell there is taken. A unit of the type it equips walks over, picks it up and becomes the equipped
    -- type for good, keeping its level, experience and health.
    Shield = "primitive-item-shield-of-peace"

    WorldLoaded = function()
        me = Player.GetPlayer("Items")
        enemy = Player.GetPlayer("Creeps")
        me.Cash = 20000
        -- Blacksmith 5x3 at (10,10): its front row is y = 13, x = 9..15. Fill it with units.
        blacksmith = Actor.Create("primitive-blacksmith", true, { Owner = me, Location = At(10, 10) })
        for x = 9, 15 do Actor.Create("primitive-buildman", true, { Owner = me, Location = At(x, 13) }) end

        Later(5, Make)
        Later(5, Hunters)
        FinishWhenDone("ITEMS", 2500)
    end

    Make = function()
        PwTest.Train(blacksmith, Shield)
        WaitFor("the Blacksmith makes a Shield of Peace, even with the cells beside it full", 600, function()
            return #me.GetActorsByType(Shield) == 1
        end, function(ok)
            if not ok then return end
            local item = me.GetActorsByType(Shield)[1]
            local l = item.Location
            Check(l.X >= 9 and l.X <= 15 and l.Y >= 9 and l.Y <= 13 and not (l.Y <= 12 and l.X >= 10 and l.X <= 14),
                "the item lies beside the Blacksmith (" .. l.X .. "," .. l.Y .. ")")
            PickUp(item)
        end)
    end

    PickUp = function(item)
        local soldier = Actor.Create("primitive-soldier", true, { Owner = me, Location = At(20, 20) })
        local stranger = Actor.Create("primitive-powerman", true, { Owner = enemy, Location = At(21, 20) })
        local powerman = Actor.Create("primitive-powerman", true, { Owner = me, Location = At(22, 22) })
        Later(1, function()
            stranger.Stance = "HoldFire"
            PwTest.PickUp(soldier, item)
            PwTest.PickUp(stranger, item)
            Check(soldier.IsIdle and stranger.IsIdle, "only the owner's units of the item's type can pick it up")
            PwTest.GiveLevels(powerman, 3)
            PwTest.GiveExperience(powerman, 5)
            powerman.Health = math.floor(powerman.MaxHealth / 2)
            percent = math.floor(powerman.Health * 100 / powerman.MaxHealth)
            PwTest.PickUp(powerman, item)
        end)
        WaitFor("a Powerman picks up the Shield of Peace", 600, function()
            return #me.GetActorsByType("primitive-powerman-shield-of-peace") == 1
        end, function(ok)
            if not ok then return end
            local equipped = me.GetActorsByType("primitive-powerman-shield-of-peace")[1]
            Check(item.IsDead or not item.IsInWorld, "the item is gone")
            Check(powerman.IsDead or not powerman.IsInWorld, "the plain Powerman is replaced")
            Check(PwTest.Level(equipped) == 3 and PwTest.Experience(equipped) == 5,
                "the equipped Powerman keeps level 3 and 5 experience (" .. PwTest.Level(equipped) .. ", " .. PwTest.Experience(equipped) .. ")")
            -- Half health stays half: the same percentage (rounded down) of the equipped Powerman's health.
            local expected = math.floor(equipped.MaxHealth * percent / 100)
            Check(equipped.Health == expected, "the equipped Powerman keeps its share of health (" .. equipped.Health .. ", expected " .. expected .. ")")
        end)
    end

    -- Hunting units fetch an item they can use on their own, like meat: only one of the type it equips (two hunting
    -- Soldiers for a Fire Arrow: one goes, the other stays), not a hunting Powerman beside it or a Soldier not hunting.
    -- The equipped Soldier is still hunting.
    Hunters = function()
        local arrow = "primitive-item-fire-arrow"
        local item = Actor.Create(arrow, true, { Owner = me, Location = At(20, 40) })
        local powerman = Actor.Create("primitive-powerman", true, { Owner = me, Location = At(16, 40) })
        local idle = Actor.Create("primitive-soldier", true, { Owner = me, Location = At(23, 40) })
        local hunters = {
            Actor.Create("primitive-soldier", true, { Owner = me, Location = At(19, 46) }),
            Actor.Create("primitive-soldier", true, { Owner = me, Location = At(21, 46) }),
        }
        Later(1, function()
            PwTest.Hunt(powerman, true)
            for _, h in ipairs(hunters) do PwTest.Hunt(h, true) end
        end)
        WaitFor("a hunting Soldier fetches the Fire Arrow on its own", 600, function()
            return #me.GetActorsByType("primitive-soldier-fire-arrow") == 1
        end, function(ok)
            if not ok then return end
            local equipped = me.GetActorsByType("primitive-soldier-fire-arrow")[1]
            Check(item.IsDead or not item.IsInWorld, "the Fire Arrow is gone")
            Check(PwTest.Hunting(equipped), "the equipped Soldier is still hunting")
            Later(60, function()
                local plain = 0
                for _, h in ipairs(hunters) do if not h.IsDead and h.IsInWorld then plain = plain + 1 end end
                Check(plain == 1 and #me.GetActorsByType("primitive-soldier-fire-arrow") == 1,
                    "only one of the two hunting Soldiers took it (" .. plain .. " plain left)")
                Check(not powerman.IsDead and powerman.IsInWorld and powerman.Type == "primitive-powerman",
                    "the hunting Powerman leaves a Soldier's item alone")
                Check(not idle.IsDead and idle.IsInWorld and idle.Type == "primitive-soldier",
                    "a Soldier that isn't hunting doesn't fetch it")
            end)
        end)
    end
    Sections[#Sections + 1] = WorldLoaded
end

-- upgrades-test
do
    local function At(x, y) return CPos.New(x + 54, y + 0) end
    local function Check(ok, message) return BaseCheck(ok, "Upgrades: " .. message) end
    local function WaitFor(message, timeout, cond, andThen) return BaseWaitFor("Upgrades: " .. message, timeout, cond, andThen) end
    local function FinishWhenDone(_, deadline) Deadline = math.max(Deadline, deadline) end
    local Gathering, GatheringAndLevels, LevelExp, Weapon, Weapon1, Weapon2, WorldLoaded, enemy, landincreaser, me, sanctuary
    -- Upgrades (Upgrade.upi, rules/primitive/upgrades.yaml). Buildings research them like units, one level at a time.
    -- 1. Level 2 is offered only after level 1, and a researched level isn't offered again.
    -- 2. The same level can't be queued twice.
    -- 3. Improve Ground Primitive Weapon 1: more attack (a Powerman hits a Buildman for its damage plus the upgrade's).
    -- 4. Improve Gathering Amount: 16 berries per trip.
    -- 5. Reduce Level Up Exp. 1: a Buildman levels every 8 experience instead of 10.
    Weapon1 = "primitive-upgrade-ground-primitive-weapon-1"
    Weapon2 = "primitive-upgrade-ground-primitive-weapon-2"
    Gathering = "primitive-upgrade-gathering-amount-1"
    LevelExp = "primitive-upgrade-level-up-exp-1"

    WorldLoaded = function()
        me = Player.GetPlayer("Upgrades")
        enemy = Player.GetPlayer("Creeps")
        me.Cash = 20000
        sanctuary = Actor.Create("primitive-sanctuary", true, { Owner = me, Location = At(10, 10) })
        landincreaser = Actor.Create("primitive-landincreaser", true, { Owner = me, Location = At(18, 2) })

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
            local attacker = Actor.Create("primitive-powerman", true, { Owner = me, Location = At(3, 26) })
            local target = Actor.Create("primitive-buildman", true, { Owner = enemy, Location = At(4, 26) })
            Later(1, function()
                target.Stance = "HoldFire"
                attacker.Attack(target)
            end)
            WaitFor("an upgraded Powerman hits", 300, function() return target.Health < target.MaxHealth end, function(hit)
                -- SubtractArmorDamage: the weapon's damage plus the upgrade's attack, minus the target's armour.
                local hits = math.max(1, PwTest.Weapon("PowerManHit", "Damage") + PwTest.Rule(Weapon1, "PwUpgrade", "Attack")
                    - PwTest.Rule("primitive-buildman", "PwArmor", "Value"))
                if hit then Check(target.MaxHealth - target.Health == hits, "an upgraded Powerman hits for " .. hits .. " (did " .. (target.MaxHealth - target.Health) .. ")") end
            end)
        end)
    end

    GatheringAndLevels = function()
        PwTest.Train(sanctuary, Gathering)
        PwTest.Train(sanctuary, LevelExp)
        WaitFor("the Sanctuary researches the gathering upgrade", 1500, function() return PwTest.Researched(me, Gathering) end, function(ok)
            if not ok then return end
            local worker = Actor.Create("primitive-buildman", true, { Owner = me, Location = At(12, 18) })
            Later(2, function() PwTest.Gather(worker, UpgradesBushNear) end)
            WaitFor("an upgraded Buildman gathers", 600, function() return PwTest.Carried(worker) > 0 end, function(gathered)
                local carries = PwTest.Rule("primitive-buildman", "BerryGatherer", "Amount") + PwTest.Rule(Gathering, "PwUpgrade", "Gathering")
                if gathered then Check(PwTest.Carried(worker) == carries, "an upgraded Buildman carries " .. carries .. " (" .. PwTest.Carried(worker) .. ")") end
            end)
        end)
        WaitFor("the Sanctuary researches Reduce Level Up Exp.", 2500, function() return PwTest.Researched(me, LevelExp) end, function(ok)
            if not ok then return end
            local man = Actor.Create("primitive-buildman", true, { Owner = me, Location = At(28, 28) })
            local needed = PwTest.Rule("primitive-buildman", "PwLevels", "XpPerLevel") - PwTest.Rule(LevelExp, "PwUpgrade", "LevelCost")
            PwTest.GiveExperience(man, needed)
            Check(PwTest.Level(man) == 1, "with Reduce Level Up Exp. 1, " .. needed .. " experience is a level")
        end)
    end
    Sections[#Sections + 1] = WorldLoaded
end

-- chief-test
do
    local function At(x, y) return CPos.New(x + 0, y + 54) end
    local function Check(ok, message) return BaseCheck(ok, "Chief: " .. message) end
    local function WaitFor(message, timeout, cond, andThen) return BaseWaitFor("Chief: " .. message, timeout, cond, andThen) end
    local function FinishWhenDone(_, deadline) Deadline = math.max(Deadline, deadline) end
    local FallOut, FirstHit, MoraleArmour, MoraleAttack, WorldLoaded, enemy, me
    -- The Chief (docs/game/rules.md): morale and Fall out.
    -- 1. Morale: Primitive units within 5 cells of their Chief attack and defend 10% better.
    -- 2. Fall out needs its upgrade; then, for 80 of his 100 mana, it wears an enemy down to 1 HP without killing it.
    -- 3. Mana refills over time.
    WorldLoaded = function()
        me = Player.GetPlayer("Chief")
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
        Actor.Create("primitive-chief", true, { Owner = me, Location = At(4, 4) })
        local attacker = Actor.Create("primitive-powerman", true, { Owner = me, Location = At(6, 4) })
        local target = Actor.Create("primitive-buildman", true, { Owner = enemy, Location = At(7, 4) })
        -- The Powerman's attack with the morale bonus (rounded down), minus the Buildman's armour.
        local attack = math.floor(PwTest.Weapon("PowerManHit", "Damage")
            * PwTest.Rule("primitive-powerman", "FirepowerMultiplier@MORALE", "Modifier") / 100)
        FirstHit("a Powerman beside his Chief hits harder", attacker, target,
            math.max(1, attack - PwTest.Rule("primitive-buildman", "PwArmor", "Value")))
    end

    MoraleArmour = function()
        Actor.Create("primitive-chief", true, { Owner = me, Location = At(4, 16) })
        local defender = Actor.Create("primitive-powerman-shield-of-peace", true, { Owner = me, Location = At(6, 16) })
        local attacker = Actor.Create("primitive-powerman", true, { Owner = enemy, Location = At(7, 16) })
        Later(1, function() defender.Stance = "HoldFire" end)
        -- The Powerman's attack against the defender's armour with the morale bonus (rounded down).
        local armour = math.floor(PwTest.Rule("primitive-powerman-shield-of-peace", "PwArmor", "Value")
            * PwTest.Rule("primitive-powerman-shield-of-peace", "PwArmorMultiplier@MORALE", "Modifier") / 100)
        FirstHit("an enemy hits a Powerman beside his Chief less", attacker, defender,
            math.max(1, PwTest.Weapon("PowerManHit", "Damage") - armour))
    end

    FallOut = function()
        local chief = Actor.Create("primitive-chief", true, { Owner = me, Location = At(20, 20) })
        local target = Actor.Create("primitive-knight", true, { Owner = enemy, Location = At(24, 20) })
        Later(2, function()
            target.Stance = "HoldFire"
            chief.Stance = "HoldFire"
            PwTest.OrderOn(chief, "PwFallOut", target)
        end)
        Later(60, function()
            Check(target.Health == target.MaxHealth and PwTest.Mana(chief) == PwTest.MaxMana(chief), "Fall out does nothing before its upgrade")
            PwTest.CompleteUpgrade(me, "primitive-upgrade-fall-out-for-chief-1")
            PwTest.OrderOn(chief, "PwFallOut", target)
            WaitFor("Fall out wears the target down", 600, function() return target.IsDead or target.Health == 1 end, function(ok)
                if not ok then return end
                Check(not target.IsDead and target.Health == 1, "Fall out leaves the target at 1 HP")
                local mana = PwTest.Mana(chief)
                Check(mana < PwTest.MaxMana(chief), "Fall out costs mana (" .. mana .. " left)")
                Later(100, function() Check(PwTest.Mana(chief) > mana, "mana refills over time") end)
            end)
        end)
    end
    Sections[#Sections + 1] = WorldLoaded
end

-- air-test
do
    local function At(x, y) return CPos.New(x + 54, y + 54) end
    local function Check(ok, message) return BaseCheck(ok, "Air: " .. message) end
    local function WaitFor(message, timeout, cond, andThen) return BaseWaitFor("Air: " .. message, timeout, cond, andThen) end
    local function FinishWhenDone(_, deadline) Deadline = math.max(Deadline, deadline) end
    local Hurt, Shot, Splash, Strafe, Train, Transport, WorldLoaded, enemy, me
    -- Air units (docs/game/primitive.md): Squadron and Flattop from the SkyHouse, the Revolver transport from the
    -- Blacksmith. They fly; in the air only weapons that reach it (arrows, stones) hit them.
    -- 1. The SkyHouse trains a Squadron, which flies.
    -- 2. A Squadron shoots a ground unit for its attack (20).
    -- 3. A Soldier shoots at a Squadron on its own; a Powerman can't reach it.
    -- 4. The Flattop's fire splashes: two units side by side are both hurt.
    -- 5. The Revolver carries 10: Buildmen (1 each) board it, the Chief (12) doesn't fit; it flies them and unloads.
    --    With Improve Revolver Transportation (+8) the Chief fits; Unload sets him down at the place clicked.
    WorldLoaded = function()
        me = Player.GetPlayer("Air")
        enemy = Player.GetPlayer("Creeps")
        me.Cash = 20000

        Later(5, Train)
        Later(5, Strafe)
        Later(5, Shot)
        Later(5, Splash)
        Later(5, Transport)
        FinishWhenDone("AIR", 3500)
    end

    Train = function()
        Actor.Create("primitive-blacksmith", true, { Owner = me, Location = At(2, 2) })
        -- Room for the Squadron, beside the Chief and the other test units.
        for x = 14, 28, 2 do Actor.Create("primitive-house", true, { Owner = me, Location = At(x, 0) }) end
        local skyhouse = Actor.Create("primitive-skyhouse", true, { Owner = me, Location = At(8, 2) })
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
        local squadron = Actor.Create("primitive-squadron", true, { Owner = me, Location = At(20, 4) })
        local target = Actor.Create("primitive-buildman", true, { Owner = enemy, Location = At(24, 4) })
        Later(1, function()
            target.Stance = "HoldFire"
            squadron.Attack(target)
        end)
        Hurt("a Squadron shoots a ground unit", target, 20, 400)
    end

    Shot = function()
        local squadron = Actor.Create("primitive-squadron", true, { Owner = enemy, Location = At(4, 16) })
        local powerman = Actor.Create("primitive-powerman", true, { Owner = me, Location = At(4, 17) })
        Later(1, function() squadron.Stance = "HoldFire" end)
        Later(150, function()
            Check(squadron.Health == squadron.MaxHealth, "a Powerman can't reach a Squadron")
            Actor.Create("primitive-soldier", true, { Owner = me, Location = At(4, 20) })
            Hurt("a Soldier shoots at a Squadron on its own", squadron, nil, 400)
        end)
    end

    Splash = function()
        local flattop = Actor.Create("primitive-flattop", true, { Owner = me, Location = At(14, 12) })
        local a = Actor.Create("primitive-buildman", true, { Owner = enemy, Location = At(19, 12) })
        local b = Actor.Create("primitive-buildman", true, { Owner = enemy, Location = At(19, 13) })
        Later(1, function()
            a.Stance = "HoldFire"
            b.Stance = "HoldFire"
            flattop.Attack(a)
        end)
        Hurt("the Flattop hits its target", a, nil, 400)
        Hurt("the Flattop's fire splashes onto the unit beside it", b, nil, 400)
    end

    Transport = function()
        local revolver = Actor.Create("primitive-revolver", true, { Owner = me, Location = At(24, 24) })
        local men = { }
        for i = 1, 3 do men[i] = Actor.Create("primitive-buildman", true, { Owner = me, Location = At(20 + i, 27) }) end
        local chief = Actor.Create("primitive-chief", true, { Owner = me, Location = At(27, 27) })
        Later(5, function()
            Check(PwTest.CanOrder(men[1], "EnterTransport", revolver), "a right-click on the Revolver loads a Buildman")
            for _, m in ipairs(men) do m.EnterTransport(revolver) end
            chief.EnterTransport(revolver)
        end)
        WaitFor("three Buildmen board the Revolver", 600, function() return revolver.PassengerCount >= 3 end, function(ok)
            if not ok then return end
            Later(100, function()
                Check(revolver.PassengerCount == 3 and chief.IsInWorld, "the Chief (12) doesn't fit in the Revolver (10)")
                revolver.Move(At(10, 26))
                revolver.UnloadPassengers()
                WaitFor("the Revolver flies them over and unloads", 900, function()
                    return not revolver.HasPassengers and men[1].IsInWorld and men[1].Location.X < At(16, 0).X
                end, function(ok)
                    if not ok then return end
                    -- Improve Revolver Transportation: room for 18, so the Chief now fits.
                    PwTest.CompleteUpgrade(me, "primitive-upgrade-revolover-transportation-1")
                    Later(5, function() chief.EnterTransport(revolver) end)
                    WaitFor("with the transport upgrade the Chief fits in the Revolver", 900, function()
                        return revolver.PassengerCount == 1 and not chief.IsInWorld
                    end, function(boarded)
                        if not boarded then return end
                        -- The Unload button: the Revolver flies to the place clicked and lets the Chief out there.
                        local drop = At(24, 18)
                        PwTest.UnloadAt(revolver, drop)
                        WaitFor("Unload sets the Chief down at the place clicked", 900, function()
                            return not revolver.HasPassengers and chief.IsInWorld and (chief.Location - drop).Length <= 3
                        end)
                    end)
                end)
            end)
        end)
    end
    Sections[#Sections + 1] = WorldLoaded
end

WorldLoaded = function()
    for _, start in ipairs(Sections) do start() end
    BaseFinish("PRIMITIVE", Deadline)
end
