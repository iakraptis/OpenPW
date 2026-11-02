-- The AI hunts (PwHuntingBotModule). Raider, a Normal AI, has two idle Knights near its Sanctuary; a Galimimus (a
-- plant-eater: only hunting goes after it) grazes nearby, another far out.
-- 1. The bot turns hunting on for the Knights by its base.
-- 2. They kill the Galimimus near the base and take its meat.
-- 3. The one far out is left alone.
WorldLoaded = function()
    raider = Player.GetPlayer("Raider")
    raider.Cash = 0
    knights = { Map.NamedActor("Knight0"), Map.NamedActor("Knight1") }
    grazer = Map.NamedActor("Grazer")
    farGrazer = Map.NamedActor("FarGrazer")

    WaitFor("the bot turns hunting on for its Knights", 200, function()
        return PwTest.Hunting(knights[1]) and PwTest.Hunting(knights[2])
    end)
    WaitFor("they kill the Galimimus by the base", 1500, function() return grazer.IsDead end, function(ok)
        if not ok then return end
        WaitFor("a Knight takes its meat", 800, function()
            return PwTest.CarriedMeat(knights[1]) + PwTest.CarriedMeat(knights[2]) > 0
        end, function()
            Check(not farGrazer.IsDead, "the Galimimus far from the base is left alone")
        end)
    end)
    FinishWhenDone("BOT HUNTING", 3000)
end
