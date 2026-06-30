namespace Catalyst.SourceGen.Generation;

using System;
using System.Collections.Generic;
using System.Linq;
using Catalyst.SourceGen.Models;
using Microsoft.CodeAnalysis;

internal static class TypeMapping {
    private static readonly Dictionary<string, Func<string, string, string>> CastStrategies = new(StringComparer.OrdinalIgnoreCase) {
        ["int"] =          (vn, _) => $"global::System.Convert.ToInt32({vn})",
        ["System.Int32"] = (vn, _) => $"global::System.Convert.ToInt32({vn})",
        ["long"] =         (vn, _) => $"global::System.Convert.ToInt64({vn})",
        ["System.Int64"] = (vn, _) => $"global::System.Convert.ToInt64({vn})",
        ["float"] =        (vn, _) => $"global::System.Convert.ToSingle({vn})",
        ["System.Single"] =(vn, _) => $"global::System.Convert.ToSingle({vn})",
        ["double"] =       (vn, _) => $"global::System.Convert.ToDouble({vn})",
        ["System.Double"] =(vn, _) => $"global::System.Convert.ToDouble({vn})",
        ["bool"] =         (vn, _) => $"global::System.Convert.ToBoolean({vn})",
        ["System.Boolean"]=(vn, _) => $"global::System.Convert.ToBoolean({vn})",
        ["string"] =       (vn, _) => $"global::System.Convert.ToString({vn})!",
        ["System.String"] =(vn, _) => $"global::System.Convert.ToString({vn})!",
    };

    private static readonly Dictionary<string, string> ClrTypeMap = new(StringComparer.OrdinalIgnoreCase) {
        ["int"] = "System.Int32",
        ["long"] = "System.Int64",
        ["float"] = "System.Single",
        ["double"] = "System.Double",
        ["bool"] = "System.Boolean",
        ["string"] = "System.String",
        ["object"] = "System.Object",
    };

    public static bool IsList(string type, out string innerType) {
        innerType = "";
        var idx = type.IndexOf("List<");
        if (idx >= 0 && type.EndsWith(">")) {
            var start = idx + 5;
            innerType = type.Substring(start, type.Length - start - 1).Trim();
            return true;
        }
        return false;
    }

    public static bool IsDictionary(string type, out string innerType) {
        innerType = "";
        var idx = type.IndexOf("Dictionary<");
        if (idx >= 0 && type.EndsWith(">")) {
            var comma = type.LastIndexOf(',');
            if (comma >= 0) {
                innerType = type.Substring(comma + 1, type.Length - comma - 2).Trim();
                return true;
            }
        }
        return false;
    }

    public static string MapTypeString(string type) {
        if (ClrTypeMap.TryGetValue(type, out var clrName)) {
            return $"global::{clrName}";
        }
        if (type.StartsWith("List<")) {
            var inner = type.Substring(5, type.Length - 6);
            return $"global::System.Collections.Generic.List<{MapTypeString(inner)}>";
        }
        if (type.StartsWith("Dictionary<")) {
            var comma = type.IndexOf(',', 11);
            if (comma < 0) {
                return "global::System.Object";
            }
            var k = type.Substring(11, comma - 11).Trim();
            var v = type.Substring(comma + 1, type.Length - comma - 2).Trim();
            return $"global::System.Collections.Generic.Dictionary<{MapTypeString(k)}, {MapTypeString(v)}>";
        }
        if (type.Contains('.')) {
            return $"global::{type}";
        }
        return $"global::Catalyst.Generated.{type}";
    }

    public static string Cast(string type, string varName, HashSet<string> aspectNames) {
        var cleanTypeName = type.TrimEnd('?');
        var endsInQuestion = type.EndsWith("?");
        var lookupName = cleanTypeName.StartsWith("global::") ? cleanTypeName.Substring(8) : cleanTypeName;

        if (CastStrategies.TryGetValue(lookupName, out var strategy)) {
            var result = strategy(varName, type);
            return endsInQuestion ? $"({varName} != null ? {result} : null)" : result;
        }

        if (lookupName == "System.Type" || lookupName == "Type") {
            var fallback = endsInQuestion ? "null" : "default(global::System.Type)!";
            return $"(global::System.Convert.ToString({varName}) is string __s_{varName} ? (global::System.Linq.Enumerable.FirstOrDefault(registries.Beings.All, r => r.BeingType.Name.Equals(__s_{varName}, global::System.StringComparison.OrdinalIgnoreCase)).BeingType ?? {fallback}) : {fallback})";
        }

        var cleanType = type.TrimEnd('?');
        var simpleName = cleanType.Substring(cleanType.LastIndexOf('.') + 1);

        if (IsList(cleanType, out var innerType)) {
            return type.EndsWith("?")
                ? $"({varName} is global::System.Collections.Generic.IList<object?> __en_{varName} ? global::System.Linq.Enumerable.ToList(global::System.Linq.Enumerable.Select(__en_{varName}, __x => {Cast(innerType, "__x", aspectNames)})) : null)"
                : $"global::System.Linq.Enumerable.ToList(global::System.Linq.Enumerable.Select((global::System.Collections.Generic.IList<object?>){varName}, __x => {Cast(innerType, "__x", aspectNames)}))";
        }

        if (IsDictionary(cleanType, out var dictValueType)) {
            return type.EndsWith("?")
                ? $"({varName} is global::System.Collections.Generic.IDictionary<string, object?> __dict_{varName} ? global::System.Linq.Enumerable.ToDictionary(__dict_{varName}, __de => __de.Key, __de => {Cast(dictValueType, "__de.Value", aspectNames)}, global::System.StringComparer.OrdinalIgnoreCase) : null)"
                : $"global::System.Linq.Enumerable.ToDictionary((global::System.Collections.Generic.IDictionary<string, object?>){varName}, __de => __de.Key, __de => {Cast(dictValueType, "__de.Value", aspectNames)}, global::System.StringComparer.OrdinalIgnoreCase)";
        }

        if (lookupName.Contains("Catalyst.Ref<") || lookupName.Contains("Ref<")) {
            var refCleanType = type.TrimEnd('?');
            var isNullable = type.EndsWith("?");
            var fallback = isNullable ? "null" : $"default({refCleanType})";
            return $"(global::System.Convert.ToString({varName}) is string __s_{varName} && global::System.Linq.Enumerable.FirstOrDefault(registries.Beings.All, r => r.BeingType.Name.Equals(__s_{varName}, global::System.StringComparison.OrdinalIgnoreCase)).BeingType is global::System.Type __t_{varName} ? new {refCleanType}(__t_{varName}) : {fallback})";
        }

        if (aspectNames.Contains(simpleName)) {
            return $"__Deser_{Helpers.Sanitize(simpleName)}({varName}, registries)";
        }

        return type.EndsWith("?") ? $"({type}){varName}" : $"({type}){varName}!";
    }
}
