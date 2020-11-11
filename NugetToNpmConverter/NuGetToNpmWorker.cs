using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NugetToNpmConverter
{
    public class NuGetToNpmWorker : BackgroundService
    {
        private const string _AllowedFrameworks = "Converter:AllowedFrameworks";
        private const string _PackageDirectory = "Converter:PackageDirectory";
        private const string _Recursive = "Converter:Recursive";
        private const string _PlaceholderDirectory = "Converter:PlaceholderDirectory";
        private const string _GeneratePlaceholders = "Converter:GeneratePlaceholders";
        private const string _PreserveExistingPackages = "Converter:PreserveExistingPackages";
        private const string _UseMinVersionAsExact = "Converter:UseMinVersionAsExact";

        private readonly IPackageNameMapper _nameMapper;
        private readonly IPackageFilter _filter;
        private readonly FindPackageByIdResource _findPackageByIdResource;
        private readonly PackageMetadataResource _metadataResource;
        private readonly IConfiguration _configuration;
        private readonly ILogger<NuGetToNpmWorker> _logger;
        private readonly NuGet.Common.ILogger _nugetLogger;
        private readonly SourceCacheContext _cacheContext;

        public NuGetToNpmWorker(
            IPackageNameMapper nameMapper,
            IPackageFilter filter,
            FindPackageByIdResource findPackageByIdResource,
            PackageMetadataResource metadataResource,
            IConfiguration configuration,
            ILogger<NuGetToNpmWorker> logger,
            NuGet.Common.ILogger nugetLogger,
            SourceCacheContext cacheContext)
        {
            _nameMapper = nameMapper;
            _filter = filter;
            _findPackageByIdResource = findPackageByIdResource;
            _metadataResource = metadataResource;
            _configuration = configuration;
            _logger = logger;
            _nugetLogger = nugetLogger;
            _cacheContext = cacheContext;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var allowedFrameworksList = new List<string>();

            _configuration
                .GetSection(_AllowedFrameworks)
                .Bind(allowedFrameworksList);
            var allowedFrameworks = ImmutableList.CreateRange(
                allowedFrameworksList.Select(NuGetFramework.Parse));
            var targetPackage = _configuration["TargetPackage"];

            if (!NuGetVersion.TryParse(_configuration["TargetPackageVersion"], 
                out var targetPackageVersion))
            {
                _logger.LogError("Can't parse TargetPackageVersion");
                return;
            }

            var travered = new HashSet<string>();
            var depInfo = await _findPackageByIdResource
                .GetDependencyInfoAsync(
                targetPackage, targetPackageVersion,
                _cacheContext, _nugetLogger, stoppingToken);
            await GeneratePackage(depInfo, allowedFrameworks, stoppingToken);
            Environment.Exit(0);
        }

        private string GetPackageDirectory(PackageIdentity pi) 
            => $"{_configuration[_PackageDirectory]}/{pi.Id}@{pi.Version}";
        private string GetPlaceholderDirectory(PackageIdentity pi) 
            => $"{_configuration[_PlaceholderDirectory]}/{pi.Id}@{pi.Version}";

        private async Task LoadPackage(
            string targetDirectory,
            FindPackageByIdDependencyInfo info,
            NuGetFramework targetFramework,
            CancellationToken cancellationToken)
        {
            using var stream = new MemoryStream();

            await _findPackageByIdResource.CopyNupkgToStreamAsync(
                info.PackageIdentity.Id,
                info.PackageIdentity.Version,
                stream, _cacheContext, _nugetLogger, 
                cancellationToken);
            using var reader = new PackageArchiveReader(stream);
            var items = await reader.GetLibItemsAsync(cancellationToken);
            var targetItems = items.FirstOrDefault(x => x.TargetFramework == targetFramework);

            await reader.CopyFilesAsync(
                targetDirectory,
                targetItems.Items,
                (a, b, c) => 
                {
                    try
                    {
                        var directory = Path.GetDirectoryName(b);
                        if (!Directory.Exists(directory))
                            Directory.CreateDirectory(directory);
                        using var stream = File.Create(b);
                        c.CopyTo(stream);
                        return b;
                    }
                    catch (Exception e)
                    {
                        _logger.LogCritical(e, "critical exception");
                        return null;
                    }
                },
                _nugetLogger,
                cancellationToken);
        }

        private async Task<bool> CreatePackageAtDir(
            string directory,
            PackageJson packageJson)
        {
            var fileInfo = new FileInfo($"{directory}/package.json");
            var preserve = _configuration.GetValue<bool>(_PreserveExistingPackages);
            var exists = fileInfo.Exists;

            if (exists && preserve) return false;
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(fileInfo.FullName, JsonConvert.SerializeObject(packageJson));
            return true;
        }

        private async Task GeneratePackage(
            FindPackageByIdDependencyInfo info,
            IImmutableList<NuGetFramework> allowedFrameworks,
            CancellationToken cancellationToken,
            string tab = "")
        {
            var key = info.PackageIdentity.Id + "@" + info.PackageIdentity.Version;
            _logger.LogTrace(tab + key);

            var metadata = await _metadataResource.GetMetadataAsync(
                info.PackageIdentity, _cacheContext, _nugetLogger, cancellationToken);

            var packageJson = new PackageJson
            {
                name = _nameMapper.Map(info.PackageIdentity.Id),
                author = metadata.Authors,
                homepage = metadata.ProjectUrl.ToString(),
                description = metadata.Description,
                displayName = info.PackageIdentity.Id,
                version = info.PackageIdentity.Version.ToString(),
                keywords = metadata.Tags?.Split(",") ?? Array.Empty<string>()
            };
            var dependencyGroup =
                info.DependencyGroups.FirstOrDefault(
                    g => allowedFrameworks.FirstOrDefault(f => f == g.TargetFramework) != null);

            var dependencies =
                dependencyGroup is not null
                    ? dependencyGroup.Packages
                        .Where(x => !_filter.IsExcluded(x))
                        .ToArray()
                    : Enumerable.Empty<PackageDependency>();

            FillPackageDependencies(packageJson, dependencies);

            var packageDirectory = GetPackageDirectory(info.PackageIdentity);
            var placeholderDirectory = GetPlaceholderDirectory(info.PackageIdentity);

            if(await CreatePackageAtDir(packageDirectory, packageJson))
            {
                await LoadPackage(
                    packageDirectory,
                    info, dependencyGroup.TargetFramework, cancellationToken);
            }

            if (_configuration.GetValue<bool>(_GeneratePlaceholders))
            {
                await CreatePackageAtDir(placeholderDirectory, packageJson);
            }

            if (_configuration.GetValue<bool>(_Recursive))
            {
                await TraverseDependencies(allowedFrameworks, tab, dependencies, cancellationToken);
            }
        }

        private async Task TraverseDependencies(
            IImmutableList<NuGetFramework> allowedFrameworks, string tab, 
            IEnumerable<PackageDependency> dependencies, 
            CancellationToken cancellationToken)
        {
            foreach (var dep in dependencies)
            {
                var depInfo = await _findPackageByIdResource.GetDependencyInfoAsync(
                    dep.Id, dep.VersionRange.MinVersion, _cacheContext, _nugetLogger,
                    cancellationToken);
                await GeneratePackage(depInfo,
                    allowedFrameworks,
                    cancellationToken, tab + "  ");
            }
        }

        private void FillPackageDependencies(
            PackageJson packageJson, 
            IEnumerable<PackageDependency> dependencies)
        {
            foreach (var p in dependencies)
            {
                if (_configuration.GetValue<bool>(_UseMinVersionAsExact))
                {
                    packageJson.dependencies[_nameMapper.Map(p.Id)] = $"{p.VersionRange.MinVersion}";
                    return;
                }

                var minVersion =
                    p.VersionRange.HasLowerBound
                    ? (p.VersionRange.IsMinInclusive
                        ? $">={p.VersionRange.MinVersion}"
                        : $">{p.VersionRange.MinVersion}")
                    : "";
                var maxVersion =
                    p.VersionRange.HasUpperBound ?
                    (p.VersionRange.IsMaxInclusive
                        ? $"<={p.VersionRange.MaxVersion}"
                        : $"<{p.VersionRange.MaxVersion}")
                    : "";

                packageJson.dependencies[_nameMapper.Map(p.Id)] =
                    $"{minVersion} {maxVersion}";
            }
        }
    }
}
