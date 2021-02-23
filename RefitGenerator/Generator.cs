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
    public static class Generator
    {
        const string Indent = "    ";
        const string Indent2 = Indent + Indent;
        const string JpnFormat = "[JsonPropertyName(\"{0}\")]";
        const string RefitAttributeFormat = "[{0}(\"{1}\")]";
        const string PropFormat = "public {0} {1} {{ get; set; }}";
        const string MultipartFormData = "multipart/form-data";
        const string FormDataUrlEncoded = "application/x-www-form-urlencoded";
        const string TemplatesDirectory = "Templates";

        public static async Task Generate(GeneratorOptions options)
        {
            Stream openApiStream = null;
            if (options.Url != null)
            {
                var http = new HttpClient();
                openApiStream = await http.GetStreamAsync(options.Url);
            }
            else if (options.File != null) openApiStream = File.OpenRead(options.File.FullName);
            else
            {
                Console.WriteLine("Either a file or a url is required");
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

                File.Copy(Path.Combine(AppContext.BaseDirectory, TemplatesDirectory, "csprojtemplate.xml"),
                    Path.Combine(options.OutputDirectory.FullName, options.ProjectName + ".csproj"));
                Directory.CreateDirectory(Path.Combine(options.OutputDirectory.FullName, "Models"));
                Directory.CreateDirectory(Path.Combine(options.OutputDirectory.FullName, "Apis"));

                foreach (var schema in openApiDocument.Components.Schemas)
                {
                    string className = schema.Key.ToPascalCase();
                    string classCode = GetModelClass(className, options.ProjectName, schema.Value.Properties);
                    await File.WriteAllTextAsync(Path.Combine(options.OutputDirectory.FullName, "Models", className + ".cs"), classCode);
                }

                foreach (var pathGroup in openApiDocument.Paths.GroupBy(x => FirstTagOrDefault(x.Value)))
                {
                    string apiName = pathGroup.Key.ToPascalCase();
                    string apiCode = GetApiInterface(pathGroup.Key, options.ProjectName, pathGroup);
                    await File.WriteAllTextAsync(Path.Combine(options.OutputDirectory.FullName, "Apis", $"I{apiName}Api.cs"), apiCode);
                }
            }
            catch (IOException)
            {
                Console.WriteLine("Could not write to that location. Maybe the files are in use?");
            }
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

                    sb.AppendLine();

                    if (operation.Deprecated)
                        sb.AppendLine(Indent2 + "[Obsolete]");

                    if (operation.RequestBody?.Content?.ContainsKey(MultipartFormData) ?? false)
                        sb.AppendLine(Indent2 + "[Multipart]"); // is it a multipart upload endpoint?

                    sb.AppendFormat(Indent2 + RefitAttributeFormat, method, path.Key); // write refit attribute
                    sb.AppendLine();

                    // assuming there's at most one type of success response per operation
                    // if there's none or content is not defined - do not expect response content type
                    var success = operation.Responses.FirstOrDefault(x => x.Key.StartsWith("2"));
                    string returnType = success.Key == null || !success.Value.Content.Any() ?
                        "Task" : $"Task<{ToCLRType(success.Value.Content.FirstOrDefault().Value?.Schema)}>";

                    // build parameters list from query, route, body, etc.
                    string parameters = string.Join(", ",
                        operation.Parameters.Select(ParseParameter).Concat(ParseBody(operation.RequestBody)));

                    sb.AppendLine($"{Indent2}{returnType} {GetOperationName(operation, method, path.Key)}({parameters});");
                }
            }

            return string.Format(interfaceTemplate, @namespace, apiName.ToPascalCase(), sb.ToString());
        }

        private static string ParseParameter(OpenApiParameter parameter)
        {
            var nameSegments = new List<string>();
            string camelCaseName = parameter.Name.ToCamelCase();

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
                            parameters.Add($"[AliasAs(\"{propertyName}\"), Body(BodySerializationMethod.UrlEncoded)] {ToCLRType(propertySchema)} {camelCaseName}");
                        else parameters.Add($"[Body(BodySerializationMethod.UrlEncoded)] {ToCLRType(propertySchema)} {camelCaseName}");
                    }
                }

                return parameters;
            }

            return new[] { $"[Body] {ToCLRType(body.Content.FirstOrDefault().Value?.Schema)} body" };
        }

        static string GetModelClass(string className, string @namespace, IDictionary<string, OpenApiSchema> properties)
        {
            var sb = new StringBuilder();
            string modelTemplate = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, TemplatesDirectory, "ModelTemplate.csx"));

            foreach (var property in properties)
            {
                string propertyName = property.Key;
                var propertySchema = property.Value;

                sb.AppendLine();
                sb.AppendFormat(Indent2 + JpnFormat, propertyName);
                sb.AppendLine();
                sb.AppendFormat(Indent2 + PropFormat, ToCLRType(propertySchema), propertyName.ToPascalCase());
                sb.AppendLine();
            }

            return string.Format(modelTemplate, @namespace, className, sb.ToString());
        }

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
            { Reference: { Id: { } id } } => id,
            { AdditionalProperties: { } ap } => $"Dictionary<string, {ToCLRType(ap)}>",
            _ => "Dictionary<string, object>"
        };
    }
}