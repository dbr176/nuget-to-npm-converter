#nullable enable


namespace NugetToNpmConverter
{
    public interface IPackageNameMapper
    {
        string? Map(string packageName);
    }
}
