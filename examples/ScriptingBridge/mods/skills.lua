-- Lua mod - no string catalog names, no Table alloc per entity
-- Game calls typed C# methods; ECS uses entity ID, not objects

Data_AddItem("FlameSword", 0, 3.5)
Data_AddBuff("Burn", 15)

local swordId = ECS.CreateEntity()
ECS.AddItemEntity("FlameSword", swordId)

ECS.RegisterSystem("BurnAura", {
    run = function(entityId, dt)
        -- entityId is an int; script reads/writes via C# when needed
        -- For read: Data_GetItem("FlameSword") returns struct
        -- For ECS component access, game adds more typed helpers
    end,
})
