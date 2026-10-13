namespace InternalsViewer.Query.CallStack.Categories;

internal sealed class RuleIndex
{
    private static readonly int[] None = [];

    public RuleIndex(IReadOnlyList<(GlobPattern Class, GlobPattern Function)> rules)
    {
        var unindexed = new List<int>();

        for (var index = 0; index < rules.Count; index++)
        {
            var (classPattern, functionPattern) = rules[index];

            if (IsIndexable(classPattern))
            {
                Classes.Add(classPattern.Literal, index, classPattern.Shape == GlobShape.Exact);
            }
            else if (IsIndexable(functionPattern))
            {
                Functions.Add(functionPattern.Literal, index, functionPattern.Shape == GlobShape.Exact);
            }
            else
            {
                unindexed.Add(index);
            }
        }

        Unindexed = [.. unindexed];
    }

    private Node Classes { get; } = new();

    private Node Functions { get; } = new();

    private int[] Unindexed { get; }

    public Enumerator Candidates(ReadOnlySpan<char> className, ReadOnlySpan<char> methodName) => new(this, className, methodName);

    private static bool IsIndexable(GlobPattern pattern) => pattern.Shape is GlobShape.Exact or GlobShape.Prefix;

    public ref struct Enumerator
    {
        private const int ClassStage = 0;

        private const int UnindexedStage = 2;

        internal Enumerator(RuleIndex rules, ReadOnlySpan<char> className, ReadOnlySpan<char> methodName)
        {
            Rules = rules;
            ClassName = className;
            MethodName = methodName;
            Node = rules.Classes;
            Bucket = None;
            Position = -1;
        }

        public readonly int Current => Bucket[Position];

        private RuleIndex Rules { get; }

        private ReadOnlySpan<char> ClassName { get; }

        private ReadOnlySpan<char> MethodName { get; }

        private Node Node { get; set; }

        private int Depth { get; set; }

        private int[] Bucket { get; set; }

        private int Position { get; set; }

        private int Stage { get; set; }

        public readonly Enumerator GetEnumerator() => this;

        public bool MoveNext()
        {
            Position++;

            while (Position >= Bucket.Length)
            {
                if (!NextBucket())
                {
                    return false;
                }

                Position = 0;
            }

            return true;
        }

        private bool NextBucket()
        {
            while (Stage < UnindexedStage)
            {
                var value = Stage == ClassStage ? ClassName : MethodName;

                if (Depth == value.Length || Node.Child(value[Depth]) is not { } child)
                {
                    Stage++;

                    Node = Rules.Functions;

                    Depth = 0;

                    continue;
                }

                Node = child;

                Depth++;

                var bucket = Depth == value.Length ? child.Final : child.Prefixed;

                if (bucket.Length > 0)
                {
                    Bucket = bucket;

                    return true;
                }
            }

            if (Stage == UnindexedStage)
            {
                Stage++;

                Bucket = Rules.Unindexed;

                return true;
            }

            return false;
        }
    }

    private sealed class Node
    {
        public int[] Prefixed { get; private set; } = None;

        public int[] Final { get; private set; } = None;

        private char[] Keys { get; set; } = [];

        private Node[] Children { get; set; } = [];

        public Node? Child(char value)
        {
            var index = Keys.AsSpan().IndexOf(char.ToUpperInvariant(value));

            return index >= 0 ? Children[index] : null;
        }

        public void Add(ReadOnlySpan<char> literal, int rule, bool exact)
        {
            if (literal.IsEmpty)
            {
                if (!exact)
                {
                    Prefixed = [.. Prefixed, rule];
                }

                Final = [.. Final, rule];

                return;
            }

            var key = char.ToUpperInvariant(literal[0]);

            var index = Keys.AsSpan().IndexOf(key);

            if (index < 0)
            {
                Keys = [.. Keys, key];

                Children = [.. Children, new Node()];

                index = Keys.Length - 1;
            }

            Children[index].Add(literal[1..], rule, exact);
        }
    }
}
