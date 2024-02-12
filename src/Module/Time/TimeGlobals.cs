namespace K4System
{
	using Microsoft.Extensions.Logging;

	using CounterStrikeSharp.API.Core.Plugin;

	public partial class ModuleTime : IModuleTime
	{
		public class TimeData
		{
			public Dictionary<string, DateTime> Times { get; set; } = new Dictionary<string, DateTime>();
			public Dictionary<string, int> TimeFields { get; set; } = new Dictionary<string, int>();
		}

		public readonly PluginContext PluginContext;
		public readonly ILogger<ModuleTime> Logger;

		public required PluginConfig Config { get; set; }
	}
}