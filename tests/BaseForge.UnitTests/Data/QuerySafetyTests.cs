using BaseForge.Core.CQRS;
using BaseForge.Core.Exceptions;

namespace BaseForge.UnitTests.Data;

/// <summary>SortBy beyaz listesi ve LIKE kaçışlama (bkz. docs/ARCH.md §6.2).</summary>
public class QuerySafetyTests
{
    private static readonly string[] Allowed = ["Name", "CreatedAt"];

    [Theory]
    [InlineData("name", "Name")]
    [InlineData("Name desc", "Name desc")]
    [InlineData(" createdat ASC , name DESC ", "CreatedAt, Name desc")]
    public void Normalize_ValidInput_ReturnsCanonical(string input, string expected)
        => Assert.Equal(expected, SortExpression.Normalize(input, Allowed));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_Empty_ReturnsNull(string? input)
        => Assert.Null(SortExpression.Normalize(input, Allowed));

    [Theory]
    [InlineData("TenantId")]
    [InlineData("Name.Length")]
    [InlineData("np(Name) desc")]
    [InlineData("Name descending")]
    [InlineData("Name desc; drop")]
    public void Normalize_UnsafeOrUnknown_Throws(string input)
        => Assert.Throws<ValidationException>(() => SortExpression.Normalize(input, Allowed));

    [Fact]
    public void Normalize_TooManyParts_Throws()
        => Assert.Throws<ValidationException>(() =>
            SortExpression.Normalize("Name, Name, Name, Name, Name, Name", Allowed));

    [Theory]
    [InlineData("50%", "%50\\%%")]
    [InlineData("a_b", "%a\\_b%")]
    [InlineData("c:\\x", "%c:\\\\x%")]
    [InlineData("  plain  ", "%plain%")]
    public void ContainsOrNull_EscapesWildcards(string input, string expected)
        => Assert.Equal(expected, LikePattern.ContainsOrNull(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" a ")]
    public void ContainsOrNull_TooShort_ReturnsNull(string? input)
        => Assert.Null(LikePattern.ContainsOrNull(input));
}
