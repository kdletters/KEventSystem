using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis.Text;

namespace Kdletters.Analyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class EventListenerAnalyzer : DiagnosticAnalyzer
    {
        private const string KEventListenerAttribute = "Kdletters.EventSystem.KEventListenerAttribute";
        private const string KEventFlag = "KEventFlag";

        // You can change these strings in the Resources.resx file. If you do not want your analyzer to be localize-able, you can use regular strings for Title and MessageFormat.
        // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/Localizing%20Analyzers.md for more on localization
        private static readonly string ArgumentTitle = "请在对应位置添加事件监听类型";
        private static readonly string ArgumentDescription = "事件监听类型错误.";
        private static readonly string ArgumentMessageFormat = $"{ArgumentDescription}方法:'{{0}}': 事件监听类型'{{1}}'必须位于'{{2}}'类下，且必须为'Delegate'";
        private static readonly DiagnosticDescriptor ArgumentRule = new DiagnosticDescriptor("EventListenerArgutemt", ArgumentTitle, ArgumentMessageFormat, Category, DiagnosticSeverity.Error, true, ArgumentDescription);

        private static readonly string ParameterTitle = "事件监听方法的参数必须与事件监听类型一致";
        private static readonly string ParameterDescription = "事件监听方法的参数错误.";
        private static readonly string ParameterMessageFormat = $"{ParameterDescription}方法:'{{0}}': 事件监听类型'{{1}}',{{2}}";
        private const string Category = nameof(EventListenerAnalyzer);
        private static readonly DiagnosticDescriptor ParameterRule = new DiagnosticDescriptor("EventListenerParameter", ParameterTitle, ParameterMessageFormat, Category, DiagnosticSeverity.Error, true, ParameterDescription);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(ArgumentRule, ParameterRule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.Method);
        }

        private static void AnalyzeSymbol(SymbolAnalysisContext context)
        {
            var methodSymbol   = (IMethodSymbol) context.Symbol;
            var delegateSymbol = context.Compilation.GetSpecialType(SpecialType.System_Delegate);
            //找到EventListenerAttribute
            var attributeSymbol = methodSymbol?.GetAttributes().FirstOrDefault(x => x.AttributeClass?.ToString() == KEventListenerAttribute);
            if (attributeSymbol is null) return;

            //找到传入的参数
            var namedTypeSymbol = attributeSymbol.ConstructorArguments[0].Value as INamedTypeSymbol;
            //判断Attribute类型是否正确
            if (namedTypeSymbol?.ContainingType?.Name != KEventFlag || !namedTypeSymbol.InheritsFrom(delegateSymbol))
            {
                var syntaxNode = attributeSymbol!.ApplicationSyntaxReference!.GetSyntax();
                syntaxNode = syntaxNode.ChildNodes().ToArray()[1];
                var sourceSpan = syntaxNode.GetLocation().SourceSpan;
                var location   = Location.Create(syntaxNode.SyntaxTree, TextSpan.FromBounds(sourceSpan.Start + 1, sourceSpan.End - 1));
                Report(context, ArgumentRule, location, methodSymbol, namedTypeSymbol, KEventFlag);
            }
            else
            {
                //获取方法参数
                var methodParameterList    = methodSymbol!.Parameters;
                var declareParameterList   = namedTypeSymbol.DelegateInvokeMethod!.Parameters;
                var methodReturnParameter  = methodSymbol.ReturnType;
                var declareReturnParameter = namedTypeSymbol.DelegateInvokeMethod.ReturnType;

                var methodSyntax = methodSymbol.DeclaringSyntaxReferences.First().GetSyntax() as MethodDeclarationSyntax;

                var pass     = true;
                var addition = "";
                if (methodReturnParameter.ToString() == declareReturnParameter.ToString())
                {
                    if (methodSyntax!.Modifiers.Any(SyntaxKind.StaticKeyword))
                    {
                        if (methodParameterList.Length == declareParameterList.Length)
                        {
                            for (int i = 0; i < methodParameterList.Length; i++)
                            {
                                if (methodParameterList[i].ToString() != declareParameterList[i].ToString())
                                {
                                    pass     = false;
                                    addition = $"静态函数，第{i + 1}个参数不同，当前：{methodParameterList[i]}，事件：{declareParameterList[i]}";
                                    break;
                                }
                            }
                        }
                        else
                        {
                            pass     = false;
                            addition = $"静态函数，参数数量不同，当前：{methodParameterList.Length}，事件：{declareParameterList.Length}";
                        }
                    }
                    else
                    {
                        if (declareParameterList.Length != 0)
                        {
                            if (methodParameterList.Length + 1 == declareParameterList.Length)
                            {
                                if (methodSymbol.ContainingType.ToString() == declareParameterList[0].Type.ToString())
                                {
                                    for (int i = 0; i < methodParameterList.Length; i++)
                                    {
                                        if (methodParameterList[i].Type.ToString() != declareParameterList[i + 1].Type.ToString())
                                        {
                                            pass     = false;
                                            addition = $"第{i + 1}个参数不同，当前：{methodParameterList[i]}，事件：{declareParameterList[i]}";
                                            break;
                                        }
                                    }
                                }
                                else
                                {
                                    pass     = false;
                                    addition = $"实例函数的委托第一个参数必须是自身，当前：{methodSymbol.ContainingType}，事件：{declareParameterList[0].Type}";
                                }
                            }
                            else
                            {
                                pass     = false;
                                addition = $"参数数量不同，当前：{methodParameterList.Length}，事件：{declareParameterList.Length - 1}";
                            }
                        }
                        else
                        {
                            pass     = false;
                            addition = $"实例函数的委托第一个参数必须是自身，当前：{methodSymbol.ContainingType}，事件：{declareParameterList[0].Type}";
                        }
                    }
                }
                else
                {
                    pass     = false;
                    addition = $"返回值类型不同，当前：{methodReturnParameter}，事件：{declareReturnParameter}";
                }

                if (pass) return;
                Report(context, ParameterRule, methodSyntax!.ParameterList.GetLocation(), methodSymbol, namedTypeSymbol, addition);
            }
        }

        private static void Report(SymbolAnalysisContext context, DiagnosticDescriptor descriptor, Location? location, params object?[]? messageArgs)
        {
            var diagnostic = Diagnostic.Create(descriptor, location, messageArgs);
            context.ReportDiagnostic(diagnostic);
        }
    }
}