-- Test: Medium AI (ai.yaml @medium). Two sections, each with its own players; the map's rules shorten the delays (a
-- second base after 300 ticks, attack escorts after 100).
-- Top: MedPrim, a Medium Primitive AI, has a Sanctuary with two berry bushes beside it, a PowerHouse, a BlackSmith,
-- a LandIncreaser, a SkyHouse, houses and six BuildMen, and plenty of berries; three more bushes stand ~33 cells
-- east. Its enemy's House is far to the south-east.
-- 1. It builds a Trap by the berry bushes nearest its base (BushDefenses).
-- 2. It trains Revolvers (its detector) and posts one between the Sanctuary and those bushes, one at the base's edge
--    toward the enemy.
-- 3. It builds a second Sanctuary by the far bushes (ExpansionDelay).
-- Bottom: MedElf, a Medium Elf AI, has a Heaven, the halls (a ManaHall trains Pixies, its detector), trees, 14
-- idle RedElves and two Pixies, which take the home and edge posts; its enemy's House is far to the south-east.
-- 4. The RedElves attack, and the Pixie it trains next goes with them (more than 20 cells from the Heaven, near an
--    attacking RedElf). Without the two at the start it would be the third Pixie, about 3400 ticks of training (one
--    at a time), and the attack can be back home before it is out.
local function Near(a, cell, cells)
    local dx, dy = a.Location.X - cell.X, a.Location.Y - cell.Y
    return dx * dx + dy * dy <= cells * cells
end

local function Any(list, cond)
    for _, a in ipairs(list) do
        if not a.IsDead and a.IsInWorld and cond(a) then return true end
    end
    return false
end

WorldLoaded = function()
    local prim = Player.GetPlayer("MedPrim")
    local elf = Player.GetPlayer("MedElf")
    prim.Cash = 30000
    elf.Cash = 5000

    -- The posts, as PwDetectorBotModule finds them: the Sanctuary's centre (7,6), its nearest bush (14,5); the enemy
    -- House (80,40) gives the edge 10 cells toward it.
    local home = CPos.New(10, 5)
    local edge = CPos.New(16, 10)
    local bush = CPos.New(14, 5)
    local far = CPos.New(41, 6)

    WaitFor("MedPrim builds a Trap by its berry bushes", 4000, function()
        return Any(prim.GetActorsByType("primitive-trap"), function(t) return Near(t, bush, 5) end)
    end)
    WaitFor("MedPrim posts a Revolver between the Sanctuary and the bushes, another at the base's edge", 7000, function()
        local revolvers = prim.GetActorsByType("primitive-revolver")
        return Any(revolvers, function(r) return Near(r, home, 4) end) and Any(revolvers, function(r) return Near(r, edge, 4) end)
    end)
    WaitFor("MedPrim builds a second Sanctuary by the far bushes", 7000, function()
        return Any(prim.GetActorsByType("primitive-sanctuary"), function(s) return Near(s, far, 10) end)
    end)

    local heaven = CPos.New(7, 50)
    Actor.Create("elf-pixie", true, { Owner = elf, Location = CPos.New(10, 47) })
    Actor.Create("elf-pixie", true, { Owner = elf, Location = CPos.New(12, 47) })
    WaitFor("MedElf's attack has a Pixie with it", 8000, function()
        local out = { }
        for _, r in ipairs(elf.GetActorsByType("elf-redelf")) do
            if not r.IsDead and r.IsInWorld and not Near(r, heaven, 20) then out[#out + 1] = r end
        end
        return Any(elf.GetActorsByType("elf-pixie"), function(p)
            return not Near(p, heaven, 20) and Any(out, function(r) return Near(p, r.Location, 8) end)
        end)
    end)
    FinishWhenDone("BOT MEDIUM", 8500)
end
