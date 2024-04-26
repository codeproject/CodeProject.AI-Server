using System.Text.RegularExpressions;

namespace CodeProject.AI.SDK.Utils
{
    /// <summary>
    /// String extensions
    /// </summary>
    public static class StringExtensions
    {
        /// <summary>
        /// Tests whether a string is equal to another string, case-insensitively
        /// </summary>
        /// <param name="source">This string</param>
        /// <param name="str">The string to test</param>
        /// <returns>True if they are the same (including null == null), false otherwise</returns>
        public static bool EqualsIgnoreCase(this string? source, string? str)
        {
            if (source is null)
                return str is null;

            if (str is null)
                return false;

            return source.Equals(str, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Tests whether a string contains another string, case-insensitively
        /// </summary>
        /// <param name="source">This string</param>
        /// <param name="str">The string to test</param>
        /// <returns>True if the string contains the given string, false otherwise</returns>
        public static bool ContainsIgnoreCase(this string? source, string? str)
        {
            if (source is null)
                return str is null;

            if (str is null)
                return false;

            return source.Contains(str, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Tests whether a string starts with another string, case-insensitively
        /// </summary>
        /// <param name="source">This string</param>
        /// <param name="str">The string to test</param>
        /// <returns>True if source starts with str (including null == null), false otherwise</returns>
        public static bool StartsWithIgnoreCase(this string? source, string? str)
        {
            if (source is null)
                return str is null;

            if (str is null)
                return false;

            return source.StartsWith(str, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// A utility class for text operations
    /// </summary>
    public class Text
    {
        /// <summary>
        /// Removes XTerm colouring from a string.
        /// </summary>
        /// <param name="text">The string</param>
        /// <returns>A string</returns>
        public static string StripXTermColors(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            // Strip the foreground <ESC>[30m - <ESC>[37m, <ESC>[90m - <ESC>[97m
            text = Regex.Replace(text, "\\u001b\\[((0|1);)?(3|9)\\dm", string.Empty, RegexOptions.Compiled);

            // Strip the background. <ESC>[40m - <ESC>[47m, <ESC>[100m - <ESC>[107m
            text = Regex.Replace(text, "\\u001b\\[(4|10)\\dm", string.Empty, RegexOptions.Compiled);

            // Strip the reset code. <ESC>[0m
            text = Regex.Replace(text, "\u001b\\[0m", string.Empty, RegexOptions.Compiled);

            return text;
        }

        /// <summary>
        /// Removes the spinner animation characters from a string.
        /// </summary>
        /// <param name="text">The string</param>
        /// <returns>A string</returns>
        public static string StripSpinnerChars(string text)
        {
            // Strip the 'spin' animation. |,/,-,\ + backspace
            // text = Regex.Replace(text, "([-\\\\\\|\\/])?[\\b]", string.Empty, RegexOptions.Compiled);
            return Regex.Replace(text, "([\\-\\\\\\|\\/])?", string.Empty, RegexOptions.Compiled);
        }

        /// <summary>
        /// Corrects the direction of the slashes in a directory path so it's correct for the 
        /// current OS.
        /// </summary>
        /// <param name="path">The path</param>
        /// <returns>A string</returns>
        public static string FixSlashes(string? path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            return Regex.Replace(path, "(\\\\|/)", Path.DirectorySeparatorChar.ToString(), RegexOptions.Compiled);
        }

        /// <summary>
        /// Shrinks a string representing a path so it fits within the given length
        /// </summary>
        /// <param name="path">The path</param>
        /// <param name="maxLength">The max length of the resultant string</param>
        /// <returns>A string</returns>
        public static string? ShrinkPath(string? path, int maxLength)
        {
            if (path is null || path.Length <= maxLength)
                return path;

            var parts = new List<string>(path.Split(new char[] { '\\', '/' }));
            if (parts.Count == 0)
                return path;

            // Always have the first and last part
            string start = parts[0];
            parts.RemoveAt(0);

            string end = string.Empty;
            if (parts.Count > 0)
            {
                end = parts[^1];
                parts.RemoveAt(parts.Count - 1);
            }

            // Try and add one more start section before we finish off with the ends
            if (parts.Count > 0)
            {
                start = start + Path.DirectorySeparatorChar + parts[0];
                parts.RemoveAt(0);
            }

            // Add end sections 1 by 1 while we can
            while (start.Length + 3 + end.Length < maxLength && parts.Count > 0)
            {
                end = parts[^1] + Path.DirectorySeparatorChar + end;
                parts.RemoveAt(parts.Count - 1);
            }

            // If we overshot then trim it back down
            while (start.Length + 3 + end.Length > maxLength)
            {
                if (start.Length > end.Length)
                    start = start.Remove(start.Length - 1);
                else
                    end = end.Remove(0, 1);
            }

            return start + "..." + end;
        }
    }
}
