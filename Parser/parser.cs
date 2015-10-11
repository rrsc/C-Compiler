﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ParserCombinator;

public partial class Parser2 {
    public sealed class ParserEnvironment { }

    public sealed class ParserInput {
        public ParserInput(ParserEnvironment environment, IEnumerable<Token> source) {
            this.Environment = environment;
            this.Source = source;
        }
        public ParserEnvironment Environment { get; }
        public IEnumerable<Token> Source { get; }
    }

    public interface ParserResult<out R> : ParserResult {
        R Result { get; }
    }

    public sealed class ParserFailed<R> : ParserResult<R> {
        public ParserInput ToInput() {
            throw new InvalidOperationException("Parser failed, can't construct input.");
        }

        public Boolean IsSuccessful => false;

        public R Result {
            get {
                throw new NotSupportedException("Parser failed, can't get result.");
            }
        }

        public ParserEnvironment Environment {
            get {
                throw new NotSupportedException("Parser failed, can't get environment.");
            }
        }

        public IEnumerable<Token> Source {
            get {
                throw new NotSupportedException("Parser failed, can't get source.");
            }
        }
    }

    public sealed class ParserSucceeded<R> : ParserResult<R> {
        public ParserSucceeded(R result, ParserEnvironment environment, IEnumerable<Token> source) {
            this.Result = result;
            this.Environment = environment;
            this.Source = source;
        }

        public ParserInput ToInput() => new ParserInput(Environment, Source);

        public Boolean IsSuccessful => true;

        public R Result { get; }

        public ParserEnvironment Environment { get; }

        public IEnumerable<Token> Source { get; }
    }

    public interface ParserResult {
        ParserInput ToInput();

        Boolean IsSuccessful { get; }

        ParserEnvironment Environment { get; }

        IEnumerable<Token> Source { get; }
    }

    public sealed class ParserFailed : ParserResult {
        public ParserInput ToInput() {
            throw new InvalidOperationException("Parser failed, can't construct input.");
        }

        public Boolean IsSuccessful => false;

        public ParserEnvironment Environment {
            get {
                throw new NotSupportedException("Parser failed, can't get environment.");
            }
        }

        public IEnumerable<Token> Source {
            get {
                throw new NotSupportedException("Parser failed, can't get source.");
            }
        }
    }

    public sealed class ParserSucceeded : ParserResult {
        public ParserSucceeded(ParserEnvironment environment, IEnumerable<Token> source) {
            this.Environment = environment;
            this.Source = source;
        }

        public Boolean IsSuccessful => true;

        public ParserInput ToInput() => new ParserInput(Environment, Source);

        public ParserEnvironment Environment { get; }

        public IEnumerable<Token> Source { get; }

        public static ParserSucceeded Create(ParserEnvironment environment, IEnumerable<Token> source) =>
            new ParserSucceeded(environment, source);

        public static ParserSucceeded<R> Create<R>(R result, ParserEnvironment environment, IEnumerable<Token> source) =>
            new ParserSucceeded<R>(result, environment, source);
    }

    public static ParsingFunction Keyword(KeywordVal keyword) => input => {
        if (input.Source.First().type == TokenType.KEYWORD && (input.Source.First() as TokenKeyword).val == keyword) {
            return new ParserSucceeded(input.Environment, input.Source.Skip(1));
        } else {
            return new ParserFailed();
        }
    };

    public static ParsingFunction Operator(OperatorVal op) => input => {
        if ((input.Source.First() as TokenOperator)?.val == op) {
            return new ParserSucceeded(input.Environment, input.Source.Skip(1));
        } else {
            return new ParserFailed();
        }
    };

    public static ParsingFunction<R> ParseToken<T, R>(Func<T, R> TokenToNode) where T : Token => input => {
        if (input.Source.First() is T) {
            var token = input.Source.First() as T;
            return ParserSucceeded.Create(
                TokenToNode(token),
                input.Environment,
                input.Source.Skip(1)
            );
        } else {
            return new ParserFailed<R>();
        }
    };

    public static ParsingFunction ParseToken<T>() where T : Token => input => {
        if (input.Source.First() is T) {
            return ParserSucceeded.Create(input.Environment, input.Source.Skip(1));
        } else {
            return new ParserFailed();
        }
    };

    public static ParsingFunction<R> ParseTokenWhen<T, R>(Predicate<T> pred, Func<T, R> TokenToNode) where T : Token => input => {
        if (input.Source.First() is T) {
            var token = input.Source.First() as T;
            if (pred(token)) {
                return ParserSucceeded.Create(
                    TokenToNode(token),
                    input.Environment,
                    input.Source.Skip(1)
                );
            }
        }
        return new ParserFailed<R>();
    };

    public delegate SyntaxTree.Expr BinaryExpressionConstructor(SyntaxTree.Expr lhs, SyntaxTree.Expr rhs);

    public static ParsingFunction<R> AlwaysFail<R>() => input => new ParserFailed<R>();

    public static ICovariantTuple<OperatorVal, BinaryExpressionConstructor> CreateDealer(OperatorVal op, BinaryExpressionConstructor constructor) =>
        CovariantTuple.Create(op, constructor);

    public static ParsingFunction<SyntaxTree.Expr> ParseBinaryOperator(
        ParsingFunction<SyntaxTree.Expr> ParseOperand,
        params ICovariantTuple<OperatorVal, BinaryExpressionConstructor>[] dealers
    ) => input => {

        // 1. Parse the first operand.
        var curResult = ParseOperand(input);
        if (!curResult.IsSuccessful) {
            return new ParserFailed<SyntaxTree.Expr>();
        }

        var lastSuccessfulResult = curResult;

        /// <summary>
        /// rhs
        ///   : operator1 operand
        ///   | operator2 operand
        ///   ...
        ///   | operatorN operand
        /// </summary>
        /// <remarks>
        /// Note that the return of this parsing function is (constructor : (Expr, Expr) => Expr, rhs : Expr).
        /// </remarks>
        var rhsParsingFunction = dealers.Aggregate(
            seed: AlwaysFail<Tuple<BinaryExpressionConstructor, SyntaxTree.Expr>>(),
            func: (Parse, dealer) => Parse.or(
                Operator(dealer.Head).then(ParseOperand).transform(rhsOperand => Tuple.Create(dealer.Tail, rhsOperand))
            )
        );

        // 2. Try to get another operator and operand, until fail.
        do {
            lastSuccessfulResult = curResult;
            input = lastSuccessfulResult.ToInput();
            curResult = rhsParsingFunction.transform(constructorAndOperand => constructorAndOperand.Item1(lastSuccessfulResult.Result, constructorAndOperand.Item2))(input);
        } while (curResult.IsSuccessful);

        return lastSuccessfulResult;
    };

    
}
