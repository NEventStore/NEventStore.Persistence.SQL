namespace NEventStore.Persistence.Sql.Tests
{
	internal static class DotEnvFile
	{
		private static readonly object SyncRoot = new object();
		private static bool _loaded;

		public static void Load()
		{
			if (_loaded) return;

			lock (SyncRoot)
			{
				if (_loaded) return;

				var envFile = FindEnvFile(Environment.CurrentDirectory)
					?? FindEnvFile(AppContext.BaseDirectory);

				if (envFile != null) Load(envFile);
				_loaded = true;
			}
		}

		private static string? FindEnvFile(string startDirectory)
		{
			var directory = new DirectoryInfo(startDirectory);
			while (directory != null)
			{
				var candidate = Path.Combine(directory.FullName, ".env");
				if (File.Exists(candidate)) return candidate;
				directory = directory.Parent;
			}
			return null;
		}

		private static void Load(string path)
		{
			foreach (var sourceLine in File.ReadLines(path))
			{
				var line = sourceLine.Trim();
				if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
				if (line.StartsWith("export ", StringComparison.Ordinal)) line = line.Substring("export ".Length).TrimStart();

				var separatorIndex = line.IndexOf('=');
				if (separatorIndex <= 0) continue;

				var key = line.Substring(0, separatorIndex).Trim();
				if (key.Length == 0 || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key, EnvironmentVariableTarget.Process))) continue;

				var value = line.Substring(separatorIndex + 1).Trim();
				if (value.Length >= 2 && ((value[0] == '"' && value[value.Length - 1] == '"') || (value[0] == '\'' && value[value.Length - 1] == '\'')))
				{
					value = value.Substring(1, value.Length - 2);
				}

				Environment.SetEnvironmentVariable(key, value, EnvironmentVariableTarget.Process);
			}
		}
	}
}
