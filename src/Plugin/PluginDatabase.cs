namespace K4System
{
	using System.Data;
	using MySqlConnector;

	public sealed class Database
	{
		private static readonly Lazy<Database> instance = new Lazy<Database>(() => new Database());
		public static Database Instance => instance.Value;

		private MySqlConnection? connection;
		private MySqlTransaction? transaction;

		private Database() { }

		public async Task InitializeAsync(string connectionString)
		{
			if (connection != null)
			{
				if (connection.State == ConnectionState.Open)
				{
					Console.WriteLine("Warning: Attempting to initialize an already open connection.");
					return;
				}

				if (connection.State == ConnectionState.Connecting)
				{
					throw new InvalidOperationException("The database connection is currently being established.");
				}

				if (connection.State != ConnectionState.Closed)
				{
					await connection.CloseAsync();
				}
			}

			connection = new MySqlConnection(connectionString);
			await connection.OpenAsync();
		}

		public async Task BeginTransactionAsync()
		{
			if (connection == null || connection.State != ConnectionState.Open)
			{
				throw new InvalidOperationException("The database connection has not been initialized or is not open.");
			}

			transaction = await connection.BeginTransactionAsync();
		}

		public async Task CommitTransactionAsync()
		{
			if (transaction == null)
			{
				throw new InvalidOperationException("No transaction to commit.");
			}

			await transaction.CommitAsync();
			transaction = null;
		}

		public async Task RollbackTransactionAsync()
		{
			if (transaction == null)
			{
				throw new InvalidOperationException("No transaction to rollback.");
			}

			await transaction.RollbackAsync();
			transaction = null;
		}

		public async Task ExecuteNonQueryAsync(string query, params MySqlParameter[] parameters)
		{
			if (connection == null || connection.State != ConnectionState.Open)
			{
				throw new InvalidOperationException("Database has not been initialized or is not open.");
			}

			using (var command = new MySqlCommand(query, connection, transaction))
			{
				command.Parameters.AddRange(parameters);
				int affectedRows = await command.ExecuteNonQueryAsync();
			}
		}

		public async Task<object?> ExecuteScalarAsync(string query, params MySqlParameter[] parameters)
		{
			if (connection == null || connection.State != ConnectionState.Open)
			{
				throw new InvalidOperationException("Database has not been initialized or is not open.");
			}

			using (var command = new MySqlCommand(query, connection, transaction))
			{
				command.Parameters.AddRange(parameters);
				return await command.ExecuteScalarAsync();
			}
		}

		public async Task<MySqlDataReader?> ExecuteReaderAsync(string query, params MySqlParameter[] parameters)
		{
			if (connection == null || connection.State != ConnectionState.Open)
			{
				throw new InvalidOperationException("Database has not been initialized or is not open.");
			}

			var command = new MySqlCommand(query, connection, transaction);
			command.Parameters.AddRange(parameters);
			return await command.ExecuteReaderAsync() as MySqlDataReader;
		}

		public static string BuildConnectionString(string server, string database, string userId, string password, int port = 3306, string sslMode = "None", bool usePooling = true, uint minPoolSize = 10, uint maxPoolSize = 50)
		{
			var builder = new MySqlConnectionStringBuilder
			{
				Server = server,
				Port = (uint)port,
				Database = database,
				UserID = userId,
				Password = password,
				Pooling = usePooling,
				MinimumPoolSize = minPoolSize,
				MaximumPoolSize = maxPoolSize,
				SslMode = (MySqlSslMode)Enum.Parse(typeof(MySqlSslMode), sslMode, true)
			};

			return builder.ConnectionString;
		}
	}
}