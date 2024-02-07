namespace K4System
{
	using MySqlConnector;

	using CounterStrikeSharp.API.Modules.Menu;
	using CounterStrikeSharp.API.Modules.Utils;
	using Microsoft.Extensions.Logging;

	public partial class ModuleRank : IModuleRank
	{
		ChatMenu ranksMenu = new ChatMenu("Available Rank List");

		public void Initialize_Menus(Plugin plugin)
		{
			foreach (Rank rank in rankDictionary.Values)
			{
				ranksMenu.AddMenuOption(rank.Point == -1 ? plugin.Localizer["k4.ranks.listdefault", rank.Color, rank.Name] : plugin.Localizer["k4.ranks.listitem", rank.Color, rank.Name, rank.Point],
					(player, option) =>
				{
					Task<(int playerCount, float percentage)> task = Task.Run(() => FetchRanksMenuDataAsync(rank.Name));
					task.Wait();
					var result = task.Result;

					int playerCount = result.playerCount;
					float percentage = result.percentage;

					RankData playerData = rankCache[player];

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
			}
		}

		public async Task<(int playerCount, float percentage)> FetchRanksMenuDataAsync(string rankName)
		{
			int playerCount = 0;
			float percentage = 0.0f;

			string query = $@"
						SELECT
							COUNT(*) AS PlayerCount,
							ROUND((COUNT(*) / TotalPlayers) * 100, 2) AS Percentage
						FROM
							`{Config.DatabaseSettings.TablePrefix}k4ranks`,
							(SELECT COUNT(*) AS TotalPlayers FROM `{Config.DatabaseSettings.TablePrefix}k4ranks`) AS Total
						WHERE
							`rank` = '{rankName}'
						GROUP BY
							`rank`;";

			try
			{
				using (MySqlCommand command = new MySqlCommand(query))
				{
					using (MySqlDataReader? reader = await Database.Instance.ExecuteReaderAsync(command.CommandText, command.Parameters.Cast<MySqlParameter>().ToArray()))
					{
						if (reader != null && reader.HasRows)
						{
							while (await reader.ReadAsync())
							{
								playerCount = reader.GetInt32(0);
								percentage = reader.GetFloat(1);
							}
						}
					}
				}

				return (playerCount, percentage);
			}
			catch (Exception ex)
			{
				Logger.LogError($"Error fetching rank data: {ex.Message}");
				return (0, 0);
			}
		}
	}
}