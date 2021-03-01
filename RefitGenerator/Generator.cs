using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
using RefitGenerator.Helpers;

using static RefitGenerator.Helpers.TypeHelper;
using static RefitGenerator.Helpers.Strings;

namespace RefitGenerator
{
    public static class Generator
    {
        public static async Task Generate(GeneratorOptions options)
        {
            Stream openApiStream = null;
            if (options.Url != null)
            {
                var http = new HttpClient();
                try
                {
                    openApiStream = await http.GetStreamAsync(options.Url);
                }
                catch
                {
                    ConsoleHelper.WriteLineColored("Failed to load data from the given URL", ConsoleColor.Red);
                }
            }
            else if (options.File != null) openApiStream = File.OpenRead(options.File.FullName);
            else
            {
                ConsoleHelper.WriteLineColored("Either a file or a url is required", ConsoleColor.Red);
                return;
            }

            try
            {
                if (options.RemoveIfExists && Directory.Exists(options.OutputDirectory.FullName))
                    Directory.Delete(options.OutputDirectory.FullName, true);

                if (!Directory.Exists(options.OutputDirectory.FullName))
                    Directory.CreateDirectory(options.OutputDirectory.FullName);

                if (options.ProjectName == null) options.ProjectName = options.OutputDirectory.Name;

                var openApiDocument = new OpenApiStreamReader().Read(openApiStream, out var diagnostic);
                openApiStream.Dispose();

                File.Copy(Path.Combine(AppContext.BaseDirectory, TemplatesDirectory, options.Executable ? "CsprojExe.xml" : "CsprojLib.xml"),
                    Path.Combine(options.OutputDirectory.FullName, options.ProjectName + ".csproj"));
                Directory.CreateDirectory(Path.Combine(options.OutputDirectory.FullName, ModelsDirectory));
                Directory.CreateDirectory(Path.Combine(options.OutputDirectory.FullName, ApisDirectory));

                if (openApiDocument.Components == null && openApiDocument.Paths == null)
                {
                    ConsoleHelper.WriteLineColored("Input document is not an OpenApi definition.", ConsoleColor.Red);
                    return;
                }

                foreach (var schema in openApiDocument.Components.Schemas)
                {
                    GetCompoundType(options, schema.Key.ToPascalCase(), schema.Value);
                }

                var allApis = new List<string>();
                foreach (var pathGroup in GetPathGroups(openApiDocument.Paths, options.GroupingStrategy))
                {
                    string apiName = pathGroup.Key.ToPascalCase();
                    allApis.Add(apiName);
                    string apiCode = InterfaceWriter.GetApiInterface(options, pathGroup.Key, pathGroup);
                    await File.WriteAllTextAsync(Path.Combine(options.OutputDirectory.FullName, ApisDirectory, $"I{apiName}Api.cs"), apiCode);
                }

                string clientCode = GetClientClass(options.ProjectName, allApis);
                await File.WriteAllTextAsync(Path.Combine(options.OutputDirectory.FullName, $"ApiClient.cs"), clientCode);

                if (options.Executable)
                {
                    string programTemplate = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, TemplatesDirectory, "ProgramTemplate.csx"));
                    await File.WriteAllTextAsync(
                        Path.Combine(options.OutputDirectory.FullName, "Program.cs"),
                        string.Format(programTemplate, options.ProjectName, openApiDocument.Servers.FirstOrDefault()?.Url ?? "url missing!"));
                }
            }
            catch (IOException)
            {
                ConsoleHelper.WriteLineColored("Could not write to that location.", ConsoleColor.Red);
            }
        }

        private static IEnumerable<IGrouping<string, KeyValuePair<string, OpenApiPathItem>>> GetPathGroups(OpenApiPaths paths, GroupingStrategy groupingStrategy)
        {
            static string FirstTagOrDefault(OpenApiPathItem path) => path.Operations.FirstOrDefault().Value?.Tags?.FirstOrDefault()?.Name ?? "Default";
            static string MostCommonTag(OpenApiPathItem path, Dictionary<string, int> tags, bool mostCommon)
            {
                var pathTags = path.Operations.FirstOrDefault().Value?.Tags;
                if (!pathTags?.Any() ?? true) return "Default";
                var ordered = pathTags.OrderBy(x => tags[x.Name]);
                return mostCommon ? ordered.Last().Name : ordered.First().Name;
            };

            if (groupingStrategy == GroupingStrategy.FirstTag)
            {
                return paths.GroupBy(x => FirstTagOrDefault(x.Value));
            }

            if (groupingStrategy == GroupingStrategy.MostCommonTag || groupingStrategy == GroupingStrategy.LeastCommonTag)
            {
                var allTags = paths.SelectMany(x => x.Value.Operations.SelectMany(x => x.Value.Tags));
                var tagDictionary = allTags.GroupBy(x => x.Name).ToDictionary(k => k.Key, v => v.Count());

                return paths.GroupBy(x => MostCommonTag(x.Value, tagDictionary, groupingStrategy == GroupingStrategy.MostCommonTag));
            }

            throw new ArgumentException($"Unsupported value: {groupingStrategy}", nameof(groupingStrategy));
        }

        private static string GetClientClass(string projectName, List<string> allApis)
        {
            string clientTemplate = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, TemplatesDirectory, "ClientTemplate.csx"));
            string properties = $"{Environment.NewLine}{string.Join(Environment.NewLine, allApis.Select(x => Indent2 + string.Format(ApiPropFormat, x)))}{Environment.NewLine}";
            string constructor = string.Join(Environment.NewLine, allApis.Select(x => $"{Indent3}{x}Api = RestService.For<I{x}Api>(http);"));
            return string.Format(clientTemplate, projectName, constructor, properties);
        }
    }
}