using System;
using System.Collections.Generic;
using System.IO;
using DataCatalyst.Loader;

namespace DataCatalyst.V2;

internal static class LoaderRegistry
{
    private static readonly Dictionary<string, IDataLoader> _loaders = new(StringComparer.OrdinalIgnoreCase)
    {
        { ".json", new DataCatalyst.Loaders.JsonDataLoader() }
    };

    public static bool TryGetLoader(string extension, out IDataLoader loader)
    {
        return _loaders.TryGetValue(extension, out loader!);
    }
}
