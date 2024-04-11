namespace K4System
{
	using K4SharedApi;
	using CounterStrikeSharp.API.Core;
	using CounterStrikeSharp.API.Core.Capabilities;
	using K4System.Models;

	public sealed partial class Plugin : BasePlugin
	{
		public static PlayerCapability<IPlayerAPI> Capability_SharedPlayerAPI { get; } = new("k4-system:sharedapi-player");

		public void Initialize_API()
		{
			Capabilities.RegisterPlayerCapability(Capability_SharedPlayerAPI, player => new PlayerAPIHandler(player, this));
		}
	}

	public class PlayerAPIHandler : IPlayerAPI
	{
		private readonly K4Player? _player;
		public PlayerAPIHandler(CCSPlayerController player, Plugin plugin)
		{
			_player = plugin.GetK4Player(player);
		}

		public bool IsLoaded => _player is not null;
		public bool IsValid => _player?.IsValid == true;
		public bool IsPlayer => _player?.IsPlayer == true;
		public CCSPlayerController Controller => _player?.Controller ?? throw new Exception("K4-SharedAPI > Controller > Player is not valid or is not a player.");

		public int Points
		{
			get
			{
				if (_player is null || !_player.IsValid || !_player.IsPlayer || _player.rankData is null)
					throw new Exception("K4-SharedAPI > Points (get) > Player is not valid or is not a player.");

				return _player.rankData.Points;
			}
			set
			{
				if (_player is null || !_player.IsValid || !_player.IsPlayer || _player.rankData is null)
					throw new Exception("K4-SharedAPI > Points (set) > Player is not valid or is not a player.");

				_player.rankData.Points = value;
			}
		}

		public int RankID
		{
			get
			{
				if (_player is null || !_player.IsValid || !_player.IsPlayer || _player.rankData is null)
					throw new Exception("K4-SharedAPI > RankID (get) > Player is not valid or is not a player.");

				return _player.rankData.Rank.Id + 1;
			}
		}

		public string RankName
		{
			get
			{
				if (_player is null || !_player.IsValid || !_player.IsPlayer || _player.rankData is null)
					throw new Exception("K4-SharedAPI > RankName (get) > Player is not valid or is not a player.");

				return _player.rankData.Rank.Name;
			}
		}

		public string RankClanTag
		{
			get
			{
				if (_player is null || !_player.IsValid || !_player.IsPlayer || _player.rankData is null)
					throw new Exception("K4-SharedAPI > RankClanTag (get) > Player is not valid or is not a player.");

				return _player.rankData.Rank.Tag ?? "";
			}
		}
	}
}