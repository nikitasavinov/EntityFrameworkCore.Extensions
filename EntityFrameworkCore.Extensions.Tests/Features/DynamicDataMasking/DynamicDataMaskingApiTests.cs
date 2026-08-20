using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EntityFrameworkCore.Extensions.Tests;

public sealed class DynamicDataMaskingApiTests
{
    [Fact]
    public void DefaultMaskingFunctionGeneratesExpectedExpression()
        => Assert.Equal("default()", MaskingFunctions.Default());

    [Fact]
    public void EmailMaskingFunctionGeneratesExpectedExpression()
        => Assert.Equal("email()", MaskingFunctions.Email());

    [Fact]
    public void ParameterizedMaskingFunctionsGenerateExpectedExpressions()
    {
        Assert.Equal("random(10, 100)", MaskingFunctions.Random(10, 100));
        Assert.Equal("partial(2, \"XX-XX\", 1)", MaskingFunctions.Partial(2, "XX-XX", 1));
    }

    [Fact]
    public void PartialMaskingFunctionRejectsDoubleQuoteInPadding()
    {
        var exception = Assert.Throws<ArgumentException>(() => MaskingFunctions.Partial(1, "a\"b", 1));

        Assert.Equal("padding", exception.ParamName);
    }

    [Fact]
    public void HasDataMaskStoresAnnotationAndReturnsPropertyBuilder()
    {
        var modelBuilder = new ModelBuilder();
        var propertyBuilder = modelBuilder.Entity<SecretEntity>().Property(entity => entity.Secret);

        var result = propertyBuilder.HasDataMask(MaskingFunctions.Email());

        Assert.Same(propertyBuilder, result);
        Assert.Equal(
            MaskingFunctions.Email(),
            propertyBuilder.Metadata.FindAnnotation(AnnotationConstants.DynamicDataMasking)?.Value);
    }

    [Fact]
    public void HasDataMaskRejectsEmptyPattern()
    {
        var modelBuilder = new ModelBuilder();
        var propertyBuilder = modelBuilder.Entity<SecretEntity>().Property(entity => entity.Secret);

        Assert.Throws<ArgumentException>(() => propertyBuilder.HasDataMask(" "));
    }

    private sealed class SecretEntity
    {
        public int Id { get; set; }
        public string Secret { get; set; } = string.Empty;
    }
}
