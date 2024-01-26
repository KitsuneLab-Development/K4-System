namespace K4System
{
	using CounterStrikeSharp.API;
	using CounterStrikeSharp.API.Core;
	using CounterStrikeSharp.API.Modules.Admin;
	using CounterStrikeSharp.API.Modules.Commands;

	using Nexd.MySQL;
	using CounterStrikeSharp.API.Modules.Commands.Targeting;
	using CounterStrikeSharp.API.Modules.Menu;

	public partial class ModuleRank : IModuleRank
	{
		public void Initialize_Commands(Plugin plugin)
		{
			CommandSettings commands = Config.CommandSettings;

			commands.RankCommands.ForEach(commandString =>
			{
				plugin.AddCommand($"css_{commandString}", "Check the current rank and points",
					[CommandHelper(0, whoCanExecute: CommandUsage.CLIENT_ONLY)] (player, info) =>
				{
					if (player == null || !player.IsValid || player.PlayerPawn.Value == null)
						return;

					if (!rankCache.ContainsPlayer(player))
					{
						info.ReplyToCommand($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.general.loading"]}");
						return;
					}

					CCSPlayerController savedPlayer = player;
					string steamID = savedPlayer.SteamID.ToString();
					string name = savedPlayer.PlayerName;
					RankData playerData = rankCache[savedPlayer];

					int playersWithMorePoints = 0;
					int totalPlayers = 0;

					MySqlQueryResult result = Database.Table($"{Config.DatabaseSettings.TablePrefix}k4ranks")
						.ExecuteQuery($"SELECT (SELECT COUNT(*) FROM `{Config.DatabaseSettings.TablePrefix}k4ranks` WHERE `points` > (SELECT `points` FROM `{Config.DatabaseSettings.TablePrefix}k4ranks` WHERE `steam_id` = '{steamID}')) AS playerCount, COUNT(*) AS totalPlayers FROM `{Config.DatabaseSettings.TablePrefix}k4ranks`;")!;

					if (result.Count > 0)
					{
						playersWithMorePoints = result.Get<int>(0, "playerCount") + 1;
						totalPlayers = result.Get<int>(0, "totalPlayers");
					}

					int higherRanksCount = rankDictionary.Count(kv => kv.Value.Point > playerData.Points);

					info.ReplyToCommand($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.ranks.rank.title", name]}");
					info.ReplyToCommand(plugin.Localizer["k4.ranks.rank.line1", playerData.Points, playerData.Rank.Color, playerData.Rank.Name, rankDictionary.Count - higherRanksCount, rankDictionary.Count]);

					var nextRankEntry = rankDictionary
								.Where(kv => kv.Value.Point > playerData.Rank.Point)
								.OrderBy(kv => kv.Value.Point)
								.FirstOrDefault();

					if (nextRankEntry.Value != null)
					{
						Rank nextRank = nextRankEntry.Value;

						info.ReplyToCommand(plugin.Localizer["k4.ranks.rank.line2", nextRank.Color, nextRank.Name]);
						info.ReplyToCommand(plugin.Localizer["k4.ranks.rank.line3", nextRank.Point - playerData.Points]);
					}

					info.ReplyToCommand(plugin.Localizer["k4.ranks.rank.line4", playersWithMorePoints, totalPlayers]);
				});
			});

			commands.RanksCommands.ForEach(commandString =>
			{
				plugin.AddCommand($"css_{commandString}", "Check the available ranks and their data",
					[CommandHelper(0, whoCanExecute: CommandUsage.CLIENT_ONLY)] (player, info) =>
				{
					if (player == null || !player.IsValid || player.PlayerPawn.Value == null)
						return;

					info.ReplyToCommand($" {plugin.Localizer["k4.general.prefix"]} The command is disabled for now because of a bug. We are working on it.");

					//ChatMenus.OpenMenu(player, ranksMenu);
				});
			});

			commands.ResetMyCommands.ForEach(commandString =>
			{
				plugin.AddCommand($"css_{commandString}", "Resets the player's own points to zero",
					[CommandHelper(0, whoCanExecute: CommandUsage.CLIENT_ONLY)] (player, info) =>
				{
					if (player == null || !player.IsValid || player.PlayerPawn.Value == null)
						return;

					if (!rankCache.ContainsPlayer(player))
					{
						info.ReplyToCommand($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.general.loading"]}");
						return;
					}

					RankData playerData = rankCache[player];

					playerData.RoundPoints -= playerData.Points;
					playerData.Points = 0;

					SavePlayerRankCache(player, false);

					Server.PrintToChatAll($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.ranks.resetmyrank", player.PlayerName]}");
				});
			});

			commands.TopCommands.ForEach(commandString =>
			{
				plugin.AddCommand($"css_{commandString}", "Check the top players by points",
					[CommandHelper(0, whoCanExecute: CommandUsage.CLIENT_ONLY)] (player, info) =>
				{
					if (player == null || !player.IsValid || player.PlayerPawn.Value == null)
						return;

					if (!rankCache.ContainsPlayer(player))
					{
						info.ReplyToCommand($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.general.loading"]}");
						return;
					}

					int printCount = 5;

					if (int.TryParse(info.ArgByIndex(1), out int parsedInt))
					{
						printCount = Math.Min(50, parsedInt);
					}

					CCSPlayerController savedPlayer = player;

					SaveAllPlayerCache(false);

					MySqlQueryResult getResult = Database.Table($"{Config.DatabaseSettings.TablePrefix}k4ranks")
						.ExecuteQuery($"SELECT `points`, `name` FROM `{Config.DatabaseSettings.TablePrefix}k4ranks` ORDER BY `points` DESC LIMIT {printCount};");

					if (getResult.Count > 0)
					{
						player.PrintToChat($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.ranks.top.title", printCount]}");

						for (int i = 0; i < getResult.Count; i++)
						{
							int points = getResult.Get<int>(i, "points");

							Rank rank = GetPlayerRank(points);

							player.PrintToChat($" {plugin.Localizer["k4.ranks.top.line", i + 1, rank.Color, rank.Name, getResult.Get<string>(i, "name"), points]}");
						}
					}
					else
					{
						player.PrintToChat($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.ranks.top.notfound", printCount]}");
					}
				});

			});

			plugin.AddCommand("css_resetrank", "Resets the targeted player's points to zero",
				[CommandHelper(1, "<target>", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)][RequiresPermissions("@k4system/admin")] (player, info) =>
			{
				string playerName = player != null && player.IsValid && player.PlayerPawn.Value != null ? player.PlayerName : "SERVER";

				TargetResult targetResult = info.GetArgTargetResult(1);

				if (!targetResult.Any())
				{
					info.ReplyToCommand($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.general.targetnotfound"]}");
					return;
				}

				foreach (CCSPlayerController target in targetResult.Players)
				{
					if (target.IsBot || target.IsHLTV)
					{
						info.ReplyToCommand($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.general.targetnobot", target.PlayerName]}");
						continue;
					}

					if (!AdminManager.CanPlayerTarget(player, target))
					{
						info.ReplyToCommand($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.general.targetimmunity", target.PlayerName]}");
						continue;
					}

					if (!rankCache.ContainsPlayer(target))
					{
						info.ReplyToCommand($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.general.targetloading", target.PlayerName]}");
						continue;
					}

					RankData playerData = rankCache[target];

					playerData.RoundPoints -= playerData.Points;
					playerData.Points = 0;

					SavePlayerRankCache(target, false);

					Server.PrintToChatAll($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.ranks.resetrank", target.PlayerName, playerName]}");
				}
			});

			plugin.AddCommand("css_setpoints", "Resets the targeted player's points to zero",
				[CommandHelper(2, "<target> <amount>", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)][RequiresPermissions("@k4system/admin")] (player, info) =>
			{
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
					if (target.IsBot || target.IsHLTV)
					{
						info.ReplyToCommand($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.general.targetnobot", target.PlayerName]}");
						continue;
					}

					if (!AdminManager.CanPlayerTarget(player, target))
					{
						info.ReplyToCommand($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.general.targetimmunity", target.PlayerName]}");
						continue;
					}

					if (!rankCache.ContainsPlayer(target))
					{
						info.ReplyToCommand($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.general.targetloading", target.PlayerName]}");
						continue;
					}

					RankData playerData = rankCache[target];

					playerData.RoundPoints = parsedInt;
					playerData.Points = 0;

					SavePlayerRankCache(target, false);

					Server.PrintToChatAll($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.ranks.setpoints", target.PlayerName, parsedInt, playerName]}");
				}
			});

			plugin.AddCommand("css_givepoints", "Give points the targeted player",
				[CommandHelper(2, "<target> <amount>", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)][RequiresPermissions("@k4system/admin")] (player, info) =>
			{
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
					if (target.IsBot || target.IsHLTV)
					{
						info.ReplyToCommand($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.general.targetnobot", target.PlayerName]}");
						continue;
					}

					if (!AdminManager.CanPlayerTarget(player, target))
					{
						info.ReplyToCommand($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.general.targetimmunity", target.PlayerName]}");
						continue;
					}

					if (!rankCache.ContainsPlayer(target))
					{
						info.ReplyToCommand($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.general.targetloading", target.PlayerName]}");
						continue;
					}

					RankData playerData = rankCache[target];

					playerData.RoundPoints += parsedInt;
					playerData.Points += parsedInt;

					SavePlayerRankCache(target, false);

					Server.PrintToChatAll($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.ranks.givepoints", playerName, parsedInt, target.PlayerName]}");
				}
			});

			plugin.AddCommand("css_removepoints", "Remove points from the targeted player",
				[CommandHelper(2, "<target> <amount>", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)][RequiresPermissions("@k4system/admin")] (player, info) =>
			{
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
					if (target.IsBot || target.IsHLTV)
					{
						info.ReplyToCommand($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.general.targetnobot", target.PlayerName]}");
						continue;
					}

					if (!AdminManager.CanPlayerTarget(player, target))
					{
						info.ReplyToCommand($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.general.targetimmunity", target.PlayerName]}");
						continue;
					}

					if (!rankCache.ContainsPlayer(target))
					{
						info.ReplyToCommand($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.general.targetloading", target.PlayerName]}");
						continue;
					}

					RankData playerData = rankCache[target];

					playerData.RoundPoints -= parsedInt;
					playerData.Points -= parsedInt;

					SavePlayerRankCache(target, false);

					Server.PrintToChatAll($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.ranks.removepoints", playerName, parsedInt, target.PlayerName]}");
				}
			});
		}
	}
}