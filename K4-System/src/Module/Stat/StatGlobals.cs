namespace K4System
{
	using CounterStrikeSharp.API.Core.Plugin;

	using Microsoft.Extensions.Logging;

	public partial class ModuleStat : IModuleStat
	{
		public class StatData
		{
			public Dictionary<string, int> StatFields { get; set; } = new Dictionary<string, int>();
		}

		public required Plugin plugin;
		public readonly ILogger<ModuleStat> Logger;

		public required PluginConfig Config;
		public readonly IPluginContext pluginContext;

		public bool FirstBlood = false;
	}
}