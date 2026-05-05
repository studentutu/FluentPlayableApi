#nullable enable

namespace Fluentplayableapi
{
    /// <summary>
    /// Tracks one fluent-declared input until it is consumed.
    /// </summary>
    internal sealed class DeclaredInput
    {
        public bool Consumed { get; set; }
    }
}