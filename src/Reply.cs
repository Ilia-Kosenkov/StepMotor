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

using System;
using System.Runtime.Serialization;

namespace StepMotor
{
    /// <summary>
    /// Represents a reply from a step motor.
    /// </summary>
    [DataContract]
    public struct Reply
    {
        /// <summary>
        /// Reply byte length.
        /// </summary>
        public static readonly int ReplyLength = 9;

        /// <summary>
        /// Address of the reply.
        /// </summary>
        [DataMember]
        public byte ReplyAddress
        {
            get;
            private set;
        }

        /// <summary>
        /// Address of the module.
        /// </summary>
        [DataMember]
        public byte ModuleAddress
        {
            get;
            private set;          
        }

        /// <summary>
        /// Status of a command execution.
        /// </summary>
        [DataMember]
        public ReturnStatus Status
        {
            get;
            private set;
        }

        /// <summary>
        /// Sent command.
        /// </summary>
        [DataMember]
        public Command Command
        {
            get;
            private set;       
        }

        /// <summary>
        /// Return value, Stores either value sent to motor or step motor parameter value if
        /// <see cref="CommandType"/> is Get***.
        /// </summary>
        [DataMember]
        public int ReturnValue
        {
            get;
            private set;
        }

        /// <summary>
        /// Constructs reply from COM-port byte reply.
        /// </summary>
        /// <param name="replyData">COM-port raw reply.</param>
        /// <exception cref="ArgumentNullException"/>
        /// <exception cref="ArgumentException"/>
        public Reply(byte[] replyData)
        {
            // Throws if input is null
            if (replyData == null)
                throw new ArgumentNullException(nameof(replyData));
            // If replyData has proper (expected) length
            if (replyData.Length == ReplyLength)
            {
                byte checkSum = 0;
                unchecked
                {
                    // Calculates checksum
                    for (var i = 0; i < ReplyLength - 1; i++)
                        checkSum += replyData[i];
                }

                // Checksum checked
                if (checkSum != replyData[ReplyLength - 1])
                    throw new ArgumentException("Wrong checksum in the reply.");

                // 0-th byte is reply address
                ReplyAddress = replyData[0];
                
                // 1-st is module address
                ModuleAddress = replyData[1];
                
                // 2-nd is status (coerced to enum)
                Status = Enum.IsDefined(typeof(ReturnStatus), replyData[2]) 
                    ? (ReturnStatus)replyData[2] 
                    : ReturnStatus.UnknownError;
                
                // 3-rd is command for which respond is received (coerced to enum)
                Command = Enum.IsDefined(typeof(Command), replyData[3]) 
                    ? (Command)replyData[3] 
                    : Command.Unknown;
                
                // Bytes 4 to 7 are Int32 return value. 
                // Step motor returns it in Most Significant Bit First format, so 
                // for LittleEndian environments sequence should be reversed
                ReturnValue = BitConverter.IsLittleEndian
                    ? BitConverter.ToInt32(new[] { replyData[7], replyData[6], replyData[5], replyData[4] }, 0)
                    : BitConverter.ToInt32(replyData, 4);
            }
            // If length is wrong, return a failed state reply
            else
            {
                ReplyAddress = 0;
                ModuleAddress = 0;
                Status = ReturnStatus.UnknownError;
                Command = Command.Unknown;
                ReturnValue = 0;
            }
        }

        public Reply(Span<byte> replyData)
        {
            if(replyData.IsEmpty || replyData.Length < ReplyLength)
                throw new ArgumentException(nameof(replyData));

            byte checkSum = 0;
            unchecked
            {
                // Calculates checksum
                for (var i = 0; i < ReplyLength - 1; i++)
                    checkSum += replyData[i];
            }

            if (checkSum != replyData[ReplyLength - 1])
                throw new ArgumentException("Wrong checksum in the reply.");

            ReplyAddress = replyData[0];

            // 1-st is module address
            ModuleAddress = replyData[1];

            // 2-nd is status (coerced to enum)
            Status = Enum.IsDefined(typeof(ReturnStatus), replyData[2])
                ? (ReturnStatus)replyData[2]
                : ReturnStatus.UnknownError;

            // 3-rd is command for which respond is received (coerced to enum)
            Command = Enum.IsDefined(typeof(Command), replyData[3])
                ? (Command)replyData[3]
                : Command.Unknown;

            // Bytes 4 to 7 are Int32 return value. 
            // Step motor returns it in Most Significant Bit First format
            ReturnValue = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(replyData.Slice(4, 4));
        }

        /// <summary>
        /// Standard string representation, 47 symbols long.
        /// </summary>
        /// <returns>String representation of <see cref="Reply"/>.</returns>
        public override string ToString()
        {
            return $"[{ReplyAddress,2};{ModuleAddress,2};{Command,15};{Status,12};{ReturnValue,10}]";
        }
    }
}
