namespace K4System
{
	using CounterStrikeSharp.API;
	using CounterStrikeSharp.API.Core;
	using CounterStrikeSharp.API.Modules.Utils;

	using System.Reflection;
	using MySqlConnector;
	using Nexd.MySQL;
	using Microsoft.Extensions.Logging;
	using CounterStrikeSharp.API.Modules.Admin;

	public partial class ModuleRank : IModuleRank
	{
		public CCSGameRules GameRules()
		{
			if (globalGameRules is null)
				globalGameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").First().GameRules!;

			return globalGameRules;
		}

		public bool IsPointsAllowed()
		{
			List<CCSPlayerController> players = Utilities.GetPlayers();
			int notBots = players.Count(player => !player.IsBot);

			return (!GameRules().WarmupPeriod || Config.RankSettings.WarmupPoints) && (Config.RankSettings.MinPlayers <= notBots);
		}

		public string ApplyRankColors(string color)
		{
			foreach (FieldInfo field in typeof(ChatColors).GetFields())
			{
				if (color.Contains(field.Name, StringComparison.OrdinalIgnoreCase))
				{
					return color.Replace(field.Name, field.GetValue(null)!.ToString(), StringComparison.OrdinalIgnoreCase);
				}
			}

			Logger.LogError($"ApplyRankColors > Invalid color is given in rank configs '{color}'");

			return color;
		}

		public async Task LoadRankData(CCSPlayerController player)
		{
			if (player is null || !player.IsValid)
			{
				Logger.LogWarning("LoadRankData > Invalid player controller");
				return;
			}

			if (player.IsBot || player.IsHLTV)
			{
				Logger.LogWarning($"LoadRankData > Player controller is BOT or HLTV");
				return;
			}

			string escapedName = MySqlHelper.EscapeString(player.PlayerName);
			string steamID = player.SteamID.ToString();

			await Database.ExecuteNonQueryAsync($@"
				INSERT INTO `{Config.DatabaseSettings.TablePrefix}k4ranks` (`name`, `steam_id`, `rank`)
				VALUES (
					'{escapedName}',
					'{steamID}',
					'{noneRank.Name}'
				)
				ON DUPLICATE KEY UPDATE
					`name` = '{escapedName}';
			");

			MySqlQueryResult result = await Database.ExecuteQueryAsync($@"
				SELECT `points`
				FROM `{Config.DatabaseSettings.TablePrefix}k4ranks`
				WHERE `steam_id` = '{steamID}';
			");

			int points = result.Rows > 0 ? result.Get<int>(0, "points") : 0;

			RankData playerData = new RankData
			{
				Points = points,
				Rank = GetPlayerRank(points),
				PlayedRound = false,
				RoundPoints = 0
			};

			rankCache[player] = playerData;
		}

		public Rank GetPlayerRank(int points)
		{
			return rankDictionary.LastOrDefault(kv => points >= kv.Value.Point).Value ?? noneRank;
		}

		public void ModifyPlayerPoints(CCSPlayerController player, int amount, string reason)
		{
			if (!IsPointsAllowed())
				return;

			if (player is null || !player.IsValid || !player.PlayerPawn.IsValid)
			{
				Logger.LogWarning("ModifyPlayerPoints > Invalid player controller");
				return;
			}

			if (player.IsBot || player.IsHLTV)
			{
				Logger.LogWarning($"ModifyPlayerPoints > Player controller is BOT or HLTV");
				return;
			}

			if (!rankCache.ContainsPlayer(player))
			{
				Logger.LogWarning($"ModifyPlayerPoints > Player is not loaded to the cache ({player.PlayerName})");
				return;
			}

			RankData playerData = rankCache[player];

			if (amount == 0)
				return;

			if (amount > 0 && AdminManager.PlayerHasPermissions(player, "@k4system/vip/points-multiplier"))
			{
				amount = (int)Math.Round(amount * Config.RankSettings.VipMultiplier);
			}

			Server.NextFrame(() =>
			{
				if (!Config.RankSettings.RoundEndPoints)
				{
					if (amount > 0)
					{
						player.PrintToChat($" {Config.GeneralSettings.Prefix} {ChatColors.Silver}Points: {ChatColors.Green}{playerData.Points} [+{amount} {reason}]");
					}
					else if (amount < 0)
					{
						player.PrintToChat($" {Config.GeneralSettings.Prefix} {ChatColors.Silver}Points: {ChatColors.Red}{playerData.Points} [-{Math.Abs(amount)} {reason}]");
					}
				}

				playerData.Points += amount;

				if (playerData.Points < 0)
					playerData.Points = 0;

				if (Config.RankSettings.ScoreboardScoreSync)
					player.Score = playerData.Points;

				playerData.RoundPoints += amount;

				playerData.Rank = GetPlayerRank(playerData.Points);

				if (Config.RankSettings.ScoreboardRanks)
					player.Clan = $"{playerData.Rank.Tag ?? $"[{playerData.Rank.Name}]"}";
			});
		}

		public async Task SavePlayerRankCache(CCSPlayerController player, bool remove)
		{
			if (player is null || !player.IsValid || !player.PlayerPawn.IsValid)
			{
				Logger.LogWarning("SavePlayerRankCache > Invalid player controller");
				return;
			}

			if (player.IsBot || player.IsHLTV)
			{
				Logger.LogWarning($"SavePlayerRankCache > Player controller is BOT or HLTV");
				return;
			}

			if (!rankCache.ContainsPlayer(player))
			{
				Logger.LogWarning($"SavePlayerRankCache > Player is not loaded to the cache ({player.PlayerName})");
				return;
			}

			RankData playerData = rankCache[player];

			string escapedName = MySqlHelper.EscapeString(player.PlayerName);
			string steamID = player.SteamID.ToString();

			await Database.ExecuteNonQueryAsync($@"
				INSERT INTO `{Config.DatabaseSettings.TablePrefix}k4ranks`
				(`steam_id`, `name`, `rank`, `points`)
				VALUES
				('{steamID}', '{escapedName}', '{playerData.Rank.Name}',
				CASE
					WHEN (`points` + {playerData.RoundPoints}) < 0 THEN 0
					ELSE (`points` + {playerData.RoundPoints})
				END)
				ON DUPLICATE KEY UPDATE
				`name` = '{escapedName}',
				`rank` = '{playerData.Rank.Name}',
				`points` =
				CASE
					WHEN (`points` + {playerData.RoundPoints}) < 0 THEN 0
					ELSE (`points` + {playerData.RoundPoints})
				END;
			");

			if (!remove)
			{
				MySqlQueryResult selectResult = await Database.ExecuteQueryAsync($@"
					SELECT `points`
					FROM `{Config.DatabaseSettings.TablePrefix}k4ranks`
					WHERE `steam_id` = '{steamID}';
				");

				playerData.Points = selectResult.Rows > 0 ? selectResult.Get<int>(0, "points") : 0;
				playerData.RoundPoints = 0;
				playerData.Rank = GetPlayerRank(playerData.Points);

				if (Config.RankSettings.ScoreboardRanks)
					player.Clan = $"{playerData.Rank.Tag ?? $"[{playerData.Rank.Name}]"}";
			}
			else
			{
				rankCache.RemovePlayer(player);
			}
		}

		public async Task SaveAllPlayerCache(bool clear)
		{
			List<CCSPlayerController> players = Utilities.GetPlayers();

			var saveTasks = players
				.Where(player => player != null && player.IsValid && player.PlayerPawn.IsValid && !player.IsBot && !player.IsHLTV && rankCache.ContainsPlayer(player))
				.Select(player => SavePlayerRankCache(player, clear))
				.ToList();

			await Task.WhenAll(saveTasks);

			if (clear)
				rankCache.Clear();
		}

		public async Task<(int playerPlace, int totalPlayers)> GetPlayerPlaceAndCount(string steamID)
		{
			MySqlQueryResult result = await Database.Table($"{Config.DatabaseSettings.TablePrefix}k4ranks")
				.ExecuteQueryAsync($"SELECT (SELECT COUNT(*) FROM `{Config.DatabaseSettings.TablePrefix}k4ranks` WHERE `points` > (SELECT `points` FROM `{Config.DatabaseSettings.TablePrefix}k4ranks` WHERE `steam_id` = '{steamID}')) AS playerCount, COUNT(*) AS totalPlayers FROM `{Config.DatabaseSettings.TablePrefix}k4ranks`;")!;

			if (result.Count > 0)
			{
				int playersWithMorePoints = result.Get<int>(0, "playerCount");
				int totalPlayers = result.Get<int>(0, "totalPlayers");

				return (playersWithMorePoints + 1, totalPlayers);
			}

			return (0, 0);
		}

		public async Task PrintTopXPlayers(CCSPlayerController player, int number)
		{
			await SaveAllPlayerCache(false);

			MySqlQueryResult result = await Database.Table($"{Config.DatabaseSettings.TablePrefix}k4ranks")
			.ExecuteQueryAsync($"SELECT `points`, `name` FROM `{Config.DatabaseSettings.TablePrefix}k4ranks` ORDER BY `points` DESC LIMIT {number};");

			if (result.Count > 0)
			{
				player.PrintToChat($" {Config.GeneralSettings.Prefix} Top {number} Players:");

				for (int i = 0; i < result.Count; i++)
				{
					int points = result.Get<int>(i, "points");

					Rank rank = GetPlayerRank(points);

					player.PrintToChat($" {ChatColors.Gold}{i + 1}. {rank.Color}[{rank.Name}] {ChatColors.Gold}{result.Get<string>(i, "name")} - {ChatColors.Blue}{points} points");
				}
			}
			else
			{
				player!.PrintToChat($" {Config.GeneralSettings.Prefix} No players found in the top {number}.");
			}
		}

		public int CalculateDynamicPoints(CCSPlayerController modifyFor, CCSPlayerController modifyFrom, int amount)
		{
			if (Config.RankSettings.DynamicDeathPoints && !modifyFor.IsBot && !modifyFrom.IsBot)
			{
				double result = rankCache[modifyFrom].Points / rankCache[modifyFor].Points * amount;
				return (int)Math.Round(result);
			}

			return amount;
		}
	}
}