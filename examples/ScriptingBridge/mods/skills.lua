-- Lua mod — loads at runtime, no rebuild
-- Adds a new item + registers a passive aura system

Data.Add("Item", "FlameSword", {
    Health = 0,
    Weight = 3.5,
})

Data.Add("Buff", "Burn", {
    Power = 15,
})

ECS.RegisterSystem("BurnAura", {
    components = {"Health", "Position"},
    run = function(dt, entities)
        for _, e in ipairs(entities) do
            if e.Health and e.Health < 50 then
                e.Health = e.Health - dt * 10
            end
        end
    end,
})
