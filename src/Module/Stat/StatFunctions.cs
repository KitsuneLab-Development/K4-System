namespace K4System
{
	using CounterStrikeSharp.API;
	using CounterStrikeSharp.API.Core;
	using CounterStrikeSharp.API.Modules.Utils;

	public partial class ModuleStat : IModuleStat
	{
		public bool IsStatsAllowed()
		{
			int notBots = Utilities.GetPlayers().Count(player => !player.IsBot);

			Plugin plugin = (this.PluginContext.Plugin as Plugin)!;
			return plugin.GameRules != null && (!plugin.GameRules.WarmupPeriod || Config.StatisticSettings.WarmupStats) && (Config.StatisticSettings.MinPlayers <= notBots);
		}

		public void BeforeRoundEnd(int winnerTeam)
		{
			if (!IsStatsAllowed())
				return;

			if (winnerTeam > (int)CsTeam.Spectator)
			{
				List<CCSPlayerController> players = Utilities.GetPlayers();

				foreach (CCSPlayerController player in players)
				{
					if (player is null || !player.IsValid || !player.PlayerPawn.IsValid)
						continue;

					if (player.IsBot || player.IsHLTV)
						continue;

					if (player.TeamNum <= (int)CsTeam.Spectator)
						continue;

					ModifyPlayerStats(player, player.TeamNum == winnerTeam ? "round_win" : "round_lose", 1);
				}
			}
		}

		public void ModifyPlayerStats(CCSPlayerController player, string field, int amount)
		{
			if (player is null || !player.IsValid || !player.PlayerPawn.IsValid)
				return;

			if (player.IsBot || player.IsHLTV)
				return;

			if (!PlayerCache.Instance.ContainsPlayer(player))
				return;

			StatData? playerData = PlayerCache.Instance.GetPlayerData(player).statData;

			if (playerData != null)
			{
				playerData.StatFields[field] += amount;
			}
		}
	}
}
