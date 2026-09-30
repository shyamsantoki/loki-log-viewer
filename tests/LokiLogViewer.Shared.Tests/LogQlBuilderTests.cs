using LokiLogViewer.Shared.Querying;

namespace LokiLogViewer.Shared.Tests;

public class LogQlBuilderTests
{
    private static ConditionNode Cond(string label, LabelOperator op, string value, bool negate = false) =>
        new(label, op, value) { Negate = negate };

    [Fact]
    public void SingleCondition_ProducesOneSelector()
    {
        var root = Cond("namespace", LabelOperator.Equals, "biapks-bnpiks-pd1");

        var selectors = LogQlBuilder.BuildSelectors(root);

        Assert.Equal(["{namespace=\"biapks-bnpiks-pd1\"}"], selectors);
    }

    [Fact]
    public void AndGroup_ProducesOneSelectorWithAllMatchers()
    {
        var root = new GroupNode(GroupOperator.And,
        [
            Cond("namespace", LabelOperator.Equals, "biapks-bnpiks-pd1"),
            Cond("container", LabelOperator.Equals, "boatvoltageapi")
        ]);

        var selectors = LogQlBuilder.BuildSelectors(root);

        Assert.Equal(["{namespace=\"biapks-bnpiks-pd1\", container=\"boatvoltageapi\"}"], selectors);
    }

    [Fact]
    public void OrGroup_ProducesOneSelectorPerBranch()
    {
        var root = new GroupNode(GroupOperator.Or,
        [
            Cond("namespace", LabelOperator.Equals, "a"),
            Cond("namespace", LabelOperator.Equals, "b")
        ]);

        var selectors = LogQlBuilder.BuildSelectors(root);

        Assert.Equal(["{namespace=\"a\"}", "{namespace=\"b\"}"], selectors);
    }

    [Fact]
    public void NegatedCondition_FlipsOperator()
    {
        var root = Cond("namespace", LabelOperator.Equals, "a", negate: true);

        var selectors = LogQlBuilder.BuildSelectors(root);

        Assert.Equal(["{namespace!=\"a\"}"], selectors);
    }

    [Fact]
    public void NegatedAndGroup_DeMorgansIntoOrOfNegatedChildren()
    {
        var root = new GroupNode(GroupOperator.And,
        [
            Cond("a", LabelOperator.Equals, "1"),
            Cond("b", LabelOperator.Equals, "2")
        ])
        { Negate = true };

        var selectors = LogQlBuilder.BuildSelectors(root);

        Assert.Equal(["{a!=\"1\"}", "{b!=\"2\"}"], selectors);
    }

    [Fact]
    public void NegatedOrGroup_DeMorgansIntoAndOfNegatedChildren()
    {
        var root = new GroupNode(GroupOperator.Or,
        [
            Cond("a", LabelOperator.Equals, "1"),
            Cond("b", LabelOperator.Equals, "2")
        ])
        { Negate = true };

        var selectors = LogQlBuilder.BuildSelectors(root);

        Assert.Equal(["{a!=\"1\", b!=\"2\"}"], selectors);
    }

    [Fact]
    public void NestedAndOfOr_DistributesIntoTwoAndClauses()
    {
        // a AND (b OR c)  =>  (a AND b) OR (a AND c)
        var root = new GroupNode(GroupOperator.And,
        [
            Cond("a", LabelOperator.Equals, "1"),
            new GroupNode(GroupOperator.Or,
            [
                Cond("b", LabelOperator.Equals, "2"),
                Cond("c", LabelOperator.Equals, "3")
            ])
        ]);

        var selectors = LogQlBuilder.BuildSelectors(root);

        Assert.Equal(
        [
            "{a=\"1\", b=\"2\"}",
            "{a=\"1\", c=\"3\"}"
        ], selectors);
    }

    [Fact]
    public void DoubleNegatedNestedGroup_CancelsOut()
    {
        // NOT(NOT(a AND b)) == a AND b
        var inner = new GroupNode(GroupOperator.And,
        [
            Cond("a", LabelOperator.Equals, "1"),
            Cond("b", LabelOperator.Equals, "2")
        ])
        { Negate = true };

        var root = new GroupNode(GroupOperator.Or, [inner]) { Negate = true };

        var selectors = LogQlBuilder.BuildSelectors(root);

        Assert.Equal(["{a=\"1\", b=\"2\"}"], selectors);
    }

    [Fact]
    public void DuplicateBranches_AreDeduplicated()
    {
        var duplicateCondition = Cond("a", LabelOperator.Equals, "1");
        var root = new GroupNode(GroupOperator.Or, [duplicateCondition, duplicateCondition]);

        var selectors = LogQlBuilder.BuildSelectors(root);

        Assert.Equal(["{a=\"1\"}"], selectors);
    }

    [Fact]
    public void ValueWithQuotesAndBackslashes_IsEscaped()
    {
        var root = Cond("line", LabelOperator.Equals, "back\\slash and \"quote\"");

        var selectors = LogQlBuilder.BuildSelectors(root);

        Assert.Equal(["{line=\"back\\\\slash and \\\"quote\\\"\"}"], selectors);
    }

    [Fact]
    public void InvalidLabelName_Throws()
    {
        var root = Cond("not-a-valid-label", LabelOperator.Equals, "x");

        Assert.Throws<ArgumentException>(() => LogQlBuilder.BuildSelectors(root));
    }

    [Fact]
    public void ExceedingMaxBranches_ThrowsQueryTooComplex()
    {
        var children = Enumerable.Range(0, 10)
            .Select(i => (QueryNode)Cond("a", LabelOperator.Equals, i.ToString()))
            .ToList();
        var root = new GroupNode(GroupOperator.Or, children);

        Assert.Throws<QueryTooComplexException>(() => LogQlBuilder.BuildSelectors(root, maxBranches: 5));
    }

    [Fact]
    public void RegexOperators_RenderCorrectTokens()
    {
        var root = new GroupNode(GroupOperator.And,
        [
            Cond("container", LabelOperator.Matches, "a|b"),
            Cond("namespace", LabelOperator.NotMatches, "test.*")
        ]);

        var selectors = LogQlBuilder.BuildSelectors(root);

        Assert.Equal(["{container=~\"a|b\", namespace!~\"test.*\"}"], selectors);
    }
}
