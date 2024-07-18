namespace K4System
{
	using Microsoft.Extensions.Logging;

	using CounterStrikeSharp.API;
	using CounterStrikeSharp.API.Core.Plugin;
	using CounterStrikeSharp.API.Modules.Timers;
	using CounterStrikeSharp.API.Modules.Utils;
	using K4System.Models;
	using Dapper;

	public partial class ModuleRank : IModuleRank
	{
		public ModuleRank(ILogger<ModuleRank> logger, IPluginContext pluginContext)
		{
			this.Logger = logger;
			this.pluginContext = pluginContext;
		}

		public Timer? reservePlayTimeTimer = null;
		public Timer? reservePlacementTimer = null;

		public void Initialize(bool hotReload)
		{
			this.plugin = (pluginContext.Plugin as Plugin)!;
			this.Config = plugin.Config;

			if (Config.GeneralSettings.LoadMessages)
				this.Logger.LogInformation("Initializing '{0}'", this.GetType().Name);

			//** ? Register Module Parts */

			Initialize_Config();
			Initialize_Menus();
			Initialize_Events();
			Initialize_Commands();

			//** ? Register Timers */

			if (Config.PointSettings.PlaytimeMinutes > 0)
			{
				reservePlayTimeTimer = plugin.AddTimer(Config.PointSettings.PlaytimeMinutes * 60, () =>
				{
					foreach (K4Player k4player in plugin.K4Players)
					{
						if (!k4player.IsValid || !k4player.IsPlayer)
							continue;

						if (Config.PointSettings.PlaytimeRewardAFK || k4player.Controller.Team >= CsTeam.Terrorist)
							ModifyPlayerPoints(k4player, Config.PointSettings.PlaytimePoints, "k4.phrases.playtime");
					}
				}, TimerFlags.REPEAT);
			}

			if (Config.RankSettings.DisplayToplistPlacement)
			{
				reservePlacementTimer = plugin.AddTimer(300, () =>
				{
					string query = $@"SELECT steam_id,
                                (SELECT COUNT(*) FROM `{Config.DatabaseSettings.TablePrefix}k4ranks`
                                 WHERE `points` > (SELECT `points` FROM `{Config.DatabaseSettings.TablePrefix}k4ranks` WHERE `steam_id` = t.steam_id)) AS playerPlace
                             FROM `{Config.DatabaseSettings.TablePrefix}k4ranks` t
                             WHERE steam_id IN @SteamIds";

					var steamIds = plugin.K4Players.Where(p => p.IsValid && p.IsPlayer && p.rankData != null && p.SteamID.ToString().Length == 17)
						   .Select(p => p.SteamID)
						   .ToArray();

					if (steamIds.Length == 0)
						return;

					Task.Run(async () =>
					{
						try
						{
							using (var connection = plugin.CreateConnection(Config))
							{
								await connection.OpenAsync();
								var result = await connection.QueryAsync(query, new { SteamIds = steamIds });

								foreach (var row in result)
								{
									string steamId = row.steam_id;
									int playerPlace = (int)row.playerPlace + 1;

									K4Player? k4player = plugin.K4Players.FirstOrDefault(p => p.SteamID == ulong.Parse(steamId));
									if (k4player != null && k4player.rankData != null)
									{
										k4player.rankData.TopPlacement = playerPlace;
									}
								}
							}
						}
						catch (Exception ex)
						{
							Server.NextFrame(() => Logger.LogError($"A problem occurred while fetching player placements: {ex.Message}"));
						}
					});
				}, TimerFlags.REPEAT);
			}
		}

		public void Release(bool hotReload)
		{
			if (Config.GeneralSettings.LoadMessages)
				this.Logger.LogInformation("Releasing '{0}'", this.GetType().Name);
		}
	}
}