#nullable enable

using NuGet.Packaging.Core;

namespace NugetToNpmConverter
{
    public interface IPackageFilter
    {
        bool IsExcluded(PackageDependency packageDependency);
    }
}
