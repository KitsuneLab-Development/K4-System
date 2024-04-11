namespace K4System
{
	using CounterStrikeSharp.API.Modules.Menu;
	using CounterStrikeSharp.API.Modules.Utils;
	using Microsoft.Extensions.Logging;
	using Dapper;
	using K4System.Models;
	using CounterStrikeSharp.API;

	public partial class ModuleRank : IModuleRank
	{
		ChatMenu ranksMenu = new ChatMenu("Available Rank List");

		public void Initialize_Menus()
		{
			foreach (Rank rank in rankDictionary.Values)
			{
				ranksMenu.AddMenuOption(rank.Point == -1 ? plugin.Localizer["k4.ranks.listdefault", rank.Color, rank.Name] : plugin.Localizer["k4.ranks.listitem", rank.Color, rank.Name, rank.Point],
					(player, option) =>
				{
					ulong steamID = player.SteamID;

					Task.Run(async () =>
					{
						(int playerCount, float percentage) taskValues = await FetchRanksMenuDataAsync(rank.Name);

						int playerCount = taskValues.playerCount;
						float percentage = taskValues.percentage;

						Server.NextFrame(() =>
						{
							K4Player? k4player = plugin.GetK4Player(steamID);
							if (k4player is null || !k4player.IsValid || !k4player.IsPlayer)
								return;

							RankData? playerData = k4player.rankData;

							if (playerData is null)
								return;

							int pointsDifference = Math.Abs(rank.Point - playerData.Points);

							player.PrintToChat($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.ranks.selected.title", rank.Color, rank.Name]}");
							player.PrintToChat($" {plugin.Localizer["k4.ranks.selected.line1", playerCount, percentage]}");

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
					});
				});
			}
		}

		public async Task<(int playerCount, float percentage)> FetchRanksMenuDataAsync(string rankName)
		{
			int playerCount = 0;
			float percentage = 0.0f;

			string query = $@"
                SELECT
                    COUNT(*) AS PlayerCount,
                    ROUND((COUNT(*) / TotalPlayers.Total) * 100, 2) AS Percentage
                FROM
                    `{Config.DatabaseSettings.TablePrefix}k4ranks`
                CROSS JOIN
                    (SELECT COUNT(*) AS Total FROM `{Config.DatabaseSettings.TablePrefix}k4ranks`) TotalPlayers
                WHERE
                    `rank` = @RankName
                GROUP BY
                    `rank`;";

			try
			{
				using (var connection = plugin.CreateConnection(Config))
				{
					await connection.OpenAsync();

					var result = await connection.QueryFirstOrDefaultAsync<(int, float)>(query, new { RankName = rankName });

					if (result != default)
					{
						playerCount = result.Item1;
						percentage = result.Item2;
					}
				}

				return (playerCount, percentage);
			}
			catch (Exception ex)
			{
				Logger.LogError($"A problem occurred while fetching rank menu data: {ex.Message}");
				return (0, 0);
			}
		}

	}
}