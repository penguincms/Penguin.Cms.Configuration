using Penguin.Cms.Configuration.Repositories.Providers;
using Penguin.Configuration.Abstractions.Interfaces;
using Penguin.Configuration.Providers;
using Penguin.DependencyInjection.Abstractions.Interfaces;
using Penguin.Messaging.Abstractions.Interfaces;
using Penguin.Messaging.Persistence.Messages;
using Penguin.Persistence.Abstractions.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;

namespace Penguin.Cms.Configuration.Services
{
    /// <summary>
    /// A CMS configuration service that wraps a list of configuration providers and provides accessibility methods
    /// </summary>
    public partial class ConfigurationService : IMessageHandler<Updating<CmsConfiguration>>, IProvideConfigurations, IConsolidateDependencies<IProvideConfigurations>
    {
        /// <summary>
        /// A dictionary of all configurations with value determined by precedence
        /// </summary>
        public Dictionary<string, string> AllConfigurations
        {
            get
            {
                Dictionary<string, string> toReturn = new Dictionary<string, string>();

                foreach (IProvideConfigurations provider in this.Providers)
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

                foreach (IProvideConfigurations provider in this.Providers)
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

        public bool CanWrite => true;

        /// <summary>
        /// A list of child providers used when constucting this instance
        /// </summary>
        public IEnumerable<IProvideConfigurations> Providers { get; protected set; }

        private static ConcurrentDictionary<string, object> CachedValues { get; set; } = new ConcurrentDictionary<string, object>();

        static ConfigurationService()
        {
            CachedValues = new ConcurrentDictionary<string, object>();
        }

        public ConfigurationService()
        {
        }

        /// <summary>
        /// Ordered by most important first. Constructs a new instance of this service
        /// </summary>
        /// <param name="providers">And ordered list of configuration providers</param>
        public ConfigurationService(params IProvideConfigurations[] providers)
        {
            this.Providers = providers.Where(p => !(p is ConfigurationService)).ToList();
        }

        /// <summary>
        /// Ordered by most important first. Constructs a new instance of this service
        /// </summary>
        /// <param name="providers">And ordered list of configuration providers</param>
        public ConfigurationService(IEnumerable<IProvideConfigurations> providers) : this(providers.ToArray())
        {
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
        public static void FlushCache()
        {
            CachedValues = new ConcurrentDictionary<string, object>();
        }

        /// <summary>
        /// Message Handler that removes a configuration from the cache when the value is updated
        /// </summary>
        /// <param name="target">A message containing the configuration to be removed</param>
        public void AcceptMessage(Updating<CmsConfiguration> target)
        {
            Contract.Requires(target != null);

            CachedValues.TryRemove(target.Target.Name, out object _);
        }

        IProvideConfigurations IConsolidateDependencies<IProvideConfigurations>.Consolidate(IEnumerable<IProvideConfigurations> dependencies)
        {
            this.Providers = dependencies;

            return this;
        }

        /// <summary>
        /// Returns all values from all configurations as CMS configuration objects
        /// </summary>
        /// <returns>All values from all configurations as CMS configuration objects</returns>
        public IEnumerable<CmsConfiguration> GetAll()
        {
            List<IProvideConfigurations> providers = this.Providers.ToList();

            List<CmsConfiguration> All = new List<CmsConfiguration>();

            HashSet<string> returnedNames = new HashSet<string>();

            HashSet<string> nullKeys = new HashSet<string>();

            foreach (IProvideConfigurations provider in providers.Where(p => p is RepositoryProvider))
            {
                foreach (CmsConfiguration c in (provider as RepositoryProvider).Repository.All)
                {
                    if (c.Value != null && returnedNames.Add(c.Name))
                    {
                        yield return c;
                    }
                    else if (c.Value is null && !returnedNames.Contains(c.Name))
                    {
                        nullKeys.Add(c.Name);
                    }
                }
            }

            foreach (IProvideConfigurations provider in providers.Where(p => !(p is RepositoryProvider)))
            {
                foreach (KeyValuePair<string, string> kvp in provider.AllConfigurations)
                {
                    if (kvp.Value != null && returnedNames.Add(kvp.Key))
                    {
                        if (nullKeys.Contains(kvp.Key))
                        {
                            nullKeys.Remove(kvp.Key);
                        }

                        yield return new CmsConfiguration()
                        {
                            Name = kvp.Key,
                            Value = kvp.Value
                        };
                    }
                    else if (kvp.Value is null && !returnedNames.Contains(kvp.Key))
                    {
                        nullKeys.Add(kvp.Key);
                    }
                }
            }

            foreach (string nullKey in nullKeys)
            {
                yield return new CmsConfiguration()
                {
                    Name = nullKey,
                    Value = null
                };
            }
        }

        /// <summary>
        /// Gets a configuration value by name
        /// </summary>
        /// <param name="Key">The key of the value to get</param>
        /// <returns>The value (or null) of the requested configuration</returns>
        public string GetConfiguration(string Key)
        {
            string toReturn = null;

            foreach (IProvideConfigurations provider in this.Providers.OrderBy(p => p.CanWrite ? 0 : 1))
            {
                toReturn = provider.GetConfiguration(Key);

                if (toReturn != null)
                {
                    break;
                }
            }

            //For each requested configuration we want to make sure the writable providers are aware of its existence,
            //but we leave the value null so that it does not override any existing return from other providers unless its actually
            //persisted
            foreach (IProvideConfigurations writeProvider in this.Providers.Where(p => p.CanWrite))
            {
                if (writeProvider.GetConfiguration(Key) is null)
                {
                    writeProvider.SetConfiguration(Key, null);
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

            foreach (IProvideConfigurations provider in this.Providers)
            {
                ConnectionString = provider.GetConnectionString(Name);

                if (ConnectionString != null)
                {
                    return ConnectionString;
                }
            }

            return null;
        }

        public bool SetConfiguration(string Name, string Value)
        {
            bool toReturn = false;

            foreach (IProvideConfigurations pc in this.Providers)
            {
                if (pc.CanWrite && pc.SetConfiguration(Name, Value))
                {
                    toReturn = true;
                }
            }

            return toReturn;
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
    }
}