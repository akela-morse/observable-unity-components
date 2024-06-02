using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ObervableUnityComponentsGenerator
{
	[Generator]
	internal class WatchGenerator : ISourceGenerator
	{
		const string GLOBAL_NAMESPACE = "<global namespace>";
		const string WATCH_SYMBOL_NAME = "ObervableUnityComponents.WatchAttribute";
		const string MONOBEHAVIOUR_SYMBOL_NAME = "UnityEngine.MonoBehaviour";
		const string SCRIPTABLEOBJECT_SYMBOL_NAME = "UnityEngine.ScriptableObject";

		public void Initialize(GeneratorInitializationContext context)
		{
			context.RegisterForSyntaxNotifications(() => new WatchSyntaxReceiver());
		}

		public void Execute(GeneratorExecutionContext context)
		{
			if (!(context.SyntaxReceiver is WatchSyntaxReceiver receiver) || receiver.Variables.Count == 0)
				return;

			var fieldClassGroup = receiver.Variables
				.Select(v => (variable: v, semanticModel: context.Compilation.GetSemanticModel(v.SyntaxTree)))
				.Select(s => (IFieldSymbol)s.semanticModel.GetDeclaredSymbol(s.variable))
				.Where(f => f.GetAttributes().Any(a => a.AttributeClass.ToDisplayString() == WATCH_SYMBOL_NAME))
				.GroupBy<IFieldSymbol, INamedTypeSymbol>(f => f.ContainingType, SymbolEqualityComparer.Default);

			if (!fieldClassGroup.Any())
				return;

			var source = new StringBuilder();

			foreach (var group in fieldClassGroup)
			{
				if (!ClassIsObservableBehaviour(group.Key, context))
					continue;

				var namespaceName = group.Key.ContainingNamespace.ToDisplayString();

				if (namespaceName != GLOBAL_NAMESPACE)
				{
					source.Append(
$@"namespace {namespaceName}
{{"
					);
				}

				source.Append(
$@"
    public partial class {group.Key.Name}
    {{
		private int lastHash;

		private int GetCurrentHash()
		{{
			unchecked
			{{
				int hash = 17;"
				);

				foreach (var fieldSymbol in group)
				{
					if (!FieldIsSerialisedInUnity(fieldSymbol, context))
						continue;

					var fieldName = fieldSymbol.Name;

					source.Append(
$@"
				hash = hash * 23 + this.{fieldName}.GetHashCode();"
					);
				}

				source.Append(
$@"
				return hash;
			}}
		}}
		
		/// <summary>
		/// Checks if any watched fields has changed since the last check
		/// </summary>
		/// <returns>True if any changes occured since the last check, False otherwise</returns>
		public bool HaveWatchedValuesChanged()
		{{
			var currentHash = GetCurrentHash();

			if (lastHash == currentHash)
				return false;

			lastHash = currentHash;

			return true;
		}}
	}}"
				);

				if (namespaceName != GLOBAL_NAMESPACE)
				{
					source.Append(
$@"
}}"
					);
				}

				// All good
				context.AddSource($"{group.Key.Name}_observable.g.cs", SourceText.From(source.ToString(), Encoding.UTF8));
			}
		}

		private bool ClassIsObservableBehaviour(INamedTypeSymbol classSymbol, GeneratorExecutionContext context)
		{
			var isValidType = false;
			var baseType = classSymbol.BaseType;

			while (baseType != null && !isValidType)
			{
				if (baseType.ToDisplayString() == MONOBEHAVIOUR_SYMBOL_NAME || baseType.ToDisplayString() == SCRIPTABLEOBJECT_SYMBOL_NAME)
					isValidType = true;
				else
					baseType = baseType.BaseType;
			}

			if (!isValidType)
			{
				context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor(
					"AKELIB0001",
					"Non-component observable class",
					"Class {0} has fields with [Watch] attribute, but is not a MonoBehaviour or a ScriptableObject.",
					"Observable Unity Components",
					DiagnosticSeverity.Error,
					true), classSymbol.Locations.FirstOrDefault(), classSymbol.ToDisplayString()
				));

				return false;
			}

			// partial check
			//if (!classSymbol.DeclaringSyntaxReferences.Any(s => s.GetSyntax() is BaseTypeDeclarationSyntax d && d.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword))))
			//{
			//	context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor(
			//		"AKELIB0003",
			//		"Non-partial class",
			//		"Class {0} has fields with [Watch] attribute, but is not marked partial.",
			//		"Observable Unity Components",
			//		DiagnosticSeverity.Error,
			//		true), classSymbol.Locations.FirstOrDefault(), classSymbol.ToDisplayString()
			//	));

			//	return null;
			//}

			return true;
		}

		private bool FieldIsSerialisedInUnity(IFieldSymbol fieldSymbol, GeneratorExecutionContext context)
		{
			// Full list of conditions for a field to be serialised in Unity: https://docs.unity3d.com/Manual/script-Serialization.html
			if ((fieldSymbol.DeclaredAccessibility != Accessibility.Public && !fieldSymbol.GetAttributes().Any(x => x.AttributeClass.ToDisplayString() == "UnityEngine.SerializeField"))
				|| fieldSymbol.IsStatic || fieldSymbol.IsConst || fieldSymbol.IsReadOnly)
			{
				context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor(
					"AKELIB0004",
					"Non-serializable watched field",
					"Field {0} has a [Watch] attribute, but is not serializable. A serializable field must be non-static, non-constant, non-readonly, and either be public or have a [SerializeField] attribute.",
					"Observable Unity Components",
					DiagnosticSeverity.Warning,
					true), fieldSymbol.Locations.FirstOrDefault(), fieldSymbol.ToDisplayString()
				));

				return false;
			}

			return true;
		}

		private string GetMemoryNameFromFieldName(string fieldName)
		{
			var memoryName = Regex.Replace(fieldName, @"^[a-zA-Z]?_", "");
			memoryName += "_last";

			return memoryName;
		}
	}
}
