﻿// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Roslynator.CSharp.Refactorings.EnumWithFlagsAttribute;

namespace Roslynator.CSharp.Refactorings
{
    internal static class EnumDeclarationRefactoring
    {
        public static async Task ComputeRefactoringAsync(RefactoringContext context, EnumDeclarationSyntax enumDeclaration)
        {
            ExtractTypeDeclarationToNewFileRefactoring.ComputeRefactorings(context, enumDeclaration);

            if (context.IsRefactoringEnabled(RefactoringIdentifiers.GenerateCombinedEnumMember)
                && enumDeclaration.BracesSpan().Contains(context.Span))
            {
                await GenerateCombinedEnumMemberRefactoring.ComputeRefactoringAsync(context, enumDeclaration).ConfigureAwait(false);
            }

            if (context.IsRefactoringEnabled(RefactoringIdentifiers.GenerateEnumMember)
                && context.Span.IsEmpty
                && enumDeclaration.BracesSpan().Contains(context.Span))
            {
                await GenerateEnumMemberRefactoring.ComputeRefactoringAsync(context, enumDeclaration).ConfigureAwait(false);
            }
        }
    }
}