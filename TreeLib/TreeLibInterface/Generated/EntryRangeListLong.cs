// NOTE: This file is auto-generated. DO NOT MAKE CHANGES HERE! They will be overwritten on rebuild.

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
using System.Collections.Generic;

using TreeLib.Internal;

#pragma warning disable CS1591

namespace TreeLib
{
    /// <summary>
    /// A type defining the struct returned for each item in a tree by an enumerator. The struct contains properties
    /// for all relevant per-item data, including one or more of key, value, rank/count, and/or range start/length, as
    /// appropriate for the type of collection.
    /// </summary>
    public struct EntryRangeListLong    {


        [Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]
        [Widen]
        private readonly long xStart;

        /// <summary>
        /// Returns the rank of an item in a rank collection, or the start of a range in a range collection
        /// (for range-to-range mapping, returns the X side start)
        /// </summary>
        [Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)]
        [Widen]
        public long Start { get { return xStart; } }


        [Feature(Feature.RankMulti, Feature.Range, Feature.Range2)]
        [Widen]
        private readonly long xLength;

        /// <summary>
        /// Returns the count of an item in a multi-rank collection, or the length of a range in a range collection
        /// (for range-to-range mapping, returns the X side length)
        /// </summary>
        [Feature(Feature.RankMulti, Feature.Range, Feature.Range2)]
        [Widen]
        public long Length { get { return xLength; } }


        public EntryRangeListLong(
            [Feature(Feature.Rank, Feature.RankMulti, Feature.Range, Feature.Range2)][Widen] long xStart,
            [Feature(Feature.RankMulti, Feature.Range, Feature.Range2)][Widen] long xLength)
        {
            this.xStart = xStart;
            this.xLength = xLength;
        }

        public override bool Equals(object obj)
        {
            EntryRangeListLong other
                = (EntryRangeListLong)obj;

            bool xStartEqual = true;
            xStartEqual = this.xStart == other.xStart;
            if (!xStartEqual)
            {
                return false;
            }

            bool xLengthEqual = true;
            xLengthEqual = this.xLength == other.xLength;
            if (!xLengthEqual)
            {
                return false;
            }

            return true;
        }

        public override int GetHashCode()
        {
            // need a reasonable initial value
            int hashCode = 0L.GetHashCode();
            // implementation derived from Roslyn compiler implementation for anonymous types:
            // Microsoft.CodeAnalysis.CSharp.Symbols.AnonymousTypeManager.AnonymousTypeGetHashCodeMethodSymbol 
            const int HASH_FACTOR = -1521134295;
            unchecked
            {
                hashCode = hashCode * HASH_FACTOR + this.xStart.GetHashCode();
                hashCode = hashCode * HASH_FACTOR + this.xLength.GetHashCode();
            }
            return hashCode;
        }


        public override string ToString()
        {
            List<string> fields = new List<string>();
            fields.Add(Convert.ToString(xStart));
            fields.Add(Convert.ToString(xLength));
            return String.Join(", ", fields.ToArray());
        }
    }
}
