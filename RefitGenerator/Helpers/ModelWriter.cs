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
    static class ModelWriter
    {
        public static void WriteModel(GeneratorOptions options, string className, IDictionary<string, OpenApiSchema> properties)
        {
            if (!properties.Any()) return;

            string classCode = GetModelClass(options, className, properties);
            File.WriteAllText(Path.Combine(options.OutputDirectory.FullName, ModelsDirectory, className + ".cs"), classCode);
        }

        static string GetModelClass(GeneratorOptions options, string className, IDictionary<string, OpenApiSchema> properties)
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
                sb.AppendFormat(Indent2 + ModelPropFormat, ToCLRType(options, className, propertyName.ToPascalCase(), propertySchema), propertyName.ToPascalCase());
                sb.AppendLine();
            }

            return string.Format(modelTemplate, options.ProjectName, className, sb.ToString());
        }
    }
}
