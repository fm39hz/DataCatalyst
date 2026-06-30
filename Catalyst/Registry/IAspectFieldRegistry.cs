namespace Catalyst.Registry;

using System;
using System.Collections.Generic;

public interface IAspectFieldRegistry {
	public void Register(string aspectName, Dictionary<string, Type> fields);
	public Dictionary<string, Type>? GetFields(string aspectName);
	public bool HasFields(string aspectName);
	public bool Frozen { get; }
	public void Freeze();
}
