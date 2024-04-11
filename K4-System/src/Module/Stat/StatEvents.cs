
namespace K4System
{
	using CounterStrikeSharp.API;
	using CounterStrikeSharp.API.Core;
	using CounterStrikeSharp.API.Modules.Utils;
	using K4System.Models;

	public partial class ModuleStat : IModuleStat
	{
		public void Initialize_Events()
		{
			plugin.RegisterEventHandler((EventPlayerDeath @event, GameEventInfo info) =>
			{
				if (!IsStatsAllowed())
					return HookResult.Continue;

				K4Player? k4victim = plugin.GetK4Player(@event.Userid);

				if (k4victim is null || !k4victim.IsValid)
					return HookResult.Continue;

				if (!k4victim.IsPlayer && !Config.StatisticSettings.StatsForBots)
					return HookResult.Continue;

				if (!k4victim.IsPlayer)
				{
					ModifyPlayerStats(k4victim, "deaths", 1);
				}

				K4Player? k4attacker = plugin.GetK4Player(@event.Attacker);
				if (k4attacker != null && k4attacker.IsValid && k4attacker.IsPlayer)
				{
					ModifyPlayerStats(k4attacker, "kills", 1);

					if (!FirstBlood)
					{
						FirstBlood = true;
						ModifyPlayerStats(k4attacker, "firstblood", 1);
					}

					if (@event.Noscope)
						ModifyPlayerStats(k4attacker, "noscope_kill", 1);

					if (@event.Penetrated > 0)
						ModifyPlayerStats(k4attacker, "penetrated_kill", 1);

					if (@event.Thrusmoke)
						ModifyPlayerStats(k4attacker, "thrusmoke_kill", 1);

					if (@event.Attackerblind)
						ModifyPlayerStats(k4attacker, "flashed_kill", 1);

					if (@event.Dominated > 0)
						ModifyPlayerStats(k4attacker, "dominated_kill", 1);

					if (@event.Revenge > 0)
						ModifyPlayerStats(k4attacker, "revenge_kill", 1);

					if (@event.Assistedflash)
						ModifyPlayerStats(k4attacker, "assist_flash", 1);

					switch (@event.Hitgroup)
					{
						case (int)HitGroup_t.HITGROUP_HEAD:
							ModifyPlayerStats(k4attacker, "headshots", 1);
							break;
						case (int)HitGroup_t.HITGROUP_CHEST:
							ModifyPlayerStats(k4attacker, "chest_hits", 1);
							break;
						case (int)HitGroup_t.HITGROUP_STOMACH:
							ModifyPlayerStats(k4attacker, "stomach_hits", 1);
							break;
						case (int)HitGroup_t.HITGROUP_LEFTARM:
							ModifyPlayerStats(k4attacker, "left_arm_hits", 1);
							break;
						case (int)HitGroup_t.HITGROUP_RIGHTARM:
							ModifyPlayerStats(k4attacker, "right_arm_hits", 1);
							break;
						case (int)HitGroup_t.HITGROUP_LEFTLEG:
							ModifyPlayerStats(k4attacker, "left_leg_hits", 1);
							break;
						case (int)HitGroup_t.HITGROUP_RIGHTLEG:
							ModifyPlayerStats(k4attacker, "right_leg_hits", 1);
							break;
						case (int)HitGroup_t.HITGROUP_NECK:
							ModifyPlayerStats(k4attacker, "neck_hits", 1);
							break;
						case (int)HitGroup_t.HITGROUP_UNUSED:
							ModifyPlayerStats(k4attacker, "unused_hits", 1);
							break;
						case (int)HitGroup_t.HITGROUP_GEAR:
							ModifyPlayerStats(k4attacker, "gear_hits", 1);
							break;
						case (int)HitGroup_t.HITGROUP_SPECIAL:
							ModifyPlayerStats(k4attacker, "special_hits", 1);
							break;
					}
				}

				K4Player? k4assister = plugin.GetK4Player(@event.Assister);
				if (k4assister != null && k4assister.IsValid && k4assister.IsPlayer)
				{
					ModifyPlayerStats(k4assister, "assists", 1);
				}

				return HookResult.Continue;
			});

			plugin.RegisterEventHandler((EventGrenadeThrown @event, GameEventInfo info) =>
			{
				if (!IsStatsAllowed())
					return HookResult.Continue;

				K4Player? k4player = plugin.GetK4Player(@event.Userid);
				if (k4player is null || !k4player.IsValid || !k4player.IsPlayer)
					return HookResult.Continue;

				ModifyPlayerStats(k4player, "grenades", 1);
				return HookResult.Continue;
			});

			plugin.RegisterEventHandler((EventPlayerHurt @event, GameEventInfo info) =>
			{
				if (!IsStatsAllowed())
					return HookResult.Continue;

				K4Player? k4victim = plugin.GetK4Player(@event.Userid);
				if (k4victim is null || !k4victim.IsValid)
					return HookResult.Continue;

				if (k4victim.IsPlayer)
				{
					ModifyPlayerStats(k4victim, "hits_taken", 1);
				}

				K4Player? k4attacker = plugin.GetK4Player(@event.Attacker);
				if (k4attacker != null && k4attacker.IsValid && k4attacker.IsPlayer)
				{
					ModifyPlayerStats(k4attacker, "hits_given", 1);

					if (@event.Hitgroup == 1)
					{
						ModifyPlayerStats(k4attacker, "headshots", 1);
					}
				}

				return HookResult.Continue;
			});

			plugin.RegisterEventHandler((EventRoundStart @event, GameEventInfo info) =>
			{
				FirstBlood = false;
				return HookResult.Continue;
			});

			plugin.RegisterEventHandler((EventBombPlanted @event, GameEventInfo info) =>
			{
				K4Player? k4player = plugin.GetK4Player(@event.Userid);
				if (k4player is null || !k4player.IsValid || !k4player.IsPlayer)
					return HookResult.Continue;

				ModifyPlayerStats(k4player, "bomb_planted", 1);
				return HookResult.Continue;
			});

			plugin.RegisterEventHandler((EventHostageRescued @event, GameEventInfo info) =>
			{
				K4Player? k4player = plugin.GetK4Player(@event.Userid);
				if (k4player is null || !k4player.IsValid || !k4player.IsPlayer)
					return HookResult.Continue;

				ModifyPlayerStats(k4player, "hostage_rescued", 1);
				return HookResult.Continue;
			});

			plugin.RegisterEventHandler((EventHostageKilled @event, GameEventInfo info) =>
			{
				K4Player? k4player = plugin.GetK4Player(@event.Userid);
				if (k4player is null || !k4player.IsValid || !k4player.IsPlayer)
					return HookResult.Continue;

				ModifyPlayerStats(k4player, "hostage_killed", 1);
				return HookResult.Continue;
			});

			plugin.RegisterEventHandler((EventBombDefused @event, GameEventInfo info) =>
			{
				K4Player? k4player = plugin.GetK4Player(@event.Userid);
				if (k4player is null || !k4player.IsValid || !k4player.IsPlayer)
					return HookResult.Continue;

				ModifyPlayerStats(k4player, "bomb_defused", 1);
				return HookResult.Continue;
			});

			plugin.RegisterEventHandler((EventRoundEnd @event, GameEventInfo info) =>
			{
				foreach (K4Player k4player in plugin.K4Players)
				{
					if (!k4player.IsValid || !k4player.IsPlayer)
						continue;

					CsTeam team = k4player.Controller.Team;

					if (team <= CsTeam.Spectator)
						continue;

					ModifyPlayerStats(k4player, "rounds_overall", 1);
					ModifyPlayerStats(k4player, team == CsTeam.Terrorist ? "rounds_t" : "rounds_ct", 1);
				}
				return HookResult.Continue;
			});

			plugin.RegisterEventHandler((EventWeaponFire @event, GameEventInfo info) =>
			{
				if (!IsStatsAllowed())
					return HookResult.Continue;

				K4Player? k4player = plugin.GetK4Player(@event.Userid);
				if (k4player is null || !k4player.IsValid || !k4player.IsPlayer)
					return HookResult.Continue;

				if (@event.Weapon.Contains("knife") || @event.Weapon.Contains("bayonet"))
					return HookResult.Continue;

				ModifyPlayerStats(k4player, "shoots", 1);
				return HookResult.Continue;
			});

			plugin.RegisterEventHandler((EventRoundMvp @event, GameEventInfo info) =>
			{
				if (!IsStatsAllowed())
					return HookResult.Continue;

				K4Player? k4player = plugin.GetK4Player(@event.Userid);
				if (k4player is null || !k4player.IsValid || !k4player.IsPlayer)
					return HookResult.Continue;

				ModifyPlayerStats(k4player, "mvp", 1);
				return HookResult.Continue;
			});

			plugin.RegisterEventHandler<EventCsWinPanelMatch>((@event, info) =>
			{
				if (!IsStatsAllowed())
					return HookResult.Continue;

				List<K4Player> k4players = plugin.K4Players.Where(p => p.IsValid && p.IsPlayer).ToList();

				if (Config.GeneralSettings.FFAMode)
				{
					K4Player? k4player = k4players.OrderByDescending(p => p.Controller.Score).FirstOrDefault();

					if (k4player != null)
					{
						ModifyPlayerStats(k4player, "game_win", 1);
					}

					k4players.Where(p => p != k4player).ToList().ForEach(p => ModifyPlayerStats(p, "game_lose", 1));
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

					if (winnerTeam > CsTeam.Spectator)
					{
						k4players.Where(p => p.Controller.Team > CsTeam.Spectator)
							.ToList()
							.ForEach(p => ModifyPlayerStats(p, p.Controller.Team == winnerTeam ? "game_win" : "game_lose", 1));
					}
				}

				return HookResult.Continue;
			});
		}
	}
}
