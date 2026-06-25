using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using DataCatalyst.Storage;

namespace DataCatalyst.V2;

internal static class GeneratorUtils
{
    public static ImmutableArray<RawEntry> ParseEntries(string content, string path)
    {
        try
        {
            var ext = Path.GetExtension(path);
            if (LoaderRegistry.TryGetLoader(ext, out var loader))
            {
                var filename = Path.GetFileNameWithoutExtension(path);
                var result = loader.Load(content, filename);
                return result.Entries.Cast<RawEntry>().ToImmutableArray();
            }
            return ImmutableArray<RawEntry>.Empty;
        }
        catch
        {
            return ImmutableArray<RawEntry>.Empty;
        }
    }

    public static string SanitizeName(string name)
    {
        if (string.IsNullOrEmpty(name)) return "Unknown";
        var chars = name.Select(c => (char.IsLetterOrDigit(c) || c == '_') ? c : '_').ToArray();
        var result = new string(chars);
        if (result.Length == 0 || !char.IsLetter(result[0]))
            result = "_" + result;
        return result;
    }
}
