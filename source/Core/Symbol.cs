﻿// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Roslynator.Extensions;

namespace Roslynator
{
    public static class Symbol
    {
        public static bool ContainsPublicIndexerWithInt32Parameter(ITypeSymbol typeSymbol)
        {
            if (typeSymbol == null)
                throw new ArgumentNullException(nameof(typeSymbol));

            foreach (ISymbol symbol in typeSymbol.GetMembers("get_Item"))
            {
                if (symbol.IsPublicInstanceMethod())
                {
                    var methodSymbol = (IMethodSymbol)symbol;

                    if (methodSymbol.SingleParameterOrDefault()?.Type.IsInt32() == true)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static bool IsEventHandlerOrConstructedFromEventHandlerOfT(
            ITypeSymbol typeSymbol,
            SemanticModel semanticModel)
        {
            if (typeSymbol == null)
                throw new ArgumentNullException(nameof(typeSymbol));

            if (semanticModel == null)
                throw new ArgumentNullException(nameof(semanticModel));

            return typeSymbol.Equals(semanticModel.GetTypeByMetadataName(MetadataNames.System_EventHandler))
                || typeSymbol.IsConstructedFrom(semanticModel.GetTypeByMetadataName(MetadataNames.System_EventHandler_T));
        }

        public static bool IsException(ITypeSymbol typeSymbol, SemanticModel semanticModel)
        {
            if (typeSymbol == null)
                throw new ArgumentNullException(nameof(typeSymbol));

            if (semanticModel == null)
                throw new ArgumentNullException(nameof(semanticModel));

            return typeSymbol.IsClass()
                && typeSymbol.EqualsOrDerivedFrom(semanticModel.GetTypeByMetadataName(MetadataNames.System_Exception));
        }

        public static bool IsEnumerableMethod(
            IMethodSymbol methodSymbol,
            string methodName,
            SemanticModel semanticModel)
        {
            if (methodSymbol == null)
                throw new ArgumentNullException(nameof(methodSymbol));

            if (semanticModel == null)
                throw new ArgumentNullException(nameof(semanticModel));

            return methodSymbol.IsExtensionMethod
                && methodSymbol.Name == methodName
                && IsEnumerableMethod(methodSymbol, semanticModel);
        }

        public static bool IsImmutableArrayExtensionMethod(
            IMethodSymbol methodSymbol,
            string methodName,
            SemanticModel semanticModel)
        {
            if (methodSymbol == null)
                throw new ArgumentNullException(nameof(methodSymbol));

            if (semanticModel == null)
                throw new ArgumentNullException(nameof(semanticModel));

            return methodSymbol.IsExtensionMethod
                && methodSymbol.Name == methodName
                && IsImmutableArrayExtensionMethod(methodSymbol, semanticModel);
        }

        public static bool IsEnumerableOrImmutableArrayExtensionMethod(
            IMethodSymbol methodSymbol,
            string methodName,
            SemanticModel semanticModel)
        {
            if (methodSymbol == null)
                throw new ArgumentNullException(nameof(methodSymbol));

            if (semanticModel == null)
                throw new ArgumentNullException(nameof(semanticModel));

            return methodSymbol.IsExtensionMethod
                && methodSymbol.Name == methodName
                && (IsEnumerableMethod(methodSymbol, semanticModel) || IsImmutableArrayExtensionMethod(methodSymbol, semanticModel));
        }

        private static bool IsEnumerableMethod(IMethodSymbol methodSymbol, SemanticModel semanticModel)
        {
            if (IsContainingType(methodSymbol, MetadataNames.System_Linq_Enumerable, semanticModel))
            {
                IMethodSymbol reducedFrom = methodSymbol.ReducedFrom;

                IParameterSymbol parameter = (reducedFrom != null)
                    ? reducedFrom.Parameters.First()
                    : methodSymbol.Parameters.First();

                return parameter.Type.IsConstructedFromIEnumerableOfT();
            }
            else
            {
                return false;
            }
        }

        private static bool IsImmutableArrayExtensionMethod(IMethodSymbol methodSymbol, SemanticModel semanticModel)
        {
            if (IsContainingType(methodSymbol, MetadataNames.System_Linq_ImmutableArrayExtensions, semanticModel))
            {
                IMethodSymbol reducedFrom = methodSymbol.ReducedFrom;

                IParameterSymbol parameter = (reducedFrom != null)
                    ? reducedFrom.Parameters.First()
                    : methodSymbol.Parameters.First();

                return SymbolExtensions.IsConstructedFromImmutableArrayOfT(parameter.Type, semanticModel);
            }
            else
            {
                return false;
            }
        }

        public static bool IsEnumerableMethodWithoutParameters(IMethodSymbol methodSymbol, string methodName, SemanticModel semanticModel)
        {
            return IsEnumerableMethod(methodSymbol, methodName, semanticModel)
                && !methodSymbol.Parameters.Any();
        }

        public static bool IsEnumerableMethodWithPredicate(IMethodSymbol methodSymbol, string methodName, SemanticModel semanticModel)
        {
            if (IsEnumerableMethod(methodSymbol, methodName, semanticModel))
            {
                IParameterSymbol parameter = methodSymbol.SingleParameterOrDefault();

                return parameter != null
                    && IsPredicateFunc(parameter.Type, methodSymbol.TypeArguments[0], semanticModel);
            }
            else
            {
                return false;
            }
        }

        public static bool IsFunc(ISymbol symbol, ITypeSymbol parameter1, ITypeSymbol parameter2, SemanticModel semanticModel)
        {
            if (symbol == null)
                throw new ArgumentNullException(nameof(symbol));

            if (parameter1 == null)
                throw new ArgumentNullException(nameof(parameter1));

            if (parameter2 == null)
                throw new ArgumentNullException(nameof(parameter2));

            if (semanticModel == null)
                throw new ArgumentNullException(nameof(semanticModel));

            if (symbol.IsNamedType())
            {
                INamedTypeSymbol funcSymbol = semanticModel.GetTypeByMetadataName(MetadataNames.System_Func_T2);

                var namedTypeSymbol = (INamedTypeSymbol)symbol;

                if (namedTypeSymbol.ConstructedFrom.Equals(funcSymbol))
                {
                    ImmutableArray<ITypeSymbol> typeArguments = namedTypeSymbol.TypeArguments;

                    return typeArguments.Length == 2
                        && typeArguments[0].Equals(parameter1)
                        && typeArguments[1].Equals(parameter2);
                }
            }

            return false;
        }

        public static bool IsFunc(ISymbol symbol, ITypeSymbol parameter1, ITypeSymbol parameter2, ITypeSymbol parameter3, SemanticModel semanticModel)
        {
            if (symbol == null)
                throw new ArgumentNullException(nameof(symbol));

            if (parameter1 == null)
                throw new ArgumentNullException(nameof(parameter1));

            if (parameter2 == null)
                throw new ArgumentNullException(nameof(parameter2));

            if (semanticModel == null)
                throw new ArgumentNullException(nameof(semanticModel));

            if (symbol.IsNamedType())
            {
                INamedTypeSymbol funcSymbol = semanticModel.GetTypeByMetadataName(MetadataNames.System_Func_T3);

                var namedTypeSymbol = (INamedTypeSymbol)symbol;

                if (namedTypeSymbol.ConstructedFrom.Equals(funcSymbol))
                {
                    ImmutableArray<ITypeSymbol> typeArguments = namedTypeSymbol.TypeArguments;

                    return typeArguments.Length == 3
                        && typeArguments[0].Equals(parameter1)
                        && typeArguments[1].Equals(parameter2)
                        && typeArguments[2].Equals(parameter3);
                }
            }

            return false;
        }

        public static bool IsPredicateFunc(ISymbol symbol, ITypeSymbol parameter, SemanticModel semanticModel)
        {
            if (symbol == null)
                throw new ArgumentNullException(nameof(symbol));

            if (parameter == null)
                throw new ArgumentNullException(nameof(parameter));

            if (semanticModel == null)
                throw new ArgumentNullException(nameof(semanticModel));

            if (symbol.IsNamedType())
            {
                INamedTypeSymbol funcSymbol = semanticModel.GetTypeByMetadataName(MetadataNames.System_Func_T2);

                var namedTypeSymbol = (INamedTypeSymbol)symbol;

                if (namedTypeSymbol.ConstructedFrom.Equals(funcSymbol))
                {
                    ImmutableArray<ITypeSymbol> typeArguments = namedTypeSymbol.TypeArguments;

                    return typeArguments.Length == 2
                        && typeArguments[0].Equals(parameter)
                        && typeArguments[1].IsBoolean();
                }
            }

            return false;
        }

        public static bool IsPredicateFunc(ISymbol symbol, ITypeSymbol parameter1, ITypeSymbol parameter2, SemanticModel semanticModel)
        {
            if (symbol == null)
                throw new ArgumentNullException(nameof(symbol));

            if (parameter1 == null)
                throw new ArgumentNullException(nameof(parameter1));

            if (parameter2 == null)
                throw new ArgumentNullException(nameof(parameter2));

            if (semanticModel == null)
                throw new ArgumentNullException(nameof(semanticModel));

            if (symbol.IsNamedType())
            {
                INamedTypeSymbol funcSymbol = semanticModel.GetTypeByMetadataName(MetadataNames.System_Func_T3);

                var namedTypeSymbol = (INamedTypeSymbol)symbol;

                if (namedTypeSymbol.ConstructedFrom.Equals(funcSymbol))
                {
                    ImmutableArray<ITypeSymbol> typeArguments = namedTypeSymbol.TypeArguments;

                    return typeArguments.Length == 3
                        && typeArguments[0].Equals(parameter1)
                        && typeArguments[1].Equals(parameter2)
                        && typeArguments[2].IsBoolean();
                }
            }

            return false;
        }

        public static bool ImplementsINotifyPropertyChanged(ITypeSymbol typeSymbol, SemanticModel semanticModel)
        {
            if (typeSymbol == null)
                throw new ArgumentNullException(nameof(typeSymbol));

            if (semanticModel == null)
                throw new ArgumentNullException(nameof(semanticModel));

            if (typeSymbol != null)
            {
                INamedTypeSymbol notifyPropertyChanged = semanticModel.GetTypeByMetadataName(MetadataNames.System_ComponentModel_INotifyPropertyChanged);

                return notifyPropertyChanged != null
                    && typeSymbol.AllInterfaces.Contains(notifyPropertyChanged);
            }

            return false;
        }

        public static bool ImplementsICollectionOfT(ITypeSymbol symbol)
        {
            if (symbol == null)
                throw new ArgumentNullException(nameof(symbol));

            return symbol
                .AllInterfaces
                .Any(f => f.IsConstructedFrom(SpecialType.System_Collections_Generic_ICollection_T));
        }

        private static bool IsContainingType(ISymbol symbol, string fullyQualifiedMetadataName, SemanticModel semanticModel)
        {
            return symbol
                .ContainingType?
                .Equals(semanticModel.GetTypeByMetadataName(fullyQualifiedMetadataName)) == true;
        }
    }
}
