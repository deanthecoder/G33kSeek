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
using System.Threading;
using System.Threading.Tasks;
using G33kSeek.Models;
using NCalc;

namespace G33kSeek.Providers;

/// <summary>
/// Evaluates calculator expressions for the <c>=</c> prefix.
/// </summary>
/// <remarks>
/// This is the first fully functional provider and establishes the pattern for value-producing queries whose Enter action copies output.
/// </remarks>
public sealed class CalculatorQueryProvider : IQueryProvider
{
    private static readonly Regex WholeNumberRegex = new(@"(?<![\w.])\d+(?![\w.])", RegexOptions.Compiled);

    public string Prefix => "=";

    public QueryProviderHelpEntry HelpEntry =>
        new("Calculator", "Use = to evaluate maths expressions. Enter copies the result.", "=sin(pi/2)");

    public Task<QueryResponse> QueryAsync(QueryRequest request, CancellationToken cancellationToken)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        cancellationToken.ThrowIfCancellationRequested();

        var expressionText = request.ProviderQuery?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(expressionText))
        {
            return Task.FromResult(
                new QueryResponse(
                    [
                        new QueryResult(
                            HelpEntry.Title,
                            "Type an expression like =2+2 or =sin(pi / 2). Trig uses radians.",
                            Prefix)
                    ],
                    "Calculator mode is ready."));
        }

        try
        {
            var resultText = Evaluate(expressionText);
            return Task.FromResult(
                new QueryResponse(
                    [
                        new QueryResult(
                            resultText,
                            $"= {expressionText}",
                            "Enter copies",
                            new QueryActionDescriptor(
                                QueryActionKind.CopyText,
                                resultText,
                                $"Copied {resultText} to the clipboard."))
                    ],
                    $"Calculation ready: {resultText}. Press Enter to copy it."));
        }
        catch (Exception exception)
        {
            return Task.FromResult(
                new QueryResponse(
                    [
                        new QueryResult(
                            "Invalid calculation",
                            exception.Message,
                            "=")
                    ],
                    "That expression could not be evaluated."));
        }
    }

    private static string Evaluate(string expressionText)
    {
        var normalizedExpressionText = NormalizeIntegerLiterals(expressionText);
        var expression = new Expression(
            normalizedExpressionText,
            ExpressionOptions.IgnoreCaseAtBuiltInFunctions)
        {
            Parameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["pi"] = Math.PI,
                ["e"] = Math.E
            },
            Functions = new Dictionary<string, ExpressionFunction>(StringComparer.OrdinalIgnoreCase)
            {
                ["sin"] = arguments => Math.Sin(ToDouble(arguments[0].Evaluate())),
                ["cos"] = arguments => Math.Cos(ToDouble(arguments[0].Evaluate())),
                ["tan"] = arguments => Math.Tan(ToDouble(arguments[0].Evaluate())),
                ["asin"] = arguments => Math.Asin(ToDouble(arguments[0].Evaluate())),
                ["acos"] = arguments => Math.Acos(ToDouble(arguments[0].Evaluate())),
                ["atan"] = arguments => Math.Atan(ToDouble(arguments[0].Evaluate()))
            }
        };

        var rawResult = expression.Evaluate();
        return FormatResult(rawResult);
    }

    private static string NormalizeIntegerLiterals(string expressionText)
    {
        return WholeNumberRegex.Replace(
            expressionText,
            match => $"{match.Value}.0");
    }

    private static double ToDouble(object value) =>
        Convert.ToDouble(value, CultureInfo.InvariantCulture);

    private static string FormatResult(object rawResult)
    {
        return rawResult switch
        {
            null => string.Empty,
            decimal decimalValue => decimalValue.ToString(CultureInfo.InvariantCulture),
            double doubleValue => doubleValue.ToString("G15", CultureInfo.InvariantCulture),
            float floatValue => floatValue.ToString("G9", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => rawResult.ToString() ?? string.Empty
        };
    }
}
