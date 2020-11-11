#nullable enable

using System.Collections.Generic;

namespace NugetToNpmConverter
{
    public class CustomPackageNameMapper : IPackageNameMapper
    {
        private readonly Dictionary<string, string> _mappings;

        public CustomPackageNameMapper(Dictionary<string, string> mappings)
        {
            _mappings = mappings;
        }

        public string? Map(string packageName)
        {
            if (_mappings.TryGetValue(packageName, out var format))
                return string.Format(format, packageName);
            return null;
        }
    }
}
