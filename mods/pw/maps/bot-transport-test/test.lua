-- The AI's air transport (PwTransportBotModule). Raider, a Normal AI, has a Revolver and six idle Powermen at home
-- (top left); the Target player's House stands at the bottom right. With no berries the bot has nothing else to do.
-- 1. The Revolver loads at least four of the Powermen.
-- 2. It flies them over and unloads them beside the House.
-- 3. They attack the House.
WorldLoaded = function()
    raider = Player.GetPlayer("Raider")
    raider.Cash = 0
    revolver = Map.NamedActor("Revolver")
    house = Map.NamedActor("TargetHouse")
    men = { }
    for i = 0, 5 do men[#men + 1] = Map.NamedActor("Man" .. i) end
    aboard = 0
    Watch()

    WaitFor("the bot loads 4+ Powermen into the Revolver", 2500, function() return aboard >= 4 end, function(ok)
        if not ok then return end
        WaitFor("the Revolver lands them beside the House", 2500, function()
            for _, m in ipairs(men) do
                if not m.IsDead and m.IsInWorld and m.Location.X >= 20 and m.Location.Y >= 18 then return true end
            end
            return false
        end)
    end)
    WaitFor("they attack the House", 5000, function() return house.IsDead or house.Health < house.MaxHealth end)
    FinishWhenDone("BOT TRANSPORT", 5500)
end

Watch = function()
    if not revolver.IsDead and revolver.PassengerCount > aboard then aboard = revolver.PassengerCount end
    if not finished then Trigger.AfterDelay(10, Watch) end
end
