-- Meat (docs/game/rules.md). A killed wild creature leaves meat worth about its cost (Galimimus 40: 30-40). Living
-- units pick it up, carry it and eat it while hurt (1 meat, 1 HP per tick); machines don't. Meat on the ground rots.
-- Wild creatures heal on their own and come back a while after they die (here after 100 ticks).
WorldLoaded = function()
    me = Player.GetPlayer("Multi0")
    creeps = Player.GetPlayer("Creeps")

    Later(5, Hunt)
    Later(5, Rot)
    Later(5, Regenerate)
    FinishWhenDone("MEAT", 2500)
end

Hunt = function()
    local prey = Actor.Create("wild-galimimus", true, { Owner = creeps, Location = CPos.New(10, 10) })
    local hunter = Actor.Create("primitive-powerman", true, { Owner = me, Location = CPos.New(10, 13) })
    local machine = Actor.Create("primitive-bowmachine", true, { Owner = me, Location = CPos.New(20, 6) })
    Later(1, function() hunter.Attack(prey) end)
    WaitFor("a hunted Galimimus dies", 600, function() return prey.IsDead end, function(ok)
        if not ok then return end
        Later(2, function()
            local meat = Map.ActorsInCircle(WPos.New(16 * 1024, 16 * 1024, 0), WDist.FromCells(24), function(a) return PwTest.MeatAmount(a) > 20 end)[1]
            Check(meat ~= nil, "the Galimimus leaves meat")
            if meat == nil then return end
            local amount = PwTest.MeatAmount(meat)
            Check(amount >= 29 and amount <= 40, "the meat is worth about its cost (" .. amount .. ")")
            Check(not PwTest.CanOrder(machine, "PwPickUpMeat", meat), "a machine can't pick up meat")
            -- The hunter, hurt, picks it up and eats it.
            hunter.Health = hunter.MaxHealth - 10
            PwTest.OrderOn(hunter, "PwPickUpMeat", meat)
            WaitFor("a hurt Powerman picks the meat up", 300, function() return meat.IsDead or not meat.IsInWorld end, function()
                WaitFor("eating the meat heals the Powerman to full", 300, function() return hunter.Health == hunter.MaxHealth end, function()
                    Check(PwTest.CarriedMeat(hunter) > 0, "he keeps the meat he didn't need (" .. PwTest.CarriedMeat(hunter) .. ")")
                end)
            end)
            WaitFor("the Galimimus comes back", 400, function() return #creeps.GetActorsByType("wild-galimimus") >= 1 end)
        end)
    end)
end

Rot = function()
    -- Meat made without an amount is worth 1: it rots away within 32 ticks.
    local meat = Actor.Create("meat-small", true, { Owner = creeps, Location = CPos.New(25, 25) })
    WaitFor("meat on the ground rots away", 100, function() return meat.IsDead or not meat.IsInWorld end)
end

Regenerate = function()
    local beast = Actor.Create("wild-stegosaurus", true, { Owner = creeps, Location = CPos.New(26, 4) })
    Later(2, function()
        beast.Health = beast.MaxHealth - 10
        local hurt = beast.Health
        Later(100, function() Check(beast.Health > hurt, "a wild creature heals on its own") end)
    end)
end
