using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Utils;

namespace K4ryuuSystem
{
	public partial class K4System
	{
		private void SetupGameEvents()
		{
			RegisterListener<Listeners.OnMapEnd>(() =>
			{
				List<CCSPlayerController> players = Utilities.GetPlayers();
				foreach (CCSPlayerController player in players)
				{
					if (player.IsBot)
						continue;

					if (PlayerSummaries.ContainsPlayer(player!))
					{
						if (Config.GeneralSettings.ModuleTimes)
							SaveClientTime(player);

						if (Config.GeneralSettings.ModuleStats)
							SaveClientStats(player);

						if (Config.GeneralSettings.ModuleRanks)
							SaveClientRank(player);
					}
				}
			});

			RegisterEventHandler<EventPlayerDisconnect>((@event, info) =>
			{
				CCSPlayerController player = @event.Userid;

				if (!player.IsValidPlayer())
					return HookResult.Continue;

				if (Config.GeneralSettings.ModuleTimes)
					SaveClientTime(player);

				if (Config.GeneralSettings.ModuleStats)
					SaveClientStats(player);

				if (Config.GeneralSettings.ModuleRanks)
					SaveClientRank(player);

				PlayerSummaries.RemovePlayer(player);

				return HookResult.Continue;
			});
			RegisterEventHandler<EventPlayerConnectFull>((@event, info) =>
			{
				CCSPlayerController player = @event.Userid;

				if (Config.GeneralSettings.ModuleRanks)
					playerKillStreaks[player.UserId ?? 0] = (0, DateTime.MinValue);

				if (player.IsValidPlayer())
					LoadPlayerData(player);

				return HookResult.Continue;
			});
			RegisterEventHandler<EventHostageRescued>((@event, info) =>
			{
				ModifyClientPoints(@event.Userid, CHANGE_MODE.GIVE, Config.PointSettings.HostageRescue, "Hostage Rescued");
				return HookResult.Continue;
			});
			RegisterEventHandler<EventHostageKilled>((@event, info) =>
			{
				ModifyClientPoints(@event.Userid, CHANGE_MODE.REMOVE, Config.PointSettings.HostageKill, "Hostage Killed");
				return HookResult.Continue;
			});
			RegisterEventHandler<EventHostageHurt>((@event, info) =>
			{
				ModifyClientPoints(@event.Userid, CHANGE_MODE.REMOVE, Config.PointSettings.HostageHurt, "Hostage Hurt");
				return HookResult.Continue;
			});
			RegisterEventHandler<EventBombDropped>((@event, info) =>
			{
				ModifyClientPoints(@event.Userid, CHANGE_MODE.REMOVE, Config.PointSettings.BombDrop, "Bomb Dropped");
				return HookResult.Continue;
			});
			RegisterEventHandler<EventBombPickup>((@event, info) =>
			{
				ModifyClientPoints(@event.Userid, CHANGE_MODE.GIVE, Config.PointSettings.BombPickup, "Bomb Pickup");
				return HookResult.Continue;
			});
			RegisterEventHandler<EventBombDefused>((@event, info) =>
			{
				ModifyClientPoints(@event.Userid, CHANGE_MODE.GIVE, Config.PointSettings.Defuse, "Bomb Defused");
				return HookResult.Continue;
			});
			RegisterEventHandler<EventPlayerTeam>((@event, info) =>
			{
				if (Config.GeneralSettings.ModuleTimes)
					return HookResult.Continue;

				CCSPlayerController player = @event.Userid;
				if (player == null || !player.IsValid || player.IsBot || @event.Oldteam == @event.Team)
					return HookResult.Continue;

				if (!PlayerSummaries.ContainsPlayer(player!))
					LoadPlayerData(player!);

				DateTime now = DateTime.UtcNow;
				double seconds = (now - PlayerSummaries[player].Times["Team"]).TotalSeconds;

				PlayerSummaries[player].TimeFields[GetFieldForTeam((CsTeam)@event.Oldteam)] += (int)seconds;

				PlayerSummaries[player].Times["Team"] = now;

				return HookResult.Continue;
			});
			RegisterEventHandler<EventRoundMvp>((@event, info) =>
			{
				CCSPlayerController player = @event.Userid;
				if (player == null || !player.IsValid || player.IsBot)
					return HookResult.Continue;

				if (IsStatsAllowed())
					PlayerSummaries[player].StatFields["mvp"]++;

				if (Config.GeneralSettings.ModuleRanks)
					ModifyClientPoints(@event.Userid, CHANGE_MODE.GIVE, Config.PointSettings.MVP, "Round MVP");

				return HookResult.Continue;
			});
			RegisterEventHandler<EventRoundStart>((@event, info) =>
			{
				if (!IsPointsAllowed())
					return HookResult.Continue;

				List<CCSPlayerController> players = Utilities.GetPlayers();
				foreach (CCSPlayerController player in players)
				{
					if (player.IsBot)
						continue;

					if (!PlayerSummaries.ContainsPlayer(player!))
						LoadPlayerData(player!);

					if (!Config.GeneralSettings.SpawnMessage || PlayerSummaries[player].SpawnedThisRound)
						continue;

					player.PrintToChat($" {Config.GeneralSettings.Prefix} {ChatColors.Green}The server is using the {ChatColors.Gold}K4-System {ChatColors.Green}plugin. Type {ChatColors.Red}!k4 {ChatColors.Green}to get more information!");

					PlayerSummaries[player].SpawnedThisRound = true;
				}

				return HookResult.Continue;
			});
			RegisterEventHandler<EventRoundEnd>((@event, info) =>
			{
				CsTeam winnerTeam = (CsTeam)@event.Winner;

				List<CCSPlayerController> players = Utilities.GetPlayers();
				foreach (CCSPlayerController player in players)
				{
					if (player.IsBot || !PlayerSummaries[player].SpawnedThisRound)
						continue;

					CsTeam playerTeam = (CsTeam)player.TeamNum;

					if (playerTeam != CsTeam.None && playerTeam != CsTeam.Spectator)
					{
						if (playerTeam == winnerTeam)
						{
							if (IsStatsAllowed())
								PlayerSummaries[player].StatFields["round_win"]++;

							if (IsPointsAllowed())
								ModifyClientPoints(player, CHANGE_MODE.GIVE, Config.PointSettings.RoundWin, "Round Win");
						}
						else
						{
							if (IsStatsAllowed())
								PlayerSummaries[player].StatFields["round_lose"]++;

							if (IsPointsAllowed())
								ModifyClientPoints(player, CHANGE_MODE.REMOVE, Config.PointSettings.RoundLose, "Round Lose");
						}
					}

					PlayerSummaries[player].SpawnedThisRound = false;
				}

				return HookResult.Continue;
			});
			RegisterEventHandler<EventPlayerSpawn>((@event, info) =>
			{
				if (!Config.GeneralSettings.ModuleTimes)
					return HookResult.Continue;

				CCSPlayerController player = @event.Userid;
				if (player == null || !player.IsValid || player.IsBot)
					return HookResult.Continue;

				if (!PlayerSummaries.ContainsPlayer(player))
					LoadPlayerData(player);

				var playerData = PlayerSummaries[player].Times;
				if (playerData != null && playerData.ContainsKey("Death"))
				{
					PlayerSummaries[player].TimeFields["dead"] += (int)(DateTime.UtcNow - playerData["Death"]).TotalSeconds;
				}

				PlayerSummaries[player].Times["Death"] = DateTime.UtcNow;

				return HookResult.Continue;
			});
			RegisterEventHandler<EventBombPlanted>((@event, info) =>
			{
				ModifyClientPoints(@event.Userid, CHANGE_MODE.GIVE, Config.PointSettings.Plant, "Bomb Plant");
				return HookResult.Continue;
			});
			RegisterEventHandler<EventGrenadeThrown>((@event, info) =>
			{
				CCSPlayerController player = @event.Userid;

				if (!player.IsValidPlayer())
					return HookResult.Continue;

				if (IsStatsAllowed())
					PlayerSummaries[player].StatFields["grenades"]++;

				return HookResult.Continue;
			});
			RegisterEventHandler<EventPlayerHurt>((@event, info) =>
			{
				CCSPlayerController player = @event.Attacker;

				if (!player.IsValidPlayer())
					return HookResult.Continue;

				if (IsStatsAllowed())
					PlayerSummaries[player].StatFields["hits"]++;

				return HookResult.Continue;
			});
			RegisterEventHandler<EventPlayerDeath>((@event, info) =>
			{
				CCSPlayerController victimController = @event.Userid;
				CCSPlayerController killerController = @event.Attacker;
				CCSPlayerController assisterController = @event.Assister;

				if (!victimController.IsValid || victimController.UserId <= 0)
					return HookResult.Continue;

				if (!victimController.IsBot && IsStatsAllowed() && (Config.StatisticSettings.StatsForBots || !killerController.IsBot))
				{
					PlayerSummaries[victimController].StatFields["deaths"]++;
				}

				if (!victimController.IsBot && (Config.RankSettings.PointsForBots || !killerController.IsBot) && IsPointsAllowed())
				{
					if (victimController.UserId == killerController.UserId)
					{
						ModifyClientPoints(victimController, CHANGE_MODE.REMOVE, Config.PointSettings.Suicide, "Suicide");
					}
					else
						ModifyClientPoints(victimController, CHANGE_MODE.REMOVE, Config.PointSettings.Death, "Dying");
				}

				if (victimController.UserId == killerController.UserId)
					return HookResult.Continue;

				if (killerController.IsValidPlayer())
				{
					if (IsStatsAllowed() && (Config.StatisticSettings.StatsForBots || !victimController.IsBot))
					{
						PlayerSummaries[killerController].StatFields["kills"]++;
					}

					if ((Config.RankSettings.PointsForBots || !victimController.IsBot) && IsPointsAllowed())
					{
						if (!Config.RankSettings.FFAMode && killerController.TeamNum == victimController.TeamNum)
						{
							ModifyClientPoints(killerController, CHANGE_MODE.REMOVE, Config.PointSettings.TeamKill, "TeamKill");
						}
						else
						{
							ModifyClientPoints(killerController, CHANGE_MODE.GIVE, Config.PointSettings.Kill, "Kill");

							if (@event.Headshot)
								ModifyClientPoints(killerController, CHANGE_MODE.GIVE, Config.PointSettings.Headshot, "Headshot");

							int penetrateCount = @event.Penetrated;
							if (penetrateCount > 0 && Config.PointSettings.Penetrated > 0)
							{
								int calculatedPoints = @event.Penetrated * Config.PointSettings.Penetrated;
								ModifyClientPoints(killerController, CHANGE_MODE.GIVE, calculatedPoints, "Penetrated");
							}

							if (@event.Noscope)
								ModifyClientPoints(killerController, CHANGE_MODE.GIVE, Config.PointSettings.NoScope, "NoScope");

							if (@event.Thrusmoke)
								ModifyClientPoints(killerController, CHANGE_MODE.GIVE, Config.PointSettings.Thrusmoke, "ThruSmoke");

							if (@event.Attackerblind)
								ModifyClientPoints(killerController, CHANGE_MODE.GIVE, Config.PointSettings.BlindKill, "Blind Kill");

							if (@event.Distance >= Config.PointSettings.LongDistance)
								ModifyClientPoints(killerController, CHANGE_MODE.GIVE, Config.PointSettings.LongDistanceKill, "Long Distance");

							string lowerCaseWeaponName = @event.Weapon.ToLower();

							switch (lowerCaseWeaponName)
							{
								case var _ when lowerCaseWeaponName.Contains("hegrenade") || lowerCaseWeaponName.Contains("tagrenade") || lowerCaseWeaponName.Contains("firebomb") || lowerCaseWeaponName.Contains("molotov") || lowerCaseWeaponName.Contains("incgrenade") || lowerCaseWeaponName.Contains("flashbang") || lowerCaseWeaponName.Contains("smokegrenade") || lowerCaseWeaponName.Contains("frag") || lowerCaseWeaponName.Contains("bumpmine"):
									{
										ModifyClientPoints(killerController, CHANGE_MODE.GIVE, Config.PointSettings.GrenadeKill, "Grenade Kill");
										break;
									}
								case var _ when lowerCaseWeaponName.Contains("cord") || lowerCaseWeaponName.Contains("bowie") || lowerCaseWeaponName.Contains("butterfly") || lowerCaseWeaponName.Contains("karambit") || lowerCaseWeaponName.Contains("skeleton") || lowerCaseWeaponName.Contains("m9_bayonet") || lowerCaseWeaponName.Contains("bayonet") || lowerCaseWeaponName.Contains("t") || lowerCaseWeaponName.Contains("knifegg") || lowerCaseWeaponName.Contains("stiletto") || lowerCaseWeaponName.Contains("ursus") || lowerCaseWeaponName.Contains("tactical") || lowerCaseWeaponName.Contains("push") || lowerCaseWeaponName.Contains("widowmaker") || lowerCaseWeaponName.Contains("outdoor") || lowerCaseWeaponName.Contains("canis"):
									{
										ModifyClientPoints(killerController, CHANGE_MODE.GIVE, Config.PointSettings.KnifeKill, "Knife Kill");
										break;
									}
								case "taser":
									{
										ModifyClientPoints(killerController, CHANGE_MODE.GIVE, Config.PointSettings.TaserKill, "Taser Kill");
										break;
									}
							}

							int attackerIndex = (int)killerController.UserId!;
							if (playerKillStreaks.ContainsKey(attackerIndex))
							{
								if (playerKillStreaks[attackerIndex].killStreak > 0 && DateTime.Now - playerKillStreaks[attackerIndex].lastKillTime <= TimeSpan.FromSeconds(Config.PointSettings.SecondsBetweenKills))
								{
									playerKillStreaks[attackerIndex] = (playerKillStreaks[attackerIndex].killStreak + 1, DateTime.Now);
									int killStreak = playerKillStreaks[attackerIndex].killStreak;

									Dictionary<int, (int points, string message)> killStreakMap = new Dictionary<int, (int points, string message)>
									{
										{ 2, (Config.PointSettings.DoubleKill, "Double Kill") },
										{ 3, (Config.PointSettings.TripleKill, "Triple Kill") },
										{ 4, (Config.PointSettings.Domination, "Domination") },
										{ 5, (Config.PointSettings.Rampage, "Rampage") },
										{ 6, (Config.PointSettings.MegaKill, "Mega Kill") },
										{ 7, (Config.PointSettings.Ownage, "Ownage") },
										{ 8, (Config.PointSettings.UltraKill, "Ultra Kill") },
										{ 9, (Config.PointSettings.KillingSpree, "Killing Spree") },
										{ 10, (Config.PointSettings.MonsterKill, "Monster Kill") },
										{ 11, (Config.PointSettings.Unstoppable, "Unstoppable") },
										{ 12, (Config.PointSettings.GodLike, "God Like") }
									};

									if (killStreakMap.TryGetValue(killStreak, out var killStreakInfo))
									{
										ModifyClientPoints(killerController, CHANGE_MODE.GIVE, killStreakInfo.points, killStreakInfo.message);
									}
									else
										ResetKillStreak(attackerIndex);
								}
								else
									ResetKillStreak(attackerIndex);

							}
						}
					}
				}

				if (assisterController.IsValidPlayer() && IsPointsAllowed())
				{
					if (!PlayerSummaries.ContainsPlayer(assisterController))
						LoadPlayerData(assisterController);

					ModifyClientPoints(assisterController, CHANGE_MODE.GIVE, Config.PointSettings.Assist, "Assist");

					if (@event.Assistedflash)
						ModifyClientPoints(assisterController, CHANGE_MODE.GIVE, Config.PointSettings.AssistFlash, "Flash Assist");
				}

				return HookResult.Continue;
			});
		}
	}
}