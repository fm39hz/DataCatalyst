// Polyfills for language features not present in netstandard2.0:
//   - IsExternalInit enables { get; init; } property accessors.
//   - ModuleInitializerAttribute enables [ModuleInitializer]-tagged static methods (used by DataCatalyst plugin
//     self-registration). Both types are recognised by the C# compiler purely by metadata name; the BCL
//     does not need to ship them.

namespace System.Runtime.CompilerServices;

[ComponentModel.EditorBrowsable(ComponentModel.EditorBrowsableState.Never)]
internal static class IsExternalInit { }

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
[ComponentModel.EditorBrowsable(ComponentModel.EditorBrowsableState.Never)]
internal sealed class ModuleInitializerAttribute : Attribute { }
