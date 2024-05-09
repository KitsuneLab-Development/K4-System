
namespace K4System
{
	using CounterStrikeSharp.API;
	using CounterStrikeSharp.API.Core;
	using CounterStrikeSharp.API.Modules.Admin;
	using CounterStrikeSharp.API.Modules.Memory;
	using CounterStrikeSharp.API.Modules.Utils;
	using K4System.Models;

	public partial class ModuleRank : IModuleRank
	{
		public void Initialize_Events()
		{
			plugin.RegisterEventHandler((EventPlayerTeam @event, GameEventInfo info) =>
			{
				K4Player? k4player = plugin.GetK4Player(@event.Userid);
				if (k4player is null || !k4player.IsValid || !k4player.IsPlayer)
					return HookResult.Continue;

				RankData? rankData = k4player.rankData;

				if (rankData is null)
					return HookResult.Continue;

				if (rankData.Rank.Permissions != null && rankData.Rank.Permissions.Count > 0)
				{
					foreach (Permission permission in rankData.Rank.Permissions)
					{
						AdminManager.AddPlayerPermissions(k4player.Controller, permission.PermissionName);
					}
				}

				if (!@event.Disconnect && @event.Team != @event.Oldteam)
				{
					rankData.PlayedRound = false;
				}

				SetPlayerClanTag(k4player);

				return HookResult.Continue;
			});

			plugin.RegisterEventHandler((EventPlayerSpawn @event, GameEventInfo info) =>
			{
				K4Player? k4player = plugin.GetK4Player(@event.Userid);
				if (k4player is null || !k4player.IsValid || !k4player.IsPlayer)
					return HookResult.Continue;

				SetPlayerClanTag(k4player);
				return HookResult.Continue;
			});

			plugin.RegisterEventHandler((EventHostageRescued @event, GameEventInfo info) =>
			{
				ModifyPlayerPointsConnector(@event.Userid, Config.PointSettings.HostageRescue, "k4.phrases.hostagerescued");
				return HookResult.Continue;
			});

			plugin.RegisterEventHandler((EventHostageKilled @event, GameEventInfo info) =>
			{
				ModifyPlayerPointsConnector(@event.Userid, Config.PointSettings.HostageKill, "k4.phrases.hostagekilled");
				return HookResult.Continue;
			});

			plugin.RegisterEventHandler((EventHostageHurt @event, GameEventInfo info) =>
			{
				ModifyPlayerPointsConnector(@event.Userid, Config.PointSettings.HostageHurt, "k4.phrases.hostagehurt");
				return HookResult.Continue;
			});

			plugin.RegisterEventHandler((EventBombPickup @event, GameEventInfo info) =>
			{
				ModifyPlayerPointsConnector(@event.Userid, Config.PointSettings.BombPickup, "k4.phrases.bombpickup");
				return HookResult.Continue;
			});

			plugin.RegisterEventHandler((EventBombDefused @event, GameEventInfo info) =>
			{
				K4Player? k4player = plugin.GetK4Player(@event.Userid);
				if (k4player is null || !k4player.IsValid || !k4player.IsPlayer)
					return HookResult.Continue;

				ModifyPlayerPoints(k4player, Config.PointSettings.BombDefused, "k4.phrases.bombdefused");

				var players = plugin.K4Players.Where(p => p.IsValid && p.IsPlayer && p.Controller.Team == CsTeam.CounterTerrorist && p != k4player);
				foreach (K4Player player in players)
				{
					ModifyPlayerPoints(player, Config.PointSettings.BombDefusedOthers, "k4.phrases.bombdefused");
				}

				return HookResult.Continue;
			});

			plugin.RegisterEventHandler((EventBombDropped @event, GameEventInfo info) =>
			{
				ModifyPlayerPointsConnector(@event.Userid, Config.PointSettings.BombDrop, "k4.phrases.bombdropped");
				return HookResult.Continue;
			});

			plugin.RegisterEventHandler((EventBombExploded @event, GameEventInfo info) =>
			{
				foreach (K4Player k4player in plugin.K4Players)
				{
					if (k4player.IsValid && k4player.IsPlayer && k4player.Controller.Team == CsTeam.Terrorist)
					{
						ModifyPlayerPoints(k4player, Config.PointSettings.BombExploded, "k4.phrases.bombexploded");
					}
				}
				return HookResult.Continue;
			});

			plugin.RegisterEventHandler((EventHostageRescuedAll @event, GameEventInfo info) =>
			{
				foreach (K4Player k4player in plugin.K4Players)
				{
					if (k4player.IsValid && k4player.IsPlayer && k4player.Controller.Team == CsTeam.CounterTerrorist)
					{
						ModifyPlayerPoints(k4player, Config.PointSettings.HostageRescueAll, "k4.phrases.hostagerescuedall");
					}
				}
				return HookResult.Continue;
			});

			plugin.RegisterEventHandler((EventRoundMvp @event, GameEventInfo info) =>
			{
				ModifyPlayerPointsConnector(@event.Userid, Config.PointSettings.MVP, "k4.phrases.mvp");
				return HookResult.Continue;
			});

			plugin.RegisterEventHandler((EventRoundStart @event, GameEventInfo info) =>
			{
				foreach (K4Player k4player in plugin.K4Players)
				{
					if (k4player.IsValid && k4player.IsPlayer)
					{
						RankData? rankData = k4player.rankData;
						if (rankData is not null)
						{
							rankData.PlayedRound = true;
						}
					}
				}

				if (plugin.K4Players.Count(p => p.IsValid && p.IsPlayer) < Config.RankSettings.MinPlayers)
				{
					Server.PrintToChatAll($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.ranks.notenoughplayers", Config.RankSettings.MinPlayers]}");
				}

				return HookResult.Continue;
			}, HookMode.Post);

			plugin.RegisterEventHandler((EventRoundPrestart @event, GameEventInfo info) =>
			{
				if (Config.RankSettings.RankBasedTeamBalance)
				{
					int team1Size = plugin.K4Players.Count(p => p.Controller.Team == CsTeam.Terrorist);
					int team2Size = plugin.K4Players.Count(p => p.Controller.Team == CsTeam.CounterTerrorist);

					if (Math.Abs(team1Size - team2Size) > 1)
					{
						var team1Players = plugin.K4Players.Where(p => p.Controller.Team == CsTeam.Terrorist).ToList();
						var team2Players = plugin.K4Players.Where(p => p.Controller.Team == CsTeam.CounterTerrorist).ToList();

						var team1RankPoints = team1Players.Select(p => p.rankData?.Points ?? 0).Sum();
						var team2RankPoints = team2Players.Select(p => p.rankData?.Points ?? 0).Sum();

						while (Math.Abs(team1RankPoints - team2RankPoints) > Config.RankSettings.RankBasedTeamBalanceMaxDifference)
						{
							if (team1RankPoints > team2RankPoints)
							{
								var playerToSwitch = team1Players.OrderByDescending(p => p.rankData?.Points ?? 0).First();
								team1Players.Remove(playerToSwitch);
								team2Players.Add(playerToSwitch);
								playerToSwitch.Controller.ChangeTeam(CsTeam.CounterTerrorist);
							}
							else
							{
								var playerToSwitch = team2Players.OrderByDescending(p => p.rankData?.Points ?? 0).First();
								team2Players.Remove(playerToSwitch);
								team1Players.Add(playerToSwitch);
								playerToSwitch.Controller.ChangeTeam(CsTeam.Terrorist);
							}

							team1RankPoints = team1Players.Select(p => p.rankData?.Points ?? 0).Sum();
							team2RankPoints = team2Players.Select(p => p.rankData?.Points ?? 0).Sum();
						}

						Server.PrintToChatAll($" {plugin.Localizer["k4.general.prefix"]} {plugin.Localizer["k4.ranks.rank.teamsbalanced"]}");
					}
				}

				return HookResult.Continue;
			}, HookMode.Post);

			plugin.RegisterEventHandler((EventBombPlanted @event, GameEventInfo info) =>
			{
				ModifyPlayerPointsConnector(@event.Userid, Config.PointSettings.BombPlant, "k4.phrases.bombplanted");
				return HookResult.Continue;
			});

			plugin.RegisterEventHandler((EventPlayerDeath @event, GameEventInfo info) =>
			{
				if (!IsPointsAllowed())
				{
					return HookResult.Continue;
				}

				K4Player? k4victim = plugin.GetK4Player(@event.Userid);
				if (k4victim is null || !k4victim.IsValid)
					return HookResult.Continue;

				K4Player? k4attacker = plugin.GetK4Player(@event.Attacker);

				if (k4victim.IsPlayer)
				{
					k4victim.KillStreak = (0, DateTime.Now);

					if (k4attacker is null || !k4attacker.IsValid)
					{
						ModifyPlayerPoints(k4victim, Config.PointSettings.Suicide, "k4.phrases.suicide");
					}
					else
					{
						if (Config.RankSettings.PointsForBots || k4attacker.IsPlayer)
						{
							string? extraInfo = Config.RankSettings.PlayerNameKillMessages ? plugin.Localizer["k4.phrases.dying.extra", k4attacker.PlayerName] : null!;
							ModifyPlayerPoints(k4victim, CalculateDynamicPoints(k4attacker, k4victim, Config.PointSettings.Death), "k4.phrases.dying", extraInfo);
						}
					}
				}

				if (k4attacker?.IsValid == true && k4attacker.IsPlayer && (Config.RankSettings.PointsForBots || k4victim.IsPlayer))
				{
					if (!Config.GeneralSettings.FFAMode && k4attacker.Controller.Team == k4victim.Controller.Team)
					{
						ModifyPlayerPoints(k4attacker, Config.PointSettings.TeamKill, "k4.phrases.teamkill");
					}
					else
					{
						string? extraInfo = Config.RankSettings.PlayerNameKillMessages ? plugin.Localizer["k4.phrases.kill.extra", k4victim.PlayerName] : null!;
						ModifyPlayerPoints(k4attacker, CalculateDynamicPoints(k4attacker, k4victim, Config.PointSettings.Kill), "k4.phrases.kill", extraInfo);

						if (@event.Headshot)
						{
							ModifyPlayerPoints(k4attacker, Config.PointSettings.Headshot, "k4.phrases.headshot");
						}

						int penetrateCount = @event.Penetrated;
						if (penetrateCount > 0 && Config.PointSettings.Penetrated > 0)
						{
							int calculatedPoints = penetrateCount * Config.PointSettings.Penetrated;
							ModifyPlayerPoints(k4attacker, calculatedPoints, "k4.phrases.penetrated");
						}

						if (@event.Noscope)
						{
							ModifyPlayerPoints(k4attacker, Config.PointSettings.NoScope, "k4.phrases.noscope");
						}

						if (@event.Thrusmoke)
						{
							ModifyPlayerPoints(k4attacker, Config.PointSettings.Thrusmoke, "k4.phrases.thrusmoke");
						}

						if (@event.Attackerblind)
						{
							ModifyPlayerPoints(k4attacker, Config.PointSettings.BlindKill, "k4.phrases.blindkill");
						}

						if (@event.Distance >= Config.PointSettings.LongDistance)
						{
							ModifyPlayerPoints(k4attacker, Config.PointSettings.LongDistanceKill, "k4.phrases.longdistance");
						}

						string lowerCaseWeaponName = @event.Weapon.ToLower();

						switch (lowerCaseWeaponName)
						{
							//Killed by grenade explosion
							case var _ when lowerCaseWeaponName.Contains("hegrenade"):
								{
									ModifyPlayerPoints(k4attacker, Config.PointSettings.GrenadeKill, "k4.phrases.grenadekill");
									break;
								}
							//Molotov or Incendiary fire kill
							case var _ when lowerCaseWeaponName.Contains("inferno"):
								{
									ModifyPlayerPoints(k4attacker, Config.PointSettings.InfernoKill, "k4.phrases.infernokill");
									break;
								}
							// Grenade impact kill (hitting a player and killing them with a grenade when they are 1hp for example)
							case var _ when lowerCaseWeaponName.Contains("grenade") || lowerCaseWeaponName.Contains("molotov") || lowerCaseWeaponName.Contains("flashbang") || lowerCaseWeaponName.Contains("bumpmine"):
								{
									ModifyPlayerPoints(k4attacker, Config.PointSettings.ImpactKill, "k4.phrases.impactkill");
									break;
								}
							// knife_ would not handle default knives (therefore changed to just "knife"), this will also not handle other Danger Zone items such as axes and wrenches (if they are even implemented yet in CS2)
							case var _ when lowerCaseWeaponName.Contains("knife") || lowerCaseWeaponName.Contains("bayonet"):
								{
									ModifyPlayerPoints(k4attacker, Config.PointSettings.KnifeKill, "k4.phrases.knifekill");
									break;
								}
							case "taser":
								{
									ModifyPlayerPoints(k4attacker, Config.PointSettings.TaserKill, "k4.phrases.taserkill");
									break;
								}
						}

						int time = Config.PointSettings.SecondsBetweenKills;
						bool isTimeBetweenKills = time <= 0 || DateTime.Now - k4attacker.KillStreak.lastKillTime <= TimeSpan.FromSeconds(time);

						if (k4attacker.KillStreak.killStreak > 0 && isTimeBetweenKills)
						{
							k4attacker.KillStreak = (k4attacker.KillStreak.killStreak + 1, DateTime.Now);
							int killStreak = k4attacker.KillStreak.killStreak;

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
								ModifyPlayerPoints(k4attacker, killStreakInfo.points, killStreakInfo.message);
							}
							else
								k4attacker.KillStreak = (1, DateTime.Now);
						}
						else
							k4attacker.KillStreak = (1, DateTime.Now);

					}
				}

				K4Player? k4assister = plugin.GetK4Player(@event.Assister);
				if (k4assister?.IsValid == true && k4assister.IsPlayer && (Config.RankSettings.PointsForBots || k4victim.IsPlayer))
				{
					ModifyPlayerPoints(k4assister, Config.PointSettings.Assist, "k4.phrases.assist");

					if (@event.Assistedflash)
					{
						ModifyPlayerPoints(k4assister, Config.PointSettings.AssistFlash, "k4.phrases.assistflash");
					}
				}

				return HookResult.Continue;
			});
		}
	}
}