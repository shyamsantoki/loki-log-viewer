using System.Text.RegularExpressions;

namespace LokiLogViewer.Shared.Querying;

// Loki stream selectors ("{label="value", ...}") only support AND between matchers - there is
// no native OR/NOT across labels. To support a fully nested AND/OR/NOT tree, we:
//   1. Push NOT down to the leaves (De Morgan), so negation ends up baked into each leaf's operator.
//   2. Convert the negation-free tree to disjunctive normal form: an OR of AND-only clauses.
//   3. Render each AND-clause as one LogQL selector - one Loki query per clause.
public static partial class LogQlBuilder
{
    [GeneratedRegex("^[a-zA-Z_][a-zA-Z0-9_]*$")]
    private static partial Regex LabelNamePattern();

    public static IReadOnlyList<string> BuildSelectors(QueryNode root, int maxBranches = 25)
    {
        var normalized = PushDownNegation(root, negate: false);
        var branches = ToDnf(normalized, maxBranches);

        var selectors = new List<string>();
        var seen = new HashSet<string>();
        foreach (var branch in branches)
        {
            var selector = ConjunctionToSelector(branch);
            if (seen.Add(selector))
            {
                selectors.Add(selector);
            }
        }

        return selectors;
    }

    private static QueryNode PushDownNegation(QueryNode node, bool negate)
    {
        var effectiveNegate = node.Negate ^ negate;

        return node switch
        {
            ConditionNode condition => condition with
            {
                Operator = effectiveNegate ? NegateOperator(condition.Operator) : condition.Operator,
                Negate = false
            },
            GroupNode group => group with
            {
                Operator = effectiveNegate ? NegateGroupOperator(group.Operator) : group.Operator,
                Children = group.Children.Select(c => PushDownNegation(c, effectiveNegate)).ToList(),
                Negate = false
            },
            _ => throw new NotSupportedException($"Unsupported node type: {node.GetType()}")
        };
    }

    private static LabelOperator NegateOperator(LabelOperator op) => op switch
    {
        LabelOperator.Equals => LabelOperator.NotEquals,
        LabelOperator.NotEquals => LabelOperator.Equals,
        LabelOperator.Matches => LabelOperator.NotMatches,
        LabelOperator.NotMatches => LabelOperator.Matches,
        _ => throw new ArgumentOutOfRangeException(nameof(op))
    };

    private static GroupOperator NegateGroupOperator(GroupOperator op) => op switch
    {
        GroupOperator.And => GroupOperator.Or,
        GroupOperator.Or => GroupOperator.And,
        _ => throw new ArgumentOutOfRangeException(nameof(op))
    };

    // Assumes `node` already has all negation pushed down (PushDownNegation has been applied).
    private static List<List<ConditionNode>> ToDnf(QueryNode node, int maxBranches)
    {
        switch (node)
        {
            case ConditionNode condition:
                return [[condition]];

            case GroupNode { Operator: GroupOperator.Or } group:
            {
                var result = new List<List<ConditionNode>>();
                foreach (var child in group.Children)
                {
                    result.AddRange(ToDnf(child, maxBranches));
                    if (result.Count > maxBranches)
                    {
                        throw new QueryTooComplexException(maxBranches);
                    }
                }
                return result;
            }

            case GroupNode { Operator: GroupOperator.And } group:
            {
                List<List<ConditionNode>> result = [[]];
                foreach (var child in group.Children)
                {
                    var childDnf = ToDnf(child, maxBranches);
                    var next = new List<List<ConditionNode>>();
                    foreach (var existing in result)
                    {
                        foreach (var addition in childDnf)
                        {
                            next.Add([..existing, ..addition]);
                            if (next.Count > maxBranches)
                            {
                                throw new QueryTooComplexException(maxBranches);
                            }
                        }
                    }
                    result = next;
                }
                return result;
            }

            default:
                throw new NotSupportedException($"Unsupported node type: {node.GetType()}");
        }
    }

    private static string ConjunctionToSelector(IReadOnlyList<ConditionNode> conjunction)
    {
        if (conjunction.Count == 0)
        {
            throw new ArgumentException(
                "Each condition group must resolve to at least one label matcher - Loki requires a non-empty selector.");
        }

        var matchers = conjunction.Select(c =>
        {
            if (!LabelNamePattern().IsMatch(c.Label))
            {
                throw new ArgumentException($"'{c.Label}' is not a valid Loki label name.");
            }

            return $"{c.Label}{OperatorToken(c.Operator)}\"{EscapeValue(c.Value)}\"";
        });

        return "{" + string.Join(", ", matchers) + "}";
    }

    private static string OperatorToken(LabelOperator op) => op switch
    {
        LabelOperator.Equals => "=",
        LabelOperator.NotEquals => "!=",
        LabelOperator.Matches => "=~",
        LabelOperator.NotMatches => "!~",
        _ => throw new ArgumentOutOfRangeException(nameof(op))
    };

    private static string EscapeValue(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
