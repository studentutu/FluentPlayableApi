#nullable enable

using UnityEngine.Playables;

namespace FluentPlayableApi
{
    /// <summary>
    /// Tracks the destination waiting for the next authored playable.
    /// </summary>
    internal abstract class PendingInput
    {
        protected PendingInput(DeclaredInput declaredInput)
        {
            DeclaredInput = declaredInput;
        }

        public DeclaredInput DeclaredInput { get; }
        public abstract bool IsOutput { get; }
        public abstract int SourceOutputPort { get; }
        public abstract PlayableHandle DestinationHandle { get; }
        public abstract int DestinationInput { get; }

        public static PendingInput ForOutput<TOutput>(TOutput output, int sourceOutputPort, DeclaredInput declaredInput)
            where TOutput : struct, IPlayableOutput
        {
            return new OutputPendingInput<TOutput>(output, sourceOutputPort, declaredInput);
        }

        public static PendingInput ForPlayable<TDestination>(TDestination destination, int destinationInput, DeclaredInput declaredInput)
            where TDestination : struct, IPlayable
        {
            return new PlayablePendingInput<TDestination>(destination, destinationInput, declaredInput);
        }

        public abstract bool Attach<TSource>(PlayableGraph graph, TSource source, int sourceOutputPort)
            where TSource : struct, IPlayable;

        private sealed class OutputPendingInput<TOutput> : PendingInput
            where TOutput : struct, IPlayableOutput
        {
            private readonly TOutput _output;

            public OutputPendingInput(TOutput output, int sourceOutputPort, DeclaredInput declaredInput)
                : base(declaredInput)
            {
                _output = output;
                SourceOutputPort = sourceOutputPort;
            }

            public override bool IsOutput => true;
            public override int SourceOutputPort { get; }
            public override PlayableHandle DestinationHandle => default;
            public override int DestinationInput => -1;

            public override bool Attach<TSource>(PlayableGraph graph, TSource source, int sourceOutputPort)
            {
                _output.SetSourcePlayable(source, sourceOutputPort);
                return true;
            }
        }

        private sealed class PlayablePendingInput<TDestination> : PendingInput
            where TDestination : struct, IPlayable
        {
            private readonly TDestination _destination;

            public PlayablePendingInput(TDestination destination, int destinationInput, DeclaredInput declaredInput)
                : base(declaredInput)
            {
                _destination = destination;
                DestinationInput = destinationInput;
            }

            public override bool IsOutput => false;
            public override int SourceOutputPort => -1;
            public override PlayableHandle DestinationHandle => _destination.GetHandle();
            public override int DestinationInput { get; }

            public override bool Attach<TSource>(PlayableGraph graph, TSource source, int sourceOutputPort)
            {
                return graph.Connect(source, sourceOutputPort, _destination, DestinationInput);
            }
        }
    }
}
