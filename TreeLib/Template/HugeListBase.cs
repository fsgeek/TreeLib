﻿/*
 *  Copyright © 2016 Thomas R. Lawrence
 * 
 *  GNU Lesser General Public License
 * 
 *  This file is part of TreeLib
 * 
 *  TreeLib is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU Lesser General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 *  GNU Lesser General Public License for more details.
 *
 *  You should have received a copy of the GNU Lesser General Public License
 *  along with this program. If not, see <http://www.gnu.org/licenses/>.
 * 
*/
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Reflection;

using TreeLib.Internal;

namespace TreeLib
{
    public class HugeListBase<T> :
        /*[Widen]*/IHugeList<T>,
        /*[Widen]*/IList<T>,
        /*[Widen]*/ICollection<T>,
        /*[Widen]*/IReadOnlyList<T>,
        /*[Widen]*/IReadOnlyCollection<T>,
        IEnumerable<T>,
        /*[Widen]*/IChunkedEnumerable<EntryRangeMap<T[]>>,
        IHugeListValidation
    {
        private const int DefaultMaxBlockSize = 512;

        [Widen]
        private readonly IRangeMap<T[]> tree;
        private readonly int maxBlockSize = DefaultMaxBlockSize;
        [Widen]
        private int currentStartIndex = -1; // start of the one and only segment that may contain extra space (last accessed)
        private ushort version;

        /// <summary>
        /// Create a new HugeList based on a splay tree.
        /// The HugeList will use the default maximum block size of 512.
        /// </summary>
        public HugeListBase()
        {
            this.tree = new /*[Widen]*/SplayTreeRangeMap<T[]>();
        }

        /// <summary>
        /// Create a new HugeList based on a splay tree, with the specified maximum block size.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">maxBlockSize is less than 1</exception>
        public HugeListBase(int maxBlockSize)
            : this()
        {
            if (maxBlockSize < 1)
            {
                throw new ArgumentOutOfRangeException();
            }

            this.maxBlockSize = maxBlockSize;
        }

        /// <summary>
        /// Create a new HugeList based on the provided underlying tree implementation. All tree implementations are functionally
        /// equivalent but may have different performance characteristics. It is recommended to measure the scenario to determine
        /// which works best. The tree implementation must be one of the following: AVLTreeRangeMap, RedBlackTreeRangeMap, or
        /// SplayTreeRangeMap (or the long variants if useing HugeListLong), and the generic type parameter must be T[], where
        /// T is the same concrete type used to specialize the HugeList. The provided template collection must be empty.
        /// The HugeList will use the default maximum block size of 512.
        /// </summary>
        /// <param name="storage">An instance of the specific tree implementation to use</param>
        /// <exception cref="ArgumentNullException">storage is null</exception>
        /// <exception cref="ArgumentException">storage is not empty</exception>
        public HugeListBase([Widen]IRangeMap<T[]> storage)
        {
            if (storage == null)
            {
                throw new ArgumentNullException();
            }
            if (storage.Count != 0)
            {
                throw new ArgumentException();
            }

            this.tree = (/*[Widen]*/IRangeMap<T[]>)(((ICloneable)storage).Clone());
        }

        /// <summary>
        /// Create a new HugeList based on the provided underlying tree implementation. All tree implementations are functionally
        /// equivalent but may have different performance characteristics. It is recommended to measure the scenario to determine
        /// which works best. The tree implementation must be one of the following: AVLTreeRangeMap, RedBlackTreeRangeMap, or
        /// SplayTreeRangeMap (or the long variants if useing HugeListLong), and the generic type parameter must be T[], where
        /// T is the same concrete type used to specialize the HugeList. The provided template collection must be empty.
        /// The HugeList will use the specified maximum block size. There is a trade-off in the maximum block size. Smaller values
        /// will make small inserts and deletes faster by reducing the size of the array fragments that must be copied, but increase
        /// the number of fragments, slowing queries. Larger block sizes have the converse characteristics. The parameter should
        /// be tuned for the specific application by performance measurement.
        /// </summary>
        /// <param name="storage">an instance of the specific tree implementation to use</param>
        /// <param name="maxBlockSize">the maximum lenngth an internal array fragment is permitted to be</param>
        /// <exception cref="ArgumentNullException">storage is null</exception>
        /// <exception cref="ArgumentException">storage is not empty</exception>
        /// <exception cref="ArgumentOutOfRangeException">maxBlockSize is less than 1</exception>
        public HugeListBase([Widen]IRangeMap<T[]> storage, int maxBlockSize)
            : this(storage)
        {
            if (maxBlockSize < 1)
            {
                throw new ArgumentOutOfRangeException();
            }

            this.maxBlockSize = maxBlockSize;
        }

        /// <summary>
        /// Create a new HugeList based on the provided underlying tree type. All tree implementations are functionally
        /// equivalent but may have different performance characteristics. It is recommended to measure the scenario to determine
        /// which works best. The tree type must be one of the following: AVLTreeRangeMap&lt;&gt;, RedBlackTreeRangeMap&lt;&gt;, or
        /// SplayTreeRangeMap&lt;&gt; (or the long variants if useing HugeListLong). The type parameter should be left unspecialized
        /// as this constructor will specialize it automatically. For example:
        /// <code>new HugeList(AVLTreeRangeMap&lt;&gt;)</code>
        /// The HugeList will use the default maximum block size of 512.
        /// </summary>
        /// <param name="storage">The type of the tree implementation to use</param>
        /// <exception cref="ArgumentNullException">treeType is null</exception>
        /// <exception cref="ArgumentException">treeType is not a compatible type (it takes other than one generic type parameter,
        /// does not have a default constructor, or fails to implement IRangeMap&lt;T%gt;, or IRangeMapLong&lt;T%gt; in the case
        /// of HugeListLong</exception>
        public HugeListBase(Type treeType)
        {
            if (treeType == null)
            {
                throw new ArgumentNullException();
            }
            if (!treeType.ContainsGenericParameters)
            {
                throw new ArgumentException();
            }

            treeType = treeType.MakeGenericType(typeof(T[])); // throws ArgumentException for us if generic signature doesn't match
            ConstructorInfo ci = treeType.GetConstructor(Type.EmptyTypes);
            if (ci == null)
            {
                throw new ArgumentException();
            }

            this.tree = ci.Invoke(null) as /*[Widen]*/IRangeMap<T[]>;
            if (this.tree == null)
            {
                throw new ArgumentException();
            }
        }

        /// <summary>
        /// Create a new HugeList based on the provided underlying tree type. All tree implementations are functionally
        /// equivalent but may have different performance characteristics. It is recommended to measure the scenario to determine
        /// which works best. The tree type must be one of the following: AVLTreeRangeMap&lt;&gt;, RedBlackTreeRangeMap&lt;&gt;, or
        /// SplayTreeRangeMap&lt;&gt; (or the long variants if useing HugeListLong). The type parameter should be left unspecialized
        /// as this constructor will specialize it automatically. For example:
        /// <code>new HugeList(AVLTreeRangeMap&lt;&gt;)</code>
        /// The HugeList will use the specified maximum block size. There is a trade-off in the maximum block size. Smaller values
        /// will make small inserts and deletes faster by reducing the size of the array fragments that must be copied, but increase
        /// the number of fragments, slowing queries. Larger block sizes have the converse characteristics. The parameter should
        /// be tuned for the specific application by performance measurement.
        /// </summary>
        /// <param name="storage">The type of the tree implementation to use</param>
        /// <param name="maxBlockSize">the maximum lenngth an internal array fragment is permitted to be</param>
        /// <exception cref="ArgumentNullException">treeType is null</exception>
        /// <exception cref="ArgumentException">treeType is not a compatible type (it takes other than one generic type parameter,
        /// does not have a default constructor, or fails to implement IRangeMap&lt;T%gt;, or IRangeMapLong&lt;T%gt; in the case
        /// of HugeListLong</exception>
        /// <exception cref="ArgumentOutOfRangeException">maxBlockSize is less than 1</exception>
        public HugeListBase(Type treeType, int maxBlockSize)
            : this(treeType)
        {
            if (maxBlockSize < 1)
            {
                throw new ArgumentOutOfRangeException();
            }

            this.maxBlockSize = maxBlockSize;
        }


        //
        // Public accessors
        //

        public int MaxBlockSize { get { return maxBlockSize; } }

        [Widen]
        public int Count { get { return unchecked((/*[Widen]*/int)tree.GetExtent()); } }

        public bool IsReadOnly { get { return false; } }

        public T this[[Widen] int index]
        {
            get
            {
                int offset, unused;
                T[] segment = Select(index, out offset, out unused);
                return segment[offset];
            }

            set
            {
                int offset, unused;
                T[] segment = Select(index, out offset, out unused);
                segment[offset] = value;
            }
        }

        public void InsertRangeDefault([Widen]int index, [Widen]int count)
        {
            InsertRangeInternal(index, null, count);
        }

        public void InsertRange([Widen]int index, T[] items, [Widen]int offset, [Widen]int count)
        {
            InsertRangeInternal(index, new ArrayProvider(items, offset, count), count);
        }

        public void InsertRange([Widen]int index, [Widen]IHugeList<T> items, [Widen]int offset, [Widen]int count)
        {
            InsertRangeInternal(index, new HugeListProvider(items, offset, count), count);
        }

        public void InsertRange([Widen]int index, T[] items)
        {
            if (items == null)
            {
                throw new ArgumentNullException();
            }

            InsertRange(index, items, 0, items.Length);
        }

        public void InsertRange([Widen]int index, IEnumerable<T> collection)
        {
            unchecked
            {
                if (collection == null)
                {
                    throw new ArgumentNullException();
                }

                T[] staging = new T[maxBlockSize];
                int i = 0;
                foreach (T item in collection)
                {
                    staging[i] = item;
                    i++;
                    if (i == staging.Length)
                    {
                        InsertRangeInternal(index, new ArrayProvider(staging, 0, staging.Length), staging.Length);
                        index += staging.Length;
                        i = 0;
                    }
                }
                InsertRangeInternal(index, new ArrayProvider(staging, 0, i), i);
            }
        }

        public void Insert([Widen]int index, T item)
        {
            InsertRange(index, new T[1] { item });
        }

        public void Add(T item)
        {
            Insert(Count, item);
        }

        public void AddRange(T[] items)
        {
            InsertRange(this.Count, items);
        }

        public void AddRange(IEnumerable<T> collection)
        {
            InsertRange(Count, collection);
        }

        public void AddRange([Widen]IHugeList<T> collection)
        {
            if (collection == null)
            {
                throw new ArgumentNullException();
            }

            InsertRange(tree.GetExtent(), collection, 0, collection.Count);
        }

        public void RemoveRange([Widen]int index, [Widen]int count)
        {
            unchecked
            {
                if ((index < 0) || (count < 0) || (unchecked((/*[Widen]*/uint)index + (/*[Widen]*/uint)count) > unchecked((/*[Widen]*/uint)tree.GetExtent())))
                {
                    throw new ArgumentOutOfRangeException();
                }

                Debug.Assert((currentStartIndex == -1) || (tree.GetLength(currentStartIndex) < tree.GetValue(currentStartIndex).Length));

                /*[Widen]*/
                int start;
                /*[Widen]*/
                int segmentCount;
                T[] segment;
                tree.NearestLessOrEqual(index, out start, out segmentCount, out segment);

                /*[Widen]*/
                int beforeStart;
                if (!tree.NearestLess(start, out beforeStart))
                {
                    beforeStart = -1;
                }

                while (count != 0)
                {
                    int offset = unchecked((int)(index - start));
                    int apportionedToFirstSegment = unchecked((int)Math.Min(segmentCount - offset, count));

                    /*[Widen]*/
                    int nextStart;
                    /*[Widen]*/
                    int nextSegmentCount;
                    T[] nextSegment;
                    if (tree.NearestGreaterOrEqual(index, out nextStart, out nextSegmentCount, out nextSegment)
                        && (count - (nextStart == index ? 0 : apportionedToFirstSegment) >= nextSegmentCount))
                    {
                        // following segment can be deleted in entirity

                        Debug.Assert((nextStart == start) == (start == index));

                        tree.Delete(nextStart);

                        if (currentStartIndex == nextStart)
                        {
                            currentStartIndex = -1;
                        }
                        else if (currentStartIndex > nextStart)
                        {
                            // CASE (A4)
                            currentStartIndex -= nextSegmentCount;
                        }

                        count -= nextSegmentCount;

                        segmentCount = 0;
                        if (index != tree.GetExtent())
                        {
                            tree.NearestLessOrEqual(index, out start, out segmentCount, out segment);
                        }
                    }
                    else
                    {
                        // removal apportioned between current and next (next's portion may be 0)

                        if (nextStart == index)
                        {
                            tree.NearestGreater(index, out nextStart, out nextSegmentCount, out nextSegment);
                        }

                        /*[Widen]*/
                        int originalSegmentCount = segmentCount;

                        if (segmentCount + nextSegmentCount - count <= maxBlockSize)
                        {
                            // one segment will hold this and next, so merge

                            // CASE (A2), (A7)

                            if (segment.Length < maxBlockSize)
                            {
                                // CASE (A6)
                                Array.Resize(ref segment, maxBlockSize);
                                // write segment back to tree deferred to below
                            }

                            Array.Copy(segment, offset + apportionedToFirstSegment, segment, offset, unchecked((int)(segmentCount - (offset + apportionedToFirstSegment))));
                            segmentCount -= apportionedToFirstSegment;
                            count -= apportionedToFirstSegment;

                            int fromNextSegment = unchecked((int)(nextSegmentCount - count));
                            if (fromNextSegment != 0)
                            {
                                // CASE (A5), (A7)
                                Array.Copy(nextSegment, unchecked((int)count), segment, unchecked((int)segmentCount), fromNextSegment);
                                segmentCount += fromNextSegment;
                            }
                            count = 0;

                            if (originalSegmentCount - segmentCount > 0)
                            {
                                Array.Clear(segment, unchecked((int)segmentCount), unchecked((int)(originalSegmentCount - segmentCount)));
                            }

                            if (nextSegmentCount != 0) // might be at end (no next segment)
                            {
                                tree.Delete(nextStart);
                                if (currentStartIndex == nextStart)
                                {
                                    // CASE A21
                                    currentStartIndex = -1;
                                }
                            }
                            tree.Set(start, segmentCount, segment);

                            if (currentStartIndex == start)
                            {
                                currentStartIndex = -1;
                            }
                            else if (currentStartIndex > start)
                            {
                                currentStartIndex -= originalSegmentCount - segmentCount + nextSegmentCount;
                                Debug.Assert(currentStartIndex >= start + segmentCount);
                            }

                            if (segmentCount < maxBlockSize)
                            {
                                ClearIfNotCurrentSegment(start);
                                currentStartIndex = start; // created some free space - CASE (A2)
                            }
                        }
                        else
                        {
                            // either range is contained in one segment,
                            // OR range spans two segments, but after reduction still too large for one segment, so adjust both

                            // keeping the excess in the current (as opposed to next) is more likely to be useful because of the
                            // chance of subsequent insert at that index.

                            // CASE (A1)

                            if (currentStartIndex == nextStart)
                            {
                                // CASE (A3)
                                Debug.Assert(nextSegmentCount < nextSegment.Length);
                                // trim is deferred until after values are removed
                            }
                            else
                            {
                                ClearIfNotCurrentSegment(start);
                            }

                            if (segment.Length < maxBlockSize)
                            {
                                // CASE (A3)
                                Array.Resize(ref segment, maxBlockSize);
                                tree.SetValue(start, segment);
                            }

                            Array.Copy(segment, offset + apportionedToFirstSegment, segment, offset, unchecked((int)(segmentCount - (offset + apportionedToFirstSegment))));
                            Array.Clear(segment, (int)(segmentCount - apportionedToFirstSegment), apportionedToFirstSegment);
                            segmentCount -= apportionedToFirstSegment;
                            count -= apportionedToFirstSegment;
                            tree.SetLength(start, segmentCount);
                            Debug.Assert((currentStartIndex == -1) || (currentStartIndex == start) || (currentStartIndex == nextStart));
                            if (currentStartIndex == nextStart)
                            {
                                currentStartIndex -= apportionedToFirstSegment;
                            }
                            nextStart -= apportionedToFirstSegment;

                            if (count != 0)
                            {
                                // CASE (A8)

                                Debug.Assert(nextSegmentCount != 0);

                                Array.Copy(nextSegment, (int)count, nextSegment, 0, unchecked((int)nextSegmentCount) - count);
                                //Array.Clear(nextSegment, nextSegmentCount - count, count);
                                nextSegmentCount -= count;
                                count = 0;
                                tree.SetLength(nextStart, nextSegmentCount);
                                // At this point nextSegment is invalid because it contains excess space but is not necessarily
                                // maxBlockSize length. Also, free space hasn't been cleared. However, this will all be fixed up
                                // in one resize pass just below where we call TrimSegment().
                            }

                            Debug.Assert(nextSegmentCount != 0);
                            // CASE (A3)
                            // trim segment (either not needed or follows from speculative condition above)
                            TrimSegment(nextStart);

                            currentStartIndex = start;

                            // move start to next segment upon exiting loop, since that one may need to be coalesced with the one
                            // that follows if it is sufficiently small
                            start += segmentCount;
                            segmentCount = nextSegmentCount;
                        }
                    }
                }


                // By construction, segments at start and nextStart will have been joined above, if possible.
                // Try to coalesce a leading or trailing segment that will now fit

                /*[Widen]*/
                int lengthTest;
                Debug.Assert((start == tree.GetExtent()) || tree.TryGetLength(start, out lengthTest));
                Debug.Assert((start == tree.GetExtent()) == (segmentCount == 0));
                if (segmentCount != 0)
                {
                    TryJoinNext(start);
                }
                if (beforeStart >= 0)
                {
                    TryJoinNext(beforeStart);
                }
            }
        }

        public void RemoveAt([Widen]int index)
        {
            RemoveRange(index, 1);
        }

        public bool Remove(T item)
        {
            /*[Widen]*/
            int index = IndexOf(item);
            if (index < 0)
            {
                return false;
            }
            RemoveAt(index);
            return true;
        }

        [Widen]
        public int RemoveAll(Predicate<T> match)
        {
            unchecked
            {
                if (match == null)
                {
                    throw new ArgumentNullException();
                }

                ClearCurrentSegment();

                /*[Widen]*/
                int start = 0;
                /*[Widen]*/
                int removed = 0;
                /*[Widen]*/
                int previousStart = 0;
                /*[Widen]*/
                int previousCount = 0;
                /*[Widen]*/
                int previousPreviousStart = 0;
                /*[Widen]*/
                int previousPreviousCount = 0;
                ushort version = this.version;
                while (true)
                {
                    if ((previousPreviousCount != 0) && (previousCount != 0) && (previousPreviousCount + previousCount <= maxBlockSize))
                    {
                        bool f = TryJoinNext(previousPreviousStart);
                        Debug.Assert(f && (tree.GetLength(previousPreviousStart) == previousPreviousCount + previousCount));
                        previousStart = previousPreviousStart;
                        previousCount += previousPreviousCount;
                        previousPreviousCount = 0;
                    }

                    if (start == Count)
                    {
                        break;
                    }

                    T[] segment;
                    /*[Widen]*/
                    int segmentCount;
                    tree.Get(start, out segmentCount, out segment);

                    int index = 0;
                    for (int i = 0; i < segmentCount; i++)
                    {
                        segment[index] = segment[i];
                        if (!match(segment[i]))
                        {
                            ++index;
                        }
                    }

                    if (version != this.version)
                    {
                        throw new InvalidOperationException();
                    }

                    if (index < segmentCount)
                    {
                        removed += segmentCount;

                        if (index == 0)
                        {
                            tree.Delete(start);
                            continue;
                        }

                        Array.Resize(ref segment, index);
                        tree.Set(start, index, segment);

                        removed -= index;
                    }

                    previousPreviousCount = previousCount;
                    previousPreviousStart = previousStart;
                    previousCount = index;
                    previousStart = start;

                    start += index;
                }

                return removed;
            }
        }

        public void ReplaceRange([Widen]int index, [Widen]int count, T[] items, [Widen]int offset, [Widen]int count2)
        {
            unchecked
            {
                if ((index < 0) || (count < 0) || (offset < 0) || (count2 < 0))
                {
                    throw new ArgumentOutOfRangeException();
                }
                if (items == null)
                {
                    throw new ArgumentNullException();
                }
                if (unchecked((/*[Widen]*/uint)index + (/*[Widen]*/uint)count > (/*[Widen]*/uint)tree.GetExtent())
                    || unchecked((/*[Widen]*/uint)offset + (/*[Widen]*/uint)count2 > (/*[Widen]*/uint)items.Length))
                {
                    throw new ArgumentException();
                }

                /*[Widen]*/
                int delta = count2 - count;
                if (delta < 0)
                {
                    RemoveRange(index, -delta);
                    CopyFrom(index, items, offset, count2);
                }
                else
                {
                    InsertRange(index + count, items, offset + count, delta);
                    CopyFrom(index, items, offset, count);
                }
            }
        }

        public void ReplaceRange([Widen]int index, [Widen]int count, T[] items)
        {
            ReplaceRange(index, count, items, 0, items.Length);
        }

        public void Clear()
        {
            tree.Clear();
            currentStartIndex = -1;
            version = unchecked((ushort)(version + 1));
        }

        public void CopyTo([Widen]int index, T[] array, [Widen]int arrayIndex, [Widen]int count)
        {
            if (array == null)
            {
                throw new ArgumentNullException();
            }
            if ((index < 0) || (count < 0))
            {
                throw new ArgumentOutOfRangeException();
            }
            if (unchecked((/*[Widen]*/uint)arrayIndex + (/*[Widen]*/uint)count > (/*[Widen]*/uint)array.Length))
            {
                throw new ArgumentException();
            }

            IterateRangeBatch(
                index,
                array,
                arrayIndex,
                count,
                CopyToBatchHelper);
        }

        private static void CopyToBatchHelper(T[] v, [Widen]int vOffset, T[] x, [Widen]int xOffset, [Widen]int count1)
        {
            Array.Copy(v, vOffset, x, xOffset, count1);
        }

        public void CopyTo(T[] items, [Widen]int arrayIndex)
        {
            CopyTo(0, items, arrayIndex, Math.Min(items.Length - arrayIndex, Count));
        }

        public void CopyTo(T[] array)
        {
            CopyTo(0, array, 0, Count);
        }

        public void IterateRange([Widen]int index, T[] external, [Widen]int externalOffset, [Widen]int count, IterateOperator<T> op)
        {
            unchecked
            {
                IterateRangeBatch(
                    index,
                    external,
                    externalOffset,
                    count,
                    // must declare anonymous delegate because 'op' is captured
                    delegate (T[] v, /*[Widen]*/int vOffset, T[] x, /*[Widen]*/int xOffset, /*[Widen]*/int count1)
                    {
                        if (x != null)
                        {
                            for (/*[Widen]*/int i = 0; i < count1; i++)
                            {
                                op(ref v[i + vOffset], ref x[i + xOffset]);
                            }
                        }
                        else
                        {
                            for (/*[Widen]*/int i = 0; i < count1; i++)
                            {
                                T unused = default(T);
                                op(ref v[i + vOffset], ref unused);
                            }
                        }
                    });
            }
        }

        public void IterateRangeBatch([Widen]int index, T[] external, [Widen]int externalOffset, [Widen]int count, [Widen]IterateOperatorBatch<T> op)
        {
            unchecked
            {
                if ((count < 0) || (index < 0) || (externalOffset < 0))
                {
                    throw new ArgumentOutOfRangeException();
                }
                if (unchecked((/*[Widen]*/uint)index + (/*[Widen]*/uint)count > (/*[Widen]*/uint)this.Count))
                {
                    throw new ArgumentException();
                }
                if (external != null)
                {
                    if (unchecked((/*[Widen]*/uint)externalOffset + (/*[Widen]*/uint)count > (/*[Widen]*/uint)external.Length))
                    {
                        throw new ArgumentException();
                    }
                }

                if (count == 0)
                {
                    return;
                }

                /*[Widen]*/
                int start;
                tree.NearestLessOrEqual(index, out start);

                /*[Widen]*/
                int j = externalOffset;
                foreach (EntryRangeMap<T[]> segmentEntry in tree.GetFastEnumerable(start))
                {
                    /*[Widen]*/
                    int offset = index - segmentEntry.Start;

                    /*[Widen]*/
                    int contiguous = Math.Min(count, segmentEntry.Length - offset);
                    op(segmentEntry.Value, offset, external, j, contiguous);
                    j += contiguous;

                    count -= contiguous;
                    index += contiguous;

                    if (count == 0)
                    {
                        break;
                    }
                }
            }
        }

        [Widen]
        public int BinarySearch([Widen]int start, [Widen]int count, T value, IComparer<T> comparer, bool multi)
        {
            unchecked
            {
                if ((start < 0) || (count < 0))
                {
                    throw new ArgumentOutOfRangeException();
                }
                if (unchecked((/*[Widen]*/uint)start + (/*[Widen]*/uint)count > (/*[Widen]*/uint)tree.GetExtent()))
                {
                    throw new ArgumentException();
                }
                if (comparer == null)
                {
                    comparer = Comparer<T>.Default;
                }
                /*[Widen]*/
                int lower = start;
                /*[Widen]*/
                int upper = start + count - 1;
                while (lower <= upper)
                {
                    /*[Widen]*/
                    int middle = unchecked((/*[Widen]*/int)(((/*[Widen]*/uint)lower + (/*[Widen]*/uint)upper) / 2)); // avoid overflow for large arrays

                    int c = comparer.Compare(this[middle], value);
                    if (c == 0)
                    {
                        if (multi)
                        {
                            while ((middle > start) && (0 == comparer.Compare(this[middle - 1], value)))
                            {
                                middle--;
                            }
                        }
                        return middle;
                    }
                    else if (c < 0)
                    {
                        lower = middle + 1;
                    }
                    else
                    {
                        upper = middle - 1;
                    }
                }
                return ~lower;
            }
        }

        [Widen]
        public int BinarySearch([Widen]int start, [Widen]int count, T value, IComparer<T> comparer)
        {
            return BinarySearch(start, count, value, comparer, false/*multi*/);
        }

        [Widen]
        public int BinarySearch(T item, IComparer<T> comparer)
        {
            return BinarySearch(0, Count, item, comparer, false/*multi*/);
        }

        [Widen]
        public int BinarySearch(T item)
        {
            return BinarySearch(0, Count, item, null/*use default comparer*/, false/*multi*/);
        }

        [Widen]
        public int IndexOfAny(T[] values, [Widen]int index, [Widen]int count)
        {
            unchecked
            {
                if (values == null)
                {
                    throw new ArgumentNullException();
                }
                if ((index < 0) || (count < 0))
                {
                    throw new ArgumentOutOfRangeException();
                }
                if (unchecked((/*[Widen]*/uint)index + (/*[Widen]*/uint)count > (/*[Widen]*/uint)tree.GetExtent()))
                {
                    throw new ArgumentException();
                }

                /*[Widen]*/
                int start;
                tree.NearestLessOrEqual(index, out start);

                foreach (EntryRangeMap<T[]> segmentEntry in tree.GetFastEnumerable(start))
                {
                    int offset = unchecked((int)(index - segmentEntry.Start));

                    int c = unchecked((int)Math.Min(segmentEntry.Length - offset, count));
                    int bestIndex = unchecked((int)(segmentEntry.Length));
                    for (int i = 0; i < values.Length; i++)
                    {
                        int p = Array.IndexOf<T>(segmentEntry.Value, values[i], offset, c);
                        if ((p >= 0) && (bestIndex > p))
                        {
                            bestIndex = p;
                        }
                    }
                    if (bestIndex < segmentEntry.Length)
                    {
                        return segmentEntry.Start + bestIndex;
                    }

                    index += c;
                    count -= c;

                    if (count == 0)
                    {
                        break;
                    }
                }

                return -1;
            }
        }

        [Widen]
        public int IndexOf(T value, [Widen]int index, [Widen]int count)
        {
            unchecked
            {
                if ((index < 0) || (count < 0))
                {
                    throw new ArgumentOutOfRangeException();
                }
                if (unchecked((/*[Widen]*/uint)index + (/*[Widen]*/uint)count > (/*[Widen]*/uint)tree.GetExtent()))
                {
                    throw new ArgumentException();
                }

                /*[Widen]*/
                int start;
                tree.NearestLessOrEqual(index, out start);

                foreach (EntryRangeMap<T[]> segmentEntry in tree.GetFastEnumerable(start))
                {
                    int offset = unchecked((int)(index - segmentEntry.Start));

                    int c = unchecked((int)Math.Min(segmentEntry.Length - offset, count));
                    int p = Array.IndexOf<T>(segmentEntry.Value, value, offset, c);
                    if (p >= 0)
                    {
                        return segmentEntry.Start + p;
                    }

                    index += c;
                    count -= c;

                    if (count == 0)
                    {
                        break;
                    }
                }

                return -1;
            }
        }

        [Widen]
        public int IndexOf(T value, [Widen]int start)
        {
            return IndexOf(value, start, Count - start);
        }

        [Widen]
        public int IndexOf(T value)
        {
            return IndexOf(value, 0, Count);
        }

        [Widen]
        public int FindIndex([Widen]int index, [Widen]int count, Predicate<T> match)
        {
            unchecked
            {
                if (match == null)
                {
                    throw new ArgumentNullException();
                }
                if ((index < 0) || (count < 0))
                {
                    throw new ArgumentOutOfRangeException();
                }
                if (unchecked((/*[Widen]*/uint)index + (/*[Widen]*/uint)count > (/*[Widen]*/uint)tree.GetExtent()))
                {
                    throw new ArgumentException();
                }

                /*[Widen]*/
                int start;
                tree.NearestLessOrEqual(index, out start);

                foreach (EntryRangeMap<T[]> segmentEntry in tree.GetFastEnumerable(start))
                {
                    int offset = unchecked((int)(index - segmentEntry.Start));

                    int c = unchecked((int)Math.Min(segmentEntry.Length - offset, count));
                    int p = Array.FindIndex(segmentEntry.Value, offset, c, match);
                    if (p >= 0)
                    {
                        return segmentEntry.Start + p;
                    }

                    index += c;
                    count -= c;

                    if (count == 0)
                    {
                        break;
                    }
                }

                return -1;
            }
        }

        [Widen]
        public int FindIndex(Predicate<T> match)
        {
            return FindIndex(0, Count, match);
        }

        [Widen]
        public int FindIndex([Widen]int startIndex, Predicate<T> match)
        {
            return FindIndex(startIndex, Count - startIndex, match);
        }

        [Widen]
        public int LastIndexOfAny(T[] values, [Widen]int end, [Widen]int count)
        {
            unchecked
            {
                if (values == null)
                {
                    throw new ArgumentNullException();
                }
                if ((tree.GetExtent() != 0) && (end < 0))
                {
                    throw new ArgumentOutOfRangeException();
                }
                if ((tree.GetExtent() != 0) && (count < 0))
                {
                    throw new ArgumentOutOfRangeException();
                }
                if (tree.GetExtent() == 0)
                {
                    return -1;
                }
                if (end >= tree.GetExtent())
                {
                    throw new ArgumentOutOfRangeException();
                }
                if (count > end + 1)
                {
                    throw new ArgumentOutOfRangeException();
                }

                foreach (EntryRangeMap<T[]> segmentEntry in tree.GetFastEnumerable(end, false/*forward*/))
                {
                    int offset = unchecked((int)(end - segmentEntry.Start));

                    int c = unchecked((int)Math.Min(offset + 1, count));
                    int bestIndex = -1;
                    for (int i = 0; i < values.Length; i++)
                    {
                        int p = Array.LastIndexOf<T>(segmentEntry.Value, values[i], offset, c);
                        if (bestIndex < p)
                        {
                            bestIndex = p;
                        }
                    }
                    if (bestIndex >= 0)
                    {
                        return segmentEntry.Start + bestIndex;
                    }

                    end -= offset + 1;
                    count -= c;

                    if (count == 0)
                    {
                        break;
                    }
                }

                return -1;
            }
        }

        [Widen]
        public int LastIndexOf(T value, [Widen]int end, [Widen]int count)
        {
            unchecked
            {
                if ((tree.GetExtent() != 0) && (end < 0))
                {
                    throw new ArgumentOutOfRangeException();
                }
                if ((tree.GetExtent() != 0) && (count < 0))
                {
                    throw new ArgumentOutOfRangeException();
                }
                if (tree.GetExtent() == 0)
                {
                    return -1;
                }
                if (end >= tree.GetExtent())
                {
                    throw new ArgumentOutOfRangeException();
                }
                if (count > end + 1)
                {
                    throw new ArgumentOutOfRangeException();
                }

                foreach (EntryRangeMap<T[]> segmentEntry in tree.GetFastEnumerable(end, false/*forward*/))
                {
                    int offset = unchecked((int)(end - segmentEntry.Start));

                    int c = unchecked((int)Math.Min(offset + 1, count));
                    int p = Array.LastIndexOf<T>(segmentEntry.Value, value, offset, c);
                    if (p >= 0)
                    {
                        return segmentEntry.Start + p;
                    }

                    end -= offset + 1;
                    count -= c;

                    if (count == 0)
                    {
                        break;
                    }
                }

                return -1;
            }
        }

        [Widen]
        public int LastIndexOf(T value, [Widen]int index)
        {
            unchecked
            {
                return LastIndexOf(value, index, index + 1);
            }
        }

        [Widen]
        public int LastIndexOf(T value)
        {
            unchecked
            {
                return LastIndexOf(value, tree.GetExtent() - 1, tree.GetExtent());
            }
        }

        [Widen]
        public int FindLastIndex([Widen]int end, [Widen]int count, Predicate<T> match)
        {
            unchecked
            {
                if (tree.GetExtent() == 0)
                {
                    if (end != -1)
                    {
                        throw new ArgumentOutOfRangeException();
                    }
                }
                else
                {
                    if (unchecked((/*[Widen]*/uint)end >= (/*[Widen]*/uint)tree.GetExtent()))
                    {
                        throw new ArgumentOutOfRangeException();
                    }
                }
                if ((count < 0) || (end - count + 1 < 0))
                {
                    throw new ArgumentOutOfRangeException();
                }

                foreach (EntryRangeMap<T[]> segmentEntry in tree.GetFastEnumerable(end, false/*forward*/))
                {
                    int offset = unchecked((int)(end - segmentEntry.Start));

                    int c = unchecked((int)Math.Min(offset + 1, count));
                    int p = Array.FindLastIndex<T>(segmentEntry.Value, offset, c, match);
                    if (p >= 0)
                    {
                        return segmentEntry.Start + p;
                    }

                    end -= offset + 1;
                    count -= c;

                    if (count == 0)
                    {
                        break;
                    }
                }

                return -1;
            }
        }

        [Widen]
        public int FindLastIndex([Widen]int startIndex, Predicate<T> match)
        {
            unchecked
            {
                return FindLastIndex(startIndex, Count == 0 ? 0 : startIndex + 1, match);
            }
        }

        [Widen]
        public int FindLastIndex(Predicate<T> match)
        {
            unchecked
            {
                return FindLastIndex(Count - 1, match);
            }
        }

        public bool Contains(T value)
        {
            return IndexOf(value) >= 0;
        }

        public T[] ToArray()
        {
            T[] array = new T[Count];
            CopyTo(array);
            return array;
        }


        //
        // Internals
        //

        private abstract class Provider
        {
            [Widen]
            public abstract int Count { get; }
            public abstract void Take(T[] array, int arrayOffset, int count);
        }

        private sealed class ArrayProvider : Provider
        {
            private readonly T[] data;
            [Widen]
            private int dataOffset;
            [Widen]
            private int dataCount;

            public ArrayProvider(T[] data, [Widen]int dataOffset, [Widen]int dataCount)
            {
                if (data == null)
                {
                    throw new ArgumentNullException();
                }
                if ((dataOffset < 0) || (dataCount < 0))
                {
                    throw new ArgumentOutOfRangeException();
                }
                if (unchecked((/*[Widen]*/uint)dataOffset + (/*[Widen]*/uint)dataCount > (/*[Widen]*/uint)data.Length))
                {
                    throw new ArgumentException();
                }

                this.data = data;
                this.dataOffset = dataOffset;
                this.dataCount = dataCount;
            }

            public override void Take(T[] array, int arrayOffset, int count)
            {
                Debug.Assert((arrayOffset >= 0) && (count >= 0)
                    && unchecked((/*[Widen]*/uint)arrayOffset + (/*[Widen]*/uint)count <= (/*[Widen]*/uint)array.Length));
                Debug.Assert(count <= dataCount);
                Array.Copy(data, dataOffset, array, arrayOffset, count);
                dataOffset += count;
                dataCount -= count;
            }

            [Widen]
            public override int Count { get { return dataCount; } }
        }

        private sealed class HugeListProvider : Provider
        {
            [Widen]
            private IHugeList<T> list;
            [Widen]
            private int listOffset;
            [Widen]
            private int listCount;
            private readonly IEnumerator<EntryRangeMap<T[]>> enumerator;
            private EntryRangeMap<T[]> current;

            public HugeListProvider([Widen]IHugeList<T> list, [Widen]int listOffset, [Widen]int listCount)
            {
                if (list == null)
                {
                    throw new ArgumentNullException();
                }
                if ((listOffset < 0) || (listCount < 0))
                {
                    throw new ArgumentOutOfRangeException();
                }
                if (unchecked((/*[Widen]*/uint)listOffset + (/*[Widen]*/uint)listCount > (/*[Widen]*/uint)list.Count))
                {
                    throw new ArgumentException();
                }

                this.list = list;
                this.listOffset = listOffset;
                this.listCount = listCount;
                this.enumerator = list.GetEnumerableChunked(listOffset).GetEnumerator();
            }

            public override void Take(T[] array, int arrayOffset, int count)
            {
                Debug.Assert((arrayOffset >= 0) && (count >= 0)
                    && unchecked((/*[Widen]*/uint)arrayOffset + (/*[Widen]*/uint)count <= (/*[Widen]*/uint)array.Length));
                Debug.Assert(count <= listCount);

                while (count != 0)
                {
                    if (current.Start + current.Length <= listOffset)
                    {
                        enumerator.MoveNext();
                        current = enumerator.Current;
                    }
                    int offset = unchecked((int)(listOffset - current.Start));
                    int c = unchecked((int)Math.Min(current.Length - offset, count));
                    Array.Copy(current.Value, offset, array, arrayOffset, c);
                    listOffset += c;
                    listCount -= c;
                    arrayOffset += c; // CASE (A25)
                    count -= c;
                }
            }

            [Widen]
            public override int Count { get { return listCount; } }
        }

        private void InsertRangeInternal([Widen]int index, Provider source/*optional*/, [Widen]int count)
        {
            unchecked
            {
                if ((index < 0) || (count < 0))
                {
                    throw new ArgumentOutOfRangeException();
                }
                if (index > tree.GetExtent())
                {
                    throw new ArgumentException();
                }
                checked
                {
                    /*[Widen]*/
                    int q;
                    q = checked(index + count);
                }
                Debug.Assert((source == null) || (count == source.Count)); // not externally provided

                if (count == 0)
                {
                    return;
                }

                version = unchecked((ushort)(version + 1));


                /*[Widen]*/
                int previousStart = 0;
                /*[Widen]*/
                int previousSegmentCount = 0;
                T[] previousSegment = null;

                /*[Widen]*/
                int start;
                /*[Widen]*/
                int segmentCount;
                T[] segment;

                if (tree.NearestLessOrEqual(index, out start, out segmentCount, out segment))
                {
                    if ((start + segmentCount == index) && (segmentCount == maxBlockSize) && (index == tree.GetExtent()))
                    {
                        // if end of tree and previous segment is full, force new segment insertion
                        start = index;
                        segmentCount = 0;
                        segment = null;
                    }
                    // obtain preceding segment (if index is at start of a segment, it is viewed as if index is between two segments)
                    tree.NearestLess(start, out previousStart, out previousSegmentCount, out previousSegment);
                }
                Debug.Assert((segmentCount == 0) || (index - start <= maxBlockSize));

                /*[Widen]*/
                int originalStart = start;

                T[] extra = null;
                int extraOffset = 0;
                int extraCount = 0;

                while ((count != 0) || (extraCount != 0))
                {
                    Debug.Assert((segmentCount == 0) || (index - start <= maxBlockSize));
                    Debug.Assert((previousSegmentCount == 0) || ((currentStartIndex == previousStart) == (previousSegment.Length > previousSegmentCount)));

                    if ((start == index) && (previousSegmentCount != 0)
                        && ((currentStartIndex == previousStart) || (previousSegmentCount < maxBlockSize)))
                    {
                        // index is at start of current and previous segment has room, fill at end as much as will fit

                        // CASE (A9), (A10)

                        Debug.Assert(extraCount == 0);

                        int c;
                        if (currentStartIndex == previousStart)
                        {
                            // CASE (A9), (A10)
                            Debug.Assert(previousSegment.Length == maxBlockSize);
                            c = unchecked((int)Math.Min(count, maxBlockSize - previousSegmentCount));
                        }
                        else
                        {
                            // CASE (A15)
                            Debug.Assert(currentStartIndex != previousStart);
                            ClearCurrentSegment();

                            Array.Resize(ref previousSegment, maxBlockSize);
                            // write segment back to tree deferred to below
                            c = unchecked((int)Math.Min(count, maxBlockSize - previousSegmentCount));
                            currentStartIndex = previousStart;
                        }

                        if (source != null)
                        {
                            source.Take(previousSegment, unchecked((int)previousSegmentCount), c);
                        }

                        previousSegmentCount += c;

                        tree.Set(previousStart, previousSegmentCount, previousSegment);

                        Debug.Assert((currentStartIndex == -1) || (currentStartIndex == previousStart));
                        if (previousSegmentCount == maxBlockSize)
                        {
                            // CASE (A9)
                            currentStartIndex = -1; // we just filled it completely
                        }
                        // else CASE (A10)

                        Debug.Assert(originalStart == index);
                        originalStart += c; // this is just for the benefit of the asserts at the end of the method

                        count -= c;
                        index += c;
                        start += c;
                    }
                    else if (((start == index) && ((count >= maxBlockSize) || (segmentCount == maxBlockSize))) || (segmentCount == 0))
                    {
                        // at start of segment and at least one segment of data available, insert new full segment
                        // OR list is empty or at end
                        // OR previous (from preceding case) and next segments are full

                        // CASE (A11), (A14) (occurs after the case that follows) [count == 0]
                        // CASE (A15) (occurs after the previous case)

                        T[] newSegment = new T[maxBlockSize];
                        int newSegmentCount = unchecked((int)Math.Min(maxBlockSize, count)); // in case segment is empty
                        if (source != null)
                        {
                            source.Take(newSegment, 0, newSegmentCount);
                        }
                        count -= newSegmentCount;

                        if ((count == 0) && (extraCount != 0))
                        {
                            // CASE (A11)

                            int c = unchecked((int)Math.Min(maxBlockSize - newSegmentCount, extraCount));
                            Array.Copy(extra, extraOffset, newSegment, newSegmentCount, c);

                            extraOffset += c;
                            extraCount -= c;
                            newSegmentCount += c;
                        }
                        Debug.Assert(newSegmentCount != 0);

                        tree.Insert(index, newSegmentCount, newSegment);
                        start = index;

                        if (currentStartIndex >= start)
                        {
                            currentStartIndex += newSegmentCount;
                        }
                        if (newSegmentCount < maxBlockSize)
                        {
                            ClearCurrentSegment();
                            currentStartIndex = start;
                        }

                        index += newSegmentCount;
                        start = index;
                    }
                    else if (index != start)
                    {
                        // insert into interior of segment - may generate extra
                        // this may occur for count > maxBlockSize (handling the prefix before the above case reduces full blocks)

                        Debug.Assert(extraCount == 0);

                        int originalSegmentCount = unchecked((int)segmentCount);
                        int offset = unchecked((int)(index - start));

                        int c = unchecked((int)Math.Min(count, maxBlockSize - offset));
                        if (segment.Length < maxBlockSize)
                        {
                            // CASE (A17)
                            Array.Resize(ref segment, maxBlockSize);
                            // write segment back to tree deferred to below
                        }

                        if (segmentCount + count <= maxBlockSize)
                        {
                            Array.Copy(segment, offset, segment, offset + c, segmentCount - offset);
                            if (source == null)
                            {
                                Array.Clear(segment, offset, c);
                            }
                            segmentCount += c;
                        }
                        else
                        {
                            // CASE (A11), (A12), (A13)

                            extra = segment;
                            extraOffset = offset;
                            extraCount = unchecked((int)segmentCount) - offset;

                            segment = new T[maxBlockSize];
                            Array.Copy(extra, 0, segment, 0, offset);
                            // write segment back to tree deferred to below

                            segmentCount = offset + c;
                        }
                        if (source != null)
                        {
                            source.Take(segment, offset, c);
                        }
                        count -= c;

                        // for small blocks, we can incorporate some extra
                        int d = unchecked((int)Math.Min(extraCount, maxBlockSize - segmentCount));
                        if (d != 0)
                        {
                            Array.Copy(extra, extraOffset, segment, segmentCount, d);
                            extraOffset += d;
                            extraCount -= d;
                            segmentCount += d;
                        }

                        tree.Set(start, segmentCount, segment);

                        Debug.Assert(maxBlockSize >= offset + c + d);
                        Debug.Assert(maxBlockSize >= segmentCount);
                        if (currentStartIndex > start)
                        {
                            // CASE (A12)
                            currentStartIndex += (segmentCount - originalSegmentCount);
                        }
                        if (segmentCount < maxBlockSize)
                        {
                            ClearIfNotCurrentSegment(start);
                            currentStartIndex = start;
                        }
                        else if (currentStartIndex == start)
                        {
                            currentStartIndex = -1;
                        }

                        start += segmentCount;
                        index = start;

                        tree.TryGet(start, out segmentCount, out segment);
                    }
                    else if ((count != 0) && ((count + extraCount > maxBlockSize) || (extraCount + segmentCount > maxBlockSize)))
                    {
                        // still can't fit in a single segment - insert all of count, and as much of extra as will fit

                        // CASE (A18)

                        Debug.Assert(count < maxBlockSize);
                        Debug.Assert((count != 0) && (extraCount != 0));
                        Debug.Assert(index == start);

                        segment = new T[maxBlockSize];
                        if (source != null)
                        {
                            source.Take(segment, 0, unchecked((int)count));
                            Debug.Assert(source.Count == 0);
                        }
                        segmentCount = count;
                        count = 0;

                        int d = unchecked((int)Math.Min(extraCount, maxBlockSize - segmentCount));
                        Debug.Assert(d != 0);
                        Array.Copy(extra, extraOffset, segment, segmentCount, d);
                        extraOffset += d;
                        extraCount -= d;
                        segmentCount += d;

                        tree.Insert(start, segmentCount, segment);

                        if (currentStartIndex >= start)
                        {
                            // CASE (A18)
                            currentStartIndex += segmentCount;
                        } // else CASE (A20)
                        ClearIfNotCurrentSegment(start);
                        if (segmentCount < maxBlockSize)
                        {
                            // CASE (A22)
                            currentStartIndex = start;
                        }

                        start += segmentCount;
                        index = start;

                        tree.TryGet(start, out segmentCount, out segment);
                    }
                    else
                    {
                        // insert any remaining count and extra

                        // CASE (A0)

                        Debug.Assert(index == start);
                        Debug.Assert(count + extraCount <= maxBlockSize);
                        Debug.Assert((currentStartIndex != start) || (segment.Length > segmentCount));

                        bool canAbsorbFollower = count + extraCount + segmentCount <= maxBlockSize;
                        if (!canAbsorbFollower || (currentStartIndex != start))
                        {
                            // CASE (A12), (A13)
                            ClearCurrentSegment();
                        }

                        T[] newSegment = new T[maxBlockSize];
                        if (source != null)
                        {
                            source.Take(newSegment, 0, unchecked((int)count));
                        }
                        if (extraCount != 0)
                        {
                            // CASE (A12)
                            Array.Copy(extra, extraOffset, newSegment, count, extraCount);
                        }
                        int newSegmentCount = unchecked((int)(count + extraCount));
                        count = 0;
                        extraCount = 0;

                        // try to absorb the following
                        if (newSegmentCount + segmentCount <= maxBlockSize)
                        {
                            // CASE (A0)
                            Debug.Assert(canAbsorbFollower);
                            Array.Copy(segment, 0, newSegment, newSegmentCount, segmentCount);
                            newSegmentCount += unchecked((int)segmentCount);
                            tree.Delete(start);
                        }
                        else
                        {
                            // CASE (A13)
                            Debug.Assert(!canAbsorbFollower);
                        }

                        tree.Insert(start, newSegmentCount, newSegment);

                        currentStartIndex = -1;
                        if (newSegmentCount < maxBlockSize)
                        {
                            currentStartIndex = start;
                        }

                        index += newSegmentCount; // must exit loop with on valid segment
                    }

                    previousSegmentCount = 0;
                }


                // verify no mergeable blocks were left at the start edge or end edge
                Debug.Assert((0 == tree.GetExtent()) || (tree.GetLength(originalStart) + originalStart == tree.GetExtent())
                    || (tree.GetLength(originalStart) + tree.GetLength(originalStart + tree.GetLength(originalStart)) > maxBlockSize));
                /*[Widen]*/
                int previousStartTest;
                Debug.Assert((index == tree.GetExtent())
                    || (tree.NearestLess(index, out previousStartTest)
                        && (tree.GetLength(previousStartTest) + tree.GetLength(index) > maxBlockSize)));
            }
        }

        private T[] Select([Widen]int index, out int offset, out int count)
        {
            if (unchecked((/*[Widen]*/uint)index >= (/*[Widen]*/uint)tree.GetExtent()))
            {
                throw new ArgumentOutOfRangeException();
            }

            /*[Widen]*/
            int start, count2;
            T[] segment;
            tree.NearestLessOrEqual(index, out start, out count2, out segment);
            offset = unchecked((int)(index - start));
            count = unchecked((int)count2);
            return segment;
        }

        private bool TryJoinNext([Widen]int index)
        {
            unchecked
            {
                /*[Widen]*/
                int countL;
                T[] segmentL;
                tree.Get(index, out countL, out segmentL);

                /*[Widen]*/
                int countR;
                T[] segmentR;
                if (!tree.TryGet(index + countL, out countR, out segmentR))
                {
                    return false;
                }

                if (countL + countR <= maxBlockSize)
                {
                    if (segmentL.Length > countL)
                    {
                        // left side has excess - absorb right into left's buffer

                        Debug.Assert(currentStartIndex == index);
                        Debug.Assert(segmentL.Length == maxBlockSize);

                        Array.Copy(segmentR, 0, segmentL, countL, countR);

                        tree.Delete(index + countL);
                        tree.SetLength(index, countL + countR);

                        if (countL + countR == maxBlockSize)
                        {
                            // CASE (A24)
                            currentStartIndex = -1; // used up all the capacity
                        }
                    }
                    else if (segmentR.Length > countR)
                    {
                        // right side has excess - absorb left into right's buffer

                        Debug.Assert(currentStartIndex == index + countL);
                        Debug.Assert(segmentR.Length == maxBlockSize);

                        Array.Copy(segmentR, 0, segmentR, countL, countR);
                        Array.Copy(segmentL, 0, segmentR, 0, countL);

                        tree.Delete(index);
                        tree.SetLength(index, countL + countR);

                        currentStartIndex = index;
                        if (countL + countR == maxBlockSize)
                        {
                            currentStartIndex = -1; // used it up
                        }
                    }
                    else
                    {
                        // CASE (A23)

                        Debug.Assert((currentStartIndex != index) && (currentStartIndex != index + countL));

                        ClearCurrentSegment();

                        T[] segment = new T[maxBlockSize];
                        Array.Copy(segmentL, 0, segment, 0, countL);
                        Array.Copy(segmentR, 0, segment, countL, countR);

                        tree.Delete(index);
                        tree.Set(index, countL + countR, segment);

                        if (countL + countR < maxBlockSize)
                        {
                            currentStartIndex = index;
                        } // else: CASE (A25)
                    }

                    return true;
                }

                return false;
            }
        }

        private void TrimSegment([Widen]int startIndex)
        {
            /*[Widen]*/
            int length;
            T[] segment;
            tree.Get(startIndex, out length, out segment);
            if (segment.Length > unchecked((int)length))
            {
                Array.Resize(ref segment, unchecked((int)length));
                tree.SetValue(startIndex, segment);
            }
        }

        private void ClearIfNotCurrentSegment([Widen]int start)
        {
            if ((currentStartIndex >= 0) && (currentStartIndex != start))
            {
                TrimSegment(currentStartIndex);
                currentStartIndex = -1;
            }
        }

        private void ClearCurrentSegment()
        {
            if (currentStartIndex >= 0)
            {
                TrimSegment(currentStartIndex);
                currentStartIndex = -1;
            }
        }

        private void CopyFrom([Widen]int index, T[] array, [Widen]int arrayIndex, [Widen]int count)
        {
            Debug.Assert(array != null);
            IterateRangeBatch(
                index,
                array,
                arrayIndex,
                count,
                delegate (T[] v, /*[Widen]*/int vOffset, T[] x, /*[Widen]*/int xOffset, /*[Widen]*/int count1)
                {
                    Array.Copy(x, xOffset, v, vOffset, count1);
                });
        }


        //
        // Enumeration
        //

        public IEnumerator<T> GetEnumerator()
        {
            return new Enumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerable<EntryRangeMap<T[]>> GetEnumerableChunked()
        {
            return new ChunkedEnumerableSurrogate(this, -2/*default start*/, true/*forward*/);
        }

        public IEnumerable<EntryRangeMap<T[]>> GetEnumerableChunked([Widen] int start)
        {
            if (start < 0)
            {
                throw new ArgumentOutOfRangeException();
            }
            if (start > tree.GetExtent())
            {
                throw new ArgumentException();
            }

            return new ChunkedEnumerableSurrogate(this, start, true/*forward*/);
        }

        public IEnumerable<EntryRangeMap<T[]>> GetEnumerableChunked(bool forward)
        {
            return new ChunkedEnumerableSurrogate(this, -2/*default start*/, forward);
        }

        public IEnumerable<EntryRangeMap<T[]>> GetEnumerableChunked([Widen] int start, bool forward)
        {
            if (forward)
            {
                if (start < 0)
                {
                    throw new ArgumentOutOfRangeException();
                }
                if (start > tree.GetExtent())
                {
                    throw new ArgumentException();
                }
            }
            else
            {
                if (start < -1)
                {
                    throw new ArgumentOutOfRangeException();
                }
                if (start >= tree.GetExtent())
                {
                    throw new ArgumentException();
                }
            }

            return new ChunkedEnumerableSurrogate(this, start, forward);
        }

        private class Enumerator : IEnumerator<T>
        {
            private readonly IEnumerator<EntryRangeMap<T[]>> inner;
            private readonly HugeListBase<T> list;
            private EntryRangeMap<T[]> current;
            private int offset;
            private bool started, valid;
            private ushort version;

            public Enumerator(HugeListBase<T> list)
            {
                this.list = list;
                this.inner = list.tree.GetEnumerator();
                Reset();
            }

            public T Current
            {
                get
                {
                    if (valid)
                    {
                        return current.Value[offset];
                    }
                    return default(T);
                }
            }

            object IEnumerator.Current { get { return Current; } }

            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                unchecked
                {
                    if (!started)
                    {
                        started = true;
                        valid = inner.MoveNext();
                        current = inner.Current; // make current.Length = 0 for below
                        offset = -1;
                    }
                    if (valid)
                    {
                        if (version != list.version)
                        {
                            throw new InvalidOperationException();
                        }
                        offset++;
                        Debug.Assert((offset >= 0) && (offset <= current.Length));
                        if (offset == current.Length)
                        {
                            valid = inner.MoveNext();
                            current = inner.Current;
                            offset = 0;
                        }
                    }
                    return valid;
                }
            }

            public void Reset()
            {
                inner.Reset();
                started = false;
                valid = false;
                version = list.version;
            }
        }

        private class ChunkedEnumerableSurrogate : IEnumerable<EntryRangeMap<T[]>>
        {
            private readonly HugeListBase<T> list;
            [Widen]
            private readonly int start; // -1 == default (beginning or end, depending on value of forward)
            private readonly bool forward;

            public ChunkedEnumerableSurrogate(HugeListBase<T> list, [Widen]int start, bool forward)
            {
                this.list = list;
                this.start = start;
                this.forward = forward;
            }

            public IEnumerator<EntryRangeMap<T[]>> GetEnumerator()
            {
                return new ChunkedEnumerator(list, start, forward);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return this.GetEnumerator();
            }
        }

        private class ChunkedEnumerator : IEnumerator<EntryRangeMap<T[]>>
        {
            private readonly IEnumerator<EntryRangeMap<T[]>> inner;
            private readonly HugeListBase<T> list;
            private uint version;

            public ChunkedEnumerator(HugeListBase<T> list, [Widen]int start/*-2==default*/, bool forward)
            {
                this.list = list;

                if (forward || (start >= 0))
                {
                    // for all forward enumeration or backward starting on a real item, find the start of the containing segment
                    list.tree.NearestLessOrEqual(start, out start);
                }

                if (start >= -1)
                {
                    // for enumerations where start is specified, pass it through
                    this.inner = list.tree.GetEnumerable(start, forward).GetEnumerator();
                }
                else
                {
                    // for enumerations where start is not specified, do not pass in extent (in case list size increases
                    // and enumerator is reset) - instead use the unconstrained enumerator of the specified direction
                    this.inner = list.tree.GetEnumerable(forward).GetEnumerator();
                }

                Reset();
            }

            public EntryRangeMap<T[]> Current
            {
                get
                {
                    EntryRangeMap<T[]> innerCurrent = inner.Current;
                    // rebuild struct without SetValue() callback info
                    return new EntryRangeMap<T[]>(innerCurrent.Value, innerCurrent.Start, innerCurrent.Length);
                }
            }

            object IEnumerator.Current { get { return Current; } }

            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                unchecked
                {
                    if (version != list.version)
                    {
                        throw new InvalidOperationException();
                    }

                    return inner.MoveNext();
                }
            }

            public void Reset()
            {
                inner.Reset();
                version = list.version;
            }
        }


        //
        // Validation
        //

        [ExcludeFromCodeCoverage]
        void IHugeListValidation.Validate()
        {
            /*[Widen]*/
            int segmentsWithExcess = 0;
            /*[Widen]*/
            int start = 0;
            /*[Widen]*/
            int previousLength = maxBlockSize;

            /*[Widen]*/
            int currentStartIndexSegmentCount = -1;
            foreach (EntryRangeMap<T[]> segment in tree)
            {
                Debug.Assert(segment.Start == start); // tree correctness

                if (start == currentStartIndex)
                {
                    currentStartIndexSegmentCount = segment.Length;
                }

                if (segment.Value.Length < segment.Length)
                {
                    Debug.Assert(false, "segment array length inadequate");
                    throw new InvalidHugeListException("segment array length inadequate");
                }
                if (segment.Value.Length > segment.Length)
                {
                    segmentsWithExcess++;
                }
                if (previousLength + segment.Length <= maxBlockSize)
                {
                    Debug.Assert(false, "adjacent segments that are small enough they should have been coalesced");
                    throw new InvalidHugeListException("adjacent segments that are small enough they should have been coalesced");
                }

                start += segment.Length;
                previousLength = segment.Length;
            }

            if (!((currentStartIndexSegmentCount != -1) == (currentStartIndex != -1)))
            {
                Debug.Assert(false, "currentStartIndex invalid - not found");
                throw new InvalidHugeListException("currentStartIndex invalid - not found");
            }
            if ((currentStartIndexSegmentCount >= 0) && !(currentStartIndexSegmentCount < maxBlockSize))
            {
                Debug.Assert(false, "currentStartIndex invalid - segment is full");
                throw new InvalidHugeListException("currentStartIndex invalid - segment is full");
            }
            if ((currentStartIndex >= 0) && !(tree.GetLength(currentStartIndex) < tree.GetValue(currentStartIndex).Length))
            {
                Debug.Assert(false, "currentStartIndex invalid - segment has no excess capacity");
                throw new InvalidHugeListException("currentStartIndex invalid - segment has no excess capacity");
            }
            if (segmentsWithExcess != (currentStartIndex >= 0 ? 1 : 0))
            {
                Debug.Assert(false, "wrong number of segments with unused capacity");
                throw new InvalidHugeListException("wrong number of segments with unused capacity");
            }
        }

        [ExcludeFromCodeCoverage]
        void IHugeListValidation.Validate(out string dump)
        {
            dump = null;
            try
            {
                ((IHugeListValidation)this).Validate();
            }
            catch (InvalidHugeListException)
            {
                dump = ((IHugeListValidation)this).Metadata;
                throw;
            }
        }

        [ExcludeFromCodeCoverage]
        string IHugeListValidation.Metadata
        {
            get
            {
                using (StringWriter writer = new StringWriter())
                {
                    writer.WriteLine("----- HUGELIST METADATA DUMP -----");
                    writer.WriteLine("extent: {0}", tree.GetExtent());
                    writer.WriteLine("currentStartIndex: {0}", currentStartIndex);
                    writer.WriteLine("maxBlockSize: {0}", maxBlockSize);
                    writer.WriteLine("enumerator version: {0}", version);

                    /*[Widen]*/
                    int segmentsWithExcess = 0;
                    /*[Widen]*/
                    int start = 0;
                    /*[Widen]*/
                    int previousLength = maxBlockSize;
                    /*[Widen]*/
                    int currentStartIndexSegmentCount = -1;

                    StringBuilder line = new StringBuilder();
                    foreach (EntryRangeMap<T[]> segment in tree)
                    {
                        Debug.Assert(segment.Start == start); // tree correctness

                        line.Clear();
                        line.AppendFormat(
                            "{0,8}..{1,8} ({2,8},{3,8}) {4,6} {5,7} {6,8}",
                            segment.Start,
                            segment.Start + segment.Length - 1,
                            segment.Length,
                            segment.Value.Length,
                            segment.Length < segment.Value.Length ? "EXCESS" : (segment.Length > segment.Value.Length ? "!!!TOO SMALL!!!" : null),
                            start == currentStartIndex ? "CURRENT" : null,
                            previousLength + segment.Length <= maxBlockSize ? "COALESCE" : null);
                        writer.WriteLine(line);

                        if (start == currentStartIndex)
                        {
                            currentStartIndexSegmentCount = segment.Length;
                        }

                        if (segment.Value.Length > segment.Length)
                        {
                            segmentsWithExcess++;
                        }

                        start += segment.Length;
                        previousLength = segment.Length;
                    }

                    if (!((currentStartIndexSegmentCount != -1) == (currentStartIndex != -1)))
                    {
                        writer.WriteLine("currentStartIndex invalid - not found");
                    }
                    if ((currentStartIndexSegmentCount >= 0) && !(currentStartIndexSegmentCount < maxBlockSize))
                    {
                        writer.WriteLine("currentStartIndex invalid - segment is full");
                    }
                    if ((currentStartIndex >= 0) && !(tree.GetLength(currentStartIndex) < tree.GetValue(currentStartIndex).Length))
                    {
                        writer.WriteLine("currentStartIndex invalid - segment has no excess capacity");
                    }
                    if (segmentsWithExcess != (currentStartIndex >= 0 ? 1 : 0))
                    {
                        writer.WriteLine("wrong number of segments with unused capacity: {0}, should be {1}", segmentsWithExcess, currentStartIndex >= 0 ? 1 : 0);
                    }
                    writer.WriteLine();

                    return writer.ToString();
                }
            }
        }
    }
}
