
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

					RankData playerData = rankCache[player];

					if (playerData.Rank.Permissions != null && playerData.Rank.Permissions.Count > 0)
					{
						foreach (Permission permission in playerData.Rank.Permissions)
						{
							AdminManager.RemovePlayerPermissions(Utilities.GetPlayerFromSlot(player.Slot), permission.PermissionName);
						}
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

						ModifyPlayerPoints(player, Config.PointSettings.PlaytimePoints, "k4.phrases.playtime");
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

				ModifyPlayerPoints(player, Config.PointSettings.HostageRescue, "k4.phrases.hostagerescued");

				return HookResult.Continue;
			});

			plugin.RegisterEventHandler((EventHostageKilled @event, GameEventInfo info) =>
			{
				CCSPlayerController player = @event.Userid;

				if (player is null || !player.IsValid || !player.PlayerPawn.IsValid || player.IsBot || player.IsHLTV)
					return HookResult.Continue;

				ModifyPlayerPoints(player, Config.PointSettings.HostageKill, "k4.phrases.hostagekilled");

				return HookResult.Continue;
			});

			plugin.RegisterEventHandler((EventHostageHurt @event, GameEventInfo info) =>
			{
				CCSPlayerController player = @event.Userid;

				if (player is null || !player.IsValid || !player.PlayerPawn.IsValid || player.IsBot || player.IsHLTV)
					return HookResult.Continue;

				ModifyPlayerPoints(player, Config.PointSettings.HostageHurt, "k4.phrases.hostagehurt");

				return HookResult.Continue;
			});

			plugin.RegisterEventHandler((EventBombPickup @event, GameEventInfo info) =>
			{
				CCSPlayerController player = @event.Userid;

				if (player is null || !player.IsValid || !player.PlayerPawn.IsValid || player.IsBot || player.IsHLTV)
					return HookResult.Continue;

				ModifyPlayerPoints(player, Config.PointSettings.BombPickup, "k4.phrases.bombpickup");

				return HookResult.Continue;
			});

			plugin.RegisterEventHandler((EventBombDefused @event, GameEventInfo info) =>
			{
				CCSPlayerController player = @event.Userid;

				if (player is null || !player.IsValid || !player.PlayerPawn.IsValid || player.IsBot || player.IsHLTV)
					return HookResult.Continue;

				ModifyPlayerPoints(player, Config.PointSettings.BombDefused, "k4.phrases.bombdefused");

				return HookResult.Continue;
			});

			plugin.RegisterEventHandler((EventBombDropped @event, GameEventInfo info) =>
			{
				CCSPlayerController player = @event.Userid;

				if (player is null || !player.IsValid || !player.PlayerPawn.IsValid || player.IsBot || player.IsHLTV)
					return HookResult.Continue;

				ModifyPlayerPoints(player, Config.PointSettings.BombDrop, "k4.phrases.bombdropped");

				return HookResult.Continue;
			});

			plugin.RegisterEventHandler((EventBombExploded @event, GameEventInfo info) =>
			{
				CCSPlayerController player = @event.Userid;

				if (player is null || !player.IsValid || !player.PlayerPawn.IsValid || player.IsBot || player.IsHLTV)
					return HookResult.Continue;

				ModifyPlayerPoints(player, Config.PointSettings.BombExploded, "k4.phrases.bombexploded");

				return HookResult.Continue;
			});

			plugin.RegisterEventHandler((EventRoundMvp @event, GameEventInfo info) =>
			{
				CCSPlayerController player = @event.Userid;

				if (player is null || !player.IsValid || !player.PlayerPawn.IsValid || player.IsBot || player.IsHLTV)
					return HookResult.Continue;

				ModifyPlayerPoints(player, Config.PointSettings.MVP, "k4.phrases.mvp");

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
						ModifyPlayerPoints(player, Config.PointSettings.RoundWin, "k4.phrases.roundwin");
					}
					else
					{
						ModifyPlayerPoints(player, Config.PointSettings.RoundLose, "k4.phrases.roundlose");
					}

					if (Config.RankSettings.RoundEndPoints)
					{
						RankData playerData = rankCache[player];

						if (playerData.RoundPoints > 0)
						{
							player.PrintToChat($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.ranks.summarypoints.gain", playerData.RoundPoints]}");
						}
						else if (playerData.RoundPoints < 0)
						{
							player.PrintToChat($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.ranks.summarypoints.loss", Math.Abs(playerData.RoundPoints)]}");
						}
						else
							player.PrintToChat($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.ranks.summarypoints.nochange"]}");
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

				ModifyPlayerPoints(player, Config.PointSettings.BombPlant, "k4.phrases.bombplanted");

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
						ModifyPlayerPoints(victim, Config.PointSettings.Suicide, "k4.phrases.suicide");
					}
					else if (killer != null && killer.IsValid && (Config.RankSettings.PointsForBots || !killer.IsBot))
					{
						ModifyPlayerPoints(victim, CalculateDynamicPoints(victim, killer, Config.PointSettings.Death), "k4.phrases.dying");
					}
				}

				if (killer != null && killer.IsValid && killer.PlayerPawn.IsValid && !killer.IsBot && victim.UserId != killer.UserId && (Config.RankSettings.PointsForBots || !victim.IsBot))
				{
					if (!Config.GeneralSettings.FFAMode && killer.TeamNum == victim.TeamNum)
					{
						ModifyPlayerPoints(killer, Config.PointSettings.TeamKill, "k4.phrases.teamkill");
					}
					else
					{
						ModifyPlayerPoints(killer, CalculateDynamicPoints(killer, victim, Config.PointSettings.Kill), "k4.phrases.kill");

						if (@event.Headshot)
						{
							ModifyPlayerPoints(killer, Config.PointSettings.Headshot, "k4.phrases.headshot");
						}

						int penetrateCount = @event.Penetrated;
						if (penetrateCount > 0 && Config.PointSettings.Penetrated > 0)
						{
							int calculatedPoints = penetrateCount * Config.PointSettings.Penetrated;
							ModifyPlayerPoints(killer, calculatedPoints, "k4.phrases.penetrated");
						}

						if (@event.Noscope)
						{
							ModifyPlayerPoints(killer, Config.PointSettings.NoScope, "k4.phrases.noscope");
						}

						if (@event.Thrusmoke)
						{
							ModifyPlayerPoints(killer, Config.PointSettings.Thrusmoke, "k4.phrases.thrusmoke");
						}

						if (@event.Attackerblind)
						{
							ModifyPlayerPoints(killer, Config.PointSettings.BlindKill, "k4.phrases.blindkill");
						}

						if (@event.Distance >= Config.PointSettings.LongDistance)
						{
							ModifyPlayerPoints(killer, Config.PointSettings.LongDistanceKill, "k4.phrases.longdistance");
						}

						string lowerCaseWeaponName = @event.Weapon.ToLower();

						switch (lowerCaseWeaponName)
						{
							case var _ when lowerCaseWeaponName.Contains("grenade") || lowerCaseWeaponName.Contains("firebomb") || lowerCaseWeaponName.Contains("molotov") || lowerCaseWeaponName.Contains("flashbang") || lowerCaseWeaponName.Contains("bumpmine"):
								{
									ModifyPlayerPoints(killer, Config.PointSettings.GrenadeKill, "k4.phrases.grenadekill");
									break;
								}
							case var _ when lowerCaseWeaponName.Contains("knife_") || lowerCaseWeaponName.Contains("bayonet"):
								{
									ModifyPlayerPoints(killer, Config.PointSettings.KnifeKill, "k4.phrases.knifekill");
									break;
								}
							case "taser":
								{
									ModifyPlayerPoints(killer, Config.PointSettings.TaserKill, "k4.phrases.taserkill");
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
									{ 2, (Config.PointSettings.DoubleKill, "k4.phrases.doublekill") },
									{ 3, (Config.PointSettings.TripleKill, "k4.phrases.triplekill") },
									{ 4, (Config.PointSettings.Domination, "k4.phrases.domination") },
									{ 5, (Config.PointSettings.Rampage, "k4.phrases.rampage") },
									{ 6, (Config.PointSettings.MegaKill, "k4.phrases.megakill") },
									{ 7, (Config.PointSettings.Ownage, "k4.phrases.ownage") },
									{ 8, (Config.PointSettings.UltraKill, "k4.phrases.ultrakill") },
									{ 9, (Config.PointSettings.KillingSpree, "k4.phrases.killingspree") },
									{ 10, (Config.PointSettings.MonsterKill, "k4.phrases.monsterkill") },
									{ 11, (Config.PointSettings.Unstoppable, "k4.phrases.unstoppable") },
									{ 12, (Config.PointSettings.GodLike, "k4.phrases.godlike") }
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
					ModifyPlayerPoints(assiter, Config.PointSettings.Assist, "k4.phrases.assist");

					if (@event.Assistedflash)
					{
						ModifyPlayerPoints(assiter, Config.PointSettings.AssistFlash, "k4.phrases.assistflash");
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