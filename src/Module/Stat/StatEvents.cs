
namespace K4System
{
	using CounterStrikeSharp.API;
	using CounterStrikeSharp.API.Core;
	using CounterStrikeSharp.API.Modules.Utils;
	using Microsoft.Extensions.Logging;

	public partial class ModuleStat : IModuleStat
	{
		public void Initialize_Events(Plugin plugin)
		{
			plugin.RegisterEventHandler((EventPlayerConnectFull @event, GameEventInfo info) =>
			{
				CCSPlayerController player = @event.Userid;

				if (player is null || !player.IsValid || player.IsBot || player.IsHLTV)
					return HookResult.Continue;

				int slot = player.Slot;
				string playerName = player.PlayerName;
				string steamId = player.SteamID.ToString();

				Task.Run(async () =>
				{
					await LoadStatData(slot, playerName, steamId);
				});

				return HookResult.Continue;
			});

			plugin.RegisterListener<Listeners.OnMapStart>((mapName) =>
			{
				plugin.AddTimer(1.0f, () =>
				{
					globalGameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").First().GameRules;
				});
			});

			plugin.RegisterEventHandler((EventPlayerDeath @event, GameEventInfo info) =>
			{
				if (!IsStatsAllowed())
				{
					return HookResult.Continue;
				}

				CCSPlayerController victim = @event.Userid;

				if (victim.IsBot && !Config.StatisticSettings.StatsForBots)
					return HookResult.Continue;

				if (victim != null && victim.IsValid && victim.PlayerPawn.IsValid && victim.UserId.HasValue && victim.UserId != -1 && !victim.IsBot)
				{
					ModifyPlayerStats(victim, "deaths", 1);
				}

				CCSPlayerController attacker = @event.Attacker;

				if (attacker != null && attacker.IsValid && attacker.PlayerPawn.IsValid && !attacker.IsBot && !attacker.IsHLTV)
				{
					ModifyPlayerStats(attacker, "kills", 1);

					if (!FirstBlood)
					{
						FirstBlood = true;
						ModifyPlayerStats(attacker, "firstblood", 1);
					}
				}

				CCSPlayerController assister = @event.Assister;

				if (assister != null && assister.IsValid && assister.PlayerPawn.IsValid && !assister.IsBot && !assister.IsHLTV)
				{
					ModifyPlayerStats(assister, "assists", 1);
				}

				return HookResult.Continue;
			});

			plugin.RegisterEventHandler((EventGrenadeThrown @event, GameEventInfo info) =>
			{
				if (!IsStatsAllowed())
				{
					return HookResult.Continue;
				}

				CCSPlayerController player = @event.Userid;

				if (player != null && player.IsValid && player.PlayerPawn.IsValid && !player.IsBot && !player.IsHLTV)
				{
					ModifyPlayerStats(player, "grenades", 1);
				}

				return HookResult.Continue;
			});

			plugin.RegisterEventHandler((EventPlayerHurt @event, GameEventInfo info) =>
			{
				if (!IsStatsAllowed())
				{
					return HookResult.Continue;
				}

				CCSPlayerController victim = @event.Userid;

				if (victim != null && victim.IsValid && victim.PlayerPawn.IsValid && victim.UserId.HasValue && victim.UserId != -1 && !victim.IsBot && !victim.IsHLTV)
				{
					ModifyPlayerStats(victim, "hits_taken", 1);
				}

				CCSPlayerController attacker = @event.Attacker;

				if (attacker != null && attacker.IsValid && attacker.PlayerPawn.IsValid && !attacker.IsBot && !attacker.IsHLTV)
				{
					ModifyPlayerStats(attacker, "hits_given", 1);

					if (@event.Hitgroup == 1)
					{
						ModifyPlayerStats(attacker, "headshots", 1);
					}
				}

				return HookResult.Continue;
			});

			plugin.RegisterEventHandler((EventRoundStart @event, GameEventInfo info) =>
			{
				FirstBlood = false;
				return HookResult.Continue;
			});

			plugin.RegisterEventHandler((EventRoundEnd @event, GameEventInfo info) =>
			{
				if (!IsStatsAllowed())
				{
					return HookResult.Continue;
				}

				int winnerTeam = @event.Winner;

				if (winnerTeam > (int)CsTeam.Spectator)
				{
					List<CCSPlayerController> players = Utilities.GetPlayers();

					foreach (CCSPlayerController player in players)
					{
						if (player is null || !player.IsValid || !player.PlayerPawn.IsValid)
							continue;

						if (player.IsBot || player.IsHLTV)
							continue;

						if (player.TeamNum <= (int)CsTeam.Spectator)
							continue;

						ModifyPlayerStats(player, player.TeamNum == winnerTeam ? "round_win" : "round_lose", 1);
					}
				}

				SaveAllPlayerCache(false);

				return HookResult.Continue;
			});

			plugin.RegisterEventHandler((EventWeaponFire @event, GameEventInfo info) =>
			{
				if (!IsStatsAllowed())
				{
					return HookResult.Continue;
				}

				CCSPlayerController player = @event.Userid;

				if (player is null || !player.IsValid || !player.PlayerPawn.IsValid)
					return HookResult.Continue;

				if (player.IsBot || player.IsHLTV)
					return HookResult.Continue;
				
				if(@event.Weapon.Contains("knife") || @event.Weapon.Contains("bayonet"))
				{
					return HookResult.Continue;
				}

				ModifyPlayerStats(player, "shoots", 1);
				return HookResult.Continue;
			});

			plugin.RegisterEventHandler((EventRoundMvp @event, GameEventInfo info) =>
			{
				if (!IsStatsAllowed())
				{
					return HookResult.Continue;
				}

				CCSPlayerController player = @event.Userid;

				if (player is null || !player.IsValid || !player.PlayerPawn.IsValid)
					return HookResult.Continue;

				if (player.IsBot || player.IsHLTV)
					return HookResult.Continue;

				ModifyPlayerStats(player, "mvp", 1);
				return HookResult.Continue;
			});

			plugin.RegisterEventHandler((EventPlayerDisconnect @event, GameEventInfo info) =>
			{
				CCSPlayerController player = @event.Userid;

				if (player is null || !player.IsValid || !player.PlayerPawn.IsValid)
					return HookResult.Continue;

				if (player.IsBot || player.IsHLTV)
					return HookResult.Continue;

				if (!statCache.ContainsPlayer(player))
					return HookResult.Continue;

				SavePlayerStatCache(player, true);

				return HookResult.Continue;
			});

			plugin.RegisterEventHandler<EventCsWinPanelMatch>((@event, info) =>
			{
				if (!IsStatsAllowed())
				{
					return HookResult.Continue;
				}

				if (Config.GeneralSettings.FFAMode)
				{
					List<CCSPlayerController> players = Utilities.GetPlayers();

					CCSPlayerController? player = players.OrderByDescending(p => p?.Score).FirstOrDefault();

					if (player != null && player.IsValid && player.PlayerPawn.IsValid && !player.IsBot && !player.IsHLTV)
					{
						ModifyPlayerStats(player, "game_win", 1);
					}

					foreach (CCSPlayerController otherPlayer in players.Where(p => p != player && p != null && p.IsValid && p.PlayerPawn.IsValid && !p.IsBot && !p.IsHLTV))
					{
						ModifyPlayerStats(otherPlayer, "game_lose", 1);
					}
				}
				else
				{
					int ctScore = Utilities.FindAllEntitiesByDesignerName<CCSTeam>("cs_team_manager")
						.Where(team => team.Teamname == "CT")
						.Select(team => team.Score)
						.FirstOrDefault();

					int tScore = Utilities.FindAllEntitiesByDesignerName<CCSTeam>("cs_team_manager")
						.Where(team => team.Teamname == "TERRORIST")
						.Select(team => team.Score)
						.FirstOrDefault();

					CsTeam winnerTeam = ctScore > tScore ? CsTeam.CounterTerrorist : tScore > ctScore ? CsTeam.Terrorist : CsTeam.None;

					if ((int)winnerTeam > (int)CsTeam.Spectator)
					{
						List<CCSPlayerController> players = Utilities.GetPlayers();

						foreach (CCSPlayerController player in players.Where(p => p != null && p.IsValid && p.PlayerPawn.IsValid && !p.IsBot && !p.IsHLTV))
						{
							CsTeam playerTeam = (CsTeam)player.TeamNum;

							if (playerTeam > CsTeam.Spectator)
							{
								ModifyPlayerStats(player, playerTeam == winnerTeam ? "game_win" : "game_lose", 1);
							}
						}
					}
				}

				SaveAllPlayerCache(false);

				return HookResult.Continue;
			});
		}
	}
}
