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

							ModifyClientPoints(player, CHANGE_MODE.GIVE, Config.PointSettings.RoundWin, "Round Win");
						}
						else
						{
							if (IsStatsAllowed())
								MySql!.ExecuteNonQueryAsync($"UPDATE `k4stats` SET `round_lose` = (`round_lose` + 1) WHERE `steam_id` = {player.SteamID};");

							ModifyClientPoints(player, CHANGE_MODE.REMOVE, Config.PointSettings.RoundLose, "Round Lose");
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
				ModifyClientPoints(@event.Userid, CHANGE_MODE.GIVE, Config.PointSettings.Plant, "Bomb Plant");
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

				if (!victimController.IsBot && IsStatsAllowed() && (Config.StatisticSettings.StatsForBots || !killerController.IsBot))
				{
					MySql!.ExecuteNonQueryAsync($"UPDATE `k4stats` SET `deaths` = (`deaths` + 1) WHERE `steam_id` = {victimController.SteamID};");
				}

				if (!victimController.IsBot && (Config.RankSettings.PointsForBots || !killerController.IsBot) && IsPointsAllowed())
				{
					if (victimController.UserId == killerController.UserId)
					{
						ModifyClientPoints(victimController, CHANGE_MODE.REMOVE, Config.PointSettings.Suicide, "Suicide");
					}
					else
					{
						ModifyClientPoints(victimController, CHANGE_MODE.REMOVE, Config.PointSettings.Death, "Dying");
					}

					if (Config.RankSettings.ScoreboardScoreSync)
						victimController.Score = PlayerSummaries[victimController].Points;
				}

				if (victimController.UserId == killerController.UserId)
					return HookResult.Continue;

				if (killerController.IsValidPlayer())
				{
					if (IsStatsAllowed() && (Config.StatisticSettings.StatsForBots || !victimController.IsBot))
					{
						MySql!.ExecuteNonQueryAsync($"UPDATE `k4stats` SET `kills` = (`kills` + 1) WHERE `steam_id` = {killerController.SteamID};");
					}

					if ((Config.RankSettings.PointsForBots || !victimController.IsBot) && IsPointsAllowed())
					{
						if (!Config.RankSettings.FFAMode && killerController.TeamNum == victimController.TeamNum)
						{
							ModifyClientPoints(killerController, CHANGE_MODE.REMOVE, Config.PointSettings.TeamKill, "TeamKill");
						}
						else
						{
							if (!PlayerSummaries.ContainsPlayer(killerController))
								LoadPlayerData(killerController);

							int pointChange = 0;

							if (Config.PointSettings.Kill > 0)
							{
								pointChange += Config.PointSettings.Kill;
								killerController.PrintToChat($" {Config.GeneralSettings.Prefix} {ChatColors.White}Points: {ChatColors.Green}{PlayerSummaries[killerController].Points} [+{Config.PointSettings.Kill} Kill]");
								PlayerSummaries[killerController].Points += Config.PointSettings.Kill;
							}

							if (@event.Headshot && Config.PointSettings.Headshot > 0)
							{
								pointChange += Config.PointSettings.Headshot;
								killerController.PrintToChat($" {Config.GeneralSettings.Prefix} {ChatColors.White}Points: {ChatColors.Green}{PlayerSummaries[killerController].Points} {ChatColors.Olive}[+{Config.PointSettings.Headshot} Headshot]");
								PlayerSummaries[killerController].Points += Config.PointSettings.Headshot;
							}

							int penetrateCount = @event.Penetrated;
							if (penetrateCount > 0 && Config.PointSettings.Penetrated > 0)
							{
								int calculatedPoints = @event.Penetrated * Config.PointSettings.Penetrated;
								pointChange += calculatedPoints;
								killerController.PrintToChat($" {Config.GeneralSettings.Prefix} {ChatColors.White}Points: {ChatColors.Green}{PlayerSummaries[killerController].Points} {ChatColors.LightBlue}[+{calculatedPoints} Penetration]");
								PlayerSummaries[killerController].Points += calculatedPoints;
							}

							if (@event.Noscope && Config.PointSettings.NoScope > 0)
							{
								pointChange += Config.PointSettings.NoScope;
								killerController.PrintToChat($" {Config.GeneralSettings.Prefix} {ChatColors.White}Points: {ChatColors.Green}{PlayerSummaries[killerController].Points} {ChatColors.LightRed}[+{Config.PointSettings.NoScope} NoScope]");
								PlayerSummaries[killerController].Points += Config.PointSettings.NoScope;
							}

							if (@event.Thrusmoke && Config.PointSettings.Thrusmoke > 0)
							{
								pointChange += Config.PointSettings.Thrusmoke;
								killerController.PrintToChat($" {Config.GeneralSettings.Prefix} {ChatColors.White}Points: {ChatColors.Green}{PlayerSummaries[killerController].Points} {ChatColors.LightRed}[+{Config.PointSettings.Thrusmoke} ThruSmoke]");
								PlayerSummaries[killerController].Points += Config.PointSettings.Thrusmoke;
							}

							if (@event.Attackerblind && Config.PointSettings.BlindKill > 0)
							{
								pointChange += Config.PointSettings.BlindKill;
								killerController.PrintToChat($" {Config.GeneralSettings.Prefix} {ChatColors.White}Points: {ChatColors.Green}{PlayerSummaries[killerController].Points} {ChatColors.LightRed}[+{Config.PointSettings.BlindKill} Blind Kill]");
								PlayerSummaries[killerController].Points += Config.PointSettings.BlindKill;
							}

							if (@event.Distance >= Config.PointSettings.LongDistance && Config.PointSettings.LongDistanceKill > 0)
							{
								pointChange += Config.PointSettings.LongDistanceKill;
								killerController.PrintToChat($" {Config.GeneralSettings.Prefix} {ChatColors.White}Points: {ChatColors.Green}{PlayerSummaries[killerController].Points} {ChatColors.Magenta}[+{Config.PointSettings.LongDistanceKill} Long Distance]");
								PlayerSummaries[killerController].Points += Config.PointSettings.LongDistanceKill;
							}

							string lowerCaseWeaponName = @event.Weapon.ToLower();

							switch (lowerCaseWeaponName)
							{
								case var _ when lowerCaseWeaponName.Contains("hegrenade") || lowerCaseWeaponName.Contains("tagrenade") || lowerCaseWeaponName.Contains("firebomb") || lowerCaseWeaponName.Contains("molotov") || lowerCaseWeaponName.Contains("incgrenade") || lowerCaseWeaponName.Contains("flashbang") || lowerCaseWeaponName.Contains("smokegrenade") || lowerCaseWeaponName.Contains("frag") || lowerCaseWeaponName.Contains("bumpmine"):
									{
										if (Config.PointSettings.GrenadeKill > 0)
										{
											pointChange += Config.PointSettings.GrenadeKill;
											killerController.PrintToChat($" {Config.GeneralSettings.Prefix} {ChatColors.White}Points: {ChatColors.Green}{PlayerSummaries[killerController].Points} {ChatColors.Magenta}[+{Config.PointSettings.GrenadeKill} Grenade Kill]");
											PlayerSummaries[killerController].Points += Config.PointSettings.GrenadeKill;
										}
										break;
									}
								case var _ when lowerCaseWeaponName.Contains("cord") || lowerCaseWeaponName.Contains("bowie") || lowerCaseWeaponName.Contains("butterfly") || lowerCaseWeaponName.Contains("karambit") || lowerCaseWeaponName.Contains("skeleton") || lowerCaseWeaponName.Contains("m9_bayonet") || lowerCaseWeaponName.Contains("bayonet") || lowerCaseWeaponName.Contains("t") || lowerCaseWeaponName.Contains("knifegg") || lowerCaseWeaponName.Contains("stiletto") || lowerCaseWeaponName.Contains("ursus") || lowerCaseWeaponName.Contains("tactical") || lowerCaseWeaponName.Contains("push") || lowerCaseWeaponName.Contains("widowmaker") || lowerCaseWeaponName.Contains("outdoor") || lowerCaseWeaponName.Contains("canis"):
									{
										if (Config.PointSettings.KnifeKill > 0)
										{
											pointChange += Config.PointSettings.KnifeKill;
											killerController.PrintToChat($" {Config.GeneralSettings.Prefix} {ChatColors.White}Points: {ChatColors.Green}{PlayerSummaries[killerController].Points} {ChatColors.Magenta}[+{Config.PointSettings.KnifeKill} Knife Kill]");
											PlayerSummaries[killerController].Points += Config.PointSettings.KnifeKill;
										}
										break;
									}
								case "taser":
									{
										if (Config.PointSettings.TaserKill > 0)
										{
											pointChange += Config.PointSettings.TaserKill;
											killerController.PrintToChat($" {Config.GeneralSettings.Prefix} {ChatColors.White}Points: {ChatColors.Green}{PlayerSummaries[killerController].Points} {ChatColors.Magenta}[+{Config.PointSettings.TaserKill} Taser Kill]");
											PlayerSummaries[killerController].Points += Config.PointSettings.TaserKill;
										}
										break;
									}
							}

							int attackerIndex = killerController.UserId ?? 0;
							if (playerKillStreaks.ContainsKey(attackerIndex))
							{
								// Check if the player got a kill within the last 5 seconds
								if (playerKillStreaks[attackerIndex].killStreak > 0 && DateTime.Now - playerKillStreaks[attackerIndex].lastKillTime <= TimeSpan.FromSeconds(Config.PointSettings.SecondsBetweenKills))
								{
									playerKillStreaks[attackerIndex] = (playerKillStreaks[attackerIndex].killStreak + 1, DateTime.Now);
									int killStreak = playerKillStreaks[attackerIndex].killStreak;

									// Award points for the kill streak
									int points = 0;
									string killStreakMessage = "";

									switch (killStreak)
									{
										case 2:
											points = Config.PointSettings.DoubleKill;
											killStreakMessage = "Double Kill";
											break;
										case 3:
											points = Config.PointSettings.TripleKill;
											killStreakMessage = "Triple Kill";
											break;
										case 4:
											points = Config.PointSettings.Domination;
											killStreakMessage = "Domination";
											break;
										case 5:
											points = Config.PointSettings.Rampage;
											killStreakMessage = "Rampage";
											break;
										case 6:
											points = Config.PointSettings.MegaKill;
											killStreakMessage = "Mega Kill";
											break;
										case 7:
											points = Config.PointSettings.Ownage;
											killStreakMessage = "Ownage";
											break;
										case 8:
											points = Config.PointSettings.UltraKill;
											killStreakMessage = "Ultra Kill";
											break;
										case 9:
											points = Config.PointSettings.KillingSpree;
											killStreakMessage = "Killing Spree";
											break;
										case 10:
											points = Config.PointSettings.MonsterKill;
											killStreakMessage = "Monster Kill";
											break;
										case 11:
											points = Config.PointSettings.Unstoppable;
											killStreakMessage = "Unstoppable";
											break;
										case 12:
											points = Config.PointSettings.GodLike;
											killStreakMessage = "God Like";
											break;
										default:
											ResetKillStreak(attackerIndex);
											break;
									}

									if (points > 0)
									{
										pointChange += points;
										killerController.PrintToChat($" {Config.GeneralSettings.Prefix} {ChatColors.White}Points: {ChatColors.Green}{PlayerSummaries[killerController].Points} {ChatColors.LightYellow}[+{points} {killStreakMessage}]");
										PlayerSummaries[killerController].Points += points;
									}
								}
								else
								{
									ResetKillStreak(attackerIndex);
								}
							}

							if (Config.RankSettings.ScoreboardScoreSync)
								killerController.Score = PlayerSummaries[killerController].Points;

							if (AdminManager.PlayerHasPermissions(killerController, "@k4system/vip/points-multiplier"))
							{
								pointChange = (int)Math.Round(pointChange * Config.RankSettings.VipMultiplier);
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

					if (Config.PointSettings.Assist > 0)
					{
						pointChange += Config.PointSettings.Assist;
						assisterController.PrintToChat($" {Config.GeneralSettings.Prefix} {ChatColors.White}Points: {ChatColors.Green}{PlayerSummaries[assisterController].Points} [+{Config.PointSettings.Assist} Assist]");
						PlayerSummaries[assisterController].Points += Config.PointSettings.Assist;
					}

					if (@event.Assistedflash && Config.PointSettings.AssistFlash > 0)
					{
						pointChange += Config.PointSettings.AssistFlash;
						assisterController.PrintToChat($" {Config.GeneralSettings.Prefix} {ChatColors.White}Points: {ChatColors.Green}{PlayerSummaries[assisterController].Points} [+{Config.PointSettings.AssistFlash} Flash Assist]");
						PlayerSummaries[assisterController].Points += Config.PointSettings.AssistFlash;
					}

					if (Config.RankSettings.ScoreboardScoreSync)
						assisterController.Score = PlayerSummaries[assisterController].Points;

					if (AdminManager.PlayerHasPermissions(assisterController, "@k4system/vip/points-multiplier"))
					{
						pointChange = (int)Math.Round(pointChange * Config.RankSettings.VipMultiplier);
					}

					MySql!.ExecuteNonQueryAsync($"UPDATE `k4ranks` SET `points` = (`points` + {pointChange}) WHERE `steam_id` = {assisterController.SteamID};");
				}

				return HookResult.Continue;
			});
		}
	}
}