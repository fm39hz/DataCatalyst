namespace System.Runtime.CompilerServices;

[ComponentModel.EditorBrowsable(ComponentModel.EditorBrowsableState.Never)]
internal static class IsExternalInit { }

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
[ComponentModel.EditorBrowsable(ComponentModel.EditorBrowsableState.Never)]
internal sealed class ModuleInitializerAttribute : Attribute { }
