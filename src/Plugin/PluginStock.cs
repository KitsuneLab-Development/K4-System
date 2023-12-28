
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
		public bool InitializeDatabase(string tableName, string createTableQuery)
		{
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
								missingColumns.Remove(reader.GetString(0));
							}
						}
					}
				}

				if (missingColumns.Count > 0)
				{
					this.Logger.LogCritical("The following columns are missing in the {0} table: {1}", tableName, string.Join(", ", missingColumns));
					return false;
				}
			}

			return true;
		}
	}
}