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

#pragma warning disable CS1572 // silence warning: XML comment has a param tag for '...', but there is no parameter by that name
#pragma warning disable CS1573 // silence warning: Parameter '...' has no matching param tag in the XML comment
#pragma warning disable CS1587 // silence warning: XML comment is not placed on a valid language element
#pragma warning disable CS1591 // silence warning: Missing XML comment for publicly visible type or member

//
// Implementation of top-down splay tree written by Daniel Sleator <sleator@cs.cmu.edu>.
// Taken from http://www.link.cs.cmu.edu/link/ftp-site/splaying/top-down-splay.c
//
// An overview of splay trees can be found here: https://en.wikipedia.org/wiki/Splay_tree
//

namespace TreeLib
{

    /// <summary>
    /// Implements a map, list or range collection using a splay tree. 
    /// </summary>
    
    /// <summary>
    /// Represents a ordered key-value mapping, augmented with rank information. The rank of a key-value pair is the index it would
    /// be located in if all the key-value pairs in the tree were placed into a sorted array.
    /// </summary>
    /// <typeparam name="KeyType">Type of key used to index collection. Must be comparable.</typeparam>
    /// <typeparam name="ValueType">Type of value associated with each entry.</typeparam>
    public class SplayTreeArrayRankMap<[Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)] KeyType, [Payload(Payload.Value)] ValueType> :

        /*[Feature(Feature.Rank)]*//*[Payload(Payload.Value)]*//*[Widen]*/IRankMap<KeyType, ValueType>,

        INonInvasiveTreeInspection,
        /*[Feature(Feature.Rank, Feature.RankMulti)]*//*[Widen]*/INonInvasiveMultiRankMapInspection,

        IEnumerable<EntryRankMap<KeyType, ValueType>>,
        IEnumerable,
        ITreeEnumerable<EntryRankMap<KeyType, ValueType>>,
        /*[Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]*/IKeyedTreeEnumerable<KeyType, EntryRankMap<KeyType, ValueType>>,

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
            [Payload(Payload.Value)]
            public ValueType value;

            [Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]
            [Widen]
            public int xOffset;
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

            [ExcludeFromCodeCoverage]
            public override bool Equals(object obj)
            {
                return node == (uint)obj;
            }

            public override int GetHashCode()
            {
                return node.GetHashCode();
            }
        }

        [Storage(Storage.Array)]
        private const uint ReservedElements = 2;
        [Storage(Storage.Array)]
        private readonly static NodeRef _Nil = new NodeRef(0);
        [Storage(Storage.Array)]
        private readonly static NodeRef N = new NodeRef(1);

        [Storage(Storage.Array)]
        private NodeRef Nil { get { return SplayTreeArrayRankMap<KeyType, ValueType>._Nil; } } // allow tree.Nil or this.Nil in all cases

        [Storage(Storage.Array)]
        private Node[] nodes;

        //
        // State for both array & object form
        //

        private NodeRef root;
        [Count]
        private uint count;

        [Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]
        [Widen]
        private int xExtent;

        [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]
        private readonly IComparer<KeyType> comparer;

        private readonly AllocationMode allocationMode;
        private NodeRef freelist;

        private uint version;

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
        public SplayTreeArrayRankMap([Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)] IComparer<KeyType> comparer,uint capacity,AllocationMode allocationMode)
        {
            if (allocationMode == AllocationMode.DynamicDiscard)
            {
                throw new ArgumentException();
            }

            this.comparer = comparer;
            this.root = Nil;

            this.allocationMode = allocationMode;
            this.freelist = Nil;
            EnsureFree(capacity);
        }

        /// <summary>
        /// Create a new collection using an array storage mechanism, based on a splay tree, with the specified capacity and allocation mode and using
        /// the default comparer.
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
        public SplayTreeArrayRankMap(uint capacity,AllocationMode allocationMode)
            : this(/*[Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]*/Comparer<KeyType>.Default, capacity, allocationMode)
        {
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
        public SplayTreeArrayRankMap(uint capacity)
            : this(/*[Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]*/Comparer<KeyType>.Default, capacity, AllocationMode.DynamicRetainFreelist)
        {
        }

        /// <summary>
        /// Create a new collection using an array storage mechanism, based on a splay tree, using
        /// the specified comparer. The allocation mode is DynamicRetainFreelist.
        /// </summary>
        /// <param name="comparer">The comparer to use for sorting keys</param>
        [Storage(Storage.Array)]
        [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]
        public SplayTreeArrayRankMap(IComparer<KeyType> comparer)
            : this(comparer, 0, AllocationMode.DynamicRetainFreelist)
        {
        }

        /// <summary>
        /// Create a new collection using an array storage mechanism, based on a splay tree, using
        /// the default comparer. The allocation mode is DynamicRetainFreelist.
        /// </summary>
        [Storage(Storage.Array)]
        public SplayTreeArrayRankMap()
            : this(/*[Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]*/Comparer<KeyType>.Default, 0, AllocationMode.DynamicRetainFreelist)
        {
        }

        /// <summary>
        /// Create a new collection based on a splay tree that is an exact clone of the provided collection, including in
        /// allocation mode, content, structure, capacity and free list state, and comparer.
        /// </summary>
        /// <param name="original">the tree to copy</param>
        [Storage(Storage.Array)]
        public SplayTreeArrayRankMap(SplayTreeArrayRankMap<KeyType, ValueType> original)
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
        /// Returns the number of key-value pairs in the collection as an unsigned int.
        /// </summary>
        /// <exception cref="OverflowException">The collection contains more than UInt32.MaxValue key-value pairs.</exception>
        public uint Count { get { return checked((uint)this.count); } }

        
        /// <summary>
        /// Returns the number of key-value pairs in the collection.
        /// </summary>
        public long LongCount { get { return unchecked((long)this.count); } }

        
        /// <summary>
        /// Removes all key-value pairs from the collection.
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

        
        /// <summary>
        /// Determines whether the key is present in the collection.
        /// </summary>
        /// <param name="key">Key to search for</param>
        /// <returns>true if the key is present in the collection</returns>
        [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]
        public bool ContainsKey(KeyType key)
        {
            if (root != Nil)
            {
                Splay(ref root, key, comparer);
                return 0 == comparer.Compare(key, nodes[root].key);
            }
            return false;
        }

        [Feature(Feature.Dict, Feature.Rank)]
        private bool PredicateAddRemoveOverrideCore(            bool initial,            bool resident,            [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]ref KeyType key,            [Payload(Payload.Value)]ref ValueType value,            [Payload(Payload.Value)]UpdatePredicate<KeyType, ValueType> predicateMap)
        {
            uint version = this.version;

            // very crude protection against completely trashing the tree if the predicate tries to modify it
            NodeRef savedRoot = this.root;
            this.root = Nil;
uint savedCount = this.count;
            this.count = 0;
            /*[Widen]*/
            int savedXExtent = this.xExtent;
            this.xExtent = 0;
            try
            {
                /*[Payload(Payload.Value)]*/
                initial = predicateMap(key, ref value, resident);
            }
            finally
            {
                this.root = savedRoot;
                this.count = savedCount;
                this.xExtent = savedXExtent;
            }

            if (version != this.version)
            {
                throw new InvalidOperationException();
            }

            return initial;
        }

        [Feature(Feature.Dict, Feature.Rank)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool PredicateAddRemoveOverride(            bool initial,            bool resident,            [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]ref KeyType key,            [Payload(Payload.Value)]ref ValueType value,            [Payload(Payload.Value)]UpdatePredicate<KeyType, ValueType> predicateMap)
        {
            bool predicateExists = false;
            /*[Payload(Payload.Value)]*/
            predicateExists = predicateMap != null;
            if (predicateExists)
            {
                initial = PredicateAddRemoveOverrideCore(
                    initial,
                    resident,
                    /*[Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]*/ref key,
                    /*[Payload(Payload.Value)]*/ref value,
                    /*[Payload(Payload.Value)]*/predicateMap);
            }

            return initial;
        }

        [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]
        private bool TrySetOrAddInternal(            KeyType key,            [Payload(Payload.Value)]ValueType value,            bool updateExisting,[Feature(Feature.Dict, Feature.Rank)][Payload(Payload.Value)]UpdatePredicate<KeyType, ValueType> predicateMap)
        {
            unchecked
            {

                Splay(ref root, key, comparer);
                int c = comparer.Compare(key, nodes[root].key);

                bool add = true;

                /*[Feature(Feature.Dict, Feature.Rank)]*/
                {
                    bool predicateExists = false;
                    /*[Payload(Payload.Value)]*/
                    predicateExists = predicateMap != null;
                    if (predicateExists)
                    {
                        if ((root != Nil) && (c == 0))
                        {
                            value = nodes[root].value;
                        }

                        add = PredicateAddRemoveOverride(
                            add/*initial*/,
                            (root != Nil) && (c == 0)/*resident*/,
                            ref key,
                            /*[Payload(Payload.Value)]*/ref value,
                            /*[Payload(Payload.Value)]*/predicateMap);

                        if (!add && (c != 0))
                        {
                            return false;
                        }
                    }
                }

                if (add && ((root == Nil) || (c < 0)))
                {
uint countNew = checked(this.count + 1);
                    /*[Widen]*/
                    int xExtentNew = checked(this.xExtent + 1);

                    NodeRef i = Allocate();
                    nodes[i].key = key;
                    nodes[i].value = value;
                    nodes[i].xOffset = nodes[root].xOffset;

                    nodes[i].left = nodes[root].left;
                    nodes[i].right = root;
                    if (root != Nil)
                    {
                        nodes[root].xOffset = 1;
                        nodes[root].left = Nil;
                    }

                    root = i;

                    this.count = countNew;
                    this.xExtent = xExtentNew;

                    return true;
                }
                else if (add && (c > 0))
                {
                    // insert item just after root

                    Debug.Assert(root != Nil);
uint countNew = checked(this.count + 1);
                    /*[Widen]*/
                    int xExtentNew = checked(this.xExtent + 1);

                    NodeRef i = Allocate();
                    nodes[i].key = key;
                    nodes[i].value = value;

                    nodes[i].xOffset = nodes[root].xOffset + 1;
                    if (nodes[root].right != Nil)
                    {
                        nodes[nodes[root].right].xOffset += -1 + 1;
                    }
                    nodes[root].xOffset = -1;

                    nodes[i].left = root;
                    nodes[i].right = nodes[root].right;
                    nodes[root].right = Nil;

                    root = i;

                    this.count = countNew;
                    this.xExtent = xExtentNew;

                    return true;
                }
                else
                {
                    Debug.Assert(c == 0);
                    if (updateExisting)
                    {
                        nodes[root].value = value;
                    }
                    return false;
                }
            }
        }

        [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]
        private bool TrySetOrRemoveInternal(            KeyType key,            bool updateExisting,            [Feature(Feature.Dict, Feature.Rank)][Payload(Payload.Value)]UpdatePredicate<KeyType, ValueType> predicateMap)
        {
            unchecked
            {
                if (root != Nil)
                {
                    Splay(ref root, key, comparer);
                    int c = comparer.Compare(key, nodes[root].key);

                    ValueType value = nodes[root].value;

                    bool remove = true;
                    /*[Feature(Feature.Dict, Feature.Rank)]*/
                    {
                        remove = PredicateAddRemoveOverride(
                            remove/*initial*/,
                            c == 0/*resident*/,
                            ref key,
                            /*[Payload(Payload.Value)]*/ref value,
                            /*[Payload(Payload.Value)]*/predicateMap);
                    }

                    if (c == 0)
                    {
                        if (remove)
                        {
                            /*[Feature(Feature.Rank, Feature.RankMulti)]*/
                            Splay(ref nodes[root].right, key, comparer);
                            /*[Feature(Feature.Rank, Feature.RankMulti)]*/
                            Debug.Assert((nodes[root].right == Nil) || (nodes[nodes[root].right].left == Nil));
                            /*[Feature(Feature.Rank, Feature.RankMulti)]*/
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
                                Splay(ref x, key, comparer);
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
                        else if (updateExisting)
                        {
                            nodes[root].value = value;
                        }
                    }
                }
                return false;
            }
        }

        
        /// <summary>
        /// Attempts to remove a key-value pair from the collection. If the key is not present, no change is made to the collection.
        /// </summary>
        /// <param name="key">the key to search for and possibly remove</param>
        /// <returns>true if the key-value pair was found and removed</returns>
        [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]
        public bool TryRemove(KeyType key)
        {
            return TrySetOrRemoveInternal(
                key,
                false/*updateExisting*/,
                /*[Feature(Feature.Dict, Feature.Rank)]*//*[Payload(Payload.Value)]*/null/*predicateMap*/);
        }

        
        /// <summary>
        /// Attempts to get the value associated with a key in the collection.
        /// </summary>
        /// <param name="key">key to search for</param>
        /// <param name="value">out parameter that returns the value associated with the key</param>
        /// <returns>true if they key was found</returns>
        [Payload(Payload.Value)]
        [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]
        public bool TryGetValue(KeyType key,out ValueType value)
        {
            if (root != Nil)
            {
                Splay(ref root, key, comparer);
                if (0 == comparer.Compare(key, nodes[root].key))
                {
                    value = nodes[root].value;
                    return true;
                }
            }
            value = default(ValueType);
            return false;
        }

        
        /// <summary>
        /// Attempts to set the value associated with a key in the collection. If the key is not present, no change is made to the collection.
        /// </summary>
        /// <param name="key">key to search for</param>
        /// <param name="value">replacement value to associate with the key</param>
        /// <returns>true if the key-value pair was found and the value was updated</returns>
        [Payload(Payload.Value)]
        [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]
        public bool TrySetValue(KeyType key,ValueType value)
        {
            if (root != Nil)
            {
                Splay(ref root, key, comparer);
                if (0 == comparer.Compare(key, nodes[root].key))
                {
                    nodes[root].value = value;
                    return true;
                }
            }
            return false;
        }

        
        /// <summary>
        /// Removes a key-value pair from the collection.
        /// </summary>
        /// <param name="key">key of the key-value pair to remove</param>
        /// <exception cref="ArgumentException">the key is not present in the collection</exception>
        [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]
        public void Remove(KeyType key)
        {
            if (!TryRemove(key))
            {
                throw new ArgumentException("item not in tree");
            }
        }

        
        /// <summary>
        /// Retrieves the value associated with a key in the collection
        /// </summary>
        /// <param name="key">key to search for</param>
        /// <returns>the value associated with the key</returns>
        /// <exception cref="ArgumentException">the key is not present in the collection</exception>
        [Payload(Payload.Value)]
        [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]
        public ValueType GetValue(KeyType key)
        {
            ValueType value;
            if (!TryGetValue(key, out value))
            {
                throw new ArgumentException("item not in tree");
            }
            return value;
        }

        
        /// <summary>
        /// Updates the value associated with a key in the collection
        /// </summary>
        /// <param name="key">key to search for</param>
        /// <param name="value">replacement value to associate with the key</param>
        /// <exception cref="ArgumentException">the key is not present in the collection</exception>
        [Payload(Payload.Value)]
        [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]
        public void SetValue(KeyType key,ValueType value)
        {
            if (!TrySetValue(key, value))
            {
                throw new ArgumentException("item not in tree");
            }
        }

        
        /// <summary>
        /// Conditionally update or add an item, based on the return value from the predicate.
        /// ConditionalSetOrAdd is more efficient when the decision to add or update depends on the value of the item.
        /// </summary>
        /// <param name="key">The key of the item to update or add</param>
        /// <param name="predicate">The predicate to invoke. If the predicate returns true, the item will be added to the
        /// collection if it is not already in the collection. Whether true or false, if the item is in the collection, the
        /// ref value upon return will be used to update the item.</param>
        /// <exception cref="InvalidOperationException">The tree was modified while the predicate was invoked. If this happens,
        /// the tree may be left in an unstable state.</exception>
        [Feature(Feature.Dict, Feature.Rank)]
        public void ConditionalSetOrAdd(KeyType key,[Payload(Payload.Value)]UpdatePredicate<KeyType, ValueType> predicateMap)
        {
            /*[Payload(Payload.Value)]*/
            if (predicateMap == null)
            {
                throw new ArgumentNullException();
            }

            TrySetOrAddInternal(
                key,
                /*[Payload(Payload.Value)]*/default(ValueType),
                true/*updateExisting*/,
                /*[Feature(Feature.Dict, Feature.Rank)]*//*[Payload(Payload.Value)]*/predicateMap);
        }

        
        /// <summary>
        /// Conditionally update or add an item, based on the return value from the predicate.
        /// ConditionalSetOrRemove is more efficient when the decision to remove or update depends on the value of the item.
        /// </summary>
        /// <param name="key">The key of the item to update or remove</param>
        /// <param name="predicate">The predicate to invoke. If the predicate returns true, the item will be removed from the
        /// collection if it is in the collection. If the item remains in the collection, the ref value upon return will be used
        /// to update the item.</param>
        /// <exception cref="InvalidOperationException">The tree was modified while the predicate was invoked. If this happens,
        /// the tree may be left in an unstable state.</exception>
        [Feature(Feature.Dict, Feature.Rank)]
        public void ConditionalSetOrRemove(KeyType key,[Payload(Payload.Value)]UpdatePredicate<KeyType, ValueType> predicateMap)
        {
            /*[Payload(Payload.Value)]*/
            if (predicateMap == null)
            {
                throw new ArgumentNullException();
            }

            TrySetOrRemoveInternal(
                key,
                true/*updateExisting*/,
                /*[Feature(Feature.Dict, Feature.Rank)]*//*[Payload(Payload.Value)]*/predicateMap);
        }

        [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]
        private bool LeastInternal(out KeyType keyOut,[Payload(Payload.Value)] out ValueType valueOut)
        {
            if (root != Nil)
            {
                Splay(ref root, default(KeyType), FixedComparer.Minimum);
                keyOut = nodes[root].key;
                valueOut = nodes[root].value;
                return true;
            }
            keyOut = default(KeyType);
            valueOut = default(ValueType);
            return false;
        }

        
        /// <summary>
        /// Retrieves the lowest in the collection (in sort order) and the value associated with it.
        /// </summary>
        /// <param name="leastOut">out parameter receiving the key</param>
        /// <param name="value">out parameter receiving the value associated with the key</param>
        /// <returns>true if a key was found (i.e. collection contains at least 1 key-value pair)</returns>
        [Payload(Payload.Value)]
        [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]
        public bool Least(out KeyType keyOut,[Payload(Payload.Value)] out ValueType valueOut)
        {
            return LeastInternal(out keyOut, /*[Payload(Payload.Value)]*/out valueOut);
        }

        
        /// <summary>
        /// Retrieves the lowest key in the collection (in sort order)
        /// </summary>
        /// <param name="leastOut">out parameter receiving the key</param>
        /// <returns>true if a key was found (i.e. collection contains at least 1 key-value pair)</returns>
        [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]
        public bool Least(out KeyType keyOut)
        {
            ValueType value;
            return LeastInternal(out keyOut, /*[Payload(Payload.Value)]*/out value);
        }

        [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]
        private bool GreatestInternal(out KeyType keyOut,[Payload(Payload.Value)] out ValueType valueOut)
        {
            if (root != Nil)
            {
                Splay(ref root, default(KeyType), FixedComparer.Maximum);
                keyOut = nodes[root].key;
                valueOut = nodes[root].value;
                return true;
            }
            keyOut = default(KeyType);
            valueOut = default(ValueType);
            return false;
        }

        
        /// <summary>
        /// Retrieves the highest key in the collection (in sort order) and the value associated with it.
        /// </summary>
        /// <param name="greatestOut">out parameter receiving the key</param>
        /// <param name="value">out parameter receiving the value associated with the key</param>
        /// <returns>true if a key was found (i.e. collection contains at least 1 key-value pair)</returns>
        [Payload(Payload.Value)]
        [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]
        public bool Greatest(out KeyType keyOut,[Payload(Payload.Value)] out ValueType valueOut)
        {
            return GreatestInternal(out keyOut, /*[Payload(Payload.Value)]*/out valueOut);
        }

        
        /// <summary>
        /// Retrieves the highest key in the collection (in sort order)
        /// </summary>
        /// <param name="greatestOut">out parameter receiving the key</param>
        /// <returns>true if a key was found (i.e. collection contains at least 1 key-value pair)</returns>
        [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]
        public bool Greatest(out KeyType keyOut)
        {
            ValueType value;
            return GreatestInternal(out keyOut, /*[Payload(Payload.Value)]*/out value);
        }

        [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]
        private bool NearestLess(KeyType key,out KeyType nearestKey,[Payload(Payload.Value)] out ValueType valueOut,bool orEqual)
        {
            if (root != Nil)
            {
                Splay(ref root, key, comparer);
                int rootComparison = comparer.Compare(key, nodes[root].key);
                if ((rootComparison > 0) || (orEqual && (rootComparison == 0)))
                {
                    nearestKey = nodes[root].key;
                    valueOut = nodes[root].value;
                    return true;
                }
                else if (nodes[root].left != Nil)
                {
                    KeyType rootKey = nodes[root].key;
                    Splay(ref nodes[root].left, rootKey, comparer);
                    nearestKey = nodes[nodes[root].left].key;
                    valueOut = nodes[nodes[root].left].value;
                    return true;
                }
            }
            nearestKey = default(KeyType);
            valueOut = default(ValueType);
            return false;
        }

        
        /// <summary>
        /// Retrieves the highest key in the collection that is less than or equal to the provided key and
        /// the value associated with it.
        /// </summary>
        /// <param name="key">key to search below</param>
        /// <param name="nearestKey">highest key less than or equal to provided key</param>
        /// <param name="value">out parameter receiving the value associated with the returned key</param>
        /// <returns>true if there was a key less than or equal to the provided key</returns>
        [Payload(Payload.Value)]
        [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]
        public bool NearestLessOrEqual(KeyType key,out KeyType nearestKey,[Payload(Payload.Value)] out ValueType valueOut)
        {
            return NearestLess(key, out nearestKey, /*[Payload(Payload.Value)]*/out valueOut, true/*orEqual*/);
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
            ValueType value;
            return NearestLess(key, out nearestKey, /*[Payload(Payload.Value)]*/out value, true/*orEqual*/);
        }

        
        /// <summary>
        /// Retrieves the highest key in the collection that is less than or equal to the provided key and
        /// the value and rank associated with it.
        /// </summary>
        /// <param name="key">key to search below</param>
        /// <param name="nearestKey">highest key less than or equal to provided key</param>
        /// <param name="value">out parameter receiving the value associated with the returned key</param>
        /// <param name="rank">the rank of the returned key</param>
        /// <returns>true if there was a key less than or equal to the provided key</returns>
        [Feature(Feature.Rank, Feature.RankMulti)]
        public bool NearestLessOrEqual(KeyType key,out KeyType nearestKey,[Payload(Payload.Value)] out ValueType valueOut,[Feature(Feature.Rank, Feature.RankMulti)][Widen] out int rank)
        {
            rank = 0;
            bool f = NearestLess(key, out nearestKey, /*[Payload(Payload.Value)]*/out valueOut, true/*orEqual*/);
            if (f)
            {
                ValueType duplicateValue;
                bool g = TryGet(nearestKey, /*[Payload(Payload.Value)]*/out duplicateValue, out rank);
                Debug.Assert(g);
            }
            return f;
        }

        
        /// <summary>
        /// Retrieves the highest key in the collection that is less than the provided key and
        /// the value associated with it.
        /// </summary>
        /// <param name="key">key to search below</param>
        /// <param name="nearestKey">highest key less than the provided key</param>
        /// <param name="value">out parameter receiving the value associated with the returned key</param>
        /// <returns>true if there was a key less than the provided key</returns>
        [Payload(Payload.Value)]
        [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]
        public bool NearestLess(KeyType key,out KeyType nearestKey,[Payload(Payload.Value)] out ValueType valueOut)
        {
            return NearestLess(key, out nearestKey, /*[Payload(Payload.Value)]*/out valueOut, false/*orEqual*/);
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
            ValueType value;
            return NearestLess(key, out nearestKey, /*[Payload(Payload.Value)]*/out value, false/*orEqual*/);
        }

        
        /// <summary>
        /// Retrieves the highest key in the collection that is less than the provided key and
        /// the value and rank  associated with it.
        /// </summary>
        /// <param name="key">key to search below</param>
        /// <param name="nearestKey">highest key less than the provided key</param>
        /// <param name="value">out parameter receiving the value associated with the returned key</param>
        /// <param name="rank">the rank of the returned key</param>
        /// <returns>true if there was a key less than the provided key</returns>
        [Feature(Feature.Rank, Feature.RankMulti)]
        public bool NearestLess(KeyType key,out KeyType nearestKey,[Payload(Payload.Value)] out ValueType valueOut,[Feature(Feature.Rank, Feature.RankMulti)][Widen] out int rank)
        {
            rank = 0;
            bool f = NearestLess(key, out nearestKey, /*[Payload(Payload.Value)]*/out valueOut, false/*orEqual*/);
            if (f)
            {
                ValueType duplicateValue;
                bool g = TryGet(nearestKey, /*[Payload(Payload.Value)]*/out duplicateValue, out rank);
                Debug.Assert(g);
            }
            return f;
        }

        [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]
        private bool NearestGreater(KeyType key,out KeyType nearestKey,[Payload(Payload.Value)] out ValueType valueOut,bool orEqual)
        {
            if (root != Nil)
            {
                Splay(ref root, key, comparer);
                int rootComparison = comparer.Compare(key, nodes[root].key);
                if ((rootComparison < 0) || (orEqual && (rootComparison == 0)))
                {
                    nearestKey = nodes[root].key;
                    valueOut = nodes[root].value;
                    return true;
                }
                else if (nodes[root].right != Nil)
                {
                    KeyType rootKey = nodes[root].key;
                    Splay(ref nodes[root].right, rootKey, comparer);
                    nearestKey = nodes[nodes[root].right].key;
                    valueOut = nodes[nodes[root].right].value;
                    return true;
                }
            }
            nearestKey = default(KeyType);
            valueOut = default(ValueType);
            return false;
        }

        
        /// <summary>
        /// Retrieves the lowest key in the collection that is greater than or equal to the provided key and
        /// the value associated with it.
        /// </summary>
        /// <param name="key">key to search above</param>
        /// <param name="nearestKey">lowest key greater than or equal to provided key</param>
        /// <param name="value">out parameter receiving the value associated with the returned key</param>
        /// <returns>true if there was a key greater than or equal to the provided key</returns>
        [Payload(Payload.Value)]
        [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]
        public bool NearestGreaterOrEqual(KeyType key,out KeyType nearestKey,[Payload(Payload.Value)] out ValueType valueOut)
        {
            return NearestGreater(key, out nearestKey, /*[Payload(Payload.Value)]*/out valueOut, true/*orEqual*/);
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
            ValueType value;
            return NearestGreater(key, out nearestKey, /*[Payload(Payload.Value)]*/out value, true/*orEqual*/);
        }

        
        /// <summary>
        /// Retrieves the lowest key in the collection that is greater than or equal to the provided key and
        /// the value and rank  associated with it.
        /// </summary>
        /// <param name="key">key to search above</param>
        /// <param name="nearestKey">lowest key greater than or equal to provided key</param>
        /// <param name="value">out parameter receiving the value associated with the returned key</param>
        /// <param name="rank">the rank of the returned key</param>
        /// <returns>true if there was a key greater than or equal to the provided key</returns>
        [Feature(Feature.Rank, Feature.RankMulti)]
        public bool NearestGreaterOrEqual(KeyType key,out KeyType nearestKey,[Payload(Payload.Value)] out ValueType valueOut,[Feature(Feature.Rank, Feature.RankMulti)][Widen] out int rank)
        {
            rank = this.xExtent;
            bool f = NearestGreater(key, out nearestKey, /*[Payload(Payload.Value)]*/out valueOut, true/*orEqual*/);
            if (f)
            {
                ValueType duplicateValue;
                bool g = TryGet(nearestKey, /*[Payload(Payload.Value)]*/out duplicateValue, out rank);
                Debug.Assert(g);
            }
            return f;
        }

        
        /// <summary>
        /// Retrieves the lowest key in the collection that is greater than the provided key and
        /// the value associated with it.
        /// </summary>
        /// <param name="key">key to search above</param>
        /// <param name="nearestKey">lowest key greater than the provided key</param>
        /// <param name="value">out parameter receiving the value associated with the returned key</param>
        /// <returns>true if there was a key greater than the provided key</returns>
        [Payload(Payload.Value)]
        [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]
        public bool NearestGreater(KeyType key,out KeyType nearestKey,[Payload(Payload.Value)] out ValueType valueOut)
        {
            return NearestGreater(key, out nearestKey, /*[Payload(Payload.Value)]*/out valueOut, false/*orEqual*/);
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
            ValueType value;
            return NearestGreater(key, out nearestKey, /*[Payload(Payload.Value)]*/out value, false/*orEqual*/);
        }

        
        /// <summary>
        /// Retrieves the lowest key in the collection that is greater than the provided key and
        /// the value and rank  associated with it.
        /// </summary>
        /// <param name="key">key to search above</param>
        /// <param name="nearestKey">lowest key greater than the provided key</param>
        /// <param name="value">out parameter receiving the value associated with the returned key</param>
        /// <param name="rank">the rank of the returned key</param>
        /// <returns>true if there was a key greater than the provided key</returns>
        [Feature(Feature.Rank, Feature.RankMulti)]
        public bool NearestGreater(KeyType key,out KeyType nearestKey,[Payload(Payload.Value)] out ValueType valueOut,[Feature(Feature.Rank, Feature.RankMulti)][Widen] out int rank)
        {
            rank = this.xExtent;
            bool f = NearestGreater(key, out nearestKey, /*[Payload(Payload.Value)]*/out valueOut, false/*orEqual*/);
            if (f)
            {
                ValueType duplicateValue;
                bool g = TryGet(nearestKey, /*[Payload(Payload.Value)]*/out duplicateValue, out rank);
                Debug.Assert(g);
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
        /// Attempts to add a key-value pair to the collection. If the key is already present, no change is made to the collection.
        /// </summary>
        /// <param name="key">key to search for and possibly insert</param>
        /// <param name="value">value to associate with the key</param>
        /// <returns>true if the key-value pair was added; false if the key was already present</returns>
        [Feature(Feature.Rank, Feature.RankMulti)]
        public bool TryAdd(KeyType key,[Payload(Payload.Value)] ValueType value)
        {
            return TrySetOrAddInternal(
                key,
                /*[Payload(Payload.Value)]*/value,
                false/*updateExisting*/,
                /*[Feature(Feature.Dict, Feature.Rank)]*//*[Payload(Payload.Value)]*/null/*predicateMap*/);
        }

        // TryRemove() - reuses Feature.Dict implementation

        // TryGetValue() - reuses Feature.Dict implementation

        // TrySetValue() - reuses Feature.Dict implementation

        
        /// <summary>
        /// Attempts to get the value and rank index associated with a key in the collection.
        /// </summary>
        /// <param name="key">key to search for</param>
        /// <param name="value">out parameter that returns the value associated with the key</param>
        /// <param name="rank">out pararmeter that returns the rank index associated with the key-value pair</param>
        /// <returns>true if they key was found</returns>
        [Feature(Feature.Rank, Feature.RankMulti)]
        public bool TryGet(KeyType key,[Payload(Payload.Value)] out ValueType value,[Widen] out int rank)
        {
            unchecked
            {
                if (root != Nil)
                {
                    Splay(ref root, key, comparer);
                    if (0 == comparer.Compare(key, nodes[root].key))
                    {
                        rank = nodes[root].xOffset;
                        value = nodes[root].value;
                        return true;
                    }
                }
                rank = 0;
                value = default(ValueType);
                return false;
            }
        }

        
        /// <summary>
        /// Attempts to return the key of a key-value pair at the specified rank index.
        /// If all key-value pairs in the collection were converted to a sorted array, this would be the equivalent of array[rank].Key.
        /// </summary>
        /// <param name="rank">the rank index to query</param>
        /// <param name="key">the key located at that index</param>
        /// <returns>true if there is an element at the the specified index</returns>
        [Feature(Feature.Rank, Feature.RankMulti)]
        public bool TryGetKeyByRank([Widen] int rank,out KeyType key)
        {
            unchecked
            {
                if (rank < 0)
                {
                    throw new ArgumentOutOfRangeException();
                }

                if (root != Nil)
                {
                    Splay2(ref root, rank);
                    if (rank < Start(root))
                    {
                        Debug.Assert(rank >= 0);
                        Debug.Assert(nodes[root].left != Nil); // because rank >= 0 and tree starts at 0
                        Splay2(ref nodes[root].left, 0);
                        key = nodes[nodes[root].left].key;
                        return true;
                    }

                    Splay2(ref nodes[root].right, 0);
                    Debug.Assert((nodes[root].right == Nil) || (nodes[nodes[root].right].left == Nil));
                    /*[Widen]*/
                    int length = nodes[root].right != Nil ? nodes[nodes[root].right].xOffset : this.xExtent - nodes[root].xOffset;
                    if (/*(rank >= Start(root, Side.X)) && */rank < Start(root) + length)
                    {
                        Debug.Assert(rank >= Start(root));
                        key = nodes[root].key;
                        return true;
                    }
                }
                key = default(KeyType);
                return false;
            }
        }

        
        /// <summary>
        /// Adds a key-value pair to the collection.
        /// </summary>
        /// <param name="key">key to insert</param>
        /// <param name="value">value to associate with the key</param>
        /// <exception cref="ArgumentException">key is already present in the collection</exception>
        [Feature(Feature.Rank, Feature.RankMulti)]
        public void Add(KeyType key,[Payload(Payload.Value)] ValueType value)
        {
            if (!TryAdd(key, /*[Payload(Payload.Value)]*/value))
            {
                throw new ArgumentException("item already in tree");
            }
        }

        // Remove() - reuses Feature.Dict implementation

        // GetValue() - reuses Feature.Dict implementation

        // SetValue() - reuses Feature.Dict implementation

        
        /// <summary>
        /// Retrieves the value and rank index associated with a key in the collection.
        /// </summary>
        /// <param name="key">key to search for</param>
        /// <param name="value">out parameter that returns the value associated with the key</param>
        /// <param name="rank">out pararmeter that returns the rank index associated with the key-value pair</param>
        /// <exception cref="ArgumentException">the key is not present in the collection</exception>
        [Feature(Feature.Rank, Feature.RankMulti)]
        public void Get(KeyType key,[Payload(Payload.Value)] out ValueType value,[Widen] out int rank)
        {
            if (!TryGet(key, /*[Payload(Payload.Value)]*/out value, out rank))
            {
                throw new ArgumentException("item not in tree");
            }
        }

        
        /// <summary>
        /// Retrieves the key of a key-value pair at the specified rank index.
        /// If all key-value pairs in the collection were converted to a sorted array, this would be the equivalent of array[rank].Key.
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
        /// <returns>The adjusted count</returns>
        /// <exception cref="ArgumentException">if the count is an invalid value or the key does not exist in the collection</exception>
        /// <exception cref="OverflowException">the sum of counts would have exceeded Int32.MaxValue</exception>
        [Feature(Feature.Rank, Feature.RankMulti)]
        [Widen]
        public int AdjustCount(KeyType key,[Widen] int countAdjust)
        {
            unchecked
            {
                Splay(ref root, key, comparer);
                int c;
                if ((root != Nil) && ((c = comparer.Compare(key, nodes[root].key)) == 0))
                {
                    // update and possibly remove

                    Splay(ref nodes[root].right, key, comparer);
                    Debug.Assert((nodes[root].right == Nil) || (nodes[nodes[root].right].left == Nil));
                    /*[Widen]*/
                    int oldLength = nodes[root].right != Nil ? nodes[nodes[root].right].xOffset : this.xExtent - nodes[root].xOffset;

                    /*[Widen]*/
                    int adjustedLength = checked(oldLength + countAdjust);
                    if (adjustedLength > 0)
                    {
                        /*[Feature(Feature.Rank)]*/
                        if (adjustedLength > 1)
                        {
                            throw new ArgumentOutOfRangeException();
                        }

                        this.xExtent = checked(this.xExtent + countAdjust);

                        if (nodes[root].right != Nil)
                        {
                            unchecked
                            {
                                nodes[nodes[root].right].xOffset += countAdjust;
                            }
                        }

                        return adjustedLength;
                    }
                    else if (oldLength + countAdjust == 0)
                    {
                        Debug.Assert(countAdjust < 0);

                        NodeRef dead, x;

                        dead = root;
                        if (nodes[root].left == Nil)
                        {
                            x = nodes[root].right;
                            if (x != Nil)
                            {
                                Debug.Assert(nodes[root].xOffset == 0);
                                nodes[x].xOffset = 0; //nodes[x].xOffset = nodes[root].xOffset;
                            }
                        }
                        else
                        {
                            x = nodes[root].left;
                            nodes[x].xOffset += nodes[root].xOffset;
                            Splay(ref x, key, comparer);
                            Debug.Assert(nodes[x].right == Nil);
                            if (nodes[root].right != Nil)
                            {
                                nodes[nodes[root].right].xOffset += nodes[root].xOffset - nodes[x].xOffset + countAdjust;
                            }
                            nodes[x].right = nodes[root].right;
                        }
                        root = x;

                        this.count = unchecked(this.count - 1);
                        this.xExtent = unchecked(this.xExtent - oldLength);
                        Free(dead);

                        return 0;
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

                        // TODO: suboptimal - inline Add and remove duplicate Splay()

                        Add(key, /*[Payload(Payload.Value)]*/default(ValueType));

                        return countAdjust;
                    }
                    else
                    {
                        // allow non-adding case
                        Debug.Assert(countAdjust == 0);
                        return 0;
                    }
                }
            }
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
            nodes[node].key = default(KeyType); // zero any contained references for garbage collector
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

                for (long i = (long)newLength - 1; i >= oldLength; i--)
                {
                    Free(new NodeRef(unchecked((uint)i)));
                }
            }
        }


        // http://www.link.cs.cmu.edu/link/ftp-site/splaying/top-down-splay.c
        //
        //            An implementation of top-down splaying
        //                D. Sleator <sleator@cs.cmu.edu>
        //                         March 1992
        //
        //"Splay trees", or "self-adjusting search trees" are a simple and
        //efficient data structure for storing an ordered set. The data
        //structure consists of a binary tree, without parent pointers, and no
        //additional fields. It allows searching, insertion, deletion,
        //deletemin, deletemax, splitting, joining, and many other operations,
        //all with amortized logarithmic performance. Since the trees adapt to
        //the sequence of requests, their performance on real access patterns is
        //typically even better. Splay trees are described in a number of texts
        //and papers [1,2,3,4,5].
        //
        //The code here is adapted from simple top-down splay, at the bottom of
        //page 669 of [3]. It can be obtained via anonymous ftp from
        //spade.pc.cs.cmu.edu in directory /usr/sleator/public.
        //
        //The chief modification here is that the splay operation works even if the
        //item being splayed is not in the tree, and even if the tree root of the
        //tree is NULL. So the line:
        //
        //                          t = splay(i, t);
        //
        //causes it to search for item with key i in the tree rooted at t. If it's
        //there, it is splayed to the root. If it isn't there, then the node put
        //at the root is the last one before NULL that would have been reached in a
        //normal binary search for i. (It's a neighbor of i in the tree.) This
        //allows many other operations to be easily implemented, as shown below.
        //
        //[1] "Fundamentals of data structures in C", Horowitz, Sahni,
        //   and Anderson-Freed, Computer Science Press, pp 542-547.
        //[2] "Data Structures and Their Algorithms", Lewis and Denenberg,
        //   Harper Collins, 1991, pp 243-251.
        //[3] "Self-adjusting Binary Search Trees" Sleator and Tarjan,
        //   JACM Volume 32, No 3, July 1985, pp 652-686.
        //[4] "Data Structure and Algorithm Analysis", Mark Weiss,
        //   Benjamin Cummins, 1992, pp 119-130.
        //[5] "Data Structures, Algorithms, and Performance", Derick Wood,
        //   Addison-Wesley, 1993, pp 367-375.

        // use FixedComparer for finding the first or last in a tree
        [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]
        private class FixedComparer : IComparer<KeyType>
        {
            private readonly int fixedResult;

            public readonly static FixedComparer Minimum = new FixedComparer(-1);
            public readonly static FixedComparer Maximum = new FixedComparer(1);

            public FixedComparer(int fixedResult)
            {
                this.fixedResult = fixedResult;
            }

            public int Compare(KeyType x,KeyType y)
            {
                return fixedResult;
            }
        }

        [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]
        private void Splay(ref NodeRef root,KeyType leftComparand,IComparer<KeyType> comparer)
        {
            unchecked
            {
                this.version = unchecked(this.version + 1);

                if (root == Nil)
                {
                    return;
                }

                NodeRef t = root;

                nodes[N].left = Nil;
                nodes[N].right = Nil;

                NodeRef l = N;
                /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*/
                /*[Widen]*/
                int lxOffset = 0;
                NodeRef r = N;
                /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*/
                /*[Widen]*/
                int rxOffset = 0;

                while (true)
                {
                    int c;

                    c = comparer.Compare(leftComparand, nodes[t].key);
                    if (c < 0)
                    {
                        if (nodes[t].left == Nil)
                        {
                            break;
                        }
                        c = comparer.Compare(leftComparand, nodes[nodes[t].left].key);
                        if (c < 0)
                        {
                            // rotate right
                            NodeRef u = nodes[t].left;
                            /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*/
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
                        c = comparer.Compare(leftComparand, nodes[nodes[t].right].key);
                        if (c > 0)
                        {
                            // rotate left
                            NodeRef u = nodes[t].right;
                            /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*/
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

        [Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Widen]
        private int Start(NodeRef n)
        {
            return nodes[n].xOffset;
        }

        [Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]
        private void Splay2(ref NodeRef root,[Widen] int position)
        {
            unchecked
            {
                this.version = unchecked(this.version + 1);

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
                        c = position.CompareTo(nodes[t].xOffset + nodes[nodes[t].right].xOffset);
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

                    Check.Assert((offset >= leftEdge) && (offset < rightEdge), "range containment invariant");

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

        /// <summary>
        /// INonInvasiveTreeInspection.Root is a diagnostic method intended to be used ONLY for validation of trees
        /// during unit testing. It is not intended for consumption by users of the library and there is no
        /// guarrantee that it will be supported in future versions.
        /// </summary>
        [ExcludeFromCodeCoverage]
        object INonInvasiveTreeInspection.Root { get { return root != Nil ? (object)root : null; } }

        /// <summary>
        /// INonInvasiveTreeInspection.GetLeftChild() is a diagnostic method intended to be used ONLY for validation of trees
        /// during unit testing. It is not intended for consumption by users of the library and there is no
        /// guarrantee that it will be supported in future versions.
        /// </summary>
        [ExcludeFromCodeCoverage]
        object INonInvasiveTreeInspection.GetLeftChild(object node)
        {
            NodeRef n = (NodeRef)node;
            return nodes[n].left != Nil ? (object)nodes[n].left : null;
        }

        /// <summary>
        /// INonInvasiveTreeInspection.GetRightChild() is a diagnostic method intended to be used ONLY for validation of trees
        /// during unit testing. It is not intended for consumption by users of the library and there is no
        /// guarrantee that it will be supported in future versions.
        /// </summary>
        [ExcludeFromCodeCoverage]
        object INonInvasiveTreeInspection.GetRightChild(object node)
        {
            NodeRef n = (NodeRef)node;
            return nodes[n].right != Nil ? (object)nodes[n].right : null;
        }

        /// <summary>
        /// INonInvasiveTreeInspection.GetKey() is a diagnostic method intended to be used ONLY for validation of trees
        /// during unit testing. It is not intended for consumption by users of the library and there is no
        /// guarrantee that it will be supported in future versions.
        /// </summary>
        [ExcludeFromCodeCoverage]
        object INonInvasiveTreeInspection.GetKey(object node)
        {
            NodeRef n = (NodeRef)node;
            object key = null;
            key = nodes[n].key;
            return key;
        }

        /// <summary>
        /// INonInvasiveTreeInspection.GetValue() is a diagnostic method intended to be used ONLY for validation of trees
        /// during unit testing. It is not intended for consumption by users of the library and there is no
        /// guarrantee that it will be supported in future versions.
        /// </summary>
        [ExcludeFromCodeCoverage]
        object INonInvasiveTreeInspection.GetValue(object node)
        {
            NodeRef n = (NodeRef)node;
            object value = null;
            value = nodes[n].value;
            return value;
        }

        /// <summary>
        /// INonInvasiveTreeInspection.GetMetadata() is a diagnostic method intended to be used ONLY for validation of trees
        /// during unit testing. It is not intended for consumption by users of the library and there is no
        /// guarrantee that it will be supported in future versions.
        /// </summary>
        [ExcludeFromCodeCoverage]
        object INonInvasiveTreeInspection.GetMetadata(object node)
        {
            return null;
        }

        /// <summary>
        /// INonInvasiveTreeInspection.Validate() is a diagnostic method intended to be used ONLY for validation of trees
        /// during unit testing. It is not intended for consumption by users of the library and there is no
        /// guarrantee that it will be supported in future versions.
        /// </summary>
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

                    Check.Assert(!visited.ContainsKey(node), "cycle");
                    visited.Add(node, false);

                    if (nodes[node].left != Nil)
                    {
                        /*[Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]*/
                        Check.Assert(comparer.Compare(nodes[nodes[node].left].key, nodes[node].key) < 0, "ordering invariant");
                        worklist.Enqueue(nodes[node].left);
                    }
                    if (nodes[node].right != Nil)
                    {
                        /*[Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]*/
                        Check.Assert(comparer.Compare(nodes[node].key, nodes[nodes[node].right].key) < 0, "ordering invariant");
                        worklist.Enqueue(nodes[node].right);
                    }
                }
            }

            /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*/
            ValidateRanges();
        }

        // INonInvasiveMultiRankMapInspection

        /// <summary>
        /// INonInvasiveMultiRankMapInspection.GetRanks() is a diagnostic method intended to be used ONLY for validation of trees
        /// during unit testing. It is not intended for consumption by users of the library and there is no
        /// guarrantee that it will be supported in future versions.
        /// </summary>
        [Feature(Feature.Rank, Feature.RankMulti)]
        [Widen]
        MultiRankMapEntry[] /*[Widen]*/INonInvasiveMultiRankMapInspection.GetRanks()
        {
            /*[Widen]*/
            MultiRankMapEntry[] ranks = new /*[Widen]*/MultiRankMapEntry[Count];
            int i = 0;

            if (root != Nil)
            {
                Stack<STuple<NodeRef, /*[Widen]*/int>> stack = new Stack<STuple<NodeRef, /*[Widen]*/int>>();

                /*[Widen]*/
                int xOffset = 0;

                NodeRef node = root;
                while (node != Nil)
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
                    value = nodes[node].value;

                    ranks[i++] = new /*[Widen]*/MultiRankMapEntry(key, new /*[Widen]*/Range(xOffset, 0), value);

                    node = nodes[node].right;
                    while (node != Nil)
                    {
                        xOffset += nodes[node].xOffset;
                        stack.Push(new STuple<NodeRef, /*[Widen]*/int>(node, xOffset));
                        node = nodes[node].left;
                    }
                }
                Check.Assert(i == ranks.Length, "count invariant");

                for (i = 1; i < ranks.Length; i++)
                {
                    Check.Assert(ranks[i - 1].rank.start < ranks[i].rank.start, "range sequence invariant");
                    ranks[i - 1].rank.length = ranks[i].rank.start - ranks[i - 1].rank.start;
                }

                ranks[i - 1].rank.length = this.xExtent - ranks[i - 1].rank.start;
            }

            return ranks;
        }

        /// <summary>
        /// INonInvasiveMultiRankMapInspection.Validate() is a diagnostic method intended to be used ONLY for validation of trees
        /// during unit testing. It is not intended for consumption by users of the library and there is no
        /// guarrantee that it will be supported in future versions.
        /// </summary>
        [Feature(Feature.Rank, Feature.RankMulti)]
        void /*[Widen]*/INonInvasiveMultiRankMapInspection.Validate()
        {
            ((INonInvasiveTreeInspection)this).Validate();
        }


        //
        // IEnumerable
        //

        /// <summary>
        /// Get the default enumerator, which is the robust enumerator for splay trees.
        /// </summary>
        /// <returns></returns>
        public IEnumerator<EntryRankMap<KeyType, ValueType>> GetEnumerator()
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

        //
        // ITreeEnumerable
        //

        public IEnumerable<EntryRankMap<KeyType, ValueType>> GetEnumerable()
        {
            return new RobustEnumerableSurrogate(this, true/*forward*/);
        }

        public IEnumerable<EntryRankMap<KeyType, ValueType>> GetEnumerable(bool forward)
        {
            return new RobustEnumerableSurrogate(this, forward);
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
        public IEnumerable<EntryRankMap<KeyType, ValueType>> GetRobustEnumerable()
        {
            return new RobustEnumerableSurrogate(this, true/*forward*/);
        }

        public IEnumerable<EntryRankMap<KeyType, ValueType>> GetRobustEnumerable(bool forward)
        {
            return new RobustEnumerableSurrogate(this, forward);
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
        public IEnumerable<EntryRankMap<KeyType, ValueType>> GetFastEnumerable()
        {
            return new FastEnumerableSurrogate(this, true/*forward*/);
        }

        public IEnumerable<EntryRankMap<KeyType, ValueType>> GetFastEnumerable(bool forward)
        {
            return new FastEnumerableSurrogate(this, forward);
        }

        //
        // IKeyedTreeEnumerable
        //

        [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]
        public IEnumerable<EntryRankMap<KeyType, ValueType>> GetEnumerable(KeyType startAt)
        {
            return new RobustEnumerableSurrogate(this, startAt, true/*forward*/); // default
        }

        [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]
        public IEnumerable<EntryRankMap<KeyType, ValueType>> GetEnumerable(KeyType startAt,bool forward)
        {
            return new RobustEnumerableSurrogate(this, startAt, forward); // default
        }

        [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]
        public IEnumerable<EntryRankMap<KeyType, ValueType>> GetFastEnumerable(KeyType startAt)
        {
            return new FastEnumerableSurrogate(this, startAt, true/*forward*/);
        }

        [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]
        public IEnumerable<EntryRankMap<KeyType, ValueType>> GetFastEnumerable(KeyType startAt,bool forward)
        {
            return new FastEnumerableSurrogate(this, startAt, forward);
        }

        [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]
        public IEnumerable<EntryRankMap<KeyType, ValueType>> GetRobustEnumerable(KeyType startAt)
        {
            return new RobustEnumerableSurrogate(this, startAt, true/*forward*/);
        }

        [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]
        public IEnumerable<EntryRankMap<KeyType, ValueType>> GetRobustEnumerable(KeyType startAt,bool forward)
        {
            return new RobustEnumerableSurrogate(this, startAt, forward);
        }

        //
        // Surrogates
        //

        public struct RobustEnumerableSurrogate : IEnumerable<EntryRankMap<KeyType, ValueType>>
        {
            private readonly SplayTreeArrayRankMap<KeyType, ValueType> tree;
            private readonly bool forward;

            [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]
            private readonly bool startKeyed;
            [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]
            private readonly KeyType startKey;

            // Construction

            public RobustEnumerableSurrogate(SplayTreeArrayRankMap<KeyType, ValueType> tree,bool forward)
            {
                this.tree = tree;
                this.forward = forward;

                this.startKeyed = false;
                this.startKey = default(KeyType);
            }

            [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]
            public RobustEnumerableSurrogate(SplayTreeArrayRankMap<KeyType, ValueType> tree,KeyType startKey,bool forward)
            {
                this.tree = tree;
                this.forward = forward;

                this.startKeyed = true;
                this.startKey = startKey;
            }

            // IEnumerable

            public IEnumerator<EntryRankMap<KeyType, ValueType>> GetEnumerator()
            {
                /*[Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]*/
                if (startKeyed)
                {
                    return new RobustEnumerator(tree, startKey, forward);
                }

                return new RobustEnumerator(tree, forward);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return this.GetEnumerator();
            }
        }

        public struct FastEnumerableSurrogate : IEnumerable<EntryRankMap<KeyType, ValueType>>
        {
            private readonly SplayTreeArrayRankMap<KeyType, ValueType> tree;
            private readonly bool forward;

            [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]
            private readonly bool startKeyed;
            [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]
            private readonly KeyType startKey;

            // Construction

            public FastEnumerableSurrogate(SplayTreeArrayRankMap<KeyType, ValueType> tree,bool forward)
            {
                this.tree = tree;
                this.forward = forward;

                this.startKeyed = false;
                this.startKey = default(KeyType);
            }

            [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]
            public FastEnumerableSurrogate(SplayTreeArrayRankMap<KeyType, ValueType> tree,KeyType startKey,bool forward)
            {
                this.tree = tree;
                this.forward = forward;

                this.startKeyed = true;
                this.startKey = startKey;
            }

            // IEnumerable

            public IEnumerator<EntryRankMap<KeyType, ValueType>> GetEnumerator()
            {
                /*[Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]*/
                if (startKeyed)
                {
                    return new FastEnumerator(tree, startKey, forward);
                }

                return new FastEnumerator(tree, forward);
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
        public class RobustEnumerator :
            IEnumerator<EntryRankMap<KeyType, ValueType>>,
            /*[Payload(Payload.Value)]*/ISetValue<ValueType>
        {
            private readonly SplayTreeArrayRankMap<KeyType, ValueType> tree;
            private readonly bool forward;

            [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]
            private readonly bool startKeyed;
            [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]
            private readonly KeyType startKey;

            private bool started;
            private bool valid;
            private uint enumeratorVersion;

            [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]
            private KeyType currentKey;

            public RobustEnumerator(SplayTreeArrayRankMap<KeyType, ValueType> tree,bool forward)
            {
                this.tree = tree;
                this.forward = forward;

                Reset();
            }

            [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]
            public RobustEnumerator(SplayTreeArrayRankMap<KeyType, ValueType> tree,KeyType startKey,bool forward)
            {
                this.tree = tree;
                this.forward = forward;

                this.startKeyed = true;
                this.startKey = startKey;

                Reset();
            }

            public EntryRankMap<KeyType, ValueType> Current
            {
                get
                {

                    if (valid)
                        /*[Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]*/
                        {
                            KeyType key = currentKey;
                            ValueType value = default(ValueType);
                            /*[Widen]*/
                            int rank = 0;
                            // OR
                            /*[Feature(Feature.Rank, Feature.RankMulti)]*/
                            tree.Get(
                                /*[Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]*/currentKey,
                                /*[Payload(Payload.Value)]*/out value,
                                /*[Feature(Feature.Rank, Feature.RankMulti)]*/out rank);

                            return new EntryRankMap<KeyType, ValueType>(
                                /*[Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]*/key,
                                /*[Payload(Payload.Value)]*/value,
                                /*[Payload(Payload.Value)]*/this,
                                /*[Payload(Payload.Value)]*/this.enumeratorVersion,
                                /*[Feature(Feature.Rank, Feature.RankMulti)]*/rank);
                        }
                    return new EntryRankMap<KeyType, ValueType>();
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

                this.enumeratorVersion = unchecked(this.enumeratorVersion + 1);

                if (!started)
                {
                    /*[Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]*/
                    if (!startKeyed)
                    {
                        if (forward)
                        {
                            valid = tree.Least(out currentKey);
                        }
                        else
                        {
                            valid = tree.Greatest(out currentKey);
                        }
                    }
                    else
                    {
                        if (forward)
                        {
                            valid = tree.NearestGreaterOrEqual(startKey, out currentKey);
                        }
                        else
                        {
                            valid = tree.NearestLessOrEqual(startKey, out currentKey);
                        }
                    }

                    started = true;
                }
                else if (valid)
                {
                    /*[Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]*/
                    if (forward)
                    {
                        valid = tree.NearestGreater(currentKey, out currentKey);
                    }
                    else
                    {
                        valid = tree.NearestLess(currentKey, out currentKey);
                    }
                }

                return valid;
            }

            public void Reset()
            {
                started = false;
                valid = false;
                this.enumeratorVersion = unchecked(this.enumeratorVersion + 1);
            }

            [Payload(Payload.Value)]
            public void SetValue(ValueType value,uint requiredEnumeratorVersion)
            {
                if (this.enumeratorVersion != requiredEnumeratorVersion)
                {
                    throw new InvalidOperationException();
                }

                // TODO: improve this to O(1) by using internal query methods above that expose the node and updating
                // the node directly

                /*[Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]*/
                tree.SetValue(currentKey, value);
            }
        }

        /// <summary>
        /// This enumerator is fast because it uses an in-order traversal of the tree that has O(1) cost per element.
        /// However, any change to the tree invalidates it, and that *includes queries* since a query causes a splay
        /// operation that changes the structure of the tree.
        /// Worse, this enumerator also uses a stack that can be as deep as the tree, and since the depth of a splay
        /// tree is in the worst case n (number of nodes), the stack can potentially be size n.
        /// </summary>
        public class FastEnumerator :
            IEnumerator<EntryRankMap<KeyType, ValueType>>,
            /*[Payload(Payload.Value)]*/ISetValue<ValueType>
        {
            private readonly SplayTreeArrayRankMap<KeyType, ValueType> tree;
            private readonly bool forward;

            private readonly bool startKeyedOrIndexed;
            //
            [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]
            private readonly KeyType startKey;

            private uint treeVersion;
            private uint enumeratorVersion;

            private NodeRef currentNode;
            private NodeRef leadingNode;

            [Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]
            [Widen]
            private int currentXStart, nextXStart;

            private readonly Stack<STuple<NodeRef, /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*//*[Widen]*/int>> stack
                = new Stack<STuple<NodeRef, /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*//*[Widen]*/int>>();

            public FastEnumerator(SplayTreeArrayRankMap<KeyType, ValueType> tree,bool forward)
            {
                this.tree = tree;
                this.forward = forward;

                Reset();
            }

            [Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]
            public FastEnumerator(SplayTreeArrayRankMap<KeyType, ValueType> tree,KeyType startKey,bool forward)
            {
                this.tree = tree;
                this.forward = forward;

                this.startKeyedOrIndexed = true;
                this.startKey = startKey;

                Reset();
            }

            public EntryRankMap<KeyType, ValueType> Current
            {
                get
                {
                    if (currentNode != tree.Nil)
                    {
                        /*[Feature(Feature.Rank)]*/
                        Debug.Assert((forward && (nextXStart - currentXStart == 1))
                            || (!forward && ((nextXStart - currentXStart == -1) || ((currentXStart == 0) && (nextXStart == 0)))));

                        return new EntryRankMap<KeyType, ValueType>(
                            /*[Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]*/
                            tree.nodes[currentNode].key,
                            /*[Payload(Payload.Value)]*/tree.nodes[currentNode].value,
                            /*[Payload(Payload.Value)]*/this,
                            /*[Payload(Payload.Value)]*/this.enumeratorVersion,
                            /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*/currentXStart);
                    }
                    return new EntryRankMap<KeyType, ValueType>();
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
                unchecked
                {
                    stack.Clear();

                    currentNode = tree.Nil;
                    leadingNode = tree.Nil;

                    this.treeVersion = tree.version;

                    // push search path to starting item

                    NodeRef node = tree.root;
                    /*[Widen]*/
                    int xPosition = 0;
                    while (node != tree.Nil)
                    {
                        xPosition += tree.nodes[node].xOffset;

                        int c;
                        {
                            if (!startKeyedOrIndexed)
                            {
                                c = forward ? -1 : 1;
                            }
                            else
                            {
                                /*[Feature(Feature.Dict, Feature.Rank, Feature.RankMulti)]*/
                                c = tree.comparer.Compare(startKey, tree.nodes[node].key);
                            }
                        }

                        if ((forward && (c <= 0)) || (!forward && (c >= 0)))
                        {
                            stack.Push(new STuple<NodeRef, /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*//*[Widen]*/int>(
                                node,
                                /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*/xPosition));
                        }

                        if (c == 0)
                        {

                            // successor not needed for forward traversal
                            if (forward)
                            {
                                break;
                            }
                            // successor not needed for case where xLength always == 1
                            /*[Feature(Feature.Dict, Feature.Rank)]*/
                            break;
                        }

                        if (c < 0)
                        {

                            node = tree.nodes[node].left;
                        }
                        else
                        {
                            Debug.Assert(c >= 0);
                            node = tree.nodes[node].right;
                        }
                    }

                    Advance();
                }
            }

            private void Advance()
            {
                unchecked
                {
                    if (this.treeVersion != tree.version)
                    {
                        throw new InvalidOperationException();
                    }

                    this.enumeratorVersion = unchecked(this.enumeratorVersion + 1);
                    currentNode = leadingNode;
                    currentXStart = nextXStart;

                    leadingNode = tree.Nil;

                    if (stack.Count == 0)
                    {
                        if (forward)
                        {
                            nextXStart = tree.xExtent;
                        }
                        else
                        {
                            nextXStart = 0;
                        }
                        return;
                    }

                    STuple<NodeRef, /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*//*[Widen]*/int> cursor
                        = stack.Pop();

                    leadingNode = cursor.Item1;
                    nextXStart = cursor.Item2;

                    NodeRef node = forward ? tree.nodes[leadingNode].right : tree.nodes[leadingNode].left;
                    /*[Widen]*/
                    int xPosition = nextXStart;
                    while (node != tree.Nil)
                    {
                        xPosition += tree.nodes[node].xOffset;

                        stack.Push(new STuple<NodeRef, /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*//*[Widen]*/int>(
                            node,
                            /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*/xPosition));
                        node = forward ? tree.nodes[node].left : tree.nodes[node].right;
                    }
                }
            }

            [Payload(Payload.Value)]
            public void SetValue(ValueType value,uint requiredEnumeratorVersion)
            {
                if ((this.enumeratorVersion != requiredEnumeratorVersion) || (this.treeVersion != tree.version))
                {
                    throw new InvalidOperationException();
                }

                tree.nodes[currentNode].value = value;
            }
        }


        //
        // Cloning
        //

        public object Clone()
        {
            return new SplayTreeArrayRankMap<KeyType, ValueType>(this);
        }
    }
}
