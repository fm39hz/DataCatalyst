using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace DataCatalyst.Storage;

public static class AspectTypeRegistry
{
    private static readonly Dictionary<string, Type> _typeByName
        = new(StringComparer.OrdinalIgnoreCase);
    private static bool _initialized;

    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        var entryAsm = Assembly.GetEntryAssembly();
        if (entryAsm != null) ScanAssembly(entryAsm);

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            if (asm != entryAsm)
                ScanAssembly(asm);
    }

    private static void ScanAssembly(Assembly asm)
    {
        try
        {
            foreach (var type in asm.GetTypes())
            {
                if (type.IsValueType && !type.IsPrimitive && !type.IsEnum
                    && type.Namespace != null && !type.Namespace.StartsWith("System"))
                {
                    _typeByName[type.Name] = type;
                }
            }
        }
        catch { }
    }

    public static bool TryGetType(string name, out Type? type)
    {
        Initialize();
        return _typeByName.TryGetValue(name, out type);
    }

    public static void Register(Type type)
    {
        _typeByName[type.Name] = type;
    }
}
