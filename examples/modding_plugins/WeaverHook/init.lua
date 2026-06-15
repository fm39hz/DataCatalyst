-- WeaverHook example — Hook.Before/After via [ModHook] + weaver

Hook.Before("Game.AddItem", function(call)
    local item = call.Args[1]
    if item:Get("Weight") > 100 then
        Log("blocked: item too heavy")
        call.ReturnValue = false
        return true  -- skip
    end

    if item:Get("Tag") == "legendary" then
        -- legendary items: add to best slot
        call.ReturnValue = true
        return true  -- skip, game logic không chạy
    end
end)

Hook.After("Player.TakeDamage", function(call)
    Log("damage taken: " .. call.Args[1])
end)

-- Rung 1 data API (từ EntryExposer)
Item_Add("CustomSword", { Health = 0, Weight = 3.5 })

local sword = Item_Get("CustomSword")
Log("sword weight: " .. sword.Weight)
