# WeaverHook - Ring 2-3: Script modding with IL hooks

Demonstrates: `[ModHook]` attribute + `EnableModHookWeaver` + `Hook.Before`/`After` in Lua.

## Game code

```csharp
// Game.cs - game dev marks methods for mod hooking
public class Game {
    [ModHook]
    public bool AddItem(Item i) {
        if (i.Weight > 50f) return false;
        _inventory.Add(i);
        return true;
    }

    [ModHook("Player.TakeDamage")]
    public void TakeDamage(int damage) {
        _health -= damage;
    }
}
```

## Mod Lua script

```lua
-- Ring 2: hook vào method có [ModHook]
Hook.Before("Game.AddItem", function(call)
    local item = call.Args[1]
    if item.Weight > 100 then
        Log("item too heavy, blocked")
        call.ReturnValue = false
        return true  -- skip original
    end
end)

Hook.After("Player.TakeDamage", function(call)
    Log("took " .. call.Args[1] .. " damage, health now: " .. call.Instance._health)
end)
```

## Config

### Ring 2 - only `[ModHook]` methods

```xml
<PropertyGroup>
  <EnableModHookWeaver>false</EnableModHookWeaver>
</PropertyGroup>
```

### Ring 3 - auto-weave all public methods

```xml
<PropertyGroup>
  <EnableModHookWeaver>true</EnableModHookWeaver>
  <ModHookIncludePattern>^(Namespace\..*\.(Update|Add|Remove|Create|Set))</ModHookIncludePattern>
  <ModHookExcludePattern>^(.*\.(get_|set_|add_|remove_)|.*c__DisplayClass)</ModHookExcludePattern>
</PropertyGroup>
```

## IL what happens

```il
;; Before weave
.method public bool AddItem(Item i) { ldarg.1; call Inventory.Add; ret }

;; After weave - dispatch injected
.method public bool AddItem(Item i) {
    ldstr "Game.AddItem"
    ldarg.0                          ;; instance
    ldc.i4 1; newarr object; dup
    ldc.i4 0; ldarg.1; stelem.ref   ;; args[0] = Item
    ldloca retLocal
    call HookDispatcher.Before       ;; → bool skip?
    brfalse.s CONTINUE
    ldloc retLocal; unbox.any bool; ret  ;; skip → return mod value

    CONTINUE:
    ldarg.1; call Inventory.Add; stloc result
    ldstr "Game.AddItem"
    ldarg.0
    ldloc result; box
    call HookDispatcher.After
    ldloc result; ret
}
```
