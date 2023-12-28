namespace K4System
{
	using CounterStrikeSharp.API.Modules.Menu;
	using CounterStrikeSharp.API.Modules.Utils;
	using Microsoft.Extensions.Logging;
	using Nexd.MySQL;

	public partial class ModuleRank : IModuleRank
	{
		ChatMenu ranksMenu = new ChatMenu("Rank List");

		public void Initialize_Menus(Plugin plugin)
		{
			foreach (Rank rank in rankDictionary.Values)
			{
				ranksMenu.AddMenuOption(rank.Point == -1 ? plugin.Localizer["k4.ranks.listdefault", rank.Color, rank.Name] : plugin.Localizer["k4.ranks.listitem", rank.Color, rank.Name, rank.Point],
					(player, option) =>
				{
					Logger.LogInformation($"Player {player.PlayerName} selected rank {option.Text}.");

					MySqlQueryResult result = Database.ExecuteQuery($@"
						SELECT
							COUNT(*) AS PlayerCount,
							ROUND((COUNT(*) / TotalPlayers) * 100, 2) AS Percentage
						FROM
							`k4ranks`,
							(SELECT COUNT(*) AS TotalPlayers FROM `k4ranks`) AS Total
						WHERE
							`rank` = '{rank.Name}'
						GROUP BY
							`rank`;"
					);

					Logger.LogInformation(result.ToString());

					int playerInRank = result.Count > 0 ? result.Get<int>(0, "PlayerCount") : 0;
					float playerPercentageInRank = result.Count > 0 ? result.Get<float>(0, "Percentage") : 0.0f;

					RankData playerData = rankCache[player];

					int pointsDifference = Math.Abs(rank.Point - playerData.Points);

					player.PrintToChat($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.ranks.selected.title", rank.Color, rank.Name]}");
					player.PrintToChat($" {plugin.Localizer["k4.ranks.selected.line1", playerInRank, playerPercentageInRank]}");

					if (rank.Name == playerData.Rank.Name)
						player.PrintToChat($" {plugin.Localizer["k4.ranks.selected.line2.current", rank.Point]}");
					else
						player.PrintToChat($" {plugin.Localizer[rank.Point > playerData.Rank.Point ? "k4.ranks.selected.line2" : "k4.ranks.selected.line2.passed", rank.Point == -1 ? "None" : rank.Point, pointsDifference]}");

					if (rank.Permissions != null && rank.Permissions.Count > 0)
					{
						player.PrintToChat($" {plugin.Localizer["k4.ranks.selected.benefitline"]}");

						int permissionCount = 0;
						string permissionLine = "";

						foreach (Permission permission in rank.Permissions)
						{
							permissionLine += $"{ChatColors.Lime}{permission.DisplayName}{ChatColors.Silver}, ";
							permissionCount++;

							if (permissionCount % 3 == 0)
							{
								player.PrintToChat($" {permissionLine.TrimEnd(',', ' ')}");
								permissionLine = "";
							}
						}

						if (!string.IsNullOrEmpty(permissionLine))
						{
							player.PrintToChat($" {permissionLine.TrimEnd(',', ' ')}");
						}
					}
				});
			}
		}
	}
}