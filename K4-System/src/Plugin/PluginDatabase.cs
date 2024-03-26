
using System.Data;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace K4System
{
	public sealed class Database
	{
		private ILogger? _Logger;
		private static readonly Lazy<Database> instance = new Lazy<Database>(() => new Database());
		public static Database Instance => instance.Value;

		private string? connectionString;
		private Database() { }

		public void Initialize(ILogger logger, string server, string database, string userId, string password, int port = 3306, string sslMode = "None")
		{
			_Logger = logger;
			connectionString = BuildConnectionString(server, database, userId, password, port, sslMode);
		}

		private static string BuildConnectionString(string server, string database, string userId, string password, int port, string sslMode)
		{
			MySqlConnectionStringBuilder builder = new MySqlConnectionStringBuilder
			{
				Server = server,
				Database = database,
				UserID = userId,
				Password = password,
				Port = (uint)port,
				SslMode = Enum.Parse<MySqlSslMode>(sslMode, true),
				// daffyy's solution to prevent crash from pooling runs out with default values
				Pooling = true,
				MinimumPoolSize = 0,
				MaximumPoolSize = 640,
				ConnectionIdleTimeout = 30
			};

			return builder.ConnectionString;
		}

		public async Task ExecuteNonQueryAsync(string query, params MySqlParameter[] parameters)
		{
			try
			{
				using (MySqlConnection connection = new MySqlConnection(connectionString))
				{
					await connection.OpenAsync();
					using (MySqlCommand command = new MySqlCommand(query, connection))
					{
						command.Parameters.AddRange(parameters);
						await command.ExecuteNonQueryAsync();
					}
				}
			}
			catch (Exception ex)
			{
				string errorMessage = $"An error occurred while executing SQL query: {query}. Error message: {ex.Message}";
				_Logger?.LogError(ex, errorMessage);
				throw;
			}
		}



		public async Task<object?> ExecuteScalarAsync(string query, params MySqlParameter[] parameters)
		{
			try
			{
				using (MySqlConnection connection = new MySqlConnection(connectionString))
				{
					await connection.OpenAsync();
					using (MySqlCommand command = new MySqlCommand(query, connection))
					{
						command.Parameters.AddRange(parameters);
						return await command.ExecuteScalarAsync();
					}
				}
			}
			catch (Exception ex)
			{
				string errorMessage = $"An error occurred while executing SQL query (ExecuteScalarAsync): {query}. Error message: {ex.Message}";
				_Logger?.LogError(ex, errorMessage);
				throw;
			}
		}

		public async Task<DataTable> ExecuteReaderAsync(string query, params MySqlParameter[] parameters)
		{
			try
			{
				using (MySqlConnection connection = new MySqlConnection(connectionString))
				{
					await connection.OpenAsync();
					using (MySqlCommand command = new MySqlCommand(query, connection))
					{
						command.Parameters.AddRange(parameters);
						using (MySqlDataReader reader = await command.ExecuteReaderAsync())
						{
							DataTable dataTable = new DataTable();
							dataTable.Load(reader);
							return dataTable;
						}
					}
				}
			}
			catch (Exception ex)
			{
				string errorMessage = $"An error occurred while executing SQL query (ExecuteReaderAsync): {query}. Error message: {ex.Message}";
				_Logger?.LogError(ex, errorMessage);
				throw;
			}
		}

		public async Task ExecuteWithTransactionAsync(Func<MySqlConnection, MySqlTransaction, Task> executeActions)
		{
			try
			{
				using (MySqlConnection connection = new MySqlConnection(connectionString))
				{
					await connection.OpenAsync();
					using (MySqlTransaction transaction = await connection.BeginTransactionAsync())
					{
						try
						{
							await executeActions(connection, transaction);
							await transaction.CommitAsync();
						}
						catch (Exception ex)
						{
							await transaction.RollbackAsync();
							string errorMessage = $"An error occurred while executing actions within a transaction. Error message: {ex.Message}";
							_Logger?.LogError(ex, errorMessage);
							throw;
						}
					}
				}
			}
			catch (Exception ex)
			{
				string errorMessage = $"An error occurred while setting up a transaction. Error message: {ex.Message}";
				_Logger?.LogError(ex, errorMessage);
				throw;
			}
		}
	}
}
