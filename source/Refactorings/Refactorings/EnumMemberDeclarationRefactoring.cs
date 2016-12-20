﻿// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Roslynator.CSharp.Refactorings
{
    internal static class EnumMemberDeclarationRefactoring
    {
        public static async Task ComputeRefactoringAsync(RefactoringContext context, EnumMemberDeclarationSyntax enumMemberDeclaration)
        {
            if (context.IsRefactoringEnabled(RefactoringIdentifiers.GenerateEnumValues)
                && enumMemberDeclaration.Span.Contains(context.Span))
            {
                SyntaxNode parent = enumMemberDeclaration.Parent;

                if (parent?.IsKind(SyntaxKind.EnumDeclaration) == true)
                    await GenerateEnumValuesRefactoring.ComputeRefactoringAsync(context, (EnumDeclarationSyntax)parent).ConfigureAwait(false);
            }
       }
    }
}