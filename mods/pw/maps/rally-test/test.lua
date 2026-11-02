-- Rally points (RallyPoint on every production building, after OpenSYW).
-- 1. A Powerman trained at a PowerHouse with a rally point walks to it.
-- 2. A Buildman trained at a Sanctuary whose rally point is on a berry bush starts gathering there
--    (GatherAtRallyPoint), not at the nearer bush.
WorldLoaded = function()
    me = Player.GetPlayer("Multi0")
    me.Cash = 5000
    sanctuary = Actor.Create("primitive-sanctuary", true, { Owner = me, Location = CPos.New(10, 10) })
    powerhouse = Actor.Create("primitive-powerhouse", true, { Owner = me, Location = CPos.New(18, 2) })
    bushFar = Map.NamedActor("BushFar")

    Later(2, function()
        local spot = CPos.New(4, 28)
        powerhouse.RallyPoint = spot
        powerhouse.Build({ "primitive-powerman" }, Expect("the PowerHouse trains a Powerman", function(units)
            local man = units[1]
            WaitFor("the Powerman walks to the rally point", 1500, function()
                return man.IsIdle and (man.Location - spot).Length <= 2
            end)
        end))

        sanctuary.RallyPoint = CPos.New(24, 9)
        sanctuary.Build({ "primitive-buildman" }, Expect("the Sanctuary trains a Buildman", function(units)
            local worker = units[1]
            local start = PwTest.Berries(bushFar)
            WaitFor("the Buildman gathers at the bush under the rally point", 1500, function()
                return PwTest.Carried(worker) > 0 and PwTest.Berries(bushFar) < start
            end)
        end))
    end)
    FinishWhenDone("RALLY", 3000)
end
