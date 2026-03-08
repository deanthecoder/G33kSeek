// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
// 
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
// 
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using G33kSeek.Services;

namespace G33kSeek.Tests;

public class UnitConversionServiceTests
{
    [Test]
    public void TryConvertReturnsDataSizeConversion()
    {
        var service = new UnitConversionService();

        var success = service.TryConvert("10mb in bytes", out var convertedValue, out var description);

        Assert.That(success, Is.True);
        Assert.That(convertedValue, Is.EqualTo("10485760 bytes"));
        Assert.That(description, Is.EqualTo("10 MB = 10485760 bytes"));
    }

    [Test]
    public void TryConvertReturnsBinaryKilobyteConversion()
    {
        var service = new UnitConversionService();

        var success = service.TryConvert("1kb in bytes", out var convertedValue, out var description);

        Assert.That(success, Is.True);
        Assert.That(convertedValue, Is.EqualTo("1024 bytes"));
        Assert.That(description, Is.EqualTo("1 KB = 1024 bytes"));
    }

    [Test]
    public void TryConvertReturnsDecimalToHexConversion()
    {
        var service = new UnitConversionService();

        var success = service.TryConvert("255 in hex", out var convertedValue, out var description);

        Assert.That(success, Is.True);
        Assert.That(convertedValue, Is.EqualTo("0xFF"));
        Assert.That(description, Is.EqualTo("255 = 0xFF"));
    }

    [Test]
    public void TryConvertReturnsCelsiusToFahrenheitConversion()
    {
        var service = new UnitConversionService();

        var success = service.TryConvert("100c in f", out var convertedValue, out var description);

        Assert.That(success, Is.True);
        Assert.That(convertedValue, Is.EqualTo("212 F"));
        Assert.That(description, Is.EqualTo("100 C = 212 F"));
    }

    [Test]
    public void TryConvertReturnsKilogramsToPoundsConversion()
    {
        var service = new UnitConversionService();

        var success = service.TryConvert("10kg in lbs", out var convertedValue, out var description);

        Assert.That(success, Is.True);
        Assert.That(convertedValue, Is.EqualTo("22.0462 lb"));
        Assert.That(description, Is.EqualTo("10 kg = 22.0462 lb"));
    }

    [Test]
    public void TryConvertReturnsKilogramsToStoneConversion()
    {
        var service = new UnitConversionService();

        var success = service.TryConvert("10kg in st", out var convertedValue, out var description);

        Assert.That(success, Is.True);
        Assert.That(convertedValue, Is.EqualTo("1 st 8 lb"));
        Assert.That(description, Is.EqualTo("10 kg = 1 st 8 lb"));
    }

    [Test]
    public void TryConvertReturnsStoneAndPoundsForKilogramsToStone()
    {
        var service = new UnitConversionService();

        var success = service.TryConvert("68kg in stone", out var convertedValue, out var description);

        Assert.That(success, Is.True);
        Assert.That(convertedValue, Is.EqualTo("10 st 10 lb"));
        Assert.That(description, Is.EqualTo("68 kg = 10 st 10 lb"));
    }

    [Test]
    public void TryConvertReturnsCentimetersToFeetAndInchesConversion()
    {
        var service = new UnitConversionService();

        var success = service.TryConvert("180cm in ft", out var convertedValue, out var description);

        Assert.That(success, Is.True);
        Assert.That(convertedValue, Is.EqualTo("5 ft 10.8661 in"));
        Assert.That(description, Is.EqualTo("180 cm = 5 ft 10.8661 in"));
    }

    [Test]
    public void TryConvertReturnsFeetToCentimetersConversion()
    {
        var service = new UnitConversionService();

        var success = service.TryConvert("6ft in cm", out var convertedValue, out var description);

        Assert.That(success, Is.True);
        Assert.That(convertedValue, Is.EqualTo("182.88 cm"));
        Assert.That(description, Is.EqualTo("6 ft = 182.88 cm"));
    }

    [Test]
    public void TryConvertReturnsHexToDecimalConversion()
    {
        var service = new UnitConversionService();

        var success = service.TryConvert("0xff in decimal", out var convertedValue, out var description);

        Assert.That(success, Is.True);
        Assert.That(convertedValue, Is.EqualTo("255"));
        Assert.That(description, Is.EqualTo("0XFF = 255 decimal"));
    }

    [Test]
    public void TryConvertReturnsFalseForUnknownUnits()
    {
        var service = new UnitConversionService();

        var success = service.TryConvert("10 frogs in bytes", out var convertedValue, out var description);

        Assert.That(success, Is.False);
        Assert.That(convertedValue, Is.Null);
        Assert.That(description, Is.Null);
    }
}
