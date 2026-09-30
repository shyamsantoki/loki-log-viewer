using System.Text.Json.Serialization;

namespace LokiLogViewer.Shared.Querying;

public enum LabelOperator
{
    Equals,
    NotEquals,
    Matches,
    NotMatches
}

public enum GroupOperator
{
    And,
    Or
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ConditionNode), "condition")]
[JsonDerivedType(typeof(GroupNode), "group")]
public abstract record QueryNode
{
    public bool Negate { get; init; }
}

public sealed record ConditionNode(string Label, LabelOperator Operator, string Value) : QueryNode;

public sealed record GroupNode(GroupOperator Operator, IReadOnlyList<QueryNode> Children) : QueryNode;
