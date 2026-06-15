using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using Mono.Cecil.Rocks;

namespace FM39hz.DataCatalyst.Plugins.Modding.Weaver;

public sealed class WeaverTask : Task {
    [Required]
    public string AssemblyPath { get; set; } = "";

    [Required]
    public ITaskItem[] ReferencePath { get; set; } = Array.Empty<ITaskItem>();

    public bool EnableModHookWeaver { get; set; }

    public string ModHookIncludePattern { get; set; } = ".*";

    public string ModHookExcludePattern { get; set; } =
        "^(.*\\.(get_|set_|add_|remove_)|.*\\..*\\..*c__DisplayClass)";

    public override bool Execute() {
        if (!EnableModHookWeaver && !HasModHookAttribute(AssemblyPath)) {
            Log.LogMessage(MessageImportance.Low,
                "DataCatalyst weaver: no [ModHook] attributes found, skipping.");
            return true;
        }

        if (!File.Exists(AssemblyPath)) {
            Log.LogError($"Assembly not found: {AssemblyPath}");
            return false;
        }

        try {
            var config = new WeaverConfig {
                Enabled = EnableModHookWeaver,
                IncludePattern = ModHookIncludePattern,
                ExcludePattern = ModHookExcludePattern,
            };

            // Load Modding.Runtime to resolve HookDispatcher
            var runtimePath = FindModdingRuntime(ReferencePath);
            if (runtimePath is null) {
                Log.LogError("Cannot find FM39hz.DataCatalyst.Plugins.Modding.Runtime.dll in references");
                return false;
            }

            var resolver = new DefaultAssemblyResolver();
            foreach (var refItem in ReferencePath) {
                var dir = Path.GetDirectoryName(refItem.ItemSpec);
                if (dir is not null) resolver.AddSearchDirectory(dir);
            }
            resolver.AddSearchDirectory(Path.GetDirectoryName(runtimePath));

            var readerParams = new ReaderParameters {
                AssemblyResolver = resolver,
                ReadWrite = true,
                ReadSymbols = false,
            };

            using var targetAssembly = AssemblyDefinition.ReadAssembly(AssemblyPath, readerParams);
            using var runtimeAssembly = AssemblyDefinition.ReadAssembly(runtimePath);

            var injector = new HookInjector(config, runtimeAssembly);
            injector.Initialize();
            var count = injector.Inject(targetAssembly);

            targetAssembly.Write(AssemblyPath);

            Log.LogMessage(MessageImportance.Normal,
                $"DataCatalyst weaver: injected hooks into {count} methods in {Path.GetFileName(AssemblyPath)}");
            return true;

        } catch (Exception ex) {
            Log.LogErrorFromException(ex, showStackTrace: true);
            return false;
        }
    }

    private static bool HasModHookAttribute(string assemblyPath) {
        try {
            using var ass = AssemblyDefinition.ReadAssembly(assemblyPath);
            return ass.MainModule.GetAllTypes()
                .SelectMany(t => t.Methods)
                .Any(m => m.CustomAttributes.Any(
                    a => a.AttributeType.Name == "ModHookAttribute"));
        } catch {
            return false;
        }
    }

    private static string? FindModdingRuntime(ITaskItem[] references) {
        foreach (var r in references) {
            var name = Path.GetFileNameWithoutExtension(r.ItemSpec);
            if (name == "FM39hz.DataCatalyst.Plugins.Modding.Runtime")
                return r.ItemSpec;
        }
        return null;
    }
}
