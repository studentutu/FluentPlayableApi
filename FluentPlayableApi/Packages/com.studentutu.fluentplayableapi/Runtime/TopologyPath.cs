#nullable enable

using System;
using System.Collections.Generic;

namespace FluentPlayableApi
{
    /// <summary>
    /// Normalizes fluent graph lookup paths.
    /// </summary>
    public static class TopologyPath
    {
        /// <summary>
        /// Range: non-empty slash or backslash separated path. Condition: no current or parent segments. Output: canonical slash path.
        /// </summary>
        public static string Normalize(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Path cannot be null, empty, or whitespace.", nameof(path));
            }

            string[] rawSegments = path.Replace('\\', '/').Split('/');
            var segments = new List<string>(rawSegments.Length);

            for (int i = 0; i < rawSegments.Length; i++)
            {
                string segment = rawSegments[i].Trim();
                if (segment.Length == 0)
                {
                    continue;
                }

                if (segment == "." || segment == "..")
                {
                    throw new ArgumentException($"Path '{path}' contains invalid segment '{segment}'.", nameof(path));
                }

                segments.Add(segment);
            }

            if (segments.Count == 0)
            {
                throw new ArgumentException("Path must contain at least one segment.", nameof(path));
            }

            return string.Join("/", segments);
        }

        /// <summary>
        /// Range: non-empty parent and child paths. Condition: each side is normalized independently. Output: joined canonical path.
        /// </summary>
        public static string Join(string parent, string child)
        {
            return Normalize(Normalize(parent) + "/" + Normalize(child));
        }

        /// <summary>
        /// Range: non-empty path. Condition: path can contain separators. Output: last canonical segment.
        /// </summary>
        public static string NameOf(string path)
        {
            string normalized = Normalize(path);
            int lastSlash = normalized.LastIndexOf("/", StringComparison.Ordinal);
            return lastSlash < 0 ? normalized : normalized.Substring(lastSlash + 1);
        }

        /// <summary>
        /// Range: lookup key. Condition: key may use slash or backslash. Output: true when it targets an exact path.
        /// </summary>
        public static bool IsPath(string key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            return key.IndexOf('/') >= 0 || key.IndexOf('\\') >= 0;
        }
    }
}
