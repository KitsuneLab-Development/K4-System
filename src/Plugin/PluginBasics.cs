namespace K4System
{
	using System.Text;

	using CounterStrikeSharp.API;
	using CounterStrikeSharp.API.Core;
	using CounterStrikeSharp.API.Modules.Commands;
	using CounterStrikeSharp.API.Modules.Utils;
	using Microsoft.Extensions.Logging;

	public sealed partial class Plugin : BasePlugin
	{
		public Timer? databasePoolingTimer;

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
		}

		public void Initialize_Events()
		{
			RegisterEventHandler((EventPlayerConnectFull @event, GameEventInfo info) =>
			{
				CCSPlayerController player = @event.Userid;

				if (player is null || !player.IsValid || !player.PlayerPawn.IsValid || player.IsBot || player.IsHLTV)
					return HookResult.Continue;

				if (PlayerCache.Instance.ContainsPlayer(player))
					return HookResult.Continue;

				LoadPlayerCache(player);

				return HookResult.Continue;
			});

			RegisterEventHandler((EventPlayerDisconnect @event, GameEventInfo info) =>
			{
				CCSPlayerController player = @event.Userid;

				if (player is null || !player.IsValid || !player.PlayerPawn.IsValid)
					return HookResult.Continue;

				if (player.IsBot || player.IsHLTV)
					return HookResult.Continue;

				if (!PlayerCache.Instance.ContainsPlayer(player))
					return HookResult.Continue;

				if (Config.GeneralSettings.ModuleTimes)
					ModuleTime.BeforeDisconnect(player);

				// Do not save cache for each player on mapchange, because it's handled by an optimised query for all players
				if (@event.Reason != 1)
				{
					SavePlayerCache(player, true);
				}

				return HookResult.Continue;
			});


			RegisterEventHandler((EventRoundStart @event, GameEventInfo info) =>
			{
				if (!Config.GeneralSettings.SpawnMessage)
					return HookResult.Continue;

				List<CCSPlayerController> players = Utilities.GetPlayers();

				foreach (CCSPlayerController player in players)
				{
					if (player is null || !player.IsValid || !player.PlayerPawn.IsValid)
						continue;

					if (player.IsBot || player.IsHLTV)
						continue;

					if (player.SteamID.ToString().Length != 17)
						continue;

					player.PrintToChat($" {Localizer["k4.general.prefix"]} {ChatColors.Lime}{Localizer["k4.general.spawnmessage"]}");
				}

				return HookResult.Continue;
			});

			RegisterEventHandler((EventRoundEnd @event, GameEventInfo info) =>
			{
				if (Config.GeneralSettings.ModuleStats)
					ModuleStat.BeforeRoundEnd(@event.Winner);

				if (Config.GeneralSettings.ModuleRanks)
					ModuleRank.BeforeRoundEnd(@event.Winner);

				SaveAllPlayersCache();

				return HookResult.Continue;
			});

			RegisterListener<Listeners.OnMapStart>((mapName) =>
			{
				AddTimer(1.0f, () =>
				{
					Database.Instance.AdjustDatabasePooling();
					GameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").First().GameRules;
				});
			});

			RegisterListener<Listeners.OnMapEnd>(() =>
			{
				GameRules = null;
			});
		}
	}
}