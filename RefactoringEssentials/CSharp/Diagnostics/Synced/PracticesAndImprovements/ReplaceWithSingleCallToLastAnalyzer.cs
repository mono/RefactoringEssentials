using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace RefactoringEssentials.CSharp.Diagnostics
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ReplaceWithSingleCallToLastAnalyzer : DiagnosticAnalyzer
    {
        static readonly DiagnosticDescriptor descriptor = new DiagnosticDescriptor(
            CSharpDiagnosticIDs.ReplaceWithSingleCallToLastAnalyzerID,
            GettextCatalog.GetString("Redundant Where() call with predicate followed by Last()"),
            GettextCatalog.GetString("Replace with single call to 'Last()'"),
            DiagnosticAnalyzerCategories.PracticesAndImprovements,
            DiagnosticSeverity.Info,
            isEnabledByDefault: true,
            helpLinkUri: HelpLink.CreateFor(CSharpDiagnosticIDs.ReplaceWithSingleCallToLastAnalyzerID)
        );

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(descriptor);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterOperationAction(
                (nodeContext) =>
                {
                    Diagnostic diagnostic;
                    if (TryGetDiagnostic(nodeContext, out diagnostic))
                        nodeContext.ReportDiagnostic(diagnostic);
                },
                OperationKind.Invocation
            );
        }

        static bool TryGetDiagnostic(OperationAnalysisContext nodeContext, out Diagnostic diagnostic)
        {
            diagnostic = default(Diagnostic);
            var anyInvoke = (IInvocationOperation)nodeContext.Operation;

			var method = anyInvoke.TargetMethod;
            if (!HasPredicateVersion(method))
                return false;

			if (anyInvoke.Arguments.Length != 1)
				return false;

			var possibleWhere = anyInvoke.Arguments[0];
			if (!(possibleWhere.Value is IInvocationOperation whereInvocation))
				return false;

			var whereMethod = whereInvocation.TargetMethod;
			if (whereMethod.Name != "Where" || !ReplaceWithSingleCallToAnyAnalyzer.IsQueryExtensionClass(whereMethod.ContainingType))
				return false;

			if (whereInvocation.Arguments.Length != 2)
				return false;

			var predicate = whereInvocation.Arguments[1].Parameter.Type;
            if (predicate.GetTypeParameters ().Length != 2)
                return false;
            diagnostic = Diagnostic.Create(
                descriptor,
                anyInvoke.Syntax.GetLocation()
            );
            return true;
        }

        static bool HasPredicateVersion(IMethodSymbol member)
        {
            if (!ReplaceWithSingleCallToAnyAnalyzer.IsQueryExtensionClass(member.ContainingType))
                return false;
            return member.Name == "Last";
        }
    }
}