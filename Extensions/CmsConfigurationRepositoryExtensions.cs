using Penguin.Persistence.Abstractions.Interfaces;
using System.Linq;

namespace Penguin.Cms.Configuration.Extensions
{
    public static class CmsConfigurationRepositoryExtensions
    {
        public static CmsConfiguration GetByName(this IRepository<CmsConfiguration> repository, string Name) => repository?.FirstOrDefault(c => c.Name == Name);

        public static string GetValueByName(this IRepository<CmsConfiguration> repository, string Name) => repository?.FirstOrDefault(c => c.Name == Name)?.Value;

        public static bool SetValue(this IRepository<CmsConfiguration> repository, string Name, string Value)
        {
            if (repository is null)
            {
                return false;
            }
            else
            {
                using (IWriteContext context = repository.WriteContext())
                {
                    CmsConfiguration existing = repository.FirstOrDefault(c => c.Name == Name);

                    if (existing is null)
                    {
                        repository.Add(new CmsConfiguration()
                        {
                            Name = Name,
                            Value = Value
                        });
                    }
                    else if (existing.Value != Value)
                    {
                        existing.Value = Value;
                        repository.Update(existing);
                    }
                }

                return true;
            }
        }
    }
}