-- Test: Wildlife. Merged from wild-test, hunting-test, meat-test, share-meat-test (research/tools/merge_tests.py): each is a section of
-- this map, 54 cells from the next, with its own players; At(x, y) is a cell of the section. A
-- section's checks carry its name.
local BaseCheck, BaseWaitFor, BaseFinish = Check, WaitFor, FinishWhenDone
local Sections, Deadline = { }, 0

-- wild-test
do
    local function At(x, y) return CPos.New(x + 0, y + 0) end
    local function Check(ok, message) return BaseCheck(ok, "Wild: " .. message) end
    local function WaitFor(message, timeout, cond, andThen) return BaseWaitFor("Wild: " .. message, timeout, cond, andThen) end
    local function FinishWhenDone(_, deadline) Deadline = math.max(Deadline, deadline) end
    local Flee, Flyer, Grazer, Predator, WorldLoaded, creeps, me
    -- Wild creatures. The local player (Multi0) is Primitive; the creatures belong to Creeps.
    -- 1. A plant-eater wanders around where it was placed and never fights.
    -- 2. A predator attacks player units within 3 cells, leaves the others alone, and fights back when attacked; a
    --    player's Soldier 4 cells from a predator attacks it on its own.
    -- 3. A flyer wanders too, and melee can't reach it.
    -- 4. A plant-eater that is hit runs off.
    WorldLoaded = function()
        me = Player.GetPlayer("Wild")
        creeps = Player.GetPlayer("Creeps")

        Later(5, Grazer)
        Later(5, Predator)
        Later(5, Flyer)
        Later(5, Flee)
        FinishWhenDone("WILD", 3000)
    end

    Grazer = function()
        local start = At(6, 26)
        local grazer = Actor.Create("wild-galimimus", true, { Owner = creeps, Location = start })
        local worker = Actor.Create("primitive-buildman", true, { Owner = me, Location = At(7, 26) })
        WaitFor("a plant-eater wanders", 1500, function() return grazer.Location ~= start end, function()
            Check((grazer.Location - start).Length <= 6, "a plant-eater stays near where it was placed")
        end)
        Later(600, function() Check(worker.Health == worker.MaxHealth, "a plant-eater doesn't attack") end)
    end

    Predator = function()
        local raptor = Actor.Create("wild-velociraptor", true, { Owner = creeps, Location = At(16, 16) })
        local near = Actor.Create("primitive-buildman", true, { Owner = me, Location = At(18, 16) })
        local far = Actor.Create("primitive-buildman", true, { Owner = me, Location = At(16, 28) })
        WaitFor("a predator attacks a unit that comes close", 300, function() return near.IsDead or near.Health < near.MaxHealth end)
        Later(300, function() Check(not far.IsDead and far.Health == far.MaxHealth, "a predator leaves units further away alone") end)

        local tough = Actor.Create("wild-triceratops", true, { Owner = creeps, Location = At(27, 20) })
        Later(5, function()
            local fighter = Actor.Create("primitive-soldier", true, { Owner = me, Location = At(27, 27) })
            fighter.Attack(tough)
            WaitFor("a predator fights back when attacked from afar", 900, function()
                return fighter.IsDead or fighter.Health < fighter.MaxHealth
            end)
        end)

        local prey = Actor.Create("wild-velociraptor", true, { Owner = creeps, Location = At(44, 44) })
        local guard = Actor.Create("primitive-soldier", true, { Owner = me, Location = At(44, 48) })
        WaitFor("a player's unit attacks a nearby predator on its own", 300, function()
            return prey.IsDead or prey.Health < prey.MaxHealth
        end)
    end

    Flyer = function()
        local start = At(26, 6)
        local flyer = Actor.Create("wild-pteranodon", true, { Owner = creeps, Location = start })
        WaitFor("a flyer wanders", 1500, function() return flyer.Location ~= start end, function()
            local fighter = Actor.Create("primitive-powerman", true, { Owner = me, Location = flyer.Location })
            fighter.Attack(flyer)
            Later(300, function() Check(flyer.Health == flyer.MaxHealth, "melee can't reach a flyer") end)
        end)
    end

    Flee = function()
        local start = At(10, 6)
        local grazer = Actor.Create("wild-protoceratops", true, { Owner = creeps, Location = start })
        local hunter = Actor.Create("primitive-soldier", true, { Owner = me, Location = At(10, 12) })
        Later(2, function() hunter.Attack(grazer) end)
        WaitFor("a hit plant-eater runs off", 400, function()
            return grazer.IsDead or (grazer.Health < grazer.MaxHealth and grazer.Location ~= start)
        end)
    end
    Sections[#Sections + 1] = WorldLoaded
end

-- hunting-test
do
    local function At(x, y) return CPos.New(x + 54, y + 0) end
    local function Check(ok, message) return BaseCheck(ok, "Hunting: " .. message) end
    local function WaitFor(message, timeout, cond, andThen) return BaseWaitFor("Hunting: " .. message, timeout, cond, andThen) end
    local function FinishWhenDone(_, deadline) Deadline = math.max(Deadline, deadline) end
    local Together, WorldLoaded, brawler, brawlerStart, deaths, farGrazer, flyer, grazer, hunter, me, raptor, spareGrazer
    -- Hunting (PwHunter, the Hunt toggle). A new design, not the original's.
    -- 1. A hunting Knight goes for the wild creatures around it, the aggressive one first: the Velociraptor (6 cells away)
    --    dies before the Galimimus (2 cells away).
    -- 2. It picks up their meat.
    -- 3. A Galimimus outside its radius is left alone, and so is one beside a Knight whose hunting is off.
    -- 4. A unit only hunts what it can strike: a hunting Powerman (melee) leaves a flying Pteranodon alone.
    -- 5. Two hunters can hunt the same creature together: both strike a Stegosaurus beside them.
    -- 6. A player's attack order turns hunting off: one of those hunters, ordered onto a creature, stops hunting.
    WorldLoaded = function()
        me = Player.GetPlayer("Hunting")
        hunter = Map.NamedActor("HuntingHunter")
        grazer = Map.NamedActor("HuntingGrazer")
        raptor = Map.NamedActor("HuntingRaptor")
        farGrazer = Map.NamedActor("HuntingFarGrazer")
        spareGrazer = Map.NamedActor("HuntingSpareGrazer")
        brawler = Map.NamedActor("HuntingBrawler")
        flyer = Map.NamedActor("HuntingFlyer")
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
            -- Checked now: the Galimimus comes back 1290 ticks after it dies (PwRespawns), and the Powerman may hunt it.
            Check(flyer.Health == flyer.MaxHealth, "the hunting Powerman leaves the flying Pteranodon alone")
            Check((brawler.Location - brawlerStart).Length <= 3, "the Powerman doesn't chase what it can't strike")
            WaitFor("the hunter picks up their meat", 1500, function() return PwTest.CarriedMeat(hunter) > 0 end, function()
                Check(not farGrazer.IsDead, "a Galimimus outside its radius is left alone")
                Check(not spareGrazer.IsDead and spareGrazer.Health == spareGrazer.MaxHealth, "a Knight that isn't hunting leaves the Galimimus beside it alone")
            end)
        end)
        Later(2, Together)
        FinishWhenDone("HUNTING", 5000)
    end

    Together = function()
        -- A slow Stegosaurus (it doesn't run far and lead them to the others' creatures) beside two hunting Powermen,
        -- far below the rest: a fight moves it around, and higher up it could come within the hunting Powerman's
        -- radius (step 4).
        local prey = Actor.Create("wild-stegosaurus", true, { Owner = Player.GetPlayer("Creeps"), Location = At(26, 38) })
        local a = Actor.Create("primitive-powerman", true, { Owner = me, Location = At(24, 38) })
        local b = Actor.Create("primitive-powerman", true, { Owner = me, Location = At(28, 38) })
        local struck = { }
        Trigger.OnDamaged(prey, function(_, attacker)
            if attacker == a then struck.a = true elseif attacker == b then struck.b = true end
        end)
        Later(2, function()
            PwTest.Hunt(a, true)
            PwTest.Hunt(b, true)
        end)
        WaitFor("two hunters kill the Stegosaurus", 2500, function() return prey.IsDead end, function(ok)
            if not ok then return end
            Check(struck.a and struck.b, "both of them struck it")
            local other = Actor.Create("wild-stegosaurus", true, { Owner = Player.GetPlayer("Creeps"), Location = At(26, 43) })
            Later(2, function()
                Check(PwTest.Hunting(a), "the hunter still hunts before it is given an order")
                PwTest.OrderOn(a, "Attack", other)
                Check(not PwTest.Hunting(a), "an attack order turns hunting off")
                Check(PwTest.Hunting(b), "the other hunter keeps hunting")
            end)
        end)
    end
    Sections[#Sections + 1] = WorldLoaded
end

-- meat-test
do
    local function At(x, y) return CPos.New(x + 0, y + 54) end
    local function Check(ok, message) return BaseCheck(ok, "Meat: " .. message) end
    local function WaitFor(message, timeout, cond, andThen) return BaseWaitFor("Meat: " .. message, timeout, cond, andThen) end
    local function FinishWhenDone(_, deadline) Deadline = math.max(Deadline, deadline) end
    local Hunt, Regenerate, Rot, WorldLoaded, creeps, me
    -- Meat (docs/game/rules.md). A killed wild creature leaves meat worth about its cost (Galimimus 40: 30-40). Living
    -- units pick it up, carry it and eat it while hurt (1 meat, 1 HP per tick); machines don't. Meat on the ground rots.
    -- Wild creatures heal on their own and come back a while after they die (here after 100 ticks).
    WorldLoaded = function()
        me = Player.GetPlayer("Meat")
        creeps = Player.GetPlayer("Creeps")

        Later(5, Hunt)
        Later(5, Rot)
        Later(5, Regenerate)
        FinishWhenDone("MEAT", 2500)
    end

    Hunt = function()
        local prey = Actor.Create("wild-galimimus-quick", true, { Owner = creeps, Location = At(10, 10) })
        local hunter = Actor.Create("primitive-powerman", true, { Owner = me, Location = At(10, 13) })
        local machine = Actor.Create("primitive-bowmachine", true, { Owner = me, Location = At(20, 6) })
        Later(1, function() hunter.Attack(prey) end)
        WaitFor("a hunted Galimimus dies", 600, function() return prey.IsDead end, function(ok)
            if not ok then return end
            Later(2, function()
                local meat = Map.ActorsInCircle(Map.CenterOfCell(At(16, 16)), WDist.FromCells(24), function(a) return PwTest.MeatAmount(a) > 20 end)[1]
                Check(meat ~= nil, "the Galimimus leaves meat")
                if meat == nil then return end
                local amount = PwTest.MeatAmount(meat)
                -- PwDropsMeat: three quarters of its Amount plus a random quarter; it may have rotted by 1 since.
                local worth = PwTest.Rule("wild-galimimus-quick", "PwDropsMeat", "Amount")
                Check(amount >= math.floor(worth * 3 / 4) - 1 and amount <= worth,
                    "the meat is worth about its cost, " .. worth .. " (" .. amount .. ")")
                Check(not PwTest.CanOrder(machine, "PwPickUpMeat", meat), "a machine can't pick up meat")
                -- The hunter, hurt, picks it up and eats it.
                hunter.Health = hunter.MaxHealth - 10
                PwTest.OrderOn(hunter, "PwPickUpMeat", meat)
                WaitFor("a hurt Powerman picks the meat up", 300, function() return meat.IsDead or not meat.IsInWorld end, function()
                    WaitFor("eating the meat heals the Powerman to full", 300, function() return hunter.Health == hunter.MaxHealth end, function()
                        Check(PwTest.CarriedMeat(hunter) > 0, "he keeps the meat he didn't need (" .. PwTest.CarriedMeat(hunter) .. ")")
                    end)
                end)
                WaitFor("the Galimimus comes back", 400, function() return #creeps.GetActorsByType("wild-galimimus-quick") >= 1 end)
            end)
        end)
    end

    Rot = function()
        -- Meat made without an amount is worth 1: it rots away within 32 ticks.
        local meat = Actor.Create("meat-small", true, { Owner = creeps, Location = At(25, 25) })
        WaitFor("meat on the ground rots away", 100, function() return meat.IsDead or not meat.IsInWorld end)
    end

    Regenerate = function()
        local beast = Actor.Create("wild-stegosaurus", true, { Owner = creeps, Location = At(26, 4) })
        Later(2, function()
            beast.Health = beast.MaxHealth - 10
            local hurt = beast.Health
            Later(100, function() Check(beast.Health > hurt, "a wild creature heals on its own") end)
        end)
    end
    Sections[#Sections + 1] = WorldLoaded
end

-- share-meat-test
do
    local function At(x, y) return CPos.New(x + 54, y + 54) end
    local function Check(ok, message) return BaseCheck(ok, "ShareMeat: " .. message) end
    local function WaitFor(message, timeout, cond, andThen) return BaseWaitFor("ShareMeat: " .. message, timeout, cond, andThen) end
    local function FinishWhenDone(_, deadline) Deadline = math.max(Deadline, deadline) end
    local WorldLoaded, carrier, leaver, machine, machineStart, second, third, total
    -- Share meat (the command panel's button, order PwShareMeat). A Powerman carrying 100 meat, two units without any, a
    -- Bowmachine (a machine: it eats no meat) and one more Powerman are told to share.
    -- 1. The meat eaters meet: they end up next to each other.
    -- 2. The meat is split evenly, nothing lost: 100 among 3 is 34, 33, 33.
    -- 3. The Bowmachine isn't part of it, and a Powerman given another order on the way leaves the share.
    WorldLoaded = function()
        carrier = Map.NamedActor("ShareMeatCarrier")
        second = Map.NamedActor("ShareMeatSecond")
        third = Map.NamedActor("ShareMeatThird")
        machine = Map.NamedActor("ShareMeatMachine")
        leaver = Map.NamedActor("ShareMeatLeaver")
        machineStart = machine.Location

        Later(2, function()
            PwTest.SetMeat(carrier, 100)
            PwTest.ShareMeat({ carrier, second, third, machine, leaver })
            -- Another order on the way (a player's new order replaces the current one): it leaves the share.
            Later(10, function()
                leaver.Stop()
                leaver.Move(At(2, 30))
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
    Sections[#Sections + 1] = WorldLoaded
end

WorldLoaded = function()
    for _, start in ipairs(Sections) do start() end
    BaseFinish("WILDLIFE", Deadline)
end
