namespace DataCatalyst.Core;

using System;

[AttributeUsage(
    AttributeTargets.Struct | AttributeTargets.Class |
    AttributeTargets.Enum | AttributeTargets.Method,
    AllowMultiple = false, Inherited = false)]
public sealed class DataPrimitiveAttribute : Attribute { }
