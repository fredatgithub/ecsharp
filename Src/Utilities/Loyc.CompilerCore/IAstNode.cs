using System;
using System.Collections.Generic;
using System.Text;
//using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using Loyc.Runtime;

namespace Loyc.CompilerCore
{
	/// <summary>A simplified token interface (used for example by 
	/// EnumerableSource) that offers the Type, Text, and Position properties.
	/// </summary>
	public interface ITokenValueAndPos : ITokenValue
	{
		/// <summary>Returns the source file and position therein the best 
		/// represents this node. Typically it is the position of the beginning
		/// of the text from which this node was created.</summary>
		/// <remarks>If the position cannot be described by a line and position 
		/// (e.g. because it's a synthetic token and not from a real file) then 
		/// the return value can be SourcePos.Nowhere. However, if a node is 
		/// synthetic, the position of an existing token should usually be used, 
		/// so that if an error occurs regarding this node, a relevant position 
		/// can be reported to the user.</remarks>
		SourcePos Position { get; }
	}

	public interface IBaseNode : ITokenValueAndPos
	{
        /// <summary>Returns a dictionary that can be used to store additional state
        /// beyond the content of the token or node.
        /// </summary><remarks>
        /// Extra is never null or read-only.
        /// 
		/// Extra should normally hold transient information, not information that 
		/// is part of the node's syntax. However, the node can also use this 
		/// dictionary to hold the normal properties of the node, such as Block, 
		/// Attrs, BriefText, etc. In this case, the symbol name should begin with 
		/// an underscore to differentiate it from transient state, e.g. :_Block, 
		/// :_Attrs, :_BriefText. This is the recommended way to store properties
		/// that are normally null. For example, a node designed to store expressions
		/// normally has a list of Params but not Attrs or Block. So rather than 
		/// having two references that are usually null, They should be placed in
		/// the Extra dictionary only on request.
		/// 
		/// By using ExtraAttributes(of object) as a node's base class, so that
		/// Extra==this, overhead is reduced because a separate dictionary object is
		/// not needed for every node.
        /// </remarks>
		IDictionary<Symbol, object> Extra { get; }
	}

	/// <summary>
	/// IAstNode is the interface for all nodes in a Loyc Abstract Syntax Tree 
	/// (AST). However, all Loyc AST nodes are derived from <see
	/// cref="Loyc.CompilerCore.AstNode"/> and it is recommended that you access
	/// nodes through that class.
	/// </summary><remarks>
	/// IAstNode returns lists of sub-nodes as IList(of IAstNode), which causes
	/// AstList to be boxed, so IAstNode has an efficiency problem. Also, although
	/// AstList implements IList(of IAstNode), it does not allow you to put any
	/// node in the list that only implements IAstNode; the nodes in that list 
	/// must be derived from AstNode.
	/// </remarks>
	public interface IAstNode : IBaseNode
	{
		/*
		/// <summary>Returns the parent node in the original source file, if
		/// any. The parent of a SourceFile is a SourceFileList. This is a 
		/// write-once property; once a node has a parent, it cannot be 
		/// changed.</summary>
		/// <remarks>If the node is synthetic (generated), the parent should
		/// be an appropriate closely related node.
		/// 
		/// It's important to note that a ICodeNode may be the child of any 
		/// number of others, and the parent, after being manipulated by extensions, 
		/// may not even have this node as a child. Therefore, do not rely on this 
		/// value as a way to return to the correct parent after traversing down 
		/// a tree of nodes.
		/// 
		/// This property should be set for tokens during the tree parsing phase,
		/// and during the main parsing phase for other nodes in a source file.
		/// </remarks>
		IAstNode LexicalParent { get; set; }

		/// <summary>Returns a summary of the content of the node in the style
		/// of the source language. This method should try to keep the token 
		/// under 40 characters, but it is not a requirement.</summary>
		string BriefText { get; }
		*/

		/// <summary>Range of positions that this node uses in an ICharSource.</summary>
		SourceRange Range { get; }

		/// <summary>Returns the name of this node, e.g. in the node for "class Foo
		/// {}", the name would be "Foo"; in the node for "2+2", the name would be
		/// "+".</summary>
		string Name { get; set; }

		/// <summary>Returns the parsed value of the token, such as a string, an
		/// integer, an AST node, or something else depending on the token type. If
		/// the token does not need to be parsed, or is not able to parse itself, it may
		/// return null. The implementor can decide whether the setter should work; it 
		/// may throw an exception instead.</summary>
		object Content { get; set; }

		/// <summary>Returns the list of attributes that the code applies to this
		/// node. If the node cannot have attributes, this may return null or a
		/// read-only, empty list.</summary>
		IList<IAstNode> Attrs { get; }
		/// <summary>Returns the list of generic parameters that the code supplies
		/// in this node. If the node cannot take generic parameters, this may
		/// return null or a read-only, empty list.</summary>
		IList<IAstNode> OfParams { get; }
		/// <summary>Returns the list of parameters that the code supplies in this
		/// node. If the node cannot take parameters, this may return null or a
		/// read-only, empty list.</summary>
		IList<IAstNode> Params { get; }
		/// <summary>Returns the list of substatements or other arbitrary-length
		/// children that the code supplies as children of this node. If the node cannot
		/// have substatements, this may return null or a read-only, empty list.</summary>
		/// <remarks>Conventionally, token lists are not hierarchical, but in 
		/// Loyc, the Essential Tree Parser makes a token tree, as explained
		/// in the Loyc design overview under "Essential tree parsing (ETP)".
		/// Child tokens are placed in this list.</remarks>
		IList<IAstNode> Block { get; }
		/// <summary>Returns a list of "out-of-band" nodes associated with this
		/// node, if the parser was configured to keep them.</summary>
		/// <remarks>Out-of-band nodes are nodes that are ignored by the compiler,
		/// such as comments, preprocessor directives, and conditionally-compiled
		/// blocks whose condition was not met.</remarks>
		IList<IAstNode> Oob { get; }
		/// <summary>Returns the specified list of child nodes of this node.</summary>
        /// <remarks>This is used to retrieve lists, such as the inheritance list of
		/// a class node, that are not classified as the list of attributes, 
		/// parameters, of-parameters, or the Block.</remarks>
		IList<IAstNode> ListOf(Symbol id);

		/// <summary>Returns an enumerator of all the nodes of all lists of nodes
		/// within this one.</summary>
		IEnumerable<IAstNode> AllChildren { get; }

		/// <summary>Returns the data type of the node.</summary>
		/// <remarks>For type declaration nodes, this is the QST entry of the type.
		/// For methods, this is their result type. For proeprties, this is their data 
		/// type. For expressions or expression-statements, this is the result type of
		/// the expression. If the type is not known, DataType is null. It should
		/// only be set once, by the code that discovers the type.
		/// </remarks>
		ITypeNode DataType { get; set; }

		/// <summary>Returns whether this node has been modified since it was
		/// parsed.</summary>
		/// <remarks>This flag should be cleared by the parser. When it is false,
		/// the original source text (the portion that belongs to this node but not
		/// any of its children) can be used to represent the node in the source
		/// language. It is set automatically when Name, Content, or any child list
		/// is modified. It is set locally, not recursively, so a node may be
		/// modified when its parents and children are still unmodified.</remarks>
		bool IsModified { get; set; }

		/// <summary>If this node is synthetic, returns the node that was used as the
		/// basis to create this node; returns null if this node was created
		/// directly from the source code, or if the basis was not recorded.</summary>
		/// <remarks>A typical AST transform is to replace an AST node created from
		/// the user's syntax to a synthetic node that represents the meaning of the
		/// user's input. For example, at a low level there are no properties, only
		/// methods and fields. Therefore, in the QST (though not the AST),
		/// properties are replaced by methods, and in the AST, property accesses are
		/// replaced by method calls.
		/// 
		/// There is no setter for this property because the basis is typically
		/// selected during node construction.</remarks>
		IAstNode Basis { get; }
	}

	public interface IScope
	{
	}

	public interface ITypeNode : IScope
	{

	}
}