namespace K4System
{
	using Microsoft.Extensions.Logging;

	using CounterStrikeSharp.API.Core.Plugin;

	public partial class ModuleUtils : IModuleUtils
	{
		public readonly Plugin plugin;
		public readonly ILogger<ModuleUtils> Logger;

		public readonly PluginConfig Config;
	}
}