namespace K4System
{
	using MySqlConnector;

	using CounterStrikeSharp.API;
	using CounterStrikeSharp.API.Core;
	using CounterStrikeSharp.API.Modules.Admin;
	using CounterStrikeSharp.API.Modules.Commands;
	using CounterStrikeSharp.API.Modules.Commands.Targeting;
	using CounterStrikeSharp.API.Modules.Menu;
	using Microsoft.Extensions.Logging;
	using System.Data;
	using K4System.Models;
	using Dapper;

	public partial class ModuleRank : IModuleRank
	{
		public void Initialize_Commands()
		{
			CommandSettings commands = Config.CommandSettings;

			commands.RankCommands.ForEach(commandString =>
			{
				plugin.AddCommand($"css_{commandString}", "Check the current rank and points", plugin.CallbackAnonymizer(OnCommandRank));
			});

			commands.RanksCommands.ForEach(commandString =>
			{
				plugin.AddCommand($"css_{commandString}", "Check the available ranks and their data", plugin.CallbackAnonymizer(OnCommandRanks));
			});

			commands.TopCommands.ForEach(commandString =>
			{
				plugin.AddCommand($"css_{commandString}", "Check the top players by points", plugin.CallbackAnonymizer(OnCommandTop));
			});


			plugin.AddCommand("css_setpoints", "SEt the targeted player's points", plugin.CallbackAnonymizer(OnCommandSetPoints));
			plugin.AddCommand("css_givepoints", "Give points the targeted player", plugin.CallbackAnonymizer(OnCommandGivePoints));
			plugin.AddCommand("css_removepoints", "Remove points from the targeted player", plugin.CallbackAnonymizer(OnCommandRemovePoints));
			plugin.AddCommand("css_toggletag", "Toggles the tag assigned by permissions", plugin.CallbackAnonymizer(OnCommandToggleTag));
			plugin.AddCommand("css_togglepointmsg", "Toggles the chat messages of points modifications", plugin.CallbackAnonymizer(OnCommandTogglePointMessages));
		}

		public void OnCommandTogglePointMessages(CCSPlayerController? player, CommandInfo info)
		{
			if (!plugin.CommandHelper(player, info, CommandUsage.CLIENT_ONLY))
				return;

			K4Player? k4player = plugin.GetK4Player(player!);

			if (k4player is null)
			{
				info.ReplyToCommand($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.general.loading"]}");
				return;
			}

			RankData? playerData = k4player.rankData;

			if (playerData is null)
				return;

			playerData.MuteMessages = !playerData.MuteMessages;
			info.ReplyToCommand($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.general.togglepointmsg", playerData.MuteMessages ? plugin.Localizer["k4.general.state.disabled"] : plugin.Localizer["k4.general.state.enabled"]]}");
		}

		public void OnCommandToggleTag(CCSPlayerController? player, CommandInfo info)
		{
			if (!plugin.CommandHelper(player, info, CommandUsage.CLIENT_ONLY))
				return;

			K4Player? k4player = plugin.GetK4Player(player!);

			if (k4player is null)
			{
				info.ReplyToCommand($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.general.loading"]}");
				return;
			}

			RankData? playerData = k4player.rankData;

			if (playerData is null)
				return;

			playerData.HideAdminTag = !playerData.HideAdminTag;

			SetPlayerClanTag(k4player);

			info.ReplyToCommand($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.general.toggletag", playerData.HideAdminTag ? plugin.Localizer["k4.general.state.disabled"] : plugin.Localizer["k4.general.state.enabled"]]}");
		}

		public void OnCommandRank(CCSPlayerController? player, CommandInfo info)
		{
			if (!plugin.CommandHelper(player, info, CommandUsage.CLIENT_ONLY))
				return;

			K4Player? k4player = plugin.GetK4Player(player!);

			if (k4player is null)
			{
				info.ReplyToCommand($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.general.loading"]}");
				return;
			}

			int printCount = 5;

			if (int.TryParse(info.ArgByIndex(1), out int parsedInt))
			{
				printCount = Math.Clamp(parsedInt, 1, 25);
			}

			Task.Run(async () =>
			{
				(int playerPlace, int totalPlayers) taskValues = await GetPlayerPlaceAndCountAsync(k4player);

				int playerPlace = taskValues.playerPlace;
				int totalPlayers = taskValues.totalPlayers;

				RankData? playerData = k4player.rankData;

				if (playerData is null)
					return;

				int higherRanksCount = rankDictionary.Count(kv => kv.Value.Point > playerData.Points);

				Server.NextFrame(() =>
				{
					player!.PrintToChat($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.ranks.rank.title", k4player.PlayerName]}");
					player.PrintToChat(plugin.Localizer["k4.ranks.rank.line1", playerData.Points, playerData.Rank.Color, playerData.Rank.Name, rankDictionary.Count - higherRanksCount, rankDictionary.Count]);

					KeyValuePair<string, Rank> nextRankEntry = rankDictionary
								.Where(kv => kv.Value.Point > playerData.Rank.Point)
								.OrderBy(kv => kv.Value.Point)
								.FirstOrDefault();

					if (nextRankEntry.Value != null)
					{
						Rank nextRank = nextRankEntry.Value;

						player.PrintToChat(plugin.Localizer["k4.ranks.rank.line2", nextRank.Color, nextRank.Name]);
						player.PrintToChat(plugin.Localizer["k4.ranks.rank.line3", nextRank.Point - playerData.Points]);
					}

					player.PrintToChat(plugin.Localizer["k4.ranks.rank.line4", playerPlace, totalPlayers]);
				});
			});
		}

		public void OnCommandRanks(CCSPlayerController? player, CommandInfo info)
		{
			if (!plugin.CommandHelper(player, info, CommandUsage.CLIENT_ONLY))
				return;

			MenuManager.OpenChatMenu(player!, ranksMenu);
		}



		public void OnCommandTop(CCSPlayerController? player, CommandInfo info)
		{
			if (!plugin.CommandHelper(player, info, CommandUsage.CLIENT_ONLY))
				return;

			K4Player? k4player = plugin.GetK4Player(player!);

			if (k4player is null)
			{
				info.ReplyToCommand($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.general.loading"]}");
				return;
			}

			int printCount = 5;

			if (int.TryParse(info.ArgByIndex(1), out int parsedInt))
			{
				printCount = Math.Clamp(parsedInt, 1, 25);
			}

			Logger.LogInformation($"Player {k4player.PlayerName} requested top {printCount} players.");

			Task.Run(async () =>
			{
				Console.WriteLine("Saving all players data");

				await plugin.SaveAllPlayersDataAsync();

				Console.WriteLine("Fetching top data");

				List<(int points, string name)>? rankData = await FetchTopDataAsync(printCount);

				Console.WriteLine("Fetched top data, waiting tick to print to chat.");

				Server.NextFrame(() =>
				{
					Console.WriteLine("Printing top data to chat.");

					if (!k4player.IsValid || !k4player.IsPlayer)
						return;

					Console.WriteLine("Player is valid and is player.");

					if (rankData?.Count > 0)
					{
						for (int i = 0; i < rankData.Count; i++)
						{
							int points = rankData[i].points;
							string name = rankData[i].name;

							Rank rank = GetPlayerRank(points);

							player!.PrintToChat($" {plugin.Localizer["k4.ranks.top.line", i + 1, rank.Color, rank.Name, name, points]}");
						}
					}
					else
					{
						player!.PrintToChat($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.ranks.top.notfound", printCount]}");
					}
				});
			});
		}

		public async Task<List<(int points, string name)>?> FetchTopDataAsync(int printCount)
		{
			string query = $@"SELECT `points`, `name` FROM `{Config.DatabaseSettings.TablePrefix}k4ranks` ORDER BY `points` DESC LIMIT @PrintCount;";

			try
			{
				using (var connection = plugin.CreateConnection(Config))
				{
					await connection.OpenAsync();

					var rankData = (await connection.QueryAsync<(int, string)>(query, new { PrintCount = printCount })).ToList();

					return rankData;
				}
			}
			catch (Exception ex)
			{
				Server.NextFrame(() => { Logger.LogError($"A problem occurred while fetching top data: {ex.Message}"); });
				return null;
			}
		}

		public void OnCommandSetPoints(CCSPlayerController? player, CommandInfo info)
		{
			if (!plugin.CommandHelper(player, info, CommandUsage.CLIENT_AND_SERVER, 2, "<target> <amount>", "@k4system/admin"))
				return;

			string playerName = player != null && player.IsValid && player.PlayerPawn.Value != null ? player.PlayerName : "SERVER";

			if (!int.TryParse(info.ArgByIndex(2), out int parsedInt))
			{
				info.ReplyToCommand($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.general.invalidamount"]}");
				return;
			}

			TargetResult targetResult = info.GetArgTargetResult(1);

			if (!targetResult.Any())
			{
				info.ReplyToCommand($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.general.targetnotfound"]}");
				return;
			}

			foreach (CCSPlayerController target in targetResult.Players)
			{
				K4Player? k4player = plugin.GetK4Player(target);

				if (k4player is null || !k4player.IsValid)
				{
					info.ReplyToCommand($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.general.targetloading", target.PlayerName]}");
					continue;
				}

				if (!k4player.IsPlayer)
				{
					info.ReplyToCommand($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.general.targetnobot", target.PlayerName]}");
					continue;
				}

				if (!AdminManager.CanPlayerTarget(player, target))
				{
					info.ReplyToCommand($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.general.targetimmunity", target.PlayerName]}");
					continue;
				}

				RankData? playerData = k4player.rankData;

				if (playerData is null)
					return;

				playerData.RoundPoints = parsedInt;
				playerData.Points = 0;

				if (playerName != "SERVER")
					Server.PrintToChatAll($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.ranks.setpoints", target.PlayerName, parsedInt, playerName]}");

				Task.Run(() => plugin.SavePlayerDataAsync(k4player, false));
			}
		}

		public void OnCommandGivePoints(CCSPlayerController? player, CommandInfo info)
		{
			if (!plugin.CommandHelper(player, info, CommandUsage.CLIENT_AND_SERVER, 2, "<target> <amount>", "@k4system/admin"))
				return;

			string playerName = player != null && player.IsValid && player.PlayerPawn.Value != null ? player.PlayerName : "SERVER";

			if (!int.TryParse(info.ArgByIndex(2), out int parsedInt))
			{
				info.ReplyToCommand($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.general.invalidamount"]}");
				return;
			}

			TargetResult targetResult = info.GetArgTargetResult(1);

			if (!targetResult.Any())
			{
				info.ReplyToCommand($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.general.targetnotfound"]}");
				return;
			}

			foreach (CCSPlayerController target in targetResult.Players)
			{
				K4Player? k4player = plugin.GetK4Player(target);

				if (k4player is null || !k4player.IsValid)
				{
					info.ReplyToCommand($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.general.targetloading", target.PlayerName]}");
					continue;
				}

				if (!k4player.IsPlayer)
				{
					info.ReplyToCommand($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.general.targetnobot", target.PlayerName]}");
					continue;
				}

				if (!AdminManager.CanPlayerTarget(player, target))
				{
					info.ReplyToCommand($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.general.targetimmunity", target.PlayerName]}");
					continue;
				}

				RankData? playerData = k4player.rankData;

				if (playerData is null)
					return;

				playerData.RoundPoints += parsedInt;
				playerData.Points += parsedInt;

				if (playerName != "SERVER")
					Server.PrintToChatAll($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.ranks.givepoints", playerName, parsedInt, target.PlayerName]}");

				Task.Run(() => plugin.SavePlayerDataAsync(k4player, false));
			}
		}

		public void OnCommandRemovePoints(CCSPlayerController? player, CommandInfo info)
		{
			if (!plugin.CommandHelper(player, info, CommandUsage.CLIENT_AND_SERVER, 2, "<target> <amount>", "@k4system/admin"))
				return;

			string playerName = player != null && player.IsValid && player.PlayerPawn.Value != null ? player.PlayerName : "SERVER";

			if (!int.TryParse(info.ArgByIndex(2), out int parsedInt))
			{
				info.ReplyToCommand($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.general.invalidamount"]}");
				return;
			}

			TargetResult targetResult = info.GetArgTargetResult(1);

			if (!targetResult.Any())
			{
				info.ReplyToCommand($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.general.targetnotfound"]}");
				return;
			}

			foreach (CCSPlayerController target in targetResult.Players)
			{
				K4Player? k4player = plugin.GetK4Player(target);

				if (k4player is null || !k4player.IsValid)
				{
					info.ReplyToCommand($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.general.targetloading", target.PlayerName]}");
					continue;
				}

				if (!k4player.IsPlayer)
				{
					info.ReplyToCommand($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.general.targetnobot", target.PlayerName]}");
					continue;
				}

				if (!AdminManager.CanPlayerTarget(player, target))
				{
					info.ReplyToCommand($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.general.targetimmunity", target.PlayerName]}");
					continue;
				}

				RankData? playerData = k4player.rankData;

				if (playerData is null)
					return;

				playerData.RoundPoints -= parsedInt;
				playerData.Points -= parsedInt;

				if (playerName != "SERVER")
					Server.PrintToChatAll($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.ranks.removepoints", playerName, parsedInt, target.PlayerName]}");

				Task.Run(() => plugin.SavePlayerDataAsync(k4player, false));
			}
		}
	}
}
