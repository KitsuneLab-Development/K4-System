namespace K4System
{
	using CounterStrikeSharp.API;
	using CounterStrikeSharp.API.Core;
	using CounterStrikeSharp.API.Modules.Utils;

	public partial class ModuleStat : IModuleStat
	{
		public bool IsStatsAllowed()
		{
			List<CCSPlayerController> players = Utilities.GetPlayers();
			int notBots = players.Count(player => !player.IsBot);

			return globalGameRules != null && (!globalGameRules.WarmupPeriod || Config.StatisticSettings.WarmupStats) && (Config.StatisticSettings.MinPlayers <= notBots);
		}

		public void LoadStatData(int slot, Dictionary<string, int> statData)
		{
			StatData playerData = new StatData
			{
				StatFields = statData,
			};

			statCache[slot] = playerData;
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

			if (!statCache.ContainsPlayer(player))
				return;

			StatData playerData = statCache[player];
			playerData.StatFields[field] += amount;
		}
	}
}
