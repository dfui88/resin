﻿using System;
using System.IO;
using System.Threading.Tasks;

namespace Sir.Store
{
    /// <summary>
    /// Read a value by supplying an offset, length and data type.
    /// </summary>
    public class ValueReader
    {
        private readonly Stream _stream;

        public ValueReader(Stream stream)
        {
            _stream = stream;
        }

        public object Read(long offset, int len, byte dataType)
        {
            int read;
            byte[] buf;

            _stream.Seek(offset, SeekOrigin.Begin);
            buf = new byte[len];
            read = _stream.Read(buf, 0, len);

            if (read != len)
            {
                throw new InvalidDataException();
            }

            var typeId = Convert.ToInt32(dataType);

            if (DataType.BOOL == typeId)
            {
                return Convert.ToBoolean(buf[0]);
            }
            else if (DataType.CHAR == typeId)
            {
                return BitConverter.ToChar(buf, 0);
            }
            else if (DataType.FLOAT == typeId)
            {
                return BitConverter.ToSingle(buf, 0);
            }
            else if (DataType.INT == typeId)
            {
                return BitConverter.ToInt32(buf, 0);
            }
            else if (DataType.DOUBLE == typeId)
            {
                return BitConverter.ToDouble(buf, 0);
            }
            else if (DataType.LONG == typeId)
            {
                return BitConverter.ToInt64(buf, 0);
            }
            else if (DataType.DATETIME == typeId)
            {
                return DateTime.FromBinary(BitConverter.ToInt64(buf, 0));
            }
            else if (DataType.STRING == typeId)
            {
                return new string(System.Text.Encoding.Unicode.GetChars(buf));
            }
            else
            {
                return buf;
            }
        }
    }
}