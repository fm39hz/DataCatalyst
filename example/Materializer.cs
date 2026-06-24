using DataCatalyst;
using DataCatalyst.Attributes;
using Example;

[Materializer]
sealed partial class MockMat : IMaterializer<MockNode>
{
    public void Apply<T>(MockNode node, T c) where T : struct
    {
        if (typeof(T) == typeof(Health))
        {
            var h = (Health)(object)c;
            node.Hp = h.Current;
            node.MaxHp = h.Max;
        }
        else if (typeof(T) == typeof(CombatStats))
        {
            var s = (CombatStats)(object)c;
            node.Damage = s.BaseDamage;
            node.Defense = s.BaseDefense;
            node.AttackSpeed = s.AttackSpeed;
        }
        else if (typeof(T) == typeof(Label))
        {
            var l = (Label)(object)c;
            node.Name = l.Name;
        }
    }
}
