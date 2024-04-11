namespace K4System
{
	using Microsoft.Extensions.Logging;

	using CounterStrikeSharp.API.Core.Plugin;

	public partial class ModuleUtils : IModuleUtils
	{
		public required Plugin plugin;
		public readonly ILogger<ModuleUtils> Logger;

		public required PluginConfig Config;
		public readonly IPluginContext pluginContext;
	}
}