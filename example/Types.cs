using System;
using DataCatalyst;
using DataCatalyst.Attributes;

namespace DataCatalyst.Generated;

[GameAspect]
public struct Health { public int Current { get; set; } public int Max { get; set; } }

[GameAspect]
public struct Mana { public int Current { get; set; } public int Max { get; set; } }

[GameAspect]
public struct CombatStats { public int BaseDamage { get; set; } public int BaseDefense { get; set; } public float AttackSpeed { get; set; } }

[GameAspect]
public struct Label { public string Name { get; set; } }

[GameAspect]
public struct InitialWeapon { public string WeaponId { get; set; } }

[GameAspect]
public struct ExperienceReward { public int Amount { get; set; } }

[GameAspect]
public struct PatrolRadius { public int Meters { get; set; } }

[GameAspect]
public struct Durability { public int Points { get; set; } }



[GameAspect]
public struct Stamina { public int Current { get; set; } public int Max { get; set; } }

[GameConcept]
public struct Creature : IConcept { }

[GameConcept]
public struct Enemy : IConcept { }

[GameConcept]
public struct Hero : IConcept { }

[GameConcept]
public struct Item : IConcept { }

[GameConcept]
public struct Weapon : IConcept { }

[GameConcept]
public struct GameState : IConcept { }

