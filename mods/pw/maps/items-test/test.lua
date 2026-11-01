-- Items (Item.itm, rules/primitive/items.yaml). The Blacksmith makes an item, which appears on the ground beside it,
-- even when every cell there is taken. A unit of the type it equips walks over, picks it up and becomes the equipped
-- type for good, keeping its level, experience and health.
Shield = "primitive-item-shield-of-peace"

WorldLoaded = function()
    me = Player.GetPlayer("Multi0")
    enemy = Player.GetPlayer("Creeps")
    me.Cash = 20000
    -- Blacksmith 5x3 at (10,10): its front row is y = 13, x = 9..15. Fill it with units.
    blacksmith = Actor.Create("primitive-blacksmith", true, { Owner = me, Location = CPos.New(10, 10) })
    for x = 9, 15 do Actor.Create("primitive-buildman", true, { Owner = me, Location = CPos.New(x, 13) }) end

    Later(5, Make)
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
    local soldier = Actor.Create("primitive-soldier", true, { Owner = me, Location = CPos.New(20, 20) })
    local stranger = Actor.Create("primitive-powerman", true, { Owner = enemy, Location = CPos.New(21, 20) })
    local powerman = Actor.Create("primitive-powerman", true, { Owner = me, Location = CPos.New(22, 22) })
    Later(1, function()
        stranger.Stance = "HoldFire"
        PwTest.PickUp(soldier, item)
        PwTest.PickUp(stranger, item)
        Check(soldier.IsIdle and stranger.IsIdle, "only the owner's units of the item's type can pick it up")
        PwTest.GiveExperience(powerman, 14 * 3 + 5)
        powerman.Health = 80
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
        -- Half health (80 of 160) stays half (75 of 150).
        Check(equipped.Health == 75, "the equipped Powerman keeps its share of health (" .. equipped.Health .. ")")
    end)
end
