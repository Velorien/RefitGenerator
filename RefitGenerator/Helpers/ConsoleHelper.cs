using System;

namespace RefitGenerator.Helpers
{
    static class ConsoleHelper
    {
        public static void WriteLineColored(string content, ConsoleColor foreground)
        {
            var currentForeground = Console.ForegroundColor;
            Console.ForegroundColor = foreground;
            Console.WriteLine(content);
            Console.ForegroundColor = currentForeground;
        }
    }
}
