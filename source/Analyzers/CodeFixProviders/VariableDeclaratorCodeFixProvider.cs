﻿// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Roslynator.CSharp.Refactorings;

namespace Roslynator.CSharp.CodeFixProviders
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(VariableDeclaratorCodeFixProvider))]
    [Shared]
    public class VariableDeclaratorCodeFixProvider : BaseCodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(DiagnosticIdentifiers.MergeLocalDeclarationWithInitialization); }
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            SyntaxNode root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            VariableDeclaratorSyntax declarator = root
                .FindNode(context.Span, getInnermostNodeForTie: true)?
                .FirstAncestorOrSelf<VariableDeclaratorSyntax>();

            Debug.Assert(declarator != null, $"{nameof(declarator)} is null");

            CodeAction codeAction = CodeAction.Create(
                "Merge local declaration with initialization",
                cancellationToken => MergeLocalDeclarationWithInitializationRefactoring.RefactorAsync(context.Document, declarator, cancellationToken),
                DiagnosticIdentifiers.MergeLocalDeclarationWithInitialization + EquivalenceKeySuffix);

            context.RegisterCodeFix(codeAction, context.Diagnostics);
        }
    }
}
