using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RefitGenerator
{
    static class StringExtensions
    {
        public static string ToPascalCase(this string input) => string.Join("", input.Split('_').Select(Capitalize));

        public static string Capitalize(this string input) => input[0].ToString().ToUpper() + input[1..];

        public static string ToCamelCase(this string input)
        {
            var segments = input.Split('_');
            return string.Join("", new string[] { segments[0] }.Concat(segments[1..].Select(Capitalize)));
        }
    }
}
