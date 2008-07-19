using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Loyc.Utilities;

namespace Loyc.CompilerCore
{
	public class StringCharSource : CharIndexPositionMapper
	{
		public StringCharSource(string text) 
			{ _text = text; }
		public StringCharSource(string text, SourcePos startingPos) : base(startingPos)
			{ _text = text; }

		protected readonly string _text;
		public const int EOF = -1;
		
		public override int this[int index] { get {
			if (index < 0 || index >= _text.Length)
				return EOF;
			else
				return _text[index]; 
		} }
		public override int Count { get { return _text.Length; } }
		public override string Substring(int startIndex, int length) 
			{ return _text.Substring(startIndex, length); }

		// For now we'll put some line tracking here. In the end it may be
		// better to abstract it out of here (although the public functions will 
		// stay here, of course). Note! line/col numbers start at 0, as do
		// positions within a line.
	}
	public class StringCharSourceFile : StringCharSource, ISourceFile
	{
		public StringCharSourceFile(ILanguageStyle language, string text) 
			: base(text) { _language = language; }
		public StringCharSourceFile(ILanguageStyle language, string text, SourcePos startingPos)
			: base(text, startingPos) { _language = language; }
		protected ILanguageStyle _language;
		public ILanguageStyle Language { get { return _language; } }
	}

	[TestFixture]
	public class StringCharSourceTests : CharIndexPositionMapperTests
	{
		protected override CharIndexPositionMapper CreateSource(string s)
		{
			return new StringCharSource(s);
		}
	}
}