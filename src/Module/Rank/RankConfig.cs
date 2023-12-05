namespace K4System
{
	using System.Text.RegularExpressions;
	using Microsoft.Extensions.Logging;
	using Newtonsoft.Json;

	public partial class ModuleRank : IModuleRank
	{
		public void Initialize_Config()
		{
			string ranksFilePath = Path.Join(ModuleDirectory, "ranks.jsonc");

			string defaultRanksContent = @"{
	""None"": { // Whatever you set here, be unique. Not read by plugin
		""Name"": ""None"", // This name is set in MySQL and also if not Tag is preset, this is the Tag
		""Point"": -1, // Whatever you set to -1 is the default rank
		""Color"": ""Default""
	},
	""Silver"": {
		""Name"": ""Silver"",
		""Tag"": ""[S]"", // Clan tag (scoreboard) of the rank. If not set, it uses the key instead, which is currently ""Silver""
		""Point"": 250, // From this amount of experience, the player is Silver
		""Color"": ""LightBlue"" // Color code for the rank. Find color names here: https://github.com/roflmuffin/CounterStrikeSharp/blob/main/managed/CounterStrikeSharp.API/Modules/Utils/ChatColors.cs
	},
	""Gold"": {
		""Name"": ""Gold"",
		""Tag"": ""[G]"",
		""Point"": 1000,
		""Color"": ""Red""
	}
	// You can add as many as you want
}";

			if (!File.Exists(ranksFilePath))
			{
				File.WriteAllText(ranksFilePath, defaultRanksContent);
				Logger.LogInformation("Default ranks file created.");
			}

			try
			{
				var jsonContent = Regex.Replace(File.ReadAllText(ranksFilePath), @"/\*(.*?)\*/|//(.*)", string.Empty, RegexOptions.Multiline);
				rankDictionary = JsonConvert.DeserializeObject<Dictionary<string, Rank>>(jsonContent)!;

				rankDictionary = rankDictionary.OrderBy(kv => kv.Value.Point).ToDictionary(kv => kv.Key, kv => kv.Value);

				rankDictionary.Values.ToList().ForEach(rank => rank.Color = ApplyRankColors(rank.Color));

				if (!rankDictionary.Values.Any(rank => rank.Point == -1))
				{
					Logger.LogWarning("Default rank is not set. You can set it by creating a rank with -1 point.");
				}
				else
				{
					noneRank = rankDictionary.Values.First(rank => rank.Point == -1);
				}
			}
			catch (Exception ex)
			{
				Logger.LogError("An error occurred: " + ex.Message);
			}
		}
	}
}