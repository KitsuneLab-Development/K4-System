namespace K4System
{
	using System.Text;

	using CounterStrikeSharp.API;
	using CounterStrikeSharp.API.Core;
	using CounterStrikeSharp.API.Modules.Admin;
	using CounterStrikeSharp.API.Modules.Commands;
	using CounterStrikeSharp.API.Modules.Commands.Targeting;
	using CounterStrikeSharp.API.Modules.Utils;
	using K4System.Models;
	using static K4System.ModuleRank;
	using static K4System.ModuleStat;
	using static K4System.ModuleTime;

	public sealed partial class Plugin : BasePlugin
	{
		DateTime lastRoundStartEventTime = DateTime.MinValue;

		public void Initialize_Commands()
		{
			AddCommand("css_k4", "More informations about K4-System",
				[CommandHelper(0, whoCanExecute: CommandUsage.CLIENT_ONLY)] (player, info) =>
			{
				if (player == null || !player.IsValid || player.PlayerPawn.Value == null)
					return;

				info.ReplyToCommand($" {Localizer["k4.general.prefix"]} {ChatColors.Lime}{Localizer["k4.general.availablecommands"]}");

				CommandSettings commands = Config.CommandSettings;

				string rankLocale = Localizer["k4.general.availablecommands.rank"];
				string otherLocale = Localizer["k4.general.availablecommands.other"];

				Dictionary<string, List<string>> commandCategories = new Dictionary<string, List<string>>
				{
					{ rankLocale, new List<string>() },
					{ otherLocale, new List<string>() },
					{ Localizer["k4.general.availablecommands.stat"], commands.StatCommands },
					{ Localizer["k4.general.availablecommands.time"], commands.TimeCommands },
				};

				commandCategories[rankLocale].AddRange(commands.RankCommands);
				commandCategories[rankLocale].AddRange(commands.TopCommands);
				commandCategories[rankLocale].AddRange(commands.ResetMyCommands);
				commandCategories[rankLocale].AddRange(commands.RanksCommands);

				commandCategories[otherLocale].AddRange(commands.AdminListCommands);

				StringBuilder messageBuilder = new StringBuilder();

				foreach (var category in commandCategories)
				{
					if (category.Value.Count > 0)
					{
						messageBuilder.AppendLine($"--- {ChatColors.Silver}{category.Key}: {ChatColors.Lime}{category.Value[0]}{ChatColors.Silver}");

						for (int i = 1; i < category.Value.Count; i++)
						{
							if (i > 0 && i % 6 == 0)
							{
								info.ReplyToCommand(messageBuilder.ToString());
								messageBuilder.Clear();
							}

							if (messageBuilder.Length > 0)
							{
								messageBuilder.Append($"{ChatColors.Silver}, ");
							}

							messageBuilder.Append($" {ChatColors.Lime}{category.Value[i]}");
						}

						if (messageBuilder.Length > 0)
						{
							info.ReplyToCommand(messageBuilder.ToString());
							messageBuilder.Clear();
						}
					}
				}
			});

			Config.CommandSettings.ResetMyCommands.ForEach(commandString =>
			{
				AddCommand($"css_{commandString}", "Resets the player's own points to zero", CallbackAnonymizer(OnCommandResetMyData));
			});

			AddCommand("css_resetdata", "Resets the targeted player's data to zero", CallbackAnonymizer(OnCommandResetData));
		}

		public void Initialize_Events()
		{
			RegisterEventHandler((EventPlayerActivate @event, GameEventInfo info) =>
			{
				CCSPlayerController player = @event.Userid;

				if (player is null || !player.IsValid || !player.PlayerPawn.IsValid || player.IsHLTV)
					return HookResult.Continue;

				// Do not load the data, if the user is in the cache already
				// This free up some resources and prevent plugin to load the same data twice
				if (K4Players.Any(p => p.Controller == player))
					return HookResult.Continue;

				K4Player k4player = new K4Player(this, player);

				if (player.IsBot)
				{
					K4Players.Add(k4player);
					return HookResult.Continue;
				}

				Task.Run(() => LoadPlayerCacheAsync(k4player));
				return HookResult.Continue;
			});

			RegisterEventHandler((EventPlayerDisconnect @event, GameEventInfo info) =>
			{
				K4Player? k4player = GetK4Player(@event.Userid);

				if (k4player is null || !k4player.IsValid || !k4player.IsPlayer)
					return HookResult.Continue;

				if (Config.GeneralSettings.ModuleTimes)
					ModuleTime.BeforeDisconnect(k4player);

				// Do not save cache for each player on mapchange, because it's handled by an optimised query for all players
				if (@event.Reason != 1)
				{
					Task.Run(() => SavePlayerDataAsync(k4player, true));
				}

				return HookResult.Continue;
			});


			RegisterEventHandler((EventRoundStart @event, GameEventInfo info) =>
			{
				if (!Config.GeneralSettings.SpawnMessage)
					return HookResult.Continue;

				// Check if the event was fired within the last 3 seconds
				// This fixes the duplicated round start being fired by the game
				if ((DateTime.Now - lastRoundStartEventTime).TotalSeconds < 3)
					return HookResult.Continue;

				lastRoundStartEventTime = DateTime.Now;

				foreach (K4Player k4Player in K4Players.ToList())
				{
					if (!k4Player.IsValid || !k4Player.IsPlayer)
						continue;

					k4Player.Controller.PrintToChat($" {Localizer["k4.general.prefix"]} {ChatColors.Lime}{Localizer["k4.general.spawnmessage"]}");
				}

				return HookResult.Continue;
			});

			RegisterEventHandler((EventRoundEnd @event, GameEventInfo info) =>
			{
				if (Config.GeneralSettings.ModuleStats)
					ModuleStat.BeforeRoundEnd(@event.Winner);

				if (Config.GeneralSettings.ModuleRanks)
					ModuleRank.BeforeRoundEnd(@event.Winner);

				Task.Run(SaveAllPlayersDataAsync);
				return HookResult.Continue;
			}, HookMode.Post);

			RegisterListener<Listeners.OnMapStart>((mapName) =>
			{
				AddTimer(1.0f, () =>
				{
					GameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").First().GameRules;
				});
			});

			RegisterListener<Listeners.OnMapEnd>(() =>
			{
				GameRules = null;
				Task.Run(PurgeTableRowsAsync);
			});
		}

		public void OnCommandResetMyData(CCSPlayerController? player, CommandInfo info)
		{
			if (!CommandHelper(player, info, CommandUsage.CLIENT_ONLY, 1, "[all|time|stat|rank]"))
				return;

			string resetTarget = info.ArgByIndex(1);

			if (resetTarget != "all" && resetTarget != "time" && resetTarget != "stat" && resetTarget != "rank")
			{
				info.ReplyToCommand($" {Localizer["k4.general.prefix"]} {Localizer["k4.general.commandhelp", info.ArgByIndex(0), "[all|time|stat|rank]"]}");
				return;
			}

			K4Player? k4player = GetK4Player(player!);

			if (k4player is null)
			{
				info.ReplyToCommand($" {Localizer["k4.general.prefix"]} {Localizer["k4.general.loading"]}");
				return;
			}

			if (resetTarget == "all" || resetTarget == "time")
			{
				TimeData? playerData = k4player.timeData;

				if (playerData is null)
					return;

				foreach (var field in playerData.TimeFields.Keys.ToList())
				{
					playerData.TimeFields[field] = 0;
				}
			}

			if (resetTarget == "all" || resetTarget == "rank")
			{
				RankData? playerData = k4player.rankData;

				if (playerData is null)
					return;

				playerData.RoundPoints -= playerData.Points;
				playerData.Points = Config.RankSettings.StartPoints;
				playerData.Rank = ModuleRank.GetNoneRank();
			}

			if (resetTarget == "all" || resetTarget == "stat")
			{
				StatData? playerData = k4player.statData;

				if (playerData is null)
					return;

				foreach (var field in playerData.StatFields.Keys.ToList())
				{
					playerData.StatFields[field] = 0;
				}
			}

			Server.PrintToChatAll($" {Localizer["k4.general.prefix"]} {Localizer["k4.ranks.resetmydata", player!.PlayerName]}");

			Task.Run(() => SavePlayerDataAsync(k4player, false));
		}

		public void OnCommandResetData(CCSPlayerController? player, CommandInfo info)
		{
			if (!CommandHelper(player, info, CommandUsage.CLIENT_AND_SERVER, 2, "<target> [all|time|stat|rank]", "@k4system/admin"))
				return;

			string resetTarget = info.ArgByIndex(1);

			if (resetTarget != "all" && resetTarget != "time" && resetTarget != "stat" && resetTarget != "rank")
			{
				info.ReplyToCommand($" {Localizer["k4.general.prefix"]} {Localizer["k4.general.commandhelp", info.ArgByIndex(0), "<target> [all|time|stat|rank]"]}");
				return;
			}

			string playerName = player != null && player.IsValid && player.PlayerPawn.Value != null ? player.PlayerName : "SERVER";

			TargetResult targetResult = info.GetArgTargetResult(1);

			if (!targetResult.Any())
			{
				info.ReplyToCommand($" {Localizer["k4.general.prefix"]} {Localizer["k4.general.targetnotfound"]}");
				return;
			}

			foreach (CCSPlayerController target in targetResult.Players)
			{
				K4Player? k4player = GetK4Player(target);

				if (k4player is null || !k4player.IsValid)
				{
					info.ReplyToCommand($" {Localizer["k4.general.prefix"]} {Localizer["k4.general.targetloading", target.PlayerName]}");
					continue;
				}

				if (!k4player.IsPlayer)
				{
					info.ReplyToCommand($" {Localizer["k4.general.prefix"]} {Localizer["k4.general.targetnobot", target.PlayerName]}");
					continue;
				}

				if (!AdminManager.CanPlayerTarget(player, target))
				{
					info.ReplyToCommand($" {Localizer["k4.general.prefix"]} {Localizer["k4.general.targetimmunity", target.PlayerName]}");
					continue;
				}

				if (resetTarget == "all" || resetTarget == "time")
				{
					TimeData? playerData = k4player.timeData;

					if (playerData is null)
						return;

					foreach (var field in playerData.TimeFields.Keys.ToList())
					{
						playerData.TimeFields[field] = 0;
					}
				}

				if (resetTarget == "all" || resetTarget == "rank")
				{
					RankData? playerData = k4player.rankData;

					if (playerData is null)
						return;

					playerData.RoundPoints -= playerData.Points;
					playerData.Points = Config.RankSettings.StartPoints;
					playerData.Rank = ModuleRank.GetNoneRank();
				}

				if (resetTarget == "all" || resetTarget == "stat")
				{
					StatData? playerData = k4player.statData;

					if (playerData is null)
						return;

					foreach (var field in playerData.StatFields.Keys.ToList())
					{
						playerData.StatFields[field] = 0;
					}
				}

				if (playerName != "SERVER")
					Server.PrintToChatAll($" {Localizer["k4.general.prefix"]} {Localizer["k4.ranks.resetdata", target.PlayerName, playerName]}");

				Task.Run(() => SavePlayerDataAsync(k4player, false));
			}
		}
	}
}