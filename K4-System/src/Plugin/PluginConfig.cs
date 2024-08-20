namespace K4System
{
	using CounterStrikeSharp.API.Core;

	using System.Text.Json.Serialization;

	public sealed class AdminSettingsEntry
	{
		[JsonPropertyName("permission")]
		public required string Permission { get; set; }

		[JsonPropertyName("listcolor")]
		public string? ListColor { get; set; } = null;

		[JsonPropertyName("clantag")]
		public string? ClanTag { get; set; } = null;
	}


	public sealed class GeneralSettings
	{
		[JsonPropertyName("load-messages")]
		public bool LoadMessages { get; set; } = true;

		[JsonPropertyName("spawn-message")]
		public bool SpawnMessage { get; set; } = true;

		[JsonPropertyName("module_ranks")]
		public bool ModuleRanks { get; set; } = true;

		[JsonPropertyName("module_stats")]
		public bool ModuleStats { get; set; } = true;

		[JsonPropertyName("module_times")]
		public bool ModuleTimes { get; set; } = true;

		[JsonPropertyName("module_utils")]
		public bool ModuleUtils { get; set; } = true;

		[JsonPropertyName("lvl-ranks-table")]
		public bool LevelRanksCompatibility { get; set; } = false;

		[JsonPropertyName("ffa-mode")]
		public bool FFAMode { get; set; } = false;

		[JsonPropertyName("expensive-command-cooldown")]
		public int ExpensiveCommandCooldown { get; set; } = 3;

		[JsonPropertyName("table-purge-days")]
		public int TablePurgeDays { get; set; } = 30;

		[JsonPropertyName("admin-settings")]
		public List<AdminSettingsEntry> AdminSettingsList { get; set; } = new List<AdminSettingsEntry>
		{
			new AdminSettingsEntry
			{
				Permission = "@myplugin/owner-permission",
				ListColor = "red",
				ClanTag = "[Owner]"
			},
			new AdminSettingsEntry
			{
				Permission = "#myplugin/admin-group",
				ListColor = "lightblue",
				ClanTag = "[Admin]"
			}
		};
	}

	public sealed class UtilSettings
	{
		[JsonPropertyName("admin-list-enable")]
		public bool AdminListEnable { get; set; } = true;

		[JsonPropertyName("connect-message-enable")]
		public bool ConnectMessageEnable { get; set; } = true;

		[JsonPropertyName("disconnect-message-enable")]
		public bool DisconnectMessageEnable { get; set; } = true;
	}

	public sealed class CommandSettings
	{
		[JsonPropertyName("rank-commands")]
		public List<string> RankCommands { get; set; } = new List<string>
		{
			"rank",
			"myrank"
		};

		[JsonPropertyName("top-commands")]
		public List<string> TopCommands { get; set; } = new List<string>
		{
			"top",
			"ranktop",
			"toplist"
		};

		[JsonPropertyName("resetmyrank-commands")]
		public List<string> ResetMyCommands { get; set; } = new List<string>
		{
			"resetmydata"
		};

		[JsonPropertyName("ranks-commands")]
		public List<string> RanksCommands { get; set; } = new List<string>
		{
			"ranks",
			"ranklist"
		};

		[JsonPropertyName("stat-commands")]
		public List<string> StatCommands { get; set; } = new List<string>
		{
			"stats",
			"mystats",
			"stat",
			"mystat",
			"statistic",
			"statistics"
		};

		[JsonPropertyName("time-commands")]
		public List<string> TimeCommands { get; set; } = new List<string>
		{
			"time",
			"mytime",
			"playtime"
		};

		[JsonPropertyName("admin-list-commands")]
		public List<string> AdminListCommands { get; set; } = new List<string>
		{
			"admins",
			"listadmins"
		};
	}

	public sealed class DatabaseSettings
	{
		[JsonPropertyName("host")]
		public string Host { get; set; } = "localhost";

		[JsonPropertyName("username")]
		public string Username { get; set; } = "root";

		[JsonPropertyName("database")]
		public string Database { get; set; } = "database";

		[JsonPropertyName("password")]
		public string Password { get; set; } = "password";

		[JsonPropertyName("port")]
		public int Port { get; set; } = 3306;

		[JsonPropertyName("sslmode")]
		public string Sslmode { get; set; } = "none";

		[JsonPropertyName("table-prefix")]
		public string TablePrefix { get; set; } = "";

		[JsonPropertyName("lvlranks-table-name")]
		public string LvLRanksTableName { get; set; } = "lvl_base";
	}

	public sealed class StatisticSettings
	{
		[JsonPropertyName("stats-for-bots")]
		public bool StatsForBots { get; set; } = false;

		[JsonPropertyName("warmup-stats")]
		public bool WarmupStats { get; set; } = false;

		[JsonPropertyName("minimum-players")]
		public int MinPlayers { get; set; } = 4;
	}

	public sealed class RankSettings
	{
		[JsonPropertyName("rank-based-team-balance")]
		public bool RankBasedTeamBalance { get; set; } = false;

		[JsonPropertyName("rank-based-team-balance-max-difference")]
		public int RankBasedTeamBalanceMaxDifference { get; set; } = 200;

		[JsonPropertyName("display-toplist-placement")]
		public bool DisplayToplistPlacement { get; set; } = true;

		[JsonPropertyName("display-toplist-placement-max")]
		public int DisplayToplistPlacementMax { get; set; } = 10;

		[JsonPropertyName("display-toplist-placement-tag")]
		public string DisplayToplistPlacementTag { get; set; } = "Top{placement} |";

		[JsonPropertyName("killstreak-reset-on-round-end")]
		public bool KillstreakResetOnRoundEnd { get; set; } = false;

		[JsonPropertyName("start-points")]
		public int StartPoints { get; set; } = 0;

		[JsonPropertyName("country-tag-before-rank")]
		public bool CountryTagEnabled { get; set; } = false;

		[JsonPropertyName("playername-in-kill-messages")]
		public bool PlayerNameKillMessages { get; set; } = false;

		[JsonPropertyName("points-for-bots")]
		public bool PointsForBots { get; set; } = false;

		[JsonPropertyName("warmup-points")]
		public bool WarmupPoints { get; set; } = false;

		[JsonPropertyName("round-end-points")]
		public bool RoundEndPoints { get; set; } = false;

		[JsonPropertyName("minimum-players")]
		public int MinPlayers { get; set; } = 4;

		[JsonPropertyName("scoreboard-clantags")]
		public bool ScoreboardClantags { get; set; } = true;

		[JsonPropertyName("scoreboard-score-sync")]
		public bool ScoreboardScoreSync { get; set; } = false;

		[JsonPropertyName("vip-multiplier")]
		public double VipMultiplier { get; set; } = 1.25;

		[JsonPropertyName("dynamic-death-points")]
		public bool DynamicDeathPoints { get; set; } = true;

		[JsonPropertyName("dynamic-death-points-max-multiplier")]
		public double DynamicDeathPointsMaxMultiplier { get; set; } = 3.00;

		[JsonPropertyName("dynamic-death-points-min-multiplier")]
		public double DynamicDeathPointsMinMultiplier { get; set; } = 0.5;
	}

	public sealed class PointSettings
	{
		[JsonPropertyName("death")]
		public int Death { get; set; } = -5;

		[JsonPropertyName("kill")]
		public int Kill { get; set; } = 8;

		[JsonPropertyName("headshot")]
		public int Headshot { get; set; } = 5;

		[JsonPropertyName("penetrated")]
		public int Penetrated { get; set; } = 3;

		[JsonPropertyName("noscope")]
		public int NoScope { get; set; } = 15;

		[JsonPropertyName("thrusmoke")]
		public int Thrusmoke { get; set; } = 15;

		[JsonPropertyName("blind-kill")]
		public int BlindKill { get; set; } = 5;

		[JsonPropertyName("team-kill")]
		public int TeamKill { get; set; } = -10;

		[JsonPropertyName("suicide")]
		public int Suicide { get; set; } = -5;

		[JsonPropertyName("assist")]
		public int Assist { get; set; } = 5;

		[JsonPropertyName("team-assist")]
		public int TeamAssist { get; set; } = -5;

		[JsonPropertyName("assist-flash")]
		public int AssistFlash { get; set; } = 7;

		[JsonPropertyName("team-assist-flash")]
		public int TeamAssistFlash { get; set; } = -7;

		[JsonPropertyName("round-win")]
		public int RoundWin { get; set; } = 5;

		[JsonPropertyName("round-lose")]
		public int RoundLose { get; set; } = -2;

		[JsonPropertyName("mvp")]
		public int MVP { get; set; } = 10;

		[JsonPropertyName("bomb-drop")]
		public int BombDrop { get; set; } = -2;

		[JsonPropertyName("bomb-pickup")]
		public int BombPickup { get; set; } = 2;

		[JsonPropertyName("bomb-defuse")]
		public int BombDefused { get; set; } = 10;

		[JsonPropertyName("bomb-defuse-others")]
		public int BombDefusedOthers { get; set; } = 3;

		[JsonPropertyName("bomb-plant")]
		public int BombPlant { get; set; } = 10;

		[JsonPropertyName("bomb-explode")]
		public int BombExploded { get; set; } = 10;

		[JsonPropertyName("hostage-hurt")]
		public int HostageHurt { get; set; } = -2;

		[JsonPropertyName("hostage-kill")]
		public int HostageKill { get; set; } = 20;

		[JsonPropertyName("hostage-rescue")]
		public int HostageRescue { get; set; } = 15;

		[JsonPropertyName("hostage-rescueall")]
		public int HostageRescueAll { get; set; } = 10;

		[JsonPropertyName("long-distance-kill")]
		public int LongDistanceKill { get; set; } = 8;

		[JsonPropertyName("long-distance")]
		public int LongDistance { get; set; } = 30;

		[JsonPropertyName("seconds-between-kills")]
		public int SecondsBetweenKills { get; set; } = 0;

		[JsonPropertyName("double-kill")]
		public int DoubleKill { get; set; } = 5;

		[JsonPropertyName("triple-kill")]
		public int TripleKill { get; set; } = 10;

		[JsonPropertyName("domination")]
		public int Domination { get; set; } = 15;

		[JsonPropertyName("rampage")]
		public int Rampage { get; set; } = 20;

		[JsonPropertyName("mega-kill")]
		public int MegaKill { get; set; } = 25;

		[JsonPropertyName("ownage")]
		public int Ownage { get; set; } = 30;

		[JsonPropertyName("ultra-kill")]
		public int UltraKill { get; set; } = 35;

		[JsonPropertyName("killing-spree")]
		public int KillingSpree { get; set; } = 40;

		[JsonPropertyName("monster-kill")]
		public int MonsterKill { get; set; } = 45;

		[JsonPropertyName("unstoppable")]
		public int Unstoppable { get; set; } = 50;

		[JsonPropertyName("godlike")]
		public int GodLike { get; set; } = 60;

		[JsonPropertyName("grenade-kill")]
		public int GrenadeKill { get; set; } = 30;

		[JsonPropertyName("inferno-kill")]
		public int InfernoKill { get; set; } = 30;

		[JsonPropertyName("impact-kill")]
		public int ImpactKill { get; set; } = 100;

		[JsonPropertyName("taser-kill")]
		public int TaserKill { get; set; } = 20;

		[JsonPropertyName("knife-kill")]
		public int KnifeKill { get; set; } = 15;

		[JsonPropertyName("playtime-points")]
		public int PlaytimePoints { get; set; } = 10;

		[JsonPropertyName("playtime-minutes")]
		public float PlaytimeMinutes { get; set; } = 5.00f;

		[JsonPropertyName("playtime-reward-afk")]
		public bool PlaytimeRewardAFK { get; set; } = false;
	}

	public sealed class PluginConfig : BasePluginConfig
	{
		[JsonPropertyName("general-settings")]
		public GeneralSettings GeneralSettings { get; set; } = new GeneralSettings();

		[JsonPropertyName("command-settings")]
		public CommandSettings CommandSettings { get; set; } = new CommandSettings();

		[JsonPropertyName("database-settings")]
		public DatabaseSettings DatabaseSettings { get; set; } = new DatabaseSettings();

		[JsonPropertyName("statistic-settings")]
		public StatisticSettings StatisticSettings { get; set; } = new StatisticSettings();

		[JsonPropertyName("util-settings")]
		public UtilSettings UtilSettings { get; set; } = new UtilSettings();

		[JsonPropertyName("rank-settings")]
		public RankSettings RankSettings { get; set; } = new RankSettings();

		[JsonPropertyName("point-settings")]
		public PointSettings PointSettings { get; set; } = new PointSettings();

		[JsonPropertyName("ConfigVersion")]
		public override int Version { get; set; } = 13;
	}
}