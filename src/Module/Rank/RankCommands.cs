namespace K4System
{
	using System.Text.RegularExpressions;
	using CounterStrikeSharp.API;
	using CounterStrikeSharp.API.Core;
	using CounterStrikeSharp.API.Modules.Admin;
	using CounterStrikeSharp.API.Modules.Commands;
	using CounterStrikeSharp.API.Modules.Utils;

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
						info.ReplyToCommand($" {Config.GeneralSettings.Prefix} Your data is not yet loaded. Please try again later...");
						return;
					}

					CCSPlayerController savedPlayer = player;
					string steamID = savedPlayer.SteamID.ToString();
					string name = savedPlayer.PlayerName;

					using (var syncContext = new SyncContextScope())
					{
						Server.NextFrame(async () =>
						{
							(int playerPlace, int totalPlayers) = await GetPlayerPlaceAndCount(steamID);

							RankData playerData = rankCache[savedPlayer];

							int higherRanksCount = rankDictionary.Count(kv => kv.Value.Point > playerData.Points);

							info.ReplyToCommand($" {Config.GeneralSettings.Prefix} {ChatColors.Lime}{name}'s Rank:");
							info.ReplyToCommand($"--- {ChatColors.Silver}You have {ChatColors.Lime}{playerData.Points} {ChatColors.Silver}points and are currently {playerData.Rank.Color}{playerData.Rank.Name} {ChatColors.Silver}({rankDictionary.Count - higherRanksCount} out of {rankDictionary.Count})");

							var nextRankEntry = rankDictionary
									.Where(kv => kv.Value.Point > playerData.Rank.Point)
									.OrderBy(kv => kv.Value.Point)
									.FirstOrDefault();

							if (nextRankEntry.Value != null)
							{
								Rank nextRank = nextRankEntry.Value;

								info.ReplyToCommand($"--- {ChatColors.Silver}Next rank: {nextRank.Color}{nextRank.Name}");
								info.ReplyToCommand($"--- {ChatColors.Silver}Points until next rank: {ChatColors.Lime}{nextRank.Point - playerData.Points}");
							}

							info.ReplyToCommand($"--- {ChatColors.Silver}Place in top list: {ChatColors.Lime}{playerPlace} out of {totalPlayers}");
						});
					}
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
						info.ReplyToCommand($" {Config.GeneralSettings.Prefix} Your data is not yet loaded. Please try again later...");
						return;
					}

					RankData playerData = rankCache[player];

					playerData.RoundPoints -= playerData.Points;
					playerData.Points = 0;

					_ = SavePlayerRankCache(player, false);

					Server.PrintToChatAll($" {Config.GeneralSettings.Prefix} {ChatColors.Lime}{player!.PlayerName} {ChatColors.Silver}has reset their rank and points.");
				});
			});

			commands.TopCommands.ForEach(commandString =>
			{
				plugin.AddCommand($"css_{commandString}", "Check the top 5 players by points",
					[CommandHelper(0, whoCanExecute: CommandUsage.CLIENT_ONLY)] (player, info) =>
				{
					if (player == null || !player.IsValid || player.PlayerPawn.Value == null)
						return;

					if (!rankCache.ContainsPlayer(player))
					{
						info.ReplyToCommand($" {Config.GeneralSettings.Prefix} Your data is not yet loaded. Please try again later...");
						return;
					}

					int printCount = int.TryParse(new string(info.ArgByIndex(0).Reverse().TakeWhile(char.IsDigit).Reverse().ToArray()), out int result) ? result : 5;

					CCSPlayerController savedPlayer = player;

					using (var syncContext = new SyncContextScope())
					{
						Server.NextFrame(async () =>
						{
							await PrintTopXPlayers(savedPlayer, printCount);
						});
					}
				});
			});

			plugin.AddCommand("css_resetrank", "Resets the targeted player's points to zero",
				[CommandHelper(1, "<SteamID64>", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)][RequiresPermissions("@k4system/admin")] (player, info) =>
			{
				string playerName = player != null && player.IsValid && player.PlayerPawn.Value != null ? player.PlayerName : "SERVER";

				List<CCSPlayerController> players = Utilities.GetPlayers();
				foreach (CCSPlayerController target in players)
				{
					if (target.IsBot || target.IsHLTV)
						continue;

					if (target.SteamID.ToString() != Regex.Replace(info.ArgByIndex(1), @"['"",\s]", ""))
						continue;

					if (!rankCache.ContainsPlayer(target))
					{
						info.ReplyToCommand($" {Config.GeneralSettings.Prefix} The player's data is not yet loaded. Please try again later...");
						return;
					}

					RankData playerData = rankCache[target];

					playerData.RoundPoints -= playerData.Points;
					playerData.Points = 0;

					_ = SavePlayerRankCache(target, false);

					Server.PrintToChatAll($" {Config.GeneralSettings.Prefix} {ChatColors.Lime}{target.PlayerName}{ChatColors.Silver}'s rank and points have been reset by {ChatColors.Lime}{playerName}{ChatColors.Silver}.");

					return;
				}
			});

			plugin.AddCommand("css_setpoints", "Resets the targeted player's points to zero",
				[CommandHelper(2, "<SteamID64> <amount>", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)][RequiresPermissions("@k4system/admin")] (player, info) =>
			{
				string playerName = player != null && player.IsValid && player.PlayerPawn.Value != null ? player.PlayerName : "SERVER";

				if (!int.TryParse(info.ArgByIndex(2), out int parsedInt))
				{
					info.ReplyToCommand($" {Config.GeneralSettings.Prefix} {ChatColors.Red}The given amount is invalid.");
					return;
				}

				List<CCSPlayerController> players = Utilities.GetPlayers();
				foreach (CCSPlayerController target in players)
				{
					if (target.IsBot || target.IsHLTV)
						continue;

					if (target.SteamID.ToString() != Regex.Replace(info.ArgByIndex(1), @"['"",\s]", ""))
						continue;

					if (!rankCache.ContainsPlayer(target))
					{
						info.ReplyToCommand($" {Config.GeneralSettings.Prefix} The player's data is not yet loaded. Please try again later...");
						return;
					}

					RankData playerData = rankCache[target];

					playerData.RoundPoints = parsedInt;
					playerData.Points = 0;

					_ = SavePlayerRankCache(target, false);

					Server.PrintToChatAll($" {Config.GeneralSettings.Prefix} {ChatColors.Lime}{target.PlayerName}{ChatColors.Silver}'s points has been set to {ChatColors.Lime}{parsedInt} {ChatColors.Silver}by {ChatColors.Lime}{playerName}{ChatColors.Silver}.");

					return;
				}
			});

			plugin.AddCommand("css_givepoints", "Give points the targeted player",
				[CommandHelper(2, "<SteamID64> <amount>", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)][RequiresPermissions("@k4system/admin")] (player, info) =>
			{
				string playerName = player != null && player.IsValid && player.PlayerPawn.Value != null ? player.PlayerName : "SERVER";

				Server.PrintToChatAll(info.ArgByIndex(2));

				if (!int.TryParse(info.ArgByIndex(2), out int parsedInt))
				{
					info.ReplyToCommand($" {Config.GeneralSettings.Prefix} {ChatColors.Red}The given amount is invalid.");
					return;
				}

				List<CCSPlayerController> players = Utilities.GetPlayers();
				foreach (CCSPlayerController target in players)
				{
					if (target.IsBot || target.IsHLTV)
						continue;

					if (target.SteamID.ToString() != Regex.Replace(info.ArgByIndex(1), @"['"",\s]", ""))
						continue;

					if (!rankCache.ContainsPlayer(target))
					{
						info.ReplyToCommand($" {Config.GeneralSettings.Prefix} The player's data is not yet loaded. Please try again later...");
						return;
					}

					RankData playerData = rankCache[target];

					playerData.RoundPoints += parsedInt;
					playerData.Points += parsedInt;

					_ = SavePlayerRankCache(target, false);

					Server.PrintToChatAll($" {Config.GeneralSettings.Prefix} {ChatColors.Lime}{playerName} {ChatColors.Silver}has given {ChatColors.Lime}{parsedInt} {ChatColors.Silver}points to {ChatColors.Lime}{target.PlayerName}{ChatColors.Silver}.");

					return;
				}
			});

			plugin.AddCommand("css_removepoints", "Remove points from the targeted player",
				[CommandHelper(2, "<SteamID64> <amount>", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)][RequiresPermissions("@k4system/admin")] (player, info) =>
			{
				string playerName = player != null && player.IsValid && player.PlayerPawn.Value != null ? player.PlayerName : "SERVER";

				if (!int.TryParse(info.ArgByIndex(2), out int parsedInt))
				{
					info.ReplyToCommand($" {Config.GeneralSettings.Prefix} {ChatColors.Red}The given amount is invalid.");
					return;
				}

				List<CCSPlayerController> players = Utilities.GetPlayers();
				foreach (CCSPlayerController target in players)
				{
					if (target.IsBot || target.IsHLTV)
						continue;

					if (target.SteamID.ToString() != Regex.Replace(info.ArgByIndex(1), @"['"",\s]", ""))
						continue;

					if (!rankCache.ContainsPlayer(target))
					{
						info.ReplyToCommand($" {Config.GeneralSettings.Prefix} The player's data is not yet loaded. Please try again later...");
						return;
					}

					RankData playerData = rankCache[target];

					playerData.RoundPoints -= parsedInt;
					playerData.Points -= parsedInt;

					_ = SavePlayerRankCache(target, false);

					Server.PrintToChatAll($" {Config.GeneralSettings.Prefix} {ChatColors.Lime}{playerName} {ChatColors.Silver}has removed {ChatColors.Lime}{parsedInt} {ChatColors.Silver}points from {ChatColors.Lime}{target.PlayerName}{ChatColors.Silver}.");

					return;
				}
			});
		}
	}
}