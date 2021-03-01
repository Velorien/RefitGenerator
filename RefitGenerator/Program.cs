using System.Threading.Tasks;
using System.IO;
using System.CommandLine;
using System.CommandLine.Invocation;

namespace RefitGenerator
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var rootCommand = new RootCommand("A utility to generate Refit client code from OpenApi json or yaml")
            {
                new Option<string>(new[] { "-u", "--url" })
                {
                    Description = "A url to OpenApi json or yaml"
                },
                new Option<FileInfo>(new[] { "-f", "--file" })
                {
                    Description = "Path to OpenApi json or yaml file"
                },
                new Option<DirectoryInfo>(new[] { "-o", "--outputDirectory" }, () => new DirectoryInfo("./"))
                {
                    Description = "Output directory"
                },
                new Option<string>(new[] { "-p", "--projectName" })
                {
                    Description = "Project name and namespace",
                },
                new Option<GroupingStrategy>(new [] { "--groupBy", "--groupingStrategy" }, () => GroupingStrategy.FirstTag)
                {
                    Description = "Strategy for grouping paths into api interfaces"
                },
                new Option<bool>(new[] { "-r", "--removeIfExists" })
                {
                    Description = "Remove target directory if exists"
                },
                new Option<bool>("--executable", "Generate a .NET 5 console app instead of .NET Standard 2.0 library")
            };

            rootCommand.Handler = CommandHandler.Create<GeneratorOptions>(Generator.Generate);

            await rootCommand.InvokeAsync(args);
        }
    }
}
