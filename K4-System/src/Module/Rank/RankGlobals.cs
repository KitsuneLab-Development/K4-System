namespace K4System
{
	using CounterStrikeSharp.API.Core.Plugin;

	using Microsoft.Extensions.Logging;

	public partial class ModuleRank : IModuleRank
	{
		public class Rank
		{
			public int Id { get; set; }
			public required string Name { get; set; }
			public string? Tag { get; set; }
			public required int Point { get; set; }
			public required string Color { get; set; }
			public List<Permission>? Permissions { get; set; }
		}

		public class Permission
		{
			public required string DisplayName { get; set; }
			public required string PermissionName { get; set; }
		}

		public class RankData
		{
			public required int Points { get; set; }
			public required Rank Rank { get; set; }
			public required bool PlayedRound { get; set; }
			public required int RoundPoints { get; set; }
			public required bool HideAdminTag { get; set; }
			public required bool MuteMessages { get; set; }
			public int TopPlacement { get; set; }
		}

		public required Plugin plugin;
		public readonly ILogger<ModuleRank> Logger;

		public required PluginConfig Config;
		public readonly IPluginContext pluginContext;
		public Dictionary<string, Rank> rankDictionary = new Dictionary<string, Rank>();
		public Rank? noneRank;
	}
}