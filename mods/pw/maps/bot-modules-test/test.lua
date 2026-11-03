-- Test: Bot modules. Merged from bot-ability-test, bot-defence-test, bot-hunting-test, bot-transport-test (research/tools/merge_tests.py): each is a section of
-- this map, 54 cells from the next, with its own players; At(x, y) is a cell of the section. A
-- section's checks carry its name.
local BaseCheck, BaseWaitFor, BaseFinish = Check, WaitFor, FinishWhenDone
local Sections, Deadline = { }, 0

-- bot-ability-test
do
    local function At(x, y) return CPos.New(x + 0, y + 0) end
    local function Check(ok, message) return BaseCheck(ok, "BotAbility: " .. message) end
    local function WaitFor(message, timeout, cond, andThen) return BaseWaitFor("BotAbility: " .. message, timeout, cond, andThen) end
    local function FinishWhenDone(_, deadline) Deadline = math.max(Deadline, deadline) end
    local WorldLoaded, chief, enemy, raider
    -- The AI's abilities (PwAbilityBotModule). Raider, a Normal AI, has a BotAbilityChief (Fall out still locked) and berries; a
    -- Target Powerman stands 6 cells from him. Both hold fire, so only the ability can hurt it.
    -- 1. With a BotAbilityChief, the bot researches Improve Fall out for BotAbilityChief (the Sanctuary).
    -- 2. The BotAbilityChief casts Fall out on the Powerman: its life goes down to 1.
    WorldLoaded = function()
        raider = Player.GetPlayer("BotAbilityRaider")
        raider.Cash = 2000
        chief = Map.NamedActor("BotAbilityChief")
        enemy = Map.NamedActor("BotAbilityEnemy")
        Later(1, function()
            chief.Stance = "HoldFire"
            enemy.Stance = "HoldFire"
        end)

        WaitFor("the bot researches Improve Fall out for BotAbilityChief", 4000, function()
            return PwTest.Researched(raider, "primitive-upgrade-fall-out-for-chief-1")
        end)
        WaitFor("the BotAbilityChief casts Fall out on the Powerman (life down to 1)", 6000, function()
            return not enemy.IsDead and enemy.Health == 1
        end)
        FinishWhenDone("BOT ABILITY", 6500)
    end
    Sections[#Sections + 1] = WorldLoaded
end

-- bot-defence-test
do
    local function At(x, y) return CPos.New(x + 54, y + 0) end
    local function Check(ok, message) return BaseCheck(ok, "BotDefence: " .. message) end
    local function WaitFor(message, timeout, cond, andThen) return BaseWaitFor("BotDefence: " .. message, timeout, cond, andThen) end
    local function FinishWhenDone(_, deadline) Deadline = math.max(Deadline, deadline) end
    local WorldLoaded, killedBy, raider, raptor, sanctuary
    -- The AI defends against wild creatures (PwDefenseBotModule, AutoTargetPriority@PREDATORS). Raider, a Normal AI,
    -- has a Sanctuary and four idle Powermen a few cells away; a Velociraptor stands right by the Sanctuary.
    -- 1. The bot's Powermen go for the Velociraptor and kill it.
    -- 2. The Sanctuary still stands.
    WorldLoaded = function()
        raider = Player.GetPlayer("BotDefenceRaider")
        raider.Cash = 0
        raptor = Map.NamedActor("BotDefenceRaptor")
        sanctuary = Map.NamedActor("BotDefenceRaiderSanctuary")
        killedBy = nil
        Trigger.OnKilled(raptor, function(self, killer) killedBy = killer and killer.Owner end)

        WaitFor("the bot's units kill the Velociraptor by its base", 2000, function() return raptor.IsDead end, function(ok)
            if not ok then return end
            Check(killedBy == raider, "the bot killed it")
            Check(not sanctuary.IsDead, "the Sanctuary still stands")
        end)
        FinishWhenDone("BOT DEFENCE", 2500)
    end
    Sections[#Sections + 1] = WorldLoaded
end

-- bot-hunting-test
do
    local function At(x, y) return CPos.New(x + 0, y + 54) end
    local function Check(ok, message) return BaseCheck(ok, "BotHunting: " .. message) end
    local function WaitFor(message, timeout, cond, andThen) return BaseWaitFor("BotHunting: " .. message, timeout, cond, andThen) end
    local function FinishWhenDone(_, deadline) Deadline = math.max(Deadline, deadline) end
    local WorldLoaded, farGrazer, grazer, knights, raider
    -- The AI hunts (PwHuntingBotModule). Raider, a Normal AI, has two idle Knights near its Sanctuary; a Galimimus (a
    -- plant-eater: only hunting goes after it) grazes nearby, another far out.
    -- 1. The bot turns hunting on for the Knights by its base.
    -- 2. They kill the Galimimus near the base and take its meat.
    -- 3. The one far out is left alone.
    WorldLoaded = function()
        raider = Player.GetPlayer("BotHuntingRaider")
        raider.Cash = 0
        knights = { Map.NamedActor("BotHuntingKnight0"), Map.NamedActor("BotHuntingKnight1") }
        grazer = Map.NamedActor("BotHuntingGrazer")
        farGrazer = Map.NamedActor("BotHuntingFarGrazer")

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
    Sections[#Sections + 1] = WorldLoaded
end

-- bot-transport-test
do
    local function At(x, y) return CPos.New(x + 54, y + 54) end
    local function Check(ok, message) return BaseCheck(ok, "BotTransport: " .. message) end
    local function WaitFor(message, timeout, cond, andThen) return BaseWaitFor("BotTransport: " .. message, timeout, cond, andThen) end
    local function FinishWhenDone(_, deadline) Deadline = math.max(Deadline, deadline) end
    local Watch, WorldLoaded, aboard, house, men, raider, revolver
    -- The AI's air transport (PwTransportBotModule). Raider, a Normal AI, has a BotTransportRevolver and six idle Powermen at home
    -- (top left); the Target player's House stands at the bottom right. With no berries the bot has nothing else to do.
    -- 1. The BotTransportRevolver loads at least four of the Powermen.
    -- 2. It flies them over and unloads them beside the House.
    -- 3. They attack the House.
    WorldLoaded = function()
        raider = Player.GetPlayer("BotTransportRaider")
        raider.Cash = 0
        revolver = Map.NamedActor("BotTransportRevolver")
        house = Map.NamedActor("BotTransportTargetHouse")
        men = { }
        for i = 0, 5 do men[#men + 1] = Map.NamedActor("BotTransportMan" .. i) end
        aboard = 0
        Watch()

        WaitFor("the bot loads 4+ Powermen into the BotTransportRevolver", 2500, function() return aboard >= 4 end, function(ok)
            if not ok then return end
            WaitFor("the BotTransportRevolver lands them beside the House", 2500, function()
                for _, m in ipairs(men) do
                    if not m.IsDead and m.IsInWorld and m.Location.X >= At(20, 18).X and m.Location.Y >= At(20, 18).Y then return true end
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
    Sections[#Sections + 1] = WorldLoaded
end

WorldLoaded = function()
    for _, start in ipairs(Sections) do start() end
    BaseFinish("BOT MODULES", Deadline)
end
