namespace Example;

public class MockNode
{
    public string? Name { get; set; }
    public int Hp { get; set; }
    public int MaxHp { get; set; }
    public int Damage { get; set; }
    public int Defense { get; set; }
    public float AttackSpeed { get; set; }
    public override string ToString() =>
        $"Node({Name}): HP={Hp}/{MaxHp} DMG={Damage} DEF={Defense} SPD={AttackSpeed:F1}";
}
