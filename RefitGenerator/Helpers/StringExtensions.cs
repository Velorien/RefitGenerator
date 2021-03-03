using System.Linq;
using System.Text.RegularExpressions;

namespace RefitGenerator.Helpers
{
    static class StringExtensions
    {
        private static readonly Regex splitRegex = new ("\\W|_");

        public static string ToPascalCase(this string input) => 
            string.Join("", splitRegex.Split(input)
                                      .Select(x => x.Trim())
                                      .Where(x => !string.IsNullOrWhiteSpace(x))
                                      .Select(Capitalize));

        public static string Capitalize(this string input) => input[0].ToString().ToUpper() + input[1..];

        public static string ToCamelCase(this string input)
        {
            var segments = splitRegex.Split(input).Select(x => x.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
            return string.Join("", new string[] { segments[0] }.Concat(segments[1..].Select(Capitalize)));
        }
    }
}
