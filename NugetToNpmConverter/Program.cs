#nullable enable

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using System;
using System.Collections.Generic;

namespace NugetToNpmConverter
{
    public class Program
    {
        public static void Main(string[] args)
        {
            using var host = CreateHostBuilder(args).Build();
            host.Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureHostConfiguration(config =>
                {
                    config.AddCommandLine(args);
                })
                .ConfigureServices((hostContext, services) =>
                {
                    services
                        .AddHostedService<NuGetToNpmWorker>()
                        .AddSingleton<IPackageNameMapper>(CreatePackageNameMapper)
                        .AddSingleton<NuGet.Common.ILogger, NuGetLoggingAdapter>()
                        .AddSingleton<SourceCacheContext>()
                        .AddSingleton(sp =>
                            Repository.Factory.GetCoreV3(sp
                                .GetService<IConfiguration>()!["NuGet:PackageSource"]))
                        .AddSingleton(sp =>
                            sp.GetService<SourceRepository>()!.GetResource<FindPackageByIdResource>())
                        .AddSingleton(sp => 
                            sp.GetService<SourceRepository>()!.GetResource<PackageMetadataResource>())
                        .AddSingleton<IPackageFilter>(sp => 
                            new ByIdPackageFilter(GetExcludedLibraries(sp)));
                });

        private static List<string> GetExcludedLibraries(IServiceProvider sp)
        {
            var list = new List<string>();
            sp.GetService<IConfiguration>()
                !.GetSection("Converter:ExcludedLibraries:ByName")
                .Bind(list);
            return list;
        }

        private static IPackageNameMapper CreatePackageNameMapper(IServiceProvider sp)
        {
            var customMappings = new Dictionary<string, string>();
            var config = sp.GetService<IConfiguration>()!;
            config.GetSection("Converter:CustomNameMappings").Bind(customMappings);
            var customMapper = new CustomPackageNameMapper(customMappings);
            var defaultMapper =
                new PackageNameMapper(config["Converter:NameMapping"]);

            return new CombinedNameMapper(new IPackageNameMapper[]
            {
                customMapper,
                defaultMapper
            });
        }
    }
}
