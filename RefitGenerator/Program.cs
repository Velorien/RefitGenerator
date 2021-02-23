﻿using System.Threading.Tasks;
using System.IO;
using System.CommandLine;
using System.CommandLine.Invocation;

namespace RefitGenerator
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var rootCommand = new RootCommand
            {
                new Option<string>(
                    aliases: new [] { "-u", "--url" },
                    description: "A url to OpenApi json or yaml"
                ),
                new Option<FileInfo>(
                    aliases: new[] { "-f", "--file" },
                    description: "Path to OpenApi json or yaml file"
                ),
                new Option<DirectoryInfo>(
                    aliases: new[] { "-o", "--outputDirectory" },
                    description: "Destination directory",
                    getDefaultValue: () =>  new DirectoryInfo("./")
                ),
                new Option<string>(
                    aliases: new[] { "-p", "--projectName" },
                    description: "Project name and namespace"
                )
            };

            rootCommand.Handler = CommandHandler.Create<string, FileInfo, DirectoryInfo, string>(Generator.Generate);

            await rootCommand.InvokeAsync(args);
        }
    }
}