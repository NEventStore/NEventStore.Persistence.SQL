namespace NEventStore.Persistence.Sql.Tests
{
	internal static class EnvironmentFileConnectionString
	{
		private const string SelectorName = "NEVENTSTORE_ENVIRONMENT";

		public static string? TryBuild(string providerName)
		{
			var selectedEnvironment = Environment.GetEnvironmentVariable(SelectorName, EnvironmentVariableTarget.Process) ?? "dynamic";
			selectedEnvironment = selectedEnvironment.Trim().ToLowerInvariant();
			if (selectedEnvironment != "dynamic" && selectedEnvironment != "debug" && selectedEnvironment != "test" && selectedEnvironment != "ci")
			{
				throw new InvalidOperationException(
					string.Format("Unsupported {0} value '{1}'. Expected dynamic, debug, test, or ci.", SelectorName, selectedEnvironment));
			}

			var fileName = string.Format(".env.{0}", selectedEnvironment);
			var filePath = FindFile(fileName);
			if (filePath == null)
			{
				return null;
			}

			var values = ReadFile(filePath);
			return Build(providerName, values, filePath);
		}

		private static string? FindFile(string fileName)
		{
			var searched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (var startDirectory in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
			{
				var directory = new DirectoryInfo(startDirectory);
				while (directory != null && searched.Add(directory.FullName))
				{
					var candidate = Path.Combine(directory.FullName, fileName);
					if (File.Exists(candidate))
					{
						return candidate;
					}
					directory = directory.Parent;
				}
			}

			return null;
		}

		private static Dictionary<string, string> ReadFile(string filePath)
		{
			var values = new Dictionary<string, string>(StringComparer.Ordinal);
			foreach (var rawLine in File.ReadLines(filePath))
			{
				var line = rawLine.Trim();
				if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
				{
					continue;
				}

				var separator = line.IndexOf('=');
				if (separator <= 0)
				{
					throw new InvalidOperationException(string.Format("Invalid line in '{0}': {1}", filePath, rawLine));
				}

				values[line.Substring(0, separator).Trim()] = line.Substring(separator + 1).Trim().Trim('"');
			}
			return values;
		}

		private static string Build(string providerName, IReadOnlyDictionary<string, string> values, string filePath)
		{
			switch (providerName)
			{
				case "MsSql":
					return string.Format(
						"Server={0},{1};Database={2};User Id={3};Password={4};TrustServerCertificate=True;",
						Get(values, "SQLSERVER_HOST", filePath), Get(values, "SQLSERVER_PORT", filePath),
						Get(values, "SQLSERVER_DATABASE", filePath), Get(values, "SQLSERVER_USERNAME", filePath),
						Get(values, "SQLSERVER_PASSWORD", filePath));
				case "MySql":
					return string.Format(
						"Server={0};Port={1};Database={2};Uid={3};Pwd={4};AutoEnlist=false;",
						Get(values, "MYSQL_HOST", filePath), Get(values, "MYSQL_PORT", filePath),
						Get(values, "MYSQL_DATABASE", filePath), Get(values, "MYSQL_USERNAME", filePath),
						Get(values, "MYSQL_PASSWORD", filePath));
				case "PostgreSql":
					return string.Format(
						"Server={0};Port={1};Database={2};Uid={3};Pwd={4};Enlist=false;",
						Get(values, "POSTGRES_HOST", filePath), Get(values, "POSTGRES_PORT", filePath),
						Get(values, "POSTGRES_DATABASE", filePath), Get(values, "POSTGRES_USERNAME", filePath),
						Get(values, "POSTGRES_PASSWORD", filePath));
				case "Oracle":
					return string.Format(
						"Data Source={0}:{1}/{2};User Id={3};Password={4};Persist Security Info=True;",
						Get(values, "ORACLE_HOST", filePath), Get(values, "ORACLE_PORT", filePath),
						Get(values, "ORACLE_SERVICE", filePath), Get(values, "ORACLE_USERNAME", filePath),
						Get(values, "ORACLE_PASSWORD", filePath));
				default:
					throw new InvalidOperationException(string.Format("No .env mapping exists for provider '{0}'.", providerName));
			}
		}

		private static string Get(IReadOnlyDictionary<string, string> values, string key, string filePath)
		{
			if (values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
			{
				return value;
			}

			throw new InvalidOperationException(string.Format("Required value '{0}' is missing from '{1}'.", key, filePath));
		}
	}
}
