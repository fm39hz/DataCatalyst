namespace DataCatalyst.Registry;

using DataCatalyst.Storage;

public sealed class RegistrySet(
	IBeingRegistry beings,
	IRequiresRegistry requires,
	IAspectFieldRegistry aspectFields,
	IAspectTypeRegistry aspectTypes) {
	public IBeingRegistry Beings { get; } = beings;
	public IRequiresRegistry Requires { get; } = requires;
	public IAspectFieldRegistry AspectFields { get; } = aspectFields;
	public IAspectTypeRegistry AspectTypes { get; } = aspectTypes;

	public RegistrySet()
		: this(new BeingRegistry(), new RequiresRegistry(), new AspectFieldRegistry(), new AspectTypeRegistry()) { }

		public bool Frozen => Beings.Frozen && Requires.Frozen && AspectFields.Frozen && AspectTypes.Frozen;

		public void Freeze() {
			Beings.Freeze();
			Requires.Freeze();
			AspectFields.Freeze();
			AspectTypes.Freeze();
		}
}
