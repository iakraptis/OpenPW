-- Hunting (PwHunter, the Hunt toggle). A new design, not the original's.
-- 1. A hunting Knight goes for the wild creatures around it, the aggressive one first: the Velociraptor (6 cells away)
--    dies before the Galimimus (2 cells away).
-- 2. It picks up their meat.
-- 3. A Galimimus outside its radius is left alone, and so is one beside a Knight whose hunting is off.
-- 4. A unit only hunts what it can strike: a hunting Powerman (melee) leaves a flying Pteranodon alone.
WorldLoaded = function()
    me = Player.GetPlayer("Multi0")
    hunter = Map.NamedActor("Hunter")
    grazer = Map.NamedActor("Grazer")
    raptor = Map.NamedActor("Raptor")
    farGrazer = Map.NamedActor("FarGrazer")
    spareGrazer = Map.NamedActor("SpareGrazer")
    brawler = Map.NamedActor("Brawler")
    flyer = Map.NamedActor("Flyer")
    brawlerStart = brawler.Location

    deaths = { }
    for _, a in ipairs({ grazer, raptor }) do
        Trigger.OnKilled(a, function(self, killer)
            deaths[#deaths + 1] = self.Type
            if killer ~= hunter then print("note: " .. self.Type .. " killed by " .. (killer and killer.Type or "nothing")) end
        end)
    end

    Later(2, function()
        Check(not PwTest.Hunting(hunter), "hunting is off at first")
        PwTest.Hunt(hunter, true)
        PwTest.Hunt(brawler, true)
        WaitFor("the Hunt order turns it on", 20, function() return PwTest.Hunting(hunter) end)
    end)

    WaitFor("the hunter kills both creatures around it", 3000, function() return raptor.IsDead and grazer.IsDead end, function(ok)
        if not ok then return end
        Check(deaths[1] == "wild-velociraptor", "the aggressive Velociraptor first (" .. table.concat(deaths, ", ") .. ")")
        WaitFor("the hunter picks up their meat", 1500, function() return PwTest.CarriedMeat(hunter) > 0 end, function()
            Check(not farGrazer.IsDead, "a Galimimus outside its radius is left alone")
            Check(not spareGrazer.IsDead and spareGrazer.Health == spareGrazer.MaxHealth, "a Knight that isn't hunting leaves the Galimimus beside it alone")
            Check(flyer.Health == flyer.MaxHealth, "the hunting Powerman leaves the flying Pteranodon alone")
            Check((brawler.Location - brawlerStart).Length <= 3, "the Powerman doesn't chase what it can't strike")
        end)
    end)
    FinishWhenDone("HUNTING", 5000)
end
