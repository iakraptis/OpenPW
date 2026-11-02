-- Share meat (the command panel's button, order PwShareMeat). A Powerman carrying 100 meat, two units without any, a
-- Bowmachine (a machine: it eats no meat) and one more Powerman are told to share.
-- 1. The meat eaters meet: they end up next to each other.
-- 2. The meat is split evenly, nothing lost: 100 among 3 is 34, 33, 33.
-- 3. The Bowmachine isn't part of it, and a Powerman given another order on the way leaves the share.
WorldLoaded = function()
    carrier = Map.NamedActor("Carrier")
    second = Map.NamedActor("Second")
    third = Map.NamedActor("Third")
    machine = Map.NamedActor("Machine")
    leaver = Map.NamedActor("Leaver")
    machineStart = machine.Location

    Later(2, function()
        PwTest.SetMeat(carrier, 100)
        PwTest.ShareMeat({ carrier, second, third, machine, leaver })
        -- Another order on the way (a player's new order replaces the current one): it leaves the share.
        Later(10, function()
            leaver.Stop()
            leaver.Move(CPos.New(2, 30))
        end)
    end)

    local sharers = { carrier, second, third }
    WaitFor("the meat is shared", 600, function()
        for _, u in ipairs(sharers) do if PwTest.CarriedMeat(u) == 0 then return false end end
        return true
    end, function(ok)
        if not ok then return end
        local meat = { }
        local total = 0
        for _, u in ipairs(sharers) do
            meat[#meat + 1] = PwTest.CarriedMeat(u)
            total = total + PwTest.CarriedMeat(u)
        end
        table.sort(meat)
        Check(total == 100 and meat[1] == 33 and meat[2] == 33 and meat[3] == 34,
            "split evenly: 33, 33, 34 (" .. table.concat(meat, ", ") .. ")")
        local together = true
        for _, u in ipairs(sharers) do
            for _, v in ipairs(sharers) do
                if (u.Location - v.Location).Length > 6 then together = false end
            end
        end
        Check(together, "the meat eaters met before sharing")
        Check(PwTest.CarriedMeat(leaver) == 0, "the Powerman ordered away left the share")
        Check(machine.Location == machineStart, "the Bowmachine (no meat eater) stayed out of it")
    end)
    FinishWhenDone("SHARE MEAT", 1000)
end
