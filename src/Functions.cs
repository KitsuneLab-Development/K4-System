using Newtonsoft.Json;
using System.Text.RegularExpressions;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using Nexd.MySQL;
using System.Reflection;
using MySqlConnector;
using System.Text;
using CounterStrikeSharp.API.Modules.Admin;

namespace K4ryuuSystem
{
	public partial class K4System
	{
		public void LoadRanksFromConfig()
		{
			string ranksFilePath = Path.Join(ModuleDirectory, "ranks.jsonc");

			string defaultRanksContent = @"{
        ""None"": {
            ""Exp"": -1, // Whatever you set to -1 is the default rank
            ""Color"": ""Default""
        },
        ""Silver"": {
            ""Tag"": ""[S]"", // Clan tag (scoreboard) of the rank. If not set, it uses the key instead, which is currently ""Silver""
            ""Exp"": 250, // From this amount of experience, the player is Silver
            ""Color"": ""LightBlue"" // Color code for the rank. Find color names here: https://github.com/roflmuffin/CounterStrikeSharp/blob/main/managed/CounterStrikeSharp.API/Modules/Utils/ChatColors.cs
        },
        ""Gold"": {
            ""Tag"": ""[G]"",
            ""Exp"": 1000,
            ""Color"": ""Red""
        }
        // You can add as many as you want
    }";

			if (!File.Exists(ranksFilePath))
			{
				File.WriteAllText(ranksFilePath, defaultRanksContent);

				Log("Default ranks file created.", LogLevel.Info);
			}

			try
			{
				using (FileStream fs = new FileStream(ranksFilePath, FileMode.Open, FileAccess.Read))
				using (StreamReader sr = new StreamReader(fs))
				{
					string jsonContent = Regex.Replace(sr.ReadToEnd(), @"/\*(.*?)\*/|//(.*)", string.Empty, RegexOptions.Multiline);
					ranks = JsonConvert.DeserializeObject<Dictionary<string, Rank>>(jsonContent)!;

					Rank? searchNoneRank = ranks.Values.FirstOrDefault(rank => rank.Exp == -1);

					if (searchNoneRank == null)
					{
						noneRank = "None";

						Log("Default rank is not set. You can set it by creating a rank with -1 exp.", LogLevel.Info);
					}
					else
					{
						noneRank = ranks.FirstOrDefault(pair => pair.Value == searchNoneRank).Key;

						Log($"Default rank is set to: {noneRank}", LogLevel.Debug);
					}
				}
			}
			catch (Exception ex)
			{
				Log("An error occurred: " + ex.Message, LogLevel.Error);
			}
		}

		public async void PrintTopXPlayers(CCSPlayerController player, int number)
		{
			Log("PrintTopXPlayers method is starting.", LogLevel.Debug);

			List<CCSPlayerController> players = Utilities.GetPlayers();
			foreach (CCSPlayerController savePlayer in players)
			{
				if (savePlayer.IsBot || savePlayer.IsHLTV)
					continue;

				if (PlayerSummaries.ContainsPlayer(savePlayer))
					await SaveClientRank(savePlayer);
			}

			MySqlQueryResult result = await MySql!.Table($"{TablePrefix}k4ranks").ExecuteQueryAsync($"SELECT `points`, `name` FROM `{TablePrefix}k4ranks` ORDER BY `points` DESC LIMIT {number};");

			Log($"Executed MySQL query to retrieve top {number} players.", LogLevel.Debug);

			if (result.Count > 0)
			{
				player!.PrintToChat($" {Config.GeneralSettings.Prefix} Top {number} Players:");

				for (int i = 0; i < result.Count; i++)
				{
					int pointCheck = result.Get<int>(i, "points");
					string playerRank = noneRank;
					string rankColor = $"{ChatColors.Default}";

					foreach (var kvp in ranks)
					{
						string level = kvp.Key;
						Rank rank = kvp.Value;

						if (pointCheck >= rank.Exp)
						{
							playerRank = level;
							rankColor = rank.Color;
						}
						else
							break;
					}

					string modifiedValue = rankColor;
					if (rankColor.Contains('{'))
					{
						foreach (FieldInfo field in typeof(ChatColors).GetFields())
						{
							string pattern = $"{{{field.Name}}}";
							if (rankColor.Contains(pattern, StringComparison.OrdinalIgnoreCase))
							{
								modifiedValue = modifiedValue.Replace(pattern, field.GetValue(null)!.ToString(), StringComparison.OrdinalIgnoreCase);
							}
						}
					}

					Log($"Printing player {i + 1} - Name: {result.Get<string>(i, "name")}, Points: {result.Get<int>(i, "points")}, Rank: {playerRank}", LogLevel.Debug);

					player.PrintToChat($" {ChatColors.Gold}{i + 1}. {modifiedValue}[{playerRank}] {ChatColors.Gold}{result.Get<string>(i, "name")} - {ChatColors.Blue}{result.Get<int>(i, "points")} points");
				}
			}
			else
			{
				player!.PrintToChat($" {Config.GeneralSettings.Prefix} No players found in the top {number}.");
			}

			Log("PrintTopXPlayers method has completed.", LogLevel.Debug);
		}

		public void LoadPlayerData(CCSPlayerController player)
		{
			Log("LoadPlayerData method is starting.", LogLevel.Debug);

			User newUser = new User
			{
				Points = 0,
				Rank = noneRank,
				RankPoints = -1
			};

			PlayerSummaries[player] = newUser;

			string escapedName = MySqlHelper.EscapeString(player.PlayerName);

			MySqlQueryValue values = new MySqlQueryValue()
				.Add("name", escapedName)
				.Add("steam_id", player.SteamID.ToString());

			MySql!.Table($"{TablePrefix}k4times").InsertIfNotExist(values, $"`name` = '{escapedName}'");
			MySql!.Table($"{TablePrefix}k4stats").InsertIfNotExist(values, $"`name` = '{escapedName}', `lastseen` = CURRENT_TIMESTAMP");
			MySql!.Table($"{TablePrefix}k4ranks").InsertIfNotExist(values.Add("`rank`", MySqlHelper.EscapeString(noneRank)), $"`name` = '{escapedName}'");

			if (Config.GeneralSettings.ModuleTimes)
			{
				MySqlQueryResult result = MySql!.Table($"{TablePrefix}k4times").Where(MySqlQueryCondition.New("steam_id", "=", player.SteamID.ToString())).Select();

				string[] timeFieldNames = { "all", "ct", "t", "spec", "alive", "dead" };

				foreach (var timeField in timeFieldNames)
				{
					PlayerSummaries[player].TimeFields[timeField] = result.Rows > 0 ? result.Get<int>(0, timeField) : 0;
				}

				DateTime now = DateTime.UtcNow;
				PlayerSummaries[player].Times["Connect"] = PlayerSummaries[player].Times["Team"] = PlayerSummaries[player].Times["Death"] = now;
			}

			if (Config.GeneralSettings.ModuleStats)
			{
				MySqlQueryResult result = MySql!.Table($"{TablePrefix}k4stats").Where(MySqlQueryCondition.New("steam_id", "=", player.SteamID.ToString())).Select();

				string[] statFieldNames = { "kills", "deaths", "hits", "headshots", "grenades", "mvp", "round_win", "round_lose" };

				foreach (var statField in statFieldNames)
				{
					PlayerSummaries[player].StatFields[statField] = result.Rows > 0 ? result.Get<int>(0, statField) : 0;
				}
			}

			if (Config.GeneralSettings.ModuleRanks)
			{
				newUser.RankObject = ranks.ContainsKey(noneRank) ? ranks[noneRank] : new Rank
				{
					Exp = -1,
					Color = $"{ChatColors.Default}"
				};

				MySqlQueryResult result = MySql!.Table($"{TablePrefix}k4ranks").Where(MySqlQueryCondition.New("steam_id", "=", player.SteamID.ToString())).Select("points");

				PlayerSummaries[player].Points = result.Rows > 0 ? result.Get<int>(0, "points") : 0;

				if (Config.RankSettings.ScoreboardScoreSync)
					player.Score = PlayerSummaries[player].Points;

				string newRank = noneRank;
				string clanTag = string.Empty;
				Rank? setRank = null;

				foreach (var kvp in ranks)
				{
					string level = kvp.Key;
					Rank rank = kvp.Value;

					if (PlayerSummaries[player].Points >= rank.Exp)
					{
						setRank = rank;
						newRank = level;
						clanTag = rank.Tag ?? level;
					}
					else
						break;
				}

				if (setRank == null)
					return;

				if (Config.RankSettings.ScoreboardRanks)
					player.Clan = $"{clanTag ?? $"[{newRank}]"}";

				PlayerSummaries[player].Rank = newRank;
				PlayerSummaries[player].RankPoints = setRank.Exp;

				string modifiedValue = setRank.Color;
				foreach (FieldInfo field in typeof(ChatColors).GetFields())
				{
					string pattern = $"{field.Name}";
					if (setRank.Color.Contains(pattern, StringComparison.OrdinalIgnoreCase))
					{
						modifiedValue = modifiedValue.Replace(pattern, field.GetValue(null)!.ToString(), StringComparison.OrdinalIgnoreCase);
					}
				}

				PlayerSummaries[player].RankObject!.Color = modifiedValue;
			}

			Log("LoadPlayerData method has completed.", LogLevel.Debug);
		}

		private (int playerPlace, int totalPlayers) GetPlayerPlaceAndCount(string steamID)
		{
			Log("GetPlayerPlaceAndCount method is starting.", LogLevel.Debug);

			MySqlQueryResult result = MySql!.Table($"{TablePrefix}k4ranks").ExecuteQuery($"SELECT (SELECT COUNT(*) FROM `{TablePrefix}k4ranks` WHERE `points` > (SELECT `points` FROM `{TablePrefix}k4ranks` WHERE `steam_id` = '{steamID}')) AS playerCount, COUNT(*) AS totalPlayers FROM `{TablePrefix}k4ranks`;")!;

			Log("Executed MySQL query to get player place and count.", LogLevel.Debug);

			if (result.Count > 0)
			{
				int playersWithMorePoints = result.Get<int>(0, "playerCount");
				int totalPlayers = result.Get<int>(0, "totalPlayers");

				// Log the player place and total players
				Log($"Player place: {playersWithMorePoints + 1}, Total players: {totalPlayers}", LogLevel.Debug);

				return (playersWithMorePoints + 1, totalPlayers);
			}

			Log("GetPlayerPlaceAndCount method has completed.", LogLevel.Debug);

			return (0, 0);
		}


		public void ModifyClientPoints(CCSPlayerController player, CHANGE_MODE mode, int amount, string reason)
		{
			// Log that the ModifyClientPoints method is starting
			Log("ModifyClientPoints method is starting.", LogLevel.Debug);

			if (!player.IsValidPlayer())
			{
				// Log that the player is not valid
				Log("Player is not valid.", LogLevel.Debug);
				return;
			}

			if (!IsPointsAllowed() || amount == 0)
			{
				// Log that points are not allowed or the amount is zero
				Log("Points are not allowed or the amount is zero.", LogLevel.Debug);
				return;
			}

			if (!PlayerSummaries.ContainsPlayer(player))
			{
				LoadPlayerData(player);

				// Log that player data is loaded
				Log($"Player data loaded for {player.PlayerName}.", LogLevel.Debug);
			}

			if (AdminManager.PlayerHasPermissions(player, "@k4system/vip/points-multiplier"))
			{
				amount = (int)Math.Round(amount * Config.RankSettings.VipMultiplier);

				// Log the VIP multiplier applied
				Log($"VIP multiplier applied. New amount: {amount}.", LogLevel.Debug);
			}

			switch (mode)
			{
				case CHANGE_MODE.SET:
					{
						player.PrintToChat($" {Config.GeneralSettings.Prefix} {ChatColors.White}Points: {ChatColors.Gold}{PlayerSummaries[player].Points} [={amount} {reason}]");
						PlayerSummaries[player].Points = amount;

						// Log points set operation
						Log($"Points set to {amount} for {player.PlayerName}.", LogLevel.Info);
						break;
					}
				case CHANGE_MODE.GIVE:
					{
						if (!Config.RankSettings.RoundEndPoints)
						{
							player.PrintToChat($" {Config.GeneralSettings.Prefix} {ChatColors.White}Points: {ChatColors.Green}{PlayerSummaries[player].Points} [+{amount} {reason}]");
						}
						else PlayerSummaries[player].PointsChanged += amount;

						PlayerSummaries[player].Points += amount;

						// Log points give operation
						Log($"Points added (+{amount}) for {player.PlayerName}.", LogLevel.Info);
						break;
					}
				case CHANGE_MODE.REMOVE:
					{
						if (!Config.RankSettings.RoundEndPoints)
						{
							player.PrintToChat($" {Config.GeneralSettings.Prefix} {ChatColors.White}Points: {ChatColors.Red}{PlayerSummaries[player].Points} [-{amount} {reason}]");
						}
						else PlayerSummaries[player].PointsChanged -= amount;

						PlayerSummaries[player].Points -= amount;

						if (PlayerSummaries[player].Points < 0)
							PlayerSummaries[player].Points = 0;

						// Log points remove operation
						Log($"Points removed (-{amount}) for {player.PlayerName}.", LogLevel.Info);
						break;
					}
				default:
					{
						Log($"Invalid operation at the point modification function: {mode}", LogLevel.Error);
						break;
					}
			}

			if (Config.RankSettings.ScoreboardScoreSync)
				player.Score = PlayerSummaries[player].Points;

			string newRank = noneRank;
			string clanTag = string.Empty;
			Rank? setRank = null;

			foreach (var kvp in ranks)
			{
				string level = kvp.Key;
				Rank rank = kvp.Value;

				if (PlayerSummaries[player].Points >= rank.Exp)
				{
					setRank = rank;
					newRank = level;
					clanTag = rank.Tag ?? level;
				}
				else
					break;
			}

			if (setRank == null)
				return;

			if (Config.RankSettings.ScoreboardRanks)
				player.Clan = $"{clanTag ?? $"[{newRank}]"}";

			if (newRank == noneRank || newRank == PlayerSummaries[player].Rank)
				return;

			Server.PrintToChatAll($" {ChatColors.Red}{Config.GeneralSettings.Prefix} {ChatColors.Gold}{player.PlayerName} has been {(setRank.Exp > PlayerSummaries[player].RankPoints ? "promoted" : "demoted")} to {newRank}.");

			PlayerSummaries[player].Rank = newRank;
			PlayerSummaries[player].RankPoints = setRank.Exp;

			string modifiedValue = setRank.Color;
			foreach (FieldInfo field in typeof(ChatColors).GetFields())
			{
				string pattern = $"{field.Name}";
				if (setRank.Color.Contains(pattern, StringComparison.OrdinalIgnoreCase))
				{
					modifiedValue = modifiedValue.Replace(pattern, field.GetValue(null)!.ToString(), StringComparison.OrdinalIgnoreCase);
				}
			}

			PlayerSummaries[player].RankObject!.Color = modifiedValue;

			// Log that the ModifyClientPoints method has completed
			Log("ModifyClientPoints method has completed.", LogLevel.Debug);
		}

		public void CheckNewRank(CCSPlayerController player)
		{
			// Log that the CheckNewRank method is starting
			Log("CheckNewRank method is starting.", LogLevel.Debug);

			if (!Config.GeneralSettings.ModuleRanks)
			{
				// Log that ranks module is not enabled
				Log("Ranks module is not enabled.", LogLevel.Info);
				return;
			}

			if (Config.RankSettings.ScoreboardScoreSync)
				player.Score = PlayerSummaries[player].Points;

			string newRank = noneRank;
			string clanTag = string.Empty;
			Rank? setRank = null;

			foreach (var kvp in ranks)
			{
				string level = kvp.Key;
				Rank rank = kvp.Value;

				if (PlayerSummaries[player].Points >= rank.Exp)
				{
					setRank = rank;
					newRank = level;
					clanTag = rank.Tag ?? level;
				}
				else
					break;
			}

			if (setRank == null)
			{
				PlayerSummaries[player].Rank = noneRank;
				PlayerSummaries[player].RankPoints = 0;
				PlayerSummaries[player].RankObject!.Color = $"{ChatColors.Default}";

				// Log that player rank has been reset
				Log($"Player rank reset for {player.PlayerName}.", LogLevel.Info);
				return;
			}

			if (Config.RankSettings.ScoreboardRanks)
				player.Clan = $"{clanTag ?? $"[{newRank}]"}";

			PlayerSummaries[player].Rank = newRank;
			PlayerSummaries[player].RankPoints = setRank.Exp;

			string modifiedValue = setRank.Color;
			foreach (FieldInfo field in typeof(ChatColors).GetFields())
			{
				string pattern = $"{field.Name}";
				if (setRank.Color.Contains(pattern, StringComparison.OrdinalIgnoreCase))
				{
					modifiedValue = modifiedValue.Replace(pattern, field.GetValue(null)!.ToString(), StringComparison.OrdinalIgnoreCase);
				}
			}

			PlayerSummaries[player].RankObject!.Color = modifiedValue;

			Log("CheckNewRank method has completed.", LogLevel.Debug);
		}

		public bool IsPointsAllowed()
		{
			Log("IsPointsAllowed method is starting.", LogLevel.Debug);

			List<CCSPlayerController> players = Utilities.GetPlayers();
			int notBots = players.Count(player => !player.IsBot);

			bool isAllowed = Config.GeneralSettings.ModuleRanks && (!K4ryuu.GameRules().WarmupPeriod || Config.RankSettings.WarmupPoints) && (Config.RankSettings.MinPlayers <= notBots);

			Log($"Points are {(isAllowed ? "allowed" : "not allowed")}.", LogLevel.Info);

			return isAllowed;
		}

		public bool IsStatsAllowed()
		{
			Log("IsStatsAllowed method is starting.", LogLevel.Debug);

			List<CCSPlayerController> players = Utilities.GetPlayers();
			int notBots = players.Count(player => !player.IsBot);

			bool isAllowed = Config.GeneralSettings.ModuleStats && (!K4ryuu.GameRules().WarmupPeriod || Config.StatisticSettings.WarmupStats) && (Config.StatisticSettings.MinPlayers <= notBots);

			Log($"Stats are {(isAllowed ? "allowed" : "not allowed")}.", LogLevel.Info);

			return isAllowed;
		}

		public void ResetKillStreak(int playerIndex)
		{
			// Log that the ResetKillStreak method is starting
			Log("ResetKillStreak method is starting.", LogLevel.Debug);

			playerKillStreaks[playerIndex] = (1, DateTime.Now);

			// Log that the kill streak is reset for the player
			Log($"Kill streak reset for player at index {playerIndex}.", LogLevel.Info);
		}

		private string GetFieldForTeam(CsTeam team)
		{
			switch (team)
			{
				case CsTeam.Terrorist:
					return "t";
				case CsTeam.CounterTerrorist:
					return "ct";
				default:
					return "spec";
			}
		}

		public string FormatPlaytime(int totalSeconds)
		{
			string[] units = { "y", "mo", "d", "h", "m", "s" };
			int[] values = { totalSeconds / 31536000, totalSeconds % 31536000 / 2592000, totalSeconds % 2592000 / 86400, totalSeconds % 86400 / 3600, totalSeconds % 3600 / 60, totalSeconds % 60 };

			StringBuilder formattedTime = new StringBuilder();

			bool addedValue = false;

			for (int i = 0; i < units.Length; i++)
			{
				if (values[i] > 0)
				{
					formattedTime.Append($"{values[i]}{units[i]}, ");
					addedValue = true;
				}
			}

			if (!addedValue)
			{
				formattedTime.Append("0s");
			}

			return formattedTime.ToString().TrimEnd(' ', ',');
		}

		public async Task SaveClientRank(CCSPlayerController player)
		{
			// Log that the SaveClientRank method is starting
			Log("SaveClientRank method is starting.", LogLevel.Debug);

			string escapedName = MySqlHelper.EscapeString(player.PlayerName);

			string updateQuery = @$"INSERT INTO `{TablePrefix}k4ranks`
                            (`steam_id`, `name`, `rank`, `points`)
                            VALUES ('{player.SteamID}', '{escapedName}', '{PlayerSummaries[player].Rank}', {PlayerSummaries[player].Points})
                            ON DUPLICATE KEY UPDATE
                            `name` = '{escapedName}',
                            `rank` = '{PlayerSummaries[player].Rank}',
                            `points` = {PlayerSummaries[player].Points};";

			await MySql!.ExecuteNonQueryAsync(updateQuery);

			// Log that client rank has been saved
			Log($"Client rank saved for {player.PlayerName}.", LogLevel.Info);
		}

		public async Task SaveClientStats(CCSPlayerController player)
		{
			// Log that the SaveClientStats method is starting
			Log("SaveClientStats method is starting.", LogLevel.Debug);

			string escapedName = MySqlHelper.EscapeString(player.PlayerName);

			string updateQuery = @$"INSERT INTO `{TablePrefix}k4stats`
                            (`steam_id`, `name`, `kills`, `deaths`, `hits`, `headshots`, `grenades`, `mvp`, `round_win`, `round_lose`)
                            VALUES ('{player.SteamID}', '{escapedName}', {PlayerSummaries[player].StatFields["kills"]}, {PlayerSummaries[player].StatFields["deaths"]}, {PlayerSummaries[player].StatFields["hits"]}, {PlayerSummaries[player].StatFields["headshots"]}, {PlayerSummaries[player].StatFields["grenades"]}, {PlayerSummaries[player].StatFields["mvp"]}, {PlayerSummaries[player].StatFields["round_win"]}, {PlayerSummaries[player].StatFields["round_lose"]})
                            ON DUPLICATE KEY UPDATE
                            `name` = '{escapedName}',
                            `kills` = {PlayerSummaries[player].StatFields["kills"]},
                            `deaths` = {PlayerSummaries[player].StatFields["deaths"]},
                            `hits` = {PlayerSummaries[player].StatFields["hits"]},
                            `headshots` = {PlayerSummaries[player].StatFields["headshots"]},
                            `grenades` = {PlayerSummaries[player].StatFields["grenades"]},
                            `mvp` = {PlayerSummaries[player].StatFields["mvp"]},
                            `round_win` = {PlayerSummaries[player].StatFields["round_win"]},
                            `round_lose` = {PlayerSummaries[player].StatFields["round_lose"]};";

			await MySql!.ExecuteNonQueryAsync(updateQuery);

			// Log that client stats have been saved
			Log($"Client stats saved for {player.PlayerName}.", LogLevel.Info);
		}

		public async Task SaveClientTime(CCSPlayerController player)
		{
			// Log that the SaveClientTime method is starting
			Log("SaveClientTime method is starting.", LogLevel.Debug);

			DateTime now = DateTime.UtcNow;

			PlayerSummaries[player].TimeFields["all"] += (int)Math.Round((now - PlayerSummaries[player].Times["Connect"]).TotalSeconds);
			PlayerSummaries[player].TimeFields[GetFieldForTeam((CsTeam)player.TeamNum)] += (int)Math.Round((now - PlayerSummaries[player].Times["Team"]).TotalSeconds);
			PlayerSummaries[player].TimeFields[player.PawnIsAlive ? "alive" : "dead"] = (int)Math.Round((now - PlayerSummaries[player].Times["Death"]).TotalSeconds);

			string escapedName = MySqlHelper.EscapeString(player.PlayerName);

			string updateQuery = @$"INSERT INTO `{TablePrefix}k4times`
                            (`steam_id`, `name`, `all`, `ct`, `t`, `spec`, `dead`, `alive`)
                            VALUES ('{player.SteamID}', '{escapedName}', {PlayerSummaries[player].TimeFields["all"]}, {PlayerSummaries[player].TimeFields["ct"]}, {PlayerSummaries[player].TimeFields["t"]}, {PlayerSummaries[player].TimeFields["spec"]}, {PlayerSummaries[player].TimeFields["dead"]}, {PlayerSummaries[player].TimeFields["alive"]})
                            ON DUPLICATE KEY UPDATE
                            `name` = '{escapedName}',
                            `all` = {PlayerSummaries[player].TimeFields["all"]},
                            `ct` = {PlayerSummaries[player].TimeFields["ct"]},
                            `t` = {PlayerSummaries[player].TimeFields["t"]},
                            `spec` = {PlayerSummaries[player].TimeFields["spec"]},
                            `dead` = {PlayerSummaries[player].TimeFields["dead"]},
                            `alive` = {PlayerSummaries[player].TimeFields["alive"]};";

			await MySql!.ExecuteNonQueryAsync(updateQuery);

			// Log that client time has been saved
			Log($"Client time saved for {player.PlayerName}.", LogLevel.Info);

			PlayerSummaries[player].Times["Connect"] = PlayerSummaries[player].Times["Team"] = PlayerSummaries[player].Times["Death"] = now;
		}
	}
}