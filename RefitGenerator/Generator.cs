using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;

namespace RefitGenerator
{
    public class Generator
    {
        const string Indent = "    ";
        const string Indent2 = Indent + Indent;
        const string JpnFormat = "[JsonPropertyName(\"{0}\")]";
        const string RefifFormat = "[{0}(\"{1}\")]";
        const string PropFormat = "public {0} {1} {{ get; set; }}";
        private const string MultipartFormData = "multipart/form-data";

        public static async Task Generate(string url, FileInfo file, DirectoryInfo outputDirectory, string projectName)
        {
            Stream openApiStream = null;
            if (url != null)
            {
                var http = new HttpClient();
                openApiStream = await http.GetStreamAsync(url);
            }
            else if (file != null) openApiStream = File.OpenRead(file.FullName);
            else
            {
                Console.WriteLine("Either a file or a url is required");
                return;
            }

            if (!Directory.Exists(outputDirectory.FullName))
                Directory.CreateDirectory(outputDirectory.FullName);

            if (projectName == null) projectName = outputDirectory.Name;

            var openApiDocument = new OpenApiStreamReader().Read(openApiStream, out var diagnostic);
            openApiStream.Dispose();

            File.Copy(Path.Combine(AppContext.BaseDirectory, "csprojtemplate.xml"), Path.Combine(outputDirectory.FullName, projectName + ".csproj"));
            Directory.CreateDirectory(Path.Combine(outputDirectory.FullName, "Models"));
            Directory.CreateDirectory(Path.Combine(outputDirectory.FullName, "Apis"));

            foreach (var schema in openApiDocument.Components.Schemas)
            {
                string classCode = GetModelClass(schema.Key, projectName, schema.Value.Properties);
                await File.WriteAllTextAsync(Path.Combine(outputDirectory.FullName, "Models", schema.Key + ".cs"), classCode);
            }

            var allApis = new List<string>();
            foreach (var pathGroup in openApiDocument.Paths.GroupBy(x => FirstTagOrDefault(x.Value)))
            {
                string apiName = ToPascalCase(pathGroup.Key);
                allApis.Add(apiName);
                string apiCode = GetApiInterface(pathGroup.Key, projectName, pathGroup);
                await File.WriteAllTextAsync(Path.Combine(outputDirectory.FullName, "Apis", $"I{apiName}Api.cs"), apiCode);
            }

            if (allApis.Count > 1)
            {
                string combinedApiName = GetCombinedApiName(allApis);
                string apiCode = GetCombinedApiInterface(allApis, projectName, combinedApiName);
                await File.WriteAllTextAsync(Path.Combine(outputDirectory.FullName, "Apis", $"I{combinedApiName}Api.cs"), apiCode);
            }
        }

        static string GetCombinedApiName(List<string> allApis)
        {
            string combinedApiName = "Combined";
            int counter = 0;
            while (allApis.Contains(combinedApiName))
            {
                counter++;
                combinedApiName = $"Combined{counter}";
            }

            return combinedApiName;
        }

        static string GetCombinedApiInterface(IEnumerable<string> allApis, string @namespace, string combinedApiName)
        {
            var sb = new StringBuilder();

            sb.AppendLine("namespace " + @namespace + ".Apis");
            sb.AppendLine("{"); // open namespace
            sb.AppendLine($"{Indent}public interface I{combinedApiName}Api : {string.Join(", ", allApis.Select(x => $"I{x}Api"))}");
            sb.AppendLine(Indent + "{");
            WriteClosingCharacters(sb);

            return sb.ToString();
        }

        static string GetApiInterface(string apiName, string @namespace, IEnumerable<KeyValuePair<string, OpenApiPathItem>> paths)
        {
            var sb = new StringBuilder();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.IO;");
            sb.AppendLine("using System.Threading.Tasks;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using Refit;");
            sb.AppendLine($"using {@namespace}.Models;");
            sb.AppendLine();

            sb.AppendLine("namespace " + @namespace + ".Apis");
            sb.AppendLine("{"); // open namespace
            sb.AppendLine(Indent + $"public interface I{ToPascalCase(apiName)}Api");
            sb.AppendLine(Indent + "{"); // open class

            int total = paths.SelectMany(x => x.Value.Operations).Count();
            int current = 0;
            foreach (var path in paths)
            {
                foreach (var operation in path.Value.Operations)
                {
                    current++;
                    if (operation.Value.Deprecated) sb.AppendLine(Indent2 + "[Obsolete]");
                    if (operation.Value.RequestBody?.Content?.ContainsKey(MultipartFormData) ?? false) sb.AppendLine(Indent2 + "[Multipart]");
                    sb.AppendFormat(Indent2 + RefifFormat, operation.Key, path.Key);
                    sb.AppendLine();


                    var success = operation.Value.Responses.FirstOrDefault(x => x.Key.StartsWith("2"));
                    string returnType = success.Key == null ? "Task" : $"Task<{ToCLRType(success.Value.Content.FirstOrDefault().Value?.Schema)}>";
                    string parameters = string.Join(", ", operation.Value.Parameters.Select(ParseParameter)
                                                            .Concat(ParseBody(operation.Value.RequestBody)));
                    sb.AppendLine($"{Indent2}{returnType} {GetOperationName(operation.Value, operation.Key, path.Key)}({parameters});");
                    if (current != total) sb.AppendLine();
                }
            }

            WriteClosingCharacters(sb);
            return sb.ToString();
        }

        private static string ParseParameter(OpenApiParameter parameter)
        {
            var nameSegments = new List<string>();
            string camelCaseName = ToCamelCase(parameter.Name);

            if (parameter.In == ParameterLocation.Query && (parameter.Schema.Type == "object" || parameter.Schema.Type == "array"))
                nameSegments.Add("[Query]");

            if (camelCaseName != parameter.Name) nameSegments.Add($"[AliasAs(\"{parameter.Name}\")]");

            nameSegments.Add(ToCLRType(parameter.Schema));
            nameSegments.Add(camelCaseName);
            return string.Join(" ", nameSegments);
        }

        private static string GetOperationName(OpenApiOperation operation, OperationType operationType, string path)
        {
            if (operation.OperationId != null)
                return ToPascalCase(operation.OperationId);

            return operationType + "__" + string.Join("_", path.Split('/', StringSplitOptions.RemoveEmptyEntries).Where(x => !x.StartsWith("{")).Select(Capitalize));
        }

        private static string[] ParseBody(OpenApiRequestBody body)
        {
            if (body == null || body.Content == null) return Array.Empty<string>();
            if (body.Content.ContainsKey(MultipartFormData))
            {
                var formData = new List<string>();
                foreach (var property in body.Content[MultipartFormData].Schema.Properties)
                {
                    string camelCase = ToCamelCase(property.Key);
                    if (property.Value.Type == "file" || property.Value.Type == "string" && property.Value.Format == "binary")
                    {
                        if (camelCase != property.Key)
                            formData.Add($"[AliasAs(\"{property.Key}\")] Stream {camelCase}");
                        else formData.Add($"Stream {camelCase}");
                    }
                    else
                    {
                        if (camelCase != property.Key)
                            formData.Add($"[AliasAs(\"{property.Key}\")] Dictionary<string, object> {camelCase}");
                        else formData.Add($"Dictionary<string, object> {camelCase}");
                    }
                }

                return formData.ToArray();
            }
            if (body.Content.Any(x => x.Value.Schema.Type == "string" && (x.Value.Schema.Format == "binary" || x.Value.Schema.Format == "base64")))
            {
                return Array.Empty<string>();
            }
            return new [] { $"[Body] {ToCLRType(body.Content.FirstOrDefault().Value?.Schema)} body" };
        }

        static string GetModelClass(string className, string @namespace, IDictionary<string, OpenApiSchema> properties)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Text.Json.Serialization;");
            sb.AppendLine();

            sb.AppendLine("namespace " + @namespace + ".Models");
            sb.AppendLine("{"); // open namespace
            sb.AppendLine(Indent + "public class " + className);
            sb.AppendLine(Indent + "{"); // open class

            WriteModelProperties(sb, properties);
            WriteClosingCharacters(sb);

            return sb.ToString();
        }

        static void WriteClosingCharacters(StringBuilder sb)
        {
            sb.AppendLine(Indent + "}"); // close class
            sb.AppendLine("}"); // close namespace
        }

        static void WriteModelProperties(StringBuilder sb, IDictionary<string, OpenApiSchema> properties)
        {
            int counter = 0;
            foreach (var property in properties)
            {
                counter++;
                sb.AppendFormat(Indent2 + JpnFormat, property.Key);
                sb.AppendLine();
                sb.AppendFormat(Indent2 + PropFormat, ToCLRType(property.Value), ToPascalCase(property.Key));
                sb.AppendLine();
                if (properties.Count != counter) sb.AppendLine();
            }
        }

        static string ToPascalCase(string input) => string.Join("", input.Split('_').Select(Capitalize));

        static string ToCamelCase(string input) 
        {
            var segments = input.Split('_');
            return string.Join("", new string[] { segments[0] }.Concat(segments[1..].Select(Capitalize)));
        }

        static string Capitalize(string input) => input[0].ToString().ToUpper() + input[1..];

        static string FirstTagOrDefault(OpenApiPathItem path) => path.Operations.FirstOrDefault().Value?.Tags?.FirstOrDefault()?.Name ?? "Default";

        static string ToCLRType(OpenApiSchema property) => property switch
        {
            { Type: "array" } => ToCLRType(property.Items) + "[]",
            { Type: "string" } => "string",
            { Type: "boolean" } => "bool",
            { Type: "number", Format: "float" } => "float",
            { Type: "number" } => "double",
            { Type: "integer", Format: "int64" } => "long",
            { Type: "integer" } => "int",
            _ => property?.Reference?.Id ?? "UndefinedReference"
        };
    }
}