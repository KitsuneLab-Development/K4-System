namespace K4System
{
	using Microsoft.Extensions.Logging;

	using CounterStrikeSharp.API.Core.Plugin;

	public partial class ModuleUtils : IModuleUtils
	{
		public readonly PluginContext PluginContext;
		public readonly ILogger<ModuleUtils> Logger;

		public required PluginConfig Config { get; set; }
	}
}