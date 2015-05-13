﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Loyc.LLParserGenerator.ParsersAndMacros;
using Loyc.Syntax;
using Loyc.Collections;

namespace Loyc.LLParserGenerator
{
	using S = CodeSymbols;

	/// <summary>Helper class invoked after <see cref="StageTwoParser"/>. Its job
	/// is to create variables referenced by labels (label:item) and by code blocks
	/// ($RuleName), to modify the code blocks to remove the $ operator, and to
	/// update the ResultSaver of each labeled predicate.
	/// </summary>
	class AutoValueSaverVisitor : RecursivePredVisitor
	{
		public static void Run(Rule rule, IMessageSink sink, IDictionary<Symbol, Rule> rules, LNode terminalType)
		{
			// 1. Scan for a list of code blocks that use $labels, and a list of rules referenced.
			var data = new DataGatheringVisitor(rules, rule);
			if (data.RulesReferenced.Count != 0 || data.OtherReferences.Count != 0 || data.ProperLabels.Count != 0)
			{
				var vsv = new AutoValueSaverVisitor(data, sink, rules, terminalType);
				// 2. Scan for predicates with labels, and RuleRefs referenced by 
				//    code blocks. For each such predicate, generate a variable at 
				//    the beginning of the rule and set the ResultSaver (TODO).
				vsv.Process(rule);
				// 3. Replace recognized $substitutions in code blocks
				data.ReplaceSubstitutionsInCodeBlocks();
			}
		}

		void Process(Rule rule)
		{
			Visit(rule.Pred);
			if (_newVarInitializers.Count != 0) {
				var decls = _newVarInitializers.OrderBy(p => p.Key.Name).Select(p => p.Value.B);
				LNode decls2 = F.Call(S.Splice, decls);
				rule.Pred.PreAction = Pred.MergeActions(decls2, rule.Pred.PreAction);
			}
		}

		class DataGatheringVisitor : RecursivePredVisitor
		{
			// code blocks with $labels
			IDictionary<Symbol, Rule> _rules;
			public DataGatheringVisitor(IDictionary<Symbol, Rule> rules, Rule rule)
				{ _rules = rules; Visit(rule.Pred); }
			// List of predicates that are using {...$substitution...}
			public HashSet<Pred> PredsUsingSubstitution = new HashSet<Pred>();
			// Rules referenced by code blocks
			public HashSet<Rule> RulesReferenced = new HashSet<Rule>();
			// Labels, token sets, and unidentified things referenced by code blocks
			// The integer counts the number of times that an unlabeled thing, that 
			// was referenced by code block, appears in the grammar.
			public Dictionary<LNode, int> OtherReferences = new Dictionary<LNode, int>();
			// Labels encountered in predicates
			public HashSet<Symbol> ProperLabels = new HashSet<Symbol>();

			#region Step 1: data gathering

			public override void Visit(AndPred pred)
			{
				base.Visit(pred);
				VisitCode(pred, pred.Pred as LNode);
			}
			public override void VisitOther(Pred pred)
			{
				VisitCode(pred, pred.PreAction);
				VisitCode(pred, pred.PostAction);
				if (pred.VarLabel != null)
					ProperLabels.Add(pred.VarLabel);
			}
			void VisitCode(Pred pred, LNode code)
			{
				if (code == null) return;
				code.ReplaceRecursive(node => {
					if (node.Calls(S.Substitute, 1)) {
						var arg = node.Args[0];
						PredsUsingSubstitution.Add(pred);
						if (arg.IsId && _rules.ContainsKey(arg.Name))
							RulesReferenced.Add(_rules[arg.Name]);
						else
							OtherReferences[arg] = 0;
					}
					return null; // search only, no replace
				});
			}

			#endregion

			#region Step 3: perform code substitutions 

			internal void ReplaceSubstitutionsInCodeBlocks()
			{
				foreach (var pred in PredsUsingSubstitution) {
					pred.PreAction = ReplaceSubstitutionsIn(pred.PreAction);
					pred.PostAction = ReplaceSubstitutionsIn(pred.PostAction);
					var and = pred as AndPred;
					if (and != null && and.Pred is LNode)
						and.Pred = ReplaceSubstitutionsIn((LNode)and.Pred);
				}
			}
			LNode ReplaceSubstitutionsIn(LNode code)
			{
				if (code == null) return null;
				return code.ReplaceRecursive(node => {
					if (node.Calls(S.Substitute, 1)) { // found $subst_expr
						var label = node.Args[0];
						if (label.IsId) {
							if (ProperLabels.Contains(label.Name))
								return label;
							else if (_rules.ContainsKey(label.Name))
								return F.Id(PickVarNameForRuleName(label.Name));
						}
						if (OtherReferences.TryGetValue(label, -1) > 0)
							return F.Id(PickVarNameForLNode(label));
						// Do not change the code in other cases (e.g. the code 
						// block might contain $LI/$LA, which is handled later)
					}
					return null;
				});
			}
			
			#endregion
		}

		static LNodeFactory F = new LNodeFactory(new EmptySourceFile("LLLPG $substitution analyzer"));

		IMessageSink _sink;
		IDictionary<Symbol, Rule> _rules;
		DataGatheringVisitor _data;
		LNode _terminalType;
		AutoValueSaverVisitor(DataGatheringVisitor data, IMessageSink sink, IDictionary<Symbol, Rule> rules, LNode terminalType)
			{ _data = data; _sink = sink; _rules = rules; _terminalType = terminalType; }

		// A map from variable names to a Pair<variable type, initializer statement>
		Dictionary<Symbol,Pair<LNode,LNode>> _newVarInitializers = new Dictionary<Symbol,Pair<LNode,LNode>>();

		#region Step 2: generate variables and set remaining ResultSavers

		public override void Visit(RuleRef pred)
		{
			var retType = pred.Rule.ReturnType;
			if (pred.VarLabel != null)
				MaybeCreateVariableFor(pred, pred.VarLabel, retType);
			else if (_data.RulesReferenced.Contains(pred.Rule))
				MaybeCreateVariableFor(pred, PickVarNameForRuleName(pred.Rule.Name), retType);
		}

		public override void VisitOther(Pred pred)
		{
			LNode basis = pred.Basis;
			if (pred.VarLabel != null) {
				basis = null;
				MaybeCreateVariableFor(pred, pred.VarLabel, _terminalType);
			}
			// If code blocks refer to this predicate's label or basis node, tally
			// the reference and create a variable decl for it if we haven't yet.
			// TODO: bug here: LNode equality depends on trivia. 
			//       Should we change default definition of LNode equality?
			int predCounter;
			if (_data.OtherReferences.TryGetValueSafe(basis, out predCounter)) {
				_data.OtherReferences[pred.Basis] = predCounter + 1;
				MaybeCreateVariableFor(pred, PickVarNameForLNode(basis), _terminalType);
			}
		}
		private void MaybeCreateVariableFor(Pred pred, Symbol varName, LNode primType)
		{
			if (pred.ResultSaver != null)
				return;
			if (primType == null) {
				primType = F.Object;
				_sink.Write(Severity.Error, pred, Localize.From("The type of this expression is unknown (did you set LLLPG's 'terminalType'  option?)"));
			}
			LNode type = primType, oldType;
			if (pred.VarIsList)
				type = F.Of(F.Id("List"), primType); // TODO: allow user-defined list type
			if (!_newVarInitializers.ContainsKey(varName))
				_newVarInitializers[varName] = Pair.Create(type, F.Var(type, varName, DefaultOf(type)));
			else if (!(oldType = _newVarInitializers[varName].A).Equals(type))
				_sink.Write(Severity.Error, pred, Localize.From(
					"Type mismatch: Variable '{0}' was generated earlier with type {1}, but this predicate expects {2}.",
					varName, oldType, type));
			pred.ResultSaver = Pred.GetStandardResultSaver(F.Id(varName),
				pred.VarIsList ? S.AddSet : S.Assign);
		}
		static LNode DefaultOf(LNode type)
		{
			if (type.IsIdNamed(S.Int32))
				return F.Literal(0);
			return F.Call(S.Default, type);
		}

		#endregion
		
		static Symbol PickVarNameForRuleName(Symbol name)
			{ return GSymbol.Get("got_" + name); }

		// Converts the subject of a substitution expr like $'*' to a valid ident-
		// ifier, under the assumption that it doesn't refer to a rule or label.
		static Symbol PickVarNameForLNode(LNode label)
		{
			LNode a, b;
			if (label.IsId) {
				// Ignore the predefined special substitutions $LA and $LI
				if (label.Name.Name == "LA" || label.Name.Name == "LI")
					return null;
				return GSymbol.Get("tok_" + label.Name);
			} else if (label.IsLiteral) {
				return LiteralToVarName(label.Value);
			} else if (label.Calls(S.Dot, 2))
				return GSymbol.Get("tok__" + label.Args[1].Name);
			else
				return null;
		}
		static Symbol LiteralToVarName(object literal)
		{
			string prefix = literal is char ? "ch_" : "lit_";
			return GSymbol.Get(prefix + LiteralToIdent(literal));
		}
		static string LiteralToIdent(object literal)
		{
			return Ecs.EcsNodePrinter.SanitizeIdentifier((literal ?? "null").ToString());
		}
	}
}