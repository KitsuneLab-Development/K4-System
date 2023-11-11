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
			RegisterEventHandler<EventPlayerDisconnect>((@event, info) =>
			{
				CCSPlayerController playerController = @event.Userid;

				if (playerController.IsValidPlayer())
					PlayerSummaries.RemovePlayer(playerController);

				return HookResult.Continue;
			});
			RegisterEventHandler<EventPlayerConnectFull>((@event, info) =>
			{
				CCSPlayerController playerController = @event.Userid;

				playerKillStreaks[playerController.UserId ?? 0] = (0, DateTime.MinValue);

				if (playerController.IsValidPlayer())
					LoadPlayerData(playerController);

				return HookResult.Continue;
			});
			RegisterEventHandler<EventHostageRescued>((@event, info) =>
			{
				ModifyClientPoints(@event.Userid, CHANGE_MODE.GIVE, config.HostageRescuePoints, "Hostage Rescued");
				return HookResult.Continue;
			});
			RegisterEventHandler<EventHostageKilled>((@event, info) =>
			{
				ModifyClientPoints(@event.Userid, CHANGE_MODE.REMOVE, config.HostageKillPoints, "Hostage Killed");
				return HookResult.Continue;
			});
			RegisterEventHandler<EventHostageHurt>((@event, info) =>
			{
				ModifyClientPoints(@event.Userid, CHANGE_MODE.REMOVE, config.HostageHurtPoints, "Hostage Hurt");
				return HookResult.Continue;
			});
			RegisterEventHandler<EventBombDropped>((@event, info) =>
			{
				ModifyClientPoints(@event.Userid, CHANGE_MODE.REMOVE, config.BombDropPoints, "Bomb Dropped");
				return HookResult.Continue;
			});
			RegisterEventHandler<EventBombPickup>((@event, info) =>
			{
				ModifyClientPoints(@event.Userid, CHANGE_MODE.GIVE, config.BombPickupPoints, "Bomb Pickup");
				return HookResult.Continue;
			});
			RegisterEventHandler<EventBombDefused>((@event, info) =>
			{
				ModifyClientPoints(@event.Userid, CHANGE_MODE.GIVE, config.DefusePoints, "Bomb Defused");
				return HookResult.Continue;
			});
			RegisterEventHandler<EventPlayerTeam>((@event, info) =>
			{
				CCSPlayerController player = @event.Userid;
				if (player == null || !player.IsValid || player.IsBot || @event.Oldteam == @event.Team)
					return HookResult.Continue;

				DateTime now = DateTime.UtcNow;
				double seconds = (now - PlayerSummaries[player].Times["Team"]).TotalSeconds;

				UpdatePlayerData(player, GetFieldForTeam((CsTeam)@event.Oldteam), seconds);

				PlayerSummaries[player].Times["Team"] = now;

				return HookResult.Continue;
			});
			RegisterEventHandler<EventRoundMvp>((@event, info) =>
			{
				CCSPlayerController player = @event.Userid;

				if (player == null || !player.IsValid || player.IsBot)
					return HookResult.Continue;

				MySql!.ExecuteNonQueryAsync($"UPDATE `k4stats` SET `mvp` = (`mvp` + 1) WHERE `steam_id` = {player.SteamID};");

				ModifyClientPoints(@event.Userid, CHANGE_MODE.GIVE, config.MVPPoints, "Round MVP");
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

					if (config.DisableSpawnMessage || PlayerSummaries[player].SpawnedThisRound)
						continue;

					player.PrintToChat($" {config.ChatPrefix} {ChatColors.Green}The server is using the {ChatColors.Gold}K4-System {ChatColors.Green}plugin. Type {ChatColors.Red}!k4 {ChatColors.Green}to get more information!");

					PlayerSummaries[player].SpawnedThisRound = true;
				}

				return HookResult.Continue;
			});
			RegisterEventHandler<EventRoundEnd>((@event, info) =>
			{
				CsTeam winnerTeam = (CsTeam)@event.Winner;

				if (!IsPointsAllowed())
					return HookResult.Continue;

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
								MySql!.ExecuteNonQueryAsync($"UPDATE `k4stats` SET `round_win` = (`round_win` + 1) WHERE `steam_id` = {player.SteamID};");

							ModifyClientPoints(player, CHANGE_MODE.GIVE, config.RoundWinPoints, "Round Win");
						}
						else
						{
							if (IsStatsAllowed())
								MySql!.ExecuteNonQueryAsync($"UPDATE `k4stats` SET `round_lose` = (`round_lose` + 1) WHERE `steam_id` = {player.SteamID};");

							ModifyClientPoints(player, CHANGE_MODE.REMOVE, config.RoundLosePoints, "Round Lose");
						}
					}

					PlayerSummaries[player].SpawnedThisRound = false;
				}

				return HookResult.Continue;
			});
			RegisterEventHandler<EventPlayerSpawn>((@event, info) =>
			{
				CCSPlayerController player = @event.Userid;
				if (player == null || !player.IsValid || player.IsBot)
					return HookResult.Continue;

				if (!PlayerSummaries.ContainsPlayer(player))
					LoadPlayerData(player);

				if (player.UserId != null)
				{
					var playerData = PlayerSummaries[player].Times;
					if (playerData != null && playerData.ContainsKey("Death"))
					{
						UpdatePlayerData(player, "dead", (DateTime.UtcNow - playerData["Death"]).TotalSeconds);
					}
				}

				PlayerSummaries[player].Times["Death"] = DateTime.UtcNow;

				return HookResult.Continue;
			});
			RegisterEventHandler<EventBombPlanted>((@event, info) =>
			{
				ModifyClientPoints(@event.Userid, CHANGE_MODE.GIVE, config.PlantPoints, "Bomb Plant");
				return HookResult.Continue;
			});
			RegisterEventHandler<EventGrenadeThrown>((@event, info) =>
			{
				CCSPlayerController playerController = @event.Userid;

				if (!playerController.IsValidPlayer())
					return HookResult.Continue;

				if (IsStatsAllowed())
					MySql!.ExecuteNonQueryAsync($"UPDATE `k4stats` SET `grenades` = (`grenades` + 1) WHERE `steam_id` = {playerController.SteamID};");

				return HookResult.Continue;
			});
			RegisterEventHandler<EventPlayerHurt>((@event, info) =>
			{
				CCSPlayerController attackerController = @event.Attacker;

				if (!attackerController.IsValidPlayer())
					return HookResult.Continue;

				if (IsStatsAllowed())
					MySql!.ExecuteNonQueryAsync($"UPDATE `k4stats` SET `hits` = (`hits` + 1){(@event.Hitgroup == 1 ? $", `headshots` = (`headshots` + 1)" : "")} WHERE `steam_id` = {attackerController.SteamID};");

				return HookResult.Continue;
			});
			RegisterEventHandler<EventPlayerDeath>((@event, info) =>
			{
				CCSPlayerController victimController = @event.Userid;
				CCSPlayerController killerController = @event.Attacker;
				CCSPlayerController assisterController = @event.Assister;

				// Decrease victim points
				if (!victimController.IsValid)
					return HookResult.Continue;

				if (!victimController.IsBot && IsStatsAllowed() && (config.StatsForBots || !killerController.IsBot))
				{
					MySql!.ExecuteNonQueryAsync($"UPDATE `k4stats` SET `deaths` = (`deaths` + 1) WHERE `steam_id` = {victimController.SteamID};");
				}

				if (!victimController.IsBot && (config.PointsForBots || !killerController.IsBot) && IsPointsAllowed())
				{
					if (victimController.UserId == killerController.UserId)
					{
						ModifyClientPoints(victimController, CHANGE_MODE.REMOVE, config.SuicidePoints, "Suicide");
					}
					else
					{
						ModifyClientPoints(victimController, CHANGE_MODE.REMOVE, config.DeathPoints, "Dying");
					}

					if (config.ScoreboardScoreSync)
						victimController.Score = PlayerSummaries[victimController].Points;
				}

				if (victimController.UserId == killerController.UserId)
					return HookResult.Continue;

				if (killerController.IsValidPlayer())
				{
					if (IsStatsAllowed() && (config.StatsForBots || !victimController.IsBot))
					{
						MySql!.ExecuteNonQueryAsync($"UPDATE `k4stats` SET `kills` = (`kills` + 1) WHERE `steam_id` = {killerController.SteamID};");
					}

					if ((config.PointsForBots || !victimController.IsBot) && IsPointsAllowed())
					{
						if (!config.FFAMode && killerController.TeamNum == victimController.TeamNum)
						{
							ModifyClientPoints(killerController, CHANGE_MODE.REMOVE, config.TeamKillPoints, "TeamKill");
						}
						else
						{
							if (!PlayerSummaries.ContainsPlayer(killerController))
								LoadPlayerData(killerController);

							int pointChange = 0;

							if (config.KillPoints > 0)
							{
								pointChange += config.KillPoints;
								killerController.PrintToChat($" {config.ChatPrefix} {ChatColors.White}Points: {ChatColors.Green}{PlayerSummaries[killerController].Points} [+{config.KillPoints} Kill]");
								PlayerSummaries[killerController].Points += config.KillPoints;
							}

							if (@event.Headshot && config.HeadshotPoints > 0)
							{
								pointChange += config.HeadshotPoints;
								killerController.PrintToChat($" {config.ChatPrefix} {ChatColors.White}Points: {ChatColors.Green}{PlayerSummaries[killerController].Points} {ChatColors.Olive}[+{config.HeadshotPoints} Headshot]");
								PlayerSummaries[killerController].Points += config.HeadshotPoints;
							}

							int penetrateCount = @event.Penetrated;
							if (penetrateCount > 0 && config.PenetratedPoints > 0)
							{
								int calculatedPoints = @event.Penetrated * config.PenetratedPoints;
								pointChange += calculatedPoints;
								killerController.PrintToChat($" {config.ChatPrefix} {ChatColors.White}Points: {ChatColors.Green}{PlayerSummaries[killerController].Points} {ChatColors.LightBlue}[+{calculatedPoints} Penetration]");
								PlayerSummaries[killerController].Points += calculatedPoints;
							}

							if (@event.Noscope && config.NoScopePoints > 0)
							{
								pointChange += config.NoScopePoints;
								killerController.PrintToChat($" {config.ChatPrefix} {ChatColors.White}Points: {ChatColors.Green}{PlayerSummaries[killerController].Points} {ChatColors.LightRed}[+{config.NoScopePoints} NoScope]");
								PlayerSummaries[killerController].Points += config.NoScopePoints;
							}

							if (@event.Thrusmoke && config.ThrusmokePoints > 0)
							{
								pointChange += config.ThrusmokePoints;
								killerController.PrintToChat($" {config.ChatPrefix} {ChatColors.White}Points: {ChatColors.Green}{PlayerSummaries[killerController].Points} {ChatColors.LightRed}[+{config.ThrusmokePoints} ThruSmoke]");
								PlayerSummaries[killerController].Points += config.ThrusmokePoints;
							}

							if (@event.Attackerblind && config.BlindKillPoints > 0)
							{
								pointChange += config.BlindKillPoints;
								killerController.PrintToChat($" {config.ChatPrefix} {ChatColors.White}Points: {ChatColors.Green}{PlayerSummaries[killerController].Points} {ChatColors.LightRed}[+{config.BlindKillPoints} Blind Kill]");
								PlayerSummaries[killerController].Points += config.BlindKillPoints;
							}

							if (@event.Distance >= config.LongDistance && config.LongDistanceKillPoints > 0)
							{
								pointChange += config.LongDistanceKillPoints;
								killerController.PrintToChat($" {config.ChatPrefix} {ChatColors.White}Points: {ChatColors.Green}{PlayerSummaries[killerController].Points} {ChatColors.Magenta}[+{config.LongDistanceKillPoints} Long Distance]");
								PlayerSummaries[killerController].Points += config.LongDistanceKillPoints;
							}

							string lowerCaseWeaponName = @event.Weapon.ToLower();

							switch (lowerCaseWeaponName)
							{
								case var _ when lowerCaseWeaponName.Contains("hegrenade") || lowerCaseWeaponName.Contains("tagrenade") || lowerCaseWeaponName.Contains("firebomb") || lowerCaseWeaponName.Contains("molotov") || lowerCaseWeaponName.Contains("incgrenade") || lowerCaseWeaponName.Contains("flashbang") || lowerCaseWeaponName.Contains("smokegrenade") || lowerCaseWeaponName.Contains("frag") || lowerCaseWeaponName.Contains("bumpmine"):
									{
										if (config.GrenadeKillPoints > 0)
										{
											pointChange += config.GrenadeKillPoints;
											killerController.PrintToChat($" {config.ChatPrefix} {ChatColors.White}Points: {ChatColors.Green}{PlayerSummaries[killerController].Points} {ChatColors.Magenta}[+{config.GrenadeKillPoints} Grenade Kill]");
											PlayerSummaries[killerController].Points += config.GrenadeKillPoints;
										}
										break;
									}
								case var _ when lowerCaseWeaponName.Contains("cord") || lowerCaseWeaponName.Contains("bowie") || lowerCaseWeaponName.Contains("butterfly") || lowerCaseWeaponName.Contains("karambit") || lowerCaseWeaponName.Contains("skeleton") || lowerCaseWeaponName.Contains("m9_bayonet") || lowerCaseWeaponName.Contains("bayonet") || lowerCaseWeaponName.Contains("t") || lowerCaseWeaponName.Contains("knifegg") || lowerCaseWeaponName.Contains("stiletto") || lowerCaseWeaponName.Contains("ursus") || lowerCaseWeaponName.Contains("tactical") || lowerCaseWeaponName.Contains("push") || lowerCaseWeaponName.Contains("widowmaker") || lowerCaseWeaponName.Contains("outdoor") || lowerCaseWeaponName.Contains("canis"):
									{
										if (config.KnifeKillPoints > 0)
										{
											pointChange += config.KnifeKillPoints;
											killerController.PrintToChat($" {config.ChatPrefix} {ChatColors.White}Points: {ChatColors.Green}{PlayerSummaries[killerController].Points} {ChatColors.Magenta}[+{config.KnifeKillPoints} Knife Kill]");
											PlayerSummaries[killerController].Points += config.KnifeKillPoints;
										}
										break;
									}
								case "taser":
									{
										if (config.TaserKillPoints > 0)
										{
											pointChange += config.TaserKillPoints;
											killerController.PrintToChat($" {config.ChatPrefix} {ChatColors.White}Points: {ChatColors.Green}{PlayerSummaries[killerController].Points} {ChatColors.Magenta}[+{config.TaserKillPoints} Taser Kill]");
											PlayerSummaries[killerController].Points += config.TaserKillPoints;
										}
										break;
									}
							}

							int attackerIndex = killerController.UserId ?? 0;
							if (playerKillStreaks.ContainsKey(attackerIndex))
							{
								// Check if the player got a kill within the last 5 seconds
								if (playerKillStreaks[attackerIndex].killStreak > 0 && DateTime.Now - playerKillStreaks[attackerIndex].lastKillTime <= TimeSpan.FromSeconds(config.SecondsBetweenKills))
								{
									playerKillStreaks[attackerIndex] = (playerKillStreaks[attackerIndex].killStreak + 1, DateTime.Now);
									int killStreak = playerKillStreaks[attackerIndex].killStreak;

									// Award points for the kill streak
									int points = 0;
									string killStreakMessage = "";

									switch (killStreak)
									{
										case 2:
											points = config.DoubleKillPoints;
											killStreakMessage = "DoubleK ill";
											break;
										case 3:
											points = config.TripleKillPoints;
											killStreakMessage = "Triple Kill";
											break;
										case 4:
											points = config.DominationPoints;
											killStreakMessage = "Domination";
											break;
										case 5:
											points = config.RampagePoints;
											killStreakMessage = "Rampage";
											break;
										case 6:
											points = config.MegaKillPoints;
											killStreakMessage = "Mega Kill";
											break;
										case 7:
											points = config.OwnagePoints;
											killStreakMessage = "Ownage";
											break;
										case 8:
											points = config.UltraKillPoints;
											killStreakMessage = "Ultra Kill";
											break;
										case 9:
											points = config.KillingSpreePoints;
											killStreakMessage = "Killing Spree";
											break;
										case 10:
											points = config.MonsterKillPoints;
											killStreakMessage = "Monster Kill";
											break;
										case 11:
											points = config.UnstoppablePoints;
											killStreakMessage = "Unstoppable";
											break;
										case 12:
											points = config.GodLikePoints;
											killStreakMessage = "God Like";
											break;
										default:
											ResetKillStreak(attackerIndex);
											break;
									}

									if (points > 0)
									{
										pointChange += points;
										killerController.PrintToChat($" {config.ChatPrefix} {ChatColors.White}Points: {ChatColors.Green}{PlayerSummaries[killerController].Points} {ChatColors.LightYellow}[+{points} {killStreakMessage}]");
										PlayerSummaries[killerController].Points += points;
									}
								}
								else
								{
									ResetKillStreak(attackerIndex);
								}
							}

							if (config.ScoreboardScoreSync)
								killerController.Score = PlayerSummaries[killerController].Points;

							if (AdminManager.PlayerHasPermissions(killerController, "@k4system/vip/points-multiplier"))
							{
								pointChange = (int)Math.Round(pointChange * config.VipPointMultiplier);
							}

							MySql!.ExecuteNonQueryAsync($"UPDATE `k4ranks` SET `points` = (`points` + {pointChange}) WHERE `steam_id` = {killerController.SteamID};");
						}
					}
				}

				if (assisterController.IsValidPlayer())
				{
					if (!PlayerSummaries.ContainsPlayer(assisterController))
						LoadPlayerData(assisterController);

					int pointChange = 0;

					if (config.AssistPoints > 0)
					{
						pointChange += config.AssistPoints;
						assisterController.PrintToChat($" {config.ChatPrefix} {ChatColors.White}Points: {ChatColors.Green}{PlayerSummaries[assisterController].Points} [+{config.AssistPoints} Assist]");
						PlayerSummaries[assisterController].Points += config.AssistPoints;
					}

					if (@event.Assistedflash && config.AsssistFlashPoints > 0)
					{
						pointChange += config.AsssistFlashPoints;
						assisterController.PrintToChat($" {config.ChatPrefix} {ChatColors.White}Points: {ChatColors.Green}{PlayerSummaries[assisterController].Points} [+{config.AsssistFlashPoints} Flash Assist]");
						PlayerSummaries[assisterController].Points += config.AsssistFlashPoints;
					}

					if (config.ScoreboardScoreSync)
						assisterController.Score = PlayerSummaries[assisterController].Points;

					if (AdminManager.PlayerHasPermissions(assisterController, "@k4system/vip/points-multiplier"))
					{
						pointChange = (int)Math.Round(pointChange * config.VipPointMultiplier);
					}

					MySql!.ExecuteNonQueryAsync($"UPDATE `k4ranks` SET `points` = (`points` + {pointChange}) WHERE `steam_id` = {assisterController.SteamID};");
				}

				return HookResult.Continue;
			});
		}
	}
}