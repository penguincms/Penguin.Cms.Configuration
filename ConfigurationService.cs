using Penguin.Configuration;
using Penguin.Configuration.Abstractions;
using Penguin.DependencyInjection.Abstractions;
using Penguin.Messaging.Abstractions.Interfaces;
using Penguin.Messaging.Persistence.Messages;
using Penguin.Persistence.Abstractions.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Linq;

namespace Penguin.Cms.Configurations
{
    /// <summary>
    /// A CMS configuration service that wraps a list of configuration providers and provides accessibility methods
    /// </summary>
    public partial class ConfigurationService : ISelfRegistering, IMessageHandler, IProvideConfigurations
    {
        /// <summary>
        /// A dictionary of all configurations with value determined by precedence
        /// </summary>
        public Dictionary<string, string> AllConfigurations
        {
            get
            {
                Dictionary<string, string> toReturn = new Dictionary<string, string>();

                foreach (IProvideConfigurations provider in Providers)
                {
                    foreach (KeyValuePair<string, string> kvp in provider.AllConfigurations)
                    {
                        if (!toReturn.ContainsKey(kvp.Key))
                        {
                            toReturn.Add(kvp.Key, kvp.Value);
                        }
                    }
                }

                return toReturn;
            }
        }

        /// <summary>
        /// A dictionary of all connection strings with value determined by precendence
        /// </summary>
        public Dictionary<string, string> AllConnectionStrings
        {
            get
            {
                Dictionary<string, string> toReturn = new Dictionary<string, string>();

                foreach (IProvideConfigurations provider in Providers)
                {
                    foreach (KeyValuePair<string, string> kvp in provider.AllConnectionStrings)
                    {
                        if (!toReturn.ContainsKey(kvp.Key))
                        {
                            toReturn.Add(kvp.Key, kvp.Value);
                        }
                    }
                }

                return toReturn;
            }
        }

        /// <summary>
        /// If any configuration repositories are passed in when creating this object, this returns the active repository
        /// </summary>
        public IRepository<CmsConfiguration> ConfigurationRepository => (Providers.SingleOrDefault(p => p is RepositoryProvider) as RepositoryProvider)?.Repository;

        /// <summary>
        /// Simply checks all configurations for a "Debug" bool
        /// </summary>
        public bool Debug => GetBool("Debug");

        /// <summary>
        /// A list of child providers used when constucting this instance
        /// </summary>
        public IEnumerable<IProvideConfigurations> Providers { get; protected set; }

        static ConfigurationService()
        {
            CachedValues = new ConcurrentDictionary<string, object>();
        }

        /// <summary>
        /// Ordered by most important first. Constructs a new instance of this service
        /// </summary>
        /// <param name="providers">And ordered list of configuration providers</param>
        public ConfigurationService(params IProvideConfigurations[] providers)
        {
            Providers = providers;
        }

        /// <summary>
        /// Creates a new instance of this service using the provided repository and IConfiguration
        /// </summary>
        /// <param name="configurationRepository">A repository implementation for accessing database configurations</param>
        /// <param name="configuration">An IConfiguration object used by .Net Core applications</param>
        public ConfigurationService(IRepository<CmsConfiguration> configurationRepository, Microsoft.Extensions.Configuration.IConfiguration configuration) : this(new RepositoryProvider(configurationRepository), new JsonProvider(configuration))
        {
        }

        /// <summary>
        /// Flushes all values cached (static) by the configuration service
        /// </summary>
        public static void FlushCache() => CachedValues = new ConcurrentDictionary<string, object>();

        /// <summary>
        /// Recursively searches for a connection string by name. Allows for linking connection strings to eachother by name
        /// </summary>
        /// <param name="toTest">The name (or connection string) to return</param>
        /// <returns>The furthest resolvable value representing the connection string</returns>
        public string FindConnectionString(string toTest = "DefaultConnectionString")
        {
            string ConnectionString;

            if (this.GetConnectionString(toTest) != null)
            {
                ConnectionString = this.GetConnectionString(toTest);
            }
            else if (!string.IsNullOrWhiteSpace(this.GetConfiguration(toTest)))
            {
                ConnectionString = FindConnectionString(this.GetConfiguration(toTest));
            }
            else
            {
                ConnectionString = toTest;
            }

            if (ConnectionString.StartsWith("name=", StringComparison.OrdinalIgnoreCase))
            {
                ConnectionString = ConnectionString.Replace("name=", "");
                ConnectionString = ConfigurationManager.ConnectionStrings[ConnectionString].ConnectionString;
            }

            return ConnectionString;
        }

        /// <summary>
        /// Returns all values from all configurations as CMS configuration objects
        /// </summary>
        /// <returns>All values from all configurations as CMS configuration objects</returns>
        public List<CmsConfiguration> GetAll()
        {
            List<CmsConfiguration> All = new List<CmsConfiguration>();

            foreach (IProvideConfigurations provider in Providers)
            {
                if (provider is RepositoryProvider)
                {
                    All.AddRange((provider as RepositoryProvider).Repository.All.ToList());
                }
                else
                {
                    foreach (KeyValuePair<string, string> kvp in provider.AllConfigurations)
                    {
                        if (!All.Any(c => c.Name == kvp.Key))
                        {
                            All.Add(new CmsConfiguration()
                            {
                                Name = kvp.Key,
                                Value = kvp.Value
                            });
                        }
                    }
                }
            }

            return All;
        }

        /// <summary>
        /// Gets a configuration value as a bool
        /// </summary>
        /// <param name="Name">the name of the configuration value to get</param>
        /// <returns>The configuration value, or false if null</returns>
        public bool GetBool(string Name)
        {
            string toReturn = GetConfiguration(Name);
            return toReturn != null && bool.Parse(toReturn);
        }

        /// <summary>
        /// Returns a value from ONLY the IRepository if this object was constructed using one
        /// </summary>
        /// <param name="Name">The name of the configuration to get</param>
        /// <returns>The value (or null) of the configuration</returns>
        public CmsConfiguration GetCmsConfiguration(string Name)
        {
            CmsConfiguration toReturn = GetFromRepository(Name);

            if (toReturn is null)
            {
                string Value = GetConfiguration(Name);

                if (!string.IsNullOrWhiteSpace(Value))
                {
                    toReturn = new CmsConfiguration()
                    {
                        Name = Name,
                        Value = Value
                    };
                }
            }

            return toReturn;
        }

        /// <summary>
        /// Gets a configuration value by name
        /// </summary>
        /// <param name="Key">The key of the value to get</param>
        /// <returns>The value (or null) of the requested configuration</returns>
        public string GetConfiguration(string Key)
        {
            string toReturn = null;

            foreach (IProvideConfigurations provider in Providers)
            {
                toReturn = provider.GetConfiguration(Key);

                if (toReturn != null)
                {
                    return toReturn;
                }
            }

            return toReturn;
        }

        /// <summary>
        /// Gets a connection string by name
        /// </summary>
        /// <param name="Name">The name of the connection string</param>
        /// <returns>The value (or null) of the connection string</returns>
        public string GetConnectionString(string Name)
        {
            string ConnectionString;

            foreach (IProvideConfigurations provider in Providers)
            {
                ConnectionString = provider.GetConnectionString(Name);

                if (ConnectionString != null)
                {
                    return ConnectionString;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets a configuration as an int
        /// </summary>
        /// <param name="Name">The name of the configuration to get</param>
        /// <returns>the int representation (or 0 if null)</returns>
        public int GetInt(string Name)
        {
            string toReturn = GetConfiguration(Name);
            return toReturn == null ? 0 : int.Parse(toReturn, NumberStyles.Integer, CultureInfo.CurrentCulture);
        }

        /// <summary>
        /// Attempts to set the value of a configuration in a configuration repository (if provided)
        /// </summary>
        /// <param name="key">The key of the value to set</param>
        /// <param name="value">The new value of the configuration</param>
        public void Set(string key, string value)
        {
            this.Set(key, value);
        }

        /// <summary>
        /// Attempts to get a configuration value without fail
        /// </summary>
        /// <param name="key">The key of the value to get</param>
        /// <param name="value">a ref to the value to set</param>
        /// <returns>A bool indicating whether or not an error occurred</returns>
        public bool TryGet(string key, out string value)
        {
            try
            {
                value = this.GetConfiguration(key);
                return true;
            }
            catch (Exception)
            {
                value = null;
                return false;
            }
        }

        /// <summary>
        /// Message Handler that removes a configuration from the cache when the value is updated
        /// </summary>
        /// <param name="target">A message containing the configuration to be removed</param>
        public void Update(Updating<CmsConfiguration> target)
        {
            Contract.Requires(target != null);

            CachedValues.TryRemove(target.Target.Name, out object _);
            
        }

        private static ConcurrentDictionary<string, object> CachedValues { get; set; } = new ConcurrentDictionary<string, object>();

        private CmsConfiguration GetFromRepository(string Name)
        {
            if (ConfigurationRepository is null)
            {
                return null;
            }

            CmsConfiguration Value = ConfigurationRepository.FirstOrDefault(c => c.Name == Name);

            if (Value != null)
            {
                if (CachedValues.ContainsKey(Name))
                {
                    CachedValues[Name] = Value?.Value;
                }
                else
                {
                    CachedValues.TryAdd(Name, Value?.Value);
                }
            }

            return Value;
        }
    }
}