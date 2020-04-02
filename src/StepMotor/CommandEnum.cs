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
    /// <summary>
    /// A list of available commands
    /// </summary>
    [DataContract]
    public enum Command : byte
    {
        /// <summary>
        /// Unknown command
        /// </summary>
        [EnumMember]
        Unknown = 0,

        /// <summary>
        /// ROR: rotates right
        /// </summary>
        [EnumMember]
        RotateRight = 1,

        /// <summary>
        /// ROLL rotates left
        /// </summary>
        [EnumMember]
        RotateLeft = 2,

        /// <summary>
        /// MST: Motor stops
        /// </summary>
        [EnumMember]
        MotorStop = 3,

        /// <summary>
        /// MVP: Moves to position
        /// </summary>
        [EnumMember]
        MoveToPosition = 4,

        /// <summary>
        /// SAP: Sets axis parameter
        /// </summary>
        [EnumMember]
        SetAxisParameter = 5,

        /// <summary>
        /// GAP: Gets axis parameter
        /// </summary>
        [EnumMember]
        GetAxisParameter = 6,

        /// <summary>
        /// STAP: Stores axis parameter
        /// </summary>
        [EnumMember]
        StoreAxisParameter = 7,

        /// <summary>
        /// RSAP: Restores axis parameter
        /// </summary>
        [EnumMember]
        RestoreAxisParameter = 8,

        /// <summary>
        /// SGP: Sets global parameter
        /// </summary>
        [EnumMember]
        SetGlobalParameter = 9,

        /// <summary>
        /// GGP: Gets global parameter
        /// </summary>
        [EnumMember]
        GetGlobalParameter = 10,

        /// <summary>
        /// STGP: Stores global parameter
        /// </summary>
        [EnumMember]
        StoreGlobalParameter = 11,

        /// <summary>
        /// RSGP: Restores global parameter
        /// </summary>
        [EnumMember]
        RestoreGlobalParameter = 12,

        /// <summary>
        /// RFS: Reference search
        /// </summary>
        [EnumMember]
        ReferenceSearch = 13,

        /// <summary>
        /// SIO: Sets output
        /// </summary>
        [EnumMember]
        SetOutput = 14,

        /// <summary>
        /// GIO: Gets input/output
        /// </summary>
        [EnumMember]
        GetInputOutput = 15,

        /// <summary>
        /// CALC: Calculates
        /// </summary>
        [EnumMember]
        Calculate = 19,

        /// <summary>
        /// COMP: Compares
        /// </summary>
        [EnumMember]
        Compare = 20,

        /// <summary>
        /// SAC: Accesses SPI Bus
        /// </summary>
        [EnumMember]
        SpiBusAccess = 29,

        /// <summary>
        /// SCO: Sets coordinate
        /// </summary>
        [EnumMember]
        SetCoordinate = 30,

        /// <summary>
        /// GCO: Gets coordinate
        /// </summary>
        [EnumMember]
        GetCoordinate = 31,

        /// <summary>
        /// CCO: Captures coordinate
        /// </summary>
        [EnumMember]
        CaptureCoordinate = 32,

        /// <summary>
        /// CALCX: Calculates using a register
        /// </summary>
        [EnumMember]
        CalculateWithRegister = 33,

        /// <summary>
        /// AAP: Accumulates to axis parameter
        /// </summary>
        [EnumMember]
        AccumulatorToAxisParameter = 34,

        /// <summary>
        /// AGP: Accumulates to global parameter
        /// </summary>
        [EnumMember]
        AccumulatorToGlobalParameter = 35,

        /// <summary>
        /// CLE: Clears error flags
        /// </summary>
        [EnumMember]
        ClearErrorFlags = 36
    }
}
