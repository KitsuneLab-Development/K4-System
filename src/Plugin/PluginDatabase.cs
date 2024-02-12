
using System.Data;
using CounterStrikeSharp.API;
using MySqlConnector;

namespace K4System
{
	public sealed class Database
	{
		private static readonly Lazy<Database> instance = new Lazy<Database>(() => new Database());
		public static Database Instance => instance.Value;

		private string? connectionString;

		private Database() { }

		public void Initialize(string server, string database, string userId, string password, int port = 3306, string sslMode = "None")
		{
			connectionString = BuildConnectionString(server, database, userId, password, port, sslMode);
		}

		private static string BuildConnectionString(string server, string database, string userId, string password, int port, string sslMode)
		{
			var builder = new MySqlConnectionStringBuilder
			{
				Server = server,
				Database = database,
				UserID = userId,
				Password = password,
				Port = (uint)port,
				SslMode = Enum.Parse<MySqlSslMode>(sslMode, true),
			};

			return builder.ConnectionString;
		}

		public async Task ExecuteNonQueryAsync(string query, params MySqlParameter[] parameters)
		{
			using (var connection = new MySqlConnection(connectionString))
			{
				await connection.OpenAsync();
				using (var command = new MySqlCommand(query, connection))
				{
					command.Parameters.AddRange(parameters);
					await command.ExecuteNonQueryAsync();
				}
			}
		}

		public async Task<object?> ExecuteScalarAsync(string query, params MySqlParameter[] parameters)
		{
			using (var connection = new MySqlConnection(connectionString))
			{
				await connection.OpenAsync();
				using (var command = new MySqlCommand(query, connection))
				{
					command.Parameters.AddRange(parameters);
					return await command.ExecuteScalarAsync();
				}
			}
		}

		public async Task<MySqlDataReader> ExecuteReaderAsync(string query, params MySqlParameter[] parameters)
		{
			using (var connection = new MySqlConnection(connectionString))
			{
				await connection.OpenAsync();

				using (var command = new MySqlCommand(query, connection))
				{
					command.Parameters.AddRange(parameters);
					return await command.ExecuteReaderAsync(CommandBehavior.CloseConnection);
				}
			}
		}

		public async Task ExecuteWithTransactionAsync(Func<MySqlConnection, MySqlTransaction, Task> executeActions)
		{
			using (var connection = new MySqlConnection(connectionString))
			{
				await connection.OpenAsync();
				using (var transaction = await connection.BeginTransactionAsync())
				{
					try
					{
						await executeActions(connection, transaction);
						await transaction.CommitAsync();
					}
					catch
					{
						await transaction.RollbackAsync();
						throw;
					}
				}
			}
		}
	}
}
