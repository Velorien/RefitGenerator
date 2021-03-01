using Microsoft.OpenApi.Models;

namespace RefitGenerator.Helpers
{
    static class TypeHelper
    {
        public static string ToCLRType(OpenApiSchema property) => property switch
        {
            { Type: "array" } => ToCLRType(property.Items) + "[]",
            { Type: "string", Format: "binary" } => "Stream",
            { Type: "string" } => "string",
            { Type: "boolean" } => "bool",
            { Type: "number", Format: "float" } => "float",
            { Type: "number" } => "double",
            { Type: "integer", Format: "int64" } => "long",
            { Type: "integer" } => "int",
            { Reference: { Id: { } id } } => id.ToPascalCase(),
            { AdditionalProperties: { } ap } => $"Dictionary<string, {ToCLRType(ap)}>",
            _ => "object"
        };
    }
}
