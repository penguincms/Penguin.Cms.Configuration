using Penguin.Cms.Configuration.Extensions;
using Penguin.Configuration.Abstractions.Interfaces;
using Penguin.DependencyInjection.Abstractions.Attributes;
using Penguin.Persistence.Abstractions.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Penguin.Cms.Configuration.Repositories.Providers
{
    /// <summary>
    /// A configuation provider that wraps a repository for cms configurations
    /// </summary>
    [Register(DependencyInjection.Abstractions.Enums.ServiceLifetime.Scoped, typeof(IProvideConfigurations))]
    public class RepositoryProvider : IProvideConfigurations
    {
        /// <summary>
        /// All configurations found in the repository
        /// </summary>
        public Dictionary<string, string> AllConfigurations => this.Repository.All.ToDictionary(k => k.Name, v => v.Value);

        /// <summary>
        /// Not used
        /// </summary>
        public Dictionary<string, string> AllConnectionStrings => new Dictionary<string, string>();

        public bool CanWrite => true;

        /// <summary>
        /// The Repository used when constructing this instance
        /// </summary>
        public IRepository<CmsConfiguration> Repository { get; protected set; }

        /// <summary>
        /// Creates a new instance of this configuration provider
        /// </summary>
        /// <param name="provider">The repository implementation to wrap</param>
        public RepositoryProvider(IRepository<CmsConfiguration> provider)
        {
            if (provider is null)
            {
                throw new ArgumentNullException(nameof(provider), $"Can not create instance of {nameof(RepositoryProvider)} with null {nameof(provider)}");
            }

            this.Repository = provider;
        }

        /// <summary>
        /// Gets a configuration by Key
        /// </summary>
        /// <param name="Key">The Key of the configuration to get</param>
        /// <returns>The value (or null) of the configuration</returns>
        public string GetConfiguration(string Key) => this.Repository.Where(k => k.Name == Key).ToList().LastOrDefault()?.Value;

        /// <summary>
        /// Unused
        /// </summary>
        /// <param name="Name">Unused</param>
        /// <returns>Null</returns>
        public string GetConnectionString(string Name) => null;

        public bool SetConfiguration(string Name, string Value) => this.Repository.SetValue(Name, Value);
    }
}