﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Loyc.Collections
{
	/// <summary>An interface for depositing items. Includes only an Add(T) method.</summary>
	public interface IAdd<in T>
	{
		void Add(T item);
	}

	/// <summary>Represents a write-only collection: you can modify it, but you
	/// cannot learn what it contains.</summary>
	#if CSharp4
	public interface ISinkCollection<in T> : IAdd<T>
	#else
	public interface ISinkCollection<T> : IAdd<T>
	#endif
	{
		//inherited void Add(T item);
		void Clear();
		bool Remove(T item);
	}

	/// <summary>Represents a write-only array.</summary>
	#if CSharp4
	public interface ISinkArray<in T>
	#else
	public interface ISinkArray<T>
	#endif
	{
		T this[int index] { set; }
	}

	/// <summary>Represents a write-only indexable list class.</summary>
	#if CSharp4
	public interface ISinkList<in T> : ISinkCollection<T>, ISinkArray<T>
	#else
	public interface ISinkList<T> : ISinkCollection<T>, ISinkArray<T>
	#endif
	{
	}
}
