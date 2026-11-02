-- The AI defends against wild creatures (PwDefenseBotModule, AutoTargetPriority@PREDATORS). Raider, a Normal AI,
-- has a Sanctuary and four idle Powermen a few cells away; a Velociraptor stands right by the Sanctuary.
-- 1. The bot's Powermen go for the Velociraptor and kill it.
-- 2. The Sanctuary still stands.
WorldLoaded = function()
    raider = Player.GetPlayer("Raider")
    raider.Cash = 0
    raptor = Map.NamedActor("Raptor")
    sanctuary = Map.NamedActor("RaiderSanctuary")
    killedBy = nil
    Trigger.OnKilled(raptor, function(self, killer) killedBy = killer and killer.Owner end)

    WaitFor("the bot's units kill the Velociraptor by its base", 2000, function() return raptor.IsDead end, function(ok)
        if not ok then return end
        Check(killedBy == raider, "the bot killed it")
        Check(not sanctuary.IsDead, "the Sanctuary still stands")
    end)
    FinishWhenDone("BOT DEFENCE", 2500)
end
