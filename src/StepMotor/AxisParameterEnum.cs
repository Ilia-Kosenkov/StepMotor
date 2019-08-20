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

using System.Runtime.Serialization;
// ReSharper disable UnusedMember.Global

namespace StepMotor
{
    public sealed partial class CommandParam
    {
        [DataContract]
        public enum AxisParameter : byte
        {
            [EnumMember] TargetPosition = 0,
            [EnumMember] ActualPosition = 1,
            [EnumMember] TargetSpeed = 2,
            [EnumMember] ActualSpeed = 3,
            [EnumMember] MaximumSpeed = 4,
            [EnumMember] MaximumAcceleration = 5,
            [EnumMember] AbsoluteMaximumCurrent = 6,
            [EnumMember] StandbyCurrent = 7,
            [EnumMember] TargetPositionReached = 8,
            [EnumMember] ReferenceSwitchStatus = 9,
            [EnumMember] RightLimitSwitchStatus = 10,
            [EnumMember] LeftLimitSwitchStatus = 11,
            [EnumMember] RightLimitSwitchDisable = 12,
            [EnumMember] LeftLimitSwitchDisable = 13,
            [EnumMember] StepRatePreScaler = 14,


            [EnumMember] MinimumSpeed = 130,
            [EnumMember] ActualAcceleration = 135,
            [EnumMember] AccelerationThreshold = 136,
            [EnumMember] AccelerationDivider = 137,
            [EnumMember] RampMode = 138,
            [EnumMember] InterruptFlags = 139,
            [EnumMember] MicroStepResolution = 140,
            [EnumMember] ReferenceSwitchTolerance = 141,
            [EnumMember] SnapshotPosition = 142,
            [EnumMember] MaximumCurrentAtRest = 143,
            [EnumMember] MaximumCurrentAtLowAcceleration = 144,
            [EnumMember] MaximumCurrentAtHighAcceleration = 145,
            [EnumMember] AccelerationFactor = 146,
            [EnumMember] ReferenceSwitchDisableFlag = 147,
            [EnumMember] LimitSwitchDisableFlag = 148,
            [EnumMember] SoftStopFlag = 149,
            [EnumMember] PositionLatchFlag = 151,
            [EnumMember] InterruptMask = 152,
            [EnumMember] RampDivisor = 153,
            [EnumMember] PulseDivisor = 154,
            [EnumMember] ReferencingMode = 193,
            [EnumMember] ReferencingSearchSpeed = 194,
            [EnumMember] ReferencingSwitchSpeed = 195,
            [EnumMember] DriverOffTime = 198,
            [EnumMember] FastDecayTime = 200,
            [EnumMember] MixedDecayThreshold = 203,
            [EnumMember] FreeWheeling = 204,
            [EnumMember] StallDetectionThreshold = 205,
            [EnumMember] ActualLoadValue = 206,
            [EnumMember] DriverErrorFlags = 208,
            [EnumMember] EncoderPosition = 209,
            [EnumMember] EncoderPreScaler = 210,
            [EnumMember] FullStepThreshold = 211,
            [EnumMember] MaximumEncoderDeviation = 212,
            [EnumMember] GroupIndex = 213
        }
    }
}
