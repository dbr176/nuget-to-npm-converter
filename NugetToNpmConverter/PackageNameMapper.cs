#nullable enable


namespace NugetToNpmConverter
{
    public class PackageNameMapper : IPackageNameMapper
    {
        private readonly string _packageNameFormat;

        public PackageNameMapper(string packageNameFormat)
        {
            _packageNameFormat = packageNameFormat;
        }

        public string? Map(string packageName)
            => string.Format(_packageNameFormat, packageName.ToLower());
    }
}
