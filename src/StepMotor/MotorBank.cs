//     MIT License
//     
//     Copyright(c) 2018-2020 Ilia Kosenkov
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
#nullable enable

using System;

namespace StepMotor
{
    public readonly struct MotorBank
    {
        public static MotorBank DefaultStart { get; } = 1;
        public static MotorBank DefaultEnd { get; }= 16;

        public byte RawValue { get; }

        public MotorBank(byte motorOrBank) => RawValue = motorOrBank;

        public static implicit operator byte(MotorBank motorBank) => motorBank.RawValue;
        public static implicit operator MotorBank(byte value) => new MotorBank(value);

        public static explicit operator MotorBank(int value)
            => value >= 0 && value <= byte.MaxValue
                ? new MotorBank((byte) value)
                : throw new ArgumentOutOfRangeException(nameof(value));

    }
}
