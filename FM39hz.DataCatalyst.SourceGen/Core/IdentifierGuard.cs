namespace FM39hz.DataCatalyst.Core;

/// <summary>
///     Helpers for validating C# identifiers. DataCatalyst enforces the C# identifier subset on every key it
///     emits: enum members, generated field names, nested type names. JSON files that use a key outside
///     this subset are rejected with <see cref="DcDiagnostics.InvalidIdentifier" />.
/// </summary>
internal static class IdentifierGuard {
	public static bool IsValid(string s) {
		if (string.IsNullOrEmpty(s)) {
			return false;
		}

		if (!char.IsLetter(s[0]) && s[0] != '_') {
			return false;
		}

		for (var i = 1; i < s.Length; i++) {
			if (!char.IsLetterOrDigit(s[i]) && s[i] != '_') {
				return false;
			}
		}

		return true;
	}
}
