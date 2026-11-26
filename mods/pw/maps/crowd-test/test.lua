-- Test: Crowds. How groups of units move and fight together, against regressions in the melee attack (MeleeAttack)
-- and the locomotors' blocked wait (Locomotor WaitAverage, rules/world.yaml). Times are original ticks; each limit
-- has a margin over what the current rules give, and fails with the rules that made combat sluggish or traffic jam.
-- 1. Brawl: 30 Powermen ordered at one of 30 enemy Powermen (which fight back) deal at least BrawlDamage to them by
--    tick 150: they engage instead of queuing behind each other. By tick 1000 no unit stands idle near an enemy.
-- 2. Bottleneck: 12 Powermen walk through a 2-cell gap in a wall of Houses, all through by tick 250.
-- 3. Head-on jam: 40 Powermen each way through one 2-cell gap; at least JamThrough of the 80 get through by tick
--    3000 (a short blocked wait makes them re-route back and forth instead of waiting their turn).

-- The limits, from runs on 2026-11-26: the brawl dealt 958-1338 by tick 150 with the current rules, 600-816 without the
-- cap of three attackers per enemy (MeleeAttack) or with the old blocked wait of 80; the jam let 57-70 through with a
-- blocked wait of 40, 51-56 with 24 or 16, 35-38 with 8 and 22-25 with 4.
local BrawlDamage = 880
local JamThrough = 50

local me, foe

local function Brawl(y)
    local us, them = { }, { }
    for i = 0, 29 do
        us[#us + 1] = Actor.Create("primitive-powerman", true, { Owner = me, Location = CPos.New(10 + i % 3, y + math.floor(i / 3)) })
        them[#them + 1] = Actor.Create("primitive-powerman", true, { Owner = foe, Location = CPos.New(24 + i % 3, y + math.floor(i / 3)) })
    end
    local full = them[1].MaxHealth
    Later(1, function() for _, a in ipairs(us) do a.Attack(them[1]) end end)
    Later(150, function()
        local lost = 0
        for _, e in ipairs(them) do lost = lost + (e.IsDead and full or (full - e.Health)) end
        print("crowd: brawl damage by tick 150: " .. lost)
        Check(lost >= BrawlDamage, "Brawl: 30 Powermen sent at 30 deal at least " .. BrawlDamage .. " damage by tick 150 (dealt " .. lost .. ")")
    end)
    -- Every unit left near an enemy is still fighting: none stands idle within 5 cells (automatic targeting's reach) of
    -- an enemy. One left further away may stand, as any unit out of reach of the enemy does.
    Later(1000, function()
        local stood
        for _, pair in ipairs({ { us, them }, { them, us } }) do
            for _, u in ipairs(pair[1]) do
                if not u.IsDead and u.IsIdle then
                    for _, e in ipairs(pair[2]) do
                        if not e.IsDead then
                            local d = u.Location - e.Location
                            if d.X * d.X + d.Y * d.Y <= 25 then stood = stood or (u.Location.X .. "," .. u.Location.Y) end
                        end
                    end
                end
            end
        end
        Check(stood == nil, "Brawl: by tick 1000 no unit stands idle within 5 cells of an enemy" .. (stood and " (one at " .. stood .. ")" or ""))
    end)
end

-- A wall of Houses at x from y to y + 21, with a gap of two cells in the middle.
local function Wall(x, y, length)
    local gap = y + math.floor(length / 2) - 1
    for wy = y, y + length - 1 do
        if wy ~= gap and wy ~= gap + 1 then Actor.Create("primitive-house", true, { Owner = me, Location = CPos.New(x, wy) }) end
    end
end

local function Bottleneck(y)
    Wall(40, y, 22)
    local units = { }
    for i = 0, 11 do
        units[#units + 1] = Actor.Create("primitive-powerman", true, { Owner = me, Location = CPos.New(30 + i % 2, y + 5 + math.floor(i / 2)) })
    end
    Later(1, function()
        for i, u in ipairs(units) do u.Move(CPos.New(52 + (i - 1) % 2, y + 5 + math.floor((i - 1) / 2))) end
    end)
    WaitFor("Bottleneck: 12 Powermen through a 2-cell gap by tick 250", 250, function()
        for _, u in ipairs(units) do if u.Location.X < 50 then return false end end
        return true
    end)
end

local function Jam(y)
    Wall(60, y, 32)
    local east, west = { }, { }
    for i = 0, 39 do
        east[#east + 1] = Actor.Create("primitive-powerman", true, { Owner = me, Location = CPos.New(46 + i % 4, y + 6 + math.floor(i / 4)) })
        west[#west + 1] = Actor.Create("primitive-powerman", true, { Owner = me, Location = CPos.New(72 + i % 4, y + 6 + math.floor(i / 4)) })
    end
    Later(1, function()
        for i, u in ipairs(east) do u.Move(CPos.New(72 + (i - 1) % 4, y + 6 + math.floor((i - 1) / 4))) end
        for i, u in ipairs(west) do u.Move(CPos.New(46 + (i - 1) % 4, y + 6 + math.floor((i - 1) / 4))) end
    end)
    Later(3000, function()
        local through = 0
        for _, u in ipairs(east) do if u.Location.X > 61 then through = through + 1 end end
        for _, u in ipairs(west) do if u.Location.X < 60 then through = through + 1 end end
        print("crowd: jam through by tick 3000: " .. through)
        Check(through >= JamThrough, "Head-on jam: at least " .. JamThrough .. " of 80 Powermen through a 2-cell gap by tick 3000 (" .. through .. ")")
    end)
end

WorldLoaded = function()
    me = Player.GetPlayer("Crowd")
    foe = Player.GetPlayer("CrowdFoe")
    Brawl(5)
    Jam(20)
    Bottleneck(100)
    FinishWhenDone("CROWD", 3100)
end
