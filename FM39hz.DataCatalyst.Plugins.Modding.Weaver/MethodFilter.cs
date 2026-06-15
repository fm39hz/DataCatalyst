using System;
using System.Linq;
using Mono.Cecil;

namespace FM39hz.DataCatalyst.Plugins.Modding.Weaver;

public static class MethodFilter {
    public static bool ShouldInject(MethodDefinition method, WeaverConfig config) {
        if (!method.HasBody) return false;
        if (method.IsAbstract) return false;
        if (method.IsConstructor) return false;
        if (method.Body.Instructions.Count == 0) return false;

        var fullName = method.FullName;

        // Mode A: explicit [ModHook] attribute
        if (method.CustomAttributes.Any(a => a.AttributeType.Name == "ModHookAttribute")) {
            return config.MatchMethod(fullName);
        }

        // Mode B: auto-weave (Enabled = true)
        if (config.Enabled) {
            return config.MatchMethod(fullName);
        }

        return false;
    }

    public static string BuildHookId(MethodDefinition method) {
        var type = method.DeclaringType;
        var fullTypeName = type.FullName;

        var retType = method.ReturnType.FullName;

        var paramTypes = string.Join(",",
            method.Parameters.Select(p => p.ParameterType.FullName));

        return $"{fullTypeName}.{method.Name}|{retType}|({paramTypes})";
    }
}
