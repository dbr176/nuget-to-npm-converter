#nullable enable

using NuGet.Packaging.Core;
using System.Collections.Generic;

namespace NugetToNpmConverter
{
    public class ByIdPackageFilter : IPackageFilter
    {
        private readonly HashSet<string> _names;

        public ByIdPackageFilter(IEnumerable<string> names)
        {
            _names = new(names);
        }

        public bool IsExcluded(PackageDependency packageDependency)
            => _names.Contains(packageDependency.Id);
    }
}
