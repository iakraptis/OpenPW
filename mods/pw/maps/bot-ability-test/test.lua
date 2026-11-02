-- The AI's abilities (PwAbilityBotModule). Raider, a Normal AI, has a Chief (Fall out still locked) and berries; a
-- Target Powerman stands 6 cells from him. Both hold fire, so only the ability can hurt it.
-- 1. With a Chief, the bot researches Improve Fall out for Chief (the Sanctuary).
-- 2. The Chief casts Fall out on the Powerman: its life goes down to 1.
WorldLoaded = function()
    raider = Player.GetPlayer("Raider")
    raider.Cash = 2000
    chief = Map.NamedActor("Chief")
    enemy = Map.NamedActor("Enemy")
    Later(1, function()
        chief.Stance = "HoldFire"
        enemy.Stance = "HoldFire"
    end)

    WaitFor("the bot researches Improve Fall out for Chief", 4000, function()
        return PwTest.Researched(raider, "primitive-upgrade-fall-out-for-chief-1")
    end)
    WaitFor("the Chief casts Fall out on the Powerman (life down to 1)", 6000, function()
        return not enemy.IsDead and enemy.Health == 1
    end)
    FinishWhenDone("BOT ABILITY", 6500)
end
