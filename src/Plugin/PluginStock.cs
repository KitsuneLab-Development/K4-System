
namespace K4System
{
	using CounterStrikeSharp.API.Core;
	using CounterStrikeSharp.API.Modules.Utils;
	using Microsoft.Extensions.Logging;
	using MySqlConnector;
	using System.Reflection;
	using System.Text.RegularExpressions;

	public sealed partial class Plugin : BasePlugin
	{
		public static string ConvertSteamID64ToSteamID(long steamId64)
		{
			long authserver = (steamId64 - 76561197960265728) & 1;
			long authid = (steamId64 - 76561197960265728 - authserver) / 2;
			return $"STEAM_0:{authserver}:{authid}";
		}

		public string ApplyPrefixColors(string msg)
		{
			string modifiedValue = msg;
			Type chatColorsType = typeof(ChatColors);

			foreach (FieldInfo field in chatColorsType.GetFields())
			{
				if (modifiedValue.Equals(field.Name, StringComparison.OrdinalIgnoreCase))
				{
					modifiedValue = modifiedValue.Replace(field.Name, field.GetValue(null)!.ToString(), StringComparison.OrdinalIgnoreCase);
				}
			}

			return modifiedValue;
		}

		public bool InitializeDatabase(string tableName, string createTableQuery)
		{
			if (string.IsNullOrEmpty(this.Config.DatabaseSettings.Host) ||
				string.IsNullOrEmpty(this.Config.DatabaseSettings.Database) ||
				string.IsNullOrEmpty(this.Config.DatabaseSettings.Username) ||
				string.IsNullOrEmpty(this.Config.DatabaseSettings.Password))
			{
				return false;
			}

			tableName = $"{this.Config.DatabaseSettings.TablePrefix}{tableName}";

			string connectionString = $"Server={this.Config.DatabaseSettings.Host};Database={this.Config.DatabaseSettings.Database};port={this.Config.DatabaseSettings.Port};User Id={this.Config.DatabaseSettings.Username};password={this.Config.DatabaseSettings.Password};SslMode=none;";

			string checkTableQuery = $@"
					SELECT COUNT(*)
					FROM INFORMATION_SCHEMA.TABLES
					WHERE TABLE_SCHEMA = '{this.Config.DatabaseSettings.Database}'
					AND TABLE_NAME = '{tableName}';";

			int tableCount = 0;

			using (var connection = new MySqlConnection(connectionString))
			{
				connection.Open();

				using (var command = new MySqlCommand(checkTableQuery, connection))
				{
					tableCount = Convert.ToInt32(command.ExecuteScalar());
				}
			}

			if (tableCount == 0)
			{
				//** ? Create table */

				Logger.LogInformation("Creating table '{0}'", tableName);

				this.Database.ExecuteNonQueryAsync(createTableQuery);
				return true;
			}
			else
			{
				//** ? Check if any columns are missing */

				string pattern = $@"(?<=`)(?!{tableName})\w+(?=`)";
				MatchCollection matches = Regex.Matches(createTableQuery, pattern);

				List<string> columnNames = matches
					.Cast<Match>()
					.Select(match => match.Value)
					.ToList();

				this.Logger.LogInformation("Checking table '{0}' | Validating {1} columns", tableName, columnNames.Count);

				var missingColumns = columnNames.Distinct().ToList();
				var unusedColumns = new List<string>();

				using (var connection = new MySqlConnection(connectionString))
				{
					connection.Open();

					using (var command = new MySqlCommand($@"
							SELECT COLUMN_NAME
							FROM INFORMATION_SCHEMA.COLUMNS
							WHERE TABLE_SCHEMA = '{this.Config.DatabaseSettings.Database}'
							AND TABLE_NAME = '{tableName}';", connection))
					{
						using (var reader = command.ExecuteReader())
						{
							while (reader.Read())
							{
								string columnName = reader.GetString(0);


								if (missingColumns.Contains(columnName))
								{
									missingColumns.Remove(columnName);
								}
								else unusedColumns.Add(columnName);
							}
						}
					}
				}

				if (missingColumns.Count > 0)
				{
					this.Logger.LogCritical("The following columns are missing in the {0} table: {1}", tableName, string.Join(", ", missingColumns));
				}

				if (unusedColumns.Count > 0)
				{
					this.Logger.LogWarning("The following columns exist in the {0} table but are not used in the plugin: {1}", tableName, string.Join(", ", unusedColumns));
				}

				return missingColumns.Count == 0;
			}
		}
	}
}