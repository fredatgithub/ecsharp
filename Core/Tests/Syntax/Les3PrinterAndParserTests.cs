﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using Loyc.Collections;
using Loyc.MiniTest;
using Loyc.Syntax;
using Loyc.Syntax.Les;
using S = Loyc.Syntax.CodeSymbols;

namespace Loyc.Syntax.Les
{
	public abstract class Les3PrinterAndParserTests : Assert
	{
		protected static LNodeFactory F = new LNodeFactory(EmptySourceFile.Unknown);
		
		protected LNode a = F.Id("a"), b = F.Id("b"), c = F.Id("c"), x = F.Id("x");
		protected LNode Foo = F.Id("Foo"), IFoo = F.Id("IFoo"), T = F.Id("T");
		protected LNode zero = F.Literal(0), one = F.Literal(1), two = F.Literal(2);
		protected LNode _(string name) { return F.Id(name); }
		protected LNode _(Symbol name) { return F.Id(name); }

		protected LNode Op(LNode node) { return node.SetBaseStyle(NodeStyle.Operator); }

		protected LNode OnNewLine(LNode node)
		{
			return node.PlusAttrBefore(F.TriviaNewline);
		}
		protected LNode NewlineAfter(LNode node)
		{
			return node.PlusTrailingTrivia(F.TriviaNewline);
		}

		#region Literals
		// Note: The most rigorous testing may be done in the Lexer tests,
		//       but these tests have the advantage of also testing the printer.

		[Test]
		public void NumericLiterals()
		{
			Exact(@"123", F.Literal(123));
			Exact(@"(123)", F.InParens(F.Literal(123)));
			Exact(@"123uL", F.Literal(123uL));
			Exact(@"123.25", F.Literal(123.25));
			Exact(@"123.25f", F.Literal(123.25f));
		}

		[Test]
		public void StringLiterals()
		{
			// General parser and printer tests
			Exact(@"'!'", F.Literal('!'));
			Exact(@"""!""", F.Literal("!"));
			Exact(@"'''!'''", F.Literal("!").SetBaseStyle(NodeStyle.TQStringLiteral));
			Exact(@"""""""!""""""", F.Literal("!").SetBaseStyle(NodeStyle.TDQStringLiteral));
			Exact(@"""\\\r\n\t\0\x12""", F.Literal("\\\r\n\t\0\x12").SetBaseStyle(NodeStyle.Default));
			Exact(@"""•téŝt•""", F.Literal("•téŝt•").SetBaseStyle(NodeStyle.Default));
			Exact(@"'''''\'/ \'/'''", F.Literal("''' '").SetBaseStyle(NodeStyle.TQStringLiteral));
			Exact(@"'''\x/\z/ ""\'/'''", F.Literal(@"\x/\z/ ""'").SetBaseStyle(NodeStyle.TQStringLiteral));
			Exact(@"'''\r/\0/ '\'/'''", F.Literal("\r\0 ''").SetBaseStyle(NodeStyle.TQStringLiteral));
			Exact("{\n  ''' Line 1\n      Line 2\n     '''\n}", F.Braces(F.Literal(" Line 1\n Line 2\n").SetBaseStyle(NodeStyle.TQStringLiteral)));
			// Parser-focused tests
			Stmt ("{\n  ''' Line 1\n      Line 2\n '''\n}",     F.Braces(F.Literal(" Line 1\n Line 2\n").SetBaseStyle(NodeStyle.TQStringLiteral)));
			Stmt ("{\n\t''' Line 1\n\t    Line 2\n\t\t '''\n}", F.Braces(F.Literal(" Line 1\n Line 2\n ").SetBaseStyle(NodeStyle.TQStringLiteral)));
			Stmt ("{\n  '''\tLine 1\n \tLine 2\n  \tLine 3'''\n}", F.Braces(F.Literal("\tLine 1\n\tLine 2\nLine 3").SetBaseStyle(NodeStyle.TQStringLiteral)));
			// Parser tests. Printer will print \u1234\uABCD and \n and \t as characters, so don't use exact matching
			Stmt (@"""\u1234\uABCD\x12""", F.Literal("\u1234\uABCD\x12").SetBaseStyle(NodeStyle.Default));
			Stmt (@"'''\\r/\n/\t/\0/ '\'/'''", F.Literal("\\\r\n\t\0 ''").SetBaseStyle(NodeStyle.TQStringLiteral));
			Stmt (@"""""""\\/\r/\n/\t/\0/ \""/""""""", F.Literal("\\\r\n\t\0 \"").SetBaseStyle(NodeStyle.TDQStringLiteral));
			// Ensure that the printer doesn't print false escape sequences
			Exact(@"'''\\/r/\\/0/\\/n/\\/t/\\/\/'''", F.Literal(@"\r/\0/\n/\t/\\/").SetBaseStyle(NodeStyle.TQStringLiteral));
		}

		[Test]
		public void Utf8BasedEscapeSequences()
		{
			// Astral characters are stored as surrogate pairs in C#
			// and are printed as 6-digit escapes by the printer.
			Exact(@"""\u01F4A9.\u10FFFF""", F.Literal("\uD83D\uDCA9.\uDBFF\uDFFF").SetBaseStyle(NodeStyle.Default));
			// Invalid UTF-8 bytes are transliterated to 0xDCxx bytes.
			// High surrogates (0xD800..0xDBFF) are left alone.
			Exact(@"""\xFF.\uD800""", F.Literal("\uDCFF.\uD800").SetBaseStyle(NodeStyle.Default));
		}

		[Test]
		public void StringLiteralsWithArbitraryBytes()
		{
			Exact(@"""é""",        F.Literal("é"));
			Exact(@"""\u01F4A9""", F.Literal("\uD83D\uDCA9")); // 💩 pile of poo U+1F4A9
			Stmt (@" ""\u1F4A9""", F.Literal("\uD83D\uDCA9")); // Printer uses 6 digits, only 5 needed
			Exact(@"""\x1B\xFF""", F.Literal("\x1B\uDCFF"));
			// Triple-quote request is ignored if the string contains invalid UTF-8,
			// since triple-quoted strings do not support \xNN or \uNNNN escapes.
			Exact(@"""\x1B\xFF""", F.Literal("\x1B\uDCFF").SetBaseStyle(NodeStyle.TQStringLiteral));
			Exact("'''<\uD83D\uDCA9>'''", F.Literal("<\uD83D\uDCA9>").SetBaseStyle(NodeStyle.TQStringLiteral));
		}

		[Test]
		public void NamedFloatLiteral()
		{
			Exact("@@-inf.f", F.Literal(float.NegativeInfinity));
			Exact("@@inf.f", F.Literal(float.PositiveInfinity));
			Exact("@@nan.f", F.Literal(float.NaN));
			Exact("@@-inf.d", F.Literal(double.NegativeInfinity));
			Exact("@@inf.d", F.Literal(double.PositiveInfinity));
			Exact("@@nan.d", F.Literal(double.NaN));
		}

		[Test]
		public void CustomLiterals()
		{
			Exact(@"special""!""",      F.Literal(new CustomLiteral("!", (Symbol)"special")));
			Exact(@"special'''!'''",    F.Literal(new CustomLiteral("!", (Symbol)"special")).SetBaseStyle(NodeStyle.TQStringLiteral));
			Exact(@"@@unknown-literal", F.Literal(new CustomLiteral("unknown-literal", (Symbol)"@@")));
			Exact(@"123.5f00bar",       F.Literal(new CustomLiteral("123.5", (Symbol)"f00bar")));
			Exact(@"f00bar""0x1234""",  F.Literal(new CustomLiteral("0x1234", (Symbol)"f00bar")));
			Exact(@"`WTF!\n`""0x1234""",F.Literal(new CustomLiteral("0x1234", (Symbol)"WTF!\n")));
			// Backquotes added due to 'e' in error which resembles an exponent
			Exact(@"123.5`error`",      F.Literal(new CustomLiteral("123.5", (Symbol)"error")));
			// Parses OK but printer currently prints as in string form
			Stmt (@"0x123special",      F.Literal(new CustomLiteral("0x123", (Symbol)"special")).SetBaseStyle(NodeStyle.HexLiteral));
			Stmt (@"0x1234`f00bar`",    F.Literal(new CustomLiteral("0x1234", (Symbol)"f00bar")).SetBaseStyle(NodeStyle.HexLiteral));
		}

		[Test]
		public void NegativeLiteral()
		{
			Exact("-x", F.Call(S.Sub, x));
			Exact("- 2", F.Call(S.Sub, two));
			Exact("-2", F.Literal(-2));
			Stmt("-111222333444", F.Literal(-111222333444));
			Exact("-2L", F.Literal(-2L));
			Stmt("-2.0", F.Literal(-2.0));
			Stmt("-2d", F.Literal(-2.0));
			Exact("-2f", F.Literal(-2.0f));
			Stmt("-2.0f", F.Literal(-2.0f));
		}

		[Test]
		public void BigIntLiterals()
		{
			Exact(@"123z", F.Literal(new BigInteger(123)));
			// TODO: add underscores in printer
			Stmt (@"9_876_543_210z", F.Literal(new BigInteger(9876543210)));
			// TODO: don't print this as a string in printer
			Stmt ("-2z", F.Literal(new BigInteger(-2)));
		}
		
		#endregion

		#region Basic expressions: calls, unary & binary operators

		[Test]
		public void SimpleCalls()
		{
			Exact("x", x);
			Exact("x()", F.Call(x));
			Exact("Foo(a, b, c)", F.Call(Foo, a, b, c));
			Exact("Foo(a(b, c), b(c))", F.Call(Foo, F.Call(a, b, c), F.Call(b, c)));
		}

		[Test]
		public void LiteralKeywords()
		{
			Exact(@"x(`true`, `false`, `null`)", F.Call(x, F.Id("true"), F.Id("false"), F.Id("null")));
			Expr (@"x( true,   false,   null)", F.Call(x, F.Literal(true), F.Literal(false), F.Literal(null)));
		}

		[Test]
		public void BinaryOps()
		{
			Exact("x + 1",        F.Call(S.Add, x, one));
			Exact("x * 2 + 1",    F.Call(S.Add, F.Call(S.Mul, x, two), one));
			Exact("a + b + 1",    F.Call(S.Add, F.Call(S.Add, a, b), one));
			Exact("a = b = 0",    F.Call(S.Assign, a, F.Call(S.Assign, b, zero)));
			Exact("a >= b .. c",  F.Call(S.GE, a, F.Call(S.DotDot, b, c)));
			Exact("a == b && c != 0", F.Call(S.And, F.Call(S.Eq, a, b), F.Call(S.Neq, c, zero)));
			Exact("(a ? b : c)",  F.InParens(F.Call(S.QuestionMark, a, F.Call(S.Colon, b, c))));
			Exact("a ?? b <= c",  F.Call(S.LE, F.Call(S.NullCoalesce, a, b), c));
			Exact("a - b / c**2", F.Call(S.Sub, a, F.Call(S.Div, b, F.Call(S.Exp, c, two))));
			Exact("a >>= 1",      F.Call(S.ShrAssign, a, one));
			Exact("a.b?.c(x)",    F.Call(S.NullDot, F.Dot(a, b), F.Call(c, x)));
			
			// Custom ops
			Exact("a |-| b + c",   F.Call("'|-|", a, F.Call(S.Add, b, c)));
			Exact("a.b!!!c .?. 1", F.Call("'.?.", F.Call("'!!!", F.Dot(a, b), c), one));
			Exact("a +/ b *+ c",   F.Call("'+/", a, F.Call("'*+", b, c)));
		}

		[Test]
		public void SingleQuotedBinaryOps()
		{
			Stmt ("x '* 2 '+ 1", F.Call(S.Add, F.Call(S.Mul, x, two), one));
			Stmt ("a '>= 0 '&& b '> 1", F.Call(S.And, F.Call(S.GE, a, zero), F.Call(S.GT, b, one)));
			Exact("a '*s b '>u c", F.Call("'>u", F.Call("'*s", a, b), c));
		}

		[Test]
		public void SingleQuotedNamedOps()
		{
			Exact(@"a 'x b 'y c",           Op(F.Call(_("'x"), a, Op(F.Call(_("'y"), b, c)))));
			Exact(@"a 'X b 'Y c",           Op(F.Call(_("'Y"), Op(F.Call(_("'X"), a, b)), c)));
			Exact(@"a 'implies b 'Likes c", Op(F.Call(_("'implies"), a, Op(F.Call(_("'Likes"), b, c)))));
			Exact(@"a 'implies b == c",     Op(F.Call(_("'implies"), a, Op(F.Call(S.Eq, b, c)))));
			Exact(@"a 'Likes b && b 'Likes c", Op(F.Call(S.And, Op(F.Call(_("'Likes"), a, b)), Op(F.Call(_("'Likes"), b, c)))));
		}

		[Test]
		public void PrefixOps()
		{
			Exact("-a * b", F.Call(S.Mul, F.Call(S._Negate, a), b));
			Stmt ("-x ** +x / ~x + &x & *x && !x == ^x",
				F.Call(S.And, F.Call(S.AndBits, F.Call(S.Add, F.Call(S.Div, F.Call(S.Exp,
					F.Call(S._Negate, x), F.Call(S._UnaryPlus, x)), F.Call(S.NotBits, x)),
					F.Call(S._AddressOf, x)), F.Call(S._Dereference, x)),
					F.Call(S.Eq, F.Call(S.Not, x), F.Call(S.XorBits, x))));
			Exact("| a = %b", F.Call(S.OrBits, F.Call(S.Assign, a, F.Call(S.Mod, b))));
			Exact(".. a + b && c", F.Call(S.And, F.Call(S.DotDot, F.Call(S.Add, a, b)), c));
			Exact("$a / $*b", F.Call(S.Div, F.Call(S.Substitute, a), F.Call("'$*", b)));
			Exact("/x", F.Call(S.Div, x));
		}

		[Test]
		public void SuffixOps()
		{
			Stmt("a++ + ++a", F.Call(S.Add, F.Call(S.PostInc, a), F.Call(S.PreInc, a)));
			Stmt(@"a.b --", F.Call(@"'--suf", F.Call(S.Dot, a, b)));
			Stmt(@"a + b -<>-", F.Call(S.Add, a, F.Call(@"'-<>-suf", b)));
			// Ensure printer isn't confused by "suf" suffix which also appears on suffix operators
			Exact(@"do_suf(x)", F.Call(@"do_suf", x).SetBaseStyle(NodeStyle.Operator));
			Exact(@"`'do_suf`(x)", F.Call(@"'do_suf", x).SetBaseStyle(NodeStyle.Operator));
		}

		[Test]
		public void SubtractNegativeLiteral()
		{
			Expr("a-b", F.Call(S.Sub, a, b));
			Expr("x-2", F.Call(S.Sub, x, F.Literal(2)));
		}

		[Test]
		public void TrickyPrinterCases()
		{
			// Gotta be careful how we print operators that appear to start comments,
			// and suffix operators used as prefix/infix.
			Exact(@"a '+/* b", Op(F.Call(_("'+/*"), a, b)));
			Exact(@"a '>s b",  Op(F.Call(_("'>s"), a, b)));
			Exact(@"a '/* b",  Op(F.Call(_("'/*"), a, b)));
			Exact(@"a '// b",  Op(F.Call(_("'//"), a, b)));
			Exact(@"a '++suf b", Op(F.Call(_("'++suf"), a, b)));
			Stmt (@"`'/\\`(b)", Op(F.Call(_(@"'/\"), b)));
		}

		[Test]
		public void DollarSignOnlyAtStartOfOperator()
		{
			Stmt (@"a-$b",     F.Call(S.Sub, a, F.Call(S.Substitute, b)));
			Stmt (@"-$b",      F.Call(S.Sub, F.Call(S.Substitute, b)));
			Exact(@"`'-$`(b)", F.Call("'-$", b));
			Exact(@"$-b",      F.Call("'$-", b));
			Stmt (@"a-$-b",    F.Call(S.Sub, a, F.Call("'$-", b)));
			Exact(@"a - $-b",  F.Call(S.Sub, a, F.Call("'$-", b)));
			// TODO: need resolution in specification: $ is only for prefix ops,
			// but ' is only for infix ops, so how do we treat '$-?
			//Exact(@"a '$- b",  F.Call("'$-", a, b));
		}

		#endregion

		#region Braces, tuples, lists

		[Test]
		public void Tuples()
		{
			Stmt("(a)", F.InParens(a));
			Stmt("(a;)", F.Tuple(a));
			Stmt("(;)", F.Tuple(_("")));
			Exact("(``;)", F.Tuple(_("")));
			Stmt("(a; ;)", F.Tuple(a, _("")));
			Exact("(a; ``;)", F.Tuple(a, _("")));
			Stmt("(a; b)", F.Tuple(a, b));
			Stmt("(a; b; c + x)", F.Tuple(a, b, F.Call(S.Add, c, x)));
		}

		[Test]
		public void Stmts()
		{
			Test(Mode.Stmt, -1, "a;\nb;\nc", a, b, c);
			Stmt("a.b(c)", F.Call(F.Dot(a, b), c));
			Expr("{\n  b(c);\n} + {\n  ;\n  Foo()\n}", F.Call(S.Add, F.Braces(F.Call(b, c)), F.Braces(F.Missing, F.Call(Foo))));
			Stmt("a.{\n  b;\n  c;\n}()", F.Call(F.Dot(a, F.Braces(b, c))));
			Stmt(@"{ ""key"": ""value"", {""KEY"": ""VALUE""} }", F.Braces(
				F.Call(S.Colon, F.Literal("key"), F.Literal("value")), F.Braces(
				F.Call(S.Colon, F.Literal("KEY"), F.Literal("VALUE")))));
		}

		[Test]
		public void ListLiterals()
		{
			Exact(@"x = [1, 2, ""three""]", F.Call(S.Assign, x, 
				F.Call(S.Array, one, two, F.Literal("three"))));
			Stmt(@"{ ""a"" : [null], ""b"" : [] }", F.Braces(
				F.Call(S.Colon, F.Literal("a"), F.Call(S.Array, F.Null)), 
				F.Call(S.Colon, F.Literal("b"), F.Call(S.Array))));
			Exact("++[x]", F.Call(S.PreInc, F.Call(S.Array, x)));
			Exact("Foo = [a, b, c]", F.Call(S.Assign, Foo, F.Call(S.Array, a, b, c)));
			Exact("Foo = [a, b, c] + [x]", F.Call(S.Assign, Foo, F.Call(S.Add, F.Call(S.Array, a, b, c), F.Call(S.Array, x))));
		}

		#endregion
		
		#region Generics, indexers

		[Test]
		public void Generics()
		{
			Expr("a!b", F.Of(a, b));
			Expr("a!(b)", F.Of(a, b));
			Expr("a!(b, c)", F.Of(a, b, c));
			Expr("a!()", F.Of(a));
			Expr("a.b!((x))", F.Of(F.Dot(a, b), F.InParens(x)));
			Expr("a.b!Foo(x)", F.Call(F.Of(F.Dot(a, b), Foo), x));
			Expr("a.b!(Foo.Foo)(x)", F.Call(F.Of(F.Dot(a, b), F.Dot(Foo, Foo)), x));
			Expr("a.b!(Foo(x))", F.Of(F.Dot(a, b), F.Call(Foo, x)));
			// This last one is meaningless in most programming languages, but LES does not judge
			Stmt("Foo = a.b!c!x", F.Call(S.Assign, Foo, F.Of(F.Of(F.Dot(a, b), c), x)));
		}
		
		[Test]
		public void IndexBracks()
		{
			Exact("a[]", F.Call(S.IndexBracks, a));
			Exact("a[b]", F.Call(S.IndexBracks, a, b));
			Exact("a[b, c]", F.Call(S.IndexBracks, a, b, c));
		}

		#endregion

		#region Attributes

		[Test]
		public void SimpleAttributes()
		{
			Exact("@Foo a = b(c)",     F.Attr(Foo, F.Call(S.Assign, a, F.Call(b, c))));
			Exact("@Foo (a)(b)",       F.Attr(Foo, F.Call(F.InParens(a), b)));
			Exact("@123 Foo(x)",       F.Attr(F.Literal(123), F.Call(Foo, x)));
			Exact("@'!' Foo(x)",       F.Attr(F.Literal('!'), F.Call(Foo, x)));
			Exact("@-12 Foo(x)",       F.Attr(F.Literal(-12), F.Call(Foo, x)));
		}

		[Test]
		public void BraceAndBrackAttributes()
		{
			Exact("@[1, 2] Foo(x)",    F.Attr(F.Call(S.Array, one, two), F.Call(Foo, x)));
			Exact("@{\n  a\n} Foo(x)", F.Attr(F.Braces(a), F.Call(Foo, x)));
		}

		[Test]
		public void ParenAttributes()
		{
			Stmt("@(Foo) a = b(c)",    F.Attr(Foo, F.Call(S.Assign, a, F.Call(b, c))));
			Exact("@((Foo)) b(c)",     F.Attr(F.InParens(Foo), F.Call(b, c)));
			Stmt("@(Foo(x)) a = b(c)", F.Attr(F.Call(Foo, x), F.Call(S.Assign, a, F.Call(b, c))));
			Exact("@(@0 Foo(x)) b(c)", F.Attr(F.Attr(zero, F.Call(Foo, x)), F.Call(b, c)));
			Exact("@(a == c) b(c)",    F.Attr(F.Call(S.Eq, a, c), F.Call(b, c)));
			Exact("(@Foo a) = b(c)",   F.Call(S.Assign, F.Attr(Foo, a), F.Call(b, c)));
			Exact("((@Foo a)) = b(c)", F.Call(S.Assign, F.InParens(F.Attr(Foo, a)), F.Call(b, c)));
			Exact("(@Foo (a)) = b(c)", F.Call(S.Assign, F.Attr(Foo, F.InParens(a)), F.Call(b, c)));
		}

		#endregion

		#region Block expressions, juxtaposition, and keyword statements

		[Test]
		public void BlockCallExpressions()
		{
			Exact("a (b) {\n  c\n}", F.Call(a, b, F.Braces(c)));
			Exact("a (b, c) { }", F.Call(a, b, c, F.Braces()));
			Exact("Foo = a (b) {\n  c\n}", F.Call(S.Assign, Foo, F.Call(a, b, F.Braces(c))));
			Exact("Foo = a (b) {\n  c\n} + x", F.Call(S.Assign, Foo, F.Call(S.Add, F.Call(a, b, F.Braces(c)), x)));
			Exact("Foo = if (c) {\n  a\n} else {\n  b\n}", F.Call(S.Assign, Foo, F.Call(_("if"), c, F.Braces(a), F.Call(_("'else"), F.Braces(b)))));
			Exact("Foo = quote {\n  a\n}", F.Call(S.Assign, Foo, F.Call(_("quote"), F.Braces(a))));
			Exact("Foo = do {\n  a\n} where(b)", F.Call(S.Assign, Foo, F.Call(_("do"), F.Braces(a), F.Call(_("'where"), b))));
		}

		[Test]
		public void BlockExpressionsWithoutOptionalSemicolon()
		{
			Test(Mode.Stmt, 0, "a (b) { c; }\n a { c; }\n a()", F.Call(a, b, F.Braces(c)), 
			                                                     F.Call(a, F.Braces(c)), F.Call(a));
		}

		[Test]
		public void KeywordStatements()
		{
			Exact(".return (x + 1)",  F.Call(".return", F.InParens(F.Call(S.Add, x, one))).SetBaseStyle(NodeStyle.Special));
			Exact(".return x + 1",     F.Call(".return", F.Call(S.Add, x, one)).SetBaseStyle(NodeStyle.Special));
			Exact(".if Foo {\n  a\n}", F.Call(".if", Foo, F.Braces(a)).SetBaseStyle(NodeStyle.Special));
			Exact(".if Foo {\n  a\n} else {\n  b\n}", F.Call(".if", Foo, F.Braces(a), 
				F.Call("'else", F.Braces(b))).SetBaseStyle(NodeStyle.Special));
			Stmt (".class Foo!T where T : IFoo {\n  a\n}", F.Call(".class", 
				F.Call("'where", F.Of(Foo, T), F.Call(S.Colon, T, F.Id("IFoo"))), 
				F.Braces(a)).SetBaseStyle(NodeStyle.Special));
		}

		[Test]
		public void KeywordStatementsWithNestedBlockCalls()
		{
			// Check that block-calls ARE allowed inside parens and bracks, even 
			// though they are not allowed at the top level.
			Exact(".return (Foo { }) { }", F.Call(".return", 
				F.InParens(F.Call(Foo, F.Braces())), F.Braces()).SetBaseStyle(NodeStyle.Special));
			Exact(".return a(Foo { }) { }", F.Call(".return", 
				F.Call(a, F.Call(Foo, F.Braces())), F.Braces()).SetBaseStyle(NodeStyle.Special));
			Exact(".return [Foo { }] + x[Foo { }] { }", F.Call(".return", F.Call(S.Add, 
				F.Call(S.Array, F.Call(Foo, F.Braces())), F.Call(S.IndexBracks, x, F.Call(Foo, F.Braces()))),
				F.Braces()).SetBaseStyle(NodeStyle.Special));
			// A block can also appear as a particle
			Exact(".return a + {\n  b\n} { }", F.Call(".return", F.Call(S.Add, a, F.Braces(b)), F.Braces()).SetBaseStyle(NodeStyle.Special));
			Exact(".return {\n  b\n} { }",     F.Call(".return",                  F.Braces(b), F.Braces()).SetBaseStyle(NodeStyle.Special));
		}

		[Test]
		public void KeywordStatementPrinterCheck()
		{
			// Make sure the printer doesn't allow a block call directly inside a keyword statement
			Exact(".return Foo({ }) { }", F.Call(".return", 
				F.Call(Foo, F.Braces()), F.Braces()).SetBaseStyle(NodeStyle.Special));
			Exact(".return (Foo { };) + x({ }) { }", F.Call(".return", F.Call(S.Add,
				F.Tuple(F.Call(Foo, F.Braces())), F.Call(x, F.Braces())),
				F.Braces()).SetBaseStyle(NodeStyle.Special));
		}

		#endregion

		[Test]
		public void PrinterSpacingMinefield()
		{
			// Challenges involving `-`
			Exact("- 2", F.Call(S.Sub, two));
			Exact("- 2.5", F.Call(S.Sub, F.Literal(2.5)));
			Exact("+ -2", F.Call(S.Add, F.Literal(-2)));
			Exact("+- 2", F.Call("'+-", F.Literal(2)));
			Exact("-(2.5)", F.Call(S.Sub, F.InParens(F.Literal(2.5))));
			Exact("-x** +x", F.Call(S.Exp, F.Call(S._Negate, x), F.Call(S._UnaryPlus, x)));
			// Challenges involving `.`
			Exact("x.x", F.Dot(x, x));
			Exact("2 .x", F.Dot(two, x));
			Exact("x. 2", F.Dot(x, two));
			Exact("1 . 2", F.Dot(one, two));
			Exact("1 'x. 2", F.Call("'x.", one, two));
			Exact("@@inf.d .Foo", F.Dot(F.Literal(double.PositiveInfinity), Foo));
		}

		[Test]
		public void PrecedenceChallenge()
		{
			Exact("a.(@@ -b)",    F.Dot(a, F.Call(S._Negate, b)));
			Exact("a.(@@ -b)(x)", F.Call(F.Dot(a, F.Call(S._Negate, b)), x));
		}

		[Test]
		public void TriviaTest_Comments()
		{
			LNode node;
			node = Foo.PlusAttr(F.Trivia(S.TriviaMLComment, " before ")).PlusTrailingTrivia(F.Trivia(S.TriviaMLComment, " after "));
			Exact("/* before */Foo /* after */", node);
			node = one.PlusAttr(F.Trivia(S.TriviaSLComment, " before ")).PlusTrailingTrivia(F.Trivia(S.TriviaSLComment, " after "));
			Exact("// before \n1\t// after ", node);

			Exact("Foo(a,\t// Commentary\n  b)",
				F.Call(Foo, a.PlusTrailingTrivia(F.Trivia(S.TriviaSLComment, " Commentary")), OnNewLine(b)));

			node = F.Call(F.Id(S.Eq).PlusAttr(                 F.Trivia(S.TriviaMLComment, "[")).PlusTrailingTrivia(F.Trivia(S.TriviaSLComment, "]")), 
			                 Foo, x.PlusAttrs(F.TriviaNewline, F.Trivia(S.TriviaMLComment, "x"))
			                                 ).PlusAttr(F.Trivia(S.TriviaMLComment, " before ")).PlusTrailingTrivia(F.Trivia(S.TriviaMLComment, " after "));
			Exact("/* before */Foo /*[*/==\t//]\n /*x*/x /* after */", node);

			node = F.Call(Foo).PlusAttrs(a, F.Trivia(S.TriviaSLComment, "Comment after a"),
				b, F.Trivia(S.TriviaMLComment, "Comment before c"), c);
			Exact("@a //Comment after a\n"+
			      "@b /*Comment before c*/@c Foo()", node);
		}

		[Test]
		public void TriviaTest_Appending()
		{
			var append = F.Id(S.TriviaAppendStatement);
			LNode[] stmts = {
				F.Braces(F.Call(a), F.Call(b)).SetStyle(NodeStyle.OneLiner)
			};
			Test(Mode.Exact, 0, "{ a(); b() }", stmts);
			stmts = new[] {
				F.Call(a), F.Call(b), F.Call(c).PlusAttr(append)
			};
			Test(Mode.Exact, 0, "a()\nb(); c()", stmts);
		}

		[Test]
		public void TriviaTest_LineBreakBetweenAttrs()
		{
			Exact("@a \nFoo()", F.Call(Foo).PlusAttrs(a, F.TriviaNewline));
			Exact("@a @b \n@c \nFoo()", F.Call(Foo).PlusAttrs(a, b, F.TriviaNewline, c, F.TriviaNewline));
		}

		[Test]
		public void TriviaTest_BlankLinesBetweenStmts()
		{
			Test(Mode.Exact, 0, "a()\n\nb()\n\nc()",
				NewlineAfter(F.Call(a)),
				NewlineAfter(F.Call(b)),
				F.Call(c));
			Exact("{\n\n  a()\n  \n  b()\n  \n  c()\n  \n}",
				F.Call(NewlineAfter(F.Id(S.Braces)),
					NewlineAfter(F.Call(a)),
					NewlineAfter(F.Call(b)),
					NewlineAfter(F.Call(c))));
		}

		public void TriviaTest_BlankLinesBetweenArgs()
		{
			Exact("Foo(\n  a, \n  b, \n  c)",
				F.Call(Foo, OnNewLine(a), OnNewLine(b), OnNewLine(c)));

			Exact("Foo(\n  a, \n  \n  b, \n  // see?\n  c)",
				F.Call(Foo, NewlineAfter(OnNewLine(a)),
					NewlineAfter(OnNewLine(b)),
					c.PlusAttr(F.Trivia(S.TriviaSLComment, " see?"))));

			// TODO: this fails in printer because the newline after Foo cannot
			// be printed before '(' and is suppressed. Solution will be to wait
			// until after '(' before printing trivia attached to Foo, but this
			// is not easy to accomplish.
			//Test("Foo(\n  \n  a, \n  \n  b, \n  \n  c)",
			//	F.Call(NewlineAfter(Foo), NewlineAfter(OnNewLine(a)),
			//		NewlineAfter(OnNewLine(b)),
			//		OnNewLine(c)));
		}

		protected virtual void Expr(string text, LNode expr, int errorsExpected = 0)
		{
			Test(Mode.Expr, errorsExpected, text, new[] { expr });
		}
		protected virtual void Stmt(string text, LNode code, int errorsExpected = 0)
		{
			Test(Mode.Stmt, errorsExpected, text, new[] { code });
		}
		protected virtual void Exact(string text, LNode code, int errorsExpected = 0)
		{
			Test(Mode.Exact, errorsExpected, text, new[] { code });
		}

		/// <summary>Runs a printer or parser test.</summary>
		/// <param name="parseErrors">-1 if the printer and parser should both 
		/// test this example. If above -1, only the parser will run this example,
		/// and this parameter specifies the number of parse errors to expect 
		/// (may be 0).</param>
		protected abstract MessageHolder Test(Mode mode, int parseErrors, string text, params LNode[] code);
		protected enum Mode
		{
			Expr = 0,  // Parse expression list
			Stmt = 1,  // Parse statement list
			Exact = 3, // Parse statements, and expect exact (rather than equivalent) printer output
		}

		protected void ExpectMessageContains(MessageHolder messages, params string[] substrings)
		{
			foreach (var msg in messages.List)
				for (int i = 0; i < substrings.Length; i++)
					if (msg.Formatted.IndexOf(substrings[i], StringComparison.InvariantCultureIgnoreCase) > -1)
						substrings[i] = null;
			Assert.AreEqual(null, substrings.WhereNotNull().FirstOrDefault());
		}
	}
}
