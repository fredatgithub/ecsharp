﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Loyc.Collections;
using Loyc.Collections.Impl;

namespace Loyc.LLParserGenerator
{
	using S = Loyc.Syntax.CodeSymbols;
	using System.Diagnostics;
	using Loyc.Utilities;
	using Loyc.Syntax;

	partial class LLParserGenerator
	{
		/// <summary>
		/// Directs code generation using the visitor pattern to visit the 
		/// predicates in a rule. The process starts with <see cref="Generate(Rule)"/>.
		/// </summary>
		/// <remarks>
		/// This class is in charge of both code generation and prediction analysis.
		/// It relies on <see cref="IPGCodeGenHelper"/> for most low-level 
		/// code generation tasks, and it relies on the "Prediction analysis code" 
		/// in <see cref="LLParserGenerator"/> for the lowest-level analysis tasks.
		/// </remarks>
		protected class GenerateCodeVisitor : PredVisitor
		{
			public LLParserGenerator LLPG;
			public LNodeFactory F;
			Rule _currentRule;
			Pred _currentPred;
			RWList<LNode> _classBody; // Location where we generate terminal sets
			RWList<LNode> _target; // List of statements in method being generated
			ulong _laVarsNeeded;
			// # of alts using gotos -- a counter is used to make unique labels
			int _separatedMatchCounter = 0, _stopLabelCounter = 0;
			bool _recognizerMode;

			public GenerateCodeVisitor(LLParserGenerator llpg)
			{
				LLPG = llpg;
				F = new LNodeFactory(llpg._sourceFile);
				_classBody = llpg._classBody;
			}

			IPGCodeGenHelper CSG { get { return LLPG.SnippetGenerator; } }

			public void Generate(Rule rule)
			{
				if (rule.Name != null)
					Generate(rule, false);
				if (rule.NameAsRecognizer != null)
					Generate(rule, true);
			}
			public void Generate(Rule rule, bool recognizerMode)
			{
				CSG.BeginRule(rule);
				_currentRule = rule;
				_target = new RWList<LNode>();
				_laVarsNeeded = 0;
				_separatedMatchCounter = _stopLabelCounter = 0;
				_recognizerMode = recognizerMode;

				Visit(rule.Pred);

				if (_laVarsNeeded != 0) {
					LNode laVars = F.Call(S.Var, CSG.LAType());
					for (int i = 0; _laVarsNeeded != 0; i++, _laVarsNeeded >>= 1)
						if ((_laVarsNeeded & 1) != 0)
							laVars = laVars.PlusArg(F.Id("la" + i.ToString()));
					_target.Insert(0, laVars);
				}

				LNode ruleMethod = CSG.CreateRuleMethod(rule, _target.ToRVList(), recognizerMode);
				_classBody.Add(ruleMethod);
			}

			new public void Visit(Pred pred)
			{
				if (pred.PreAction != null && !_recognizerMode)
					_target.SpliceAdd(pred.PreAction, S.List);
				var old = _currentPred;
				_currentPred = pred;
				pred.Call(this);
				_currentPred = old;
				if (pred.PostAction != null && !_recognizerMode)
					_target.SpliceAdd(pred.PostAction, S.List);
			}

			void VisitWithNewTarget(Pred toBeVisited, RWList<LNode> target)
			{
				var old = _target;
				_target = target;
				Visit(toBeVisited);
				_target = old;
			}

			/// <summary>
			/// Visit(Alts) is the most important method in this class. It generates 
			/// all prediction code, which is the majority of the code in a parser.
			/// </summary>
			public override void Visit(Alts alts)
			{
				PredictionTree tree = alts.PredictionTree;
				var timesUsed = new Dictionary<int, int>();
				tree.CountTimesUsed(timesUsed);

				GenerateCodeForAlts(alts, timesUsed, tree);
			}

			#region GenerateCodeForAlts() and related: generates code based on a prediction tree

			// GENERATED CODE EXAMPLE: The methods in this region generate
			// the for(;;) loop in this example and everything inside it, except
			// the calls to Match() which are generated by Visit(TerminalPred).
			// The generated code uses "goto" and "match" blocks in some cases
			// to avoid code duplication. This occurs when the matching code 
			// requires multiple statements AND appears more than once in the 
			// prediction tree. Otherwise, matching is done "inline" during 
			// prediction. We generate a for(;;) loop for (...)*, and in certain 
			// cases, we generates a do...while(false) loop for (...)?.
			//
			// rule Foo ==> #[ (('a'|'A') 'A')* 'a'..'z' 'a'..'z' ];
			// public void Foo()
			// {
			//     int la0, la1;
			//     for (;;) {
			//         la0 = LA(0);
			//         if (la0 == 'a') {
			//             la1 = LA(1);
			//             if (la1 == 'A')
			//                 goto match1;
			//             else
			//                 break;
			//         } else if (la0 == 'A')
			//             goto match1;
			//         else
			//             break;
			//         match1:
			//         {
			//             Match('A', 'a');
			//             Match('A');
			//         }
			//     }
			//     MatchRange('a', 'z');
			//     MatchRange('a', 'z');
			// }

			private void GenerateCodeForAlts(Alts alts, Dictionary<int, int> timesUsed, PredictionTree tree)
			{
				// Generate matching code for each arm. The "bool" in each pair 
				// indicates whether the matching code needs to be split out 
				// (separated) from the prediction tree.
				Pair<LNode, bool>[] matchingCode = new Pair<LNode, bool>[alts.Arms.Count];
				MSet<int> unreachable = new MSet<int>();
				int separateCount = 0;
				for (int i = 0; i < alts.Arms.Count; i++) {
					if (!timesUsed.ContainsKey(i)) {
						unreachable.Add(i + 1);
						continue;
					}

					var codeForThisArm = new RWList<LNode>();
					VisitWithNewTarget(alts.Arms[i], codeForThisArm);

					matchingCode[i].A = F.Braces(codeForThisArm.ToRVList());
					if (matchingCode[i].B = timesUsed[i] > 1 && !SimpleEnoughToRepeat(matchingCode[i].A))
						separateCount++;
				}

				if (unreachable.Count == 1)
					LLPG.Output(alts.Basis, alts, Warning, string.Format("Branch {0} is unreachable.", unreachable.First()));
				else if (unreachable.Count > 1)
					LLPG.Output(alts.Basis, alts, Warning, string.Format("Branches {0} are unreachable.", unreachable.Join(", ")));
				if (!timesUsed.ContainsKey(ExitAlt) && alts.Mode != LoopMode.None)
					LLPG.Output(alts.Basis, alts, Warning, "Infinite loop. The exit branch is unreachable.");

				Symbol loopType = null;

				// Generate a loop body for (...)* or (...)?:
				if (alts.Mode == LoopMode.Star)
					loopType = S.For;
				else if (alts.Mode == LoopMode.Opt && (uint)(alts.DefaultArm ?? -1) < (uint)alts.Arms.Count)
					loopType = S.Do;

				// If the code for an arm is nontrivial and appears multiple times 
				// in the prediction table, it will have to be split out into a 
				// labeled block and reached via "goto". I'd rather just do a goto
				// from inside one "if" statement to inside another, but in C# 
				// (unlike in C and unlike in CIL) that is prohibited :(
				var extraMatching = GenerateExtraMatchingCode(matchingCode, separateCount, ref loopType);

				Symbol breakMode = loopType; // used to request a "goto" label in addition to the loop
				LNode code = GeneratePredictionTreeCode(tree, matchingCode, ref breakMode);

				if (!extraMatching.IsEmpty)
					code = LNode.MergeLists(code, F.Braces(extraMatching), S.Braces);

				if (loopType == S.For) {
					// (...)* => for (;;) {}
					code = F.Call(S.For, F._Missing, F._Missing, F._Missing, code);
				} else if (loopType == S.Do) {
					// (...)? becomes "do {...} while(false);" IF the exit branch is NOT the default.
					// If the exit branch is the default, then no loop and no "break" is needed.
					code = F.Call(S.Do, code, F.@false);
				}
				if (breakMode != loopType) {
					// Add "stop:" label (plus extra ";" for C# compatibility, in 
					// case the label ends the block in which it is located.)
					var stopLabel = F.Call(S.Label, F.Id(breakMode))
									 .PlusAttr(F.Trivia(S.TriviaRawTextAfter, ";"));
					code = LNode.MergeLists(code, stopLabel, S.Braces);
				}

				_target.SpliceAdd(code, S.Braces);
			}

			private bool SimpleEnoughToRepeat(LNode code)
			{
				Debug.Assert(code.Calls(S.Braces));
				if (code.ArgCount > 1)
					return false;
				return code.ArgCount == 1 && !code.Args[0].Calls(S.If) && code.FindArgNamed(S.Braces) == null;
			}

			private RWList<LNode> GenerateExtraMatchingCode(Pair<LNode, bool>[] matchingCode, int separateCount, ref Symbol loopType)
			{
				var extraMatching = new RWList<LNode>();
				if (separateCount != 0) {
					//int labelCounter = 0;
					int skipCount = 0;
					int firstSkip = -1;
					string suffix = NextGotoSuffix();

					for (int i = 0; i < matchingCode.Length; i++) {
						if (matchingCode[i].B) // split out this case
						{
							var label = F.Id("match" + (i + 1) /*(++labelCounter)*/ + suffix);

							// break/continue; matchN: matchingCode[i].A;
							var skip = F.Call(loopType == S.For ? S.Continue : S.Break);
							if (firstSkip == -1)
								firstSkip = extraMatching.Count;
							extraMatching.Add(skip);
							extraMatching.Add(F.Call(S.Label, label));
							extraMatching.Add(matchingCode[i].A);
							skipCount++;

							// put @@{ goto matchN; } in prediction tree
							matchingCode[i].A = F.Call(S.Goto, label);
						}
					}
					Debug.Assert(firstSkip != -1);
					if (separateCount == matchingCode.Length) {
						// All of the matching code was split out, so the first 
						// break/continue statement is not needed.
						extraMatching.RemoveAt(firstSkip);
						skipCount--;
					}
					if (skipCount > 0 && loopType == null)
						// add do...while(false) loop so that the break statements make sense
						loopType = S.Do;
				}
				return extraMatching;
			}

			private string NextStopLabel()
			{
				if (++_stopLabelCounter == 1)
					return "stop";
				else
					return string.Format("stop{0}", _stopLabelCounter);
			}
			private string NextGotoSuffix()
			{
				if (++_separatedMatchCounter == 1)
					return "";
				if (_separatedMatchCounter > 26)
					return string.Format("_{0}", _separatedMatchCounter - 1);
				else
					return ((char)('a' + _separatedMatchCounter - 1)).ToString();
			}

			protected LNode GetPredictionSubtree(PredictionBranch branch, Pair<LNode, bool>[] matchingCode, ref Symbol haveLoop)
			{
				if (branch.Sub.Tree != null)
					return GeneratePredictionTreeCode(branch.Sub.Tree, matchingCode, ref haveLoop);
				else {
					Debug.Assert(branch.Sub.Alt != ErrorAlt);
					if (branch.Sub.Alt == ExitAlt) {
						return GetExitStmt(haveLoop);
					} else {
						var code = matchingCode[branch.Sub.Alt].A;
						if (code.Calls(S.Braces, 1))
							return code.Args[0].Clone();
						else
							return code.Clone();
					}
				}
			}

			private LNode GetExitStmt(Symbol haveLoop)
			{
				if (haveLoop == null || haveLoop == S.Do)
					return (LNode)F._Missing;
				if (haveLoop == S.For)
					return (LNode)F.Call(S.Break);
				return (LNode)F.Call(S.Goto, F.Id(haveLoop));
			}

			protected LNode GeneratePredictionTreeCode(PredictionTree tree, Pair<LNode, bool>[] matchingCode, ref Symbol haveLoop)
			{
				var braces = F.Braces();

				Debug.Assert(tree.Children.Count >= 1);
				var alts = (Alts)_currentPred;

				if (tree.Children.Count == 1)
					return GetPredictionSubtree(tree.Children[0], matchingCode, ref haveLoop);

				// From the prediction table, we can generate either an if-else chain:
				//
				//   if (la0 >= '0' && la0 <= '7') sub_tree_1();
				//   else if (la0 == '-') sub_tree_2();
				//   else break;
				//
				// or a switch statement:
				//
				//   switch(la0) {
				//   case '0': case '1': case '2': case '3': case '4': case '5': case '6': case '7':
				//     sub_tree_1();
				//     break;
				//   case '-':
				//     sub_tree_2();
				//     break;
				//   default:
				//     goto breakfor;
				//   }
				//
				// Assertion levels always need an if-else chain; lookahead levels 
				// consider the complexity of switch vs if and decide which is most
				// appropriate. Generally "if" is slower, but a switch may require
				// too many labels since it doesn't support ranges like "la0 >= 'a'
				// && la0 <= 'z'".
				//
				// This class makes if-else chains directly (using IPGTerminalSet.
				// GenerateTest() to generate the test expressions), but the code 
				// snippet generator (CSG) is used to generate switch statements 
				// because the required code may be more complex and depends on the 
				// type of terminals--for example, if the terminals are Symbols, 
				// we'll need a static Dictionary in order to use a switch:
				//
				// static Dictionary<Symbol, int> Foo_JmpTbl = Foo_MakeJmpTbl();
				// static Dictionary<Symbol, int> Foo_MakeJmpTbl()
				// {
				//    var tbl = new Dictionary<Symbol, int>();
				//    tbl.Add(GSymbol.Get("0"), 1);
				//    ...
				//    tbl.Add(GSymbol.Get("7"), 1);
				//    tbl.Add(GSymbol.Get("-"), 2);
				// }
				// void Foo()
				// {
				//   Symbol la0;
				//   for (;;) {
				//     la0 = LA(0);
				//     int label;
				//     Foo_JmpTbl.TryGetValue(la0, out label);
				//     switch(label) {
				//     case 0:
				//       goto breakfor;
				//     case 1:
				//       sub_tree_1();
				//       break;
				//     case 2:
				//       sub_tree_2();
				//       break;
				//     }
				//   }
				//   breakfor:;
				// }
				//
				// We may or may not be generating code inside a for(;;) loop. If we 
				// decide to generate a switch() statement, one of the branches will 
				// usually need to break out of the for loop, but "break" can only
				// break out of the switch(). Therefore, if any of the matching code
				// is a break statement, .... hmm... I guess we could put a "breakfor"
				// label outside the for-loop and goto it.

				RWList<LNode> block = new RWList<LNode>();
				LNode laVar = null;
				MSet<int> switchCases = new MSet<int>();
				IPGTerminalSet[] branchSets = null;
				bool should = false;

				if (tree.UsesLA()) {
					laVar = F.Id("la" + tree.Lookahead.ToString());

					if (!tree.IsAssertionLevel) {
						IPGTerminalSet covered = CSG.EmptySet;
						branchSets = tree.Children.Select(branch => {
							var set = branch.Set.Subtract(covered);
							covered = covered.Union(branch.Set);
							return set;
						}).ToArray();

						should = CSG.ShouldGenerateSwitch(branchSets, switchCases, tree.Children.Last.IsErrorBranch);
						if (!should)
							switchCases.Clear();
						else if (should && haveLoop == S.For)
							haveLoop = GSymbol.Get(NextStopLabel());
					}
				}

				LNode[] branchCode = new LNode[tree.Children.Count];
				for (int i = 0; i < tree.Children.Count; i++)
					if (tree.Children[i].IsErrorBranch)
						branchCode[i] = CSG.ErrorBranch(tree.TotalCoverage, laVar);
					else
						branchCode[i] = GetPredictionSubtree(tree.Children[i], matchingCode, ref haveLoop);

				var code = GenerateIfElseChain(tree, branchCode, ref laVar, switchCases);
				if (laVar != null) {
					block.Insert(0, F.Set(laVar, CSG.LA(tree.Lookahead)));
					_laVarsNeeded |= 1ul << tree.Lookahead;
				} else if (should)
					laVar = CSG.LA(tree.Lookahead);

				if (should) {
					Debug.Assert(switchCases.Count != 0);
					code = CSG.GenerateSwitch(branchSets, switchCases, branchCode, code, laVar);
				}

				block.Add(code);
				return F.Braces(block.ToRVList());
			}

			private LNode GenerateIfElseChain(PredictionTree tree, LNode[] branchCode, ref LNode laVar, MSet<int> switchCases)
			{
				// From the prediction table, generate a chain of if-else 
				// statements in reverse, starting with the final "else" clause.
				// Skip any branches that have been claimed for use in a switch()
				LNode ifChain = null;
				bool usedTest = false;

				for (int i = tree.Children.Count - 1; i >= 0; i--) {
					if (switchCases.Contains(i))
						continue;

					if (ifChain == null)
						ifChain = branchCode[i];
					else {
						usedTest = true;
						var branch = tree.Children[i];
						LNode test;
						if (tree.IsAssertionLevel)
							test = GenerateTest(branch.AndPreds, tree.Lookahead, laVar);
						else {
							var set = CSG.Optimize(branch.Set, branch.Covered);
							test = CSG.GenerateTest(set, laVar);
						}

						LNode @if = F.Call(S.If, test, branchCode[i]);
						if (!ifChain.IsIdWithoutPAttrs(S.Missing))
							@if = @if.PlusArg(ifChain);
						ifChain = @if;
					}
				}
				if (!usedTest)
					laVar = null; // unnecessary

				return ifChain;
			}

			LNode Join(IEnumerable<LNode> nodes, Symbol op, LNode @default)
			{
				LNode result = @default;
				foreach (LNode node in nodes)
					if (result == @default)
						result = node;
					else
						result = F.Call(op, result, node);
				return result;
			}
			private LNode GenerateTest(List<Set<AndPred>> andPreds, int lookaheadAmt, LNode laVar)
			{
				return Join(andPreds.Select(set => GenerateTest(set, lookaheadAmt, laVar)), S.Or, F.@false);
			}
			private LNode GenerateTest(Set<AndPred> andPreds, int lookaheadAmt, LNode laVar)
			{
				var andPredCode = andPreds.Select(ap => {
					var code = GetAndPredCode(ap, lookaheadAmt, laVar);
					return CSG.GenerateAndPredCheck(ap, code, true);
				});
				return Join(andPredCode, S.And, F.@true);
			}

			#endregion

			public override void Visit(Seq pred)
			{
				foreach (var p in pred.List)
					Visit(p);
			}
			public override void Visit(Gate pred)
			{
				Visit(pred.Match);
			}
			public override void Visit(AndPred pred)
			{
				if (!(pred.Prematched ?? false))
					_target.Add(CSG.GenerateAndPredCheck(pred, GetAndPredCode(pred, 0, CSG.LA(0)), false));
			}
			public override void Visit(RuleRef rref)
			{
				_target.Add(rref.AutoSaveResult(F.Call(rref.Rule.Name)));
			}
			public override void Visit(TerminalPred term)
			{
				if (_recognizerMode)
					_target.Add(CSG.GenerateMatch(term.Set, false, _recognizerMode));
				else if (term.Set.ContainsEverything || (term.Prematched ?? false))
					_target.Add(term.AutoSaveResult(CSG.GenerateSkip(term.ResultSaver != null)));
				else
					_target.Add(term.AutoSaveResult(CSG.GenerateMatch(term.Set, term.ResultSaver != null, false)));
			}

			LNode GetAndPredCode(AndPred pred, int lookaheadAmt, LNode laVar)
			{
				if (pred.Pred is LNode) {
					LNode code = (LNode)pred.Pred;

					Func<LNode, LNode> selector = null; selector = arg => {
						if (arg.Equals(AndPred.SubstituteLA))
							return (LNode)laVar;
						if (arg.Equals(AndPred.SubstituteLI))
							return (LNode)F.Literal(lookaheadAmt);
						return arg.WithArgs(selector);
					};
					return code.WithArgs(selector);
				} else {
					Pred synPred = (Pred)pred.Pred; // Buffalo buffalo = (Buffalo)buffalo.Buffalo;
					return F.Call(LLPG.GetRecognizerRule(synPred).NameAsRecognizer);
				}
			}
		}
	}
}
