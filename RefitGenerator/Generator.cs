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
                    ModelWriter.WriteModel(options, schema.Key.ToPascalCase(), schema.Value.Properties);
                }

                var allApis = new List<string>();
                foreach (var pathGroup in GetPathGroups(openApiDocument.Paths, options.GroupingStrategy))
                {
                    string apiName = pathGroup.Key.ToPascalCase();
                    allApis.Add(apiName);
                    string apiCode = GetApiInterface(pathGroup.Key, options.ProjectName, pathGroup);
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

        static string GetApiInterface(string apiName, string @namespace, IEnumerable<KeyValuePair<string, OpenApiPathItem>> paths)
        {
            var sb = new StringBuilder();
            string interfaceTemplate = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, TemplatesDirectory, "InterfaceTemplate.csx"));

            foreach (var path in paths)
            {
                foreach (var operationDefiniton in path.Value.Operations)
                {
                    var method = operationDefiniton.Key;
                    var operation = operationDefiniton.Value;

                    sb.Append(GetApiInterfaceMethod(method, path.Key, operation));
                }
            }

            return string.Format(interfaceTemplate, @namespace, apiName.ToPascalCase(), sb.ToString());
        }

        private static string GetApiInterfaceMethod(OperationType method, string path, OpenApiOperation operation)
        {
            var sb = new StringBuilder();
            sb.AppendLine();

            if (operation.Deprecated)
                sb.AppendLine(Indent2 + "[Obsolete]");

            var bodyContent = operation.RequestBody?.Content;
            if (bodyContent?.ContainsKey(MultipartFormData) ?? false)
                sb.AppendLine(Indent2 + "[Multipart]"); // is it a multipart upload endpoint?

            sb.AppendFormat(Indent2 + RefitAttributeFormat, method, path); // write refit attribute
            sb.AppendLine();

            // assuming there's at most one type of success response per operation
            // if there's none or content is not defined - do not expect response content type
            var success = operation.Responses.FirstOrDefault(x => x.Key.StartsWith("2"));
            var responseSchema = success.Value?.Content?.FirstOrDefault().Value?.Schema;
            string returnType = responseSchema == null ? "Task" : $"Task<{ToCLRType(responseSchema)}>";

            // build parameters list from query, route, body, etc.
            string parameters = string.Join(", ",
                operation.Parameters.Select(ParseParameter).Where(x => x != null).Concat(ParseBody(operation.RequestBody)));

            sb.AppendLine($"{Indent2}{returnType} {GetOperationName(operation, method, path)}({parameters});");

            return sb.ToString();
        }

        private static string ParseParameter(OpenApiParameter parameter)
        {
            // todo: support it somehow
            if (parameter.In == ParameterLocation.Header) return null;

            var nameSegments = new List<string>();
            string camelCaseName = parameter.Name.ToCamelCase();

            if (parameter.In == ParameterLocation.Query)
                nameSegments.Add("[Query]");

            if (camelCaseName != parameter.Name) nameSegments.Add($"[AliasAs(\"{parameter.Name}\")]");

            nameSegments.Add(ToCLRType(parameter.Schema));
            nameSegments.Add(camelCaseName);
            return string.Join(" ", nameSegments);
        }

        private static string GetOperationName(OpenApiOperation operation, OperationType operationType, string path)
        {
            if (operation.OperationId != null)
                return operation.OperationId.ToPascalCase();

            // operation id not found, create a name from method and route
            return operationType + "__" + string.Join("_",
                path.Split('/', StringSplitOptions.RemoveEmptyEntries).Where(x => !x.StartsWith("{")).Select(x => x.Capitalize()));
        }

        private static IEnumerable<string> ParseBody(OpenApiRequestBody body)
        {
            if (body == null || body.Content == null) return Array.Empty<string>();
            if (body.Content.ContainsKey(MultipartFormData) || body.Content.ContainsKey(FormDataUrlEncoded))
            {
                var parameters = new List<string>();
                body.Content.TryGetValue(MultipartFormData, out var multipart);
                body.Content.TryGetValue(FormDataUrlEncoded, out var formData);

                foreach (var property in (multipart ?? formData).Schema.Properties)
                {
                    string propertyName = property.Key;
                    var propertySchema = property.Value;
                    string camelCaseName = propertyName.ToCamelCase();

                    if (propertySchema.Type == "array" && propertySchema.Items.Type == "string" && propertySchema.Items.Format == "binary")
                    {
                        if (camelCaseName != propertyName)
                            parameters.Add($"[AliasAs(\"{propertyName}\")] IEnumerable<StreamPart> {camelCaseName}");
                        else parameters.Add($"IEnumerable<StreamPart> {camelCaseName}");
                    }
                    else if (propertySchema.Type == "file" || propertySchema.Type == "string" && propertySchema.Format == "binary")
                    {
                        if (camelCaseName != propertyName)
                            parameters.Add($"[AliasAs(\"{propertyName}\")] StreamPart {camelCaseName}");
                        else parameters.Add($"StreamPart {camelCaseName}");
                    }
                    else
                    {
                        if (camelCaseName != propertyName)
                            parameters.Add($"[AliasAs(\"{propertyName}\")] {ToCLRType(propertySchema)} {camelCaseName}");
                        else parameters.Add($"{ToCLRType(propertySchema)} {camelCaseName}");
                    }
                }

                return parameters;
            }

            return new[] { $"[Body] {ToCLRType(body.Content.FirstOrDefault().Value?.Schema)} body" };
        }
    }
}