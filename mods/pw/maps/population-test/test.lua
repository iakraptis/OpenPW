-- Population (docs/game/rules.md): units use it (Buildman 1, Powerman 2, ...), finished buildings provide it
-- (Sanctuary 9, House 10, production buildings 1). Training can't start past the limit.
-- 1. Units and queued units count; the limit is the finished buildings' sum.
-- 2. A Powerman that would go past the limit isn't started.
-- 3. A House under construction provides nothing; finished, it adds 10 and training can go on.
WorldLoaded = function()
    me = Player.GetPlayer("Multi0")
    me.Cash = 20000
    Actor.Create("primitive-sanctuary", true, { Owner = me, Location = CPos.New(10, 10) })
    powerhouse = Actor.Create("primitive-powerhouse", true, { Owner = me, Location = CPos.New(18, 2) })
    builder = Actor.Create("primitive-buildman", true, { Owner = me, Location = CPos.New(8, 20) })
    Actor.Create("primitive-buildman", true, { Owner = me, Location = CPos.New(9, 20) })
    Actor.Create("primitive-buildman", true, { Owner = me, Location = CPos.New(10, 20) })

    Later(5, Limit)
    FinishWhenDone("POPULATION", 3000)
end

Limit = function()
    Check(PwTest.PopulationLimit(me) == 10, "Sanctuary and PowerHouse provide 10 (" .. PwTest.PopulationLimit(me) .. ")")
    Check(PwTest.PopulationUsed(me) == 3, "three Buildmen use 3 (" .. PwTest.PopulationUsed(me) .. ")")

    -- 3 + 3 Powermen x 2 = 9; a fourth (11) is over the limit.
    for i = 1, 4 do PwTest.Train(powerhouse, "primitive-powerman") end
    Check(PwTest.PopulationUsed(me) == 9, "only three of four Powermen start (" .. PwTest.PopulationUsed(me) .. " used)")

    PwTest.Construct(builder, "primitive-house", CPos.New(4, 24))
    Later(5, function()
        local house = me.GetActorsByType("primitive-house")[1]
        Check(house ~= nil, "a Buildman starts a House")
        if house == nil then return end
        Check(PwTest.PopulationLimit(me) == 10, "a House under construction provides nothing")
        WaitFor("the Buildman builds from the cell below the House", 300, function()
            return builder.Location.Y == 25 and builder.Location.X >= 4 and builder.Location.X <= 5
        end)
        WaitFor("the House is finished", 2000, function() return PwTest.Finished(house) end, function(ok)
            if not ok then return end
            Check(PwTest.PopulationLimit(me) == 20, "a finished House adds 10 (" .. PwTest.PopulationLimit(me) .. ")")
            PwTest.Train(powerhouse, "primitive-powerman")
            Check(PwTest.PopulationUsed(me) >= 11, "with the House, the fourth Powerman starts")
        end)
    end)
end
