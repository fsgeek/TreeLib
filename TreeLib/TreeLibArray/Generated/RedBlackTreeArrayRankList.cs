// NOTE: This file is auto-generated. DO NOT MAKE CHANGES HERE! They will be overwritten on rebuild.

// IMPORTANT: The TreeLib package is licensed under GNU Lesser GPL. However, this file is based on
// code that was licensed under the MIT license. Therefore, at your option, you may apply
// the MIT license to THIS FILE and it's automatically-generated derivatives only.

// adapted from .NET CoreFX BCL: https://github.com/dotnet/corefx/blob/master/src/System.Collections/src/System/Collections/Generic/SortedSet.cs

/*
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
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using TreeLib.Internal;

#pragma warning disable CS1572 // silence warning: XML comment has a param tag for '...', but there is no parameter by that name
#pragma warning disable CS1573 // silence warning: Parameter '...' has no matching param tag in the XML comment
#pragma warning disable CS1587 // silence warning: XML comment is not placed on a valid language element
#pragma warning disable CS1591 // silence warning: Missing XML comment for publicly visible type or member

//
// Based on the .NET Framework Base Class Libarary implementation of red-black trees from here:
// https://github.com/dotnet/corefx/blob/master/src/System.Collections/src/System/Collections/Generic/SortedSet.cs
//
// An overview of red-black trees can be found here: https://en.wikipedia.org/wiki/Red%E2%80%93black_tree
//

namespace TreeLib
{

    /// <summary>
    /// Implements a map, list or range collection using a red-black tree. 
    /// </summary>
    
    /// <summary>
    /// Represents a ordered key collection, augmented with rank information. The rank of a key is the index it would
    /// be located in if all the keys in the tree were placed into a sorted array.
    /// </summary>
    /// <typeparam name="KeyType">Type of key used to index collection. Must be comparable.</typeparam>
    public class RedBlackTreeArrayRankList<[Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)] KeyType> :
        /*[Feature(Feature.Rank)]*//*[Payload(Payload.None)]*//*[Widen]*/IRankList<KeyType>,

        INonInvasiveTreeInspection,
        /*[Feature(Feature.Rank, Feature.RankMulti)]*//*[Widen]*/INonInvasiveMultiRankMapInspection,

        IEnumerable<EntryRankList<KeyType>>,
        IEnumerable,

        ICloneable

        where KeyType : IComparable<KeyType>
    {

        //
        // Array form data structure
        //

        [Storage(Storage.Array)]
        [StructLayout(LayoutKind.Auto)] // defaults to LayoutKind.Sequential; use .Auto to allow framework to pack key & value optimally
        private struct Node
        {
            public NodeRef left;
            public NodeRef right;

            [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]
            public KeyType key;

            public bool isRed;

            [Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]
            [Widen]
            public int xOffset;

            //public override string ToString()
            //{
            //    return String.Format("({0})*{2}={3}*({1})", left == Null ? "null" : left.ToString(), right == Null ? "null" : right.ToString(), key, value);
            //}
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
                return node != _Null ? node.ToString() : "null";
            }
        }

        [Storage(Storage.Array)]
        private readonly static NodeRef _Null = new NodeRef(unchecked((uint)-1));

        [Storage(Storage.Array)]
        private const int ReservedElements = 0;
        [Storage(Storage.Array)]
        private Node[] nodes;

        //
        // State for both array & object form
        //

        private NodeRef Null { get { return RedBlackTreeArrayRankList<KeyType>._Null; } } // allow tree.Null or this.Null in all cases

        private NodeRef root;
        [Count]
        private uint count;
        private ushort version;

        [Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]
        [Widen]
        private int xExtent;

        [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]
        private readonly IComparer<KeyType> comparer;

        private readonly AllocationMode allocationMode;
        private NodeRef freelist;

        // Array

        /// <summary>
        /// Create a new collection using an array storage mechanism, based on a red-black tree, explicitly configured.
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
        public RedBlackTreeArrayRankList([Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)] IComparer<KeyType> comparer,uint capacity,AllocationMode allocationMode)
        {
            if (allocationMode == AllocationMode.DynamicDiscard)
            {
                throw new ArgumentException();
            }

            this.comparer = comparer;
            this.root = Null;

            this.allocationMode = allocationMode;
            this.freelist = Null;
            EnsureFree(capacity);
        }

        /// <summary>
        /// Create a new collection using an array storage mechanism, based on a red-black tree, with the specified capacity and allocation mode and using
        /// the default comparer (applicable only for keyed collections). The allocation mode is PreallocatedFixed.
        /// </summary>
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
        [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]
        public RedBlackTreeArrayRankList(uint capacity,AllocationMode allocationMode)
            : this(/*[Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]*/Comparer<KeyType>.Default, capacity, allocationMode)
        {
        }

        /// <summary>
        /// Create a new collection using an array storage mechanism, based on a red-black, with the specified capacity and using
        /// the default comparer (applicable only for keyed collections). The allocation mode is DynamicRetainFreelist.
        /// </summary>
        /// <param name="capacity">
        /// The initial capacity of the tree, the memory for which is preallocated at construction time;
        /// if the capacity is exceeded, the internal array will be resized to make more nodes available.
        /// </param>
        [Storage(Storage.Array)]
        public RedBlackTreeArrayRankList(uint capacity)
            : this(/*[Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]*/Comparer<KeyType>.Default, capacity, AllocationMode.DynamicRetainFreelist)
        {
        }

        /// <summary>
        /// Create a new collection using an array storage mechanism, based on a red-black tree, using
        /// the specified comparer. The allocation mode is DynamicRetainFreelist.
        /// </summary>
        /// <param name="comparer">The comparer to use for sorting keys</param>
        [Storage(Storage.Array)]
        [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]
        public RedBlackTreeArrayRankList(IComparer<KeyType> comparer)
            : this(comparer, 0, AllocationMode.DynamicRetainFreelist)
        {
        }

        /// <summary>
        /// Create a new collection using an array storage mechanism, based on a red-black tree, using
        /// the default comparer. The allocation mode is DynamicRetainFreelist.
        /// </summary>
        [Storage(Storage.Array)]
        public RedBlackTreeArrayRankList()
            : this(/*[Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]*/Comparer<KeyType>.Default, 0, AllocationMode.DynamicRetainFreelist)
        {
        }

        /// <summary>
        /// Create a new collection based on a red-black tree that is an exact clone of the provided collection, including in
        /// allocation mode, content, structure, capacity and free list state, and comparer.
        /// </summary>
        /// <param name="original">the tree to copy</param>
        [Storage(Storage.Array)]
        public RedBlackTreeArrayRankList(RedBlackTreeArrayRankList<KeyType> original)
        {
            this.comparer = original.comparer;

            this.nodes = (Node[])original.nodes.Clone();
            this.root = original.root;

            this.freelist = original.freelist;
            this.allocationMode = original.allocationMode;

            this.count = original.count;
            this.xExtent = original.xExtent;
        }


        //
        // IOrderedMap, IOrderedList
        //

        
        /// <summary>
        /// Returns the number of keys in the collection as an unsigned int.
        /// </summary>
        /// <exception cref="OverflowException">The collection contains more than UInt32.MaxValue keys.</exception>
        public uint Count { get { return checked((uint)this.count); } }

        
        /// <summary>
        /// Returns the number of keys in the collection.
        /// </summary>
        public long LongCount { get { return unchecked((long)this.count); } }

        
        /// <summary>
        /// Removes all keys from the collection.
        /// </summary>
        public void Clear()
        {
            // no need to do any work for DynamicDiscard mode
            if (allocationMode != AllocationMode.DynamicDiscard)
            {
                // non-recusrive depth-first traversal (in-order, but doesn't matter here)

                Stack<NodeRef> stack = new Stack<NodeRef>();

                NodeRef node = root;
                while (node != Null)
                {
                    stack.Push(node);
                    node = nodes[node].left;
                }
                while (stack.Count != 0)
                {
                    node = stack.Pop();

                    NodeRef dead = node;

                    node = nodes[node].right;
                    while (node != Null)
                    {
                        stack.Push(node);
                        node = nodes[node].left;
                    }

                    this.count = unchecked(this.count - 1);
                    Free(dead);
                }

                Debug.Assert(this.count == 0);
            }

            root = Null;
            this.count = 0;
            this.xExtent = 0;
        }

        
        /// <summary>
        /// Determines whether the key is present in the collection.
        /// </summary>
        /// <param name="key">Key to search for</param>
        /// <returns>true if the key is present in the collection</returns>
        [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]
        public bool ContainsKey(KeyType key)
        {
            return FindNode(key) != Null;
        }

        
        /// <summary>
        /// Attempts to remove a key from the collection. If the key is not present, no change is made to the collection.
        /// </summary>
        /// <param name="key">the key to search for and possibly remove</param>
        /// <returns>true if the key was found and removed</returns>
        [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]
        public bool TryRemove(KeyType key)
        {
            return DeleteInternal(
                /*[Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]*/key,
                /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*/0);
        }

        
        /// <summary>
        /// Attempts to get the key stored in the collection that matches the provided key.
        /// (This would be used if the KeyType is a compound type, with one portion being used as the comparable key and the
        /// remainder being a payload that does not participate in the comparison.)
        /// </summary>
        /// <param name="key">key to search for</param>
        /// <param name="keyOut">the actual key contained in the collection</param>
        /// <returns>true if they key was found</returns>
        [Payload(Payload.None)]
        [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]
        public bool TryGetKey(KeyType key,out KeyType keyOut)
        {
            NodeRef node = FindNode(key);
            if (node != Null)
            {
                keyOut = nodes[node].key;
                return true;
            }
            keyOut = default(KeyType);
            return false;
        }

        
        /// <summary>
        /// Attempts to update the key data for a key in the collection. If the key is not present, no change is made to the collection.
        /// (This would be used if the KeyType is a compound type, with one portion being used as the comparable key and the
        /// remainder being a payload that does not participate in the comparison.)
        /// </summary>
        /// <param name="key">key to search for and possibly replace the existing key</param>
        /// <returns>true if the key was found and updated</returns>
        [Payload(Payload.None)]
        [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]
        public bool TrySetKey(KeyType key)
        {
            return InsertUpdateInternal(
                /*[Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]*/key,
                /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*/0,
                /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*/0,
                false/*add*/,
                true/*update*/);
        }

        
        /// <summary>
        /// Removes a key from the collection.
        /// </summary>
        /// <param name="key">key to remove</param>
        /// <exception cref="ArgumentException">the key is not present in the collection</exception>
        [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]
        public void Remove(KeyType key)
        {
            if (!DeleteInternal(
                /*[Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]*/key,
                /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*/0))
            {
                throw new ArgumentException("item not in tree");
            }
        }

        
        /// <summary>
        /// Retrieves the key stored in the collection that matches the provided key.
        /// (This would be used if the KeyType is a compound type, with one portion being used as the comparable key and the
        /// remainder being a payload that does not participate in the comparison.)
        /// </summary>
        /// <param name="key">key to search for</param>
        /// <returns>the value associated with the key</returns>
        /// <exception cref="ArgumentException">the key is not present in the collection</exception>
        [Payload(Payload.None)]
        [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]
        public KeyType GetKey(KeyType key)
        {
            KeyType keyOut;
            if (!TryGetKey(key, out keyOut))
            {
                throw new ArgumentException("item not in tree");
            }
            return keyOut;
        }

        
        /// <summary>
        /// Updates the key data for a key in the collection. If the key is not present, no change is made to the collection.
        /// (This would be used if the KeyType is a compound type, with one portion being used as the comparable key and the
        /// remainder being a payload that does not participate in the comparison.)
        /// </summary>
        /// <param name="key">key to search for and possibly replace the existing key</param>
        /// <returns>true if the key was found and updated</returns>
        [Payload(Payload.None)]
        [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]
        public void SetKey(KeyType key)
        {
            if (!TrySetKey(key))
            {
                throw new ArgumentException("item not in tree");
            }
        }

        [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]
        private bool LeastInternal(out KeyType keyOut)
        {
            NodeRef node = root;
            if (node == Null)
            {
                keyOut = default(KeyType);
                return false;
            }
            while (nodes[node].left != Null)
            {
                node = nodes[node].left;
            }
            keyOut = nodes[node].key;
            return true;
        }

        
        /// <summary>
        /// Retrieves the lowest key in the collection (in sort order)
        /// </summary>
        /// <param name="leastOut">out parameter receiving the key</param>
        /// <returns>true if a key was found (i.e. collection contains at least 1 key)</returns>
        [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]
        public bool Least(out KeyType keyOut)
        {
            return LeastInternal(out keyOut);
        }

        [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]
        private bool GreatestInternal(out KeyType keyOut)
        {
            NodeRef node = root;
            if (node == Null)
            {
                keyOut = default(KeyType);
                return false;
            }
            while (nodes[node].right != Null)
            {
                node = nodes[node].right;
            }
            keyOut = nodes[node].key;
            return true;
        }

        
        /// <summary>
        /// Retrieves the highest key in the collection (in sort order)
        /// </summary>
        /// <param name="greatestOut">out parameter receiving the key</param>
        /// <returns>true if a key was found (i.e. collection contains at least 1 key)</returns>
        [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]
        public bool Greatest(out KeyType keyOut)
        {
            return GreatestInternal(out keyOut);
        }

        
        /// <summary>
        /// Retrieves the highest key in the collection that is less than or equal to the provided key.
        /// </summary>
        /// <param name="key">key to search below</param>
        /// <param name="nearestKey">highest key less than or equal to provided key</param>
        /// <returns>true if there was a key less than or equal to the provided key</returns>
        [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]
        public bool NearestLessOrEqual(KeyType key,out KeyType nearestKey)
        {
            /*[Widen]*/
            int nearestStart;
            NodeRef nearestNode;
            return NearestLess(
                out nearestNode,
                /*[Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]*/key,
                /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*/0,
                /*[Feature(Feature.Rank, Feature.RankMulti)]*/CompareKeyMode.Key,
                /*[Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]*/out nearestKey,
                /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*/out nearestStart,
                true/*orEqual*/);
        }

        
        /// <summary>
        /// Retrieves the highest key in the collection that is less than or equal to the provided key.
        /// </summary>
        /// <param name="key">key to search below</param>
        /// <param name="nearestKey">highest key less than or equal to provided key</param>
        /// <param name="rank">the rank of the returned key</param>
        /// <returns>true if there was a key less than or equal to the provided key</returns>
        [Feature(Feature.Rank, Feature.RankMulti)]
        public bool NearestLessOrEqual(KeyType key,out KeyType nearestKey,[Feature(Feature.Rank, Feature.RankMulti)][Widen] out int rank)
        {
            rank = 0;
            /*[Widen]*/
            int nearestStart;
            NodeRef nearestNode;
            bool f = NearestLess(
                out nearestNode,
                /*[Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]*/key,
                /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*/0,
                /*[Feature(Feature.Rank, Feature.RankMulti)]*/CompareKeyMode.Key,
                /*[Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]*/out nearestKey,
                /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*/out nearestStart,
                true/*orEqual*/);
            if (f)
            {
                /*[Payload(Payload.None)]*/
                KeyType duplicateKey;
                bool g = TryGet(nearestKey, /*[Payload(Payload.None)]*/out duplicateKey, out rank);
                Debug.Assert(g);
                Debug.Assert(0 == comparer.Compare(nearestKey, duplicateKey));
            }
            return f;
        }

        
        /// <summary>
        /// Retrieves the highest key in the collection that is less than the provided key.
        /// </summary>
        /// <param name="key">key to search below</param>
        /// <param name="nearestKey">highest key less than the provided key</param>
        /// <returns>true if there was a key less than the provided key</returns>
        [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]
        public bool NearestLess(KeyType key,out KeyType nearestKey)
        {
            /*[Widen]*/
            int nearestStart;
            NodeRef nearestNode;
            return NearestLess(
                out nearestNode,
                /*[Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]*/key,
                /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*/0,
                /*[Feature(Feature.Rank, Feature.RankMulti)]*/CompareKeyMode.Key,
                /*[Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]*/out nearestKey,
                /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*/out nearestStart,
                false/*orEqual*/);
        }

        
        /// <summary>
        /// Retrieves the highest key in the collection that is less than the provided key.
        /// </summary>
        /// <param name="key">key to search below</param>
        /// <param name="nearestKey">highest key less than the provided key</param>
        /// <param name="rank">the rank of the returned key</param>
        /// <returns>true if there was a key less than the provided key</returns>
        [Feature(Feature.Rank, Feature.RankMulti)]
        public bool NearestLess(KeyType key,out KeyType nearestKey,[Feature(Feature.Rank, Feature.RankMulti)][Widen] out int rank)
        {
            rank = 0;
            /*[Widen]*/
            int nearestStart;
            NodeRef nearestNode;
            bool f = NearestLess(
                out nearestNode,
                /*[Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]*/key,
                /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*/0,
                /*[Feature(Feature.Rank, Feature.RankMulti)]*/CompareKeyMode.Key,
                /*[Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]*/out nearestKey,
                /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*/out nearestStart,
                false/*orEqual*/);
            if (f)
            {
                /*[Payload(Payload.None)]*/
                KeyType duplicateKey;
                bool g = TryGet(nearestKey, /*[Payload(Payload.None)]*/out duplicateKey, out rank);
                Debug.Assert(g);
                Debug.Assert(0 == comparer.Compare(nearestKey, duplicateKey));
            }
            return f;
        }

        
        /// <summary>
        /// Retrieves the lowest key in the collection that is greater than or equal to the provided key.
        /// </summary>
        /// <param name="key">key to search above</param>
        /// <param name="nearestKey">lowest key greater than or equal to provided key</param>
        /// <returns>true if there was a key greater than or equal to the provided key</returns>
        [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]
        public bool NearestGreaterOrEqual(KeyType key,out KeyType nearestKey)
        {
            /*[Widen]*/
            int nearestStart;
            NodeRef nearestNode;
            return NearestGreater(
                out nearestNode,
                /*[Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]*/key,
                /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*/0,
                /*[Feature(Feature.Rank, Feature.RankMulti)]*/CompareKeyMode.Key,
                /*[Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]*/out nearestKey,
                /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*/out nearestStart,
                true/*orEqual*/);
        }

        
        /// <summary>
        /// Retrieves the lowest key in the collection that is greater than or equal to the provided key.
        /// </summary>
        /// <param name="key">key to search above</param>
        /// <param name="nearestKey">lowest key greater than or equal to provided key</param>
        /// <param name="rank">the rank of the returned key</param>
        /// <returns>true if there was a key greater than or equal to the provided key</returns>
        [Feature(Feature.Rank, Feature.RankMulti)]
        public bool NearestGreaterOrEqual(KeyType key,out KeyType nearestKey,[Feature(Feature.Rank, Feature.RankMulti)][Widen] out int rank)
        {
            rank = this.xExtent;
            /*[Widen]*/
            int nearestStart;
            NodeRef nearestNode;
            bool f = NearestGreater(
                out nearestNode,
                /*[Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]*/key,
                /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*/0,
                /*[Feature(Feature.Rank, Feature.RankMulti)]*/CompareKeyMode.Key,
                /*[Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]*/out nearestKey,
                /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*/out nearestStart,
                true/*orEqual*/);
            if (f)
            {
                /*[Payload(Payload.None)]*/
                KeyType duplicateKey;
                bool g = TryGet(nearestKey, /*[Payload(Payload.None)]*/out duplicateKey, out rank);
                Debug.Assert(g);
                Debug.Assert(0 == comparer.Compare(nearestKey, duplicateKey));
            }
            return f;
        }

        
        /// <summary>
        /// Retrieves the lowest key in the collection that is greater than the provided key.
        /// </summary>
        /// <param name="key">key to search above</param>
        /// <param name="nearestKey">lowest key greater than the provided key</param>
        /// <returns>true if there was a key greater than the provided key</returns>
        [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]
        public bool NearestGreater(KeyType key,out KeyType nearestKey)
        {
            /*[Widen]*/
            int nearestStart;
            NodeRef nearestNode;
            return NearestGreater(
                out nearestNode,
                /*[Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]*/key,
                /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*/0,
                /*[Feature(Feature.Rank, Feature.RankMulti)]*/CompareKeyMode.Key,
                /*[Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]*/out nearestKey,
                /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*/out nearestStart,
                false/*orEqual*/);
        }

        
        /// <summary>
        /// Retrieves the lowest key in the collection that is greater than the provided key.
        /// </summary>
        /// <param name="key">key to search above</param>
        /// <param name="nearestKey">lowest key greater than the provided key</param>
        /// <param name="rank">the rank of the returned key</param>
        /// <returns>true if there was a key greater than the provided key</returns>
        [Feature(Feature.Rank, Feature.RankMulti)]
        public bool NearestGreater(KeyType key,out KeyType nearestKey,[Feature(Feature.Rank, Feature.RankMulti)][Widen] out int rank)
        {
            rank = this.xExtent;
            /*[Widen]*/
            int nearestStart;
            NodeRef nearestNode;
            bool f = NearestGreater(
                out nearestNode,
                /*[Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]*/key,
                /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*/0,
                /*[Feature(Feature.Rank, Feature.RankMulti)]*/CompareKeyMode.Key,
                /*[Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]*/out nearestKey,
                /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*/out nearestStart,
                false/*orEqual*/);
            if (f)
            {
                /*[Payload(Payload.None)]*/
                KeyType duplicateKey;
                bool g = TryGet(nearestKey, /*[Payload(Payload.None)]*/out duplicateKey, out rank);
                Debug.Assert(g && (0 == comparer.Compare(nearestKey, duplicateKey)));
                Debug.Assert(g);
                Debug.Assert(0 == comparer.Compare(nearestKey, duplicateKey));
            }
            return f;
        }


        //
        // IRankMap, IMultiRankMap, IRankList, IMultiRankList
        //

        // Count { get; } - reuses Feature.Dict implementation

        [Feature(Feature.Rank, Feature.RankMulti)]
        [Widen]
        public int RankCount { get { return this.xExtent; } }

        // ContainsKey() - reuses Feature.Dict implementation

        
        /// <summary>
        /// Attempts to add a key to the collection. If the key is already present, no change is made to the collection.
        /// </summary>
        /// <param name="key">key to search for and possibly insert</param>
        /// <returns>true if the key was added; false if the key was already present</returns>
        [Feature(Feature.Rank, Feature.RankMulti)]
        public bool TryAdd(KeyType key)
        {

            return InsertUpdateInternal(
                key,
                /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*/0,
                1,
                true/*add*/,
                false/*update*/);
        }

        // TryRemove() - reuses Feature.Dict implementation

        // TryGetValue() - reuses Feature.Dict implementation

        // TrySetValue() - reuses Feature.Dict implementation

        
        /// <summary>
        /// Attempts to get the actual key data and rank index associated with a key in the collection.
        /// </summary>
        /// <param name="key">key to search for</param>
        /// <param name="keyOut">the actual key contained in the collection</param>
        /// <param name="rank">out pararmeter that returns the rank index associated with the key-value pair</param>
        /// <returns>true if they key was found</returns>
        [Feature(Feature.Rank, Feature.RankMulti)]
        public bool TryGet(KeyType key,[Payload(Payload.None)] out KeyType keyOut,[Widen] out int rank)
        {
            NodeRef node;
            /*[Widen]*/
            int xPosition;
            if (Find(key, out node, out xPosition))
            {
                keyOut = nodes[node].key;
                rank = xPosition;
                return true;
            }
            keyOut = default(KeyType);
            rank = 0;
            return false;
        }

        
        /// <summary>
        /// Retrieves the key at the specified rank index.
        /// If all keys in the collection were converted to a sorted array, this would be the equivalent of array[rank].
        /// </summary>
        /// <param name="rank">the rank index to query</param>
        /// <param name="key">an out paramter receiving the key at the specified rank index</param>
        /// <returns>the key located at that index</returns>
        /// <exception cref="ArgumentException">the key is not present in the collection</exception>
        [Feature(Feature.Rank, Feature.RankMulti)]
        public bool TryGetKeyByRank([Widen] int rank,out KeyType key)
        {
            if (rank < 0)
            {
                throw new ArgumentOutOfRangeException();
            }

            NodeRef node;
            /*[Widen]*/
            int xPosition;
            if (FindPosition(rank, out node, out xPosition))
            {
                key = nodes[node].key;
                return true;
            }
            key = default(KeyType);
            return false;
        }

        
        /// <summary>
        /// Adds a key to the collection.
        /// </summary>
        /// <param name="key">key to insert</param>
        /// <exception cref="ArgumentException">key is already present in the collection</exception>
        [Feature(Feature.Rank, Feature.RankMulti)]
        public void Add(KeyType key)
        {
            if (!TryAdd(key))
            {
                throw new ArgumentException("item already in tree");
            }
        }

        // Remove() - reuses Feature.Dict implementation

        // GetValue() - reuses Feature.Dict implementation

        // SetValue() - reuses Feature.Dict implementation

        
        /// <summary>
        /// Retrieves the actual key data and rank index associated with a key in the collection.
        /// </summary>
        /// <param name="key">key to search for</param>
        /// <param name="keyOut">the actual key contained in the collection</param>
        /// <param name="rank">out pararmeter that returns the rank index associated with the key-value pair</param>
        /// <returns>true if they key was found</returns>
        [Feature(Feature.Rank, Feature.RankMulti)]
        public void Get(KeyType key,[Payload(Payload.None)] out KeyType keyOut,[Widen] out int rank)
        {
            if (!TryGet(key, /*[Payload(Payload.None)]*/out keyOut, out rank))
            {
                throw new ArgumentException("item not in tree");
            }
        }

        
        /// <summary>
        /// Retrieves the key at the specified rank index.
        /// If all keys in the collection were converted to a sorted array, this would be the equivalent of array[rank].
        /// </summary>
        /// <param name="rank">the rank index to query</param>
        /// <returns>the key located at that index</returns>
        /// <exception cref="ArgumentException">the key is not present in the collection</exception>
        [Feature(Feature.Rank, Feature.RankMulti)]
        public KeyType GetKeyByRank([Widen] int rank)
        {
            KeyType key;
            if (!TryGetKeyByRank(rank, out key))
            {
                throw new ArgumentException("index not in tree");
            }
            return key;
        }

        
        /// <summary>
        /// Adjusts the rank count associated with the key-value pair. The countAdjust added to the existing count.
        /// For a RankMap, the only valid values are 0 (which does nothing) and -1 (which removes the key-value pair).
        /// </summary>
        /// <param name="key">key identifying the key-value pair to update</param>
        /// <param name="countAdjust">adjustment that is added to the count</param>
        /// <exception cref="ArgumentException">if the count is an invalid value or the key does not exist in the collection</exception>
        [Feature(Feature.Rank, Feature.RankMulti)]
        public void AdjustCount(KeyType key,[Widen] int countAdjust)
        {
            unchecked
            {
                NodeRef node;
                /*[Widen]*/
                int xPosition;
                /*[Widen]*/
                int xLength = 1;
                if (Find(key, out node, out xPosition))
                {
                    // update and possibly remove

                    /*[Widen]*/
                    int adjustedLength = checked(xLength + countAdjust);
                    if (adjustedLength > 0)
                    {
                        /*[Feature(Feature.Rank)]*/
                        if (adjustedLength > 1)
                        {
                            throw new ArgumentOutOfRangeException();
                        }

                        this.xExtent = checked(this.xExtent + countAdjust);

                        ShiftRightOfPath(xPosition + 1, countAdjust);
                    }
                    else if (xLength + countAdjust == 0)
                    {
                        Debug.Assert(countAdjust < 0);

                        DeleteInternal(
                            /*[Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]*/key,
                            /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*/0);
                    }
                    else
                    {
                        throw new ArgumentOutOfRangeException();
                    }
                }
                else
                {
                    // add

                    if (countAdjust < 0)
                    {
                        throw new ArgumentOutOfRangeException();
                    }
                    else if (countAdjust > 0)
                    {
                        /*[Feature(Feature.Rank)]*/
                        if (countAdjust > 1)
                        {
                            throw new ArgumentOutOfRangeException();
                        }

                        Add(key);
                    }
                    else
                    {
                        // allow non-adding case
                        Debug.Assert(countAdjust == 0);
                    }
                }
            }
        }

        // Array allocation

        [Storage(Storage.Array)]
        private NodeRef Allocate([Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)] KeyType key,bool isRed)
        {
            if (freelist == Null)
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
                if (freelist == Null)
                {
                    throw new OutOfMemoryException();
                }
            }
            NodeRef node = freelist;
            freelist = nodes[freelist].left;

            nodes[node].key = key;
            nodes[node].isRed = isRed;
            nodes[node].left = Null;
            nodes[node].right = Null;
            nodes[node].xOffset = 0;

            return node;
        }

        [Storage(Storage.Array)]
        private NodeRef Allocate([Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)] KeyType key)
        {
            return Allocate(/*[Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]*/key, true/*isRed*/);
        }

        [Storage(Storage.Array)]
        private void Free(NodeRef node)
        {
            nodes[node].key = default(KeyType); // zero any contained references for garbage collector

            nodes[node].left = freelist;
            freelist = node;
        }

        [Storage(Storage.Array)]
        private void EnsureFree(uint capacity)
        {
            unchecked
            {
                Debug.Assert(freelist == Null);
                Debug.Assert((nodes == null) || (nodes.Length >= ReservedElements));

                uint oldLength = nodes != null ? unchecked((uint)nodes.Length) : ReservedElements;
                uint newLength = checked(capacity + ReservedElements);

                Array.Resize(ref nodes, unchecked((int)newLength));

                for (long i = (long)newLength - 1; i >= oldLength; i--)
                {
                    Free(new NodeRef(unchecked((uint)i)));
                }
            }
        }


        private bool NearestLess(            out NodeRef nearestNode,            [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)] KeyType key,            [Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)][Widen] int position,            [Feature(Feature.Rank, Feature.RankMulti)] [Const(CompareKeyMode.Key, Feature.Dict)] [Const2(CompareKeyMode.Position, Feature.Range, Feature.Range2)] [SuppressConst(Feature.Rank, Feature.RankMulti)] CompareKeyMode mode,            [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)] out KeyType nearestKey,            [Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)][Widen] out int nearestStart,            bool orEqual)
        {
            NodeRef lastLess = Null;
            /*[Widen]*/
            int xPositionLastLess = 0;
            /*[Widen]*/
            int yPositionLastLess = 0;
            NodeRef node = root;
            if (node != Null)
            {
                /*[Widen]*/
                int xPosition = 0;
                /*[Widen]*/
                int yPosition = 0;
                while (true)
                {
                    unchecked
                    {
                        xPosition += nodes[node].xOffset;
                    }

                    int c;
                    if (mode == CompareKeyMode.Key)
                    {
                        c = comparer.Compare(key, nodes[node].key);
                    }
                    else
                    {
                        Debug.Assert(mode == CompareKeyMode.Position);
                        c = position.CompareTo(xPosition);
                    }
                    if (orEqual && (c == 0))
                    {
                        nearestNode = node;
                        nearestKey = nodes[node].key;
                        nearestStart = xPosition;
                        return true;
                    }
                    NodeRef next;
                    if (c <= 0)
                    {
                        next = nodes[node].left;
                    }
                    else
                    {
                        lastLess = node;
                        xPositionLastLess = xPosition;
                        yPositionLastLess = yPosition;
                        next = nodes[node].right;
                    }
                    if (next == Null)
                    {
                        break;
                    }
                    node = next;
                }
            }
            if (lastLess != Null)
            {
                nearestNode = lastLess;
                nearestKey = nodes[lastLess].key;
                nearestStart = xPositionLastLess;
                return true;
            }
            nearestNode = Null;
            nearestKey = default(KeyType);
            nearestStart = 0;
            return false;
        }

        private bool NearestGreater(            out NodeRef nearestNode,            [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)] KeyType key,            [Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)][Widen] int position,            [Feature(Feature.Rank, Feature.RankMulti)] [Const(CompareKeyMode.Key, Feature.Dict)] [Const2(CompareKeyMode.Position, Feature.Range, Feature.Range2)] [SuppressConst(Feature.Rank, Feature.RankMulti)] CompareKeyMode mode,            [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)] out KeyType nearestKey,            [Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)][Widen] out int nearestStart,            bool orEqual)
        {
            NodeRef lastGreater = Null;
            /*[Widen]*/
            int xPositionLastGreater = 0;
            /*[Widen]*/
            int yPositionLastGreater = 0;
            NodeRef node = root;
            if (node != Null)
            {
                /*[Widen]*/
                int xPosition = 0;
                /*[Widen]*/
                int yPosition = 0;
                while (true)
                {
                    unchecked
                    {
                        xPosition += nodes[node].xOffset;
                    }

                    int c;
                    if (mode == CompareKeyMode.Key)
                    {
                        c = comparer.Compare(key, nodes[node].key);
                    }
                    else
                    {
                        Debug.Assert(mode == CompareKeyMode.Position);
                        c = position.CompareTo(xPosition);
                    }
                    if (orEqual && (c == 0))
                    {
                        nearestNode = node;
                        nearestKey = nodes[node].key;
                        nearestStart = xPosition;
                        return true;
                    }
                    NodeRef next;
                    if (c < 0)
                    {
                        lastGreater = node;
                        xPositionLastGreater = xPosition;
                        yPositionLastGreater = yPosition;
                        next = nodes[node].left;
                    }
                    else
                    {
                        next = nodes[node].right;
                    }
                    if (next == Null)
                    {
                        break;
                    }
                    node = next;
                }
            }
            if (lastGreater != Null)
            {
                nearestNode = lastGreater;
                nearestKey = nodes[lastGreater].key;
                nearestStart = xPositionLastGreater;
                return true;
            }
            nearestNode = Null;
            nearestKey = default(KeyType);
            nearestStart = this.xExtent;
            return false;
        }

        // Searches tree for key location.
        // If key is not present and add==true, node is inserted.
        // If key is preset and update==true, value is replaced.
        // Returns true if a node was added or if add==false and a node was updated.
        // NOTE: update mode does *not* adjust for xLength/yLength!
        private bool InsertUpdateInternal(            [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)] KeyType key,            [Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)][Widen] int position,            [Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)][Widen] int xLength,            bool add,            bool update)
        {
            Debug.Assert((true) || (add != update));

            if (root == Null)
            {
                if (!add)
                {
                    return false;
                }

                root = Allocate(/*[Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]*/key, false);
                Debug.Assert(nodes[root].xOffset == 0);
                Debug.Assert(this.xExtent == 0);
                this.xExtent = xLength;

                Debug.Assert(this.count == 0);
                this.count = 1;
                this.version = unchecked((ushort)(this.version + 1));

                return true;
            }

            // Search for a node at bottom to insert the new node. 
            // If we can guarantee the node we found is not a 4-node, it would be easy to do insertion.
            // We split 4-nodes along the search path.
            NodeRef current = root;
            /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*/
            /*[Widen]*/
            int xPositionCurrent = 0;
            NodeRef parent = Null;
            NodeRef grandParent = Null;
            NodeRef greatGrandParent = Null;

            NodeRef successor = Null;
            /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*/
            /*[Widen]*/
            int xPositionSuccessor = 0;

            //even if we don't actually add to the set, we may be altering its structure (by doing rotations
            //and such). so update version to disable any enumerators/subsets working on it
            this.version = unchecked((ushort)(this.version + 1));

            int order = 0;
            /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*/
            /*[Widen]*/
            int xPositionParent = 0;
            while (current != Null)
            {
                unchecked
                {
                    xPositionCurrent += nodes[current].xOffset;
                }

                {
                    order = comparer.Compare(key, nodes[current].key);
                }

                if (order == 0)
                {
                    // We could have changed root node to red during the search process.
                    // We need to set it to black before we return.
                    nodes[root].isRed = false;
                    if (update)
                    {
                        nodes[current].key = key;
                    }
                    return !add;
                }

                // split a 4-node into two 2-nodes                
                if (Is4Node(current))
                {
                    Split4Node(current);
                    // We could have introduced two consecutive red nodes after split. Fix that by rotation.
                    if (IsRed(parent))
                    {
                        InsertionBalance(current, ref parent, grandParent, greatGrandParent);
                    }
                }
                greatGrandParent = grandParent;
                grandParent = parent;
                parent = current;
                xPositionParent = xPositionCurrent;
                if (order < 0)
                {
                    successor = parent;
                    xPositionSuccessor = xPositionParent;

                    current = nodes[current].left;
                }
                else
                {
                    current = nodes[current].right;
                }
            }
            Debug.Assert(current == Null);

            Debug.Assert(parent != Null, "Parent node cannot be null here!");
            // ready to insert the new node
            if (!add)
            {
                nodes[root].isRed = false;
                return false;
            }

            NodeRef node;
            if (order > 0)
            {
                // follows parent

                Debug.Assert(nodes[parent].right == Null);
                /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*/
                /*[Widen]*/
                int xLengthParent;
                if (successor != Null)
                {
                    xLengthParent = xPositionSuccessor - xPositionParent;
                }
                else
                {
                    xLengthParent = this.xExtent - xPositionParent;
                }

                /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*/
                if ((CompareKeyMode.Key == CompareKeyMode.Position)
                    && (position != unchecked(xPositionParent + xLengthParent)))
                {
                    nodes[root].isRed = false;
                    return false;
                }

                // compute here to throw before modifying tree
                /*[Widen]*/
                int xExtentNew;
uint countNew;
                try
                {
                    xExtentNew = checked(this.xExtent + xLength);
                    countNew = checked(this.count + 1);
                }
                catch (OverflowException)
                {
                    nodes[root].isRed = false;
                    throw;
                }

                node = Allocate(/*[Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]*/key);

                ShiftRightOfPath(unchecked(xPositionParent + 1), xLength);

                nodes[parent].right = node;

                nodes[node].xOffset = xLengthParent;

                this.xExtent = xExtentNew;
                this.count = countNew;
            }
            else
            {

                // compute here to throw before modifying tree
                /*[Widen]*/
                int xExtentNew;
uint countNew;
                try
                {
                    xExtentNew = checked(this.xExtent + xLength);
                    countNew = checked(this.count + 1);
                }
                catch (OverflowException)
                {
                    nodes[root].isRed = false;
                    throw;
                }

                Debug.Assert(parent == successor);

                node = Allocate(/*[Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]*/key);

                ShiftRightOfPath(xPositionParent, xLength);

                nodes[parent].left = node;

                nodes[node].xOffset = -xLength;

                this.xExtent = xExtentNew;
                this.count = countNew;
            }

            // the new node will be red, so we will need to adjust the colors if parent node is also red
            if (nodes[parent].isRed)
            {
                InsertionBalance(node, ref parent, grandParent, greatGrandParent);
            }

            // Root node is always black
            nodes[root].isRed = false;

            return true;
        }

        // DOES NOT adjust xExtent and yExtent!
        [Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]
        private void ShiftRightOfPath(            [Widen] int position,            [Widen] int xAdjust)
        {
            unchecked
            {
                /*[Widen]*/
                int xPositionCurrent = 0;
                NodeRef current = root;
                while (current != Null)
                {
                    xPositionCurrent += nodes[current].xOffset;

                    int order = position.CompareTo(xPositionCurrent);
                    if (order <= 0)
                    {
                        xPositionCurrent += xAdjust;
                        nodes[current].xOffset += xAdjust;
                        if (nodes[current].left != Null)
                        {
                            nodes[nodes[current].left].xOffset -= xAdjust;
                        }

                        if (order == 0)
                        {
                            break;
                        }

                        current = nodes[current].left;
                    }
                    else
                    {
                        current = nodes[current].right;
                    }
                }
            }
        }

        private bool DeleteInternal(            [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)] KeyType key,            [Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)][Widen] int position)
        {
            unchecked
            {
                if (root == Null)
                {
                    return false;
                }

                // Search for a node and then find its successor. 
                // Then copy the item from the successor to the matching node and delete the successor. 
                // If a node doesn't have a successor, we can replace it with its left child (if not empty.) 
                // or delete the matching node.
                // 
                // In top-down implementation, it is important to make sure the node to be deleted is not a 2-node.
                // Following code will make sure the node on the path is not a 2 Node. 

                //even if we don't actually remove from the set, we may be altering its structure (by doing rotations
                //and such). so update version to disable any enumerators/subsets working on it
                this.version = unchecked((ushort)(this.version + 1));

                NodeRef current = root;
                /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*/
                /*[Widen]*/
                int xPositionCurrent = 0;

                NodeRef parent = Null;
                /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*/
                /*[Widen]*/
                int xPositionParent = 0;

                NodeRef grandParent = Null;
                /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*/
                /*[Widen]*/
                int xPositionGrandparent = 0;

                NodeRef match = Null;
                /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*/
                /*[Widen]*/
                int xPositionMatch = 0;

                NodeRef parentOfMatch = Null;
                /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*/
                /*[Widen]*/
                int xPositionParentOfMatch = 0;

                bool foundMatch = false;

                NodeRef lastGreaterAncestor = Null;
                /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*/
                /*[Widen]*/
                int xPositionLastGreaterAncestor = 0;
                while (current != Null)
                {
                    xPositionCurrent += nodes[current].xOffset;

                    if (Is2Node(current))
                    {
                        // fix up 2-Node
                        if (parent == Null)
                        {
                            // current is root. Mark it as red
                            nodes[current].isRed = true;
                        }
                        else
                        {
                            NodeRef sibling = GetSibling(current, parent);
                            /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*/
                            /*[Widen]*/
                            int xPositionSibling = xPositionParent + nodes[sibling].xOffset;
                            if (nodes[sibling].isRed)
                            {
                                // If parent is a 3-node, flip the orientation of the red link. 
                                // We can achieve this by a single rotation        
                                // This case is converted to one of other cases below.
                                Debug.Assert(!nodes[parent].isRed, "parent must be a black node!");
                                NodeRef newTop;
                                if (nodes[parent].right == sibling)
                                {
                                    newTop = RotateLeft(parent);
                                }
                                else
                                {
                                    newTop = RotateRight(parent);
                                }
                                Debug.Assert(newTop == sibling);

                                nodes[parent].isRed = true;
                                nodes[sibling].isRed = false;    // parent's color
                                                                 // sibling becomes child of grandParent or root after rotation. Update link from grandParent or root
                                ReplaceChildOfNodeOrRoot(grandParent, parent, sibling);
                                // sibling will become grandParent of current node 
                                grandParent = sibling;
                                xPositionGrandparent = xPositionSibling;
                                if (parent == match)
                                {
                                    parentOfMatch = sibling;
                                    xPositionParentOfMatch = xPositionSibling;
                                }

                                // update sibling, this is necessary for following processing
                                sibling = (nodes[parent].left == current) ? nodes[parent].right : nodes[parent].left;
                                xPositionSibling += nodes[sibling].xOffset;
                            }
                            Debug.Assert(sibling != Null || nodes[sibling].isRed == false, "sibling must not be null and it must be black!");

                            if (Is2Node(sibling))
                            {
                                Merge2Nodes(parent, current, sibling);
                            }
                            else
                            {
                                // current is a 2-node and sibling is either a 3-node or a 4-node.
                                // We can change the color of current to red by some rotation.
                                TreeRotation rotation = RotationNeeded(parent, current, sibling);
                                NodeRef newGrandParent = Null;
                                /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*/
                                /*[Widen]*/
                                int xPositionNewGrandparent = xPositionParent - nodes[parent].xOffset;
                                switch (rotation)
                                {
                                    default:
                                        Debug.Assert(false);
                                        throw new InvalidOperationException();

                                    case TreeRotation.RightRotation:
                                        Debug.Assert(nodes[parent].left == sibling, "sibling must be left child of parent!");
                                        Debug.Assert(nodes[nodes[sibling].left].isRed, "Left child of sibling must be red!");
                                        nodes[nodes[sibling].left].isRed = false;
                                        newGrandParent = RotateRight(parent);
                                        break;
                                    case TreeRotation.LeftRotation:
                                        Debug.Assert(nodes[parent].right == sibling, "sibling must be left child of parent!");
                                        Debug.Assert(nodes[nodes[sibling].right].isRed, "Right child of sibling must be red!");
                                        nodes[nodes[sibling].right].isRed = false;
                                        newGrandParent = RotateLeft(parent);
                                        break;

                                    case TreeRotation.RightLeftRotation:
                                        Debug.Assert(nodes[parent].right == sibling, "sibling must be left child of parent!");
                                        Debug.Assert(nodes[nodes[sibling].left].isRed, "Left child of sibling must be red!");
                                        newGrandParent = RotateRightLeft(parent);
                                        break;

                                    case TreeRotation.LeftRightRotation:
                                        Debug.Assert(nodes[parent].left == sibling, "sibling must be left child of parent!");
                                        Debug.Assert(nodes[nodes[sibling].right].isRed, "Right child of sibling must be red!");
                                        newGrandParent = RotateLeftRight(parent);
                                        break;
                                }
                                xPositionNewGrandparent += nodes[newGrandParent].xOffset;

                                nodes[newGrandParent].isRed = nodes[parent].isRed;
                                nodes[parent].isRed = false;
                                nodes[current].isRed = true;
                                ReplaceChildOfNodeOrRoot(grandParent, parent, newGrandParent);
                                if (parent == match)
                                {
                                    parentOfMatch = newGrandParent;
                                    xPositionParentOfMatch = xPositionNewGrandparent;
                                }
                                grandParent = newGrandParent;
                                xPositionGrandparent = xPositionNewGrandparent;
                            }
                        }
                    }

                    int order;
                    if (foundMatch)
                    {
                        order = -1; // we don't need to compare any more once we found the match
                    }
                    else
                        {
                            order = comparer.Compare(key, nodes[current].key);
                        }

                    if (order == 0)
                    {
                        // save the matching node
                        foundMatch = true;
                        match = current;
                        parentOfMatch = parent;

                        xPositionMatch = xPositionCurrent;
                        xPositionParentOfMatch = xPositionParent;
                    }

                    grandParent = parent;
                    parent = current;

                    xPositionGrandparent = xPositionParent;
                    xPositionParent = xPositionCurrent;

                    if (order < 0)
                    {
                        if (!foundMatch)
                        {
                            lastGreaterAncestor = current;
                            xPositionLastGreaterAncestor = xPositionCurrent;
                        }

                        current = nodes[current].left;
                    }
                    else
                    {
                        current = nodes[current].right; // continue the search in right sub tree after we find a match (to find successor)
                    }
                }

                // move successor to the matching node position and replace links
                if (match != Null)
                {
                    /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*/
                    /*[Widen]*/
                    int xLength;
                    Debug.Assert(parent != Null);
                    if (parent != match)
                    {
                        xLength = xPositionParent - xPositionMatch;
                    }
                    else if (lastGreaterAncestor != Null)
                    {
                        xLength = xPositionLastGreaterAncestor - xPositionMatch;
                    }
                    else
                    {
                        xLength = this.xExtent - xPositionMatch;
                    }

                    ReplaceNode(
                        match,
                        parentOfMatch,
                        parent/*successor*/,
                        /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*/xPositionParent - xPositionMatch,
                        grandParent/*parentOfSuccessor*/);

                    ShiftRightOfPath(xPositionMatch + 1, -xLength);

                    this.xExtent = unchecked(this.xExtent - xLength);
                    this.count = unchecked(this.count - 1);

                    Free(match);
                }

                if (root != Null)
                {
                    nodes[root].isRed = false;
                }
                return foundMatch;
            }
        }

        // Replace the matching node with its successor.
        private void ReplaceNode(            NodeRef match,            NodeRef parentOfMatch,            NodeRef successor,            [Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)][Widen] int xOffsetMatchSuccessor,            NodeRef parentOfsuccessor)
        {
            unchecked
            {
                if (successor == match)
                {
                    // this node has no successor, should only happen if right child of matching node is null.
                    Debug.Assert(nodes[match].right == Null, "Right child must be null!");
                    successor = nodes[match].left;

                    if (successor != Null)
                    {
                        xOffsetMatchSuccessor = nodes[successor].xOffset;
                    }
                }
                else
                {
                    Debug.Assert(parentOfsuccessor != Null, "parent of successor cannot be null!");
                    Debug.Assert(nodes[successor].left == Null, "Left child of successor must be null!");
                    Debug.Assert((nodes[successor].right == Null && nodes[successor].isRed)
                        || (nodes[nodes[successor].right].isRed && !nodes[successor].isRed), "Successor must be in valid state");
                    if (nodes[successor].right != Null)
                    {
                        nodes[nodes[successor].right].isRed = false;
                    }

                    if (parentOfsuccessor != match)
                    {
                        // detach successor from its parent and set its right child
                        nodes[parentOfsuccessor].left = nodes[successor].right;
                        if (nodes[successor].right != Null)
                        {
                            nodes[nodes[successor].right].xOffset += nodes[successor].xOffset;
                        }
                        nodes[successor].right = nodes[match].right;
                        if (nodes[match].right != Null)
                        {
                            nodes[nodes[match].right].xOffset -= xOffsetMatchSuccessor;
                        }
                    }

                    nodes[successor].left = nodes[match].left;
                    if (nodes[match].left != Null)
                    {
                        nodes[nodes[match].left].xOffset -= xOffsetMatchSuccessor;
                    }
                }

                if (successor != Null)
                {
                    nodes[successor].isRed = nodes[match].isRed;

                    nodes[successor].xOffset = nodes[match].xOffset + xOffsetMatchSuccessor;
                }

                ReplaceChildOfNodeOrRoot(parentOfMatch/*parent*/, match/*child*/, successor/*new child*/);
            }
        }

        // Replace the child of a parent node. 
        // If the parent node is null, replace the root.        
        private void ReplaceChildOfNodeOrRoot(NodeRef parent,NodeRef child,NodeRef newChild)
        {
            if (parent != Null)
            {
                if (nodes[parent].left == child)
                {
                    nodes[parent].left = newChild;
                }
                else
                {
                    nodes[parent].right = newChild;
                }
            }
            else
            {
                root = newChild;
            }
        }

        private NodeRef GetSibling(NodeRef node,NodeRef parent)
        {
            if (nodes[parent].left == node)
            {
                return nodes[parent].right;
            }
            return nodes[parent].left;
        }

        // After calling InsertionBalance, we need to make sure current and parent up-to-date.
        // It doesn't matter if we keep grandParent and greatGrantParent up-to-date 
        // because we won't need to split again in the next node.
        // By the time we need to split again, everything will be correctly set.
        private void InsertionBalance(NodeRef current,ref NodeRef parent,NodeRef grandParent,NodeRef greatGrandParent)
        {
            Debug.Assert(grandParent != Null, "Grand parent cannot be null here!");
            bool parentIsOnRight = (nodes[grandParent].right == parent);
            bool currentIsOnRight = (nodes[parent].right == current);

            NodeRef newChildOfGreatGrandParent;
            if (parentIsOnRight == currentIsOnRight)
            {
                // same orientation, single rotation
                newChildOfGreatGrandParent = currentIsOnRight ? RotateLeft(grandParent) : RotateRight(grandParent);
            }
            else
            {
                // different orientation, double rotation
                newChildOfGreatGrandParent = currentIsOnRight ? RotateLeftRight(grandParent) : RotateRightLeft(grandParent);
                // current node now becomes the child of greatgrandparent 
                parent = greatGrandParent;
            }
            // grand parent will become a child of either parent of current.
            nodes[grandParent].isRed = true;
            nodes[newChildOfGreatGrandParent].isRed = false;

            ReplaceChildOfNodeOrRoot(greatGrandParent, grandParent, newChildOfGreatGrandParent);
        }

        private bool Is2Node(NodeRef node)
        {
            Debug.Assert(node != Null, "node cannot be null!");
            return IsBlack(node) && IsNullOrBlack(nodes[node].left) && IsNullOrBlack(nodes[node].right);
        }

        private bool Is4Node(NodeRef node)
        {
            return IsRed(nodes[node].left) && IsRed(nodes[node].right);
        }

        private bool IsBlack(NodeRef node)
        {
            return (node != Null && !nodes[node].isRed);
        }

        private bool IsNullOrBlack(NodeRef node)
        {
            return (node == Null || !nodes[node].isRed);
        }

        private bool IsRed(NodeRef node)
        {
            return (node != Null && nodes[node].isRed);
        }

        private void Merge2Nodes(NodeRef parent,NodeRef child1,NodeRef child2)
        {
            Debug.Assert(IsRed(parent), "parent must be red");
            // combing two 2-nodes into a 4-node
            nodes[parent].isRed = false;
            nodes[child1].isRed = true;
            nodes[child2].isRed = true;
        }

        [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]
        private NodeRef FindNode(KeyType item)
        {
            NodeRef current = root;
            while (current != Null)
            {
                int order = comparer.Compare(item, nodes[current].key);
                if (order == 0)
                {
                    return current;
                }
                else
                {
                    current = (order < 0) ? nodes[current].left : nodes[current].right;
                }
            }

            return Null;
        }

        [Feature(Feature.Rank, Feature.RankMulti)]
        private bool Find(            KeyType key,            out NodeRef match,            [Widen] out int xPositionMatch)
        {
            unchecked
            {
                match = Null;
                NodeRef successor = Null;
                xPositionMatch = 0;

                /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*/
                /*[Widen]*/
                int xPositionSuccessor = 0;
                NodeRef current = root;
                /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*/
                /*[Widen]*/
                int xPositionCurrent = 0;
                NodeRef lastGreaterAncestor = Null;
                /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*/
                /*[Widen]*/
                int xPositionLastGreaterAncestor = 0;
                while (current != Null)
                {
                    xPositionCurrent += nodes[current].xOffset;

                    int order = (match != Null) ? -1 : comparer.Compare(key, nodes[current].key);

                    if (order == 0)
                    {
                        xPositionMatch = xPositionCurrent;
                        match = current;
                    }

                    successor = current;
                    xPositionSuccessor = xPositionCurrent;

                    if (order < 0)
                    {
                        if (match == Null)
                        {
                            lastGreaterAncestor = current;
                            xPositionLastGreaterAncestor = xPositionCurrent;
                        }
                        current = nodes[current].left;
                    }
                    else
                    {
                        current = nodes[current].right; // continue the search in right sub tree after we find a match
                    }
                }

                if (match != Null)
                {
                    Debug.Assert(successor != Null);
                }

                return match != Null;
            }
        }

        [Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]
        private bool FindPosition(            [Widen] int position,            out NodeRef lastLessEqual,            [Widen] out int xPositionLastLessEqual)
        {
            unchecked
            {
                lastLessEqual = Null;
                /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*/
                xPositionLastLessEqual = 0;
                NodeRef successor = Null;

                NodeRef current = root;
                /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*/
                /*[Widen]*/
                int xPositionCurrent = 0;
                /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*/
                /*[Widen]*/
                int xPositionSuccessor = 0;
                while (current != Null)
                {
                    xPositionCurrent += nodes[current].xOffset;

                    if (position < (xPositionCurrent))
                    {
                        successor = current;
                        xPositionSuccessor = xPositionCurrent;

                        current = nodes[current].left;
                    }
                    else
                    {
                        lastLessEqual = current;
                        xPositionLastLessEqual = xPositionCurrent;

                        current = nodes[current].right; // try to find successor
                    }
                }

                return (position >= 0) && (position < (this.xExtent));
            }
        }

        private NodeRef RotateLeft(NodeRef node)
        {
            unchecked
            {
                NodeRef r = nodes[node].right;

                if (nodes[r].left != Null)
                {
                    nodes[nodes[r].left].xOffset += nodes[r].xOffset;
                }
                /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*/
                /*[Widen]*/
                int xOffsetR = nodes[r].xOffset;
                nodes[r].xOffset += nodes[node].xOffset;
                nodes[node].xOffset = -xOffsetR;

                nodes[node].right = nodes[r].left;
                nodes[r].left = node;

                return r;
            }
        }

        private NodeRef RotateLeftRight(NodeRef node)
        {
            unchecked
            {
                NodeRef lChild = nodes[node].left;
                NodeRef lrGrandChild = nodes[lChild].right;

                /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*/
                /*[Widen]*/
                int xOffsetNode = nodes[node].xOffset;
                /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*/
                /*[Widen]*/
                int xOffsetLChild = nodes[lChild].xOffset;
                /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*/
                /*[Widen]*/
                int xOffsetLRGrandchild = nodes[lrGrandChild].xOffset;

                nodes[lrGrandChild].xOffset = xOffsetLRGrandchild + xOffsetLChild + xOffsetNode;
                nodes[lChild].xOffset = -xOffsetLRGrandchild;
                nodes[node].xOffset = -xOffsetLRGrandchild - xOffsetLChild;
                if (nodes[lrGrandChild].left != Null)
                {
                    nodes[nodes[lrGrandChild].left].xOffset += xOffsetLRGrandchild;
                }
                if (nodes[lrGrandChild].right != Null)
                {
                    nodes[nodes[lrGrandChild].right].xOffset += xOffsetLRGrandchild + xOffsetLChild;
                }

                nodes[node].left = nodes[lrGrandChild].right;
                nodes[lrGrandChild].right = node;
                nodes[lChild].right = nodes[lrGrandChild].left;
                nodes[lrGrandChild].left = lChild;

                return lrGrandChild;
            }
        }

        private NodeRef RotateRight(NodeRef node)
        {
            unchecked
            {
                NodeRef l = nodes[node].left;

                if (nodes[l].right != Null)
                {
                    nodes[nodes[l].right].xOffset += nodes[l].xOffset;
                }
                /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*/
                /*[Widen]*/
                int xOffsetL = nodes[l].xOffset;
                nodes[l].xOffset += nodes[node].xOffset;
                nodes[node].xOffset = -xOffsetL;

                nodes[node].left = nodes[l].right;
                nodes[l].right = node;

                return l;
            }
        }

        private NodeRef RotateRightLeft(NodeRef node)
        {
            unchecked
            {
                NodeRef rChild = nodes[node].right;
                NodeRef rlGrandChild = nodes[rChild].left;

                /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*/
                /*[Widen]*/
                int xOffsetNode = nodes[node].xOffset;
                /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*/
                /*[Widen]*/
                int xOffsetRChild = nodes[rChild].xOffset;
                /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*/
                /*[Widen]*/
                int xOffsetRLGrandchild = nodes[rlGrandChild].xOffset;

                nodes[rlGrandChild].xOffset = xOffsetRLGrandchild + xOffsetRChild + xOffsetNode;
                nodes[rChild].xOffset = -xOffsetRLGrandchild;
                nodes[node].xOffset = -xOffsetRLGrandchild - xOffsetRChild;
                if (nodes[rlGrandChild].left != Null)
                {
                    nodes[nodes[rlGrandChild].left].xOffset += xOffsetRLGrandchild + xOffsetRChild;
                }
                if (nodes[rlGrandChild].right != Null)
                {
                    nodes[nodes[rlGrandChild].right].xOffset += xOffsetRLGrandchild;
                }

                nodes[node].right = nodes[rlGrandChild].left;
                nodes[rlGrandChild].left = node;
                nodes[rChild].left = nodes[rlGrandChild].right;
                nodes[rlGrandChild].right = rChild;

                return rlGrandChild;
            }
        }

        private enum TreeRotation
        {
            LeftRotation = 1,
            RightRotation = 2,
            RightLeftRotation = 3,
            LeftRightRotation = 4,
        }

        private TreeRotation RotationNeeded(NodeRef parent,NodeRef current,NodeRef sibling)
        {
            Debug.Assert(IsRed(nodes[sibling].left) || IsRed(nodes[sibling].right), "sibling must have at least one red child");
            if (IsRed(nodes[sibling].left))
            {
                if (nodes[parent].left == current)
                {
                    return TreeRotation.RightLeftRotation;
                }
                return TreeRotation.RightRotation;
            }
            else
            {
                if (nodes[parent].left == current)
                {
                    return TreeRotation.LeftRotation;
                }
                return TreeRotation.LeftRightRotation;
            }
        }

        private void Split4Node(NodeRef node)
        {
            nodes[node].isRed = true;
            nodes[nodes[node].left].isRed = false;
            nodes[nodes[node].right].isRed = false;
        }


        //
        // Non-invasive tree inspection support
        //

        // Helpers

        [Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]
        [ExcludeFromCodeCoverage]
        private void ValidateRanges()
        {
            if (root != Null)
            {
                Stack<STuple<NodeRef, /*[Widen]*/int, /*[Widen]*/int, /*[Widen]*/int>> stack = new Stack<STuple<NodeRef, /*[Widen]*/int, /*[Widen]*/int, /*[Widen]*/int>>();

                /*[Widen]*/
                int offset = 0;
                /*[Widen]*/
                int leftEdge = 0;
                /*[Widen]*/
                int rightEdge = this.xExtent;

                NodeRef node = root;
                while (node != Null)
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
                    while (node != Null)
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

        [ExcludeFromCodeCoverage]
        object INonInvasiveTreeInspection.Root { get { return root != Null ? (object)root : null; } }

        [ExcludeFromCodeCoverage]
        object INonInvasiveTreeInspection.GetLeftChild(object node)
        {
            NodeRef n = (NodeRef)node;
            return nodes[n].left != Null ? (object)nodes[n].left : null;
        }

        [ExcludeFromCodeCoverage]
        object INonInvasiveTreeInspection.GetRightChild(object node)
        {
            NodeRef n = (NodeRef)node;
            return nodes[n].right != Null ? (object)nodes[n].right : null;
        }

        [ExcludeFromCodeCoverage]
        object INonInvasiveTreeInspection.GetKey(object node)
        {
            NodeRef n = (NodeRef)node;
            object key = null;
            key = nodes[n].key;
            return key;
        }

        [ExcludeFromCodeCoverage]
        object INonInvasiveTreeInspection.GetValue(object node)
        {
            object value = null;
            return value;
        }

        [ExcludeFromCodeCoverage]
        object INonInvasiveTreeInspection.GetMetadata(object node)
        {
            NodeRef n = (NodeRef)node;
            return nodes[n].isRed ? "red" : "black";
        }

        [ExcludeFromCodeCoverage]
        void INonInvasiveTreeInspection.Validate()
        {
            if (root != Null)
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

                    if (nodes[node].left != Null)
                    {
                        /*[Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]*/
                        if (!(comparer.Compare(nodes[nodes[node].left].key, nodes[node].key) < 0))
                        {
                            throw new InvalidOperationException("ordering invariant");
                        }
                        worklist.Enqueue(nodes[node].left);
                    }
                    if (nodes[node].right != Null)
                    {
                        /*[Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]*/
                        if (!(comparer.Compare(nodes[node].key, nodes[nodes[node].right].key) < 0))
                        {
                            throw new InvalidOperationException("ordering invariant");
                        }
                        worklist.Enqueue(nodes[node].right);
                    }
                }
            }

            /*[Feature(Feature.Rank, Feature.MultiRank, Feature.Range, Feature.Range2)]*/
            ValidateRanges();

            ValidateDepthInvariant();
        }

        [ExcludeFromCodeCoverage]
        private void ValidateDepthInvariant()
        {
            int min = Int32.MaxValue;
            MinDepth(root, 0, ref min);
            int depth = MaxDepth(root);
            min++;
            int max = depth + 1;

            if ((2 * min < max) || (depth > 2 * Math.Log(this.count + 1) / Math.Log(2)))
            {
                throw new InvalidOperationException("depth invariant");
            }
        }

        [ExcludeFromCodeCoverage]
        private int MaxDepth(NodeRef root)
        {
            return (root == Null) ? 0 : (1 + Math.Max(MaxDepth(nodes[root].left), MaxDepth(nodes[root].right)));
        }

        [ExcludeFromCodeCoverage]
        private void MinDepth(NodeRef root,int depth,ref int min)
        {
            if (root == Null)
            {
                min = Math.Min(min, depth);
            }
            else
            {
                if (depth < min)
                {
                    MinDepth(nodes[root].left, depth + 1, ref min);
                }
                if (depth < min)
                {
                    MinDepth(nodes[root].right, depth + 1, ref min);
                }
            }
        }

        // INonInvasiveMultiRankMapInspection

        [Feature(Feature.Rank, Feature.RankMulti)]
        [ExcludeFromCodeCoverage]
        [Widen]
        MultiRankMapEntry[] INonInvasiveMultiRankMapInspection.GetRanks()
        {
            /*[Widen]*/
            MultiRankMapEntry[] ranks = new MultiRankMapEntry[Count];
            int i = 0;

            if (root != Null)
            {
                Stack<STuple<NodeRef, /*[Widen]*/int>> stack = new Stack<STuple<NodeRef, /*[Widen]*/int>>();

                /*[Widen]*/
                int xOffset = 0;

                NodeRef node = root;
                while (node != Null)
                {
                    xOffset += nodes[node].xOffset;
                    stack.Push(new STuple<NodeRef, /*[Widen]*/int>(node, xOffset));
                    node = nodes[node].left;
                }
                while (stack.Count != 0)
                {
                    STuple<NodeRef, /*[Widen]*/int> t = stack.Pop();
                    node = t.Item1;
                    xOffset = t.Item2;

                    object key = null;
                    key = nodes[node].key;
                    object value = null;

                    /*[Widen]*/
                    ranks[i++] = new MultiRankMapEntry(key, new Range(xOffset, 0), value);

                    node = nodes[node].right;
                    while (node != Null)
                    {
                        xOffset += nodes[node].xOffset;
                        stack.Push(new STuple<NodeRef, /*[Widen]*/int>(node, xOffset));
                        node = nodes[node].left;
                    }
                }
                if (!(i == ranks.Length))
                {
                    Debug.Assert(false);
                    throw new InvalidOperationException();
                }

                for (i = 1; i < ranks.Length; i++)
                {
                    if (!(ranks[i - 1].rank.start < ranks[i].rank.start))
                    {
                        Debug.Assert(false);
                        throw new InvalidOperationException();
                    }
                    ranks[i - 1].rank.length = ranks[i].rank.start - ranks[i - 1].rank.start;
                }

                ranks[i - 1].rank.length = this.xExtent - ranks[i - 1].rank.start;
            }

            return ranks;
        }

        [Feature(Feature.Rank, Feature.RankMulti)]
        [ExcludeFromCodeCoverage]
        void INonInvasiveMultiRankMapInspection.Validate()
        {
            ((INonInvasiveTreeInspection)this).Validate();
        }


        //
        // Enumeration
        //

        /// <summary>
        /// Get the default enumerator, which is the fast enumerator for red-black trees.
        /// </summary>
        /// <returns></returns>
        public IEnumerator<EntryRankList<KeyType>> GetEnumerator()
        {
            return GetFastEnumerable().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        /// <summary>
        /// Get the robust enumerator. The robust enumerator uses an internal key cursor and queries the tree using the NextGreater()
        /// method to advance the enumerator. This enumerator is robust because it tolerates changes to the underlying tree. If a key
        /// is inserted or removed and it comes before the enumerator’s current key in sorting order, it will have no affect on the
        /// enumerator. If a key is inserted or removed and it comes after the enumerator’s current key (i.e. in the portion of the
        /// collection the enumerator hasn’t visited yet), the enumerator will include the key if inserted or skip the key if removed.
        /// Because the enumerator queries the tree for each element it’s running time per element is O(lg N), or O(N lg N) to
        /// enumerate the entire tree.
        /// </summary>
        /// <returns>An IEnumerable which can be used in a foreach statement</returns>
        public IEnumerable<EntryRankList<KeyType>> GetRobustEnumerable()
        {
            return new RobustEnumerableSurrogate(this);
        }

        public struct RobustEnumerableSurrogate : IEnumerable<EntryRankList<KeyType>>
        {
            private readonly RedBlackTreeArrayRankList<KeyType> tree;

            public RobustEnumerableSurrogate(RedBlackTreeArrayRankList<KeyType> tree)
            {
                this.tree = tree;
            }

            public IEnumerator<EntryRankList<KeyType>> GetEnumerator()
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
        /// deletion and will throw an InvalidOperationException when next advanced. For red-black trees, a
        /// failed insertion or deletion will still invalidate the enumerator, as failed operations may still have performed
        /// rotations in the tree. The complexity of the fast enumerator is O(1) per element, or O(N) to enumerate the
        /// entire tree.
        /// </summary>
        /// <returns>An IEnumerable which can be used in a foreach statement</returns>
        public IEnumerable<EntryRankList<KeyType>> GetFastEnumerable()
        {
            return new FastEnumerableSurrogate(this);
        }

        public struct FastEnumerableSurrogate : IEnumerable<EntryRankList<KeyType>>
        {
            private readonly RedBlackTreeArrayRankList<KeyType> tree;

            public FastEnumerableSurrogate(RedBlackTreeArrayRankList<KeyType> tree)
            {
                this.tree = tree;
            }

            public IEnumerator<EntryRankList<KeyType>> GetEnumerator()
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
        /// it keeps a current key and uses NearestGreater to find the next one. However, since it uses queries it
        /// is slow, O(n lg(n)) to enumerate the entire tree.
        /// </summary>
        public class RobustEnumerator : IEnumerator<EntryRankList<KeyType>>
        {
            private readonly RedBlackTreeArrayRankList<KeyType> tree;
            private bool started;
            private bool valid;
            [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]
            private KeyType currentKey;

            public RobustEnumerator(RedBlackTreeArrayRankList<KeyType> tree)
            {
                this.tree = tree;
                Reset();
            }

            public EntryRankList<KeyType> Current
            {
                get
                {

                    if (valid)
                        /*[Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]*/
                        {
                            KeyType key = currentKey;
                            /*[Widen]*/
                            int rank = 0;
                            /*[Widen]*/
                            int count = 1;
                            // OR
                            /*[Feature(Feature.Rank, Feature.RankMulti)]*/
                            tree.Get(
                                /*[Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]*/currentKey,
                                /*[Payload(Payload.None)]*/out key,
                                /*[Feature(Feature.Rank, Feature.RankMulti)]*/out rank);

                            /*[Feature(Feature.Rank)]*/
                            Debug.Assert(count == 1);

                            return new EntryRankList<KeyType>(
                                /*[Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]*/key,
                                /*[Feature(Feature.Rank, Feature.RankMulti)]*/rank);
                        }
                    return new EntryRankList<KeyType>();
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

                if (!started)
                {
                    /*[Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]*/
                    valid = tree.Least(out currentKey);

                    started = true;
                }
                else if (valid)
                {
                    /*[Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]*/
                    valid = tree.NearestGreater(currentKey, out currentKey);
                }

                return valid;
            }

            public void Reset()
            {
                started = false;
                valid = false;
                currentKey = default(KeyType);
            }
        }

        /// <summary>
        /// This enumerator is fast because it uses an in-order traversal of the tree that has O(1) cost per element.
        /// However, any Add or Remove to the tree invalidates it.
        /// </summary>
        public class FastEnumerator : IEnumerator<EntryRankList<KeyType>>
        {
            private readonly RedBlackTreeArrayRankList<KeyType> tree;
            private ushort version;
            private NodeRef currentNode;
            private NodeRef nextNode;
            [Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]
            [Widen]
            private int currentXStart, nextXStart;

            private readonly Stack<STuple<NodeRef, /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*//*[Widen]*/int>> stack
                = new Stack<STuple<NodeRef, /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*//*[Widen]*/int>>();

            public FastEnumerator(RedBlackTreeArrayRankList<KeyType> tree)
            {
                this.tree = tree;
                Reset();
            }

            public EntryRankList<KeyType> Current
            {
                get
                {
                    if (currentNode != tree.Null)
                    {
                        /*[Feature(Feature.Rank)]*/
                        Debug.Assert(nextXStart - currentXStart == 1);

                        return new EntryRankList<KeyType>(
                            /*[Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]*/tree.nodes[currentNode].key,
                            /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*/currentXStart);
                    }
                    return new EntryRankList<KeyType>();
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
                return currentNode != tree.Null;
            }

            public void Reset()
            {
                stack.Clear();
                currentNode = tree.Null;
                nextNode = tree.Null;
                currentXStart = 0;
                nextXStart = 0;

                this.version = tree.version;

                PushSuccessor(
                    tree.root,
                    /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*/0);

                Advance();
            }

            private void PushSuccessor(                NodeRef node,                [Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)][Widen] int xPosition)
            {
                while (node != tree.Null)
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

                nextNode = tree.Null;
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


        //
        // Cloning
        //

        public object Clone()
        {
            return new RedBlackTreeArrayRankList<KeyType>(this);
        }
    }
}
