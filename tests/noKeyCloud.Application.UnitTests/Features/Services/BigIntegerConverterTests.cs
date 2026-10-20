using System.Numerics;
using System.Text.Json;
using noKeyCloud.Infrastructure.Services;

namespace noKeyCloud.Application.UnitTests.Features.Services;

public class BigIntegerConverterTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new BigIntegerConverter() }
    };

    [Fact]
    public void Write_ShouldWriteBigIntegerAsJsonString()
    {
        var value = BigInteger.Parse("123456789012345678901234567890");

        var json = JsonSerializer.Serialize(value, Options);

        Assert.Equal("\"123456789012345678901234567890\"", json);
    }

    [Fact]
    public void Write_ShouldWriteNegativeBigInteger()
    {
        var value = BigInteger.Parse("-98765432109876543210987654321");

        var json = JsonSerializer.Serialize(value, Options);

        Assert.Equal("\"-98765432109876543210987654321\"", json);
    }

    [Fact]
    public void Write_ShouldWriteZero()
    {
        var json = JsonSerializer.Serialize(BigInteger.Zero, Options);

        Assert.Equal("\"0\"", json);
    }

    [Fact]
    public void Write_ShouldWriteSingleDigit()
    {
        var json = JsonSerializer.Serialize(new BigInteger(7), Options);

        Assert.Equal("\"7\"", json);
    }

    [Fact]
    public void Read_FromJsonString_ShouldParseBigInteger()
    {
        var json = "\"123456789012345678901234567890\"";

        var result = JsonSerializer.Deserialize<BigInteger>(json, Options);

        Assert.Equal(BigInteger.Parse("123456789012345678901234567890"), result);
    }

    [Fact]
    public void Read_FromJsonString_NegativeValue()
    {
        var json = "\"-99999999999999999999\"";

        var result = JsonSerializer.Deserialize<BigInteger>(json, Options);

        Assert.Equal(BigInteger.Parse("-99999999999999999999"), result);
    }

    [Fact]
    public void Read_FromJsonNumber_ShouldParseBigInteger()
    {
        var json = "123456789";

        var result = JsonSerializer.Deserialize<BigInteger>(json, Options);

        Assert.Equal(new BigInteger(123456789), result);
    }

    [Fact]
    public void Read_FromJsonNumber_Negative()
    {
        var json = "-42";

        var result = JsonSerializer.Deserialize<BigInteger>(json, Options);

        Assert.Equal(new BigInteger(-42), result);
    }

    [Fact]
    public void Read_FromJsonNumber_Zero()
    {
        var json = "0";

        var result = JsonSerializer.Deserialize<BigInteger>(json, Options);

        Assert.Equal(BigInteger.Zero, result);
    }

    [Fact]
    public void Read_FromBooleanToken_ShouldThrowJsonException()
    {
        var json = "true";

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<BigInteger>(json, Options));
    }

    [Fact]
    public void Read_FromNullToken_ShouldThrowJsonException()
    {
        var json = "null";

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<BigInteger>(json, Options));
    }

    [Fact]
    public void Read_FromObjectToken_ShouldThrowJsonException()
    {
        var json = "{}";

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<BigInteger>(json, Options));
    }

    [Fact]
    public void Read_FromArrayToken_ShouldThrowJsonException()
    {
        var json = "[1,2,3]";

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<BigInteger>(json, Options));
    }

    [Fact]
    public void RoundTrip_ValueBeyondInt64_ShouldPreserveValue()
    {
        var original = BigInteger.Pow(2, 200) + BigInteger.Parse("98765432109876543210");

        var json = JsonSerializer.Serialize(original, Options);
        var deserialized = JsonSerializer.Deserialize<BigInteger>(json, Options);

        Assert.Equal(original, deserialized);
    }

    [Fact]
    public void RoundTrip_VeryLargeValue_ShouldPreserveValue()
    {
        var original = BigInteger.Parse("99999999999999999999999999999999999999999");

        var json = JsonSerializer.Serialize(original, Options);
        var deserialized = JsonSerializer.Deserialize<BigInteger>(json, Options);

        Assert.Equal(original, deserialized);
    }

    [Fact]
    public void RoundTrip_NegativeValue_ShouldPreserveValue()
    {
        var original = BigInteger.Parse("-123456789012345678901234567890");

        var json = JsonSerializer.Serialize(original, Options);
        var deserialized = JsonSerializer.Deserialize<BigInteger>(json, Options);

        Assert.Equal(original, deserialized);
    }

    [Fact]
    public void RoundTrip_Zero_ShouldPreserveValue()
    {
        var json = JsonSerializer.Serialize(BigInteger.Zero, Options);
        var deserialized = JsonSerializer.Deserialize<BigInteger>(json, Options);

        Assert.Equal(BigInteger.Zero, deserialized);
    }

    private sealed class Wrapper
    {
        public BigInteger Value { get; init; }
    }

    [Fact]
    public void Read_InsideObject_ShouldPopulateProperty()
    {
        var json = "{\"Value\":\"12345678901234567890\"}";

        var wrapper = JsonSerializer.Deserialize<Wrapper>(json, Options);

        Assert.NotNull(wrapper);
        Assert.Equal(BigInteger.Parse("12345678901234567890"), wrapper.Value);
    }

    [Fact]
    public void Write_InsideObject_ShouldSerializePropertyAsString()
    {
        var wrapper = new Wrapper { Value = BigInteger.Parse("55555555555555555555") };

        var json = JsonSerializer.Serialize(wrapper, Options);

        Assert.Equal("{\"Value\":\"55555555555555555555\"}", json);
    }
}