// NOTE: This file is auto-generated. DO NOT MAKE CHANGES HERE! They will be overwritten on rebuild.

/*
 *  Copyright � 2016 Thomas R. Lawrence
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
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using TreeLib.Internal;

// Implementation of top-down splay tree written by Daniel Sleator <sleator@cs.cmu.edu>.
// Taken from http://www.link.cs.cmu.edu/link/ftp-site/splaying/top-down-splay.c

// An overview of splay trees can be found here: https://en.wikipedia.org/wiki/Splay_tree

namespace TreeLib
{

    /// <summary>
    /// Implements a map, list or range collection using a splay tree. 
    /// </summary>
    
    /// <summary>
    /// Represents an sequenced collection of ranges with associated values. Each range is defined by it's length, and occupies
    /// a particular position in the sequence, determined by the location where it was inserted (and any insertions/deletions that
    /// have occurred before or after it in the sequence). The start indices of each range are determined as follows:
    /// The first range in the sequence starts at 0 and each subsequent range starts at the starting index of the previous range
    /// plus the length of the previous range. The 'extent' of the range collection is the sum of all lengths.
    /// All ranges must have a length of at least 1.
    /// </summary>
    /// <typeparam name="ValueType">type of the value associated with each range</typeparam>
    public class SplayTreeArrayRangeMap<[Payload(Payload.Value)] ValueType> :

        /*[Feature(Feature.Range)]*//*[Payload(Payload.Value)]*//*[Widen]*/IRangeMap<ValueType>,

        INonInvasiveTreeInspection,
        /*[Feature(Feature.Range, Feature.Range2)]*//*[Widen]*/INonInvasiveRange2MapInspection,

        IEnumerable<EntryRangeMap<ValueType>>,
        IEnumerable
    {

        //
        // Array form data structure
        //

        //[Storage(Storage.Array)]
        //[StructLayout(LayoutKind.Auto)] // defaults to LayoutKind.Sequential; use .Auto to allow framework to pack key & value optimally
        //private struct Node2
        //{
        //    [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]
        //    public KeyType key;
        //    [Payload(Payload.Value)]
        //    public ValueType value;
        //}

        [Storage(Storage.Array)]
        [StructLayout(LayoutKind.Auto)] // defaults to LayoutKind.Sequential; use .Auto to allow framework to pack key & value optimally
        private struct Node
        {
            public NodeRef left;
            public NodeRef right;
            [Payload(Payload.Value)]
            public ValueType value;

            [Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]
            [Widen]
            public int xOffset;

            //#if DEBUG
            //            public bool scratch_debug;
            //#endif
            //            public override string ToString()
            //            {
            //#if DEBUG
            //                if (scratch_debug)
            //                {
            //                    return "Scratch";
            //                }
            //#endif

            //                string keyText = null;
            //                /*[Storage(Storage.Object)]*/
            //                try
            //                {
            //                    keyText = key.ToString();
            //                }
            //                catch (NullReferenceException)
            //                {
            //                }

            //                string valueText = null;
            //                /*[Storage(Storage.Object)]*/
            //                try
            //                {
            //                    valueText = value.ToString();
            //                }
            //                catch (NullReferenceException)
            //                {
            //                }

            //                string leftText = left == Nil ? "Nil" : left.ToString();

            //                string rightText = right == Nil ? "Nil" : right.ToString();

            //                return String.Format("[{0}]*{2}={3}*[{1}])", leftText, rightText, keyText, valueText);
            //            }
        }

        [Storage(Storage.Array)]
        [StructLayout(LayoutKind.Auto)] // defaults to LayoutKind.Sequential; use .Auto to allow framework to pack key & value optimally
        private struct NodeRef
        {
            public readonly uint node;

            public NodeRef(uint node)
            {
                this.node = node;
            }

            public static implicit operator uint(NodeRef nodeRef)
            {
                return nodeRef.node;
            }

            public static bool operator ==(NodeRef left,NodeRef right)
            {
                return left.node == right.node;
            }

            public static bool operator !=(NodeRef left,NodeRef right)
            {
                return left.node != right.node;
            }

            public override bool Equals(object obj)
            {
                return node == (uint)obj;
            }

            public override int GetHashCode()
            {
                return node.GetHashCode();
            }

            public override string ToString()
            {
                return node.ToString();
            }
        }

        [Storage(Storage.Array)]
        private const uint ReservedElements = 2;
        [Storage(Storage.Array)]
        private readonly static NodeRef _Nil = new NodeRef(0);
        [Storage(Storage.Array)]
        private readonly static NodeRef N = new NodeRef(1);

        [Storage(Storage.Array)]
        private NodeRef Nil { get { return SplayTreeArrayRangeMap<ValueType>._Nil; } } // allow tree.Nil or this.Nil in all cases

        [Storage(Storage.Array)]
        private Node[] nodes;
        //[Storage(Storage.Array)]
        //private Node2[] nodes2;

        //
        // State for both array & object form
        //

        private NodeRef root;
        [Count]
        private uint count;

        [Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]
        [Widen]
        private int xExtent;

        private readonly AllocationMode allocationMode;
        private NodeRef freelist;

        private ushort version;

        // Array

        /// <summary>
        /// Create a new collection using an array storage mechanism, based on a splay tree, explicitly configured.
        /// </summary>
        /// <param name="comparer">The comparer to use for sorting keys (present only for keyed collections)</param>
        /// <param name="capacity">
        /// For PreallocatedFixed mode, the maximum capacity of the tree, the memory for which is
        /// preallocated at construction time; exceeding that capacity will result in an OutOfMemory exception.
        /// For DynamicRetainFreelist, the number of nodes to pre-allocate at construction time (the collection
        /// is permitted to exceed that capacity, in which case the internal array will be resized to increase the capacity).
        /// DynamicDiscard is not permitted for array storage trees.
        /// </param>
        /// <param name="allocationMode">The allocation mode (see capacity)</param>
        /// <exception cref="ArgumentException">an allocation mode of DynamicDiscard was specified</exception>
        [Storage(Storage.Array)]
        public SplayTreeArrayRangeMap(uint capacity,AllocationMode allocationMode)
        {
            if (allocationMode == AllocationMode.DynamicDiscard)
            {
                throw new ArgumentException();
            }
            this.root = Nil;

            this.allocationMode = allocationMode;
            this.freelist = Nil;
            EnsureFree(capacity);
            //#if DEBUG
            //            nodes[N].scratch_debug = true;
            //#endif
        }

        /// <summary>
        /// Create a new collection using an array storage mechanism, based on a splay tree, with the specified capacity and using
        /// the default comparer (applicable only for keyed collections). The allocation mode is DynamicRetainFreelist.
        /// </summary>
        /// <param name="capacity">
        /// The initial capacity of the tree, the memory for which is preallocated at construction time;
        /// if the capacity is exceeded, the internal array will be resized to make more nodes available.
        /// </param>
        [Storage(Storage.Array)]
        public SplayTreeArrayRangeMap(uint capacity)
            : this(capacity, AllocationMode.DynamicRetainFreelist)
        {
        }

        /// <summary>
        /// Create a new collection using an array storage mechanism, based on a splay tree, using
        /// the default comparer. The allocation mode is DynamicRetainFreelist.
        /// </summary>
        [Storage(Storage.Array)]
        public SplayTreeArrayRangeMap()
            : this(0, AllocationMode.DynamicRetainFreelist)
        {
        }

        /// <summary>
        /// Create a new collection based on a splay tree that is an exact clone of the provided collection, including in
        /// allocation mode, content, structure, capacity and free list state, and comparer.
        /// </summary>
        /// <param name="original">the tree to copy</param>
        [Storage(Storage.Array)]
        public SplayTreeArrayRangeMap(SplayTreeArrayRangeMap<ValueType> original)
        {
            this.nodes = (Node[])original.nodes.Clone();
            this.root = original.root;
            this.freelist = original.freelist;
            this.allocationMode = original.allocationMode;
            this.count = original.count;
        }


        //
        // IOrderedMap, IOrderedList
        //

        
        /// <summary>
        /// Returns the number of ranges in the collection as an unsigned int.
        /// </summary>
        /// <exception cref="OverflowException">The collection contains more than UInt32.MaxValue ranges.</exception>
        public uint Count { get { return checked((uint)this.count); } }

        
        /// <summary>
        /// Returns the number of ranges in the collection.
        /// </summary>
        public long LongCount { get { return unchecked((long)this.count); } }

        
        /// <summary>
        /// Removes all ranges from the collection.
        /// </summary>
        public void Clear()
        {
            // no need to do any work for DynamicDiscard mode
            if (allocationMode != AllocationMode.DynamicDiscard)
            {
                // Since splay trees can be deep and thready, breadth-first is likely to use less memory than
                // depth-first. The worst-case is still N/2, so still risks being expensive.

                Queue<NodeRef> queue = new Queue<NodeRef>();
                if (root != Nil)
                {
                    queue.Enqueue(root);
                    while (queue.Count != 0)
                    {
                        NodeRef node = queue.Dequeue();
                        if (nodes[node].left != Nil)
                        {
                            queue.Enqueue(nodes[node].left);
                        }
                        if (nodes[node].right != Nil)
                        {
                            queue.Enqueue(nodes[node].right);
                        }

                        this.count = unchecked(this.count - 1);
                        Free(node);
                    }
                }

                Debug.Assert(this.count == 0);
            }

            root = Nil;
            this.count = 0;
            this.xExtent = 0;
        }


        //
        // IRange2Map, IRange2List, IRangeMap, IRangeList
        //

        // Count { get; } - reuses Feature.Dict implementation

        
        /// <summary>
        /// Determines if there is a range in the collection starting at the specified index.
        /// </summary>
        /// <param name="start">index to look for the start of a range at</param>
        /// <returns>true if there is a range starting at the specified index</returns>
        [Feature(Feature.Range, Feature.Range2)]
        public bool Contains([Widen] int start)
        {
            if (root != Nil)
            {
                Splay2(ref root, start);
                return start == Start(root);
            }
            return false;
        }

        
        /// <summary>
        /// Attempt to insert a range of a given length at the specified start index and with an associated value.
        /// If the range can't be inserted, the collection is left unchanged. In order to insert at the specified start
        /// index, there must be an existing range starting at that index (where the new range will be inserted immediately
        /// before the existing range at that start index), or the index must be equal to the extent of
        /// the collection (wherein the range will be added at the end of the sequence).
        /// </summary>
        /// <param name="start">starting index to attempt to insert the new range at</param>
        /// <param name="length">length of the new range. The length must be at least 1.</param>
        /// <param name="value">value to associate with the range</param>
        /// <returns>true if the range was successfully inserted</returns>
        /// <exception cref="OverflowException">the sum of lengths would have exceeded Int32.MaxValue</exception>
        [Feature(Feature.Range, Feature.Range2)]
        public bool TryInsert([Widen] int start,[Widen] int xLength,[Payload(Payload.Value)] ValueType value)
        {
            unchecked
            {
                if (start < 0)
                {
                    throw new ArgumentOutOfRangeException();
                }
                if (xLength <= 0)
                {
                    throw new ArgumentOutOfRangeException();
                }

                Splay2(ref root, start);
                if (start == Start(root))
                {
uint countNew = checked(this.count + 1);
                    /*[Widen]*/
                    int xExtentNew = checked(this.xExtent + xLength);

                    NodeRef i = Allocate();
                    nodes[i].value = value;
                    nodes[i].xOffset = nodes[root].xOffset;

                    nodes[i].left = nodes[root].left;
                    nodes[i].right = root;
                    if (root != Nil)
                    {
                        nodes[root].xOffset = xLength;
                        nodes[root].left = Nil;
                    }

                    root = i;

                    this.count = countNew;
                    this.xExtent = xExtentNew;

                    return true;
                }

                Splay2(ref nodes[root].right, 0);
                /*[Widen]*/
                int length = nodes[root].right != Nil
                    ? (nodes[nodes[root].right].xOffset)
                    : (this.xExtent - nodes[root].xOffset);
                if (start == Start(root) + length)
                {
                    // append

                    Debug.Assert(nodes[root].right == Nil);

                    /*[Widen]*/
                    int xLengthRoot = this.xExtent - nodes[root].xOffset;
uint countNew = checked(this.count + 1);
                    /*[Widen]*/
                    int xExtentNew = checked(this.xExtent + xLength);

                    NodeRef i = Allocate();
                    nodes[i].value = value;
                    nodes[i].xOffset = xLengthRoot;

                    nodes[i].left = Nil;
                    nodes[i].right = Nil;
                    Debug.Assert(root != Nil);
                    Debug.Assert(nodes[root].right == Nil);
                    nodes[root].right = i;

                    this.count = countNew;
                    this.xExtent = xExtentNew;

                    return true;
                }

                return false;
            }
        }

        
        /// <summary>
        /// Attempt to delete the range starting at the specified index.
        /// The index must refer to the start of a range; indexes to the interior of a range are not permitted.
        /// </summary>
        /// <param name="start">start of a range to attempt to delete</param>
        /// <returns>true if a range was successfully deleted</returns>
        [Feature(Feature.Range, Feature.Range2)]
        public bool TryDelete([Widen] int start)
        {
            unchecked
            {
                if (root != Nil)
                {
                    Splay2(ref root, start);
                    if (start == Start(root))
                    {
                        Splay2(ref nodes[root].right, 0);
                        Debug.Assert((nodes[root].right == Nil) || (nodes[nodes[root].right].left == Nil));
                        /*[Widen]*/
                        int xLength = nodes[root].right != Nil ? nodes[nodes[root].right].xOffset : this.xExtent - nodes[root].xOffset;

                        NodeRef dead, x;

                        dead = root;
                        if (nodes[root].left == Nil)
                        {
                            x = nodes[root].right;
                            if (x != Nil)
                            {
                                nodes[x].xOffset += nodes[root].xOffset - xLength;
                            }
                        }
                        else
                        {
                            x = nodes[root].left;
                            nodes[x].xOffset += nodes[root].xOffset;
                            Splay2(ref x, start);
                            Debug.Assert(nodes[x].right == Nil);
                            if (nodes[root].right != Nil)
                            {
                                nodes[nodes[root].right].xOffset += nodes[root].xOffset - nodes[x].xOffset - xLength;
                            }
                            nodes[x].right = nodes[root].right;
                        }
                        root = x;

                        this.count = unchecked(this.count - 1);
                        this.xExtent = unchecked(this.xExtent - xLength);
                        Free(dead);

                        return true;
                    }
                }
                return false;
            }
        }

        
        /// <summary>
        /// Attempt to query the length associated with the range starting at the specified start index.
        /// The index must refer to the start of a range; indexes to the interior of a range are not permitted.
        /// </summary>
        /// <param name="start">start of the range to query</param>
        /// <param name="length">out parameter receiving the length of the range</param>
        /// <returns>true if a range was found starting at the specified index</returns>
        [Feature(Feature.Range, Feature.Range2)]
        public bool TryGetLength([Widen] int start,[Widen] out int length)
        {
            unchecked
            {
                if (root != Nil)
                {
                    Splay2(ref root, start);
                    if (start == Start(root))
                    {
                        Splay2(ref nodes[root].right, 0);
                        if (nodes[root].right != Nil)
                        {
                            Debug.Assert(nodes[nodes[root].right].left == Nil);
                            length = nodes[nodes[root].right].xOffset;
                        }
                        else
                        {
                            length = this.xExtent - start;
                        }
                        return true;
                    }
                }
                length = 0;
                return false;
            }
        }

        
        /// <summary>
        /// Attempt to change the length associated with the range starting at the specified start index.
        /// The index must refer to the start of a range; indexes to the interior of a range are not permitted.
        /// </summary>
        /// <param name="start">start of the range to query</param>
        /// <param name="length">new length for the range. The length must be at least 1.</param>
        /// <returns>true if a range was found starting at the specified index and updated</returns>
        /// <exception cref="OverflowException">the sum of lengths would have exceeded Int32.MaxValue</exception>
        [Feature(Feature.Range, Feature.Range2)]
        public bool TrySetLength([Widen] int start,[Widen] int length)
        {
            if (length <= 0)
            {
                throw new ArgumentOutOfRangeException();
            }

            if (root != Nil)
            {
                Splay2(ref root, start);
                if (start == Start(root))
                {
                    /*[Widen]*/
                    int oldLength;
                    if (nodes[root].right != Nil)
                    {
                        Splay2(ref nodes[root].right, 0);
                        Debug.Assert(nodes[nodes[root].right].left == Nil);
                        oldLength = nodes[nodes[root].right].xOffset;
                    }
                    else
                    {
                        oldLength = unchecked(this.xExtent - nodes[root].xOffset);
                    }
                    /*[Widen]*/
                    int delta = length - oldLength;
                    {
                        this.xExtent = checked(this.xExtent + delta);

                        if (nodes[root].right != Nil)
                        {
                            unchecked
                            {
                                nodes[nodes[root].right].xOffset += delta;
                            }
                        }
                    }
                    return true;
                }
            }
            return false;
        }

        
        /// <summary>
        /// Attempt to query the value associated with the range starting at the specified start index.
        /// The index must refer to the start of a range; indexes to the interior of a range are not permitted.
        /// </summary>
        /// <param name="start">start of the range to query</param>
        /// <param name="value">value associated with the range</param>
        /// <returns>true if a range was found starting at the specified index</returns>
        [Payload(Payload.Value)]
        [Feature(Feature.Range, Feature.Range2)]
        public bool TryGetValue([Widen] int start,out ValueType value)
        {
            if (root != Nil)
            {
                Splay2(ref root, start);
                if (start == Start(root))
                {
                    value = nodes[root].value;
                    return true;
                }
            }
            value = default(ValueType);
            return false;
        }

        
        /// <summary>
        /// Attempt to update the value associated with the range starting at the specified start index.
        /// The index must refer to the start of a range; indexes to the interior of a range are not permitted.
        /// </summary>
        /// <param name="start">start of the range to query</param>
        /// <param name="value">new value that replaces the old value associated with the range</param>
        /// <returns>true if a range was found starting at the specified index</returns>
        [Payload(Payload.Value)]
        [Feature(Feature.Range, Feature.Range2)]
        public bool TrySetValue([Widen] int start,ValueType value)
        {
            if (root != Nil)
            {
                Splay2(ref root, start);
                if (start == Start(root))
                {
                    nodes[root].value = value;
                    return true;
                }
            }
            return false;
        }

        
        /// <summary>
        /// Attempt to get the value and length associated with the range starting at the specified start index.
        /// The index must refer to the start of a range; indexes to the interior of a range are not permitted.
        /// </summary>
        /// <param name="start">start of the range to query</param>
        /// <param name="length">out parameter receiving the length of the range</param>
        /// <param name="value">out parameter receiving the value associated with the range</param>
        /// <returns>true if a range was found starting at the specified index</returns>
        [Feature(Feature.Range, Feature.Range2)]
        public bool TryGet([Widen] int start,[Widen] out int xLength,[Payload(Payload.Value)] out ValueType value)
        {
            unchecked
            {
                if (root != Nil)
                {
                    Splay2(ref root, start);
                    if (start == Start(root))
                    {
                        Splay2(ref nodes[root].right, 0);
                        Debug.Assert((nodes[root].right == Nil) || (nodes[nodes[root].right].left == Nil));
                        if (nodes[root].right != Nil)
                        {
                            xLength = nodes[nodes[root].right].xOffset;
                        }
                        else
                        {
                            xLength = this.xExtent - nodes[root].xOffset;
                        }
                        value = nodes[root].value;
                        return true;
                    }
                }
                xLength = 0;
                value = default(ValueType);
                return false;
            }
        }

        
        /// <summary>
        /// Inserts a range of a given length at the specified start index and with an associated value.
        /// If the range can't be inserted, the collection is left unchanged. In order to insert at the specified start
        /// index, there must be an existing range starting at that index (where the new range will be inserted immediately
        /// before the existing range at that start index), or the index must be equal to the extent of
        /// the collection (wherein the range will be added at the end of the sequence).
        /// </summary>
        /// <param name="start">starting index to attempt to insert the new range at</param>
        /// <param name="length">length of the new range. The length must be at least 1.</param>
        /// <param name="value">value to associate with the range</param>
        /// <exception cref="ArgumentException">there is no range starting at the specified index</exception>
        /// <exception cref="OverflowException">the sum of lengths would have exceeded Int32.MaxValue</exception>
        [Feature(Feature.Range, Feature.Range2)]
        public void Insert([Widen] int start,[Widen] int xLength,[Payload(Payload.Value)] ValueType value)
        {
            if (!TryInsert(start, xLength, /*[Payload(Payload.Value)]*/value))
            {
                throw new ArgumentException("item already in tree");
            }
        }

        
        /// <summary>
        /// Attempt to delete the range starting at the specified index.
        /// The index must refer to the start of a range; indexes to the interior of a range are not permitted.
        /// </summary>
        /// <param name="start">start of a range to attempt to delete</param>
        /// <returns>true if a range was successfully deleted</returns>
        /// <exception cref="ArgumentException">there is no range starting at the specified index</exception>
        [Feature(Feature.Range, Feature.Range2)]
        public void Delete([Widen] int start)
        {
            if (!TryDelete(start))
            {
                throw new ArgumentException("item not in tree");
            }
        }

        
        /// <summary>
        /// Retrieves the length associated with the range starting at the specified start index.
        /// The index must refer to the start of a range; indexes to the interior of a range are not permitted.
        /// </summary>
        /// <param name="start">start of the range to query</param>
        /// <returns>the length of the range found at the specified start index</returns>
        /// <exception cref="ArgumentException">there is no range starting at the specified index</exception>
        [Feature(Feature.Range, Feature.Range2)]
        [Widen]
        public int GetLength([Widen] int start)
        {
            /*[Widen]*/
            int length;
            if (!TryGetLength(start, out length))
            {
                throw new ArgumentException("item not in tree");
            }
            return length;
        }

        
        /// <summary>
        /// Changes the length associated with the range starting at the specified start index.
        /// The index must refer to the start of a range; indexes to the interior of a range are not permitted.
        /// </summary>
        /// <param name="start">start of the range to query</param>
        /// <param name="length">new length for the range. The length must be at least 1.</param>
        /// <exception cref="ArgumentException">there is no range starting at the specified index</exception>
        /// <exception cref="OverflowException">the sum of lengths would have exceeded Int32.MaxValue</exception>
        [Feature(Feature.Range, Feature.Range2)]
        public void SetLength([Widen] int start,[Widen] int length)
        {
            if (!TrySetLength(start, length))
            {
                throw new ArgumentException("item not in tree");
            }
        }

        
        /// <summary>
        /// Retrieves the value associated with the range starting at the specified start index.
        /// The index must refer to the start of a range; indexes to the interior of a range are not permitted.
        /// </summary>
        /// <param name="start">start of the range to query</param>
        /// <returns>the value associated with the range</returns>
        /// <exception cref="ArgumentException">there is no range starting at the specified index</exception>
        [Payload(Payload.Value)]
        [Feature(Feature.Range, Feature.Range2)]
        public ValueType GetValue([Widen] int start)
        {
            ValueType value;
            if (!TryGetValue(start, out value))
            {
                throw new ArgumentException("item not in tree");
            }
            return value;
        }

        
        /// <summary>
        /// Updates the value associated with the range starting at the specified start index.
        /// The index must refer to the start of a range; indexes to the interior of a range are not permitted.
        /// </summary>
        /// <param name="start">start of the range to query</param>
        /// <param name="value">new value that replaces the old value associated with the range</param>
        /// <exception cref="ArgumentException">there is no range starting at the specified index</exception>
        [Payload(Payload.Value)]
        [Feature(Feature.Range, Feature.Range2)]
        public void SetValue([Widen] int start,ValueType value)
        {
            if (!TrySetValue(start, value))
            {
                throw new ArgumentException("item not in tree");
            }
        }

        
        /// <summary>
        /// Retrieves the value and length associated with the range starting at the specified start index.
        /// The index must refer to the start of a range; indexes to the interior of a range are not permitted.
        /// </summary>
        /// <param name="start">start of the range to query</param>
        /// <param name="length">out parameter receiving the length of the range</param>
        /// <param name="value">out parameter receiving the value associated with the range</param>
        /// <exception cref="ArgumentException">there is no range starting at the specified index</exception>
        [Feature(Feature.Range, Feature.Range2)]
        public void Get([Widen] int start,[Widen] out int xLength,[Payload(Payload.Value)] out ValueType value)
        {
            if (!TryGet(start, out xLength, /*[Payload(Payload.Value)]*/out value))
            {
                throw new ArgumentException("item not in tree");
            }
        }

        
        /// <summary>
        /// Retrieves the extent of the sequence of ranges. The extent is the sum of the lengths of all the ranges.
        /// </summary>
        /// <returns>the extent of the ranges</returns>
        [Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]
        [Widen]
        public int GetExtent()
        {
            return this.xExtent;
        }

        [Feature(Feature.Range, Feature.Range2)]
        private bool NearestLess([Widen] int position,[Widen] out int nearestStart,bool orEqual)
        {
            if (root != Nil)
            {
                Splay2(ref root, position);
                /*[Widen]*/
                int start = Start(root);
                if ((position < start) || (!orEqual && (position == start)))
                {
                    if (nodes[root].left != Nil)
                    {
                        Splay2(ref nodes[root].left, 0);
                        Debug.Assert(nodes[nodes[root].left].right == Nil);
                        nearestStart = start + (nodes[nodes[root].left].xOffset);
                        return true;
                    }
                    nearestStart = 0;
                    return false;
                }
                else
                {
                    nearestStart = Start(root);
                    return true;
                }
            }
            nearestStart = 0;
            return false;
        }

        
        /// <summary>
        /// Search for the nearest range that starts at an index less than or equal to the specified index.
        /// Use this method to convert indexes to the interior of a range into the start index of a range.
        /// </summary>
        /// <param name="position">the index to begin searching at</param>
        /// <param name="nearestStart">an out parameter receiving the start index of the range that was found.
        /// This may be a range starting at the specified index or the range containing the index if the index refers
        /// to the interior of a range.
        /// If the value is greater than or equal to the extent it will return the start of the last range of the collection.
        /// If there are no ranges in the collection or position is less than 0, no range will be found.
        /// </param>
        /// <returns>true if a range was found with a starting index less than or equal to the specified index</returns>
        [Feature(Feature.Range, Feature.Range2)]
        public bool NearestLessOrEqual([Widen] int position,[Widen] out int nearestStart)
        {
            return NearestLess(position, out nearestStart, true/*orEqual*/);
        }

        
        /// <summary>
        /// Search for the nearest range that starts at an index less than the specified index.
        /// </summary>
        /// <param name="position">the index to begin searching at</param>
        /// <param name="nearestStart">an out parameter receiving the start index of the range that was found.
        /// If the specified index is an interior index, the start of the containing range will be returned.
        /// If the index is at the start of a range, the start of the previous range will be returned.
        /// If the value is greater than or equal to the extent it will return the start of last range of the collection.
        /// If there are no ranges in the collection or position is less than or equal to 0, no range will be found.
        /// </param>
        /// <returns>true if a range was found with a starting index less than the specified index</returns>
        [Feature(Feature.Range, Feature.Range2)]
        public bool NearestLess([Widen] int position,[Widen] out int nearestStart)
        {
            return NearestLess(position, out nearestStart, false/*orEqual*/);
        }

        [Feature(Feature.Range, Feature.Range2)]
        private bool NearestGreater([Widen] int position,[Widen] out int nearestStart,bool orEqual)
        {
            if (root != Nil)
            {
                Splay2(ref root, position);
                /*[Widen]*/
                int start = Start(root);
                if ((position > start) || (!orEqual && (position == start)))
                {
                    if (nodes[root].right != Nil)
                    {
                        Splay2(ref nodes[root].right, 0);
                        Debug.Assert(nodes[nodes[root].right].left == Nil);
                        nearestStart = start + (nodes[nodes[root].right].xOffset);
                        return true;
                    }
                    nearestStart = this.xExtent;
                    return false;
                }
                else
                {
                    nearestStart = start;
                    return true;
                }
            }
            nearestStart = 0;
            return false;
        }

        
        /// <summary>
        /// Search for the nearest range that starts at an index greater than or equal to the specified index.
        /// </summary>
        /// <param name="position">the index to begin searching at</param>
        /// <param name="nearestStart">an out parameter receiving the start index of the range that was found.
        /// If the index refers to the start of a range, that index will be returned.
        /// If the index refers to the interior index for a range, the start of the next range in the sequence will be returned.
        /// If the index is less than or equal to 0, the index 0 will be returned, which is the start of the first range.
        /// If the index is greater than the start of the last range, no range will be found.
        /// </param>
        /// <returns>true if a range was found with a starting index greater than or equal to the specified index</returns>
        [Feature(Feature.Range, Feature.Range2)]
        public bool NearestGreaterOrEqual([Widen] int position,[Widen] out int nearestStart)
        {
            return NearestGreater(position, out nearestStart, true/*orEqual*/);
        }

        
        /// <summary>
        /// Search for the nearest range that starts at an index greater than the specified index.
        /// </summary>
        /// <param name="position">the index to begin searching at</param>
        /// <param name="nearestStart">an out parameter receiving the start index of the range that was found.
        /// If the index refers to the start of a range or is an interior index for a range, the next range in the
        /// sequence will be returned.
        /// If the index is less than 0, the index 0 will be returned, which is the start of the first range.
        /// If the index is greater than or equal to the start of the last range, no range will be found.
        /// </param>
        /// <returns>true if a range was found with a starting index greater than the specified index</returns>
        [Feature(Feature.Range, Feature.Range2)]
        public bool NearestGreater([Widen] int position,[Widen] out int nearestStart)
        {
            return NearestGreater(position, out nearestStart, false/*orEqual*/);
        }

        // Array allocation

        [Storage(Storage.Array)]
        private NodeRef Allocate()
        {
            if (freelist == Nil)
            {
                if (allocationMode == AllocationMode.PreallocatedFixed)
                {
                    const string Message = "Tree capacity exhausted but is locked";
                    throw new OutOfMemoryException(Message);
                }
                Debug.Assert(unchecked((uint)nodes.Length) >= ReservedElements);
                long oldCapacity = unchecked((uint)nodes.Length - ReservedElements);
                uint newCapacity = unchecked((uint)Math.Min(Math.Max(oldCapacity * 2L, 1L), UInt32.MaxValue - ReservedElements));
                EnsureFree(newCapacity);
                if (freelist == Nil)
                {
                    throw new OutOfMemoryException();
                }
            }
            NodeRef node = freelist;
            freelist = nodes[freelist].left;

            return node;
        }

        [Storage(Storage.Array)]
        private void Free(NodeRef node)
        {
            nodes[node].value = default(ValueType); // zero any contained references for garbage collector

            nodes[node].left = freelist;
            freelist = node;
        }

        [Storage(Storage.Array)]
        private void EnsureFree(uint capacity)
        {
            unchecked
            {
                Debug.Assert(freelist == Nil);
                Debug.Assert((nodes == null) || (nodes.Length >= ReservedElements));

                uint oldLength = nodes != null ? unchecked((uint)nodes.Length) : ReservedElements;
                uint newLength = checked(capacity + ReservedElements);

                Array.Resize(ref nodes, unchecked((int)newLength));

                // TODO:was attempt at reducing array bounds checks
                //Node[] nodesNew = nodes;
                //Node2[] nodes2New = nodes2;
                //Array.Resize(ref nodesNew, unchecked((int)newCount));
                //bool key = false, payload = false;
                ///*[Payload(Payload.Value)]*/
                //payload = true;
                ///*[Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]*/
                //key = true;
                //if (payload || key)
                //{
                //    Array.Resize(ref nodes2New, unchecked((int)newCount));
                //}
                //nodes = nodesNew;
                //nodes2 = nodes2New;

                for (long i = (long)newLength - 1; i >= oldLength; i--)
                {
                    Free(new NodeRef(unchecked((uint)i)));
                }
            }
        }

        [Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Widen]
        private int Start(NodeRef n)
        {
            return nodes[n].xOffset;
        }

        [Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]
        [EnableFixed]
        private void Splay2(ref NodeRef root,[Widen] int position)
        {
            unchecked
            {
                this.version = unchecked((ushort)(this.version + 1));

                if (root == Nil)
                {
                    return;
                }

                NodeRef t = root;

                nodes[N].left = Nil;
                nodes[N].right = Nil;

                NodeRef l = N;
                /*[Widen]*/
                int lxOffset = 0;
                NodeRef r = N;
                /*[Widen]*/
                int rxOffset = 0;

                while (true)
                {
                    int c;

                    c = position.CompareTo(nodes[t].xOffset);
                    if (c < 0)
                    {
                        if (nodes[t].left == Nil)
                        {
                            break;
                        }
                        c = position.CompareTo(nodes[t].xOffset + nodes[nodes[t].left].xOffset);
                        if (c < 0)
                        {
                            // rotate right
                            NodeRef u = nodes[t].left;
                            /*[Widen]*/
                            int uXPosition = nodes[t].xOffset + nodes[u].xOffset;
                            if (nodes[u].right != Nil)
                            {
                                nodes[nodes[u].right].xOffset += uXPosition - nodes[t].xOffset;
                            }
                            nodes[t].xOffset += -uXPosition;
                            nodes[u].xOffset = uXPosition;
                            nodes[t].left = nodes[u].right;
                            nodes[u].right = t;
                            t = u;
                            if (nodes[t].left == Nil)
                            {
                                break;
                            }
                        }
                        // link right
                        Debug.Assert(nodes[t].left != Nil);
                        nodes[nodes[t].left].xOffset += nodes[t].xOffset;
                        nodes[t].xOffset -= rxOffset;
                        nodes[r].left = t;
                        r = t;
                        rxOffset += nodes[r].xOffset;
                        t = nodes[t].left;
                    }
                    else if (c > 0)
                    {
                        if (nodes[t].right == Nil)
                        {
                            break;
                        }
                        c = position.CompareTo((nodes[t].xOffset + nodes[nodes[t].right].xOffset));
                        if (c > 0)
                        {
                            // rotate left
                            NodeRef u = nodes[t].right;
                            /*[Widen]*/
                            int uXPosition = nodes[t].xOffset + nodes[u].xOffset;
                            if (nodes[u].left != Nil)
                            {
                                nodes[nodes[u].left].xOffset += uXPosition - nodes[t].xOffset;
                            }
                            nodes[t].xOffset += -uXPosition;
                            nodes[u].xOffset = uXPosition;
                            nodes[t].right = nodes[u].left;
                            nodes[u].left = t;
                            t = u;
                            if (nodes[t].right == Nil)
                            {
                                break;
                            }
                        }
                        // link left
                        Debug.Assert(nodes[t].right != Nil);
                        nodes[nodes[t].right].xOffset += nodes[t].xOffset;
                        nodes[t].xOffset -= lxOffset;
                        nodes[l].right = t;
                        l = t;
                        lxOffset += nodes[l].xOffset;
                        t = nodes[t].right;
                    }
                    else
                    {
                        break;
                    }
                }
                // reassemble
                nodes[l].right = nodes[t].left;
                if (nodes[l].right != Nil)
                {
                    nodes[nodes[l].right].xOffset += nodes[t].xOffset - lxOffset;
                }
                nodes[r].left = nodes[t].right;
                if (nodes[r].left != Nil)
                {
                    nodes[nodes[r].left].xOffset += nodes[t].xOffset - rxOffset;
                }
                nodes[t].left = nodes[N].right;
                if (nodes[t].left != Nil)
                {
                    nodes[nodes[t].left].xOffset -= nodes[t].xOffset;
                }
                nodes[t].right = nodes[N].left;
                if (nodes[t].right != Nil)
                {
                    nodes[nodes[t].right].xOffset -= nodes[t].xOffset;
                }
                root = t;
            }
        }


        //
        // Non-invasive tree inspection support
        //

        // Helpers

        [Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]
        private void ValidateRanges()
        {
            if (root != Nil)
            {
                Stack<STuple<NodeRef, /*[Widen]*/int, /*[Widen]*/int, /*[Widen]*/int>> stack = new Stack<STuple<NodeRef, /*[Widen]*/int, /*[Widen]*/int, /*[Widen]*/int>>();

                /*[Widen]*/
                int offset = 0;
                /*[Widen]*/
                int leftEdge = 0;
                /*[Widen]*/
                int rightEdge = this.xExtent;

                NodeRef node = root;
                while (node != Nil)
                {
                    offset += nodes[node].xOffset;
                    stack.Push(new STuple<NodeRef, /*[Widen]*/int, /*[Widen]*/int, /*[Widen]*/int>(node, offset, leftEdge, rightEdge));
                    rightEdge = offset;
                    node = nodes[node].left;
                }
                while (stack.Count != 0)
                {
                    STuple<NodeRef, /*[Widen]*/int, /*[Widen]*/int, /*[Widen]*/int> t = stack.Pop();
                    node = t.Item1;
                    offset = t.Item2;
                    leftEdge = t.Item3;
                    rightEdge = t.Item4;

                    if ((offset < leftEdge) || (offset >= rightEdge))
                    {
                        throw new InvalidOperationException("range containment invariant");
                    }

                    leftEdge = offset + 1;
                    node = nodes[node].right;
                    while (node != Nil)
                    {
                        offset += nodes[node].xOffset;
                        stack.Push(new STuple<NodeRef, /*[Widen]*/int, /*[Widen]*/int, /*[Widen]*/int>(node, offset, leftEdge, rightEdge));
                        rightEdge = offset;
                        node = nodes[node].left;
                    }
                }
            }
        }

        // INonInvasiveTreeInspection

        object INonInvasiveTreeInspection.Root { get { return root != Nil ? (object)root : null; } }

        object INonInvasiveTreeInspection.GetLeftChild(object node)
        {
            NodeRef n = (NodeRef)node;
            return nodes[n].left != Nil ? (object)nodes[n].left : null;
        }

        object INonInvasiveTreeInspection.GetRightChild(object node)
        {
            NodeRef n = (NodeRef)node;
            return nodes[n].right != Nil ? (object)nodes[n].right : null;
        }

        object INonInvasiveTreeInspection.GetKey(object node)
        {
            object key = null;
            return key;
        }

        object INonInvasiveTreeInspection.GetValue(object node)
        {
            NodeRef n = (NodeRef)node;
            object value = null;
            value = nodes[n].value;
            return value;
        }

        object INonInvasiveTreeInspection.GetMetadata(object node)
        {
            return null;
        }

        void INonInvasiveTreeInspection.Validate()
        {
            if (root != Nil)
            {
                Dictionary<NodeRef, bool> visited = new Dictionary<NodeRef, bool>();
                Queue<NodeRef> worklist = new Queue<NodeRef>();
                worklist.Enqueue(root);
                while (worklist.Count != 0)
                {
                    NodeRef node = worklist.Dequeue();

                    if (visited.ContainsKey(node))
                    {
                        throw new InvalidOperationException("cycle");
                    }
                    visited.Add(node, false);

                    if (nodes[node].left != Nil)
                    {
                        worklist.Enqueue(nodes[node].left);
                    }
                    if (nodes[node].right != Nil)
                    {
                        worklist.Enqueue(nodes[node].right);
                    }
                }
            }

            /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*/
            ValidateRanges();
        }

        // INonInvasiveRange2MapInspection

        [Feature(Feature.Range, Feature.Range2)]
        [Widen]
        Range2MapEntry[] INonInvasiveRange2MapInspection.GetRanges()
        {
            /*[Widen]*/
            Range2MapEntry[] ranges = new Range2MapEntry[Count];
            int i = 0;

            if (root != Nil)
            {
                Stack<STuple<NodeRef, /*[Widen]*/int, /*[Widen]*/int>> stack = new Stack<STuple<NodeRef, /*[Widen]*/int, /*[Widen]*/int>>();

                /*[Widen]*/
                int xOffset = 0;
                /*[Widen]*/
                int yOffset = 0;

                NodeRef node = root;
                while (node != Nil)
                {
                    xOffset += nodes[node].xOffset;
                    stack.Push(new STuple<NodeRef, /*[Widen]*/int, /*[Widen]*/int>(node, xOffset, yOffset));
                    node = nodes[node].left;
                }
                while (stack.Count != 0)
                {
                    STuple<NodeRef, /*[Widen]*/int, /*[Widen]*/int> t = stack.Pop();
                    node = t.Item1;
                    xOffset = t.Item2;
                    yOffset = t.Item3;

                    object value = null;
                    value = nodes[node].value;

                    /*[Widen]*/
                    ranges[i++] = new Range2MapEntry(new Range(xOffset, 0), new Range(yOffset, 0), value);

                    node = nodes[node].right;
                    while (node != Nil)
                    {
                        xOffset += nodes[node].xOffset;
                        stack.Push(new STuple<NodeRef, /*[Widen]*/int, /*[Widen]*/int>(node, xOffset, yOffset));
                        node = nodes[node].left;
                    }
                }
                if (!(i == ranges.Length))
                {
                    Debug.Assert(false);
                    throw new InvalidOperationException();
                }

                for (i = 1; i < ranges.Length; i++)
                {
                    if (!(ranges[i - 1].x.start < ranges[i].x.start))
                    {
                        Debug.Assert(false);
                        throw new InvalidOperationException();
                    }
                    ranges[i - 1].x.length = ranges[i].x.start - ranges[i - 1].x.start;
                }

                ranges[i - 1].x.length = this.xExtent - ranges[i - 1].x.start;
            }

            return ranges;
        }

        [Feature(Feature.Range, Feature.Range2)]
        void INonInvasiveRange2MapInspection.Validate()
        {
            ((INonInvasiveTreeInspection)this).Validate();
        }


        //
        // Enumeration
        //

            /// <summary>
            /// Get the default enumerator, which is the robust enumerator for splay trees.
            /// </summary>
            /// <returns></returns>
        public IEnumerator<EntryRangeMap<ValueType>> GetEnumerator()
        {
            // For splay trees, the default enumerator is Robust because the Fast enumerator is fragile
            // and potentially consumes huge amounts of memory. For clients that can handle it, the Fast
            // enumerator is available by explicitly calling GetFastEnumerable().
            return GetRobustEnumerable().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        /// <summary>
        /// Get the robust enumerator. The robust enumerator uses an internal key cursor and queries the tree using the NextGreater()
        /// method to advance the enumerator. This enumerator is robust because it tolerates changes to the underlying tree. If a key
        /// is inserted or removed and it comes before the enumerator�s current key in sorting order, it will have no affect on the
        /// enumerator. If a key is inserted or removed and it comes after the enumerator�s current key (i.e. in the portion of the
        /// collection the enumerator hasn�t visited yet), the enumerator will include the key if inserted or skip the key if removed.
        /// Because the enumerator queries the tree for each element it�s running time per element is O(lg N), or O(N lg N) to
        /// enumerate the entire tree.
        /// </summary>
        /// <returns>An IEnumerable which can be used in a foreach statement</returns>
        public IEnumerable<EntryRangeMap<ValueType>> GetRobustEnumerable()
        {
            return new RobustEnumerableSurrogate(this);
        }

        public struct RobustEnumerableSurrogate : IEnumerable<EntryRangeMap<ValueType>>
        {
            private readonly SplayTreeArrayRangeMap<ValueType> tree;

            public RobustEnumerableSurrogate(SplayTreeArrayRangeMap<ValueType> tree)
            {
                this.tree = tree;
            }

            public IEnumerator<EntryRangeMap<ValueType>> GetEnumerator()
            {
                return new RobustEnumerator(tree);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return this.GetEnumerator();
            }
        }

        /// <summary>
        /// Get the fast enumerator. The fast enumerator uses an internal stack of nodes to peform in-order traversal of the
        /// tree structure. Because it uses the tree structure, it is invalidated if the tree is modified by an insertion or
        /// deletion and will throw an InvalidOperationException when next advanced. For the Splay tree, all operations modify
        /// the tree structure, include queries, and will invalidate the enumerator. The complexity of the fast enumerator
        /// is O(1) per element, or O(N) to enumerate the entire tree.
        /// 
        /// A note about splay trees and enumeration: Enumeration of splay trees is generally problematic, for two reasons.
        /// First, every operation on a splay tree modifies the structure of the tree, including queries. Second, splay trees
        /// may have depth of N in the worst case (as compared to other trees which are guaranteed to be less deep than
        /// approximately two times the optimal depth, or 2 lg N). The first property makes fast enumeration less useful, and
        /// the second property means fast enumeration may consume up to memory proportional to N for the internal stack used
        /// for traversal. Therefore, the robust enumerator is recommended for splay trees. The drawback is the
        /// robust enumerator�s O(N lg N) complexity.
        /// </summary>
        /// <returns>An IEnumerable which can be used in a foreach statement</returns>
        public IEnumerable<EntryRangeMap<ValueType>> GetFastEnumerable()
        {
            return new FastEnumerableSurrogate(this);
        }

        public struct FastEnumerableSurrogate : IEnumerable<EntryRangeMap<ValueType>>
        {
            private readonly SplayTreeArrayRangeMap<ValueType> tree;

            public FastEnumerableSurrogate(SplayTreeArrayRangeMap<ValueType> tree)
            {
                this.tree = tree;
            }

            public IEnumerator<EntryRangeMap<ValueType>> GetEnumerator()
            {
                return new FastEnumerator(tree);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return this.GetEnumerator();
            }
        }

        /// <summary>
        /// This enumerator is robust in that it can continue to walk the tree even in the face of changes, because
        /// it keeps a current key and uses NearestGreater to find the next one. The enumerator also uses a constant
        /// amount of memory. However, since it uses queries it is slow, O(n lg(n)) to enumerate the entire tree.
        /// </summary>
        public class RobustEnumerator : IEnumerator<EntryRangeMap<ValueType>>
        {
            private readonly SplayTreeArrayRangeMap<ValueType> tree;
            private bool started;
            private bool valid;
            [Feature(Feature.Range, Feature.Range2)]
            [Widen]
            private int currentXStart;
            [Feature(Feature.Range, Feature.Range2)]
            private ushort version; // saving the currentXStart does not work well range collections

            public RobustEnumerator(SplayTreeArrayRangeMap<ValueType> tree)
            {
                this.tree = tree;
                Reset();
            }

            public EntryRangeMap<ValueType> Current
            {
                get
                {
                    /*[Feature(Feature.Range, Feature.Range2)]*/
                    if (version != tree.version)
                    {
                        throw new InvalidOperationException();
                    }

                    if (valid)
                    {

                        // OR

                        /*[Feature(Feature.Range, Feature.Range2)]*/
                        {
                            ValueType value = default(ValueType);
                            /*[Widen]*/
                            int xStart = 0, xLength = 0;
                            xStart = currentXStart;

                            tree.Get(
                                /*[Feature(Feature.Range, Feature.Range2)]*/xStart,
                                /*[Feature(Feature.Range, Feature.Range2)]*/out xLength,
                                /*[Payload(Payload.Value)]*/out value);

                            /*[Feature(Feature.Range, Feature.Range2)]*/
                            version = tree.version; // our query is ok

                            return new EntryRangeMap<ValueType>(
                                /*[Payload(Payload.Value)]*/value,
                                /*[Feature(Feature.Range, Feature.Range2)]*/xStart,
                                /*[Feature(Feature.Range, Feature.Range2)]*/xLength);
                        }
                    }
                    return new EntryRangeMap<ValueType>();
                }
            }

            object IEnumerator.Current
            {
                get
                {
                    return this.Current;
                }
            }

            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                /*[Feature(Feature.Range, Feature.Range2)]*/
                if (version != tree.version)
                {
                    throw new InvalidOperationException();
                }

                if (!started)
                {

                    // OR

                    /*[Feature(Feature.Range, Feature.Range2)]*/
                    valid = tree.xExtent != 0;
                    /*[Feature(Feature.Range, Feature.Range2)]*/
                    Debug.Assert(currentXStart == 0);

                    started = true;
                }
                else if (valid)
                {

                    // OR

                    /*[Feature(Feature.Range, Feature.Range2)]*/
                    valid = tree.NearestGreater(currentXStart, out currentXStart);
                }

                /*[Feature(Feature.Range, Feature.Range2)]*/
                version = tree.version; // our query is ok

                return valid;
            }

            public void Reset()
            {
                started = false;
                valid = false;
                currentXStart = 0;
                /*[Feature(Feature.Range, Feature.Range2)]*/
                version = tree.version;
            }
        }

        /// <summary>
        /// This enumerator is fast because it uses an in-order traversal of the tree that has O(1) cost per element.
        /// However, any change to the tree invalidates it, and that *includes queries* since a query causes a splay
        /// operation that changes the structure of the tree.
        /// Worse, this enumerator also uses a stack that can be as deep as the tree, and since the depth of a splay
        /// tree is in the worst case n (number of nodes), the stack can potentially be size n.
        /// </summary>
        public class FastEnumerator : IEnumerator<EntryRangeMap<ValueType>>
        {
            private readonly SplayTreeArrayRangeMap<ValueType> tree;
            private ushort version;
            private NodeRef currentNode;
            private NodeRef nextNode;
            [Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]
            [Widen]
            private int currentXStart, nextXStart;

            private readonly Stack<STuple<NodeRef, /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*//*[Widen]*/int>> stack
                = new Stack<STuple<NodeRef, /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*//*[Widen]*/int>>();

            public FastEnumerator(SplayTreeArrayRangeMap<ValueType> tree)
            {
                this.tree = tree;
                Reset();
            }

            public EntryRangeMap<ValueType> Current
            {
                get
                {
                    if (currentNode != tree.Nil)
                    {

                        return new EntryRangeMap<ValueType>(
                            /*[Payload(Payload.Value)]*/tree.nodes[currentNode].value,
                            /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*/currentXStart,
                            /*[Feature(Feature.RankMulti, Feature.Range, Feature.Range2)]*/nextXStart - currentXStart);
                    }
                    return new EntryRangeMap<ValueType>();
                }
            }

            object IEnumerator.Current
            {
                get
                {
                    return this.Current;
                }
            }

            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                Advance();
                return currentNode != tree.Nil;
            }

            public void Reset()
            {
                stack.Clear();
                currentNode = tree.Nil;
                nextNode = tree.Nil;
                currentXStart = 0;
                nextXStart = 0;

                this.version = tree.version;

                PushSuccessor(
                    tree.root,
                    /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*/0);

                Advance();
            }

            private void PushSuccessor(
                NodeRef node,                [Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)][Widen] int xPosition)
            {
                while (node != tree.Nil)
                {
                    xPosition += tree.nodes[node].xOffset;

                    stack.Push(new STuple<NodeRef, /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*//*[Widen]*/int>(
                        node,
                        /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*/xPosition));
                    node = tree.nodes[node].left;
                }
            }

            private void Advance()
            {
                if (this.version != tree.version)
                {
                    throw new InvalidOperationException();
                }

                currentNode = nextNode;
                currentXStart = nextXStart;

                nextNode = tree.Nil;
                nextXStart = tree.xExtent;

                if (stack.Count == 0)
                {
                    nextXStart = tree.xExtent;
                    return;
                }

                STuple<NodeRef, /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*//*[Widen]*/int> cursor
                    = stack.Pop();

                nextNode = cursor.Item1;
                nextXStart = cursor.Item2;

                PushSuccessor(
                    tree.nodes[nextNode].right,
                    /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*/nextXStart);
            }
        }
    }
}
