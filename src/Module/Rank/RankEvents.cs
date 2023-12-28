
namespace K4System
{
	using CounterStrikeSharp.API;
	using CounterStrikeSharp.API.Core;
	using CounterStrikeSharp.API.Modules.Admin;
	using CounterStrikeSharp.API.Modules.Timers;
	using CounterStrikeSharp.API.Modules.Utils;

	public partial class ModuleRank : IModuleRank
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
					await LoadRankData(slot, playerName, steamId);

					if (rankCache[slot].Rank.Permissions != null)
					{
						AdminManager.AddPlayerPermissions(Utilities.GetPlayerFromSlot(slot), rankCache[slot].Rank.Permissions!.ToArray());
					}
				});

				return HookResult.Continue;
			});

			plugin.RegisterListener<Listeners.OnMapStart>((mapName) =>
			{
				globalGameRules = null;

				plugin.AddTimer(Config.PointSettings.PlaytimeMinutes * 60, () =>
				{
					List<CCSPlayerController> players = Utilities.GetPlayers();

					foreach (CCSPlayerController player in players)
					{
						if (player is null || !player.IsValid || !player.PlayerPawn.IsValid || player.IsBot || player.IsHLTV)
							continue;

						if (!rankCache.ContainsPlayer(player))
							continue;

						ModifyPlayerPoints(player, Config.PointSettings.PlaytimePoints, "Playtime");
					}
				}, TimerFlags.STOP_ON_MAPCHANGE | TimerFlags.REPEAT);
			});

			plugin.RegisterListener<Listeners.OnMapEnd>(() =>
			{
				SaveAllPlayerCache(false);
			});

			plugin.RegisterEventHandler((EventPlayerSpawn @event, GameEventInfo info) =>
			{
				CCSPlayerController player = @event.Userid;

				if (player is null || !player.IsValid || !player.PlayerPawn.IsValid || player.IsBot || player.IsHLTV)
					return HookResult.Continue;

				if (!rankCache.ContainsPlayer(player))
					return HookResult.Continue;

				rankCache[player].PlayedRound = true;

				return HookResult.Continue;
			});

			plugin.RegisterEventHandler((EventHostageRescued @event, GameEventInfo info) =>
			{
				CCSPlayerController player = @event.Userid;

				if (player is null || !player.IsValid || !player.PlayerPawn.IsValid || player.IsBot || player.IsHLTV)
					return HookResult.Continue;

				ModifyPlayerPoints(player, Config.PointSettings.HostageRescue, "Hostage Rescued");

				return HookResult.Continue;
			});

			plugin.RegisterEventHandler((EventHostageKilled @event, GameEventInfo info) =>
			{
				CCSPlayerController player = @event.Userid;

				if (player is null || !player.IsValid || !player.PlayerPawn.IsValid || player.IsBot || player.IsHLTV)
					return HookResult.Continue;

				ModifyPlayerPoints(player, Config.PointSettings.HostageKill, "Hostage Killed");

				return HookResult.Continue;
			});

			plugin.RegisterEventHandler((EventHostageHurt @event, GameEventInfo info) =>
			{
				CCSPlayerController player = @event.Userid;

				if (player is null || !player.IsValid || !player.PlayerPawn.IsValid || player.IsBot || player.IsHLTV)
					return HookResult.Continue;

				ModifyPlayerPoints(player, Config.PointSettings.HostageHurt, "Hostage Hurt");

				return HookResult.Continue;
			});

			plugin.RegisterEventHandler((EventBombPickup @event, GameEventInfo info) =>
			{
				CCSPlayerController player = @event.Userid;

				if (player is null || !player.IsValid || !player.PlayerPawn.IsValid || player.IsBot || player.IsHLTV)
					return HookResult.Continue;

				ModifyPlayerPoints(player, Config.PointSettings.BombPickup, "Bomb Pickup");

				return HookResult.Continue;
			});

			plugin.RegisterEventHandler((EventBombDefused @event, GameEventInfo info) =>
			{
				CCSPlayerController player = @event.Userid;

				if (player is null || !player.IsValid || !player.PlayerPawn.IsValid || player.IsBot || player.IsHLTV)
					return HookResult.Continue;

				ModifyPlayerPoints(player, Config.PointSettings.BombDefused, "Bomb Defused");

				return HookResult.Continue;
			});

			plugin.RegisterEventHandler((EventBombDropped @event, GameEventInfo info) =>
			{
				CCSPlayerController player = @event.Userid;

				if (player is null || !player.IsValid || !player.PlayerPawn.IsValid || player.IsBot || player.IsHLTV)
					return HookResult.Continue;

				ModifyPlayerPoints(player, Config.PointSettings.BombDrop, "Bomb Dropped");

				return HookResult.Continue;
			});

			plugin.RegisterEventHandler((EventBombExploded @event, GameEventInfo info) =>
			{
				CCSPlayerController player = @event.Userid;

				if (player is null || !player.IsValid || !player.PlayerPawn.IsValid || player.IsBot || player.IsHLTV)
					return HookResult.Continue;

				ModifyPlayerPoints(player, Config.PointSettings.BombExploded, "Bomb Exploded");

				return HookResult.Continue;
			});

			plugin.RegisterEventHandler((EventRoundMvp @event, GameEventInfo info) =>
			{
				CCSPlayerController player = @event.Userid;

				if (player is null || !player.IsValid || !player.PlayerPawn.IsValid || player.IsBot || player.IsHLTV)
					return HookResult.Continue;

				ModifyPlayerPoints(player, Config.PointSettings.MVP, "MVP");

				return HookResult.Continue;
			});

			plugin.RegisterEventHandler((EventRoundEnd @event, GameEventInfo info) =>
			{
				int winnerTeam = @event.Winner;

				List<CCSPlayerController> players = Utilities.GetPlayers();

				foreach (CCSPlayerController player in players)
				{
					if (player is null || !player.IsValid || !player.PlayerPawn.IsValid || player.IsBot || player.IsHLTV)
						continue;

					if (!rankCache.ContainsPlayer(player))
						continue;

					if (!rankCache[player].PlayedRound)
						continue;

					if (player.TeamNum <= (int)CsTeam.Spectator)
						continue;

					if (winnerTeam == player.TeamNum)
					{
						ModifyPlayerPoints(player, Config.PointSettings.RoundWin, "Round Win");
					}
					else
					{
						ModifyPlayerPoints(player, Config.PointSettings.RoundLose, "Round Lose");
					}

					if (Config.RankSettings.RoundEndPoints)
					{
						RankData playerData = rankCache[player];

						if (playerData.RoundPoints > 0)
						{
							player.PrintToChat($" {Config.GeneralSettings.Prefix} {ChatColors.Silver}Points: {ChatColors.Green}+{playerData.RoundPoints} Round Summary");
						}
						else if (playerData.RoundPoints < 0)
						{
							player.PrintToChat($" {Config.GeneralSettings.Prefix} {ChatColors.Silver}Points: {ChatColors.Red}-{Math.Abs(playerData.RoundPoints)} Round Summary");
						}
						else
							player.PrintToChat($" {Config.GeneralSettings.Prefix} {ChatColors.Silver}No point changes in this round");
					}
				}

				SaveAllPlayerCache(false);

				return HookResult.Continue;
			});

			plugin.RegisterEventHandler((EventBombPlanted @event, GameEventInfo info) =>
			{
				CCSPlayerController player = @event.Userid;

				if (player is null || !player.IsValid || !player.PlayerPawn.IsValid || player.IsBot || player.IsHLTV)
					return HookResult.Continue;

				ModifyPlayerPoints(player, Config.PointSettings.BombPlant, "Bomb Planted");

				return HookResult.Continue;
			});

			plugin.RegisterEventHandler((EventPlayerDeath @event, GameEventInfo info) =>
			{
				if (!IsPointsAllowed())
				{
					return HookResult.Continue;
				}

				CCSPlayerController victim = @event.Userid;

				if (victim is null || !victim.IsValid || !victim.PlayerPawn.IsValid || !victim.UserId.HasValue || victim.UserId == -1)
					return HookResult.Continue;

				CCSPlayerController killer = @event.Attacker;

				if (!victim.IsBot)
				{
					if (killer is null || !killer.IsValid || victim.UserId == killer.UserId)
					{
						ModifyPlayerPoints(victim, Config.PointSettings.Suicide, "Suicide");
					}
					else if (killer != null && killer.IsValid && (Config.RankSettings.PointsForBots || !killer.IsBot))
					{
						ModifyPlayerPoints(victim, CalculateDynamicPoints(victim, killer, Config.PointSettings.Death), "Dying");
					}
				}

				if (killer != null && killer.IsValid && killer.PlayerPawn.IsValid && !killer.IsBot && victim.UserId != killer.UserId && (Config.RankSettings.PointsForBots || !victim.IsBot))
				{
					if (!Config.GeneralSettings.FFAMode && killer.TeamNum == victim.TeamNum)
					{
						ModifyPlayerPoints(killer, Config.PointSettings.TeamKill, "TeamKill");
					}
					else
					{
						ModifyPlayerPoints(killer, CalculateDynamicPoints(killer, victim, Config.PointSettings.Kill), "Kill");

						if (@event.Headshot)
						{
							ModifyPlayerPoints(killer, Config.PointSettings.Headshot, "Headshot");
						}

						int penetrateCount = @event.Penetrated;
						if (penetrateCount > 0 && Config.PointSettings.Penetrated > 0)
						{
							int calculatedPoints = penetrateCount * Config.PointSettings.Penetrated;
							ModifyPlayerPoints(killer, calculatedPoints, "Penetrated");
						}

						if (@event.Noscope)
						{
							ModifyPlayerPoints(killer, Config.PointSettings.NoScope, "NoScope");
						}

						if (@event.Thrusmoke)
						{
							ModifyPlayerPoints(killer, Config.PointSettings.Thrusmoke, "ThruSmoke");
						}

						if (@event.Attackerblind)
						{
							ModifyPlayerPoints(killer, Config.PointSettings.BlindKill, "Blind Kill");
						}

						if (@event.Distance >= Config.PointSettings.LongDistance)
						{
							ModifyPlayerPoints(killer, Config.PointSettings.LongDistanceKill, "Long Distance");
						}

						string lowerCaseWeaponName = @event.Weapon.ToLower();

						switch (lowerCaseWeaponName)
						{
							case var _ when lowerCaseWeaponName.Contains("grenade") || lowerCaseWeaponName.Contains("firebomb") || lowerCaseWeaponName.Contains("molotov") || lowerCaseWeaponName.Contains("flashbang") || lowerCaseWeaponName.Contains("bumpmine"):
								{
									ModifyPlayerPoints(killer, Config.PointSettings.GrenadeKill, "Grenade Kill");
									break;
								}
							case var _ when lowerCaseWeaponName.Contains("knife_") || lowerCaseWeaponName.Contains("bayonet"):
								{
									ModifyPlayerPoints(killer, Config.PointSettings.KnifeKill, "Knife Kill");
									break;
								}
							case "taser":
								{
									ModifyPlayerPoints(killer, Config.PointSettings.TaserKill, "Taser Kill");
									break;
								}
						}

						int attackerIndex = (killer.UserId != null) ? (int)killer.UserId : -1;

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
									ModifyPlayerPoints(killer, killStreakInfo.points, killStreakInfo.message);
								}
								else
									playerKillStreaks[attackerIndex] = (1, DateTime.Now);
							}
							else
								playerKillStreaks[attackerIndex] = (1, DateTime.Now);
						}
						else
							playerKillStreaks[attackerIndex] = (1, DateTime.Now);
					}
				}

				CCSPlayerController assiter = @event.Assister;

				if (assiter != null && assiter.IsValid && assiter.PlayerPawn.IsValid && !assiter.IsBot && (Config.RankSettings.PointsForBots || !victim.IsBot))
				{
					ModifyPlayerPoints(assiter, Config.PointSettings.Assist, "Assist");

					if (@event.Assistedflash)
					{
						ModifyPlayerPoints(assiter, Config.PointSettings.AssistFlash, "Assist Flash");
					}
				}

				return HookResult.Continue;
			});

			plugin.RegisterEventHandler((EventPlayerDisconnect @event, GameEventInfo info) =>
			{
				CCSPlayerController player = @event.Userid;

				if (player is null || !player.IsValid || !player.PlayerPawn.IsValid)
					return HookResult.Continue;

				if (player.IsBot || player.IsHLTV)
					return HookResult.Continue;

				SavePlayerRankCache(player, true);

				return HookResult.Continue;
			});
		}
	}
}