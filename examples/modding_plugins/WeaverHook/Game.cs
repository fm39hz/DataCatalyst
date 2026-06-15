[assembly: ModPlugin("WeaverHookGame")]

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

    private List<Item> _inventory = new();
    private int _health = 100;
}
