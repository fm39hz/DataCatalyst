namespace Catalyst.StateEngine;

using Catalyst;
using Catalyst.Attributes;

[GameConcept]
public readonly record struct State : IConcept;

[GameConcept]
public readonly record struct Sensor : IConcept;
