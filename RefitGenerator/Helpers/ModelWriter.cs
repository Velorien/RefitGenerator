using Microsoft.OpenApi.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using static RefitGenerator.Helpers.Strings;
using static RefitGenerator.Helpers.TypeHelper;

namespace RefitGenerator.Helpers
{
    static class ModelWriter
    {
        private static readonly Regex propertyRegex = new Regex("[^a-zA-Z].*");

        public static void WriteModel(GeneratorOptions options, string className, IDictionary<string, OpenApiSchema> properties)
        {
            string classCode = GetModelClass(options, className, properties);
            File.WriteAllText(Path.Combine(options.OutputDirectory.FullName, ModelsDirectory, className + ".cs"), classCode);
        }

        static string GetModelClass(GeneratorOptions options, string className, IDictionary<string, OpenApiSchema> properties)
        {
            var sb = new StringBuilder();
            string modelTemplate = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, TemplatesDirectory, "ModelTemplate.csx"));

            foreach (var property in properties)
            {
                string originalName = property.Key;
                var propertySchema = property.Value;
                string propertyName = originalName.ToPascalCase();
                if (propertyRegex.IsMatch(propertyName))
                    propertyName = "Property_" + propertyName;

                // so that conflict-resolving affixes do not pollute the class name
                string propertyAsTypeName = propertyName;

                if (propertyName == className)
                {
                    if (options.PrefixConflictingName)
                        propertyName = options.ConflictingNameAffix + propertyName;
                    else
                        propertyName += options.ConflictingNameAffix;
                }

                sb.AppendLine();
                sb.AppendFormat(Indent2 + JpnFormat, originalName);
                sb.AppendLine();
                sb.AppendFormat(Indent2 + ModelPropFormat, ToCLRType(options, className, propertyAsTypeName, propertySchema), propertyName);
                sb.AppendLine();
            }

            return string.Format(modelTemplate, options.ProjectName, className, sb.ToString());
        }
    }
}
