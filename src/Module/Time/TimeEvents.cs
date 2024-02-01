
namespace K4System
{
	using CounterStrikeSharp.API.Core;
	using CounterStrikeSharp.API.Modules.Utils;

	public partial class ModuleTime : IModuleTime
	{
		public void Initialize_Events(Plugin plugin)
		{
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
		}
	}
}