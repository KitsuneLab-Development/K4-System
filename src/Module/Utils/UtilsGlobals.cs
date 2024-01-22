namespace K4System
{
	using CounterStrikeSharp.API.Core;
	using CounterStrikeSharp.API.Core.Plugin;

	using Microsoft.Extensions.Logging;
	using Nexd.MySQL;

	public partial class ModuleUtils : IModuleUtils
	{
		public readonly PluginContext PluginContext;
		public readonly ILogger<ModuleUtils> Logger;

		public required PluginConfig Config { get; set; }
	}
}