using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Text;

namespace FM39hz.DataCatalyst.Plugins.Modding;

[Generator]
public sealed class ModdingGenerator : IIncrementalGenerator {
    public void Initialize(IncrementalGeneratorInitializationContext context) {
        context.RegisterPostInitializationOutput(static ctx => {
            ctx.AddSource("ModHookAttribute.g.cs", SourceText.From("""
                using System;

                [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
                public sealed class ModHookAttribute : Attribute {
                    public string? Id { get; }
                    public ModHook(string? id = null) => Id = id;
                }
                """, Encoding.UTF8));
        });
    }
}
