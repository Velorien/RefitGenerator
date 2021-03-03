using Microsoft.OpenApi.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using static RefitGenerator.Helpers.Strings;
using static RefitGenerator.Helpers.TypeHelper;

namespace RefitGenerator.Helpers
{
    static class InterfaceWriter
    {
        private static readonly string[] nonNullableTypes = { "int", "double", "float", "long", "DateTime", "bool" };

        public static string GetApiInterface(GeneratorOptions options, string apiName, IEnumerable<KeyValuePair<string, OpenApiPathItem>> paths)
        {
            var sb = new StringBuilder();
            string interfaceTemplate = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, TemplatesDirectory, "InterfaceTemplate.csx"));

            foreach (var path in paths)
            {
                foreach (var operationDefiniton in path.Value.Operations)
                {
                    var method = operationDefiniton.Key;
                    var operation = operationDefiniton.Value;

                    sb.Append(GetApiInterfaceMethod(options, method, path.Key, operation));
                }
            }

            return string.Format(interfaceTemplate, options.ProjectName, apiName.ToPascalCase(), sb.ToString());
        }

        private static string GetApiInterfaceMethod(GeneratorOptions options, OperationType method, string path, OpenApiOperation operation)
        {
            var sb = new StringBuilder();
            sb.AppendLine();

            if (operation.Deprecated)
                sb.AppendLine(Indent2 + "[Obsolete]");

            var bodyContent = operation.RequestBody?.Content;
            bool isMultipart = bodyContent != null && (bodyContent.ContainsKey(MultipartFormData) || bodyContent.ContainsKey(FormDataUrlEncoded));
            if (isMultipart) sb.AppendLine(Indent2 + "[Multipart]"); // is it a multipart upload endpoint?

            sb.AppendFormat(Indent2 + RefitAttributeFormat, method, path); // write refit attribute
            sb.AppendLine();

            string operationName = GetOperationName(operation, method, path);
            // assuming there's at most one type of success response per operation
            // if there's none or content is not defined - do not expect response content type
            var success = operation.Responses.FirstOrDefault(x => x.Key.StartsWith("2"));
            var responseSchema = success.Value?.Content?.FirstOrDefault().Value?.Schema;
            string returnType = "Task";
            if (responseSchema != null && (!responseSchema.Properties.Any() || responseSchema.Reference != null))
            {
                returnType = $"Task<{ToCLRType(options, operationName, "Response", responseSchema)}>";
            }
            else if (responseSchema != null && responseSchema.Reference == null)
            {
                // if response schema is not in the references section but defined per operation
                ModelWriter.WriteModel(options, operationName + "Response", responseSchema.Properties);
                returnType = $"Task<{operationName}Response>";
            }

            // build parameters list from query, route, body, etc.
            string parameters = string.Join(", ", ParseBody(options, operationName, operation.RequestBody)
                .Concat(operation.Parameters
                    .OrderByDescending(x => x.Required)
                    .Select(x => ParseParameter(options, operationName, x))
                    .Where(x => x != null)));

            sb.AppendLine($"{Indent2}{returnType} {operationName}({parameters});");

            return sb.ToString();
        }

        private static string GetOperationName(OpenApiOperation operation, OperationType operationType, string path)
        {
            if (operation.OperationId != null)
                return operation.OperationId.ToPascalCase();

            // operation id not found, create a name from method and route
            return operationType + "__" + string.Join("_",
                path.Split('/', StringSplitOptions.RemoveEmptyEntries).Where(x => !x.StartsWith("{")).Select(x => x.Capitalize()));
        }

        private static string ParseParameter(GeneratorOptions options, string operationName, OpenApiParameter parameter)
        {
            var nameSegments = new List<string>();
            string camelCaseName = parameter.Name.ToCamelCase();

            if (parameter.In == ParameterLocation.Header)
            {
                if (options.IgnoreAllHeaders || (options.IgnoredHeaders?.Contains(parameter.Name) ?? false))
                {
                    return null;
                }

                nameSegments.Add($"[Header(\"{parameter.Name}\")]");
            }

            if (parameter.In == ParameterLocation.Query)
                nameSegments.Add("[Query]");

            if (parameter.In != ParameterLocation.Header && camelCaseName != parameter.Name)
                nameSegments.Add($"[AliasAs(\"{parameter.Name}\")]");

            string parameterType = ToCLRType(options, operationName, "Parameter", parameter.Schema);
            if (!parameter.Required && !parameterType.EndsWith("?") && nonNullableTypes.Contains(parameterType))
            {
                parameterType += "?";
            }

            nameSegments.Add(parameterType);
            if (!parameter.Required && options.AddEqualsNullToOptionalParameters)
            {
                camelCaseName += " = null";
            }

            nameSegments.Add(camelCaseName);
            return string.Join(" ", nameSegments);
        }

        private static IEnumerable<string> ParseBody(GeneratorOptions options, string operationName, OpenApiRequestBody body)
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
                            parameters.Add($"[AliasAs(\"{propertyName}\")] {ToCLRType(options, operationName, "Parameter", propertySchema)} {camelCaseName}");
                        else parameters.Add($"{ToCLRType(options, operationName, "Parameter", propertySchema)} {camelCaseName}");
                    }
                }

                return parameters;
            }

            return new[] { $"[Body] {ToCLRType(options, operationName, "Body", body.Content.FirstOrDefault().Value?.Schema)} body" };
        }
    }
}
