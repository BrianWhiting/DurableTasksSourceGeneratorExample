using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using System;

namespace SourceGenerators.AzureFunctions.DurableFunctions
{
    public enum DurableFunctionKind
    {
        Unknown,
        Orchestration,
        Activity
    }

    public class DurableFunction
    {
        public string Name { get; }
        public DurableFunctionKind Kind { get; }
        public string? ReturnType { get; }
        public TypedParameter? Parameter { get; }

        public DurableFunction(
            string name,
            DurableFunctionKind kind,
            ITypeSymbol? returnType,
            TypedParameter? parameter)
        {
            Name = name;
            Kind = kind;
            ReturnType = returnType != null ? SyntaxNodeUtility.GetRenderedTypeExpression(returnType, false) : null;
            Parameter = parameter;
        }

        public static bool TryParse(SemanticModel model, MethodDeclarationSyntax method, out DurableFunction? function)
        {
            if (!SyntaxNodeUtility.TryGetFunctionName(model, method, out string? name) || name == null)
            {
                function = null;
                return false;
            }

            if (!SyntaxNodeUtility.TryGetFunctionKind(method, out DurableFunctionKind kind))
            {
                function = null;
                return false;
            }

            if (!SyntaxNodeUtility.TryGetReturnType(method, out TypeSyntax returnType))
            {
                function = null;
                return false;
            }

            var returnSymbol = (INamedTypeSymbol)model.GetTypeInfo(returnType).Type!;
            var taskSymbol = model.Compilation.GetTypeByMetadataName("System.Threading.Tasks.Task")!;
            if (SymbolEqualityComparer.Default.Equals(returnSymbol.OriginalDefinition, taskSymbol))
            {
                returnSymbol = null;
            }
            else
            {
                var taskWithGenericSymbol = model.Compilation.GetTypeByMetadataName("System.Threading.Tasks.Task`1")!;
                if (SymbolEqualityComparer.Default.Equals(returnSymbol.OriginalDefinition, taskWithGenericSymbol))
                {
                    // this is a Task<T> return value, lets pull out the generic.
                    returnSymbol = (INamedTypeSymbol)returnSymbol.TypeArguments[0];
                }
            }

            if (!SyntaxNodeUtility.TryGetParameter(model, method, kind, out TypedParameter? parameter))
            {
                function = null;
                return false;
            }

            function = new DurableFunction(name, kind, returnSymbol, parameter);
            return true;
        }
        public class TypedParameter
        {
            public INamedTypeSymbol Type { get; }
            public string Name { get; }

            public TypedParameter(INamedTypeSymbol type, string name)
            {
                Type = type;
                Name = name;
            }

            public override string ToString()
            {
                return $"{SyntaxNodeUtility.GetRenderedTypeExpression(Type, false)} {Name}";
            }
        }

        private static class SyntaxNodeUtility
        {
            public static bool TryGetFunctionName(SemanticModel model, MethodDeclarationSyntax method, out string? functionName)
            {
                if (TryGetAttributeByName(method, "Function", out AttributeSyntax? functionNameAttribute) && functionNameAttribute != null)
                {
                    if (functionNameAttribute.ArgumentList?.Arguments.Count == 1)
                    {
                        ExpressionSyntax expression = functionNameAttribute.ArgumentList.Arguments.First().Expression;
                        Optional<object?> constant = model.GetConstantValue(expression);
                        if (!constant.HasValue)
                        {
                            functionName = null;
                            return false;
                        }

                        functionName = constant.ToString();
                        return true;
                    }
                }

                functionName = null;
                return false;
            }

            public static bool TryGetFunctionKind(MethodDeclarationSyntax method, out DurableFunctionKind kind)
            {
                SeparatedSyntaxList<ParameterSyntax> parameters = method.ParameterList.Parameters;

                foreach (ParameterSyntax parameterSyntax in parameters)
                {
                    IEnumerable<AttributeSyntax> parameterAttributes = parameterSyntax.AttributeLists
                        .SelectMany(a => a.Attributes);
                    foreach (AttributeSyntax attribute in parameterAttributes)
                    {
                        if (attribute.ToString().Equals("OrchestrationTrigger", StringComparison.Ordinal))
                        {
                            kind = DurableFunctionKind.Orchestration;
                            return true;
                        }

                        if (attribute.ToString().Equals("ActivityTrigger", StringComparison.Ordinal))
                        {
                            kind = DurableFunctionKind.Activity;
                            return true;
                        }
                    }
                }

                kind = DurableFunctionKind.Unknown;
                return false;
            }

            public static bool TryGetReturnType(MethodDeclarationSyntax method, out TypeSyntax returnTypeSyntax)
            {
                returnTypeSyntax = method.ReturnType;
                return true;
            }

            public static bool TryGetParameter(
                SemanticModel model,
                MethodDeclarationSyntax method,
                DurableFunctionKind kind,
                out TypedParameter? parameter)
            {
                var parameterIndex = default(int?);
                if (kind == DurableFunctionKind.Orchestration)
                {
                    parameterIndex = 1;
                }
                else if (kind == DurableFunctionKind.Activity)
                {
                    parameterIndex = 0;
                }

                if (parameterIndex.HasValue && method.ParameterList.Parameters.Count >= parameterIndex.Value + 1)
                {
                    var methodParameter = method.ParameterList.Parameters[parameterIndex.Value];
                    if (methodParameter.Type != null)
                    {
                        var info = model.GetTypeInfo(methodParameter.Type);
                        if (info.Type is INamedTypeSymbol named)
                        {
                            var functionContextSymbol = model.Compilation.GetTypeByMetadataName("Microsoft.Azure.Functions.Worker.FunctionContext")!;
                            if (SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, functionContextSymbol))
                            {
                                parameter = null;
                                return true;
                            }

                            parameter = new TypedParameter(named, methodParameter.Identifier.ToString());
                            return true;
                        }
                    }
                }

                parameter = null;
                return false;
            }

            public static string GetRenderedTypeExpression(ITypeSymbol? symbol, bool supportsNullable)
            {
                if (symbol == null)
                {
                    return supportsNullable ? "object?" : "object";
                }

                if (supportsNullable && symbol.IsReferenceType
                    && symbol.NullableAnnotation != NullableAnnotation.Annotated)
                {
                    symbol = symbol.WithNullableAnnotation(NullableAnnotation.Annotated);
                }

                string expression = symbol.ToString();
                if (expression.StartsWith("System.", StringComparison.Ordinal)
                    && symbol.ContainingNamespace.Name == "System")
                {
                    expression = expression.Substring("System.".Length);
                }

                return expression;
            }

            static bool TryGetAttributeByName(MethodDeclarationSyntax method, string attributeName, out AttributeSyntax? attribute)
            {
                attribute = method.AttributeLists
                    .SelectMany(a => a.Attributes)
                    .FirstOrDefault(a => a.Name.NormalizeWhitespace().ToFullString().Equals(attributeName, StringComparison.Ordinal));

                return attribute != null;
            }
        }
    }
}
