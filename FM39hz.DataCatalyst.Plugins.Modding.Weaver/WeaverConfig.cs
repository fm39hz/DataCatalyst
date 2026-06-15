using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace FM39hz.DataCatalyst.Plugins.Modding.Weaver;

public sealed class WeaverConfig {
    public bool Enabled { get; set; }
    public string IncludePattern { get; set; } = ".*";
    public string ExcludePattern { get; set; } = "^(.*\\.(get_|set_|add_|remove_)|.*\\..*\\..*c__DisplayClass)";

    private Regex? _include;
    private Regex? _exclude;

    public bool MatchMethod(string fullName) {
        _include ??= new Regex(IncludePattern, RegexOptions.Compiled);
        _exclude ??= new Regex(ExcludePattern, RegexOptions.Compiled);
        return _include.IsMatch(fullName) && !_exclude.IsMatch(fullName);
    }
}
