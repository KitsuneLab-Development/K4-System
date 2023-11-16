using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace K4ryuuSystem
{
	public partial class K4System
	{
		private void SetupGameEvents()
		{
			RegisterListener<Listeners.OnMapEnd>(() =>
			{
				Log("Processing end of map for all players", LogLevel.Debug);

				List<CCSPlayerController> players = Utilities.GetPlayers();
				foreach (CCSPlayerController player in players)
				{
					if (player.IsBot || player.IsHLTV)
						continue;

					if (PlayerSummaries.ContainsPlayer(player!))
					{
						PlayerSummaries[player].PointsChanged = 0;

						if (Config.GeneralSettings.ModuleTimes)
						{
							SaveClientTime(player);
							Log($"Saved time data for player: {player.PlayerName}", LogLevel.Debug);
						}

						if (Config.GeneralSettings.ModuleStats)
						{
							SaveClientStats(player);
							Log($"Saved stats data for player: {player.PlayerName}", LogLevel.Debug);
						}

						if (Config.GeneralSettings.ModuleRanks)
						{
							SaveClientRank(player);
							Log($"Saved rank data for player: {player.PlayerName}", LogLevel.Debug);
						}
					}
				}

				Log("End of map processing completed", LogLevel.Debug);
			});
			RegisterEventHandler<EventPlayerDisconnect>((@event, info) =>
			{
				CCSPlayerController player = @event.Userid;

				if (!player.IsValidPlayer())
				{
					Log("Disconnected player is not valid.", LogLevel.Debug);
					return HookResult.Continue;
				}

				if (Config.GeneralSettings.ModuleTimes)
				{
					SaveClientTime(player);
					Log($"Saved time data for disconnected player: {player.PlayerName}", LogLevel.Debug);
				}

				if (Config.GeneralSettings.ModuleStats)
				{
					SaveClientStats(player);
					Log($"Saved stats data for disconnected player: {player.PlayerName}", LogLevel.Debug);
				}

				if (Config.GeneralSettings.ModuleRanks)
				{
					SaveClientRank(player);
					Log($"Saved rank data for disconnected player: {player.PlayerName}", LogLevel.Debug);
				}

				PlayerSummaries.RemovePlayer(player);
				Log($"Removed player: {player.PlayerName} from the player summaries", LogLevel.Debug);

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
	CCSPlayerController player = @event.Userid;

	if (!player.IsValidPlayer())
	{
		Log("Hostage Rescued: Player is not valid.", LogLevel.Debug);
		return HookResult.Continue;
	}

	ModifyClientPoints(player, CHANGE_MODE.GIVE, Config.PointSettings.HostageRescue, "Hostage Rescued");
	Log($"Hostage Rescued: Modified points for player: {player.PlayerName}", LogLevel.Debug);

	return HookResult.Continue;
});

			RegisterEventHandler<EventHostageKilled>((@event, info) =>
			{
				CCSPlayerController player = @event.Userid;

				if (!player.IsValidPlayer())
				{
					Log("Hostage Killed: Player is not valid.", LogLevel.Debug);
					return HookResult.Continue;
				}

				ModifyClientPoints(player, CHANGE_MODE.REMOVE, Config.PointSettings.HostageKill, "Hostage Killed");
				Log($"Hostage Killed: Modified points for player: {player.PlayerName}", LogLevel.Debug);

				return HookResult.Continue;
			});
			RegisterEventHandler<EventHostageHurt>((@event, info) =>
			{
				CCSPlayerController player = @event.Userid;

				if (!player.IsValidPlayer())
				{
					Log("Hostage Hurt: Player is not valid.", LogLevel.Debug);
					return HookResult.Continue;
				}

				ModifyClientPoints(player, CHANGE_MODE.REMOVE, Config.PointSettings.HostageHurt, "Hostage Hurt");
				Log($"Hostage Hurt: Modified points for player: {player.PlayerName}", LogLevel.Debug);

				return HookResult.Continue;
			});
			RegisterEventHandler<EventBombDropped>((@event, info) =>
			{
				CCSPlayerController player = @event.Userid;

				if (!player.IsValidPlayer())
				{
					Log("Bomb Dropped: Player is not valid.", LogLevel.Debug);
					return HookResult.Continue;
				}

				Server.NextFrame(() =>
				{
					ModifyClientPoints(player, CHANGE_MODE.REMOVE, Config.PointSettings.BombDrop, "Bomb Dropped");
				});

				Log($"Bomb Dropped: Modified points for player: {player.PlayerName}", LogLevel.Debug);

				return HookResult.Continue;
			});
			RegisterEventHandler<EventBombPickup>((@event, info) =>
			{
				CCSPlayerController player = @event.Userid;

				if (!player.IsValidPlayer())
				{
					Log("Bomb Pickup: Player is not valid.", LogLevel.Debug);
					return HookResult.Continue;
				}

				ModifyClientPoints(player, CHANGE_MODE.GIVE, Config.PointSettings.BombPickup, "Bomb Pickup");
				Log($"Bomb Pickup: Modified points for player: {player.PlayerName}", LogLevel.Debug);

				return HookResult.Continue;
			});
			RegisterEventHandler<EventBombDefused>((@event, info) =>
			{
				CCSPlayerController player = @event.Userid;

				if (!player.IsValidPlayer())
				{
					Log("Bomb Defused: Player is not valid.", LogLevel.Debug);
					return HookResult.Continue;
				}

				ModifyClientPoints(player, CHANGE_MODE.GIVE, Config.PointSettings.Defuse, "Bomb Defused");
				Log($"Bomb Defused: Modified points for player: {player.PlayerName}", LogLevel.Debug);

				return HookResult.Continue;
			});
			RegisterEventHandler<EventPlayerTeam>((@event, info) =>
			{
				if (!Config.GeneralSettings.ModuleTimes)
				{
					Log("EventPlayerTeam: ModuleTimes is not enabled.", LogLevel.Debug);
					return HookResult.Continue;
				}

				CCSPlayerController player = @event.Userid;

				if (player == null || !player.IsValid || player.IsBot || @event.Oldteam == @event.Team)
				{
					Log("EventPlayerTeam: Player is not valid or is a bot, or teams did not change.", LogLevel.Debug);
					return HookResult.Continue;
				}

				if (!PlayerSummaries.ContainsPlayer(player!))
					LoadPlayerData(player!);

				DateTime now = DateTime.UtcNow;
				double seconds = (now - PlayerSummaries[player].Times["Team"]).TotalSeconds;

				PlayerSummaries[player].TimeFields[GetFieldForTeam((CsTeam)@event.Oldteam)] += (int)seconds;

				PlayerSummaries[player].Times["Team"] = now;

				Log($"EventPlayerTeam: Team switch recorded for player: {player.PlayerName}", LogLevel.Debug);

				return HookResult.Continue;
			});
			RegisterEventHandler<EventRoundMvp>((@event, info) =>
			{
				CCSPlayerController player = @event.Userid;

				if (player == null || !player.IsValid || player.IsBot)
				{
					Log("EventRoundMvp: Player is not valid or is a bot.", LogLevel.Debug);
					return HookResult.Continue;
				}

				if (IsStatsAllowed())
				{
					PlayerSummaries[player].StatFields["mvp"]++;
					Log($"EventRoundMvp: MVP recorded for player: {player.PlayerName}", LogLevel.Debug);
				}

				if (Config.GeneralSettings.ModuleRanks)
				{
					ModifyClientPoints(@event.Userid, CHANGE_MODE.GIVE, Config.PointSettings.MVP, "Round MVP");
					Log($"EventRoundMvp: Modified points for player: {player.PlayerName}", LogLevel.Debug);
				}

				return HookResult.Continue;
			});
			RegisterEventHandler<EventRoundStart>((@event, info) =>
			{
				if (!IsPointsAllowed())
				{
					Log("EventRoundStart: Points are not allowed.", LogLevel.Debug);
					return HookResult.Continue;
				}

				List<CCSPlayerController> players = Utilities.GetPlayers();
				foreach (CCSPlayerController player in players)
				{
					if (player.IsBot || player.IsHLTV)
						continue;

					if (!PlayerSummaries.ContainsPlayer(player!))
						LoadPlayerData(player!);

					if (!Config.GeneralSettings.SpawnMessage || PlayerSummaries[player].SpawnedThisRound)
						continue;

					player.PrintToChat($" {Config.GeneralSettings.Prefix} {ChatColors.Green}The server is using the {ChatColors.Gold}K4-System {ChatColors.Green}plugin. Type {ChatColors.Red}!k4 {ChatColors.Green}to get more information!");

					PlayerSummaries[player].SpawnedThisRound = true;
					Log($"EventRoundStart: Spawn message sent to player: {player.PlayerName}", LogLevel.Debug);
				}

				return HookResult.Continue;
			});
			RegisterEventHandler<EventRoundEnd>((@event, info) =>
			{
				CsTeam winnerTeam = (CsTeam)@event.Winner;

				List<CCSPlayerController> players = Utilities.GetPlayers();
				foreach (CCSPlayerController player in players)
				{
					if (player.IsBot || player.IsHLTV)
						continue;

					if (!PlayerSummaries.ContainsPlayer(player))
						LoadPlayerData(player);

					if (!PlayerSummaries[player].SpawnedThisRound)
						continue;

					if (Config.RankSettings.RoundEndPoints)
					{
						if (PlayerSummaries[player].PointsChanged > 0)
						{
							player.PrintToChat($" {Config.GeneralSettings.Prefix} {ChatColors.White}Points: {ChatColors.Green}+{PlayerSummaries[player].PointsChanged} Round Summary");
						}
						else if (PlayerSummaries[player].PointsChanged < 0)
						{
							int absPointsChanged = Math.Abs(PlayerSummaries[player].PointsChanged);
							player.PrintToChat($" {Config.GeneralSettings.Prefix} {ChatColors.White}Points: {ChatColors.Red}-{PlayerSummaries[player].PointsChanged} Round Summary");
						}
						else
							player.PrintToChat($" {Config.GeneralSettings.Prefix} {ChatColors.White}Points: No changes in this round");

						PlayerSummaries[player].PointsChanged = 0;
					}

					CsTeam playerTeam = (CsTeam)player.TeamNum;
					if (playerTeam != CsTeam.None && playerTeam != CsTeam.Spectator)
					{
						if (winnerTeam == playerTeam)
						{
							if (IsStatsAllowed())
							{
								PlayerSummaries[player].StatFields["round_win"]++;
								Log($"EventRoundEnd: Recorded round win for player: {player.PlayerName}", LogLevel.Debug);
							}

							if (IsPointsAllowed())
							{
								ModifyClientPoints(player, CHANGE_MODE.GIVE, Config.PointSettings.RoundWin, "Round Win");
								Log($"EventRoundEnd: Modified points (round win) for player: {player.PlayerName}", LogLevel.Debug);
							}
						}
						else
						{
							if (IsStatsAllowed())
							{
								PlayerSummaries[player].StatFields["round_lose"]++;
								Log($"EventRoundEnd: Recorded round lose for player: {player.PlayerName}", LogLevel.Debug);
							}

							if (IsPointsAllowed())
							{
								ModifyClientPoints(player, CHANGE_MODE.REMOVE, Config.PointSettings.RoundLose, "Round Lose");
								Log($"EventRoundEnd: Modified points (round lose) for player: {player.PlayerName}", LogLevel.Debug);
							}
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
				if (player == null || !player.IsValid || player.IsBot || player.IsHLTV)
					return HookResult.Continue;

				if (!PlayerSummaries.ContainsPlayer(player))
					LoadPlayerData(player);

				var playerData = PlayerSummaries[player].Times;
				if (playerData != null && playerData.ContainsKey("Death"))
				{
					PlayerSummaries[player].TimeFields["dead"] += (int)(DateTime.UtcNow - playerData["Death"]).TotalSeconds;
					Log($"EventPlayerSpawn: Updated dead time for player: {player.PlayerName}", LogLevel.Debug);
				}

				PlayerSummaries[player].Times["Death"] = DateTime.UtcNow;
				Log($"EventPlayerSpawn: Recorded spawn for player: {player.PlayerName}", LogLevel.Debug);

				return HookResult.Continue;
			});
			RegisterEventHandler<EventBombPlanted>((@event, info) =>
			{
				ModifyClientPoints(@event.Userid, CHANGE_MODE.GIVE, Config.PointSettings.Plant, "Bomb Plant");
				Log($"EventBombPlanted: Modified points (bomb plant) for player: {@event.Userid.PlayerName}", LogLevel.Debug);
				return HookResult.Continue;
			});
			RegisterEventHandler<EventGrenadeThrown>((@event, info) =>
			{
				CCSPlayerController player = @event.Userid;

				if (!player.IsValidPlayer())
					return HookResult.Continue;

				if (IsStatsAllowed())
				{
					PlayerSummaries[player].StatFields["grenades"]++;
					Log($"EventGrenadeThrown: Grenade thrown recorded for player: {player.PlayerName}", LogLevel.Debug);
				}

				return HookResult.Continue;
			});
			RegisterEventHandler<EventPlayerHurt>((@event, info) =>
			{
				CCSPlayerController player = @event.Attacker;

				if (!player.IsValidPlayer())
					return HookResult.Continue;

				if (IsStatsAllowed())
				{
					PlayerSummaries[player].StatFields["hits"]++;
					Log($"EventPlayerHurt: Player hurt recorded for player: {player.PlayerName}", LogLevel.Debug);
				}

				return HookResult.Continue;
			});
			RegisterEventHandler<EventPlayerDeath>((@event, info) =>
			{
				CCSPlayerController victimController = @event.Userid;
				CCSPlayerController killerController = @event.Attacker;
				CCSPlayerController assisterController = @event.Assister;

				if (!victimController.IsValid || victimController.UserId <= 0)
					return HookResult.Continue;

				if (!victimController.IsBot)
				{
					if (!PlayerSummaries.ContainsPlayer(killerController))
						LoadPlayerData(killerController);

					if (IsStatsAllowed() && (Config.StatisticSettings.StatsForBots || !killerController.IsBot))
					{
						PlayerSummaries[victimController].StatFields["deaths"]++;
						Log($"EventPlayerDeath: Death recorded for player: {victimController.PlayerName}", LogLevel.Debug);
					}

					if (IsPointsAllowed())
					{
						if (victimController.UserId == killerController.UserId)
						{
							ModifyClientPoints(victimController, CHANGE_MODE.REMOVE, Config.PointSettings.Suicide, "Suicide");
							Log($"EventPlayerDeath: Suicide recorded for player: {victimController.PlayerName}", LogLevel.Debug);
						}
						else if (!killerController.IsBot)
						{
							ModifyClientPoints(victimController, CHANGE_MODE.REMOVE, Config.PointSettings.Death, "Dying");
							Log($"EventPlayerDeath: Death recorded for player: {victimController.PlayerName}", LogLevel.Debug);
						}
					}
				}

				if (victimController.UserId == killerController.UserId)
					return HookResult.Continue;

				if (killerController.IsValidPlayer())
				{
					if (!PlayerSummaries.ContainsPlayer(killerController))
						LoadPlayerData(killerController);

					if (IsStatsAllowed() && (Config.StatisticSettings.StatsForBots || !victimController.IsBot))
					{
						PlayerSummaries[killerController].StatFields["kills"]++;
						Log($"EventPlayerDeath: Kill recorded for player: {killerController.PlayerName}", LogLevel.Debug);
					}

					if ((Config.RankSettings.PointsForBots || !victimController.IsBot) && IsPointsAllowed())
					{
						if (!Config.RankSettings.FFAMode && killerController.TeamNum == victimController.TeamNum)
						{
							ModifyClientPoints(killerController, CHANGE_MODE.REMOVE, Config.PointSettings.TeamKill, "TeamKill");
							Log($"EventPlayerDeath: TeamKill recorded for player: {killerController.PlayerName}", LogLevel.Debug);
						}
						else
						{
							ModifyClientPoints(killerController, CHANGE_MODE.GIVE, Config.PointSettings.Kill, "Kill");
							Log($"EventPlayerDeath: Kill recorded for player: {killerController.PlayerName}", LogLevel.Debug);

							if (@event.Headshot)
							{
								ModifyClientPoints(killerController, CHANGE_MODE.GIVE, Config.PointSettings.Headshot, "Headshot");
								Log($"EventPlayerDeath: Headshot recorded for player: {killerController.PlayerName}", LogLevel.Debug);
							}

							int penetrateCount = @event.Penetrated;
							if (penetrateCount > 0 && Config.PointSettings.Penetrated > 0)
							{
								int calculatedPoints = penetrateCount * Config.PointSettings.Penetrated;
								ModifyClientPoints(killerController, CHANGE_MODE.GIVE, calculatedPoints, "Penetrated");
								Log($"EventPlayerDeath: Penetrated recorded for player: {killerController.PlayerName}", LogLevel.Debug);
							}

							if (@event.Noscope)
							{
								ModifyClientPoints(killerController, CHANGE_MODE.GIVE, Config.PointSettings.NoScope, "NoScope");
								Log($"EventPlayerDeath: NoScope recorded for player: {killerController.PlayerName}", LogLevel.Debug);
							}

							if (@event.Thrusmoke)
							{
								ModifyClientPoints(killerController, CHANGE_MODE.GIVE, Config.PointSettings.Thrusmoke, "ThruSmoke");
								Log($"EventPlayerDeath: ThruSmoke recorded for player: {killerController.PlayerName}", LogLevel.Debug);
							}

							if (@event.Attackerblind)
							{
								ModifyClientPoints(killerController, CHANGE_MODE.GIVE, Config.PointSettings.BlindKill, "Blind Kill");
								Log($"EventPlayerDeath: Blind Kill recorded for player: {killerController.PlayerName}", LogLevel.Debug);
							}

							if (@event.Distance >= Config.PointSettings.LongDistance)
							{
								ModifyClientPoints(killerController, CHANGE_MODE.GIVE, Config.PointSettings.LongDistanceKill, "Long Distance");
								Log($"EventPlayerDeath: Long Distance recorded for player: {killerController.PlayerName}", LogLevel.Debug);
							}

							string lowerCaseWeaponName = @event.Weapon.ToLower();

							switch (lowerCaseWeaponName)
							{
								case var _ when lowerCaseWeaponName.Contains("hegrenade") || lowerCaseWeaponName.Contains("tagrenade") || lowerCaseWeaponName.Contains("firebomb") || lowerCaseWeaponName.Contains("molotov") || lowerCaseWeaponName.Contains("incgrenade") || lowerCaseWeaponName.Contains("flashbang") || lowerCaseWeaponName.Contains("smokegrenade") || lowerCaseWeaponName.Contains("frag") || lowerCaseWeaponName.Contains("bumpmine"):
									{
										ModifyClientPoints(killerController, CHANGE_MODE.GIVE, Config.PointSettings.GrenadeKill, "Grenade Kill");
										Log($"EventPlayerDeath: Grenade Kill recorded for player: {killerController.PlayerName}", LogLevel.Debug);
										break;
									}
								case var _ when lowerCaseWeaponName.Contains("cord") || lowerCaseWeaponName.Contains("bowie") || lowerCaseWeaponName.Contains("butterfly") || lowerCaseWeaponName.Contains("karambit") || lowerCaseWeaponName.Contains("skeleton") || lowerCaseWeaponName.Contains("m9_bayonet") || lowerCaseWeaponName.Contains("bayonet") || lowerCaseWeaponName.Contains("t") || lowerCaseWeaponName.Contains("knifegg") || lowerCaseWeaponName.Contains("stiletto") || lowerCaseWeaponName.Contains("ursus") || lowerCaseWeaponName.Contains("tactical") || lowerCaseWeaponName.Contains("push") || lowerCaseWeaponName.Contains("widowmaker") || lowerCaseWeaponName.Contains("outdoor") || lowerCaseWeaponName.Contains("canis"):
									{
										ModifyClientPoints(killerController, CHANGE_MODE.GIVE, Config.PointSettings.KnifeKill, "Knife Kill");
										Log($"EventPlayerDeath: Knife Kill recorded for player: {killerController.PlayerName}", LogLevel.Debug);
										break;
									}
								case "taser":
									{
										ModifyClientPoints(killerController, CHANGE_MODE.GIVE, Config.PointSettings.TaserKill, "Taser Kill");
										Log($"EventPlayerDeath: Taser Kill recorded for player: {killerController.PlayerName}", LogLevel.Debug);
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
										Log($"EventPlayerDeath: {killStreakInfo.message} recorded for player: {killerController.PlayerName}", LogLevel.Debug);
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
					Log($"EventPlayerDeath: Assist recorded for player: {assisterController.PlayerName}", LogLevel.Debug);

					if (@event.Assistedflash)
					{
						ModifyClientPoints(assisterController, CHANGE_MODE.GIVE, Config.PointSettings.AssistFlash, "Flash Assist");
						Log($"EventPlayerDeath: Flash Assist recorded for player: {assisterController.PlayerName}", LogLevel.Debug);
					}
				}

				return HookResult.Continue;
			});
		}
	}
}