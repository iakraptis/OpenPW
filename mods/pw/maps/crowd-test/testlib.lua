-- Shared by the test maps. Each check logs "ok - ..." or "check failed - ..."; only the final line says
-- "<NAME> TEST PASS" or "<NAME> TEST FAIL", so one run reports every failed check.
-- Times are original ticks (tickrate-plan.md): Later, WaitFor and FinishWhenDone turn them into world ticks
-- (PwTime.Ticks); Now is the original ticks since the start.
failures = { }
checks = 0
waiters = { }
scheduled = 0
finished = false

Check = function(ok, message)
    checks = checks + 1
    if ok then
        print("ok - " .. message)
    else
        failures[#failures + 1] = message
        print("check failed - " .. message)
    end
end

Now = function() return PwTime.Now end

-- Run fn after delay original ticks.
Later = function(delay, fn)
    scheduled = scheduled + 1
    Trigger.AfterDelay(math.max(1, PwTime.Ticks(delay)), function()
        scheduled = scheduled - 1
        fn()
    end)
end

-- Wait until cond() is true (a passed check) or timeout original ticks pass (a failed check), then run andThen(ok).
WaitFor = function(message, timeout, cond, andThen)
    waiters[#waiters + 1] = { message = message, deadline = DateTime.GameTime + PwTime.Ticks(timeout), cond = cond, andThen = andThen }
end

-- Track a callback the engine will call later (production done, ...): the test doesn't finish while it's
-- pending, and reports it as a failed check if it never comes.
pending = { }
Expect = function(message, fn)
    pending[message] = true
    scheduled = scheduled + 1
    return function(...)
        if pending[message] then
            pending[message] = nil
            scheduled = scheduled - 1
        end
        if fn then fn(...) end
    end
end

-- Finish once no waits or scheduled steps are left (or at the deadline, an original tick).
FinishWhenDone = function(name, deadline)
    testName = name
    testDeadline = PwTime.Ticks(deadline)
end

Contains = function(list, value)
    for _, v in ipairs(list) do if v == value then return true end end
    return false
end

Split = function(text, sep)
    local parts = { }
    for part in string.gmatch(text, "([^" .. sep .. "]+)") do parts[#parts + 1] = part end
    return parts
end

Finish = function()
    if finished then return end
    finished = true
    -- Anything still waiting is evaluated one last time, so an early finish can't hide a check.
    for _, w in ipairs(waiters) do Check(w.cond(), w.message) end
    waiters = { }
    for message in pairs(pending) do Check(false, "never happened: " .. message) end
    pending = { }
    if #failures == 0 then
        print(testName .. " TEST PASS (" .. checks .. " checks)")
        if Test then Test.Pass() end
    else
        print(testName .. " TEST FAIL: " .. #failures .. " of " .. checks .. " checks failed: " .. table.concat(failures, "; "))
        if Test then Test.Fail() end
    end
end

Tick = function()
    if finished then return end
    for i = #waiters, 1, -1 do
        local w = waiters[i]
        local ok = w.cond()
        if ok or DateTime.GameTime >= w.deadline then
            table.remove(waiters, i)
            Check(ok, w.message)
            if w.andThen then w.andThen(ok) end
        end
    end
    if testName and ((#waiters == 0 and scheduled == 0) or DateTime.GameTime >= testDeadline) then
        Finish()
    end
end
