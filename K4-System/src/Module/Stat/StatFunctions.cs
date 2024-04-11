namespace K4System
{
	using CounterStrikeSharp.API;
	using CounterStrikeSharp.API.Core;
	using CounterStrikeSharp.API.Modules.Utils;
	using K4System.Models;

	public partial class ModuleStat : IModuleStat
	{
		public bool IsStatsAllowed()
		{
			int notBots = Utilities.GetPlayers().Count(player => !player.IsBot);
			return plugin.GameRules != null && (!plugin.GameRules.WarmupPeriod || Config.StatisticSettings.WarmupStats) && (Config.StatisticSettings.MinPlayers <= notBots);
		}

		public void BeforeRoundEnd(int winnerTeam)
		{
			if (!IsStatsAllowed())
				return;

			if (winnerTeam > (int)CsTeam.Spectator)
			{
				foreach (K4Player k4player in plugin.K4Players)
				{
					if (k4player is null || !k4player.IsValid || !k4player.IsPlayer)
						continue;

					if (k4player.Controller.Team <= CsTeam.Spectator)
						continue;

					ModifyPlayerStats(k4player, k4player.Controller.TeamNum == winnerTeam ? "round_win" : "round_lose", 1);
				}
			}
		}

		public void ModifyPlayerStats(K4Player k4player, string field, int amount)
		{
			StatData? playerData = k4player.statData;

			if (playerData != null)
			{
				playerData.StatFields[field] += amount;
			}
		}
	}
}
