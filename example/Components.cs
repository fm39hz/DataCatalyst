using DataCatalyst.Attributes;

namespace Example;

[GameAspect]
public record struct Health { public int Current { get; set; } public int Max { get; set; } }

[GameAspect]
public record struct CombatStats { public int BaseDamage { get; set; } public int BaseDefense { get; set; } public float AttackSpeed { get; set; } }

[GameAspect]
public record struct Label { public string Name { get; set; } }

[GameAspect]
public record struct Durability { public int Points { get; set; } }

[GameAspect]
public record struct ExperienceReward { public int Amount { get; set; } }

[GameAspect]
public record struct PatrolRadius { public int Meters { get; set; } }

[GameAspect]
public record struct InitialWeapon { public string WeaponId { get; set; } }
