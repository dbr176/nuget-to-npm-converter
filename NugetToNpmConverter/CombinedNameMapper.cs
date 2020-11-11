#nullable enable

using System.Collections.Generic;
using System.Linq;

namespace NugetToNpmConverter
{
    public class CombinedNameMapper : IPackageNameMapper
    {
        private readonly IEnumerable<IPackageNameMapper> _mappers;

        public CombinedNameMapper(IEnumerable<IPackageNameMapper> mappers)
        {
            _mappers = mappers;
        }

        public string? Map(string packageName)
            => _mappers
            .Select(m => m.Map(packageName))
            .FirstOrDefault(x => x != null);
    }
}
