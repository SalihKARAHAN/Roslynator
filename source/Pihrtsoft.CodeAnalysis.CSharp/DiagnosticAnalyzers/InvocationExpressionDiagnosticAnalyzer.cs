﻿// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Pihrtsoft.CodeAnalysis.CSharp.DiagnosticAnalyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class InvocationExpressionDiagnosticAnalyzer : BaseDiagnosticAnalyzer
    {
        private static readonly ImmutableDictionary<string, string> _propertiesCount
            = ImmutableDictionary.CreateRange(new KeyValuePair<string, string>[] { new KeyValuePair<string, string>("PropertyName", "Count") });

        private static readonly ImmutableDictionary<string, string> _propertiesLength
            = ImmutableDictionary.CreateRange(new KeyValuePair<string, string>[] { new KeyValuePair<string, string>("PropertyName", "Length") });

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        {
            get
            {
                return ImmutableArray.Create(
                    DiagnosticDescriptors.SimplifyLinqMethodChain,
                    DiagnosticDescriptors.UseCountOrLengthPropertyInsteadOfAnyMethod);
            }
        }

        public override void Initialize(AnalysisContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            context.RegisterSyntaxNodeAction(f => AnalyzeInvocationExpression(f), SyntaxKind.InvocationExpression);
        }

        private void AnalyzeInvocationExpression(SyntaxNodeAnalysisContext context)
        {
            if (GeneratedCodeAnalyzer?.IsGeneratedCode(context) == true)
                return;

            var invocation = (InvocationExpressionSyntax)context.Node;

            if (invocation.Expression?.IsKind(SyntaxKind.SimpleMemberAccessExpression) == true
                && invocation.ArgumentList?.Arguments.Count == 0)
            {
                var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;

                string methodName = memberAccess.Name?.Identifier.ValueText;

                switch (methodName)
                {
                    case "Any":
                        {
                            ProcessWhere(context, invocation, memberAccess, methodName);
                            ProcessAny(context, invocation, memberAccess);
                            break;
                        }
                    case "Count":
                    case "First":
                    case "FirstOrDefault":
                    case "Last":
                    case "LastOrDefault":
                    case "LongCount":
                    case "Single":
                    case "SingleOrDefault":
                        {
                            ProcessWhere(context, invocation, memberAccess, methodName);
                            break;
                        }
                }
            }
        }

        private static void ProcessWhere(
            SyntaxNodeAnalysisContext context,
            InvocationExpressionSyntax invocation,
            MemberAccessExpressionSyntax memberAccess,
            string methodName)
        {
            if (memberAccess.Expression?.IsKind(SyntaxKind.InvocationExpression) == true)
            {
                var invocation2 = (InvocationExpressionSyntax)memberAccess.Expression;

                if (invocation2.ArgumentList?.Arguments.Count == 1
                    && invocation2.Expression?.IsKind(SyntaxKind.SimpleMemberAccessExpression) == true)
                {
                    var memberAccess2 = (MemberAccessExpressionSyntax)invocation2.Expression;

                    if (memberAccess2.Name?.Identifier.ValueText == "Where"
                        && IsEnumerableExtensionMethod(context, invocation, methodName)
                        && (IsEnumerableWhereMethod(context, invocation2) || IsImmutableArrayWhereMethod( context, invocation2)))
                    {
                        TextSpan span = TextSpan.FromBounds(invocation2.Span.End, invocation.Span.End);

                        if (invocation
                            .DescendantTrivia(span)
                            .All(f => f.IsWhitespaceOrEndOfLine()))
                        {
                            context.ReportDiagnostic(
                                DiagnosticDescriptors.SimplifyLinqMethodChain,
                                memberAccess2.Name.GetLocation());
                        }
                    }
                }
            }
        }

        private static void ProcessAny(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation, MemberAccessExpressionSyntax memberAccess)
        {
            if (invocation.Parent?.IsKind(SyntaxKind.SimpleMemberAccessExpression) == false
                && IsEnumerableExtensionMethod(context, invocation, "Any"))
            {
                string propertyName = GetCountOrLengthPropertyName(context, memberAccess.Expression);

                if (propertyName != null)
                {
                    string messageArg = null;

                    TextSpan span = TextSpan.FromBounds(memberAccess.Name.Span.Start, invocation.Span.End);

                    if (invocation.DescendantTrivia(span).All(f => f.IsWhitespaceOrEndOfLine()))
                    {
                        if (invocation.Parent?.IsKind(SyntaxKind.LogicalNotExpression) == true)
                        {
                            var logicalNot = (PrefixUnaryExpressionSyntax)invocation.Parent;

                            if (logicalNot.OperatorToken.TrailingTrivia.IsWhitespaceOrEndOfLine()
                                && logicalNot.Operand.GetLeadingTrivia().IsWhitespaceOrEndOfLine())
                            {
                                messageArg = $"{propertyName} == 0";
                            }
                        }
                        else
                        {
                            messageArg = $"{propertyName} > 0";
                        }
                    }

                    if (messageArg != null)
                    {
                        Diagnostic diagnostic = Diagnostic.Create(
                            DiagnosticDescriptors.UseCountOrLengthPropertyInsteadOfAnyMethod,
                            Location.Create(context.Node.SyntaxTree, span),
                            (propertyName == "Count") ? _propertiesCount : _propertiesLength,
                            messageArg);

                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }

        private static string GetCountOrLengthPropertyName(SyntaxNodeAnalysisContext context, ExpressionSyntax expression)
        {
            ITypeSymbol typeSymbol = context
                .SemanticModel
                .GetTypeInfo(expression, context.CancellationToken)
                .Type;

            if (typeSymbol?.IsKind(SymbolKind.ErrorType) == false
                && !IsGenericIEnumerable(typeSymbol))
            {
                if (typeSymbol.BaseType?.SpecialType == SpecialType.System_Array)
                    return "Length";

                for (int i = 0; i < typeSymbol.AllInterfaces.Length; i++)
                {
                    if (typeSymbol.AllInterfaces[i].ConstructedFrom.SpecialType == SpecialType.System_Collections_Generic_ICollection_T)
                    {
                        foreach (ISymbol members in typeSymbol.GetMembers("Count"))
                        {
                            if (members.IsKind(SymbolKind.Property)
                                && members.DeclaredAccessibility == Accessibility.Public)
                            {
                                return "Count";
                            }
                        }
                    }
                }
            }

            return null;
        }

        private static bool IsEnumerableWhereMethod(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation)
        {
            var methodSymbol = context.SemanticModel
                .GetSymbolInfo(invocation, context.CancellationToken)
                .Symbol as IMethodSymbol;

            if (methodSymbol?.ReducedFrom != null)
            {
                methodSymbol = methodSymbol.ReducedFrom;

                return methodSymbol.MetadataName == "Where"
                    && methodSymbol.Parameters.Length == 2
                    && methodSymbol.ContainingType?.Equals(context.GetTypeByMetadataName("System.Linq.Enumerable")) == true
                    && IsGenericIEnumerable(methodSymbol.Parameters[0].Type)
                    && IsPredicate(methodSymbol.Parameters[1].Type, context.SemanticModel);
            }

            return false;
        }

        private static bool IsImmutableArrayWhereMethod(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation)
        {
            var methodSymbol = context.SemanticModel
                .GetSymbolInfo(invocation, context.CancellationToken)
                .Symbol as IMethodSymbol;

            if (methodSymbol?.ReducedFrom != null)
            {
                methodSymbol = methodSymbol.ReducedFrom;

                return methodSymbol.MetadataName == "Where"
                    && methodSymbol.Parameters.Length == 2
                    && methodSymbol.ContainingType?.Equals(context.GetTypeByMetadataName("System.Linq.ImmutableArrayExtensions")) == true
                    && IsGenericImmutableArray(methodSymbol.Parameters[0].Type, context.SemanticModel)
                    && IsPredicate(methodSymbol.Parameters[1].Type, context.SemanticModel);
            }

            return false;
        }

        private static bool IsEnumerableExtensionMethod(
            SyntaxNodeAnalysisContext context,
            InvocationExpressionSyntax invocation,
            string methodName,
            int parameterCount = 1)
        {
            var methodSymbol = context.SemanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;

            if (methodSymbol?.ReducedFrom != null)
            {
                methodSymbol = methodSymbol.ReducedFrom;

                return methodSymbol.MetadataName == methodName
                    && methodSymbol.Parameters.Length == parameterCount
                    && methodSymbol.ContainingType?.Equals(context.GetTypeByMetadataName("System.Linq.Enumerable")) == true
                    && IsGenericIEnumerable(methodSymbol.Parameters[0].Type);
            }

            return false;
        }

        private static bool IsGenericIEnumerable(ITypeSymbol typeSymbol)
        {
            return typeSymbol?.Kind == SymbolKind.NamedType
                && ((INamedTypeSymbol)typeSymbol).ConstructedFrom.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T;
        }

        private static bool IsPredicate(ISymbol symbol, SemanticModel semanticModel)
        {
            return symbol?.IsKind(SymbolKind.NamedType) == true
                && ((INamedTypeSymbol)symbol)
                    .ConstructedFrom
                    .Equals(semanticModel.Compilation.GetTypeByMetadataName("System.Func`2"));
        }

        private static bool IsGenericImmutableArray(ISymbol symbol, SemanticModel semanticModel)
        {
            if (symbol?.IsKind(SymbolKind.NamedType) == true)
            {
                INamedTypeSymbol namedTypeSymbol = semanticModel
                    .Compilation
                    .GetTypeByMetadataName("System.Collections.Immutable.ImmutableArray`1");

                return namedTypeSymbol != null
                    && ((INamedTypeSymbol)symbol).ConstructedFrom.Equals(namedTypeSymbol);
            }

            return false;
        }
    }
}