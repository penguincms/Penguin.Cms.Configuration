using Penguin.Cms.Entities;

namespace Penguin.Cms.Configuration
{
    /// <summary>
    /// A key value pair representing a database persistable configuration
    /// </summary>
    public class CmsConfiguration : Entity
    {
        /// <summary>
        /// The Key for the configuration
        /// </summary>
        public string Name { get => this.ExternalId; set => this.ExternalId = value; }

        /// <summary>
        /// The value of the configuration
        /// </summary>
        public string Value { get; set; }
    }
}