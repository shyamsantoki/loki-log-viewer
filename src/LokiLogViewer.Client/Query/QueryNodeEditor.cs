using LokiLogViewer.Shared.Querying;

namespace LokiLogViewer.Client.Query;

// Mutable, two-way-bindable stand-ins for the immutable Shared.Querying tree, used only while
// the user is editing a query in the builder UI. Converted to the real QueryNode tree at Run time.
public abstract class QueryNodeEditor
{
    public bool Negate { get; set; }

    public abstract QueryNode ToQueryNode();
}

public sealed class ConditionEditor : QueryNodeEditor
{
    public string Label { get; set; } = "";
    public LabelOperator Operator { get; set; } = LabelOperator.Equals;
    public string Value { get; set; } = "";

    public bool IsComplete => !string.IsNullOrWhiteSpace(Label) && !string.IsNullOrWhiteSpace(Value);

    public override QueryNode ToQueryNode() => new ConditionNode(Label, Operator, Value) { Negate = Negate };
}

public sealed class GroupEditor : QueryNodeEditor
{
    public GroupOperator Operator { get; set; } = GroupOperator.And;
    public List<QueryNodeEditor> Children { get; } = [];

    public override QueryNode ToQueryNode() =>
        new GroupNode(Operator, Children.Select(c => c.ToQueryNode()).ToList()) { Negate = Negate };
}
