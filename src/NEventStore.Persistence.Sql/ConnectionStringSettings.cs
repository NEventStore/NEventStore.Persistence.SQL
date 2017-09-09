namespace NEventStore.Persistence.Sql
{
#if NETSTANDARD2_0
    /// <summary>
    /// System.Configuration was not ported to netstandard, let's recreate the class
    /// </summary>
    public class ConnectionStringSettings
    {
        public string ConnectionString { get; set; }

        public string Name { get; set; }

        public string ProviderName { get; set; }

        public ConnectionStringSettings()
        {
        }

        public ConnectionStringSettings(string name, string connectionString, string providerName)
        {
            Name = name;
            ConnectionString = connectionString;
            ProviderName = providerName;
        }
    }
#endif
}
