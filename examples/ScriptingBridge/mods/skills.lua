-- Lua mod — generated typed methods, no manual wrapping
Item_Add("FlameSword", 0, 3.5)
Buff_Add("Burn", 15)

local item = Item_Get("FlameSword")
print(item.Health)

ECS.RegisterSystem("BurnAura", {
    run = function(entityId, dt)
        -- entityId is int, zero GC per tick
    end,
})
