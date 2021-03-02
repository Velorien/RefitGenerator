using System.Collections.Generic;
using System.IO;

namespace RefitGenerator
{
    public class GeneratorOptions
    {
        public string Url { get; set; }
        public FileInfo File { get; set; }
        public DirectoryInfo OutputDirectory { get; set; }
        public string ProjectName { get; set; }
        public bool RemoveIfExists { get; set; }
        public bool Executable { get; set; }
        public GroupingStrategy GroupingStrategy { get; set; }
        public Dictionary<string, string> SimpleTypeMap { get; } = new Dictionary<string, string>();
    }

    public enum GroupingStrategy
    {
        FirstTag, MostCommonTag, LeastCommonTag
    }
}
