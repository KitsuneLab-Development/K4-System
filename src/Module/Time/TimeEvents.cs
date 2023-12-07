
namespace K4System
{
	using CounterStrikeSharp.API;
	using CounterStrikeSharp.API.Core;
	using CounterStrikeSharp.API.Modules.Utils;
	using Microsoft.Extensions.Logging;

	public partial class ModuleTime : IModuleTime
	{
		public void Initialize_Events(Plugin plugin)
		{
			plugin.RegisterEventHandler((EventPlayerConnectFull @event, GameEventInfo info) =>
			{
				CCSPlayerController player = @event.Userid;

				if (player is null || !player.IsValid || player.IsBot || player.IsHLTV)
					return HookResult.Continue;

				_ = LoadTimeData(player);

				return HookResult.Continue;
			});

			plugin.RegisterListener<Listeners.OnMapStart>((mapName) =>
			{
				globalGameRules = null;
			});

			plugin.RegisterListener<Listeners.OnMapEnd>(() =>
			{
				_ = SaveAllPlayerCache(true);
			});

			plugin.RegisterEventHandler((EventPlayerTeam @event, GameEventInfo info) =>
			{
				CCSPlayerController player = @event.Userid;

				if (player is null || !player.IsValid || !player.PlayerPawn.IsValid)
					return HookResult.Continue;

				if (player.IsBot || player.IsHLTV)
					return HookResult.Continue;

				if (!timeCache.ContainsPlayer(player))
					return HookResult.Continue;

				DateTime now = DateTime.UtcNow;
				TimeData playerData = timeCache[player];

				if ((CsTeam)@event.Oldteam != CsTeam.None)
				{
					playerData.TimeFields[GetFieldForTeam((CsTeam)@event.Oldteam)] += (int)(DateTime.UtcNow - playerData.Times["Team"]).TotalSeconds;
				}

				playerData.Times["Team"] = now;

				return HookResult.Continue;
			});

			plugin.RegisterEventHandler((EventPlayerSpawn @event, GameEventInfo info) =>
			{
				CCSPlayerController player = @event.Userid;

				if (player is null || !player.IsValid || !player.PlayerPawn.IsValid)
					return HookResult.Continue;

				if (player.IsBot || player.IsHLTV)
					return HookResult.Continue;

				if (!timeCache.ContainsPlayer(player))
					return HookResult.Continue;

				TimeData playerData = timeCache[player];

				playerData.TimeFields["dead"] += (int)(DateTime.UtcNow - playerData.Times["Death"]).TotalSeconds;
				playerData.Times["Death"] = DateTime.UtcNow;

				return HookResult.Continue;
			});

			plugin.RegisterEventHandler((EventPlayerDeath @event, GameEventInfo info) =>
			{
				CCSPlayerController player = @event.Userid;

				if (player is null || !player.IsValid || !player.PlayerPawn.IsValid)
					return HookResult.Continue;

				if (player.IsBot || player.IsHLTV)
					return HookResult.Continue;

				if (!timeCache.ContainsPlayer(player))
					return HookResult.Continue;

				TimeData playerData = timeCache[player];

				playerData.TimeFields["alive"] += (int)(DateTime.UtcNow - playerData.Times["Death"]).TotalSeconds;
				playerData.Times["Death"] = DateTime.UtcNow;

				return HookResult.Continue;
			});

			plugin.RegisterEventHandler((EventPlayerDisconnect @event, GameEventInfo info) =>
			{
				CCSPlayerController player = @event.Userid;

				if (player is null || !player.IsValid || !player.PlayerPawn.IsValid)
					return HookResult.Continue;

				if (player.IsBot || player.IsHLTV)
					return HookResult.Continue;

				if (!timeCache.ContainsPlayer(player))
					return HookResult.Continue;

				DateTime now = DateTime.UtcNow;

				TimeData playerData = timeCache[player];

				playerData.TimeFields["all"] += (int)Math.Round((now - playerData.Times["Connect"]).TotalSeconds);
				playerData.TimeFields[GetFieldForTeam((CsTeam)player.TeamNum)] += (int)Math.Round((now - playerData.Times["Team"]).TotalSeconds);

				if ((CsTeam)player.TeamNum > CsTeam.Spectator)
					playerData.TimeFields[player.PawnIsAlive ? "alive" : "dead"] += (int)Math.Round((now - playerData.Times["Death"]).TotalSeconds);

				_ = SavePlayerTimeCache(player, true);

				return HookResult.Continue;
			});

			plugin.RegisterEventHandler((EventRoundEnd @event, GameEventInfo info) =>
			{
				_ = SaveAllPlayerCache(false);

				return HookResult.Continue;
			});
		}
	}
}