using Microsoft.OpenApi.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RefitGenerator.Helpers
{
    static class TypeHelper
    {
        public static string ToCLRSimpleType(GeneratorOptions options, OpenApiSchema property) =>
            property switch
            {
                { Type: "array" } => ToCLRSimpleType(options, property.Items) + "[]",
                { Type: "string", Format: "binary" } => "Stream",
                { Type: "string", Format: "date" or "date-time" } => "DateTime" + Nullable(property),
                { Type: "string" } => "string",
                { Type: "boolean" } => "bool" + Nullable(property),
                { Type: "number", Format: "float" } => "float" + Nullable(property),
                { Type: "number" } => "double" + Nullable(property),
                { Type: "integer", Format: "int64" } => "long" + Nullable(property),
                { Type: "integer" } => "int" + Nullable(property),
                _ => "object"
            };

        public static string ToCLRType(GeneratorOptions options, string enclosingType, string propertyName, OpenApiSchema property) =>
            property switch
            {
                { Type: "array" } => ToCLRType(options, enclosingType, propertyName, property.Items) + "[]",
                { Items: { } } => ToCLRType(options, enclosingType, propertyName, property.Items) + "[]",
                { Type: "string", Format: "binary" } => "Stream",
                { Type: "string", Format: "date" or "date-time" } => "DateTime" + Nullable(property),
                { Type: "string" } => "string",
                { Type: "boolean" } => "bool" + Nullable(property),
                { Type: "number", Format: "float" } => "float" + Nullable(property),
                { Type: "number" } => "double" + Nullable(property),
                { Type: "integer", Format: "int64" } => "long" + Nullable(property),
                { Type: "integer" } => "int" + Nullable(property),
                { AdditionalProperties: { } ap } => $"Dictionary<string, {ToCLRType(options, enclosingType, propertyName, ap)}>",
                { Reference: { Id: { } id } } => options.SimpleTypeMap.ContainsKey(id) ? options.SimpleTypeMap[id] : id.ToPascalCase(),
                { } => GetCompoundType(options, $"{enclosingType}_{propertyName}", property),
                _ => "object"
            };

        public static string GetCompoundType(GeneratorOptions options, string typeName, OpenApiSchema schema)
        {
            var allProperties = new List<OpenApiSchema>();
            ScanSchemaProperties(schema, allProperties);
            var properties = allProperties
                .SelectMany(x => x.Properties)
                .Concat(schema.Properties)
                .GroupBy(x => x.Key)
                .ToDictionary(k => k.Key, v => v.First().Value);

            if (!properties.Any()) return "object";

            ModelWriter.WriteModel(options, typeName, properties);
            return typeName;
        }

        private static void ScanSchemaProperties(OpenApiSchema schema, List<OpenApiSchema> all)
        {
            if (schema == null) return;

            all.AddRange(schema.AllOf);
            all.AddRange(schema.AnyOf);
            // todo: OneOf ???

            foreach (var allOf in schema.AllOf) ScanSchemaProperties(allOf, all);
            foreach (var anyOf in schema.AnyOf) ScanSchemaProperties(anyOf, all);
        }

        private static string Nullable(OpenApiSchema property) => property.Nullable ? "?" : string.Empty;
    }
}
