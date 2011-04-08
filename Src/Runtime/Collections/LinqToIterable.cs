#region License, Terms and Author(s)
//
// LINQBridge
// Copyright (c) 2007-9 Atif Aziz, Joseph Albahari. All rights reserved.
//
//  Author(s):
//
//      Atif Aziz, http://www.raboof.com
//
// This library is free software; you can redistribute it and/or modify it 
// under the terms of the New BSD License, a copy of which should have 
// been delivered along with this distribution.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS 
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT 
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A 
// PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT 
// OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, 
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT 
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, 
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY 
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT 
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE 
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
//
#endregion

// $Id: Enumerable.cs 240 2010-10-19 21:49:03Z azizatif $

namespace Loyc.Runtime.Linq
{
	#region Imports

	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.Diagnostics;
	using LinqBridge;
	using System.Linq;

	#endregion

	/// <summary>
	/// Provides a set of static (Shared in Visual Basic) methods for 
	/// querying objects that implement <see cref="IIterable{T}" />.
	/// </summary>
	static partial class Iterable
	{
		/// <summary>
		/// Returns the input typed as <see cref="IIterable{T}"/>. Use ToIterable()
		/// if you would like to convert a collection from IEnumerable.
		/// </summary>
		public static IIterable<T> AsIterable<T>(IIterable<T> source)
		{
			return source;
		}

		/// <summary>
		/// Returns an empty <see cref="IIterable{T}"/> that has the 
		/// specified type argument.
		/// </summary>
		public static IIterable<TResult> Empty<TResult>()
		{
			return EmptyCollection<TResult>.Value;
		}

		/// <summary>
		/// Converts the elements of an <see cref="IIterable{T}"/> to the 
		/// specified type.
		/// </summary>
		public static IIterable<TResult> Cast<T, TResult>(this IIterable<T> source) where TResult:T
		{
			CheckNotNull(source, "source");
			return new DoDownCast<T, TResult>(source);
		}
		public static IIterable<TResult> UpCast<T, TResult>(this IIterable<T> source) where T:TResult
		{
			CheckNotNull(source, "source");
			return new DoUpCast<T, TResult>(source);
		}

		class DoDownCast<T, TOut> : IIterable<TOut> where TOut : T
		{
			protected IIterable<T> s;
			public DoDownCast(IIterable<T> source) { s = source; }
			public Iterator<T> GetIterator()
			{
				var it = s.GetIterator();
				return (ref bool ended) => (TOut)it(ref ended);
			}
		}
		class DoUpCast<T, TOut> : IIterable<TOut> where T : TOut
		{
			protected IIterable<T> s;
			public DoUpCast(IIterable<T> source) { s = source; }
			public Iterator<T> GetIterator()
			{
				var it = s.GetIterator();
				return (ref bool ended) => it(ref ended);
			}
		}

		/// <summary>
		/// Filters the elements of an <see cref="IIterable{T}"/> based on a specified type.
		/// </summary>
		public static IIterable<TResult> OfType<T, TResult>(this IIterable<T> source) where TResult : T
		{
			CheckNotNull(source, "source");
			return new DoOfType<T, TResult>(source);
		}

		class DoOfType<T, TOut> : IIterable<TOut> where TOut : T
		{
			protected IIterable<T> s;
			public DoOfType(IIterable<T> source) { s = source; }
			public Iterator<T> GetIterator()
			{
				var it = s.GetIterator();
				return (ref bool ended) => {
					for(;;) {
						T current = it(ref ended);
						if (ended)
							return default(TOut);
						if (current is TOut)
							return (TOut)current;
					}
				};
			}
		}

		/// <summary>
		/// Generates a sequence of integral numbers within a specified range.
		/// </summary>
		/// <param name="start">The value of the first integer in the sequence.</param>
		/// <param name="count">The number of sequential integers to generate.</param>

		public static IIterable<int> Range(int start, int count)
		{
			if (count < 0)
				throw new ArgumentOutOfRangeException("count", count, null);
			
			return new IterableFromDelegate<int>(() => Iterator.Range(start, count));
		}

		/// <summary>
		/// Generates a sequence that contains one repeated value.
		/// </summary>
		public static IIterable<TResult> Repeat<TResult>(TResult element, int count)
		{
			if (count < 0) throw new ArgumentOutOfRangeException("count", count, null);

			return new IterableFromDelegate<int>(() => Iterator.Repeat(element, count));
		}

		/// <summary>
		/// Filters a sequence of values based on a predicate.
		/// </summary>
		public static IIterable<T> Where<T>(this IIterable<T> source, Func<T, bool> predicate)
		{
			CheckNotNull(predicate, "predicate");
			return new DoWhere<T>(source, predicate);
		}

		class DoWhere<T> : IIterable<T>
		{
			IIterable<T> s;
			Predicate<T> p;
			public DoWhere(IIterable<T> source, Predicate<T> predicate) { s = source; p = predicate; }
			public Iterator<T> GetIterator()
			{
				var it = s.GetIterator();
				return (ref bool ended) => {
					T current;
					do {
						current = it(ref ended);
						if (ended)
							return default(T);
					} while(!p(current));
					return current;
				};
			}
		}

		/// <summary>
		/// Filters a sequence of values based on a predicate. 
		/// Each element's index is used in the logic of the predicate function.
		/// </summary>

		public static IIterable<T> Where<T>(this IIterable<T> source, Func<T, int, bool> predicate)
		{
			CheckNotNull(source, "source");
			CheckNotNull(predicate, "predicate");
			return DoWhere2(source, predicate);
		}

		class DoWhere2<T> : IIterable<T>
		{
			IIterable<T> s;
			Func<T, int, bool> _pred;
			public DoWhere2(IIterable<T> source, Func<T, int, bool> predicate) { s = source; _pred = predicate; }
			public Iterator<T> GetIterator()
			{
				var it = s.GetIterator();
				var i = -1;
				return (ref bool ended) =>
				{
					T current;
					do {
						current = it(ref ended);
						if (ended)
							return default(T);
						++i;
					} while (!_pred(current, i));
					return current;
				};
			}
		}

		/// <summary>
		/// Projects each element of a sequence into a new form.
		/// </summary>

		public static IIterable<TResult> Select<T, TResult>(this IIterable<T> source, Func<T, TResult> selector)
		{
			CheckNotNull(selector, "selector");

			return new DoSelect<T, TResult>(source, selector);
		}

		class DoSelect<T, TResult> : IIterable<TResult>
		{
			protected IIterable<T> s;
			Func<T, TResult> _sel;
			public DoSelect(IIterable<T> source, Func<T, TResult> selector) { s = source; sel = selector; }
			public Iterator<TResult> GetIterator()
			{
				var it = s.GetIterator();
				return (ref bool ended) =>
				{
					T current = it(ref ended);
					if (ended) return default(TResult);
					return _sel(current);
				};
			}
		}

		/// <summary>
		/// Projects each element of a sequence into a new form by 
		/// incorporating the element's index.
		/// </summary>

		public static IIterable<TResult> Select<T, TResult>(this IIterable<T> source, Func<T, int, TResult> selector)
		{
			CheckNotNull(source, "source");
			CheckNotNull(selector, "selector");

			return new DoSelect2<T, TResult>(source, selector);
		}

		class DoSelect2<T, TResult> : IIterable<TResult>
		{
			protected IIterable<T> s;
			Func<T, int, TResult> sel;
			public DoSelect2(IIterable<T> source, Func<T, int, TResult> selector) { s = source; sel = selector; }
			public Iterator<TResult> GetIterator()
			{
				var it = s.GetIterator();
				int i = -1;
				return (ref bool ended) =>
				{
					T current = it(ref ended);
					if (ended) return default(TResult);
					++i;
					return sel(current, i);
				};
			}
		}

		/// <summary>
		/// Projects each element of a sequence to an <see cref="IIterable{T}" /> 
		/// and flattens the resulting sequences into one sequence.
		/// </summary>
		public static IIterable<TResult> SelectMany<T, TResult>(this IIterable<T> source, Func<T, IIterable<TResult>> selector)
		{
			CheckNotNull(selector, "selector");

			return Concat(Select(source, selector));
		}

		/// <summary>
		/// Projects each element of a sequence to an <see cref="IIterable{T}" />, 
		/// and flattens the resulting sequences into one sequence. The 
		/// index of each source element is used in the projected form of 
		/// that element.
		/// </summary>
		public static IIterable<TResult> SelectMany<T, TResult>(this IIterable<T> source, Func<T, int, IIterable<TResult>> selector)
		{
			CheckNotNull(selector, "selector");

			return Concat(Select(source, selector));
		}

		/// <summary>
		/// Concatenates any number of sequences.
		/// </summary>
		public static IIterable<T> Concat<T>(this IIterable<IIterable<T>> sets)
		{
			return new DoConcat<T>(sets);
		}
		
		class DoConcat<T> : IIterable<T>
		{
			IIterable<IIterable<T>> s1;

			public DoConcat(IIterable<IIterable<T>> source) { s1 = source; }
			public Iterator<T> GetIterator()
			{
				var i1 = s1.GetIterator();
				Iterator<T> i2 = null;

				return (ref bool ended) =>
				{
					for (;;) {
						if (i2 != null)
						{
							bool ended2 = false;
							T current = i2(ref ended2);
							if (!ended2)
								return current;
							i2 = null;
						}

						var s2 = i1(ref ended);
						if (ended)
							return default(T);

						i2 = s2.GetIterator();
					}
				};
			}
		}

		/// <summary>
		/// Projects each element of a sequence to an <see cref="IIterable{T}" />, 
		/// flattens the resulting sequences into one sequence, and invokes 
		/// a result selector function on each element therein.
		/// </summary>
		public static IIterable<TResult> SelectMany<T, T2, TResult>(
			 this IIterable<T> source,
			 Func<T, IIterable<T2>> collectionSelector,
			 Func<T, T2, TResult> resultSelector)
		{
			CheckNotNull(source, "source");
			CheckNotNull(collectionSelector, "collectionSelector");
			CheckNotNull(resultSelector, "resultSelector");

			Func<T, IIterable<Pair<T, T2>>> zip = t => collectionSelector(t).Select(t2 => Pair.Create(t, t2));
			return Concat(Select(source, zip)).Select(pair => resultSelector(pair.A, pair.B));
		}

		/// <summary>
		/// Projects each element of a sequence to an <see cref="IIterable{T}" />, 
		/// flattens the resulting sequences into one sequence, and invokes 
		/// a result selector function on each element therein. The index of 
		/// each source element is used in the intermediate projected form 
		/// of that element.
		/// </summary>
		public static IIterable<TResult> SelectMany<T, T2, TResult>(
			 this IIterable<T> source,
			 Func<T, int, IIterable<T2>> collectionSelector,
			 Func<T, T2, TResult> resultSelector)
		{
			CheckNotNull(source, "source");
			CheckNotNull(collectionSelector, "collectionSelector");
			CheckNotNull(resultSelector, "resultSelector");

			int i = -1;
			Func<T, IIterable<Pair<T, T2>>> zip = (t => collectionSelector(t, ++i).Select(t2 => Pair.Create(t, t2)));
			return Concat(Select(source, zip)).Select(pair => resultSelector(pair.A, pair.B));
		}

		/// <summary>
		/// Returns elements from a sequence as long as a specified condition is true.
		/// </summary>
		public static IIterable<T> TakeWhile<T>(this IIterable<T> source, Func<T, bool> predicate)
		{
			CheckNotNull(predicate, "predicate");

			return new DoTakeWhile<T>(source, predicate);
		}

		class DoTakeWhile<T> : IIterable<T>
		{
			IIterable<T> s;
			Predicate<T> p;
			public DoTakeWhile(IIterable<T> source, Predicate<T> predicate) { s = source; p = predicate; }
			public Iterator<T> GetIterator()
			{
				var it = s.GetIterator();
				bool stopped = false;
				return (ref bool ended) => {
					if (stopped)
						return default(T);
					T current = it(ref ended);
					if (ended)
						return default(T);
					if (p(current))
						return current;
					stopped = true;
					return default(T);
				};
			}
		}

		/// <summary>
		/// Returns elements from a sequence as long as a specified condition is true.
		/// The element's index is used in the logic of the predicate function.
		/// </summary>
		public static IIterable<T> TakeWhile<T>(
			 this IIterable<T> source,
			 Func<T, int, bool> predicate)
		{
			CheckNotNull(source, "source");
			CheckNotNull(predicate, "predicate");

			return new DoTakeWhile2<T>(source, predicate);
		}

		class DoTakeWhile2<T> : IIterable<T>
		{
			IIterable<T> s;
			Func<T, int, bool> p;
			public DoTakeWhile2(IIterable<T> source, Func<T, int, bool> predicate) { s = source; p = predicate; }
			public Iterator<T> GetIterator()
			{
				var it = s.GetIterator();
				bool stopped = false;
				int i = -1;
				return (ref bool ended) => {
					if (stopped)
						return default(T);
					T current = it(ref ended);
					if (ended)
						return default(T);
					if (p(current, ++i))
						return current;
					stopped = true;
					return default(T);
				};
			}
		}

		private static class Futures<T>
		{
			public static readonly Func<T> Default = () => default(T);
			public static readonly Func<T> Undefined = () => { throw new InvalidOperationException(); };
		}

		/// <summary>
		/// Base implementation of First operator.
		/// </summary>
		private static T FirstImpl<T>(this IIterable<T> source, Func<T> empty)
		{
			CheckNotNull(source, "source");
			Debug.Assert(empty != null);

			var it = source.GetIterator();
			bool ended = false;
			T first = it(ref ended);
			return ended ? empty() : first;
		}

		/// <summary>
		/// Returns the first element of a sequence.
		/// </summary>
		public static T First<T>(
			 this IIterable<T> source)
		{
			return source.FirstImpl(Futures<T>.Undefined);
		}

		/// <summary>
		/// Returns the first element in a sequence that satisfies a specified condition.
		/// </summary>
		public static T First<T>(
			 this IIterable<T> source,
			 Func<T, bool> predicate)
		{
			return First(source.Where(predicate));
		}

		/// <summary>
		/// Returns the first element of a sequence, or a default value if 
		/// the sequence contains no elements.
		/// </summary>
		public static T FirstOrDefault<T>(
			 this IIterable<T> source)
		{
			return source.FirstImpl(Futures<T>.Default);
		}

		/// <summary>
		/// Returns the first element of the sequence that satisfies a 
		/// condition or a default value if no such element is found.
		/// </summary>
		public static T FirstOrDefault<T>(
			 this IIterable<T> source,
			 Func<T, bool> predicate)
		{
			return FirstOrDefault(source.Where(predicate));
		}

		/// <summary>
		/// Base implementation of Last operator.
		/// </summary>
		private static T LastImpl<T>(
			 this IIterable<T> source,
			 Func<T> empty)
		{
			CheckNotNull(source, "source");

			var it = source.GetIterator();
			bool ended = false;
			T t = it(ref ended);
			if (ended)
				return empty();

			var listS = source as IListSource<T>;    // optimized case for lists
			if (listS != null)
				return listS[listS.Count - 1];
			var list = source as IList<T>;    // optimized case for lists
			if (list != null)
				return list[list.Count - 1];

			for(;;) {
				T next = it(ref ended);
				if (ended) return t;
				t = next;
			}
		}

		/// <summary>
		/// Returns the last element of a sequence.
		/// </summary>
		public static T Last<T>(
			 this IIterable<T> source)
		{
			return source.LastImpl(Futures<T>.Undefined);
		}

		/// <summary>
		/// Returns the last element of a sequence that satisfies a 
		/// specified condition.
		/// </summary>
		public static T Last<T>(
			 this IIterable<T> source,
			 Func<T, bool> predicate)
		{
			return Last(source.Where(predicate));
		}

		/// <summary>
		/// Returns the last element of a sequence, or a default value if 
		/// the sequence contains no elements.
		/// </summary>
		public static T LastOrDefault<T>(
			 this IIterable<T> source)
		{
			return source.LastImpl(Futures<T>.Default);
		}

		/// <summary>
		/// Returns the last element of a sequence that satisfies a 
		/// condition or a default value if no such element is found.
		/// </summary>
		public static T LastOrDefault<T>(
			 this IIterable<T> source,
			 Func<T, bool> predicate)
		{
			return LastOrDefault(source.Where(predicate));
		}

		/// <summary>
		/// Base implementation of Single operator.
		/// </summary>
		private static T SingleImpl<T>(
			 this IIterable<T> source,
			 Func<T> empty)
		{
			CheckNotNull(source, "source");

			var it = source.GetIterator();
			
			T single, next;
			if (it.MoveNext(out single))
			{
				if (it.MoveNext(out next))
					throw new InvalidOperationException("Single element expected");

				return single;
			}

			return empty();
		}

		/// <summary>
		/// Returns the only element of a sequence, and throws an exception 
		/// if there is not exactly one element in the sequence.
		/// </summary>
		public static T Single<T>(
			 this IIterable<T> source)
		{
			return source.SingleImpl(Futures<T>.Undefined);
		}

		/// <summary>
		/// Returns the only element of a sequence that satisfies a 
		/// specified condition, and throws an exception if more than one 
		/// such element exists.
		/// </summary>
		public static T Single<T>(
			 this IIterable<T> source,
			 Func<T, bool> predicate)
		{
			return Single(source.Where(predicate));
		}

		/// <summary>
		/// Returns the only element of a sequence, or a default value if 
		/// the sequence is empty; this method throws an exception if there 
		/// is more than one element in the sequence.
		/// </summary>
		public static T SingleOrDefault<T>(
			 this IIterable<T> source)
		{
			return source.SingleImpl(Futures<T>.Default);
		}

		/// <summary>
		/// Returns the only element of a sequence that satisfies a 
		/// specified condition or a default value if no such element 
		/// exists; this method throws an exception if more than one element 
		/// satisfies the condition.
		/// </summary>
		public static T SingleOrDefault<T>(
			 this IIterable<T> source,
			 Func<T, bool> predicate)
		{
			return SingleOrDefault(source.Where(predicate));
		}

		/// <summary>
		/// Returns the element at a specified index in a sequence.
		/// </summary>
		public static T ElementAt<T>(
			 this IIterable<T> source,
			 int index)
		{
			CheckNotNull(source, "source");

			if (index < 0)
				throw new ArgumentOutOfRangeException("index", index, null);

			var list = source as IListSource<T>;
			if (list != null)
				return list[index];

			try
			{
				return source.SkipWhile((item, i) => i < index).First();
			}
			catch (InvalidOperationException) // if thrown by First
			{
				throw new ArgumentOutOfRangeException("index", index, null);
			}
		}

		/// <summary>
		/// Returns the element at a specified index in a sequence or a 
		/// default value if the index is out of range.
		/// </summary>
		public static T ElementAtOrDefault<T>(
			 this IIterable<T> source,
			 int index)
		{
			CheckNotNull(source, "source");

			if (index < 0)
				return default(T);

			var list = source as IListSource<T>;
			if (list != null)
				return index < list.Count ? list[index] : default(T);

			return source.SkipWhile((item, i) => i < index).FirstOrDefault();
		}

		/// <summary>
		/// Inverts the order of the elements in a sequence.
		/// </summary>
		public static IListSource<T> Reverse<T>(this IIterable<T> source)
		{
			CheckNotNull(source, "source");

			return new ReversedListSource<T>(ToInternalList(source));
		}

		public static InternalList<T> ToInternalList<T>(IIterable<T> source)
		{
			var listS = source as IListSource<T>;
			if (listS != null)
				return new InternalList<T>(Collections.ToArray(listS), listS.Count);

			var list = InternalList<T>.Empty;
			var it = source.GetIterator();
			for (bool ended = false;;)
			{
				T current = it(ref ended);
				if (ended)
					return list;
				list.Add(current);
			}
		}

		/// <summary>
		/// Returns a specified number of contiguous elements from the start 
		/// of a sequence.
		/// </summary>
		public static IIterable<T> Take<T>(this IIterable<T> source, int count)
		{
			return source.TakeWhile((item, i) => i < count);
		}

		/// <summary>
		/// Bypasses a specified number of elements in a sequence and then 
		/// returns the remaining elements.
		/// </summary>
		public static IIterable<T> Skip<T>(this IIterable<T> source, int count)
		{
			var list = source as IListSource<T>;
			if (list != null)
				return list.Slice(count, list.Count - count);

			return source.Where((item, i) => i >= count);
		}

		/// <summary>
		/// Bypasses elements in a sequence as long as a specified condition 
		/// is true and then returns the remaining elements.
		/// </summary>
		public static IIterable<T> SkipWhile<T>(this IIterable<T> source, Func<T, bool> predicate)
		{
			CheckNotNull(predicate, "predicate");

			return new DoSkipWhile<T>(source, predicate);
		}
		
		class DoSkipWhile<T> : IIterable<T>
		{
			IIterable<T> s;
			Predicate<T> p;
			public DoSkipWhile(IIterable<T> source, Predicate<T> predicate) { s = source; p = predicate; }
			public Iterator<T> GetIterator()
			{
				var it = s.GetIterator();
				bool skip = true;
				return (ref bool ended) =>
				{
					if (skip) {
						T current;
						do
							current = it(ref ended);
						while (!ended && p(current));
						skip = false;

						return current;
					}
					return it(ref ended);
				};
			}
		}

		/// <summary>
		/// Bypasses elements in a sequence as long as a specified condition 
		/// is true and then returns the remaining elements. The element's 
		/// index is used in the logic of the predicate function.
		/// </summary>
		public static IIterable<T> SkipWhile<T>(
			 this IIterable<T> source,
			 Func<T, int, bool> predicate)
		{
			CheckNotNull(source, "source");
			CheckNotNull(predicate, "predicate");

			return new DoSkipWhile2<T>(source, predicate);
		}

		class DoSkipWhile2<T> : IIterable<T>
		{
			IIterable<T> s;
			Func<T, int, bool> p;
			public DoSkipWhile2(IIterable<T> source, Func<T, int, bool> predicate) { s = source; p = predicate; }
			public Iterator<T> GetIterator()
			{
				var it = s.GetIterator();
				int i = -1;
				bool skip = true;
				return (ref bool ended) =>
				{
					if (skip) {
						T current;
						do
							current = it(ref ended);
						while (!ended && p(current, ++i));
						skip = false;

						return current;
					}
					return it(ref ended);
				};
			}
		}

		/// <summary>
		/// Returns the number of elements in a sequence.
		/// </summary>
		public static int Count<T>(this IIterable<T> source)
		{
			CheckNotNull(source, "source");

			var source2 = source as ISource<T>;
			if (source2 != null)
				return source2.Count;
		
			var collection = source as ICollection;
			if (collection != null)
				return collection.Count;

			int count = 0;
			bool ended = false;
			var it = source.GetIterator();
			for (it(ref ended); !ended; it(ref ended))
				count++;
			return count;
		}

		/// <summary>
		/// Returns a number that represents how many elements in the 
		/// specified sequence satisfy a condition.
		/// </summary>
		public static int Count<T>(
			 this IIterable<T> source,
			 Func<T, bool> predicate)
		{
			return Count(source.Where(predicate));
		}

		/// <summary>
		/// Returns an <see cref="Int64"/> that represents the total number 
		/// of elements in a sequence.
		/// </summary>
		public static long LongCount<T>(this IIterable<T> source)
		{
			CheckNotNull(source, "source");

			var array = source as Array;
			if (array != null)
				return array.Length;

			long count = 0;
			bool ended = false;
			var it = source.GetIterator();
			for (it(ref ended); !ended; it(ref ended))
				count++;
			return count;
		}

		/// <summary>
		/// Returns an <see cref="Int64"/> that represents how many elements 
		/// in a sequence satisfy a condition.
		/// </summary>
		public static long LongCount<T>(
			 this IIterable<T> source,
			 Func<T, bool> predicate)
		{
			return LongCount(source.Where(predicate));
		}

		/// <summary>
		/// Concatenates two sequences.
		/// </summary>
		public static IIterable<T> Concat<T>(
			 this IIterable<T> first,
			 IIterable<T> second)
		{
			CheckNotNull(first, "first");
			CheckNotNull(second, "second");

			return new DoConcat2<T>(first, second);
		}

		class DoConcat2<T> : IIterable<T>
		{
			IIterable<T> s;
			IIterable<T> s2;

			public DoConcat2(IIterable<T> source, IIterable<T> source2) { s = source; s2 = source2; }
			public Iterator<T> GetIterator()
			{
				var it = s.GetIterator();
				return (ref bool ended) =>
				{
					bool ended1 = false;
					T current = it(ref ended1);
					if (ended1)
					{
						if (s2 != null) {
							it = s2.GetIterator();
							s2 = null;
							current = it(ref ended);
						} else
							ended = true;
					}
					return current;
				};
			}
		}

		/// <summary>
		/// Creates a <see cref="List{T}"/> from an <see cref="IIterable{T}"/>.
		/// </summary>
		public static List<T> ToList<T>(
			 this IIterable<T> source)
		{
			CheckNotNull(source, "source");

			return new List<T>(source.ToEnumerable());
		}

		/// <summary>
		/// Creates an array from an <see cref="IIterable{T}"/>.
		/// </summary>
		public static T[] ToArray<T>(
			 this IIterable<T> source)
		{
			var list = ToInternalList(source);
			if (list.Count == list.InternalArray.Length)
				return list.InternalArray;
			else
				return list.ToArray();
		}

		/// <summary>
		/// Returns distinct elements from a sequence by using the default 
		/// equality comparer to compare values.
		/// </summary>
		public static IIterable<T> Distinct<T>(
			 this IIterable<T> source)
		{
			return Distinct(source, /* comparer */ null);
		}

		/// <summary>
		/// Returns distinct elements from a sequence by using a specified 
		/// <see cref="IEqualityComparer{T}"/> to compare values.
		/// </summary>
		public static IIterable<T> Distinct<T>(
			 this IIterable<T> source,
			 IEqualityComparer<T> comparer)
		{
			CheckNotNull(source, "source");

			return new IterableFromDelegate<T>(() => DistinctIterable(source, comparer));
		}

		private static Iterator<T> DistinctIterable<T>(IIterable<T> source, IEqualityComparer<T> comparer)
		{
			var set = new Dictionary<T, object>(comparer);
			var gotNull = false;

			var it = source.GetIterator();
			return (ref bool ended) =>
			{
				for (;;) {
					T item = it(ref ended);
					if (ended)
						return default(T);

					if (item == null)
					{
						if (gotNull)
							continue;
						gotNull = true;
					}
					else
					{
						if (set.ContainsKey(item))
							continue;
						set.Add(item, null);
					}

					return item;
				}
			};
		}

		/// <summary>
		/// Creates a <see cref="Lookup{TKey,TElement}" /> from an 
		/// <see cref="IIterable{T}" /> according to a specified key 
		/// selector function.
		/// </summary>
		public static ILookup<TKey, T> ToLookup<T, TKey>(
			 this IIterable<T> source,
			 Func<T, TKey> keySelector)
		{
			return ToLookup(source, keySelector, e => e, /* comparer */ null);
		}

		/// <summary>
		/// Creates a <see cref="Lookup{TKey,TElement}" /> from an 
		/// <see cref="IIterable{T}" /> according to a specified key 
		/// selector function and a key comparer.
		/// </summary>
		public static ILookup<TKey, T> ToLookup<T, TKey>(
			 this IIterable<T> source,
			 Func<T, TKey> keySelector,
			 IEqualityComparer<TKey> comparer)
		{
			return ToLookup(source, keySelector, e => e, comparer);
		}

		/// <summary>
		/// Creates a <see cref="Lookup{TKey,TElement}" /> from an 
		/// <see cref="IIterable{T}" /> according to specified key 
		/// and element selector functions.
		/// </summary>

		public static ILookup<TKey, TElement> ToLookup<T, TKey, TElement>(
			 this IIterable<T> source,
			 Func<T, TKey> keySelector,
			 Func<T, TElement> elementSelector)
		{
			return ToLookup(source, keySelector, elementSelector, /* comparer */ null);
		}

		/// <summary>
		/// Creates a <see cref="Lookup{TKey,TElement}" /> from an 
		/// <see cref="IIterable{T}" /> according to a specified key 
		/// selector function, a comparer and an element selector function.
		/// </summary>

		public static ILookup<TKey, TElement> ToLookup<T, TKey, TElement>(
			 this IIterable<T> source,
			 Func<T, TKey> keySelector,
			 Func<T, TElement> elementSelector,
			 IEqualityComparer<TKey> comparer)
		{
			CheckNotNull(source, "source");
			CheckNotNull(keySelector, "keySelector");
			CheckNotNull(elementSelector, "elementSelector");

			var lookup = new Lookup<TKey, TElement>(comparer);

			T item;
			for (var it = source.GetIterator(); it.MoveNext(out item);)
			{
				var key = keySelector(item);

				var grouping = (Grouping<TKey, TElement>)lookup.Find(key);
				if (grouping == null)
				{
					grouping = new Grouping<TKey, TElement>(key);
					lookup.Add(grouping);
				}

				grouping.Add(elementSelector(item));
			}

			return lookup;
		}

		/// <summary>
		/// Groups the elements of a sequence according to a specified key 
		/// selector function.
		/// </summary>
		public static IIterable<IGrouping<TKey, T>> GroupBy<T, TKey>(
			 this IIterable<T> source,
			 Func<T, TKey> keySelector)
		{
			return GroupBy(source, keySelector, /* comparer */ null);
		}

		/// <summary>
		/// Groups the elements of a sequence according to a specified key 
		/// selector function and compares the keys by using a specified 
		/// comparer.
		/// </summary>
		public static IIterable<IGrouping<TKey, T>> GroupBy<T, TKey>(
			 this IIterable<T> source,
			 Func<T, TKey> keySelector,
			 IEqualityComparer<TKey> comparer)
		{
			return GroupBy(source, keySelector, e => e, comparer);
		}

		/// <summary>
		/// Groups the elements of a sequence according to a specified key 
		/// selector function and projects the elements for each group by 
		/// using a specified function.
		/// </summary>
		public static IIterable<IGrouping<TKey, TElement>> GroupBy<T, TKey, TElement>(
			 this IIterable<T> source,
			 Func<T, TKey> keySelector,
			 Func<T, TElement> elementSelector)
		{
			return GroupBy(source, keySelector, elementSelector, /* comparer */ null);
		}

		/// <summary>
		/// Groups the elements of a sequence according to a specified key 
		/// selector function and creates a result value from each group and 
		/// its key.
		/// </summary>
		public static IIterable<IGrouping<TKey, TElement>> GroupBy<T, TKey, TElement>(
			 this IIterable<T> source,
			 Func<T, TKey> keySelector,
			 Func<T, TElement> elementSelector,
			 IEqualityComparer<TKey> comparer)
		{
			CheckNotNull(source, "source");
			CheckNotNull(keySelector, "keySelector");
			CheckNotNull(elementSelector, "elementSelector");

			return ToLookup(source, keySelector, elementSelector, comparer).ToIterable();
		}

		/// <summary>
		/// Groups the elements of a sequence according to a key selector 
		/// function. The keys are compared by using a comparer and each 
		/// group's elements are projected by using a specified function.
		/// </summary>
		public static IIterable<TResult> GroupBy<T, TKey, TResult>(
			 this IIterable<T> source,
			 Func<T, TKey> keySelector,
			 Func<TKey, IIterable<T>, TResult> resultSelector)
		{
			return GroupBy(source, keySelector, resultSelector, /* comparer */ null);
		}

		/// <summary>
		/// Groups the elements of a sequence according to a specified key 
		/// selector function and creates a result value from each group and 
		/// its key. The elements of each group are projected by using a 
		/// specified function.
		/// </summary>
		public static IIterable<TResult> GroupBy<T, TKey, TResult>(
			 this IIterable<T> source,
			 Func<T, TKey> keySelector,
			 Func<TKey, IIterable<T>, TResult> resultSelector,
			 IEqualityComparer<TKey> comparer)
		{
			CheckNotNull(source, "source");
			CheckNotNull(keySelector, "keySelector");
			CheckNotNull(resultSelector, "resultSelector");

			return Enumerable.Select(ToLookup(source, keySelector, comparer), g => resultSelector(g.Key, g));
		}

		/// <summary>
		/// Groups the elements of a sequence according to a specified key 
		/// selector function and creates a result value from each group and 
		/// its key. The keys are compared by using a specified comparer.
		/// </summary>
		public static IIterable<TResult> GroupBy<T, TKey, TElement, TResult>(
			 this IIterable<T> source,
			 Func<T, TKey> keySelector,
			 Func<T, TElement> elementSelector,
			 Func<TKey, IIterable<TElement>, TResult> resultSelector)
		{
			return GroupBy(source, keySelector, elementSelector, resultSelector, /* comparer */ null);
		}

		/// <summary>
		/// Groups the elements of a sequence according to a specified key 
		/// selector function and creates a result value from each group and 
		/// its key. Key values are compared by using a specified comparer, 
		/// and the elements of each group are projected by using a 
		/// specified function.
		/// </summary>
		public static IIterable<TResult> GroupBy<T, TKey, TElement, TResult>(
			 this IIterable<T> source,
			 Func<T, TKey> keySelector,
			 Func<T, TElement> elementSelector,
			 Func<TKey, IIterable<TElement>, TResult> resultSelector,
			 IEqualityComparer<TKey> comparer)
		{
			CheckNotNull(source, "source");
			CheckNotNull(keySelector, "keySelector");
			CheckNotNull(elementSelector, "elementSelector");
			CheckNotNull(resultSelector, "resultSelector");

			return Enumerable.Select(ToLookup(source, keySelector, elementSelector, comparer), g => resultSelector(g.Key, g));
		}

		/// <summary>
		/// Applies an accumulator function over a sequence.
		/// </summary>
		public static T Aggregate<T>(this IIterable<T> source, Func<T, T, T> func)
		{
			CheckNotNull(source, "source");
			CheckNotNull(func, "func");

			var it = source.GetIterator();
			T total;
			if (!it.MoveNext(out total))
				throw new InvalidOperationException("Aggregate cannot operate on an empty sequence");

			for (;;) {
				bool ended = false;
				T next = it(ref ended);
				if (ended)
					return total;
				total = func(total, next);
			}
		}
		
		/// <summary>
		/// Applies an accumulator function over a sequence. The specified 
		/// seed value is used as the initial accumulator value.
		/// </summary>
		public static TAccumulate Aggregate<T, TAccumulate>(
			 this IIterable<T> source,
			 TAccumulate seed,
			 Func<TAccumulate, T, TAccumulate> func)
		{
			CheckNotNull(source, "source");
			CheckNotNull(func, "func");

			var result = seed;

			bool ended = false;
			for (var it = source.GetIterator(); ; )
			{
				T item = it(ref ended);
				if (ended) return result;
				result = func(result, item);
			}
		}

		/// <summary>
		/// Applies an accumulator function over a sequence. The specified 
		/// seed value is used as the initial accumulator value, and the 
		/// specified function is used to select the result value.
		/// </summary>

		public static TResult Aggregate<T, TAccumulate, TResult>(
			 this IIterable<T> source,
			 TAccumulate seed,
			 Func<TAccumulate, T, TAccumulate> func,
			 Func<TAccumulate, TResult> resultSelector)
		{
			CheckNotNull(resultSelector, "resultSelector");
			return resultSelector(Aggregate(source, seed, func));
		}

		/// <summary>
		/// Produces the set union of two sequences by using the default 
		/// equality comparer.
		/// </summary>

		public static IIterable<T> Union<T>(
			 this IIterable<T> first,
			 IIterable<T> second)
		{
			return Union(first, second, /* comparer */ null);
		}

		/// <summary>
		/// Produces the set union of two sequences by using a specified 
		/// <see cref="IEqualityComparer{T}" />.
		/// </summary>
		public static IIterable<T> Union<T>(
			 this IIterable<T> first,
			 IIterable<T> second,
			 IEqualityComparer<T> comparer)
		{
			return first.Concat(second).Distinct(comparer);
		}

		/// <summary>
		/// Returns the elements of the specified sequence or the type 
		/// parameter's default value in a singleton collection if the 
		/// sequence is empty.
		/// </summary>
		public static IIterable<T> DefaultIfEmpty<T>(
			 this IIterable<T> source)
		{
			return DefaultIfEmpty(source, default(T));
		}

		/// <summary>
		/// Returns the elements of the specified sequence or the specified 
		/// value in a singleton collection if the sequence is empty.
		/// </summary>
		public static IIterable<T> DefaultIfEmpty<T>(
			 this IIterable<T> source,
			 T defaultValue)
		{
			CheckNotNull(source, "source");

			return new DoDefaultIfEmpty<T>(source, Loyc.Runtime.Iterable.Single(defaultValue));
		}

		/// <summary>
		/// Returns the elements of the specified source sequence or, if that
		/// sequence is empty, the elements of the specified fallback sequence.
		/// </summary>
		public static IIterable<T> DefaultIfEmpty<T>(
			 this IIterable<T> source,
			 IIterable<T> fallback)
		{
			CheckNotNull(source, "source");
			CheckNotNull(fallback, "fallback");

			return new DoDefaultIfEmpty<T>(source, fallback);
		}

		class DoDefaultIfEmpty<T> : IIterable<T>
		{
			IIterable<T> s;
			IIterable<T> f;

			public DoDefaultIfEmpty(IIterable<T> source, IIterable<T> fallback) { s = source; f = fallback; }
			public Iterator<T> GetIterator()
			{
				var it = s.GetIterator();
				return (ref bool ended) =>
				{
					bool ended1 = false;
					T current = it(ref ended1);
					if (ended1)
					{
						if (f != null) {
							it = f.GetIterator();
							f = null;
							current = it(ref ended);
						} else
							ended = true;
					}
					f = null;
					return current;
				};
			}
		}

		/// <summary>
		/// Determines whether all elements of a sequence satisfy a condition.
		/// </summary>
		public static bool All<T>(
			 this IIterable<T> source,
			 Func<T, bool> predicate)
		{
			CheckNotNull(source, "source");
			CheckNotNull(predicate, "predicate");

			var it = source.GetIterator();
			bool ended = false;
			for (;;) {
				T item = it(ref ended);
				if (ended)
					return true;
				if (!predicate(item))
					return false;
			}
		}

		/// <summary>
		/// Determines whether a sequence contains any elements.
		/// </summary>
		public static bool Any<T>(this IIterable<T> source)
		{
			return !Empty(source);
		}

		/// <summary>
		/// Determines whether a sequence contains any elements.
		/// </summary>
		public static bool Empty<T>(this IIterable<T> source)
		{
			CheckNotNull(source, "source");

			bool ended = false;
			source.GetIterator()(ref ended);
			return ended;
		}

		/// <summary>
		/// Determines whether any element of a sequence satisfies a 
		/// condition.
		/// </summary>
		public static bool Any<T>(this IIterable<T> source, Func<T, bool> predicate)
		{
			return Any(Where(source, predicate));
		}

		/// <summary>
		/// Determines whether a sequence contains a specified element by 
		/// using the default equality comparer.
		/// </summary>
		public static bool Contains<T>(
			 this IIterable<T> source,
			 T value)
		{
			return source.Contains(value, /* comparer */ null);
		}

		/// <summary>
		/// Determines whether a sequence contains a specified element by 
		/// using a specified <see cref="IEqualityComparer{T}" />.
		/// </summary>
		public static bool Contains<T>(
			 this IIterable<T> source,
			 T value,
			 IEqualityComparer<T> comparer)
		{
			CheckNotNull(source, "source");

			if (comparer == null)
			{
				var collection = source as ICollection<T>;
				if (collection != null)
					return collection.Contains(value);
			}

			comparer = comparer ?? EqualityComparer<T>.Default;
			return source.Any(item => comparer.Equals(item, value));
		}

		/// <summary>
		/// Determines whether two sequences are equal by comparing the 
		/// elements by using the default equality comparer for their type.
		/// </summary>
		public static bool SequenceEqual<T>(
			 this IIterable<T> first,
			 IIterable<T> second)
		{
			return first.SequenceEqual(second, /* comparer */ null);
		}

		/// <summary>
		/// Determines whether two sequences are equal by comparing their 
		/// elements by using a specified <see cref="IEqualityComparer{T}" />.
		/// </summary>
		public static bool SequenceEqual<T>(
			 this IIterable<T> first,
			 IIterable<T> second,
			 IEqualityComparer<T> comparer)
		{
			CheckNotNull(first, "frist");
			CheckNotNull(second, "second");

			comparer = comparer ?? EqualityComparer<T>.Default;

			var it1 = first.GetIterator();
			var it2 = second.GetIterator();
			bool ended1 = false, ended2 = false;
			for (;;)
			{
				T current1 = it1(ref ended1);
				T current2 = it2(ref ended2);
				if (ended1 && ended2)
					return true;
				if (ended1 != ended2 || !comparer.Equals(current1, current2))
					return false;
			}
		}

		/// <summary>
		/// Base implementation for Min/Max operator.
		/// </summary>

		private static T MinMaxImpl<T>(
			 this IIterable<T> source,
			 Func<T, T, bool> lesser)
		{
			CheckNotNull(source, "source");
			Debug.Assert(lesser != null);

			return source.Aggregate((a, item) => lesser(a, item) ? a : item);
		}

		/// <summary>
		/// Base implementation for Min/Max operator for nullable types.
		/// </summary>

		private static T? MinMaxImpl<T>(
			 this IIterable<T?> source,
			 T? seed, Func<T?, T?, bool> lesser) where T : struct
		{
			CheckNotNull(source, "source");
			Debug.Assert(lesser != null);

			return source.Aggregate(seed, (a, item) => lesser(a, item) ? a : item);
			//  == MinMaxImpl(Repeat<T?>(null, 1).Concat(source), lesser);
		}

		/// <summary>
		/// Returns the minimum value in a generic sequence.
		/// </summary>

		public static T Min<T>(
			 this IIterable<T> source)
		{
			var comparer = Comparer<T>.Default;
			return source.MinMaxImpl((x, y) => comparer.Compare(x, y) < 0);
		}

		/// <summary>
		/// Invokes a transform function on each element of a generic 
		/// sequence and returns the minimum resulting value.
		/// </summary>

		public static TResult Min<T, TResult>(
			 this IIterable<T> source,
			 Func<T, TResult> selector)
		{
			return source.Select(selector).Min();
		}

		/// <summary>
		/// Returns the maximum value in a generic sequence.
		/// </summary>

		public static T Max<T>(
			 this IIterable<T> source)
		{
			var comparer = Comparer<T>.Default;
			return source.MinMaxImpl((x, y) => comparer.Compare(x, y) > 0);
		}

		/// <summary>
		/// Invokes a transform function on each element of a generic 
		/// sequence and returns the maximum resulting value.
		/// </summary>

		public static TResult Max<T, TResult>(
			 this IIterable<T> source,
			 Func<T, TResult> selector)
		{
			return source.Select(selector).Max();
		}

		/// <summary>
		/// Makes an enumerator seen as enumerable once more.
		/// </summary>
		/// <remarks>
		/// The supplied enumerator must have been started. The first element
		/// returned is the element the enumerator was on when passed in.
		/// DO NOT use this method if the caller must be a generator. It is
		/// mostly safe among aggregate operations.
		/// </remarks>

		private static IIterable<T> Renumerable<T>(this IEnumerator<T> e)
		{
			Debug.Assert(e != null);

			do { yield return e.Current; } while (e.MoveNext());
		}

		/// <summary>
		/// Sorts the elements of a sequence in ascending order according to a key.
		/// </summary>
		public static IOrderedEnumerable<T> OrderBy<T, TKey>(
			 this IIterable<T> source,
			 Func<T, TKey> keySelector)
		{
			return source.OrderBy(keySelector, /* comparer */ null);
		}

		/// <summary>
		/// Sorts the elements of a sequence in ascending order by using a 
		/// specified comparer.
		/// </summary>
		public static IOrderedEnumerable<T> OrderBy<T, TKey>(
			 this IIterable<T> source,
			 Func<T, TKey> keySelector,
			 IComparer<TKey> comparer)
		{
			CheckNotNull(source, "source");
			CheckNotNull(keySelector, "keySelector");

			return Enumerable.OrderBy(source.ToEnumerable(), keySelector, comparer);
		}

		/// <summary>
		/// Sorts the elements of a sequence in descending order according to a key.
		/// </summary>
		public static IOrderedEnumerable<T> OrderByDescending<T, TKey>(
			 this IIterable<T> source,
			 Func<T, TKey> keySelector)
		{
			return source.OrderByDescending(keySelector, /* comparer */ null);
		}

		/// <summary>
		///  Sorts the elements of a sequence in descending order by using a 
		/// specified comparer. 
		/// </summary>
		public static IOrderedEnumerable<T> OrderByDescending<T, TKey>(
			 this IIterable<T> source,
			 Func<T, TKey> keySelector,
			 IComparer<TKey> comparer)
		{
			CheckNotNull(source, "source");
			CheckNotNull(source, "keySelector");

			return Enumerable.OrderByDescending(source.ToEnumerable(), keySelector, comparer);
		}

		/*
		/// <summary>
		/// Performs a subsequent ordering of the elements in a sequence in 
		/// ascending order according to a key.
		/// </summary>
		public static IOrderedEnumerable<T> ThenBy<T, TKey>(
			 this IOrderedEnumerable<T> source,
			 Func<T, TKey> keySelector)
		{
			return source.ThenBy(keySelector, null);
		}

		/// <summary>
		/// Performs a subsequent ordering of the elements in a sequence in 
		/// ascending order by using a specified comparer.
		/// </summary>

		public static IOrderedEnumerable<T> ThenBy<T, TKey>(
			 this IOrderedEnumerable<T> source,
			 Func<T, TKey> keySelector,
			 IComparer<TKey> comparer)
		{
			CheckNotNull(source, "source");

			return source.CreateOrderedEnumerable(keySelector, comparer, false);
		}

		/// <summary>
		/// Performs a subsequent ordering of the elements in a sequence in 
		/// descending order, according to a key.
		/// </summary>

		public static IOrderedEnumerable<T> ThenByDescending<T, TKey>(
			 this IOrderedEnumerable<T> source,
			 Func<T, TKey> keySelector)
		{
			return source.ThenByDescending(keySelector, null);
		}

		/// <summary>
		/// Performs a subsequent ordering of the elements in a sequence in 
		/// descending order by using a specified comparer.
		/// </summary>
		public static IOrderedEnumerable<T> ThenByDescending<T, TKey>(
			 this IOrderedEnumerable<T> source,
			 Func<T, TKey> keySelector,
			 IComparer<TKey> comparer)
		{
			CheckNotNull(source, "source");

			return source.CreateOrderedEnumerable(keySelector, comparer, true);
		}
		*/

		class Lazy<T>
		{
			Func<T> _factory;
			T _value;

			public T Value
			{
				get { 
					if (_getter != null) {
						_value = _getter();
						_getter = null;
					}
					return _value;
				}
			}
			public bool IsValueCreated
			{
				get { return _getter == null; }
			}
			public Lazy()
			{
				_factory = () => (T) Activator.CreateInstance(typeof(T));
			}
			public Lazy(Func<T> valueFactory)
			{
				_factory = valueFactory;
			}
			public override string ToString()
			{
 				return Value.ToString();
			}
		}
		class LazyIterable<T> : IIterable<T>
		{
			Lazy<IIterable<T>> s;
			public LazyIterable(Func<IIterable<T>> getter) { s = new Lazy<IIterable<T>>(getter); }
			public Iterator<T> GetIterator() { return s.Value.GetIterator(); }
		}

		/// <summary>
		/// Base implementation for Intersect and Except operators.
		/// </summary>
		private static IIterable<T> IntersectExceptImpl<T>(
			 this IIterable<T> first,
			 IIterable<T> second,
			 IEqualityComparer<T> comparer,
			 bool flag)
		{
			CheckNotNull(first, "first");
			CheckNotNull(second, "second");

			var keys = InternalList<T>.Empty;
			var flags = new Dictionary<T, bool>(comparer);
			{
				T item;
				for (var it = first.Where(k => !flags.ContainsKey(k)).GetIterator(); it.MoveNext(out item); )
				{
					flags.Add(item, !flag);
					keys.Add(item);
				}

				for (var it = second.Where(flags.ContainsKey).GetIterator(); it.MoveNext(out item); )
					flags[item] = flag;
			}
			// As per docs, "the marked elements are yielded in the order in 
			// which they were collected."
			return keys.Where(item => flags[item]);
		}

		/// <summary>
		/// Produces the set intersection of two sequences by using the 
		/// default equality comparer to compare values.
		/// </summary>
		public static IIterable<T> Intersect<T>(
			 this IIterable<T> first,
			 IIterable<T> second)
		{
			return first.Intersect(second, /* comparer */ null);
		}

		/// <summary>
		/// Produces the set intersection of two sequences by using the 
		/// specified <see cref="IEqualityComparer{T}" /> to compare values.
		/// </summary>
		public static IIterable<T> Intersect<T>(
			 this IIterable<T> first,
			 IIterable<T> second,
			 IEqualityComparer<T> comparer)
		{
			return new LazyIterable<T>(() => IntersectExceptImpl(first, second, comparer, /* flag */ true));
		}

		/// <summary>
		/// Produces the set difference of two sequences by using the 
		/// default equality comparer to compare values.
		/// </summary>
		public static IIterable<T> Except<T>(
			 this IIterable<T> first,
			 IIterable<T> second)
		{
			return first.Except(second, /* comparer */ null);
		}

		/// <summary>
		/// Produces the set difference of two sequences by using the 
		/// specified <see cref="IEqualityComparer{T}" /> to compare values.
		/// </summary>

		public static IIterable<T> Except<T>(
			 this IIterable<T> first,
			 IIterable<T> second,
			 IEqualityComparer<T> comparer)
		{
			return IntersectExceptImpl(first, second, comparer, /* flag */ false);
		}

		/// <summary>
		/// Creates a <see cref="Dictionary{TKey,TValue}" /> from an 
		/// <see cref="IIterable{T}" /> according to a specified key 
		/// selector function.
		/// </summary>

		public static Dictionary<TKey, T> ToDictionary<T, TKey>(
			 this IIterable<T> source,
			 Func<T, TKey> keySelector)
		{
			return source.ToDictionary(keySelector, /* comparer */ null);
		}

		/// <summary>
		/// Creates a <see cref="Dictionary{TKey,TValue}" /> from an 
		/// <see cref="IIterable{T}" /> according to a specified key 
		/// selector function and key comparer.
		/// </summary>

		public static Dictionary<TKey, T> ToDictionary<T, TKey>(
			 this IIterable<T> source,
			 Func<T, TKey> keySelector,
			 IEqualityComparer<TKey> comparer)
		{
			return source.ToDictionary(keySelector, e => e);
		}

		/// <summary>
		/// Creates a <see cref="Dictionary{TKey,TValue}" /> from an 
		/// <see cref="IIterable{T}" /> according to specified key 
		/// selector and element selector functions.
		/// </summary>

		public static Dictionary<TKey, TElement> ToDictionary<T, TKey, TElement>(
			 this IIterable<T> source,
			 Func<T, TKey> keySelector,
			 Func<T, TElement> elementSelector)
		{
			return source.ToDictionary(keySelector, elementSelector, /* comparer */ null);
		}

		/// <summary>
		/// Creates a <see cref="Dictionary{TKey,TValue}" /> from an 
		/// <see cref="IIterable{T}" /> according to a specified key 
		/// selector function, a comparer, and an element selector function.
		/// </summary>
		public static Dictionary<TKey, TElement> ToDictionary<T, TKey, TElement>(
			 this IIterable<T> source,
			 Func<T, TKey> keySelector,
			 Func<T, TElement> elementSelector,
			 IEqualityComparer<TKey> comparer)
		{
			CheckNotNull(source, "source");
			CheckNotNull(keySelector, "keySelector");
			CheckNotNull(elementSelector, "elementSelector");

			var dict = new Dictionary<TKey, TElement>(comparer);

			T item;
			for (var it = source.GetIterator(); it.MoveNext(out item); )
			{
				//
				// ToDictionary is meant to throw ArgumentNullException if
				// keySelector produces a key that is null and 
				// Argument exception if keySelector produces duplicate keys 
				// for two elements. Incidentally, the doucmentation for
				// IDictionary<TKey, TValue>.Add says that the Add method
				// throws the same exceptions under the same circumstances
				// so we don't need to do any additional checking or work
				// here and let the Add implementation do all the heavy
				// lifting.
				//

				dict.Add(keySelector(item), elementSelector(item));
			}

			return dict;
		}

		/// <summary>
		/// Correlates the elements of two sequences based on matching keys. 
		/// The default equality comparer is used to compare keys.
		/// </summary>
		public static IIterable<TResult> Join<TOuter, TInner, TKey, TResult>(
			 this IIterable<TOuter> outer,
			 IIterable<TInner> inner,
			 Func<TOuter, TKey> outerKeySelector,
			 Func<TInner, TKey> innerKeySelector,
			 Func<TOuter, TInner, TResult> resultSelector)
		{
			return outer.Join(inner, outerKeySelector, innerKeySelector, resultSelector, /* comparer */ null);
		}

		/// <summary>
		/// Correlates the elements of two sequences based on matching keys. 
		/// The default equality comparer is used to compare keys. A 
		/// specified <see cref="IEqualityComparer{T}" /> is used to compare keys.
		/// </summary>
		public static IIterable<TResult> Join<TOuter, TInner, TKey, TResult>(
			 this IIterable<TOuter> outer,
			 IIterable<TInner> inner,
			 Func<TOuter, TKey> outerKeySelector,
			 Func<TInner, TKey> innerKeySelector,
			 Func<TOuter, TInner, TResult> resultSelector,
			 IEqualityComparer<TKey> comparer)
		{
			CheckNotNull(outer, "outer");
			CheckNotNull(inner, "inner");
			CheckNotNull(outerKeySelector, "outerKeySelector");
			CheckNotNull(innerKeySelector, "innerKeySelector");
			CheckNotNull(resultSelector, "resultSelector");

			var lookup = inner.ToLookup(innerKeySelector, comparer);

			return
				 from o in outer
				 from i in lookup[outerKeySelector(o)]
				 select resultSelector(o, i);
		}
		public static IEnumerable<TResult> Join<TOuter, TInner, TKey, TResult>(this IEnumerable<TOuter> outer, IEnumerable<TInner> inner, Func<TOuter, TKey> outerKeySelector, Func<TInner, TKey> innerKeySelector, Func<TOuter, TInner, TResult> resultSelector, IEqualityComparer<TKey> comparer)
		{
			 <>c__DisplayClass3<TOuter, TInner, TKey, TResult> class2;
			 ILookup<TKey, TInner> lookup = Enumerable.ToLookup<TInner, TKey>(inner, innerKeySelector, comparer);
			 return Enumerable.SelectMany<TOuter, TInner, TResult>(outer, new Func<TOuter, IEnumerable<TInner>>(class2, (IntPtr) this.<Join>b__1), new Func<TOuter, TInner, TResult>(class2, this.<Join>b__2));
		}

		 

		/// <summary>
		/// Correlates the elements of two sequences based on equality of 
		/// keys and groups the results. The default equality comparer is 
		/// used to compare keys.
		/// </summary>

		public static IIterable<TResult> GroupJoin<TOuter, TInner, TKey, TResult>(
			 this IIterable<TOuter> outer,
			 IIterable<TInner> inner,
			 Func<TOuter, TKey> outerKeySelector,
			 Func<TInner, TKey> innerKeySelector,
			 Func<TOuter, IIterable<TInner>, TResult> resultSelector)
		{
			return outer.GroupJoin(inner, outerKeySelector, innerKeySelector, resultSelector, /* comparer */ null);
		}

		/// <summary>
		/// Correlates the elements of two sequences based on equality of 
		/// keys and groups the results. The default equality comparer is 
		/// used to compare keys. A specified <see cref="IEqualityComparer{T}" /> 
		/// is used to compare keys.
		/// </summary>

		public static IIterable<TResult> GroupJoin<TOuter, TInner, TKey, TResult>(
			 this IIterable<TOuter> outer,
			 IIterable<TInner> inner,
			 Func<TOuter, TKey> outerKeySelector,
			 Func<TInner, TKey> innerKeySelector,
			 Func<TOuter, IIterable<TInner>, TResult> resultSelector,
			 IEqualityComparer<TKey> comparer)
		{
			CheckNotNull(outer, "outer");
			CheckNotNull(inner, "inner");
			CheckNotNull(outerKeySelector, "outerKeySelector");
			CheckNotNull(innerKeySelector, "innerKeySelector");
			CheckNotNull(resultSelector, "resultSelector");

			var lookup = inner.ToLookup(innerKeySelector, comparer);
			return outer.Select(o => resultSelector(o, lookup[outerKeySelector(o)]));
		}

		[DebuggerStepThrough]
		private static void CheckNotNull<T>(T value, string name) where T : class
		{
			if (value == null)
				throw new ArgumentNullException(name);
		}

		private static class Sequence<T>
		{
			public static readonly IIterable<T> Empty = new T[0];
		}

		private sealed class Grouping<K, V> : List<V>, IGrouping<K, V>
		{
			internal Grouping(K key)
			{
				Key = key;
			}

			public K Key { get; private set; }
		}
	}
}

// $Id: Enumerable.g.cs 215 2009-10-03 13:31:49Z azizatif $

namespace System.Linq
{
	#region Imports

	using System;
	using System.Collections.Generic;

	#endregion

	// This partial implementation was template-generated:
	// Sat, 03 Oct 2009 09:42:39 GMT

	partial class Enumerable
	{
		/// <summary>
		/// Computes the sum of a sequence of nullable <see cref="System.Int32" /> values.
		/// </summary>

		public static int Sum(
			 this IIterable<int> source)
		{
			CheckNotNull(source, "source");

			int sum = 0;
			foreach (var num in source)
				sum = checked(sum + num);

			return sum;
		}

		/// <summary>
		/// Computes the sum of a sequence of nullable <see cref="System.Int32" /> 
		/// values that are obtained by invoking a transform function on 
		/// each element of the input sequence.
		/// </summary>

		public static int Sum<T>(
			 this IIterable<T> source,
			 Func<T, int> selector)
		{
			return source.Select(selector).Sum();
		}

		/// <summary>
		/// Computes the average of a sequence of nullable <see cref="System.Int32" /> values.
		/// </summary>

		public static double Average(
			 this IIterable<int> source)
		{
			CheckNotNull(source, "source");

			long sum = 0;
			long count = 0;

			foreach (var num in source)
				checked
				{
					sum += (int)num;
					count++;
				}

			if (count == 0)
				throw new InvalidOperationException();

			return (double)sum / count;
		}

		/// <summary>
		/// Computes the average of a sequence of nullable <see cref="System.Int32" /> values 
		/// that are obtained by invoking a transform function on each 
		/// element of the input sequence.
		/// </summary>

		public static double Average<T>(
			 this IIterable<T> source,
			 Func<T, int> selector)
		{
			return source.Select(selector).Average();
		}


		/// <summary>
		/// Computes the sum of a sequence of <see cref="System.Int32" /> values.
		/// </summary>

		public static int? Sum(
			 this IIterable<int?> source)
		{
			CheckNotNull(source, "source");

			int sum = 0;
			foreach (var num in source)
				sum = checked(sum + (num ?? 0));

			return sum;
		}

		/// <summary>
		/// Computes the sum of a sequence of <see cref="System.Int32" /> 
		/// values that are obtained by invoking a transform function on 
		/// each element of the input sequence.
		/// </summary>

		public static int? Sum<T>(
			 this IIterable<T> source,
			 Func<T, int?> selector)
		{
			return source.Select(selector).Sum();
		}

		/// <summary>
		/// Computes the average of a sequence of <see cref="System.Int32" /> values.
		/// </summary>

		public static double? Average(
			 this IIterable<int?> source)
		{
			CheckNotNull(source, "source");

			long sum = 0;
			long count = 0;

			foreach (var num in source.Where(n => n != null))
				checked
				{
					sum += (int)num;
					count++;
				}

			if (count == 0)
				return null;

			return (double?)sum / count;
		}

		/// <summary>
		/// Computes the average of a sequence of <see cref="System.Int32" /> values 
		/// that are obtained by invoking a transform function on each 
		/// element of the input sequence.
		/// </summary>

		public static double? Average<T>(
			 this IIterable<T> source,
			 Func<T, int?> selector)
		{
			return source.Select(selector).Average();
		}

		/// <summary>
		/// Returns the minimum value in a sequence of nullable 
		/// <see cref="System.Int32" /> values.
		/// </summary>

		public static int? Min(
			 this IIterable<int?> source)
		{
			CheckNotNull(source, "source");

			return MinMaxImpl(source.Where(x => x != null), null, (min, x) => min < x);
		}

		/// <summary>
		/// Invokes a transform function on each element of a sequence and 
		/// returns the minimum nullable <see cref="System.Int32" /> value.
		/// </summary>

		public static int? Min<T>(
			 this IIterable<T> source,
			 Func<T, int?> selector)
		{
			return source.Select(selector).Min();
		}

		/// <summary>
		/// Returns the maximum value in a sequence of nullable 
		/// <see cref="System.Int32" /> values.
		/// </summary>

		public static int? Max(
			 this IIterable<int?> source)
		{
			CheckNotNull(source, "source");

			return MinMaxImpl(source.Where(x => x != null),
				 null, (max, x) => x == null || (max != null && x.Value < max.Value));
		}

		/// <summary>
		/// Invokes a transform function on each element of a sequence and 
		/// returns the maximum nullable <see cref="System.Int32" /> value.
		/// </summary>

		public static int? Max<T>(
			 this IIterable<T> source,
			 Func<T, int?> selector)
		{
			return source.Select(selector).Max();
		}

		/// <summary>
		/// Computes the sum of a sequence of nullable <see cref="System.Int64" /> values.
		/// </summary>

		public static long Sum(
			 this IIterable<long> source)
		{
			CheckNotNull(source, "source");

			long sum = 0;
			foreach (var num in source)
				sum = checked(sum + num);

			return sum;
		}

		/// <summary>
		/// Computes the sum of a sequence of nullable <see cref="System.Int64" /> 
		/// values that are obtained by invoking a transform function on 
		/// each element of the input sequence.
		/// </summary>

		public static long Sum<T>(
			 this IIterable<T> source,
			 Func<T, long> selector)
		{
			return source.Select(selector).Sum();
		}

		/// <summary>
		/// Computes the average of a sequence of nullable <see cref="System.Int64" /> values.
		/// </summary>

		public static double Average(
			 this IIterable<long> source)
		{
			CheckNotNull(source, "source");

			long sum = 0;
			long count = 0;

			foreach (var num in source)
				checked
				{
					sum += (long)num;
					count++;
				}

			if (count == 0)
				throw new InvalidOperationException();

			return (double)sum / count;
		}

		/// <summary>
		/// Computes the average of a sequence of nullable <see cref="System.Int64" /> values 
		/// that are obtained by invoking a transform function on each 
		/// element of the input sequence.
		/// </summary>

		public static double Average<T>(
			 this IIterable<T> source,
			 Func<T, long> selector)
		{
			return source.Select(selector).Average();
		}


		/// <summary>
		/// Computes the sum of a sequence of <see cref="System.Int64" /> values.
		/// </summary>

		public static long? Sum(
			 this IIterable<long?> source)
		{
			CheckNotNull(source, "source");

			long sum = 0;
			foreach (var num in source)
				sum = checked(sum + (num ?? 0));

			return sum;
		}

		/// <summary>
		/// Computes the sum of a sequence of <see cref="System.Int64" /> 
		/// values that are obtained by invoking a transform function on 
		/// each element of the input sequence.
		/// </summary>

		public static long? Sum<T>(
			 this IIterable<T> source,
			 Func<T, long?> selector)
		{
			return source.Select(selector).Sum();
		}

		/// <summary>
		/// Computes the average of a sequence of <see cref="System.Int64" /> values.
		/// </summary>

		public static double? Average(
			 this IIterable<long?> source)
		{
			CheckNotNull(source, "source");

			long sum = 0;
			long count = 0;

			foreach (var num in source.Where(n => n != null))
				checked
				{
					sum += (long)num;
					count++;
				}

			if (count == 0)
				return null;

			return (double?)sum / count;
		}

		/// <summary>
		/// Computes the average of a sequence of <see cref="System.Int64" /> values 
		/// that are obtained by invoking a transform function on each 
		/// element of the input sequence.
		/// </summary>

		public static double? Average<T>(
			 this IIterable<T> source,
			 Func<T, long?> selector)
		{
			return source.Select(selector).Average();
		}

		/// <summary>
		/// Returns the minimum value in a sequence of nullable 
		/// <see cref="System.Int64" /> values.
		/// </summary>

		public static long? Min(
			 this IIterable<long?> source)
		{
			CheckNotNull(source, "source");

			return MinMaxImpl(source.Where(x => x != null), null, (min, x) => min < x);
		}

		/// <summary>
		/// Invokes a transform function on each element of a sequence and 
		/// returns the minimum nullable <see cref="System.Int64" /> value.
		/// </summary>

		public static long? Min<T>(
			 this IIterable<T> source,
			 Func<T, long?> selector)
		{
			return source.Select(selector).Min();
		}

		/// <summary>
		/// Returns the maximum value in a sequence of nullable 
		/// <see cref="System.Int64" /> values.
		/// </summary>

		public static long? Max(
			 this IIterable<long?> source)
		{
			CheckNotNull(source, "source");

			return MinMaxImpl(source.Where(x => x != null),
				 null, (max, x) => x == null || (max != null && x.Value < max.Value));
		}

		/// <summary>
		/// Invokes a transform function on each element of a sequence and 
		/// returns the maximum nullable <see cref="System.Int64" /> value.
		/// </summary>

		public static long? Max<T>(
			 this IIterable<T> source,
			 Func<T, long?> selector)
		{
			return source.Select(selector).Max();
		}

		/// <summary>
		/// Computes the sum of a sequence of nullable <see cref="System.Single" /> values.
		/// </summary>

		public static float Sum(
			 this IIterable<float> source)
		{
			CheckNotNull(source, "source");

			float sum = 0;
			foreach (var num in source)
				sum = checked(sum + num);

			return sum;
		}

		/// <summary>
		/// Computes the sum of a sequence of nullable <see cref="System.Single" /> 
		/// values that are obtained by invoking a transform function on 
		/// each element of the input sequence.
		/// </summary>

		public static float Sum<T>(
			 this IIterable<T> source,
			 Func<T, float> selector)
		{
			return source.Select(selector).Sum();
		}

		/// <summary>
		/// Computes the average of a sequence of nullable <see cref="System.Single" /> values.
		/// </summary>

		public static float Average(
			 this IIterable<float> source)
		{
			CheckNotNull(source, "source");

			float sum = 0;
			long count = 0;

			foreach (var num in source)
				checked
				{
					sum += (float)num;
					count++;
				}

			if (count == 0)
				throw new InvalidOperationException();

			return (float)sum / count;
		}

		/// <summary>
		/// Computes the average of a sequence of nullable <see cref="System.Single" /> values 
		/// that are obtained by invoking a transform function on each 
		/// element of the input sequence.
		/// </summary>

		public static float Average<T>(
			 this IIterable<T> source,
			 Func<T, float> selector)
		{
			return source.Select(selector).Average();
		}


		/// <summary>
		/// Computes the sum of a sequence of <see cref="System.Single" /> values.
		/// </summary>

		public static float? Sum(
			 this IIterable<float?> source)
		{
			CheckNotNull(source, "source");

			float sum = 0;
			foreach (var num in source)
				sum = checked(sum + (num ?? 0));

			return sum;
		}

		/// <summary>
		/// Computes the sum of a sequence of <see cref="System.Single" /> 
		/// values that are obtained by invoking a transform function on 
		/// each element of the input sequence.
		/// </summary>

		public static float? Sum<T>(
			 this IIterable<T> source,
			 Func<T, float?> selector)
		{
			return source.Select(selector).Sum();
		}

		/// <summary>
		/// Computes the average of a sequence of <see cref="System.Single" /> values.
		/// </summary>

		public static float? Average(
			 this IIterable<float?> source)
		{
			CheckNotNull(source, "source");

			float sum = 0;
			long count = 0;

			foreach (var num in source.Where(n => n != null))
				checked
				{
					sum += (float)num;
					count++;
				}

			if (count == 0)
				return null;

			return (float?)sum / count;
		}

		/// <summary>
		/// Computes the average of a sequence of <see cref="System.Single" /> values 
		/// that are obtained by invoking a transform function on each 
		/// element of the input sequence.
		/// </summary>

		public static float? Average<T>(
			 this IIterable<T> source,
			 Func<T, float?> selector)
		{
			return source.Select(selector).Average();
		}

		/// <summary>
		/// Returns the minimum value in a sequence of nullable 
		/// <see cref="System.Single" /> values.
		/// </summary>

		public static float? Min(
			 this IIterable<float?> source)
		{
			CheckNotNull(source, "source");

			return MinMaxImpl(source.Where(x => x != null), null, (min, x) => min < x);
		}

		/// <summary>
		/// Invokes a transform function on each element of a sequence and 
		/// returns the minimum nullable <see cref="System.Single" /> value.
		/// </summary>

		public static float? Min<T>(
			 this IIterable<T> source,
			 Func<T, float?> selector)
		{
			return source.Select(selector).Min();
		}

		/// <summary>
		/// Returns the maximum value in a sequence of nullable 
		/// <see cref="System.Single" /> values.
		/// </summary>

		public static float? Max(
			 this IIterable<float?> source)
		{
			CheckNotNull(source, "source");

			return MinMaxImpl(source.Where(x => x != null),
				 null, (max, x) => x == null || (max != null && x.Value < max.Value));
		}

		/// <summary>
		/// Invokes a transform function on each element of a sequence and 
		/// returns the maximum nullable <see cref="System.Single" /> value.
		/// </summary>

		public static float? Max<T>(
			 this IIterable<T> source,
			 Func<T, float?> selector)
		{
			return source.Select(selector).Max();
		}

		/// <summary>
		/// Computes the sum of a sequence of nullable <see cref="System.Double" /> values.
		/// </summary>

		public static double Sum(
			 this IIterable<double> source)
		{
			CheckNotNull(source, "source");

			double sum = 0;
			foreach (var num in source)
				sum = checked(sum + num);

			return sum;
		}

		/// <summary>
		/// Computes the sum of a sequence of nullable <see cref="System.Double" /> 
		/// values that are obtained by invoking a transform function on 
		/// each element of the input sequence.
		/// </summary>

		public static double Sum<T>(
			 this IIterable<T> source,
			 Func<T, double> selector)
		{
			return source.Select(selector).Sum();
		}

		/// <summary>
		/// Computes the average of a sequence of nullable <see cref="System.Double" /> values.
		/// </summary>

		public static double Average(
			 this IIterable<double> source)
		{
			CheckNotNull(source, "source");

			double sum = 0;
			long count = 0;

			foreach (var num in source)
				checked
				{
					sum += (double)num;
					count++;
				}

			if (count == 0)
				throw new InvalidOperationException();

			return (double)sum / count;
		}

		/// <summary>
		/// Computes the average of a sequence of nullable <see cref="System.Double" /> values 
		/// that are obtained by invoking a transform function on each 
		/// element of the input sequence.
		/// </summary>

		public static double Average<T>(
			 this IIterable<T> source,
			 Func<T, double> selector)
		{
			return source.Select(selector).Average();
		}


		/// <summary>
		/// Computes the sum of a sequence of <see cref="System.Double" /> values.
		/// </summary>

		public static double? Sum(
			 this IIterable<double?> source)
		{
			CheckNotNull(source, "source");

			double sum = 0;
			foreach (var num in source)
				sum = checked(sum + (num ?? 0));

			return sum;
		}

		/// <summary>
		/// Computes the sum of a sequence of <see cref="System.Double" /> 
		/// values that are obtained by invoking a transform function on 
		/// each element of the input sequence.
		/// </summary>

		public static double? Sum<T>(
			 this IIterable<T> source,
			 Func<T, double?> selector)
		{
			return source.Select(selector).Sum();
		}

		/// <summary>
		/// Computes the average of a sequence of <see cref="System.Double" /> values.
		/// </summary>

		public static double? Average(
			 this IIterable<double?> source)
		{
			CheckNotNull(source, "source");

			double sum = 0;
			long count = 0;

			foreach (var num in source.Where(n => n != null))
				checked
				{
					sum += (double)num;
					count++;
				}

			if (count == 0)
				return null;

			return (double?)sum / count;
		}

		/// <summary>
		/// Computes the average of a sequence of <see cref="System.Double" /> values 
		/// that are obtained by invoking a transform function on each 
		/// element of the input sequence.
		/// </summary>

		public static double? Average<T>(
			 this IIterable<T> source,
			 Func<T, double?> selector)
		{
			return source.Select(selector).Average();
		}

		/// <summary>
		/// Returns the minimum value in a sequence of nullable 
		/// <see cref="System.Double" /> values.
		/// </summary>

		public static double? Min(
			 this IIterable<double?> source)
		{
			CheckNotNull(source, "source");

			return MinMaxImpl(source.Where(x => x != null), null, (min, x) => min < x);
		}

		/// <summary>
		/// Invokes a transform function on each element of a sequence and 
		/// returns the minimum nullable <see cref="System.Double" /> value.
		/// </summary>

		public static double? Min<T>(
			 this IIterable<T> source,
			 Func<T, double?> selector)
		{
			return source.Select(selector).Min();
		}

		/// <summary>
		/// Returns the maximum value in a sequence of nullable 
		/// <see cref="System.Double" /> values.
		/// </summary>

		public static double? Max(
			 this IIterable<double?> source)
		{
			CheckNotNull(source, "source");

			return MinMaxImpl(source.Where(x => x != null),
				 null, (max, x) => x == null || (max != null && x.Value < max.Value));
		}

		/// <summary>
		/// Invokes a transform function on each element of a sequence and 
		/// returns the maximum nullable <see cref="System.Double" /> value.
		/// </summary>

		public static double? Max<T>(
			 this IIterable<T> source,
			 Func<T, double?> selector)
		{
			return source.Select(selector).Max();
		}

		/// <summary>
		/// Computes the sum of a sequence of nullable <see cref="System.Decimal" /> values.
		/// </summary>

		public static decimal Sum(
			 this IIterable<decimal> source)
		{
			CheckNotNull(source, "source");

			decimal sum = 0;
			foreach (var num in source)
				sum = checked(sum + num);

			return sum;
		}

		/// <summary>
		/// Computes the sum of a sequence of nullable <see cref="System.Decimal" /> 
		/// values that are obtained by invoking a transform function on 
		/// each element of the input sequence.
		/// </summary>

		public static decimal Sum<T>(
			 this IIterable<T> source,
			 Func<T, decimal> selector)
		{
			return source.Select(selector).Sum();
		}

		/// <summary>
		/// Computes the average of a sequence of nullable <see cref="System.Decimal" /> values.
		/// </summary>

		public static decimal Average(
			 this IIterable<decimal> source)
		{
			CheckNotNull(source, "source");

			decimal sum = 0;
			long count = 0;

			foreach (var num in source)
				checked
				{
					sum += (decimal)num;
					count++;
				}

			if (count == 0)
				throw new InvalidOperationException();

			return (decimal)sum / count;
		}

		/// <summary>
		/// Computes the average of a sequence of nullable <see cref="System.Decimal" /> values 
		/// that are obtained by invoking a transform function on each 
		/// element of the input sequence.
		/// </summary>

		public static decimal Average<T>(
			 this IIterable<T> source,
			 Func<T, decimal> selector)
		{
			return source.Select(selector).Average();
		}


		/// <summary>
		/// Computes the sum of a sequence of <see cref="System.Decimal" /> values.
		/// </summary>

		public static decimal? Sum(
			 this IIterable<decimal?> source)
		{
			CheckNotNull(source, "source");

			decimal sum = 0;
			foreach (var num in source)
				sum = checked(sum + (num ?? 0));

			return sum;
		}

		/// <summary>
		/// Computes the sum of a sequence of <see cref="System.Decimal" /> 
		/// values that are obtained by invoking a transform function on 
		/// each element of the input sequence.
		/// </summary>

		public static decimal? Sum<T>(
			 this IIterable<T> source,
			 Func<T, decimal?> selector)
		{
			return source.Select(selector).Sum();
		}

		/// <summary>
		/// Computes the average of a sequence of <see cref="System.Decimal" /> values.
		/// </summary>

		public static decimal? Average(
			 this IIterable<decimal?> source)
		{
			CheckNotNull(source, "source");

			decimal sum = 0;
			long count = 0;

			foreach (var num in source.Where(n => n != null))
				checked
				{
					sum += (decimal)num;
					count++;
				}

			if (count == 0)
				return null;

			return (decimal?)sum / count;
		}

		/// <summary>
		/// Computes the average of a sequence of <see cref="System.Decimal" /> values 
		/// that are obtained by invoking a transform function on each 
		/// element of the input sequence.
		/// </summary>

		public static decimal? Average<T>(
			 this IIterable<T> source,
			 Func<T, decimal?> selector)
		{
			return source.Select(selector).Average();
		}

		/// <summary>
		/// Returns the minimum value in a sequence of nullable 
		/// <see cref="System.Decimal" /> values.
		/// </summary>

		public static decimal? Min(
			 this IIterable<decimal?> source)
		{
			CheckNotNull(source, "source");

			return MinMaxImpl(source.Where(x => x != null), null, (min, x) => min < x);
		}

		/// <summary>
		/// Invokes a transform function on each element of a sequence and 
		/// returns the minimum nullable <see cref="System.Decimal" /> value.
		/// </summary>

		public static decimal? Min<T>(
			 this IIterable<T> source,
			 Func<T, decimal?> selector)
		{
			return source.Select(selector).Min();
		}

		/// <summary>
		/// Returns the maximum value in a sequence of nullable 
		/// <see cref="System.Decimal" /> values.
		/// </summary>

		public static decimal? Max(
			 this IIterable<decimal?> source)
		{
			CheckNotNull(source, "source");

			return MinMaxImpl(source.Where(x => x != null),
				 null, (max, x) => x == null || (max != null && x.Value < max.Value));
		}

		/// <summary>
		/// Invokes a transform function on each element of a sequence and 
		/// returns the maximum nullable <see cref="System.Decimal" /> value.
		/// </summary>

		public static decimal? Max<T>(
			 this IIterable<T> source,
			 Func<T, decimal?> selector)
		{
			return source.Select(selector).Max();
		}
	}
}
