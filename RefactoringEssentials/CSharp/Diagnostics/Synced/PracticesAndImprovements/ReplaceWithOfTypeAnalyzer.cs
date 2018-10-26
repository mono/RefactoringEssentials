using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace RefactoringEssentials.CSharp.Diagnostics
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ReplaceWithOfTypeAnalyzer : DiagnosticAnalyzer
    {
        static readonly DiagnosticDescriptor descriptor = new DiagnosticDescriptor(
            CSharpDiagnosticIDs.ReplaceWithOfTypeAnalyzerID,
            GettextCatalog.GetString("Replace with call to OfType<T>"),
            GettextCatalog.GetString("Replace with 'OfType<T>()'"),
            DiagnosticAnalyzerCategories.PracticesAndImprovements,
            DiagnosticSeverity.Info,
            isEnabledByDefault: true,
            helpLinkUri: HelpLink.CreateFor(CSharpDiagnosticIDs.ReplaceWithOfTypeAnalyzerID)
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
					var selectInvoke = (IInvocationOperation)nodeContext.Operation;
					var selectMethod = selectInvoke.TargetMethod;
					if (!IsLinqExtension(selectMethod) || selectMethod.Name != "Select")
						return;

					if (selectInvoke.Arguments.Length != 2 || !(selectInvoke.Arguments[0].Value is IInvocationOperation whereInvoke))
						return;

					var whereMethod = whereInvoke.TargetMethod;
					if (!IsLinqExtension(whereMethod) || whereMethod.Name != "Where")
						return;

					if (!(selectInvoke.Arguments[1].Value is IDelegateCreationOperation selectDelegateCreation))
						return;

					if (!(selectDelegateCreation.Target is IAnonymousFunctionOperation selectLambda))
						return;

					var selectBlock = selectLambda.Body.Operations;
					if (selectBlock.Length != 1)
						return;

					if (!(selectBlock[0] is IReturnOperation selectRet) || !(selectRet.ReturnedValue is IConversionOperation selectConversion))
						return;

					if (!(selectConversion.Operand is IParameterReferenceOperation selectParameterReference) || selectParameterReference.Parameter != selectLambda.Symbol.Parameters[0])
						return;

					if (whereInvoke.Arguments.Length != 2 || !(whereInvoke.Arguments[1].Value is IDelegateCreationOperation whereDelegateCreation))
						return;

					if (!(whereDelegateCreation.Target is IAnonymousFunctionOperation whereLambda))
						return;

					// Check the select does as cast
					var whereBlock = whereLambda.Body.Operations;
					if (whereBlock.Length != 1)
						return;

					if (!(whereBlock[0] is IReturnOperation whereRet) || !(whereRet.ReturnedValue is IIsTypeOperation whereIsType))
						return;

					if (!(whereIsType.ValueOperand is IParameterReferenceOperation whereParameterReference) || whereParameterReference.Parameter != whereLambda.Symbol.Parameters[0])
						return;

					if (selectConversion.Type != whereIsType.TypeOperand)
						return;

					nodeContext.ReportDiagnostic(
						Diagnostic.Create(
							descriptor,
							selectInvoke.Syntax.GetLocation()
						)
					);
				},
					OperationKind.Invocation
				);

				bool IsAnyAParameterNullCheck(IOperation operation, IParameterSymbol parameter)
				{
					if (!(operation is IBinaryOperation binaryOperation))
						return false;

					if (binaryOperation.OperatorKind == BinaryOperatorKind.ConditionalAnd)
						return IsAnyAParameterNullCheck(binaryOperation.LeftOperand, parameter) || IsAnyAParameterNullCheck(binaryOperation.RightOperand, parameter);

					return binaryOperation.OperatorKind == BinaryOperatorKind.NotEquals &&
						(IsParameterReference(binaryOperation.LeftOperand, parameter) || IsParameterReference(binaryOperation.RightOperand, parameter));
				}

				bool IsParameterReference(IOperation operation, IParameterSymbol symbol)
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
			});
		}

        static bool MatchWhereSelect(InvocationExpressionSyntax selectInvoke, out ExpressionSyntax target, out TypeSyntax type)
        {
            target = null;
            type = null;

            if (selectInvoke.ArgumentList.Arguments.Count != 1)
                return false;
            var anyInvokeBase = selectInvoke.Expression as MemberAccessExpressionSyntax;
            if (anyInvokeBase == null || anyInvokeBase.Name.Identifier.Text != "Select")
                return false;
            var whereInvoke = anyInvokeBase.Expression as InvocationExpressionSyntax;
            if (whereInvoke == null || whereInvoke.ArgumentList.Arguments.Count != 1)
                return false;
            var baseMember = whereInvoke.Expression as MemberAccessExpressionSyntax;
            if (baseMember == null || baseMember.Name.Identifier.Text != "Where")
                return false;
            target = baseMember.Expression;

            ParameterSyntax param1, param2;
            ExpressionSyntax expr1, expr2;
            if (!ExtractLambda(whereInvoke.ArgumentList.Arguments[0], out param1, out expr1))
                return false;
            if (!ExtractLambda(selectInvoke.ArgumentList.Arguments[0], out param2, out expr2))
                return false;
            if (!expr1.IsKind(SyntaxKind.IsExpression))
                return false;
            type = (expr1 as BinaryExpressionSyntax)?.Right as TypeSyntax;
            if (type == null)
                return false;
            if (expr2.IsKind(SyntaxKind.AsExpression))
            {
                if (!CompareNames(param2, (expr2 as BinaryExpressionSyntax).Left as IdentifierNameSyntax))
                    return false;
                if (!type.IsEquivalentTo((expr2 as BinaryExpressionSyntax)?.Right))
                    return false;
            }
            else if (expr2.IsKind(SyntaxKind.CastExpression))
            {
                if (!CompareNames(param2, (expr2 as CastExpressionSyntax)?.Expression.SkipParens() as IdentifierNameSyntax))
                    return false;
                if (!type.IsEquivalentTo((expr2 as CastExpressionSyntax)?.Type))
                    return false;
            }
            else
                return false;

            if (!CompareNames(param1, (expr1 as BinaryExpressionSyntax)?.Left as IdentifierNameSyntax))
                return false;


            return target != null;
        }

        static bool CompareNames(ParameterSyntax param, IdentifierNameSyntax expr)
        {
            if (param == null || expr == null)
                return false;
            return param.Identifier.ValueText == expr.Identifier.ValueText;
        }

        static bool CheckNotEqualsNullExpr(BinaryExpressionSyntax expr, ParameterSyntax param)
        {
            if (expr == null)
                return false;
            if (!expr.IsKind(SyntaxKind.NotEqualsExpression))
                return false;
            if (expr.Right.IsKind(SyntaxKind.NullLiteralExpression) && CompareNames(param, expr.Left as IdentifierNameSyntax))
                return true;
            if (expr.Left.IsKind(SyntaxKind.NullLiteralExpression) && CompareNames(param, expr.Right as IdentifierNameSyntax))
                return true;
            return false;
        }

        static bool ExtractLambda(ArgumentSyntax argument, out ParameterSyntax parameter, out ExpressionSyntax body)
        {
            if (argument.Expression is SimpleLambdaExpressionSyntax)
            {
                var simple = (SimpleLambdaExpressionSyntax)argument.Expression;
                parameter = simple.Parameter;
                body = simple.Body as ExpressionSyntax;
                if (body == null)
                    return false;
                body = body.SkipParens();
                return true;
            }

            parameter = null;
            body = null;
            return false;
        }

        internal static InvocationExpressionSyntax MakeOfTypeCall(InvocationExpressionSyntax anyInvoke)
        {
            ExpressionSyntax target;
            TypeSyntax type;
            if (MatchWhereSelect(anyInvoke, out target, out type))
            {
                var ofTypeIdentifier = ((SimpleNameSyntax)SyntaxFactory.ParseName("OfType")).Identifier;
                var typeParams = SyntaxFactory.SeparatedList(new[] { type });
                var ofTypeName = SyntaxFactory.GenericName(ofTypeIdentifier, SyntaxFactory.TypeArgumentList(typeParams));
                return SyntaxFactory.InvocationExpression(SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, target, ofTypeName));
            }

            return null;
        }
    }
}