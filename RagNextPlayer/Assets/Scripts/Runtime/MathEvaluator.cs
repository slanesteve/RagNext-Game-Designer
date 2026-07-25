using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RagNextPlayer.Runtime
{
    public static class MathEvaluator
    {
        private static readonly Random Rand = new Random();

        public static double Evaluate(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression)) return 0;
            var tokens = Tokenize(expression);
            int index = 0;
            return ParseExpression(tokens, ref index);
        }

        private static List<string> Tokenize(string expr)
        {
            var tokens = new List<string>();
            var sb = new StringBuilder();
            for (int i = 0; i < expr.Length; i++)
            {
                char c = expr[i];
                if (char.IsWhiteSpace(c)) continue;

                if (c == '+' || c == '-' || c == '*' || c == '/' || c == '%' || c == '(' || c == ')' || c == ',' || c == '^')
                {
                    if (sb.Length > 0)
                    {
                        tokens.Add(sb.ToString());
                        sb.Clear();
                    }
                    tokens.Add(c.ToString());
                }
                else
                {
                    sb.Append(c);
                }
            }
            if (sb.Length > 0)
            {
                tokens.Add(sb.ToString());
            }
            return tokens;
        }

        private static double ParseExpression(List<string> tokens, ref int index)
        {
            double result = ParseTerm(tokens, ref index);
            while (index < tokens.Count)
            {
                string op = tokens[index];
                if (op == "+" || op == "-")
                {
                    index++;
                    double nextTerm = ParseTerm(tokens, ref index);
                    if (op == "+") result += nextTerm;
                    else result -= nextTerm;
                }
                else
                {
                    break;
                }
            }
            return result;
        }

        private static double ParseTerm(List<string> tokens, ref int index)
        {
            double result = ParsePower(tokens, ref index);
            while (index < tokens.Count)
            {
                string op = tokens[index];
                if (op == "*" || op == "/" || op == "%")
                {
                    index++;
                    double nextPower = ParsePower(tokens, ref index);
                    if (op == "*") result *= nextPower;
                    else if (op == "/")
                    {
                        if (nextPower == 0) throw new DivideByZeroException("Division by zero.");
                        result /= nextPower;
                    }
                    else
                    {
                        if (nextPower == 0) throw new DivideByZeroException("Modulo by zero.");
                        result %= nextPower;
                    }
                }
                else
                {
                    break;
                }
            }
            return result;
        }

        private static double ParsePower(List<string> tokens, ref int index)
        {
            double result = ParseFactor(tokens, ref index);
            while (index < tokens.Count && tokens[index] == "^")
            {
                index++;
                double exponent = ParseFactor(tokens, ref index);
                result = Math.Pow(result, exponent);
            }
            return result;
        }

        private static double ParseFactor(List<string> tokens, ref int index)
        {
            if (index >= tokens.Count) return 0;

            string token = tokens[index];
            if (token == "-") // Unary minus
            {
                index++;
                return -ParseFactor(tokens, ref index);
            }
            if (token == "+") // Unary plus
            {
                index++;
                return ParseFactor(tokens, ref index);
            }
            if (token == "(")
            {
                index++;
                double result = ParseExpression(tokens, ref index);
                if (index < tokens.Count && tokens[index] == ")")
                {
                    index++;
                }
                return result;
            }

            // Check for function calls (e.g. random(min, max))
            if (index + 1 < tokens.Count && tokens[index + 1] == "(")
            {
                string funcName = token.ToLowerInvariant();
                index += 2; // skip name and '('
                var args = new List<double>();
                if (index < tokens.Count && tokens[index] != ")")
                {
                    args.Add(ParseExpression(tokens, ref index));
                    while (index < tokens.Count && tokens[index] == ",")
                    {
                        index++;
                        args.Add(ParseExpression(tokens, ref index));
                    }
                }
                if (index < tokens.Count && tokens[index] == ")")
                {
                    index++; // skip ')'
                }

                if ((funcName == "random" || funcName == "rand") && args.Count == 2)
                {
                    int min = (int)args[0];
                    int max = (int)args[1];
                    if (min > max) (min, max) = (max, min);
                    return Rand.Next(min, max + 1);
                }
                else if (funcName == "abs" && args.Count == 1)
                {
                    return Math.Abs(args[0]);
                }
                else if (funcName == "min" && args.Count >= 2)
                {
                    double minVal = args[0];
                    for (int i = 1; i < args.Count; i++) minVal = Math.Min(minVal, args[i]);
                    return minVal;
                }
                else if (funcName == "max" && args.Count >= 2)
                {
                    double maxVal = args[0];
                    for (int i = 1; i < args.Count; i++) maxVal = Math.Max(maxVal, args[i]);
                    return maxVal;
                }
                else if (funcName == "round" && args.Count == 1)
                {
                    return Math.Round(args[0]);
                }
                else if (funcName == "floor" && args.Count == 1)
                {
                    return Math.Floor(args[0]);
                }
                else if (funcName == "ceil" && args.Count == 1)
                {
                    return Math.Ceiling(args[0]);
                }
                else if (funcName == "clamp" && args.Count == 3)
                {
                    double val = args[0];
                    double min = args[1];
                    double max = args[2];
                    return Math.Clamp(val, min, max);
                }
                else if (funcName == "sqrt" && args.Count == 1)
                {
                    return Math.Sqrt(args[0]);
                }
                else if (funcName == "pow" && args.Count == 2)
                {
                    return Math.Pow(args[0], args[1]);
                }
                else if (funcName == "log" && args.Count == 1)
                {
                    return Math.Log(args[0]);
                }
                else if (funcName == "sin" && args.Count == 1)
                {
                    return Math.Sin(args[0]);
                }
                else if (funcName == "cos" && args.Count == 1)
                {
                    return Math.Cos(args[0]);
                }
                else if (funcName == "tan" && args.Count == 1)
                {
                    return Math.Tan(args[0]);
                }
                throw new ArgumentException($"Unknown function '{funcName}' or invalid argument count.");
            }

            index++;
            if (string.Equals(token, "pi", StringComparison.OrdinalIgnoreCase)) return Math.PI;
            if (string.Equals(token, "e", StringComparison.OrdinalIgnoreCase)) return Math.E;

            if (double.TryParse(token, NumberStyles.Any, CultureInfo.InvariantCulture, out double value))
            {
                return value;
            }
            return 0;
        }
    }
}
