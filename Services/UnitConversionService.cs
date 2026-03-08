// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
// 
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
// 
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace G33kSeek.Services;

/// <summary>
/// Converts lightweight no-prefix utility queries such as data sizes and number bases.
/// </summary>
/// <remarks>
/// This keeps quick conversions available from the default launcher flow without pushing that parsing into the UI layer.
/// </remarks>
internal sealed class UnitConversionService
{
    private const decimal CentimetersPerInch = 2.54m;
    private const decimal InchesPerFoot = 12m;
    private const decimal PoundsPerKilogram = 2.2046226218487757m;
    private const decimal PoundsPerStone = 14m;

    private static readonly Regex DataSizeRegex = new(
        @"^\s*(?<value>[+-]?\d+(?:\.\d+)?)\s*(?<from>[a-zA-Z]+)\s+(?:in|to)\s+(?<to>[a-zA-Z]+)\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex PrefixedBaseRegex = new(
        @"^\s*(?<source>hex|dec|decimal)\s+(?<value>[0-9a-fA-F]+)\s+(?:in|to)\s+(?<target>hex|dec|decimal)\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex HexToDecimalRegex = new(
        @"^\s*(?<value>0x[0-9a-fA-F]+)\s+(?:in|to)\s+(?<target>dec|decimal)\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex DecimalToHexRegex = new(
        @"^\s*(?<value>\d+)\s+(?:in|to)\s+(?<target>hex)\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly IReadOnlyDictionary<string, (decimal Multiplier, string DisplayName)> DataUnits =
        new Dictionary<string, (decimal Multiplier, string DisplayName)>(StringComparer.OrdinalIgnoreCase)
        {
            ["b"] = (1m, "bytes"),
            ["byte"] = (1m, "bytes"),
            ["bytes"] = (1m, "bytes"),
            ["kb"] = (1024m, "KB"),
            ["kilobyte"] = (1024m, "KB"),
            ["kilobytes"] = (1024m, "KB"),
            ["mb"] = (1024m * 1024m, "MB"),
            ["megabyte"] = (1024m * 1024m, "MB"),
            ["megabytes"] = (1024m * 1024m, "MB"),
            ["gb"] = (1024m * 1024m * 1024m, "GB"),
            ["gigabyte"] = (1024m * 1024m * 1024m, "GB"),
            ["gigabytes"] = (1024m * 1024m * 1024m, "GB"),
            ["tb"] = (1024m * 1024m * 1024m * 1024m, "TB"),
            ["terabyte"] = (1024m * 1024m * 1024m * 1024m, "TB"),
            ["terabytes"] = (1024m * 1024m * 1024m * 1024m, "TB"),
            ["kib"] = (1024m, "KiB"),
            ["kibibyte"] = (1024m, "KiB"),
            ["kibibytes"] = (1024m, "KiB"),
            ["mib"] = (1024m * 1024m, "MiB"),
            ["mibibyte"] = (1024m * 1024m, "MiB"),
            ["mibibytes"] = (1024m * 1024m, "MiB"),
            ["gib"] = (1024m * 1024m * 1024m, "GiB"),
            ["gibibyte"] = (1024m * 1024m * 1024m, "GiB"),
            ["gibibytes"] = (1024m * 1024m * 1024m, "GiB"),
            ["tib"] = (1024m * 1024m * 1024m * 1024m, "TiB"),
            ["tibibyte"] = (1024m * 1024m * 1024m * 1024m, "TiB"),
            ["tibibytes"] = (1024m * 1024m * 1024m * 1024m, "TiB")
        };

    public bool TryConvert(string query, out string convertedValue, out string description)
    {
        if (query == null)
            throw new ArgumentNullException(nameof(query));

        if (TryConvertDataSize(query, out convertedValue, out description))
            return true;

        if (TryConvertNumberBase(query, out convertedValue, out description))
            return true;

        if (TryConvertMeasurement(query, out convertedValue, out description))
            return true;

        convertedValue = null;
        description = null;
        return false;
    }

    private static bool TryConvertDataSize(string query, out string convertedValue, out string description)
    {
        var match = DataSizeRegex.Match(query);
        if (!match.Success)
        {
            convertedValue = null;
            description = null;
            return false;
        }

        if (!decimal.TryParse(match.Groups["value"].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var sourceValue))
        {
            convertedValue = null;
            description = null;
            return false;
        }

        var fromUnitText = match.Groups["from"].Value;
        var toUnitText = match.Groups["to"].Value;
        if (!DataUnits.TryGetValue(fromUnitText, out var fromUnit) || !DataUnits.TryGetValue(toUnitText, out var toUnit))
        {
            convertedValue = null;
            description = null;
            return false;
        }

        var bytesValue = sourceValue * fromUnit.Multiplier;
        var targetValue = bytesValue / toUnit.Multiplier;
        convertedValue = $"{FormatNumber(targetValue)} {toUnit.DisplayName}";
        description = $"{FormatNumber(sourceValue)} {fromUnit.DisplayName} = {convertedValue}";
        return true;
    }

    private static bool TryConvertNumberBase(string query, out string convertedValue, out string description)
    {
        if (TryConvertPrefixedNumberBase(query, out convertedValue, out description))
            return true;

        var hexToDecimalMatch = HexToDecimalRegex.Match(query);
        if (hexToDecimalMatch.Success)
        {
            var hexValue = hexToDecimalMatch.Groups["value"].Value;
            if (ulong.TryParse(hexValue[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var parsedHex))
            {
                convertedValue = parsedHex.ToString(CultureInfo.InvariantCulture);
                description = $"{hexValue.ToUpperInvariant()} = {convertedValue} decimal";
                return true;
            }
        }

        var decimalToHexMatch = DecimalToHexRegex.Match(query);
        if (decimalToHexMatch.Success &&
            ulong.TryParse(decimalToHexMatch.Groups["value"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedDecimal))
        {
            convertedValue = $"0x{parsedDecimal:X}";
            description = $"{parsedDecimal.ToString(CultureInfo.InvariantCulture)} = {convertedValue}";
            return true;
        }

        convertedValue = null;
        description = null;
        return false;
    }

    private static bool TryConvertPrefixedNumberBase(string query, out string convertedValue, out string description)
    {
        var match = PrefixedBaseRegex.Match(query);
        if (!match.Success)
        {
            convertedValue = null;
            description = null;
            return false;
        }

        var source = match.Groups["source"].Value.ToLowerInvariant();
        var target = match.Groups["target"].Value.ToLowerInvariant();
        var value = match.Groups["value"].Value;

        if (source == target || (source == "dec" && target == "decimal") || (source == "decimal" && target == "dec"))
        {
            convertedValue = null;
            description = null;
            return false;
        }

        if ((source == "hex") && (target == "dec" || target == "decimal") &&
            ulong.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var parsedHex))
        {
            convertedValue = parsedHex.ToString(CultureInfo.InvariantCulture);
            description = $"0x{value.ToUpperInvariant()} = {convertedValue} decimal";
            return true;
        }

        if ((source == "dec" || source == "decimal") && target == "hex" &&
            ulong.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedDecimal))
        {
            convertedValue = $"0x{parsedDecimal:X}";
            description = $"{parsedDecimal.ToString(CultureInfo.InvariantCulture)} = {convertedValue}";
            return true;
        }

        convertedValue = null;
        description = null;
        return false;
    }

    private static bool TryConvertMeasurement(string query, out string convertedValue, out string description)
    {
        var match = DataSizeRegex.Match(query);
        if (!match.Success)
        {
            convertedValue = null;
            description = null;
            return false;
        }

        if (!decimal.TryParse(match.Groups["value"].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var sourceValue))
        {
            convertedValue = null;
            description = null;
            return false;
        }

        var fromUnit = match.Groups["from"].Value.ToLowerInvariant();
        var toUnit = match.Groups["to"].Value.ToLowerInvariant();

        if (TryConvertTemperature(sourceValue, fromUnit, toUnit, out convertedValue, out description))
            return true;

        if (TryConvertWeight(sourceValue, fromUnit, toUnit, out convertedValue, out description))
            return true;

        if (TryConvertLength(sourceValue, fromUnit, toUnit, out convertedValue, out description))
            return true;

        convertedValue = null;
        description = null;
        return false;
    }

    private static bool TryConvertTemperature(decimal sourceValue, string fromUnit, string toUnit, out string convertedValue, out string description)
    {
        if (IsCelsius(fromUnit) && IsFahrenheit(toUnit))
        {
            var fahrenheit = (sourceValue * 9m / 5m) + 32m;
            convertedValue = $"{FormatApproximateNumber(fahrenheit)} F";
            description = $"{FormatNumber(sourceValue)} C = {convertedValue}";
            return true;
        }

        if (IsFahrenheit(fromUnit) && IsCelsius(toUnit))
        {
            var celsius = (sourceValue - 32m) * 5m / 9m;
            convertedValue = $"{FormatApproximateNumber(celsius)} C";
            description = $"{FormatNumber(sourceValue)} F = {convertedValue}";
            return true;
        }

        convertedValue = null;
        description = null;
        return false;
    }

    private static bool TryConvertWeight(decimal sourceValue, string fromUnit, string toUnit, out string convertedValue, out string description)
    {
        if (!TryGetPoundsValue(sourceValue, fromUnit, out var poundsValue))
        {
            convertedValue = null;
            description = null;
            return false;
        }

        if (IsKilograms(toUnit))
        {
            var kilograms = poundsValue / PoundsPerKilogram;
            convertedValue = $"{FormatApproximateNumber(kilograms)} kg";
            description = $"{FormatNumber(sourceValue)} {GetWeightDisplayName(fromUnit)} = {convertedValue}";
            return true;
        }

        if (IsPounds(toUnit))
        {
            convertedValue = $"{FormatApproximateNumber(poundsValue)} lb";
            description = $"{FormatNumber(sourceValue)} {GetWeightDisplayName(fromUnit)} = {convertedValue}";
            return true;
        }

        if (IsStone(toUnit))
        {
            convertedValue = FormatStoneAndPounds(poundsValue);
            description = $"{FormatNumber(sourceValue)} {GetWeightDisplayName(fromUnit)} = {convertedValue}";
            return true;
        }

        convertedValue = null;
        description = null;
        return false;
    }

    private static bool TryConvertLength(decimal sourceValue, string fromUnit, string toUnit, out string convertedValue, out string description)
    {
        if (!TryGetCentimetersValue(sourceValue, fromUnit, out var centimetersValue))
        {
            convertedValue = null;
            description = null;
            return false;
        }

        if (IsCentimeters(toUnit))
        {
            convertedValue = $"{FormatApproximateNumber(centimetersValue)} cm";
            description = $"{FormatNumber(sourceValue)} {GetLengthDisplayName(fromUnit)} = {convertedValue}";
            return true;
        }

        if (IsInches(toUnit))
        {
            var inchesValue = centimetersValue / CentimetersPerInch;
            convertedValue = $"{FormatApproximateNumber(inchesValue)} in";
            description = $"{FormatNumber(sourceValue)} {GetLengthDisplayName(fromUnit)} = {convertedValue}";
            return true;
        }

        if (IsFeet(toUnit))
        {
            var totalInches = centimetersValue / CentimetersPerInch;
            var wholeFeet = decimal.Floor(totalInches / InchesPerFoot);
            var remainingInches = totalInches - (wholeFeet * InchesPerFoot);
            if (remainingInches >= InchesPerFoot)
            {
                wholeFeet++;
                remainingInches -= InchesPerFoot;
            }

            convertedValue = $"{wholeFeet.ToString(CultureInfo.InvariantCulture)} ft {FormatApproximateNumber(remainingInches)} in";
            description = $"{FormatNumber(sourceValue)} {GetLengthDisplayName(fromUnit)} = {convertedValue}";
            return true;
        }

        convertedValue = null;
        description = null;
        return false;
    }

    private static bool TryGetPoundsValue(decimal sourceValue, string unit, out decimal poundsValue)
    {
        if (IsKilograms(unit))
        {
            poundsValue = sourceValue * PoundsPerKilogram;
            return true;
        }

        if (IsPounds(unit))
        {
            poundsValue = sourceValue;
            return true;
        }

        if (IsStone(unit))
        {
            poundsValue = sourceValue * PoundsPerStone;
            return true;
        }

        poundsValue = 0m;
        return false;
    }

    private static bool TryGetCentimetersValue(decimal sourceValue, string unit, out decimal centimetersValue)
    {
        if (IsCentimeters(unit))
        {
            centimetersValue = sourceValue;
            return true;
        }

        if (IsInches(unit))
        {
            centimetersValue = sourceValue * CentimetersPerInch;
            return true;
        }

        if (IsFeet(unit))
        {
            centimetersValue = sourceValue * InchesPerFoot * CentimetersPerInch;
            return true;
        }

        centimetersValue = 0m;
        return false;
    }

    private static bool IsCelsius(string unit) =>
        unit is "c" or "celsius";

    private static bool IsFahrenheit(string unit) =>
        unit is "f" or "fahrenheit";

    private static bool IsKilograms(string unit) =>
        unit is "kg" or "kilogram" or "kilograms";

    private static bool IsPounds(string unit) =>
        unit is "lb" or "lbs" or "pound" or "pounds";

    private static bool IsStone(string unit) =>
        unit is "st" or "stone" or "stones";

    private static bool IsCentimeters(string unit) =>
        unit is "cm" or "centimeter" or "centimeters" or "centimetre" or "centimetres";

    private static bool IsInches(string unit) =>
        unit is "in" or "inch" or "inches";

    private static bool IsFeet(string unit) =>
        unit is "ft" or "foot" or "feet";

    private static string GetWeightDisplayName(string unit)
    {
        if (IsKilograms(unit))
            return "kg";
        if (IsPounds(unit))
            return "lb";
        return "st";
    }

    private static string GetLengthDisplayName(string unit)
    {
        if (IsCentimeters(unit))
            return "cm";
        if (IsInches(unit))
            return "in";
        return "ft";
    }

    private static string FormatNumber(decimal value) =>
        value.ToString("0.################", CultureInfo.InvariantCulture);

    private static string FormatApproximateNumber(decimal value) =>
        Math.Round(value, 4, MidpointRounding.AwayFromZero).ToString("0.####", CultureInfo.InvariantCulture);

    private static string FormatStoneAndPounds(decimal poundsValue)
    {
        var wholeStone = decimal.Floor(poundsValue / PoundsPerStone);
        var remainingPounds = poundsValue - (wholeStone * PoundsPerStone);
        var roundedPounds = decimal.Round(remainingPounds, 0, MidpointRounding.AwayFromZero);

        if (roundedPounds >= PoundsPerStone)
        {
            wholeStone++;
            roundedPounds = 0m;
        }

        return $"{wholeStone.ToString(CultureInfo.InvariantCulture)} st {roundedPounds.ToString("0", CultureInfo.InvariantCulture)} lb";
    }
}
