using System;
using Xunit;
using RagsCore.Services;

namespace RagNext.Tests
{
    public class MathEvaluatorTests
    {
        [Theory]
        [InlineData("10 + 5", 15)]
        [InlineData("10 - 4 * 2", 2)]
        [InlineData("(10 - 4) * 2", 12)]
        [InlineData("2 ^ 3", 8)]
        [InlineData("10 / 2", 5)]
        [InlineData("pi * 2", Math.PI * 2)]
        [InlineData("e", Math.E)]
        public void Evaluate_BasicArithmetic_ReturnsExpectedResult(string expression, double expected)
        {
            double result = MathEvaluator.Evaluate(expression);
            Assert.Equal(expected, result, precision: 5);
        }

        [Fact]
        public void Evaluate_TrigonometricAndMathFunctions_ReturnsExpectedResult()
        {
            double sinResult = MathEvaluator.Evaluate("sin(pi / 2)");
            double cosResult = MathEvaluator.Evaluate("cos(0)");
            double absResult = MathEvaluator.Evaluate("abs(-15.5)");

            Assert.Equal(1.0, sinResult, precision: 5);
            Assert.Equal(1.0, cosResult, precision: 5);
            Assert.Equal(15.5, absResult, precision: 5);
        }

        [Fact]
        public void Evaluate_RandomFunction_ReturnsValueWithinBounds()
        {
            double randResult = MathEvaluator.Evaluate("random(1, 10)");
            Assert.True(randResult >= 1.0 && randResult <= 10.0);
        }

        [Theory]
        [InlineData("10 + (5 *")]
        [InlineData("10 + * 5")]
        [InlineData("invalid_func_name(10)")]
        public void Evaluate_InvalidSyntax_ThrowsArgumentException(string invalidExpression)
        {
            Assert.Throws<ArgumentException>(() => MathEvaluator.Evaluate(invalidExpression));
        }

        [Fact]
        public void Evaluate_EmptyOrWhitespace_ReturnsZero()
        {
            Assert.Equal(0, MathEvaluator.Evaluate(""));
            Assert.Equal(0, MathEvaluator.Evaluate("   "));
        }
    }
}
