using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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

			foreach (var group in fieldClassGroup)
			{
				if (!ClassIsObservableBehaviour(group.Key, context) || group.Key.IsAbstract)
					continue;

				var source = new StringBuilder();

				var namespaceName = group.Key.ContainingNamespace.ToDisplayString();
				var hideMember = fieldClassGroup.Any(x => x.Key.Equals(group.Key.BaseType, SymbolEqualityComparer.Default) && !x.Key.IsAbstract);

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
		private{(hideMember ? " new" : "")} int observableGenerated_lastHash;

		private{(hideMember ? " new" : "")} int observableGenerated_GetCurrentHash()
		{{
			unchecked
			{{
				int hash = 17;"
				);

				var fullFieldList = new List<IFieldSymbol>();
				GetWatchedFieldsFromBaseType(fullFieldList, group.Key, in fieldClassGroup);

				foreach (var fieldSymbol in fullFieldList)
				{
					if (!FieldIsSerialisedInUnity(fieldSymbol, context))
						continue;

					var fieldName = fieldSymbol.Name;

					if (fieldSymbol.Type.IsReferenceType)
					{
						source.Append(
$@"
				hash = hash * 23 + (this.{fieldName} == null ? 0 : this.{fieldName}.GetHashCode());"
						);
					}
					else
					{
						source.Append(
$@"
				hash = hash * 23 + this.{fieldName}.GetHashCode();"
						);
					}
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
			var currentHash = this.observableGenerated_GetCurrentHash();

			if (this.observableGenerated_lastHash == currentHash)
				return false;

			this.observableGenerated_lastHash = currentHash;

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

		private void GetWatchedFieldsFromBaseType(List<IFieldSymbol> list, INamedTypeSymbol classSymbol, in IEnumerable<IGrouping<INamedTypeSymbol, IFieldSymbol>> fieldClassGroup)
		{
			foreach (var type in EnumerateTypeHierarchy(classSymbol))
			{
				var existingClassSymbol = fieldClassGroup.FirstOrDefault(x => x.Key.Equals(type, SymbolEqualityComparer.Default));

				if (type.Equals(classSymbol, SymbolEqualityComparer.Default))
					list.AddRange(existingClassSymbol);
				else if (existingClassSymbol != null)
					list.AddRange(existingClassSymbol.Where(x => x.DeclaredAccessibility != Accessibility.Private));
			}
		}

		private static IEnumerable<INamedTypeSymbol> EnumerateTypeHierarchy(INamedTypeSymbol leaf)
		{
			do
			{
				yield return leaf;
				leaf = leaf.BaseType;
			}
			while (leaf != null);
		}
	}
}
