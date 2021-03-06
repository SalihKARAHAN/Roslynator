﻿// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Roslynator.CSharp.Extensions;
using Roslynator.Extensions;

namespace Roslynator.CSharp.Refactorings
{
    internal static class RemoveRedundantOverridenMemberRefactoring
    {
        internal static void Analyze(SyntaxNodeAnalysisContext context, MethodDeclarationSyntax methodDeclaration)
        {
            SyntaxTokenList modifiers = methodDeclaration.Modifiers;

            if (modifiers.Contains(SyntaxKind.OverrideKeyword)
                && !modifiers.Contains(SyntaxKind.SealedKeyword)
                && !modifiers.Contains(SyntaxKind.PartialKeyword))
            {
                ExpressionSyntax expression = GetMethodExpression(methodDeclaration);

                if (expression?.IsKind(SyntaxKind.InvocationExpression) == true)
                {
                    var invocation = (InvocationExpressionSyntax)expression;

                    ExpressionSyntax invocationExpression = invocation.Expression;

                    if (invocationExpression?.IsKind(SyntaxKind.SimpleMemberAccessExpression) == true)
                    {
                        var memberAccess = (MemberAccessExpressionSyntax)invocationExpression;

                        if (memberAccess.Expression?.IsKind(SyntaxKind.BaseExpression) == true)
                        {
                            SimpleNameSyntax simpleName = memberAccess.Name;

                            IMethodSymbol methodSymbol = context.SemanticModel.GetDeclaredSymbol(methodDeclaration, context.CancellationToken);

                            if (methodSymbol != null)
                            {
                                IMethodSymbol overridenMethod = methodSymbol.OverriddenMethod;

                                if (overridenMethod != null)
                                {
                                    ISymbol symbol = context.SemanticModel.GetSymbol(simpleName, context.CancellationToken);

                                    if (overridenMethod.Equals(symbol)
                                        && !methodDeclaration.ContainsDirectives)
                                    {
                                        context.ReportDiagnostic(
                                            DiagnosticDescriptors.RemoveRedundantOverridenMember,
                                            methodDeclaration.GetLocation(),
                                            "method");
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private static ExpressionSyntax GetMethodExpression(MethodDeclarationSyntax methodDeclaration)
        {
            BlockSyntax body = methodDeclaration.Body;

            if (body != null)
            {
                StatementSyntax statement = body.SingleStatementOrDefault();

                if (statement != null)
                {
                    if (methodDeclaration.ReturnsVoid())
                    {
                        if (statement.IsKind(SyntaxKind.ExpressionStatement))
                            return ((ExpressionStatementSyntax)statement).Expression;
                    }
                    else if (statement.IsKind(SyntaxKind.ReturnStatement))
                    {
                        return ((ReturnStatementSyntax)statement).Expression;
                    }
                }
            }
            else
            {
                return methodDeclaration.ExpressionBody?.Expression;
            }

            return null;
        }

        internal static void Analyze(SyntaxNodeAnalysisContext context, PropertyDeclarationSyntax propertyDeclaration)
        {
            SyntaxTokenList modifiers = propertyDeclaration.Modifiers;

            if (modifiers.Contains(SyntaxKind.OverrideKeyword)
                && !modifiers.Contains(SyntaxKind.SealedKeyword)
                && propertyDeclaration
                    .AccessorList?
                    .Accessors
                    .All(accessor => CanRefactor(propertyDeclaration, accessor, context.SemanticModel, context.CancellationToken)) == true
                && !propertyDeclaration.ContainsDirectives)
            {
                context.ReportDiagnostic(
                    DiagnosticDescriptors.RemoveRedundantOverridenMember,
                    propertyDeclaration.GetLocation(),
                    "property");
            }
        }

        internal static bool CanRefactor(
            PropertyDeclarationSyntax propertyDeclaration,
            AccessorDeclarationSyntax accessor,
            SemanticModel semanticModel,
            CancellationToken cancellationToken)
        {
            switch (accessor.Kind())
            {
                case SyntaxKind.GetAccessorDeclaration:
                    {
                        ExpressionSyntax expression = GetGetAccessorExpression(accessor);

                        if (expression?.IsKind(SyntaxKind.SimpleMemberAccessExpression) == true)
                        {
                            var memberAccess = (MemberAccessExpressionSyntax)expression;

                            if (memberAccess.Expression?.IsKind(SyntaxKind.BaseExpression) == true)
                            {
                                SimpleNameSyntax simpleName = memberAccess.Name;

                                IPropertySymbol propertySymbol = semanticModel.GetDeclaredSymbol(propertyDeclaration, cancellationToken);

                                if (propertySymbol != null)
                                {
                                    IPropertySymbol overridenProperty = propertySymbol.OverriddenProperty;

                                    if (overridenProperty != null)
                                    {
                                        ISymbol symbol = semanticModel.GetSymbol(simpleName, cancellationToken);

                                        if (overridenProperty.Equals(symbol))
                                            return true;
                                    }
                                }
                            }
                        }

                        return false;
                    }
                case SyntaxKind.SetAccessorDeclaration:
                    {
                        ExpressionSyntax expression = GetSetAccessorExpression(accessor);

                        if (expression?.IsKind(SyntaxKind.SimpleAssignmentExpression) == true)
                        {
                            var assignment = (AssignmentExpressionSyntax)expression;

                            ExpressionSyntax left = assignment.Left;

                            if (left?.IsKind(SyntaxKind.SimpleMemberAccessExpression) == true)
                            {
                                var memberAccess = (MemberAccessExpressionSyntax)left;

                                if (memberAccess.Expression?.IsKind(SyntaxKind.BaseExpression) == true)
                                {
                                    ExpressionSyntax right = assignment.Right;

                                    if (right?.IsKind(SyntaxKind.IdentifierName) == true)
                                    {
                                        var identifierName = (IdentifierNameSyntax)right;

                                        if (identifierName.Identifier.ValueText == "value")
                                        {
                                            SimpleNameSyntax simpleName = memberAccess.Name;

                                            if (simpleName != null)
                                            {
                                                IPropertySymbol propertySymbol = semanticModel.GetDeclaredSymbol(propertyDeclaration, cancellationToken);

                                                if (propertySymbol != null)
                                                {
                                                    IPropertySymbol overridenProperty = propertySymbol.OverriddenProperty;

                                                    if (overridenProperty != null)
                                                    {
                                                        ISymbol symbol = semanticModel.GetSymbol(simpleName, cancellationToken);

                                                        if (overridenProperty.Equals(symbol))
                                                            return true;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        return false;
                    }
                case SyntaxKind.UnknownAccessorDeclaration:
                    {
                        return false;
                    }
                default:
                    {
                        Debug.Assert(false, accessor.Kind().ToString());
                        return false;
                    }
            }
        }

        internal static void Analyze(SyntaxNodeAnalysisContext context, IndexerDeclarationSyntax indexerDeclaration)
        {
            SyntaxTokenList modifiers = indexerDeclaration.Modifiers;

            if (modifiers.Contains(SyntaxKind.OverrideKeyword)
                && !modifiers.Contains(SyntaxKind.SealedKeyword)
                && indexerDeclaration
                    .AccessorList?
                    .Accessors
                    .All(accessor => CanRefactor(indexerDeclaration, accessor, context.SemanticModel, context.CancellationToken)) == true
                && !indexerDeclaration.ContainsDirectives)
            {
                context.ReportDiagnostic(
                    DiagnosticDescriptors.RemoveRedundantOverridenMember,
                    indexerDeclaration.GetLocation(),
                    "indexer");
            }
        }

        internal static bool CanRefactor(
            IndexerDeclarationSyntax indexerDeclaration,
            AccessorDeclarationSyntax accessor,
            SemanticModel semanticModel,
            CancellationToken cancellationToken)
        {
            switch (accessor.Kind())
            {
                case SyntaxKind.GetAccessorDeclaration:
                    {
                        ExpressionSyntax expression = GetGetAccessorExpression(accessor);

                        if (expression?.IsKind(SyntaxKind.ElementAccessExpression) == true)
                        {
                            var elementAccess = (ElementAccessExpressionSyntax)expression;

                            if (elementAccess.Expression?.IsKind(SyntaxKind.BaseExpression) == true
                                && elementAccess.ArgumentList != null)
                            {
                                IPropertySymbol propertySymbol = semanticModel.GetDeclaredSymbol(indexerDeclaration, cancellationToken);

                                if (propertySymbol != null)
                                {
                                    IPropertySymbol overridenProperty = propertySymbol.OverriddenProperty;

                                    if (overridenProperty != null)
                                    {
                                        ISymbol symbol = semanticModel.GetSymbol(elementAccess, cancellationToken);

                                        if (overridenProperty.Equals(symbol))
                                            return true;
                                    }
                                }
                            }
                        }

                        return false;
                    }
                case SyntaxKind.SetAccessorDeclaration:
                    {
                        ExpressionSyntax expression = GetSetAccessorExpression(accessor);

                        if (expression?.IsKind(SyntaxKind.SimpleAssignmentExpression) == true)
                        {
                            var assignment = (AssignmentExpressionSyntax)expression;

                            ExpressionSyntax left = assignment.Left;

                            if (left?.IsKind(SyntaxKind.ElementAccessExpression) == true)
                            {
                                var elementAccess = (ElementAccessExpressionSyntax)left;

                                if (elementAccess.Expression?.IsKind(SyntaxKind.BaseExpression) == true
                                    && elementAccess.ArgumentList != null)
                                {
                                    ExpressionSyntax right = assignment.Right;

                                    if (right?.IsKind(SyntaxKind.IdentifierName) == true)
                                    {
                                        var identifierName = (IdentifierNameSyntax)right;

                                        if (identifierName.Identifier.ValueText == "value")
                                        {
                                            IPropertySymbol propertySymbol = semanticModel.GetDeclaredSymbol(indexerDeclaration, cancellationToken);

                                            if (propertySymbol != null)
                                            {
                                                IPropertySymbol overridenProperty = propertySymbol.OverriddenProperty;

                                                if (overridenProperty != null)
                                                {
                                                    ISymbol symbol = semanticModel.GetSymbol(elementAccess, cancellationToken);

                                                    if (overridenProperty.Equals(symbol))
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
                case SyntaxKind.UnknownAccessorDeclaration:
                    {
                        return false;
                    }
                default:
                    {
                        Debug.Assert(false, accessor.Kind().ToString());
                        return false;
                    }
            }
        }

        private static ExpressionSyntax GetGetAccessorExpression(AccessorDeclarationSyntax accessor)
        {
            BlockSyntax body = accessor.Body;

            if (body != null)
            {
                StatementSyntax statement = body.SingleStatementOrDefault();

                if (statement?.IsKind(SyntaxKind.ReturnStatement) == true)
                    return ((ReturnStatementSyntax)statement).Expression;
            }
            else
            {
                return accessor.ExpressionBody?.Expression;
            }

            return null;
        }

        private static ExpressionSyntax GetSetAccessorExpression(AccessorDeclarationSyntax accessor)
        {
            BlockSyntax body = accessor.Body;

            if (body != null)
            {
                StatementSyntax statement = body.SingleStatementOrDefault();

                if (statement?.IsKind(SyntaxKind.ExpressionStatement) == true)
                    return ((ExpressionStatementSyntax)statement).Expression;
            }
            else
            {
                return accessor.ExpressionBody?.Expression;
            }

            return null;
        }
    }
}
