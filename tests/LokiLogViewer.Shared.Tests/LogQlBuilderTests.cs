using LokiLogViewer.Shared.Querying;
using LokiLogViewer.Shared.Search;

namespace LokiLogViewer.Shared.Tests;

public class LogQlBuilderTests
{
    [Fact]
    public void SingleCondition_ProducesOneMatcherSelector()
    {
        var selector = LogQlBuilder.BuildSelector([new LogCondition("namespace", "biapks-bnpiks-pd1")]);

        Assert.Equal("{namespace=\"biapks-bnpiks-pd1\"}", selector);
    }

    [Fact]
    public void MultipleConditions_AreJoinedAsAnAndList()
    {
        var selector = LogQlBuilder.BuildSelector(
        [
            new LogCondition("namespace", "biapks-bnpiks-pd1"),
            new LogCondition("container", "boatvoltageapi")
        ]);

        Assert.Equal("{namespace=\"biapks-bnpiks-pd1\", container=\"boatvoltageapi\"}", selector);
    }

    [Fact]
    public void ValueWithQuotesAndBackslashes_IsEscaped()
    {
        var selector = LogQlBuilder.BuildSelector([new LogCondition("line", "back\\slash and \"quote\"")]);

        Assert.Equal("{line=\"back\\\\slash and \\\"quote\\\"\"}", selector);
    }

    [Fact]
    public void InvalidLabelName_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            LogQlBuilder.BuildSelector([new LogCondition("not-a-valid-label", "x")]));
    }

    [Fact]
    public void NoConditions_Throws()
    {
        Assert.Throws<ArgumentException>(() => LogQlBuilder.BuildSelector([]));
    }
}
