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
                                successMessage: $"Copied {resultText} to the clipboard."))
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
        var normalizedExpressionText = NormalizeIntegerLiterals(NormalizeExponentiation(expressionText));
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

    private static string NormalizeExponentiation(string expressionText)
    {
        if (string.IsNullOrWhiteSpace(expressionText) || !expressionText.Contains('^'))
            return expressionText;

        return new ExponentiationExpressionRewriter(expressionText).Rewrite();
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

    private sealed class ExponentiationExpressionRewriter
    {
        private readonly string m_expressionText;
        private int m_index;

        public ExponentiationExpressionRewriter(string expressionText)
        {
            m_expressionText = expressionText ?? throw new ArgumentNullException(nameof(expressionText));
        }

        public string Rewrite()
        {
            var rewrittenExpression = ParseExpression();
            SkipWhitespace();
            if (!IsAtEnd)
                throw new FormatException($"Unexpected token '{Current}' in expression.");

            return rewrittenExpression;
        }

        private bool IsAtEnd => m_index >= m_expressionText.Length;

        private char Current => IsAtEnd ? '\0' : m_expressionText[m_index];

        private string ParseExpression() => ParseAdditive();

        private string ParseAdditive()
        {
            var left = ParseMultiplicative();
            while (true)
            {
                SkipWhitespace();
                if (!TryConsume('+') && !TryConsume('-'))
                    return left;

                var operatorCharacter = m_expressionText[m_index - 1];
                var right = ParseMultiplicative();
                left = $"({left}{operatorCharacter}{right})";
            }
        }

        private string ParseMultiplicative()
        {
            var left = ParseUnary();
            while (true)
            {
                SkipWhitespace();
                if (!TryConsume('*') && !TryConsume('/') && !TryConsume('%'))
                    return left;

                var operatorCharacter = m_expressionText[m_index - 1];
                var right = ParseUnary();
                left = $"({left}{operatorCharacter}{right})";
            }
        }

        private string ParseUnary()
        {
            SkipWhitespace();
            if (TryConsume('+'))
                return $"(+{ParseUnary()})";

            if (TryConsume('-'))
                return $"(-{ParseUnary()})";

            return ParsePower();
        }

        private string ParsePower()
        {
            var left = ParsePrimary();
            SkipWhitespace();
            if (!TryConsume('^'))
                return left;

            var right = ParseUnary();
            return $"Pow({left},{right})";
        }

        private string ParsePrimary()
        {
            SkipWhitespace();
            if (TryConsume('('))
            {
                var innerExpression = ParseExpression();
                SkipWhitespace();
                Expect(')');
                return $"({innerExpression})";
            }

            if (IsIdentifierStart(Current))
                return ParseIdentifierOrFunctionCall();

            if (char.IsDigit(Current) || Current == '.')
                return ParseNumber();

            throw new FormatException($"Unexpected token '{Current}' in expression.");
        }

        private string ParseIdentifierOrFunctionCall()
        {
            var identifier = ParseIdentifier();
            SkipWhitespace();
            if (!TryConsume('('))
                return identifier;

            var arguments = new List<string>();
            SkipWhitespace();
            if (!TryConsume(')'))
            {
                do
                {
                    arguments.Add(ParseExpression());
                    SkipWhitespace();
                } while (TryConsume(','));

                Expect(')');
            }

            return $"{identifier}({string.Join(", ", arguments)})";
        }

        private string ParseIdentifier()
        {
            var startIndex = m_index;
            while (!IsAtEnd && IsIdentifierPart(Current))
                m_index++;

            return m_expressionText[startIndex..m_index];
        }

        private string ParseNumber()
        {
            var startIndex = m_index;

            if (Current == '.')
                m_index++;

            while (!IsAtEnd && char.IsDigit(Current))
                m_index++;

            if (!IsAtEnd && Current == '.')
            {
                m_index++;
                while (!IsAtEnd && char.IsDigit(Current))
                    m_index++;
            }

            if (!IsAtEnd && (Current == 'e' || Current == 'E'))
            {
                var exponentIndex = m_index;
                m_index++;
                if (!IsAtEnd && (Current == '+' || Current == '-'))
                    m_index++;

                var hasExponentDigits = false;
                while (!IsAtEnd && char.IsDigit(Current))
                {
                    hasExponentDigits = true;
                    m_index++;
                }

                if (!hasExponentDigits)
                    m_index = exponentIndex;
            }

            return m_expressionText[startIndex..m_index];
        }

        private void SkipWhitespace()
        {
            while (!IsAtEnd && char.IsWhiteSpace(Current))
                m_index++;
        }

        private bool TryConsume(char character)
        {
            if (Current != character)
                return false;

            m_index++;
            return true;
        }

        private void Expect(char character)
        {
            if (!TryConsume(character))
                throw new FormatException($"Expected '{character}' in expression.");
        }

        private static bool IsIdentifierStart(char character) =>
            char.IsLetter(character) || character == '_';

        private static bool IsIdentifierPart(char character) =>
            char.IsLetterOrDigit(character) || character == '_';
    }
}
