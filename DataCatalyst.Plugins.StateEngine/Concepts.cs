namespace DataCatalyst.StateEngine;

using DataCatalyst;
using DataCatalyst.Attributes;

[GameConcept]
public readonly record struct State : IConcept;

[GameConcept]
public readonly record struct Sensor : IConcept;
