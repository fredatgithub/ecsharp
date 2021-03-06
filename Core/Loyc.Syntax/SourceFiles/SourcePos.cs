using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using Loyc.MiniTest;
using Loyc.Utilities;

namespace Loyc.Syntax
{
	/// <summary>Holds a line number (Line) and a position in the line (PosInLine).
	/// This class isn't really needed in Loyc but is separated from SourcePos 
	/// in case anyone might want position without a filename.</summary>
	/// <remarks>Numbering starts at one for both Line and PosInLine. 
	/// Line=0 signifies nowhere in particular, or an unknown location.</remarks>
	public class LineAndCol
	{
		protected LineAndCol() { }
		public LineAndCol(int Line, int PosInLine)
			{ _line = Line; _posInLine = PosInLine; }// this.FileName = FileName; }

		protected int _line;
		protected int _posInLine;
		public int Line { get { return _line; } }
		public int PosInLine { get { return _posInLine; } }
		
		public override string ToString()
		{
			if (Line <= 0)
				return "Nowhere";
			else
				return string.Format("{0}:{1}", Line, PosInLine);
		}

		public override bool Equals(object obj)
		{
			LineAndCol other = obj as LineAndCol;
			if (other == null)
				return false;
			return other._line == _line && other._posInLine == _posInLine;
		}
		public override int GetHashCode()
		{
			return (_line << 4) ^ _posInLine;
		}
		public static LineAndCol Nowhere = new LineAndCol();
	}

	/// <summary>Holds a filename (FileName), a line number (Line) and a position in 
	/// the line (PosInLine), representing a position in a source code file.</summary>
	/// <remarks>
	/// Line and column numbering both start at one (1). Line=0 signifies nowhere 
	/// in particular. Instances are immutable.
	/// </remarks>
	public class SourcePos : LineAndCol
	{
		protected SourcePos() { }
		public SourcePos(string FileName, int Line, int PosInLine)
			: base(Line, PosInLine) { _fileName = FileName ?? ""; }
		
		protected string _fileName;
		public string FileName { get { return _fileName; } }

		public override string ToString()
		{
			if (Line <= 0)
				return "Nowhere";
			else
				return string.Format("{0}({1},{2})", FileName, Line, PosInLine);
		}
		public override bool Equals(object obj)
		{
			SourcePos other = obj as SourcePos;
			if (other == null)
				return false;
			return other._fileName == _fileName && base.Equals(obj);
		}
		public override int GetHashCode()
		{
			return base.GetHashCode() ^ _fileName.GetHashCode();
		}
		new public static SourcePos Nowhere = new SourcePos();
	}
}
