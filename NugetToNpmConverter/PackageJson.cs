#nullable enable

using System.Collections.Generic;

namespace NugetToNpmConverter
{
    public class PackageJson
    {
        public string name;
        public string displayName;
        public string version;
        public Dictionary<string, string> dependencies = new();
        public string description;
        public string author;
        public string homepage;
        public string[] keywords;
    }
}
