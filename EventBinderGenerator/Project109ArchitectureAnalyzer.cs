using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace EventBinderGenerator
{
    /// <summary>
    /// Project109 컴파일 타임 아키텍처·성능 규칙 Roslyn DiagnosticAnalyzer입니다.
    /// - P109001: UnityEngine.Object 파생 타입의 Fake Null 연산자(?. / ?[] / ?? / ??=) 사용 차단
    /// - P109002: 클로저 할당을 유발하는 EventBus&lt;T&gt;.Invoke 호출 차단 (정적 Dispatcher 사용 강제)
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class Project109ArchitectureAnalyzer : DiagnosticAnalyzer
    {
        public const string FakeNullDiagnosticId = "P109001";
        public const string EventBusInvokeDiagnosticId = "P109002";

        private static readonly DiagnosticDescriptor FakeNullRule = new DiagnosticDescriptor(
            id: FakeNullDiagnosticId,
            title: "UnityEngine.Object Fake Null 연산자 사용 금지",
            messageFormat: "UnityEngine.Object 파생 타입 '{0}'에 '{1}' 연산자를 사용할 수 없습니다. 명시적인 '== null' 또는 '!= null' 검사를 사용하세요.",
            category: "Project109.Architecture",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "UnityEngine.Object 파생 객체는 파괴 후에도 C# 참조가 null이 아닐 수 있어 '?.'/'?[]'/'??'/'??=' 연산자가 수명 검사를 우회합니다.");

        private static readonly DiagnosticDescriptor EventBusInvokeRule = new DiagnosticDescriptor(
            id: EventBusInvokeDiagnosticId,
            title: "EventBus.Invoke 직접 호출 금지",
            messageFormat: "'{0}' 호출은 클로저 힙 할당을 유발합니다. Roslyn SG가 생성한 정적 Dispatcher(CombatEventDispatcher / PlayerEventDispatcher)를 사용하세요.",
            category: "Project109.Performance",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "핫패스 GC 방지를 위해 EventBus<T>.Invoke 대신 무할당 정적 Dispatcher 메서드를 사용해야 합니다.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(FakeNullRule, EventBusInvokeRule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterCompilationStartAction(compilationContext =>
            {
                string? assemblyName = compilationContext.Compilation.AssemblyName;
                if (!string.Equals(assemblyName, "Assembly-CSharp", StringComparison.Ordinal) &&
                    !string.Equals(assemblyName, "Assembly-CSharp-Editor", StringComparison.Ordinal))
                {
                    return;
                }

                INamedTypeSymbol? unityObjectSymbol = compilationContext.Compilation.GetTypeByMetadataName("UnityEngine.Object");
                INamedTypeSymbol? eventBusSymbol = compilationContext.Compilation.GetTypeByMetadataName("EventBus`1");

                if (unityObjectSymbol != null)
                {
                    compilationContext.RegisterSyntaxNodeAction(
                        ctx => AnalyzeConditionalAccess(ctx, unityObjectSymbol),
                        SyntaxKind.ConditionalAccessExpression);

                    compilationContext.RegisterSyntaxNodeAction(
                        ctx => AnalyzeCoalesceExpression(ctx, unityObjectSymbol),
                        SyntaxKind.CoalesceExpression);

                    compilationContext.RegisterSyntaxNodeAction(
                        ctx => AnalyzeCoalesceAssignment(ctx, unityObjectSymbol),
                        SyntaxKind.CoalesceAssignmentExpression);
                }

                if (eventBusSymbol != null)
                {
                    compilationContext.RegisterSyntaxNodeAction(
                        ctx => AnalyzeInvocation(ctx, eventBusSymbol),
                        SyntaxKind.InvocationExpression);
                }
            });
        }

        private static void AnalyzeConditionalAccess(SyntaxNodeAnalysisContext context, INamedTypeSymbol unityObjectSymbol)
        {
            if (IsExcludedSourcePath(context.Node.SyntaxTree.FilePath))
            {
                return;
            }

            var conditionalAccess = (ConditionalAccessExpressionSyntax)context.Node;
            ITypeSymbol? targetType = context.SemanticModel.GetTypeInfo(conditionalAccess.Expression, context.CancellationToken).Type;
            if (!DerivesFromUnityObject(targetType, unityObjectSymbol))
            {
                return;
            }

            string op = conditionalAccess.WhenNotNull is ElementBindingExpressionSyntax ? "?[]" : "?.";
            string typeName = targetType!.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            context.ReportDiagnostic(Diagnostic.Create(
                FakeNullRule,
                conditionalAccess.OperatorToken.GetLocation(),
                typeName,
                op));
        }

        private static void AnalyzeCoalesceExpression(SyntaxNodeAnalysisContext context, INamedTypeSymbol unityObjectSymbol)
        {
            if (IsExcludedSourcePath(context.Node.SyntaxTree.FilePath))
            {
                return;
            }

            var binaryExpr = (BinaryExpressionSyntax)context.Node;
            ITypeSymbol? leftType = context.SemanticModel.GetTypeInfo(binaryExpr.Left, context.CancellationToken).Type;
            if (!DerivesFromUnityObject(leftType, unityObjectSymbol))
            {
                return;
            }

            string typeName = leftType!.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            context.ReportDiagnostic(Diagnostic.Create(
                FakeNullRule,
                binaryExpr.OperatorToken.GetLocation(),
                typeName,
                "??"));
        }

        private static void AnalyzeCoalesceAssignment(SyntaxNodeAnalysisContext context, INamedTypeSymbol unityObjectSymbol)
        {
            if (IsExcludedSourcePath(context.Node.SyntaxTree.FilePath))
            {
                return;
            }

            var assignExpr = (AssignmentExpressionSyntax)context.Node;
            ITypeSymbol? leftType = context.SemanticModel.GetTypeInfo(assignExpr.Left, context.CancellationToken).Type;
            if (!DerivesFromUnityObject(leftType, unityObjectSymbol))
            {
                return;
            }

            string typeName = leftType!.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            context.ReportDiagnostic(Diagnostic.Create(
                FakeNullRule,
                assignExpr.OperatorToken.GetLocation(),
                typeName,
                "??="));
        }

        private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol eventBusSymbol)
        {
            string filePath = context.Node.SyntaxTree.FilePath;
            if (IsExcludedSourcePath(filePath) ||
                filePath.EndsWith("EventBus.cs", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var invocation = (InvocationExpressionSyntax)context.Node;
            if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol methodSymbol)
            {
                return;
            }

            if (!string.Equals(methodSymbol.Name, "Invoke", StringComparison.Ordinal))
            {
                return;
            }

            INamedTypeSymbol? containingType = methodSymbol.ContainingType;
            if (containingType == null ||
                !SymbolEqualityComparer.Default.Equals(containingType.OriginalDefinition, eventBusSymbol))
            {
                return;
            }

            string display = $"{containingType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}.{methodSymbol.Name}";
            context.ReportDiagnostic(Diagnostic.Create(
                EventBusInvokeRule,
                invocation.GetLocation(),
                display));
        }

        private static bool DerivesFromUnityObject(ITypeSymbol? type, INamedTypeSymbol unityObjectSymbol)
        {
            if (type == null)
            {
                return false;
            }

            if (type is ITypeParameterSymbol typeParam)
            {
                foreach (ITypeSymbol constraint in typeParam.ConstraintTypes)
                {
                    if (DerivesFromUnityObject(constraint, unityObjectSymbol))
                    {
                        return true;
                    }
                }
                return false;
            }

            ITypeSymbol? current = type;
            while (current != null)
            {
                if (SymbolEqualityComparer.Default.Equals(current, unityObjectSymbol))
                {
                    return true;
                }
                current = current.BaseType;
            }

            return false;
        }

        private static bool IsExcludedSourcePath(string? filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return false;
            }

            string normalized = filePath!.Replace('\\', '/');
            return normalized.IndexOf("/XLua/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   normalized.IndexOf("/YamlDotNet/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   normalized.IndexOf("/TextMesh Pro/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   normalized.IndexOf("/Plugins/", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
