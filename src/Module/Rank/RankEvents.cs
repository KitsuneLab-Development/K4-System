
namespace K4System
{
	using CounterStrikeSharp.API;
	using CounterStrikeSharp.API.Core;
	using CounterStrikeSharp.API.Modules.Admin;
	using CounterStrikeSharp.API.Modules.Memory;
	using CounterStrikeSharp.API.Modules.Utils;

	public partial class ModuleRank : IModuleRank
	{
		public void Initialize_Events(Plugin plugin)
		{
			VirtualFunctions.CCSPlayerPawnBase_PostThinkFunc.Hook(_ =>
			{
				if (Config.RankSettings.ScoreboardRanks == 0)
					return HookResult.Continue;

				Utilities.GetPlayers().Where(p => p?.IsValid == true && p.PlayerPawn?.IsValid == true && !p.IsBot && !p.IsHLTV && p.SteamID.ToString().Length == 17)
					.ToList()
					.ForEach(p =>
					{
						if (!PlayerCache.Instance.ContainsPlayer(p))
							return;

						RankData? rankData = PlayerCache.Instance.GetPlayerData(p).rankData;

						if (rankData is null)
							return;

						p.CompetitiveRankType = (sbyte)(Config.RankSettings.ScoreboardRanks == 1 ? 11 : 12);
						p.CompetitiveRanking = Config.RankSettings.ScoreboardRanks == 1 ? rankData.Points : rankData.Rank.Id + 1 >= 19 ? 18 : rankData.Rank.Id + 1;
					});

				return HookResult.Continue;
			}, HookMode.Post);

			plugin.RegisterEventHandler((EventPlayerTeam @event, GameEventInfo info) =>
			{
				CCSPlayerController player = @event.Userid;

				if (player is null || !player.IsValid || !player.PlayerPawn.IsValid || player.IsBot || player.IsHLTV || !PlayerCache.Instance.ContainsPlayer(player))
					return HookResult.Continue;

				RankData? rankData = PlayerCache.Instance.GetPlayerData(player).rankData;

				if (rankData is null)
					return HookResult.Continue;

				if (rankData.Rank.Permissions != null && rankData.Rank.Permissions.Count > 0)
				{
					foreach (Permission permission in rankData.Rank.Permissions)
					{
						AdminManager.AddPlayerPermissions(Utilities.GetPlayerFromSlot(player.Slot), permission.PermissionName);
					}
				}

				if (!@event.Disconnect && @event.Team != @event.Oldteam)
				{
					rankData.PlayedRound = false;
				}

				if (Config.RankSettings.ScoreboardClantags)
				{
					string tag = rankData.Rank.Tag ?? $"[{rankData.Rank.Name}]";
					SetPlayerClanTag(player, rankData, tag);
				}

				return HookResult.Continue;
			});

			plugin.RegisterEventHandler((EventPlayerSpawn @event, GameEventInfo info) =>
			{
				CCSPlayerController player = @event.Userid;

				if (player is null || !player.IsValid || !player.PlayerPawn.IsValid || player.IsBot || player.IsHLTV)
					return HookResult.Continue;

				if (!PlayerCache.Instance.ContainsPlayer(player))
					return HookResult.Continue;

				if (Config.RankSettings.ScoreboardClantags)
				{
					RankData? rankData = PlayerCache.Instance.GetPlayerData(player).rankData;

					if (rankData is null)
						return HookResult.Continue;

					string tag = rankData.Rank.Tag ?? $"[{rankData.Rank.Name}]";
					SetPlayerClanTag(player, rankData, tag);
				}

				return HookResult.Continue;
			});

			plugin.RegisterEventHandler((EventHostageRescued @event, GameEventInfo info) =>
			{
				CCSPlayerController player = @event.Userid;
				ModifyPlayerPoints(player, Config.PointSettings.HostageRescue, "k4.phrases.hostagerescued");
				return HookResult.Continue;
			});

			plugin.RegisterEventHandler((EventHostageKilled @event, GameEventInfo info) =>
			{
				CCSPlayerController player = @event.Userid;
				ModifyPlayerPoints(player, Config.PointSettings.HostageKill, "k4.phrases.hostagekilled");
				return HookResult.Continue;
			});

			plugin.RegisterEventHandler((EventHostageHurt @event, GameEventInfo info) =>
			{
				CCSPlayerController player = @event.Userid;
				ModifyPlayerPoints(player, Config.PointSettings.HostageHurt, "k4.phrases.hostagehurt");
				return HookResult.Continue;
			});

			plugin.RegisterEventHandler((EventBombPickup @event, GameEventInfo info) =>
			{
				CCSPlayerController player = @event.Userid;
				ModifyPlayerPoints(player, Config.PointSettings.BombPickup, "k4.phrases.bombpickup");
				return HookResult.Continue;
			});

			plugin.RegisterEventHandler((EventBombDefused @event, GameEventInfo info) =>
			{
				CCSPlayerController player = @event.Userid;
				ModifyPlayerPoints(player, Config.PointSettings.BombDefused, "k4.phrases.bombdefused");

				Utilities.GetPlayers().Where(p => p.TeamNum == (int)CsTeam.CounterTerrorist && p.Slot != player.Slot)
					.ToList()
					.ForEach(p => ModifyPlayerPoints(p, Config.PointSettings.BombDefusedOthers, "k4.phrases.bombdefused"));

				return HookResult.Continue;
			});

			plugin.RegisterEventHandler((EventBombDropped @event, GameEventInfo info) =>
			{
				CCSPlayerController player = @event.Userid;
				ModifyPlayerPoints(player, Config.PointSettings.BombDrop, "k4.phrases.bombdropped");
				return HookResult.Continue;
			});

			plugin.RegisterEventHandler((EventBombExploded @event, GameEventInfo info) =>
			{
				Utilities.GetPlayers().Where(p => p.TeamNum == (int)CsTeam.Terrorist)
					.ToList()
					.ForEach(p => ModifyPlayerPoints(p, Config.PointSettings.BombExploded, "k4.phrases.bombexploded"));

				return HookResult.Continue;
			});

			plugin.RegisterEventHandler((EventHostageRescuedAll @event, GameEventInfo info) =>
			{
				Utilities.GetPlayers().Where(p => p.TeamNum == (int)CsTeam.CounterTerrorist)
					.ToList()
					.ForEach(p => ModifyPlayerPoints(p, Config.PointSettings.HostageRescueAll, "k4.phrases.hostagerescuedall"));

				return HookResult.Continue;
			});

			plugin.RegisterEventHandler((EventRoundMvp @event, GameEventInfo info) =>
			{
				CCSPlayerController player = @event.Userid;
				ModifyPlayerPoints(player, Config.PointSettings.MVP, "k4.phrases.mvp");
				return HookResult.Continue;
			});

			plugin.RegisterEventHandler((EventRoundStart @event, GameEventInfo info) =>
			{
				List<CCSPlayerController> players = Utilities.GetPlayers().Where(p => p?.IsValid == true && p.PlayerPawn?.IsValid == true && !p.IsBot && !p.IsHLTV && p.SteamID.ToString().Length == 17).ToList();

				players.Where(p => p.PawnIsAlive && PlayerCache.Instance.ContainsPlayer(p))
					.ToList()
					.ForEach(p =>
					{
						RankData? rankData = PlayerCache.Instance.GetPlayerData(p).rankData;
						if (rankData is not null)
						{
							rankData.PlayedRound = true;
						}
					});

				if (players.Count < Config.RankSettings.MinPlayers)
					Server.PrintToChatAll($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.ranks.notenoughplayers", Config.RankSettings.MinPlayers]}");

				return HookResult.Continue;
			}, HookMode.Post);

			plugin.RegisterEventHandler((EventBombPlanted @event, GameEventInfo info) =>
			{
				CCSPlayerController player = @event.Userid;
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

				if (victim is null || !victim.IsValid || !victim.PlayerPawn.IsValid || victim.Connected == PlayerConnectedState.PlayerDisconnecting)
					return HookResult.Continue;

				CCSPlayerController killer = @event.Attacker;

				if (!victim.IsBot)
				{
					playerKillStreaks[victim.Slot] = (0, DateTime.Now);

					if (killer is null || !killer.IsValid || victim.UserId == killer.UserId)
					{
						ModifyPlayerPoints(victim, Config.PointSettings.Suicide, "k4.phrases.suicide");
					}
					else if (killer != null && killer.IsValid && (Config.RankSettings.PointsForBots || !killer.IsBot))
					{
						string? extraInfo = Config.RankSettings.PlayerNameKillMessages ? plugin.Localizer["k4.phrases.dying.extra", killer.PlayerName] : null!;
						ModifyPlayerPoints(victim, CalculateDynamicPoints(killer, victim, Config.PointSettings.Death), "k4.phrases.dying", extraInfo);
					}
				}

				if (killer?.IsValid == true && killer.PlayerPawn?.IsValid == true && !killer.IsBot && victim.UserId != killer.UserId && (Config.RankSettings.PointsForBots || !victim.IsBot))
				{
					if (!Config.GeneralSettings.FFAMode && killer.TeamNum == victim.TeamNum)
					{
						ModifyPlayerPoints(killer, Config.PointSettings.TeamKill, "k4.phrases.teamkill");
					}
					else
					{
						string? extraInfo = Config.RankSettings.PlayerNameKillMessages ? plugin.Localizer["k4.phrases.kill.extra", victim.PlayerName] : null!;
						ModifyPlayerPoints(killer, CalculateDynamicPoints(killer, victim, Config.PointSettings.Kill), "k4.phrases.kill", extraInfo);

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
							//Killed by grenade explosion
							case var _ when lowerCaseWeaponName.Contains("hegrenade"):
								{
									ModifyPlayerPoints(killer, Config.PointSettings.GrenadeKill, "k4.phrases.grenadekill");
									break;
								}
							//Molotov or Incendiary fire kill
							case var _ when lowerCaseWeaponName.Contains("inferno"):
								{
									ModifyPlayerPoints(killer, Config.PointSettings.InfernoKill, "k4.phrases.infernokill");
									break;
								}
							// Grenade impact kill (hitting a player and killing them with a grenade when they are 1hp for example)
							case var _ when lowerCaseWeaponName.Contains("grenade") || lowerCaseWeaponName.Contains("molotov") || lowerCaseWeaponName.Contains("flashbang") || lowerCaseWeaponName.Contains("bumpmine"):
								{
									ModifyPlayerPoints(killer, Config.PointSettings.ImpactKill, "k4.phrases.impactkill");
									break;
								}
							// knife_ would not handle default knives (therefore changed to just "knife"), this will also not handle other Danger Zone items such as axes and wrenches (if they are even implemented yet in CS2)
							case var _ when lowerCaseWeaponName.Contains("knife") || lowerCaseWeaponName.Contains("bayonet"):
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

						if (playerKillStreaks.ContainsKey(killer.Slot))
						{
							int time = Config.PointSettings.SecondsBetweenKills;
							bool isTimeBetweenKills = time <= 0 || DateTime.Now - playerKillStreaks[killer.Slot].lastKillTime <= TimeSpan.FromSeconds(time);

							if (playerKillStreaks[killer.Slot].killStreak > 0 && isTimeBetweenKills)
							{
								playerKillStreaks[killer.Slot] = (playerKillStreaks[killer.Slot].killStreak + 1, DateTime.Now);
								int killStreak = playerKillStreaks[killer.Slot].killStreak;

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
									playerKillStreaks[killer.Slot] = (1, DateTime.Now);
							}
							else
								playerKillStreaks[killer.Slot] = (1, DateTime.Now);
						}
						else
							playerKillStreaks[killer.Slot] = (1, DateTime.Now);
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
		}
	}
}