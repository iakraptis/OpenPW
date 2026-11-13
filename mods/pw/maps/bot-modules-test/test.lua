-- Test: Bot modules. Merged from bot-ability-test, bot-defence-test, bot-hunting-test, bot-transport-test (research/tools/merge_tests.py), plus bot-merge-test and bot-spell-test: each is a section of
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
    -- The AI's abilities (PwAbilityBotModule). Raider, an Easy AI, has a BotAbilityChief (Fall out still locked) and berries; a
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
    -- The AI defends against wild creatures (PwDefenseBotModule, AutoTargetPriority@PREDATORS). Raider, an Easy AI,
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
    -- The AI hunts (PwHuntingBotModule). Raider, an Easy AI, has two idle Knights near its Sanctuary; a Galimimus (a
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
    -- The AI's air transport (PwTransportBotModule). Raider, an Easy AI, has a BotTransportRevolver and six idle Powermen at home
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

-- bot-merge-test
do
    local function At(x, y) return CPos.New(x + 0, y + 108) end
    local function Check(ok, message) return BaseCheck(ok, "BotMerge: " .. message) end
    local function WaitFor(message, timeout, cond, andThen) return BaseWaitFor("BotMerge: " .. message, timeout, cond, andThen) end
    local function FinishWhenDone(_, deadline) Deadline = math.max(Deadline, deadline) end
    local WorldLoaded, raider
    -- The AI merges Tyrano units (PwMergeBotModule). Raider, an Easy Tyrano AI, has a TyranoNest, a LandNest, an
    -- UpgradeNest and a SkyNisdos, with two Velocis and a Dilophos, a Pteras and a Trices waiting by them. Its
    -- population is over the cap, so it trains nothing; it has berries for research.
    -- 1. The bot merges the two Velocis into a TwinVelocis (the LandNest allows it).
    -- 2. It researches Mutant Morphosis, which the Mutant needs.
    -- 3. It merges the Dilophos, the Pteras and the Trices into a Mutant.
    WorldLoaded = function()
        raider = Player.GetPlayer("BotMergeRaider")
        raider.Cash = 5000

        WaitFor("the bot merges its two Velocis into a TwinVelocis", 3000, function()
            return #raider.GetActorsByType("tyrano-twinvelocis") == 1 and #raider.GetActorsByType("tyrano-velocis") == 0
        end)
        WaitFor("the bot researches Mutant Morphosis", 10000, function()
            return PwTest.Researched(raider, "tyrano-upgrade-mutant-morphosis-1")
        end)
        WaitFor("the bot merges the Dilophos, Pteras and Trices into a Mutant", 12000, function()
            return #raider.GetActorsByType("tyrano-mutant") == 1 and #raider.GetActorsByType("tyrano-pteras") == 0
        end)
        FinishWhenDone("BOT MERGE", 12500)
    end
    Sections[#Sections + 1] = WorldLoaded
end

-- bot-spell-test
do
    local function At(x, y) return CPos.New(x + 54, y + 108) end
    local function Check(ok, message) return BaseCheck(ok, "BotSpell: " .. message) end
    local function WaitFor(message, timeout, cond, andThen) return BaseWaitFor("BotSpell: " .. message, timeout, cond, andThen) end
    local function FinishWhenDone(_, deadline) Deadline = math.max(Deadline, deadline) end
    local WorldLoaded, blue, enemies, raider, red, unicorn, white
    -- The AI casts the Elves' spells (PwSpellBotModule). Raider, an Easy Elf AI, has a Heaven, a RedElf and a
    -- BlueElf (both raised to level 1, which their spells need) and, far from the fight, a Unicorn beside a hurt
    -- WhiteElf. Four enemy Powermen stand together 7 cells from the elves, holding fire.
    -- 1. The RedElf casts Thunder bolt on a Powerman (its mana goes down).
    -- 2. The BlueElf casts Drop stone on the group of Powermen.
    -- 3. The Unicorn casts Cure on the hurt WhiteElf, which heals.
    WorldLoaded = function()
        raider = Player.GetPlayer("BotSpellRaider")
        raider.Cash = 0
        red = Map.NamedActor("BotSpellRedElf")
        blue = Map.NamedActor("BotSpellBlueElf")
        unicorn = Map.NamedActor("BotSpellUnicorn")
        white = Map.NamedActor("BotSpellWhiteElf")
        enemies = { }
        for i = 0, 3 do enemies[#enemies + 1] = Map.NamedActor("BotSpellEnemy" .. i) end
        PwTest.GiveExperience(red, 100)
        PwTest.GiveExperience(blue, 100)
        white.Health = white.MaxHealth / 4
        local hurt = white.Health
        Later(1, function()
            for _, e in ipairs(enemies) do e.Stance = "HoldFire" end
            white.Stance = "HoldFire"
        end)

        WaitFor("the RedElf casts Thunder bolt", 1500, function() return PwTest.Mana(red) < PwTest.MaxMana(red) end)
        WaitFor("the BlueElf casts Drop stone on the Powermen", 1500, function() return PwTest.Mana(blue) < PwTest.MaxMana(blue) end)
        WaitFor("the Unicorn casts Cure on the hurt WhiteElf", 1500, function()
            return PwTest.Mana(unicorn) < PwTest.MaxMana(unicorn) and white.Health > hurt
        end)
        FinishWhenDone("BOT SPELL", 2000)
    end
    Sections[#Sections + 1] = WorldLoaded
end

WorldLoaded = function()
    for _, start in ipairs(Sections) do start() end
    BaseFinish("BOT MODULES", Deadline)
end
