using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using static Kdletters.Analyzer.AnalyzerHelper;

namespace Kdletters.Analyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class EventDispatchAnalyzer : DiagnosticAnalyzer
    {
        private const string Dispatch = "Dispatch";
        private const string EventFlag = "KEventFlag";

        private static readonly string ArgumentTitle = "调用事件错误,请正确调用";
        private static readonly string ArgumentDescription = "调用事件错误.";
        private static readonly string ArgumentMessageFormat = $"{ArgumentDescription}{{0}}";
        private static readonly DiagnosticDescriptor ArgumentRule = new DiagnosticDescriptor("EventListenerArgutemt", ArgumentTitle, ArgumentMessageFormat, Category, DiagnosticSeverity.Error, true, ArgumentDescription);
        private const string Category = nameof(EventDispatchAnalyzer);
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(ArgumentRule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSyntaxNodeAction(AnalyzeSyntax, SyntaxKind.InvocationExpression);
        }

        private void AnalyzeSyntax(SyntaxNodeAnalysisContext context)
        {
            var invocationExpressionSyntax = (InvocationExpressionSyntax) context.Node;
            //获取函数名
            if (invocationExpressionSyntax.ChildNodes().FirstOrDefault() is not GenericNameSyntax genericArgument || genericArgument.Identifier.ToString() != Dispatch) return;

            var ga = genericArgument.TypeArgumentList.Arguments.First();
            if (context.Compilation.GetSymbolsWithName(ga.ChildNodes().ToArray()[1].ToString()).FirstOrDefault() is INamedTypeSymbol genericArgumentNamedTypeSymbol && genericArgumentNamedTypeSymbol.ContainingType.Name == EventFlag)
            {
                //获取参数
                var arguments            = invocationExpressionSyntax.ArgumentList.Arguments;
                var declareParameterList = genericArgumentNamedTypeSymbol.DelegateInvokeMethod!.Parameters;
                var pass                 = true;
                var addition             = "";
                if (arguments.Count == declareParameterList.Length)
                {
                    for (var i = 0; i < arguments.Count; i++)
                    {
                        var argumentType = context.SemanticModel.GetTypeInfo(arguments[i].Expression).Type;
                        if (argumentType!.ToString() != declareParameterList[i].ToString() && argumentType is INamedTypeSymbol namedArgumentTypeSymbol && !namedArgumentTypeSymbol.InheritsFrom(declareParameterList[i].Type))
                        {
                            pass     = false;
                            addition = $"第{i + 1}个参数不同，当前：{argumentType}，事件：{declareParameterList[i]}";
                            break;
                        }
                    }
                }
                else
                {
                    pass     = false;
                    addition = $"参数数量和委托不一致";
                }

                if (pass) return;
                Report(context, ArgumentRule, invocationExpressionSyntax.ArgumentList.GetLocation(), addition);
            }
        }

        private static void Report(SyntaxNodeAnalysisContext context, DiagnosticDescriptor descriptor, Location? location, params object?[]? messageArgs)
        {
            var diagnostic = Diagnostic.Create(descriptor, location, messageArgs);
            context.ReportDiagnostic(diagnostic);
        }
    }
}