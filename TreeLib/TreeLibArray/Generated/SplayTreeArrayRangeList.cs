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

    public class SplayTreeArrayRangeList:
        /*[Feature(Feature.Range)]*//*[Payload(Payload.None)]*//*[Widen]*/IRangeList,

        INonInvasiveTreeInspection,
        /*[Feature(Feature.Range, Feature.Range2)]*//*[Widen]*/INonInvasiveRange2MapInspection,

        IEnumerable<EntryRangeList>,
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
        private NodeRef Nil { get { return SplayTreeArrayRangeList._Nil; } } // allow tree.Nil or this.Nil in all cases

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

        [Storage(Storage.Array)]
        public SplayTreeArrayRangeList(uint capacity,AllocationMode allocationMode)
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

        [Storage(Storage.Array)]
        public SplayTreeArrayRangeList(uint capacity)
            : this(capacity, AllocationMode.DynamicRetainFreelist)
        {
        }

        [Storage(Storage.Array)]
        public SplayTreeArrayRangeList()
            : this(0, AllocationMode.DynamicRetainFreelist)
        {
        }

        [Storage(Storage.Array)]
        public SplayTreeArrayRangeList(SplayTreeArrayRangeList original)
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

        public uint Count { get { return checked((uint)this.count); } }

        public long LongCount { get { return unchecked((long)this.count); } }

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

        [Feature(Feature.Range, Feature.Range2)]
        public bool TryInsert([Widen] int start,[Widen] int xLength)
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

        [Feature(Feature.Range, Feature.Range2)]
        public bool TryGet([Widen] int start,[Widen] out int xLength)
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
                        return true;
                    }
                }
                xLength = 0;
                return false;
            }
        }

        [Feature(Feature.Range, Feature.Range2)]
        public void Insert([Widen] int start,[Widen] int xLength)
        {
            if (!TryInsert(start, xLength))
            {
                throw new ArgumentException("item already in tree");
            }
        }

        [Feature(Feature.Range, Feature.Range2)]
        public void Delete([Widen] int start)
        {
            if (!TryDelete(start))
            {
                throw new ArgumentException("item not in tree");
            }
        }

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

        [Feature(Feature.Range, Feature.Range2)]
        public void SetLength([Widen] int start,[Widen] int length)
        {
            if (!TrySetLength(start, length))
            {
                throw new ArgumentException("item not in tree");
            }
        }

        [Feature(Feature.Range, Feature.Range2)]
        public void Get([Widen] int start,[Widen] out int xLength)
        {
            if (!TryGet(start, out xLength))
            {
                throw new ArgumentException("item not in tree");
            }
        }

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

        [Feature(Feature.Range, Feature.Range2)]
        public bool NearestLessOrEqual([Widen] int position,[Widen] out int nearestStart)
        {
            return NearestLess(position, out nearestStart, true/*orEqual*/);
        }

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

        [Feature(Feature.Range, Feature.Range2)]
        public bool NearestGreaterOrEqual([Widen] int position,[Widen] out int nearestStart)
        {
            return NearestGreater(position, out nearestStart, true/*orEqual*/);
        }

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
            object value = null;
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

        public IEnumerator<EntryRangeList> GetEnumerator()
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

        public RobustEnumerableSurrogate GetRobustEnumerable()
        {
            return new RobustEnumerableSurrogate(this);
        }

        public struct RobustEnumerableSurrogate : IEnumerable<EntryRangeList>
        {
            private readonly SplayTreeArrayRangeList tree;

            public RobustEnumerableSurrogate(SplayTreeArrayRangeList tree)
            {
                this.tree = tree;
            }

            public IEnumerator<EntryRangeList> GetEnumerator()
            {
                return new RobustEnumerator(tree);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return this.GetEnumerator();
            }
        }

        public FastEnumerableSurrogate GetFastEnumerable()
        {
            return new FastEnumerableSurrogate(this);
        }

        public struct FastEnumerableSurrogate : IEnumerable<EntryRangeList>
        {
            private readonly SplayTreeArrayRangeList tree;

            public FastEnumerableSurrogate(SplayTreeArrayRangeList tree)
            {
                this.tree = tree;
            }

            public IEnumerator<EntryRangeList> GetEnumerator()
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
        public class RobustEnumerator : IEnumerator<EntryRangeList>
        {
            private readonly SplayTreeArrayRangeList tree;
            private bool started;
            private bool valid;
            [Feature(Feature.Range, Feature.Range2)]
            [Widen]
            private int currentXStart;
            [Feature(Feature.Range, Feature.Range2)]
            private ushort version; // saving the currentXStart does not work well range collections

            public RobustEnumerator(SplayTreeArrayRangeList tree)
            {
                this.tree = tree;
                Reset();
            }

            public EntryRangeList Current
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
                            /*[Widen]*/
                            int xStart = 0, xLength = 0;
                            xStart = currentXStart;

                            tree.Get(
                                /*[Feature(Feature.Range, Feature.Range2)]*/xStart,
                                /*[Feature(Feature.Range, Feature.Range2)]*/out xLength);

                            /*[Feature(Feature.Range, Feature.Range2)]*/
                            version = tree.version; // our query is ok

                            return new EntryRangeList(
                                /*[Feature(Feature.Range, Feature.Range2)]*/xStart,
                                /*[Feature(Feature.Range, Feature.Range2)]*/xLength);
                        }
                    }
                    return new EntryRangeList();
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
        public class FastEnumerator : IEnumerator<EntryRangeList>
        {
            private readonly SplayTreeArrayRangeList tree;
            private ushort version;
            private NodeRef currentNode;
            private NodeRef nextNode;
            [Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]
            [Widen]
            private int currentXStart, nextXStart;

            private readonly Stack<STuple<NodeRef, /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*//*[Widen]*/int>> stack
                = new Stack<STuple<NodeRef, /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*//*[Widen]*/int>>();

            public FastEnumerator(SplayTreeArrayRangeList tree)
            {
                this.tree = tree;
                Reset();
            }

            public EntryRangeList Current
            {
                get
                {
                    if (currentNode != tree.Nil)
                    {

                        return new EntryRangeList(
                            /*[Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]*/currentXStart,
                            /*[Feature(Feature.RankMulti, Feature.Range, Feature.Range2)]*/nextXStart - currentXStart);
                    }
                    return new EntryRangeList();
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
