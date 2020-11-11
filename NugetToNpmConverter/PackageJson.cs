#nullable enable

using System.Collections.Generic;

namespace NugetToNpmConverter
{
    public class PackageJson
    {
        public string name;
        public string displayName;
        public string version;
        public Dictionary<string, string> dependencies = new Dictionary<string, string>();
        public string description;
        public string author;
        public string homepage;
        public string[] keywords;
    }
}
