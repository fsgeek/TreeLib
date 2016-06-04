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
using System.Collections.Generic;
using System.Diagnostics;

using TreeLib;
using TreeLib.Internal;

namespace TreeLibTest
{
    public class StochasticTestRange2Map : UnitTestRange2Map
    {
        public StochasticTestRange2Map(long[] breakIterations, long startIteration)
            : base(breakIterations, startIteration)
        {
        }


        private void Validate<ValueType>(IRange2Map<ValueType>[] collections) where ValueType : IComparable<ValueType>
        {
            uint count = UInt32.MaxValue;
            Range2MapEntry[] items = null;
            for (int i = 0; i < collections.Length; i++)
            {
                Range2MapEntry[] items1 = null;

                INonInvasiveRange2MapInspection treeInspector;
                if ((items1 == null) && (treeInspector = collections[i] as INonInvasiveRange2MapInspection) != null)
                {
                    items1 = treeInspector.GetRanges();
                    try
                    {
                        treeInspector.Validate();
                        //WriteLine(TreeInspection.Dump(treeInspector));
                    }
                    catch (Exception exception)
                    {
                        Fault(collections[i], "validate", exception);
                    }
                }

                uint count1 = collections[i].Count;

                if (items == null)
                {
                    count = count1;
                    items = items1;
                    ValidateRanges(items);
                }
                else
                {
                    if (count != count1)
                    {
                        Fault(collections[i], "count");
                    }
                    if (items.Length != items1.Length)
                    {
                        Fault(collections[i], "length");
                    }
                    ValidateRanges(items1);
                    ValidateRangesEqual<ValueType>(items, items1);
                }
            }
        }


        private int Start(Range2MapEntry entry, Side side)
        {
            return side == Side.X ? entry.x.start : entry.y.start;
        }

        private int Length(Range2MapEntry entry, Side side)
        {
            return side == Side.X ? entry.x.length : entry.y.length;
        }


        private void ContainsAction(IRange2Map<float>[] collections, Random rnd, ref string description)
        {
            Range2MapEntry[] items = ((INonInvasiveRange2MapInspection)collections[0]).GetRanges();
            Side side = rnd.Next(2) == 0 ? Side.X : Side.Y;
            int extent = items.Length != 0 ? (Start(items[items.Length - 1], side) + Length(items[items.Length - 1], side)) : 0;
            if (rnd.Next(2) == 0)
            {
                // existing
                if (items.Length != 0)
                {
                    int start = Start(items[rnd.Next(items.Length)], side);
                    description = String.Format("Contains (existing) [{0}]", start);
                    for (int i = 0; i < collections.Length; i++)
                    {
                        try
                        {
                            bool f = collections[i].Contains(start, side);
                            if (!f)
                            {
                                Fault(collections[i], description + " - returned false");
                            }
                        }
                        catch (Exception exception)
                        {
                            Fault(collections[i], description + " - threw exception", exception);
                        }
                    }
                }
            }
            else
            {
                // missing
                int start;
                do
                {
                    start = rnd.Next(Math.Min(-1, -extent / 40), extent + Math.Max(1 + 1/*upper bound is exclusive*/, extent / 40));
                }
                while (Array.FindIndex(items, delegate (Range2MapEntry candidate) { return Start(candidate, side) == start; }) >= 0);
                description = String.Format("Contains (missing) [{0}]", start);
                for (int i = 0; i < collections.Length; i++)
                {
                    try
                    {
                        bool f = collections[i].Contains(start, side);
                        if (f)
                        {
                            Fault(collections[i], description + " - returned true");
                        }
                    }
                    catch (Exception exception)
                    {
                        Fault(collections[i], description + " - threw exception", exception);
                    }
                }
            }
        }

        private void TryInsertAction(IRange2Map<float>[] collections, Random rnd, ref string description)
        {
            Range2MapEntry[] items = ((INonInvasiveRange2MapInspection)collections[0]).GetRanges();
            Side side = rnd.Next(2) == 0 ? Side.X : Side.Y;
            int extent = items.Length != 0 ? (Start(items[items.Length - 1], side) + Length(items[items.Length - 1], side)) : 0;
            int xLength = rnd.Next(-1, 100);
            int yLength = rnd.Next(-1, 100);
            bool overflow = false;
            if (rnd.Next(100) == 0)
            {
                if ((rnd.Next(2) == 0) && (collections[0].GetExtent(Side.X) > 0))
                {
                    xLength = Int32.MaxValue;
                    overflow = true;
                }
                else if (collections[0].GetExtent(Side.Y) > 0)
                {
                    yLength = Int32.MaxValue;
                    overflow = true;
                }
            }
            float value = (float)rnd.NextDouble();
            if (rnd.Next(2) == 0)
            {
                // insert valid
                int index = rnd.Next(items.Length + 1);
                int start = index < items.Length ? Start(items[index], side) : extent;
                description = String.Format("TryInsert (valid) [{0}:<{1},{2}>, {3}]", start, xLength, yLength, value);
                for (int i = 0; i < collections.Length; i++)
                {
                    try
                    {
                        bool f = collections[i].TryInsert(start, side, xLength, yLength, value);
                        if (!f)
                        {
                            Fault(collections[i], description + " - returned false");
                        }
                    }
                    catch (ArgumentOutOfRangeException) when ((xLength <= 0) || (yLength <= 0))
                    {
                        // expected
                    }
                    catch (OverflowException) when (overflow)
                    {
                        // expected
                    }
                    catch (Exception exception)
                    {
                        Fault(collections[i], description + " - threw exception", exception);
                    }
                }
            }
            else
            {
                // insert invalid
                int start;
                do
                {
                    start = rnd.Next(Math.Min(-1, -extent / 40), extent + Math.Max(1 + 1/*upper bound is exclusive*/, extent / 40));
                }
                while ((start == 0) || (start == extent) || (Array.FindIndex(items, delegate (Range2MapEntry candidate) { return Start(candidate, side) == start; }) >= 0));
                description = String.Format("TryInsert (invalid) [{0}:<{1},{2}>, {3}]", start, xLength, yLength, value);
                for (int i = 0; i < collections.Length; i++)
                {
                    try
                    {
                        bool f = collections[i].TryInsert(start, side, xLength, yLength, value);
                        if (f)
                        {
                            Fault(collections[i], description + " - returned true");
                        }
                    }
                    catch (ArgumentOutOfRangeException) when ((xLength <= 0) || (yLength <= 0) || (start < 0))
                    {
                        // expected
                    }
                    catch (Exception exception)
                    {
                        Fault(collections[i], description + " - threw exception", exception);
                    }
                }
            }
        }

        private void TryDeleteAction(IRange2Map<float>[] collections, Random rnd, ref string description)
        {
            Range2MapEntry[] items = ((INonInvasiveRange2MapInspection)collections[0]).GetRanges();
            Side side = rnd.Next(2) == 0 ? Side.X : Side.Y;
            int extent = items.Length != 0 ? (Start(items[items.Length - 1], side) + Length(items[items.Length - 1], side)) : 0;
            if (rnd.Next(2) == 0)
            {
                // delete valid
                if (items.Length != 0)
                {
                    int start = Start(items[rnd.Next(items.Length)], side);
                    description = String.Format("TryDelete (valid) [{0}]", start);
                    for (int i = 0; i < collections.Length; i++)
                    {
                        try
                        {
                            bool f = collections[i].TryDelete(start, side);
                            if (!f)
                            {
                                Fault(collections[i], description + " - returned false");
                            }
                        }
                        catch (Exception exception)
                        {
                            Fault(collections[i], description + " - threw exception", exception);
                        }
                    }
                }
            }
            else
            {
                // delete invalid
                int start;
                do
                {
                    start = rnd.Next(Math.Min(-1, -extent / 40), extent + Math.Max(1 + 1/*upper bound is exclusive*/, extent / 40));
                }
                while (Array.FindIndex(items, delegate (Range2MapEntry candidate) { return Start(candidate, side) == start; }) >= 0);
                description = String.Format("TryDelete (invalid) [{0}]", start);
                for (int i = 0; i < collections.Length; i++)
                {
                    try
                    {
                        bool f = collections[i].TryDelete(start, side);
                        if (f)
                        {
                            Fault(collections[i], description + " - returned true");
                        }
                    }
                    catch (Exception exception)
                    {
                        Fault(collections[i], description + " - threw exception", exception);
                    }
                }
            }
        }

        private void TryGetLengthAction(IRange2Map<float>[] collections, Random rnd, ref string description)
        {
            Range2MapEntry[] items = ((INonInvasiveRange2MapInspection)collections[0]).GetRanges();
            Side side = rnd.Next(2) == 0 ? Side.X : Side.Y;
            int extent = items.Length != 0 ? (Start(items[items.Length - 1], side) + Length(items[items.Length - 1], side)) : 0;
            if (rnd.Next(2) == 0)
            {
                // existing
                if (items.Length != 0)
                {
                    int index = rnd.Next(items.Length);
                    int start = Start(items[index], side);
                    description = String.Format("TryGetLength (existing) [{0}]", start);
                    for (int i = 0; i < collections.Length; i++)
                    {
                        try
                        {
                            int length;
                            bool f = collections[i].TryGetLength(start, side, out length);
                            if (!f)
                            {
                                Fault(collections[i], description + " - returned false");
                            }
                            if (length != Length(items[index], side))
                            {
                                Fault(collections[i], description + " - wrong result");
                            }
                        }
                        catch (Exception exception)
                        {
                            Fault(collections[i], description + " - threw exception", exception);
                        }
                    }
                }
            }
            else
            {
                // missing
                int start;
                do
                {
                    start = rnd.Next(Math.Min(-1, -extent / 40), extent + Math.Max(1 + 1/*upper bound is exclusive*/, extent / 40));
                }
                while (Array.FindIndex(items, delegate (Range2MapEntry candidate) { return Start(candidate, side) == start; }) >= 0);
                description = String.Format("TryGetLength (missing) [{0}]", start);
                for (int i = 0; i < collections.Length; i++)
                {
                    try
                    {
                        int length;
                        bool f = collections[i].TryGetLength(start, side, out length);
                        if (f)
                        {
                            Fault(collections[i], description + " - returned true");
                        }
                        if (length != 0)
                        {
                            Fault(collections[i], description + " - wrong result");
                        }
                    }
                    catch (Exception exception)
                    {
                        Fault(collections[i], description + " - threw exception", exception);
                    }
                }
            }
        }

        private void TrySetLengthAction(IRange2Map<float>[] collections, Random rnd, ref string description)
        {
            Range2MapEntry[] items = ((INonInvasiveRange2MapInspection)collections[0]).GetRanges();
            Side side = rnd.Next(2) == 0 ? Side.X : Side.Y;
            int extent = items.Length != 0 ? (Start(items[items.Length - 1], side) + Length(items[items.Length - 1], side)) : 0;
            int length = rnd.Next(-1, 100);
            bool overflow = false;
            if (rnd.Next(2) == 0)
            {
                // set valid
                if (items.Length != 0)
                {
                    int index = rnd.Next(items.Length);
                    int start = Start(items[index], side);
                    if ((rnd.Next(100) == 0) && (collections[0].GetExtent(side) - (side == Side.X ? items[index].x.length : items[index].y.length) > 0))
                    {
                        length = Int32.MaxValue;
                        overflow = true;
                    }
                    description = String.Format("TrySetLength (valid) [{0}:<{1}>]", start, length);
                    for (int i = 0; i < collections.Length; i++)
                    {
                        try
                        {
                            bool f = collections[i].TrySetLength(start, side, length);
                            if (!f)
                            {
                                Fault(collections[i], description + " - returned false");
                            }
                        }
                        catch (ArgumentOutOfRangeException) when (length <= 0)
                        {
                            // expected
                        }
                        catch (OverflowException) when (overflow)
                        {
                            // expected
                        }
                        catch (Exception exception)
                        {
                            Fault(collections[i], description + " - threw exception", exception);
                        }
                    }
                }
            }
            else
            {
                // set invalid
                int start;
                do
                {
                    start = rnd.Next(Math.Min(-1, -extent / 40), extent + Math.Max(1 + 1/*upper bound is exclusive*/, extent / 40));
                }
                while ((start == 0) || (start == extent) || (Array.FindIndex(items, delegate (Range2MapEntry candidate) { return Start(candidate, side) == start; }) >= 0));
                if (rnd.Next(100) == 0)
                {
                    length = Int32.MaxValue;
                    overflow = true;
                }
                description = String.Format("TrySetLength (invalid) [{0}:<{1}>]", start, length);
                for (int i = 0; i < collections.Length; i++)
                {
                    try
                    {
                        bool f = collections[i].TrySetLength(start, side, length);
                        if (f)
                        {
                            Fault(collections[i], description + " - returned true");
                        }
                    }
                    catch (ArgumentOutOfRangeException) when (length <= 0)
                    {
                        // expected
                    }
                    catch (Exception exception)
                    {
                        Fault(collections[i], description + " - threw exception", exception);
                    }
                }
            }
        }

        private void TryGetValueAction(IRange2Map<float>[] collections, Random rnd, ref string description)
        {
            Range2MapEntry[] items = ((INonInvasiveRange2MapInspection)collections[0]).GetRanges();
            Side side = rnd.Next(2) == 0 ? Side.X : Side.Y;
            int extent = items.Length != 0 ? (Start(items[items.Length - 1], side) + Length(items[items.Length - 1], side)) : 0;
            if (rnd.Next(2) == 0)
            {
                // existing
                if (items.Length != 0)
                {
                    int index = rnd.Next(items.Length);
                    int start = Start(items[index], side);
                    description = String.Format("TryGetValue (existing) [{0}]", start);
                    for (int i = 0; i < collections.Length; i++)
                    {
                        try
                        {
                            float value;
                            bool f = collections[i].TryGetValue(start, side, out value);
                            if (!f)
                            {
                                Fault(collections[i], description + " - returned false");
                            }
                            if (value != (float)items[index].value)
                            {
                                Fault(collections[i], description + " - wrong result");
                            }
                        }
                        catch (Exception exception)
                        {
                            Fault(collections[i], description + " - threw exception", exception);
                        }
                    }
                }
            }
            else
            {
                // missing
                int start;
                do
                {
                    start = rnd.Next(Math.Min(-1, -extent / 40), extent + Math.Max(1 + 1/*upper bound is exclusive*/, extent / 40));
                }
                while (Array.FindIndex(items, delegate (Range2MapEntry candidate) { return Start(candidate, side) == start; }) >= 0);
                description = String.Format("TryGetValue (missing) [{0}]", start);
                for (int i = 0; i < collections.Length; i++)
                {
                    try
                    {
                        float value;
                        bool f = collections[i].TryGetValue(start, side, out value);
                        if (f)
                        {
                            Fault(collections[i], description + " - returned true");
                        }
                        if (value != 0)
                        {
                            Fault(collections[i], description + " - wrong result");
                        }
                    }
                    catch (Exception exception)
                    {
                        Fault(collections[i], description + " - threw exception", exception);
                    }
                }
            }
        }

        private void TrySetValueAction(IRange2Map<float>[] collections, Random rnd, ref string description)
        {
            Range2MapEntry[] items = ((INonInvasiveRange2MapInspection)collections[0]).GetRanges();
            Side side = rnd.Next(2) == 0 ? Side.X : Side.Y;
            int extent = items.Length != 0 ? (Start(items[items.Length - 1], side) + Length(items[items.Length - 1], side)) : 0;
            float value = (float)rnd.NextDouble();
            if (rnd.Next(2) == 0)
            {
                // set valid
                if (items.Length != 0)
                {
                    int index = rnd.Next(items.Length);
                    int start = Start(items[index], side);
                    description = String.Format("TrySetValue (valid) [{0}:<{1}>]", start, value);
                    for (int i = 0; i < collections.Length; i++)
                    {
                        try
                        {
                            bool f = collections[i].TrySetValue(start, side, value);
                            if (!f)
                            {
                                Fault(collections[i], description + " - returned false");
                            }
                        }
                        catch (Exception exception)
                        {
                            Fault(collections[i], description + " - threw exception", exception);
                        }
                    }
                }
            }
            else
            {
                // set invalid
                int start;
                do
                {
                    start = rnd.Next(Math.Min(-1, -extent / 40), extent + Math.Max(1 + 1/*upper bound is exclusive*/, extent / 40));
                }
                while ((start == 0) || (start == extent) || (Array.FindIndex(items, delegate (Range2MapEntry candidate) { return Start(candidate, side) == start; }) >= 0));
                description = String.Format("TrySetValue (invalid) [{0}:<{1}>]", start, value);
                for (int i = 0; i < collections.Length; i++)
                {
                    try
                    {
                        bool f = collections[i].TrySetValue(start, side, value);
                        if (f)
                        {
                            Fault(collections[i], description + " - returned true");
                        }
                    }
                    catch (Exception exception)
                    {
                        Fault(collections[i], description + " - threw exception", exception);
                    }
                }
            }
        }

        private void TryGetAction(IRange2Map<float>[] collections, Random rnd, ref string description)
        {
            Range2MapEntry[] items = ((INonInvasiveRange2MapInspection)collections[0]).GetRanges();
            Side side = rnd.Next(2) == 0 ? Side.X : Side.Y;
            int extent = items.Length != 0 ? (Start(items[items.Length - 1], side) + Length(items[items.Length - 1], side)) : 0;
            if (rnd.Next(2) == 0)
            {
                // existing
                if (items.Length != 0)
                {
                    int index = rnd.Next(items.Length);
                    int start = Start(items[index], side);
                    description = String.Format("TryGet (existing) [{0}]", start);
                    for (int i = 0; i < collections.Length; i++)
                    {
                        try
                        {
                            int otherSide, xLength, yLength;
                            float value;
                            bool f = collections[i].TryGet(start, side, out otherSide, out xLength, out yLength, out value);
                            if (!f)
                            {
                                Fault(collections[i], description + " - returned false");
                            }
                            if (otherSide != Start(items[index], (Side)((int)side ^ 1)))
                            {
                                Fault(collections[i], description + " - wrong result");
                            }
                            if (xLength != items[index].x.length)
                            {
                                Fault(collections[i], description + " - wrong result");
                            }
                            if (yLength != items[index].y.length)
                            {
                                Fault(collections[i], description + " - wrong result");
                            }
                            if (value != (float)items[index].value)
                            {
                                Fault(collections[i], description + " - wrong result");
                            }
                        }
                        catch (Exception exception)
                        {
                            Fault(collections[i], description + " - threw exception", exception);
                        }
                    }
                }
            }
            else
            {
                // missing
                int start;
                do
                {
                    start = rnd.Next(Math.Min(-1, -extent / 40), extent + Math.Max(1 + 1/*upper bound is exclusive*/, extent / 40));
                }
                while (Array.FindIndex(items, delegate (Range2MapEntry candidate) { return Start(candidate, side) == start; }) >= 0);
                description = String.Format("TryGet (missing) [{0}]", start);
                for (int i = 0; i < collections.Length; i++)
                {
                    try
                    {
                        int otherSide, xLength, yLength;
                        float value;
                        bool f = collections[i].TryGet(start, side, out otherSide, out xLength, out yLength, out value);
                        if (f)
                        {
                            Fault(collections[i], description + " - returned true");
                        }
                        if (otherSide != 0)
                        {
                            Fault(collections[i], description + " - wrong result");
                        }
                        if (xLength != 0)
                        {
                            Fault(collections[i], description + " - wrong result");
                        }
                        if (yLength != 0)
                        {
                            Fault(collections[i], description + " - wrong result");
                        }
                        if (value != 0)
                        {
                            Fault(collections[i], description + " - wrong result");
                        }
                    }
                    catch (Exception exception)
                    {
                        Fault(collections[i], description + " - threw exception", exception);
                    }
                }
            }
        }

        private void InsertAction(IRange2Map<float>[] collections, Random rnd, ref string description)
        {
            Range2MapEntry[] items = ((INonInvasiveRange2MapInspection)collections[0]).GetRanges();
            Side side = rnd.Next(2) == 0 ? Side.X : Side.Y;
            int extent = items.Length != 0 ? (Start(items[items.Length - 1], side) + Length(items[items.Length - 1], side)) : 0;
            int xLength = rnd.Next(-1, 100);
            int yLength = rnd.Next(-1, 100);
            bool overflow = false;
            if (rnd.Next(100) == 0)
            {
                if ((rnd.Next(2) == 0) && (collections[0].GetExtent(Side.X) > 0))
                {
                    xLength = Int32.MaxValue;
                    overflow = true;
                }
                else if (collections[0].GetExtent(Side.Y) > 0)
                {
                    yLength = Int32.MaxValue;
                    overflow = true;
                }
            }
            float value = (float)rnd.NextDouble();
            if (rnd.Next(2) == 0)
            {
                // insert valid
                int index = rnd.Next(items.Length + 1);
                int start = index < items.Length ? Start(items[index], side) : extent;
                description = String.Format("Insert (valid) [{0}:<{1},{2}>, {3}]", start, xLength, yLength, value);
                for (int i = 0; i < collections.Length; i++)
                {
                    try
                    {
                        collections[i].Insert(start, side, xLength, yLength, value);
                    }
                    catch (ArgumentOutOfRangeException) when ((xLength <= 0) || (yLength <= 0))
                    {
                        // expected
                    }
                    catch (OverflowException) when (overflow)
                    {
                        // expected
                    }
                    catch (Exception exception)
                    {
                        Fault(collections[i], description + " - threw exception", exception);
                    }
                }
            }
            else
            {
                // insert invalid
                int start;
                do
                {
                    start = rnd.Next(Math.Min(-1, -extent / 40), extent + Math.Max(1 + 1/*upper bound is exclusive*/, extent / 40));
                }
                while ((start == 0) || (start == extent) || (Array.FindIndex(items, delegate (Range2MapEntry candidate) { return Start(candidate, side) == start; }) >= 0));
                description = String.Format("Insert (invalid) [{0}:<{1},{2}>, {3}]", start, xLength, yLength, value);
                for (int i = 0; i < collections.Length; i++)
                {
                    try
                    {
                        collections[i].Insert(start, side, xLength, yLength, value);
                        Fault(collections[i], description + " - did not throw exception");
                    }
                    catch (ArgumentOutOfRangeException) when ((xLength <= 0) || (yLength <= 0) || (start < 0))
                    {
                        // expected
                    }
                    catch (ArgumentException)
                    {
                        // expected
                    }
                    catch (Exception exception)
                    {
                        Fault(collections[i], description + " - threw exception", exception);
                    }
                }
            }
        }

        private void DeleteAction(IRange2Map<float>[] collections, Random rnd, ref string description)
        {
            Range2MapEntry[] items = ((INonInvasiveRange2MapInspection)collections[0]).GetRanges();
            Side side = rnd.Next(2) == 0 ? Side.X : Side.Y;
            int extent = items.Length != 0 ? (Start(items[items.Length - 1], side) + Length(items[items.Length - 1], side)) : 0;
            if (rnd.Next(2) == 0)
            {
                // delete valid
                if (items.Length != 0)
                {
                    int start = Start(items[rnd.Next(items.Length)], side);
                    description = String.Format("Delete (valid) [{0}]", start);
                    for (int i = 0; i < collections.Length; i++)
                    {
                        try
                        {
                            collections[i].Delete(start, side);
                        }
                        catch (Exception exception)
                        {
                            Fault(collections[i], description + " - threw exception", exception);
                        }
                    }
                }
            }
            else
            {
                // delete invalid
                int start;
                do
                {
                    start = rnd.Next(Math.Min(-1, -extent / 40), extent + Math.Max(1 + 1/*upper bound is exclusive*/, extent / 40));
                }
                while (Array.FindIndex(items, delegate (Range2MapEntry candidate) { return Start(candidate, side) == start; }) >= 0);
                description = String.Format("Delete (invalid) [{0}]", start);
                for (int i = 0; i < collections.Length; i++)
                {
                    try
                    {
                        collections[i].Delete(start, side);
                        Fault(collections[i], description + " - did not throw exception");
                    }
                    catch (ArgumentException)
                    {
                        // expected
                    }
                    catch (Exception exception)
                    {
                        Fault(collections[i], description + " - threw exception", exception);
                    }
                }
            }
        }

        private void GetLengthAction(IRange2Map<float>[] collections, Random rnd, ref string description)
        {
            Range2MapEntry[] items = ((INonInvasiveRange2MapInspection)collections[0]).GetRanges();
            Side side = rnd.Next(2) == 0 ? Side.X : Side.Y;
            int extent = items.Length != 0 ? (Start(items[items.Length - 1], side) + Length(items[items.Length - 1], side)) : 0;
            if (rnd.Next(2) == 0)
            {
                // existing
                if (items.Length != 0)
                {
                    int index = rnd.Next(items.Length);
                    int start = Start(items[index], side);
                    description = String.Format("GetLength (existing) [{0}]", start);
                    for (int i = 0; i < collections.Length; i++)
                    {
                        try
                        {
                            int length = collections[i].GetLength(start, side);
                            if (length != Length(items[index], side))
                            {
                                Fault(collections[i], description + " - wrong result");
                            }
                        }
                        catch (Exception exception)
                        {
                            Fault(collections[i], description + " - threw exception", exception);
                        }
                    }
                }
            }
            else
            {
                // missing
                int start;
                do
                {
                    start = rnd.Next(Math.Min(-1, -extent / 40), extent + Math.Max(1 + 1/*upper bound is exclusive*/, extent / 40));
                }
                while (Array.FindIndex(items, delegate (Range2MapEntry candidate) { return Start(candidate, side) == start; }) >= 0);
                description = String.Format("GetLength (missing) [{0}]", start);
                for (int i = 0; i < collections.Length; i++)
                {
                    try
                    {
                        int length = collections[i].GetLength(start, side);
                        Fault(collections[i], description + " - did not throw exception");
                    }
                    catch (ArgumentException)
                    {
                        // expected
                    }
                    catch (Exception exception)
                    {
                        Fault(collections[i], description + " - threw exception", exception);
                    }
                }
            }
        }

        private void SetLengthAction(IRange2Map<float>[] collections, Random rnd, ref string description)
        {
            Range2MapEntry[] items = ((INonInvasiveRange2MapInspection)collections[0]).GetRanges();
            Side side = rnd.Next(2) == 0 ? Side.X : Side.Y;
            int extent = items.Length != 0 ? (Start(items[items.Length - 1], side) + Length(items[items.Length - 1], side)) : 0;
            int length = rnd.Next(-1, 100);
            bool overflow = false;
            if (rnd.Next(2) == 0)
            {
                // set valid
                if (items.Length != 0)
                {
                    int index = rnd.Next(items.Length);
                    int start = Start(items[index], side);
                    if ((rnd.Next(100) == 0) && (collections[0].GetExtent(side) - (side == Side.X ? items[index].x.length : items[index].y.length) > 0))
                    {
                        length = Int32.MaxValue;
                        overflow = true;
                    }
                    description = String.Format("SetLength (valid) [{0}:<{1}>]", start, length);
                    for (int i = 0; i < collections.Length; i++)
                    {
                        try
                        {
                            collections[i].SetLength(start, side, length);
                        }
                        catch (ArgumentOutOfRangeException) when (length <= 0)
                        {
                            // expected
                        }
                        catch (OverflowException) when (overflow)
                        {
                            // expected
                        }
                        catch (Exception exception)
                        {
                            Fault(collections[i], description + " - threw exception", exception);
                        }
                    }
                }
            }
            else
            {
                // set invalid
                int start;
                do
                {
                    start = rnd.Next(Math.Min(-1, -extent / 40), extent + Math.Max(1 + 1/*upper bound is exclusive*/, extent / 40));
                }
                while ((start == 0) || (start == extent) || (Array.FindIndex(items, delegate (Range2MapEntry candidate) { return Start(candidate, side) == start; }) >= 0));
                if (rnd.Next(100) == 0)
                {
                    length = Int32.MaxValue;
                    overflow = true;
                }
                description = String.Format("SetLength (invalid) [{0}:<{1}>]", start, length);
                for (int i = 0; i < collections.Length; i++)
                {
                    try
                    {
                        collections[i].SetLength(start, side, length);
                        Fault(collections[i], description + " - did not throw exception");
                    }
                    catch (ArgumentOutOfRangeException) when (length <= 0)
                    {
                        // expected
                    }
                    catch (ArgumentException)
                    {
                        // expected
                    }
                    catch (Exception exception)
                    {
                        Fault(collections[i], description + " - threw exception", exception);
                    }
                }
            }
        }

        private void GetValueAction(IRange2Map<float>[] collections, Random rnd, ref string description)
        {
            Range2MapEntry[] items = ((INonInvasiveRange2MapInspection)collections[0]).GetRanges();
            Side side = rnd.Next(2) == 0 ? Side.X : Side.Y;
            int extent = items.Length != 0 ? (Start(items[items.Length - 1], side) + Length(items[items.Length - 1], side)) : 0;
            if (rnd.Next(2) == 0)
            {
                // existing
                if (items.Length != 0)
                {
                    int index = rnd.Next(items.Length);
                    int start = Start(items[index], side);
                    description = String.Format("GetValue (existing) [{0}]", start);
                    for (int i = 0; i < collections.Length; i++)
                    {
                        try
                        {
                            float value = collections[i].GetValue(start, side);
                            if (value != (float)items[index].value)
                            {
                                Fault(collections[i], description + " - wrong result");
                            }
                        }
                        catch (Exception exception)
                        {
                            Fault(collections[i], description + " - threw exception", exception);
                        }
                    }
                }
            }
            else
            {
                // missing
                int start;
                do
                {
                    start = rnd.Next(Math.Min(-1, -extent / 40), extent + Math.Max(1 + 1/*upper bound is exclusive*/, extent / 40));
                }
                while (Array.FindIndex(items, delegate (Range2MapEntry candidate) { return Start(candidate, side) == start; }) >= 0);
                description = String.Format("GetValue (missing) [{0}]", start);
                for (int i = 0; i < collections.Length; i++)
                {
                    try
                    {
                        float value = collections[i].GetValue(start, side);
                        Fault(collections[i], description + " - did not throw exception");
                    }
                    catch (ArgumentException)
                    {
                        // expected
                    }
                    catch (Exception exception)
                    {
                        Fault(collections[i], description + " - threw exception", exception);
                    }
                }
            }
        }

        private void SetValueAction(IRange2Map<float>[] collections, Random rnd, ref string description)
        {
            Range2MapEntry[] items = ((INonInvasiveRange2MapInspection)collections[0]).GetRanges();
            Side side = rnd.Next(2) == 0 ? Side.X : Side.Y;
            int extent = items.Length != 0 ? (Start(items[items.Length - 1], side) + Length(items[items.Length - 1], side)) : 0;
            float value = (float)rnd.NextDouble();
            if (rnd.Next(2) == 0)
            {
                // set valid
                if (items.Length != 0)
                {
                    int index = rnd.Next(items.Length);
                    int start = Start(items[index], side);
                    description = String.Format("SetValue (valid) [{0}:<{1}>]", start, value);
                    for (int i = 0; i < collections.Length; i++)
                    {
                        try
                        {
                            collections[i].SetValue(start, side, value);
                        }
                        catch (Exception exception)
                        {
                            Fault(collections[i], description + " - threw exception", exception);
                        }
                    }
                }
            }
            else
            {
                // set invalid
                int start;
                do
                {
                    start = rnd.Next(Math.Min(-1, -extent / 40), extent + Math.Max(1 + 1/*upper bound is exclusive*/, extent / 40));
                }
                while ((start == 0) || (start == extent) || (Array.FindIndex(items, delegate (Range2MapEntry candidate) { return Start(candidate, side) == start; }) >= 0));
                description = String.Format("SetValue (invalid) [{0}:<{1}>]", start, value);
                for (int i = 0; i < collections.Length; i++)
                {
                    try
                    {
                        collections[i].SetValue(start, side, value);
                        Fault(collections[i], description + " - did not throw exception");
                    }
                    catch (ArgumentException)
                    {
                        // expected
                    }
                    catch (Exception exception)
                    {
                        Fault(collections[i], description + " - threw exception", exception);
                    }
                }
            }
        }

        private void GetAction(IRange2Map<float>[] collections, Random rnd, ref string description)
        {
            Range2MapEntry[] items = ((INonInvasiveRange2MapInspection)collections[0]).GetRanges();
            Side side = rnd.Next(2) == 0 ? Side.X : Side.Y;
            int extent = items.Length != 0 ? (Start(items[items.Length - 1], side) + Length(items[items.Length - 1], side)) : 0;
            if (rnd.Next(2) == 0)
            {
                // existing
                if (items.Length != 0)
                {
                    int index = rnd.Next(items.Length);
                    int start = Start(items[index], side);
                    description = String.Format("Get (existing) [{0}]", start);
                    for (int i = 0; i < collections.Length; i++)
                    {
                        try
                        {
                            int otherSide, xLength, yLength;
                            float value;
                            collections[i].Get(start, side, out otherSide, out xLength, out yLength, out value);
                            if (otherSide != Start(items[index], (Side)((int)side ^ 1)))
                            {
                                Fault(collections[i], description + " - wrong result");
                            }
                            if (xLength != items[index].x.length)
                            {
                                Fault(collections[i], description + " - wrong result");
                            }
                            if (yLength != items[index].y.length)
                            {
                                Fault(collections[i], description + " - wrong result");
                            }
                            if (value != (float)items[index].value)
                            {
                                Fault(collections[i], description + " - wrong result");
                            }
                        }
                        catch (Exception exception)
                        {
                            Fault(collections[i], description + " - threw exception", exception);
                        }
                    }
                }
            }
            else
            {
                // missing
                int start;
                do
                {
                    start = rnd.Next(Math.Min(-1, -extent / 40), extent + Math.Max(1 + 1/*upper bound is exclusive*/, extent / 40));
                }
                while (Array.FindIndex(items, delegate (Range2MapEntry candidate) { return Start(candidate, side) == start; }) >= 0);
                description = String.Format("Get (missing) [{0}]", start);
                for (int i = 0; i < collections.Length; i++)
                {
                    try
                    {
                        int otherSide, xLength, yLength;
                        float value;
                        collections[i].Get(start, side, out otherSide, out xLength, out yLength, out value);
                        Fault(collections[i], description + " - did not throw exception");
                    }
                    catch (ArgumentException)
                    {
                        // expected
                    }
                    catch (Exception exception)
                    {
                        Fault(collections[i], description + " - threw exception", exception);
                    }
                }
            }
        }

        private void GetExtentAction(IRange2Map<float>[] collections, Random rnd, ref string description)
        {
            Range2MapEntry[] items = ((INonInvasiveRange2MapInspection)collections[0]).GetRanges();
            Side side = rnd.Next(2) == 0 ? Side.X : Side.Y;
            int extent = items.Length != 0 ? (Start(items[items.Length - 1], side) + Length(items[items.Length - 1], side)) : 0;
            description = String.Format("GetExtent");
            for (int i = 0; i < collections.Length; i++)
            {
                try
                {
                    int extent1 = collections[i].GetExtent(side);
                    if (extent1 != extent)
                    {
                        Fault(collections[i], description + " - wrong result");
                    }
                }
                catch (Exception exception)
                {
                    Fault(collections[i], description + " - threw exception", exception);
                }
            }
        }

        private delegate bool OrderingVariantMethod(IRange2Map<float> collection, int position, Side side, out int nearestStart);
        private void OrderingActionBase(IRange2Map<float>[] collections, Random rnd, ref string description, OrderingVariantMethod variantAction)
        {
            Range2MapEntry[] items = ((INonInvasiveRange2MapInspection)collections[0]).GetRanges();
            Side side = rnd.Next(2) == 0 ? Side.X : Side.Y;
            int extent = items.Length != 0 ? (Start(items[items.Length - 1], side) + Length(items[items.Length - 1], side)) : 0;
            int position;
            if ((rnd.Next(2) == 0) && (items.Length != 0))
            {
                // valid start
                int index = rnd.Next(items.Length);
                position = Start(items[index], side);
            }
            else
            {
                // non-start
                do
                {
                    position = rnd.Next(Math.Min(-1, -extent / 40), extent + Math.Max(1 + 1/*upper bound is exclusive*/, extent / 40));
                }
                while ((position == 0) || (position == extent) || (Array.FindIndex(items, delegate (Range2MapEntry candidate) { return Start(candidate, side) == position; }) >= 0));
            }

            // test only equivalence -- rely on reference implementation being correct (assumed demonstrated during unit tests)
            description = description + String.Format(" {0}", position);
            bool f = false;
            int nearest = 0;
            for (int i = 0; i < collections.Length; i++)
            {
                try
                {
                    int nearest1;
                    bool f1 = variantAction(collections[i], position, side, out nearest1);
                    if (i == 0)
                    {
                        f = f1;
                        nearest = nearest1;
                    }
                    else
                    {
                        if (f != f1)
                        {
                            Fault(collections[i], description + " - return code discrepancy");
                        }
                        if (nearest != nearest1)
                        {
                            Fault(collections[i], description + " - position discrepancy");
                        }
                    }
                }
                catch (Exception exception)
                {
                    Fault(collections[i], description + " - threw exception", exception);
                }
            }
        }

        private void NearestLessOrEqualAction(IRange2Map<float>[] collections, Random rnd, ref string description)
        {
            description = "NearestLessOrEqual";
            OrderingActionBase(collections, rnd, ref description,
                delegate (IRange2Map<float> collection, int position, Side side, out int nearestStart)
                { return collection.NearestLessOrEqual(position, side, out nearestStart); });
        }

        private void NearestLessAction(IRange2Map<float>[] collections, Random rnd, ref string description)
        {
            description = "NearestLess";
            OrderingActionBase(collections, rnd, ref description,
                delegate (IRange2Map<float> collection, int position, Side side, out int nearestStart)
                { return collection.NearestLess(position, side, out nearestStart); });
        }

        private void NearestGreaterOrEqualAction(IRange2Map<float>[] collections, Random rnd, ref string description)
        {
            description = "NearestGreaterOrEqual";
            OrderingActionBase(collections, rnd, ref description,
                delegate (IRange2Map<float> collection, int position, Side side, out int nearestStart)
                { return collection.NearestGreaterOrEqual(position, side, out nearestStart); });
        }

        private void NearestGreaterAction(IRange2Map<float>[] collections, Random rnd, ref string description)
        {
            description = "NearestGreater";
            OrderingActionBase(collections, rnd, ref description,
                delegate (IRange2Map<float> collection, int position, Side side, out int nearestStart)
                { return collection.NearestGreater(position, side, out nearestStart); });
        }


        public override bool Do(int seed, StochasticControls control)
        {
            IRange2Map<float>[] collections = new IRange2Map<float>[]
            {
                new ReferenceRange2Map< float>(), // must be first

                new SplayTreeRange2Map<float>(),
                new SplayTreeArrayRange2Map<float>(),
                new AdaptRange2MapToRange2MapLong<float>(new SplayTreeRange2MapLong<float>()),

                new RedBlackTreeRange2Map<float>(),
                new RedBlackTreeArrayRange2Map<float>(),
                new AdaptRange2MapToRange2MapLong<float>(new RedBlackTreeRange2MapLong<float>()),

                new AVLTreeRange2Map<float>(),
                new AVLTreeArrayRange2Map<float>(),
                new AdaptRange2MapToRange2MapLong<float>(new AVLTreeRange2MapLong<float>()),
            };

            Tuple<Tuple<int, int>, InvokeAction<IRange2Map<float>>>[] actions = new Tuple<Tuple<int, int>, InvokeAction<IRange2Map<float>>>[]
            {
                new Tuple<Tuple<int, int>, InvokeAction<IRange2Map<float>>>(new Tuple<int, int>(100     , 100     ), ContainsAction),

                new Tuple<Tuple<int, int>, InvokeAction<IRange2Map<float>>>(new Tuple<int, int>(300 - 20, 300 + 40), TryInsertAction),
                new Tuple<Tuple<int, int>, InvokeAction<IRange2Map<float>>>(new Tuple<int, int>(300     , 300     ), TryDeleteAction),
                new Tuple<Tuple<int, int>, InvokeAction<IRange2Map<float>>>(new Tuple<int, int>(100     , 100     ), TryGetLengthAction),
                new Tuple<Tuple<int, int>, InvokeAction<IRange2Map<float>>>(new Tuple<int, int>(100     , 100     ), TrySetLengthAction),
                new Tuple<Tuple<int, int>, InvokeAction<IRange2Map<float>>>(new Tuple<int, int>(100     , 100     ), TryGetValueAction),
                new Tuple<Tuple<int, int>, InvokeAction<IRange2Map<float>>>(new Tuple<int, int>(100     , 100     ), TrySetValueAction),
                new Tuple<Tuple<int, int>, InvokeAction<IRange2Map<float>>>(new Tuple<int, int>(100     , 100     ), TryGetAction),

                new Tuple<Tuple<int, int>, InvokeAction<IRange2Map<float>>>(new Tuple<int, int>(300 - 20, 300 + 40), InsertAction),
                new Tuple<Tuple<int, int>, InvokeAction<IRange2Map<float>>>(new Tuple<int, int>(300     , 300     ), DeleteAction),
                new Tuple<Tuple<int, int>, InvokeAction<IRange2Map<float>>>(new Tuple<int, int>(100     , 100     ), GetLengthAction),
                new Tuple<Tuple<int, int>, InvokeAction<IRange2Map<float>>>(new Tuple<int, int>(100     , 100     ), SetLengthAction),
                new Tuple<Tuple<int, int>, InvokeAction<IRange2Map<float>>>(new Tuple<int, int>(100     , 100     ), GetValueAction),
                new Tuple<Tuple<int, int>, InvokeAction<IRange2Map<float>>>(new Tuple<int, int>(100     , 100     ), SetValueAction),
                new Tuple<Tuple<int, int>, InvokeAction<IRange2Map<float>>>(new Tuple<int, int>(100     , 100     ), GetAction),

                new Tuple<Tuple<int, int>, InvokeAction<IRange2Map<float>>>(new Tuple<int, int>(100     , 100     ), GetExtentAction),

                new Tuple<Tuple<int, int>, InvokeAction<IRange2Map<float>>>(new Tuple<int, int>(100     , 100     ), NearestLessOrEqualAction),
                new Tuple<Tuple<int, int>, InvokeAction<IRange2Map<float>>>(new Tuple<int, int>(100     , 100     ), NearestLessAction),
                new Tuple<Tuple<int, int>, InvokeAction<IRange2Map<float>>>(new Tuple<int, int>(100     , 100     ), NearestGreaterOrEqualAction),
                new Tuple<Tuple<int, int>, InvokeAction<IRange2Map<float>>>(new Tuple<int, int>(100     , 100     ), NearestGreaterAction),
            };

            return StochasticDriver(
                "Range2 Map Stochastic Test",
                seed,
                control,
                collections,
                actions,
                delegate (IRange2Map<float>[] _collections) { Validate<float>(_collections); });
        }
    }
}