-- Test: Campaign AI (PwCampaignBotModule, rules/campaign-ai.yaml). Raider, a campaign AI, wants 4 Skeletons and has a
-- Demon Den, two Insects and one Skeleton placed in a camp away from its base; Passive's script is UNUSE_SCRIPT.
-- 1. Raider builds a Death Den, the building that trains Skeletons, which it has none of.
-- 2. It trains 4 Skeletons (the placed one doesn't count).
-- 3. With 3 at home (ATTACK_COUNTER 3) they attack the Target's Sanctuary, gathering half-way first (ATTACK_MOVE 50).
-- 4. The placed Skeleton stays where it was.
-- 5. Passive builds and trains nothing.

local function Count(player, type)
    return #player.GetActorsByType(type)
end

WorldLoaded = function()
    local raider = Player.GetPlayer("Raider")
    local passive = Player.GetPlayer("Passive")
    local sanctuary = Map.NamedActor("TargetSanctuary")
    local kept = Map.NamedActor("KeptSkeleton")
    local keptAt = kept.Location
    raider.Cash = 5000
    passive.Cash = 5000

    local function NearSanctuary()
        for _, skeleton in ipairs(raider.GetActorsByType("demon-skeleton")) do
            if skeleton ~= kept and (skeleton.Location - sanctuary.Location).Length <= 8 then
                return true
            end
        end

        return false
    end

    WaitFor("Raider builds a Death Den for the Skeletons it wants", 3000, function()
        for _, den in ipairs(raider.GetActorsByType("demon-deathden")) do
            if not den.IsDead and PwTest.Finished(den) then return true end
        end

        return false
    end, function()
        WaitFor("Raider trains 4 Skeletons", 2500, function() return Count(raider, "demon-skeleton") >= 5 end, function()
            WaitFor("the 3 new Skeletons attack the Sanctuary", 2500, NearSanctuary, function()
                Check((kept.Location - keptAt).Length <= 2, "the placed Skeleton stays home")
                Check(Count(passive, "demon-deathden") == 0 and Count(passive, "demon-skeleton") == 0,
                    "Passive (UNUSE_SCRIPT) builds and trains nothing")
            end)
        end)
    end)

    FinishWhenDone("CAMPAIGN AI", 9000)
end
