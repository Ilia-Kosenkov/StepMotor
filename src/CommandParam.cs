//     MIT License
//     
//     Copyright(c) 2018-2019 Ilia Kosenkov
//     
//     Permission is hereby granted, free of charge, to any person obtaining a copy
//     of this software and associated documentation files (the "Software"), to deal
//     in the Software without restriction, including without limitation the rights
//     to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//     copies of the Software, and to permit persons to whom the Software is
//     furnished to do so, subject to the following conditions:
//     
//     The above copyright notice and this permission notice shall be included in all
//     copies or substantial portions of the Software.
//     
//     THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//     IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//     FITNESS FOR A PARTICULAR PURPOSE AND NONINFINGEMENT. IN NO EVENT SHALL THE
//     AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//     LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//     OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//     SOFTWARE.


// ReSharper disable UnusedMember.Global

using System;
using System.Collections.Immutable;
using System.Linq;

namespace StepMotor
{
    public sealed partial class CommandParam
    {
        public static ImmutableArray<AxisParameter> GeneralAxisParams { get; }
            = Enum.GetValues(typeof(AxisParameter))
                .OfType<AxisParameter>()
                .Take(15)
                .ToImmutableArray();

        public static ImmutableArray<AxisParameter> RotationAxisParams { get; }
            = GeneralAxisParams
                .Take(6)
                .ToImmutableArray();
        public static CommandParam Default { get; } = new CommandParam();

        public CalcType? Calculation { get; }
        public RefSearchType? RefSearch { get; }
        public MoveType? Move { get; }

        public AxisParameter? AxisParam { get; }

        public bool IsDefault =>
            Calculation is null
            && RefSearch is null
            && Move is null
            && AxisParam is null;

        public bool IsCalc => Calculation.HasValue;
        public bool IsRefSearchType => RefSearch.HasValue;
        public bool IsMoveType => Move.HasValue;
        public bool IsAxisParamType => AxisParam.HasValue;

        private CommandParam()
        {
        }

        public CommandParam(CalcType type) => Calculation = type;
        public CommandParam(RefSearchType type) => RefSearch = type;
        public CommandParam(MoveType type) => Move = type;
        public CommandParam(AxisParameter type) => AxisParam = type;


        public object GetValue()
        {
            if (Calculation.HasValue)
                return Calculation.Value;
            if (RefSearch.HasValue)
                return RefSearch.Value;
            if (AxisParam.HasValue)
                return AxisParam.Value;
            return Move;
        }

        public override int GetHashCode()
        {
            if (Calculation.HasValue)
                return Calculation.Value.GetHashCode();
            if (RefSearch.HasValue)
                return RefSearch.Value.GetHashCode();
            if (AxisParam.HasValue)
                return AxisParam.Value.GetHashCode();
            return Move?.GetHashCode() ?? 0;
        }

        public override bool Equals(object obj) =>
            obj is CommandParam other &&
            (other.Calculation == Calculation
             || other.Move == Move
             || other.RefSearch == RefSearch
             || other.AxisParam == AxisParam);

        public override string ToString()
        {
            if (Calculation.HasValue)
                return Calculation.Value.ToString();
            if (RefSearch.HasValue)
                return RefSearch.Value.ToString();
            if (AxisParam.HasValue)
                return AxisParam.Value.ToString();
            return Move?.ToString() ?? @"Default";
        }


        public static implicit operator byte(CommandParam @this)
        {
            // ReSharper disable PossibleInvalidOperationException
            if (@this.IsCalc)
                return (byte) @this.Calculation.Value;
            if (@this.IsRefSearchType)
                return (byte) @this.RefSearch.Value;
            if (@this.IsMoveType)
                return (byte) @this.Move.Value;
            if (@this.IsAxisParamType)
                return (byte)@this.AxisParam.Value;
            return 0;
            // ReSharper restore PossibleInvalidOperationException
        }

        public static implicit operator CommandParam(CalcType type) =>
            new CommandParam(type);
        public static implicit operator CommandParam(RefSearchType type) =>
            new CommandParam(type);
        public static implicit operator CommandParam(MoveType type) =>
            new CommandParam(type);
        public static implicit operator CommandParam(AxisParameter type) =>
            new CommandParam(type);
    }
}
