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

		public readonly Plugin plugin;
		public readonly ILogger<ModuleStat> Logger;
		public readonly PluginConfig Config;

		public bool FirstBlood = false;
	}
}