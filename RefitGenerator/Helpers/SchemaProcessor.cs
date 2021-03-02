using Microsoft.OpenApi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RefitGenerator.Helpers
{
    static class SchemaProcessor
    {
        public static void ProcessSchemas(GeneratorOptions options, IDictionary<string, OpenApiSchema> schemas)
        {
            // process definitions for simple types
            var simpleTypes = schemas.Where(x => !x.Value.Properties.Any() && !x.Value.OneOf.Any() &&
                                                 !x.Value.AllOf.Any() && !x.Value.OneOf.Any() && x.Value.Type != null &&
                                                 (x.Value.Type != "array" || x.Value.Type == "array" && x.Value.Items.Type != "object"));

            foreach (var type in simpleTypes)
            {
                options.SimpleTypeMap.Add(type.Key, TypeHelper.ToCLRSimpleType(options, type.Value));
            }


            var compoundKeys = schemas.Keys.Except(simpleTypes.Select(x => x.Key));
            foreach (var key in compoundKeys)
            {
                 TypeHelper.GetCompoundType(options, key.ToPascalCase(), schemas[key]);
            }
        }
    }
}
