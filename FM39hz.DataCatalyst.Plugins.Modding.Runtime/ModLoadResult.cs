namespace FM39hz.DataCatalyst.Plugins.Modding.Runtime;

using System.Collections.Generic;
using FM39hz.DataCatalyst.Abstractions;

public sealed class ModLoadResult {
    public string ModId { get; }
    public bool Success { get; }
    public ScriptError? Error { get; }
    public IReadOnlyList<ScriptError> Warnings { get; }
    public ModManifest? Manifest { get; }

    public ModLoadResult(
        string modId,
        bool success,
        ScriptError? error = null,
        IReadOnlyList<ScriptError>? warnings = null,
        ModManifest? manifest = null) {

        ModId = modId;
        Success = success;
        Error = error;
        Warnings = warnings ?? System.Array.Empty<ScriptError>();
        Manifest = manifest;
    }
}
