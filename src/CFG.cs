using CounterStrikeSharp.API.Modules.Utils;
using System.Reflection;
using System.Text.Json;

namespace K4ryuuSystem
{
	public partial class K4System
	{
		public static Config config = new();

		public static Config defaultConfig = new Config
		{
			// General Settings
			ChatPrefix = "{LightRed}[K4-System]",
			DisableSpawnMessage = false,
			LogLevel = (int)LogLevel.Info, // -1 Debug, 0 Info, 1 Warning, 2 Error

			// Database Settings
			DatabaseHost = "localhost",
			DatabasePort = 3306,
			DatabaseUser = "root",
			DatabasePassword = "password",
			DatabaseName = "database",

			// Stat Settings
			StatsForBots = false,

			// Rank Settings
			FFAMode = false,
			WarmupPoints = false,
			PointsForBots = false,
			MinPlayersPoints = 4,
			ScoreboardRanks = true,
			ScoreboardScoreSync = false,
			VipPointMultiplier = 1.5,

			// Point Values
			DeathPoints = 5,
			KillPoints = 10,
			HeadshotPoints = 5,
			PenetratedPoints = 3,
			NoScopePoints = 15,
			ThrusmokePoints = 15,
			BlindKillPoints = 5,
			TeamKillPoints = 10,
			SuicidePoints = 5,
			AssistPoints = 5,
			AsssistFlashPoints = 7,
			PlantPoints = 10,
			RoundWinPoints = 5,
			RoundLosePoints = 2,
			MVPPoints = 10,
			DefusePoints = 8,
			BombDropPoints = 2,
			BombPickupPoints = 2,
			HostageHurtPoints = 2,
			HostageKillPoints = 20,
			HostageRescuePoints = 15,
			LongDistanceKillPoints = 8,
			LongDistance = 30,
			SecondsBetweenKills = 5,
			DoubleKillPoints = 5,
			TripleKillPoints = 10,
			DominationPoints = 15,
			RampagePoints = 20,
			MegaKillPoints = 25,
			OwnagePoints = 30,
			UltraKillPoints = 35,
			KillingSpreePoints = 40,
			MonsterKillPoints = 45,
			UnstoppablePoints = 50,
			GodLikePoints = 60,
			GrenadeKillPoints = 30,
			TaserKillPoints = 20,
			KnifeKillPoints = 15
		};

		public void CheckConfig(string moduleDirectory)
		{
			string path = Path.Join(moduleDirectory, "config.json");

			CreateAndWriteFile(path);

			using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
			using (StreamReader sr = new StreamReader(fs))
			{
				// Deserialize the JSON from the file and load the configuration.
				config = JsonSerializer.Deserialize<Config>(sr.ReadToEnd())!;
			}

			if (config != null && config.ChatPrefix != null)
				config.ChatPrefix = ModifyColorValue(config.ChatPrefix);
		}

		private void CreateAndWriteFile(string path)
		{
			if (File.Exists(path))
			{
				string existingConfigJson = File.ReadAllText(path);
				Config existingConfig = JsonSerializer.Deserialize<Config>(existingConfigJson)!;

				UpdateConfigWithDefaultValues(existingConfig);

				string updatedConfigJson = JsonSerializer.Serialize(existingConfig, new JsonSerializerOptions()
				{
					WriteIndented = true
				});

				File.WriteAllText(path, updatedConfigJson);

				Log($"Config file updated @ K4-System/config.json");
			}
			else
			{
				using (FileStream fs = File.Create(path))
				{
					// File is created, and fs will automatically be disposed when the using block exits.
				}

				Log($"Config file created @ K4-System/config.json");

				string jsonConfig = JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions()
				{
					WriteIndented = true
				});

				File.WriteAllText(path, jsonConfig);
			}
		}

		private static void UpdateConfigWithDefaultValues(Config existingConfig)
		{
			foreach (PropertyInfo property in typeof(Config).GetProperties())
			{
				object? existingValue = property.GetValue(existingConfig);
				object defaultValue = property.GetValue(defaultConfig)!;

				if (existingValue == null || existingValue.Equals(defaultValue))
				{
					property.SetValue(existingConfig, defaultValue);
				}
			}
		}

		private string ModifyColorValue(string msg)
		{
			if (msg.Contains('{'))
			{
				string modifiedValue = msg;
				foreach (FieldInfo field in typeof(ChatColors).GetFields())
				{
					string pattern = $"{{{field.Name}}}";
					if (msg.Contains(pattern, StringComparison.OrdinalIgnoreCase))
					{
						modifiedValue = modifiedValue.Replace(pattern, field.GetValue(null)!.ToString(), StringComparison.OrdinalIgnoreCase);
					}
				}
				return modifiedValue;
			}

			return string.IsNullOrEmpty(msg) ? "[K4-System]" : msg;
		}


		public class Config
		{
			// General Settings
			public string? ChatPrefix { get; set; }
			public bool DisableSpawnMessage { get; set; }
			public int LogLevel { get; set; }

			// Statistics Settings
			public bool WarmupStats { get; set; }
			public bool StatsForBots { get; set; }
			public int MinPlayersStats { get; set; }


			// Rank Settings
			public bool WarmupPoints { get; set; }
			public int MinPlayersPoints { get; set; }
			public bool FFAMode { get; set; }
			public bool ScoreboardRanks { get; set; }
			public bool ScoreboardScoreSync { get; set; }
			public double VipPointMultiplier { get; set; }
			public bool PointsForBots { get; set; }

			// Database Settings
			public string? DatabaseHost { get; set; }
			public int DatabasePort { get; set; }
			public string? DatabaseUser { get; set; }
			public string? DatabasePassword { get; set; }
			public string? DatabaseName { get; set; }

			// Individual Actions
			public int DeathPoints { get; set; }
			public int KillPoints { get; set; }
			public int HeadshotPoints { get; set; }
			public int PenetratedPoints { get; set; }
			public int NoScopePoints { get; set; }
			public int ThrusmokePoints { get; set; }
			public int BlindKillPoints { get; set; }
			public int TeamKillPoints { get; set; }
			public int SuicidePoints { get; set; }
			public int AssistPoints { get; set; }
			public int AsssistFlashPoints { get; set; }
			public int PlantPoints { get; set; }

			// Round and Game Outcomes
			public int RoundWinPoints { get; set; }
			public int RoundLosePoints { get; set; }
			public int MVPPoints { get; set; }
			public int DefusePoints { get; set; }
			public int BombDropPoints { get; set; }
			public int BombPickupPoints { get; set; }

			// Hostage Actions
			public int HostageHurtPoints { get; set; }
			public int HostageKillPoints { get; set; }
			public int HostageRescuePoints { get; set; }

			// Kill Streaks
			public int LongDistanceKillPoints { get; set; }
			public int LongDistance { get; set; }
			public int SecondsBetweenKills { get; set; }
			public int DoubleKillPoints { get; set; }
			public int TripleKillPoints { get; set; }

			// Multi-Kill Streaks
			public int DominationPoints { get; set; }
			public int RampagePoints { get; set; }
			public int MegaKillPoints { get; set; }
			public int OwnagePoints { get; set; }
			public int UltraKillPoints { get; set; }
			public int KillingSpreePoints { get; set; }
			public int MonsterKillPoints { get; set; }
			public int UnstoppablePoints { get; set; }
			public int GodLikePoints { get; set; }

			// Special Kills
			public int GrenadeKillPoints { get; set; }
			public int TaserKillPoints { get; set; }
			public int KnifeKillPoints { get; set; }
		}
	}
}
