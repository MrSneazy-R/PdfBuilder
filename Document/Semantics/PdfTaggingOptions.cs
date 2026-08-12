using System.Collections.ObjectModel;

namespace PdfBuilder.Document;

/// <summary>Document-scoped tagged-PDF configuration. Enabling it does not claim PDF/UA conformance.</summary>
public sealed class PdfTaggingOptions
{
    private readonly Dictionary<string, string> _roleMap = new(StringComparer.Ordinal)
    {
        ["Header"] = "Sect",
        ["Footer"] = "Sect"
    };
    private readonly ReadOnlyDictionary<string, string> _readOnlyRoleMap;

    public PdfTaggingOptions() => _readOnlyRoleMap = new ReadOnlyDictionary<string, string>(_roleMap);

    /// <summary>Gets or sets whether marked content and a structure tree are emitted.</summary>
    public bool Enabled { get; set; }
    /// <summary>Gets the custom-to-standard role map.</summary>
    public IReadOnlyDictionary<string, string> RoleMap => _readOnlyRoleMap;

    internal void MapRole(string customRole, string standardRole)
    {
        ValidateRole(customRole, nameof(customRole));
        ValidateRole(standardRole, nameof(standardRole));
        _roleMap[customRole] = standardRole;
    }

    private static void ValidateRole(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Any(character => char.IsWhiteSpace(character) || character is '/' or '#' or '(' or ')' or '<' or '>' or '[' or ']' or '{' or '}' or '%'))
            throw new ArgumentException("A semantic role must be a non-empty PDF-name token.", parameterName);
    }
}

internal sealed class PdfSemanticDescriptor
{
    internal PdfSemanticRole Role { get; init; }
    internal string? AlternativeText { get; set; }
    internal int? ReadingOrder { get; set; }
    internal int NodeId { get; set; } = -1;
    internal int RegistryGeneration { get; set; } = -1;
}

internal sealed class PdfSemanticNode
{
    internal required int Id { get; init; }
    internal required int ParentId { get; init; }
    internal required PdfSemanticRole Role { get; init; }
    internal string? AlternativeText { get; init; }
    internal int? ReadingOrder { get; init; }
    internal int Sequence { get; init; }
    internal List<int> Children { get; } = new();
}

internal sealed class PdfSemanticRegistry
{
    private readonly List<PdfSemanticNode> _nodes = new();
    private readonly Stack<int> _stack = new();
    private int _sequence;

    internal int Generation { get; private set; }
    internal IReadOnlyList<PdfSemanticNode> Nodes => _nodes;
    internal int CurrentParentId => _stack.Count == 0 ? 0 : _stack.Peek();

    internal void ResetContent()
    {
        _nodes.Clear();
        _stack.Clear();
        _sequence = 0;
        Generation++;
    }

    internal PdfSemanticNode GetOrCreate(PdfSemanticDescriptor descriptor)
    {
        if (descriptor.RegistryGeneration == Generation && descriptor.NodeId > 0)
            return _nodes[descriptor.NodeId - 1];

        int id = _nodes.Count + 1;
        var node = new PdfSemanticNode
        {
            Id = id,
            ParentId = CurrentParentId,
            Role = descriptor.Role,
            AlternativeText = descriptor.AlternativeText,
            ReadingOrder = descriptor.ReadingOrder,
            Sequence = _sequence++
        };
        _nodes.Add(node);
        if (node.ParentId > 0)
            _nodes[node.ParentId - 1].Children.Add(node.Id);
        descriptor.NodeId = id;
        descriptor.RegistryGeneration = Generation;
        return node;
    }

    internal IDisposable Enter(int nodeId)
    {
        _stack.Push(nodeId);
        return new PopScope(_stack, nodeId);
    }

    private sealed class PopScope : IDisposable
    {
        private readonly Stack<int> _stack;
        private readonly int _nodeId;
        private bool _disposed;
        internal PopScope(Stack<int> stack, int nodeId) { _stack = stack; _nodeId = nodeId; }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_stack.Count == 0 || _stack.Pop() != _nodeId)
                throw new InvalidOperationException("Semantic composition scopes were disposed out of order.");
        }
    }
}
