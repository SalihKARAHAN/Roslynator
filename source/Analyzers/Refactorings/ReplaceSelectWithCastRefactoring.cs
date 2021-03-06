﻿// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Roslynator.CSharp.Extensions;
using Roslynator.Extensions;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using static Roslynator.CSharp.CSharpFactory;

namespace Roslynator.CSharp.Refactorings
{
    internal static class ReplaceSelectWithCastRefactoring
    {
        public static void Analyze(
            SyntaxNodeAnalysisContext context,
            InvocationExpressionSyntax invocation,
            MemberAccessExpressionSyntax memberAccess)
        {
            if (CanRefactor(invocation, context.SemanticModel, context.CancellationToken))
            {
                TextSpan span = TextSpan.FromBounds(memberAccess.Name.Span.Start, invocation.Span.End);

                if (!invocation.ContainsDirectives(span))
                {
                    context.ReportDiagnostic(
                        DiagnosticDescriptors.UseCastMethodInsteadOfSelectMethod,
                        Location.Create(invocation.SyntaxTree, span));
                }
            }
        }

        public static bool CanRefactor(
            InvocationExpressionSyntax invocation,
            SemanticModel semanticModel,
            CancellationToken cancellationToken)
        {
            IMethodSymbol methodSymbol = semanticModel.GetMethodSymbol(invocation, cancellationToken);

            if (methodSymbol != null
                && IsEnumerableOrImmutableArrayExtensionSelectMethod(methodSymbol, semanticModel))
            {
                ArgumentListSyntax argumentList = invocation.ArgumentList;

                if (argumentList?.IsMissing == false)
                {
                    SeparatedSyntaxList<ArgumentSyntax> arguments = argumentList.Arguments;

                    if (arguments.Count == 1)
                    {
                        ArgumentSyntax argument = arguments.First();

                        ExpressionSyntax expression = argument.Expression;

                        if (expression?.IsMissing == false)
                        {
                            SyntaxKind expressionKind = expression.Kind();

                            if (expressionKind == SyntaxKind.SimpleLambdaExpression)
                            {
                                var lambda = (SimpleLambdaExpressionSyntax)expression;

                                if (CanRefactor(lambda.Parameter, lambda.Body))
                                    return true;
                            }
                            else if (expressionKind == SyntaxKind.ParenthesizedLambdaExpression)
                            {
                                var lambda = (ParenthesizedLambdaExpressionSyntax)expression;

                                ParameterListSyntax parameterList = lambda.ParameterList;

                                if (parameterList != null)
                                {
                                    SeparatedSyntaxList<ParameterSyntax> parameters = parameterList.Parameters;

                                    if (parameters.Count == 1
                                        && CanRefactor(parameters.First(), lambda.Body))
                                    {
                                        return true;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return false;
        }

        private static bool CanRefactor(ParameterSyntax parameter, CSharpSyntaxNode body)
        {
            if (parameter != null && body != null)
            {
                CastExpressionSyntax castExpression = GetCastExpression(body);

                if (castExpression != null)
                {
                    ExpressionSyntax expression = castExpression.Expression;

                    if (expression?.IsKind(SyntaxKind.IdentifierName) == true
                        && string.Equals(
                            parameter.Identifier.ValueText,
                            ((IdentifierNameSyntax)expression).Identifier.ValueText,
                            StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static CastExpressionSyntax GetCastExpression(CSharpSyntaxNode body)
        {
            switch (body?.Kind())
            {
                case SyntaxKind.CastExpression:
                    {
                        return (CastExpressionSyntax)body;
                    }
                case SyntaxKind.Block:
                    {
                        var block = (BlockSyntax)body;

                        SyntaxList<StatementSyntax> statements = block.Statements;

                        if (statements.Count == 1)
                        {
                            StatementSyntax statement = statements.First();

                            if (statement.IsKind(SyntaxKind.ReturnStatement))
                            {
                                var returnStatement = (ReturnStatementSyntax)statement;

                                ExpressionSyntax returnExpression = returnStatement.Expression;

                                if (returnExpression?.IsKind(SyntaxKind.CastExpression) == true)
                                    return (CastExpressionSyntax)returnExpression;
                            }
                        }

                        break;
                    }
            }

            return null;
        }

        private static bool IsEnumerableOrImmutableArrayExtensionSelectMethod(IMethodSymbol methodSymbol, SemanticModel semanticModel)
        {
            if (Symbol.IsEnumerableOrImmutableArrayExtensionMethod(methodSymbol, "Select", semanticModel))
            {
                IParameterSymbol parameter = methodSymbol.SingleParameterOrDefault();

                return parameter != null
                    && Symbol.IsFunc(parameter.Type, methodSymbol.TypeArguments[0], methodSymbol.TypeArguments[1], semanticModel);
            }
            else
            {
                return false;
            }
        }

        public static async Task<Document> RefactorAsync(
            Document document,
            InvocationExpressionSyntax invocation,
            CancellationToken cancellationToken)
        {
            var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;

            TypeSyntax type = GetType(invocation);

            InvocationExpressionSyntax newInvocation = invocation.Update(
                memberAccess.WithName(GenericName(Identifier("Cast"), type)),
                invocation.ArgumentList.WithArguments(SeparatedList<ArgumentSyntax>()));

            return await document.ReplaceNodeAsync(invocation, newInvocation, cancellationToken).ConfigureAwait(false);
        }

        private static TypeSyntax GetType(InvocationExpressionSyntax invocation)
        {
            ExpressionSyntax expression = invocation.ArgumentList.Arguments.First().Expression;

            switch (expression.Kind())
            {
                case SyntaxKind.SimpleLambdaExpression:
                    {
                        var lambda = (SimpleLambdaExpressionSyntax)expression;

                        return GetCastExpression(lambda.Body).Type;
                    }
                case SyntaxKind.ParenthesizedLambdaExpression:
                    {
                        var lambda = (ParenthesizedLambdaExpressionSyntax)expression;

                        return GetCastExpression(lambda.Body).Type;
                    }
                default:
                    {
                        Debug.Assert(false, expression.Kind().ToString());
                        return null;
                    }
            }
        }
    }
}
