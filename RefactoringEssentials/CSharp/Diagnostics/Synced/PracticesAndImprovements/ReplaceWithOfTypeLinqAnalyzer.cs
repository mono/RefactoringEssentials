using System;
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
    public class ReplaceWithOfTypeLinqAnalyzer : DiagnosticAnalyzer
    {
		static readonly string[] replaceableMethods =
		{
			"Any",
			"Count",
			"First",
			"FirstOrDefault",
			"Last",
			"LastOrDefault",
			"LongCount",
			"Single",
			"SingleOrDefault",
			"Where"
		};

        static readonly DiagnosticDescriptor descriptor = new DiagnosticDescriptor(
            CSharpDiagnosticIDs.ReplaceWithOfTypeLinqAnalyzerID,
            GettextCatalog.GetString("Replace with call to OfType<T>().{0}()"),
            GettextCatalog.GetString("Replace with 'OfType<T>().{0}()'"),
            DiagnosticAnalyzerCategories.PracticesAndImprovements,
            DiagnosticSeverity.Info,
            isEnabledByDefault: true,
            helpLinkUri: HelpLink.CreateFor(CSharpDiagnosticIDs.ReplaceWithOfTypeLinqAnalyzerID)
        );

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(descriptor);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
			context.RegisterCompilationStartAction(compilationContext =>
			{
				var compilation = compilationContext.Compilation;

				var enumerableType = compilation.GetTypeByMetadataName("System.Linq.Enumerable");
				var queryableType = compilation.GetTypeByMetadataName("System.Linq.Queryable");
				var parallelEnumerableType = compilation.GetTypeByMetadataName("System.Linq.ParallelEnumerable");
				if (enumerableType == null || queryableType == null || parallelEnumerableType == null)
					return;

				compilationContext.RegisterOperationAction(nodeContext =>
				{
					var anyInvoke = (IInvocationOperation)nodeContext.Operation;
					var method = anyInvoke.TargetMethod;
					if (!IsLinqExtension(method) || !IsPredicateMethod(method))
						return;

					if (anyInvoke.Arguments.Length != 2 || !(anyInvoke.Arguments[0].Value is IInvocationOperation selectInvoke))
						return;

					var selectMethod = selectInvoke.TargetMethod;
					if (!IsLinqExtension(selectMethod) || selectMethod.Name != "Select")
						return;

					if (!(anyInvoke.Arguments[1].Value is IDelegateCreationOperation anyDelegateCreation))
						return;

					if (!(anyDelegateCreation.Target is IAnonymousFunctionOperation anyLambda))
						return;

					var anyBlock = anyLambda.Body.Operations;
					if (anyBlock.Length != 1)
						return;

					if (!(anyBlock[0] is IReturnOperation anyRet) || !(anyRet.ReturnedValue is IBinaryOperation anyCheck))
						return;

					if (!IsAnyAParameterNullCheck(anyCheck, anyLambda.Symbol.Parameters[0]))
						return;

					// check that anyInvoke does a nullcheck

					if (selectInvoke.Arguments.Length != 2 || !(selectInvoke.Arguments[1].Value is IDelegateCreationOperation selectDelegateCreation))
						return;

					if (!(selectDelegateCreation.Target is IAnonymousFunctionOperation selectLambda))
						return;

					// Check the select does as cast
					var selectBlock = selectLambda.Body.Operations;
					if (selectBlock.Length != 1)
						return;

					if (!(selectBlock[0] is IReturnOperation selectRet) || !(selectRet.ReturnedValue is IConversionOperation selectConversion) || !selectConversion.IsTryCast)
						return;

					if (!(selectConversion.Operand is IParameterReferenceOperation parameterReference) || parameterReference.Parameter != selectLambda.Symbol.Parameters[0])
						return;

					nodeContext.ReportDiagnostic(
						Diagnostic.Create(
							descriptor,
							anyInvoke.Syntax.GetLocation(),
							method.Name
						)
					);
				},
					OperationKind.Invocation
				);

				bool IsAnyAParameterNullCheck (IOperation operation, IParameterSymbol parameter)
				{
					if (!(operation is IBinaryOperation binaryOperation))
						return false;

					if (binaryOperation.OperatorKind == BinaryOperatorKind.ConditionalAnd)
						return IsAnyAParameterNullCheck(binaryOperation.LeftOperand, parameter) || IsAnyAParameterNullCheck(binaryOperation.RightOperand, parameter);

					return binaryOperation.OperatorKind == BinaryOperatorKind.NotEquals &&
						(IsParameterReference(binaryOperation.LeftOperand, parameter) || IsParameterReference(binaryOperation.RightOperand, parameter));
				}

				bool IsParameterReference (IOperation operation, IParameterSymbol symbol)
				{
					if (operation is IConversionOperation conversion)
						operation = conversion.Operand;

					return operation is IParameterReferenceOperation parameterReference && parameterReference.Parameter == symbol;
				}

				bool IsLinqExtension(IMethodSymbol symbol)
				{
					var methodType = symbol.ContainingType;
					return methodType == enumerableType || methodType == queryableType || methodType == parallelEnumerableType;
				}

				bool IsPredicateMethod(IMethodSymbol member)
				{
					return Array.IndexOf(replaceableMethods, member.Name) >= 0;
				}
			});
        }
	}
}