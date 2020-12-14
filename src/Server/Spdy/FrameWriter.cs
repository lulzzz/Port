﻿using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Log.It;
using Port.Server.Spdy.Primitives;

namespace Port.Server.Spdy
{
    internal class FrameWriter : IFrameWriter, IAsyncDisposable
    {
        private readonly Stream _buffer;
        private ILogger _logger = LogFactory.Create<FrameWriter>();
        public FrameWriter(
            Stream buffer)
        {
            _buffer = buffer;
        }

        public async ValueTask WriteUInt24Async(
            UInt24 value,
            CancellationToken cancellationToken = default)
        {
            await WriteAsync(
                new[]
                {
                    value.Three,
                    value.Two,
                    value.One
                }, cancellationToken)
                .ConfigureAwait(false);
        }

        public async ValueTask WriteInt32Async(
            int value,
            CancellationToken cancellationToken = default)
        {
            await WriteAsBigEndianAsync(
                    BitConverter.GetBytes(value), cancellationToken)
                .ConfigureAwait(false);
        }

        public async ValueTask WriteUInt32Async(
            uint value,
            CancellationToken cancellationToken = default)
        {
            await WriteAsBigEndianAsync(
                    BitConverter.GetBytes(value), cancellationToken)
                .ConfigureAwait(false);
        }

        public async ValueTask WriteByteAsync(
            byte value,
            CancellationToken cancellationToken)
        {
            await WriteAsBigEndianAsync(new[] {value}, cancellationToken)
                .ConfigureAwait(false);
        }

        public async ValueTask WriteBytesAsync(
            byte[] value,
            CancellationToken cancellationToken = default)
        {
            await WriteAsLittleEndianAsync(value, cancellationToken)
                .ConfigureAwait(false);
        }

        public async ValueTask WriteUShortAsync(
            ushort value,
            CancellationToken cancellationToken = default)
        {
            await WriteAsBigEndianAsync(
                    BitConverter.GetBytes(value), cancellationToken)
                .ConfigureAwait(false);
        }

        public async ValueTask WriteStringAsync(
            string value,
            Encoding encoding,
            CancellationToken cancellationToken = default)
        {
            var bytes = encoding.GetBytes(value);
            await WriteInt32Async(bytes.Length, cancellationToken)
                .ConfigureAwait(false);
            await WriteBytesAsync(bytes, cancellationToken)
                .ConfigureAwait(false);
        }

        private async ValueTask WriteAsLittleEndianAsync(
            byte[] value,
            CancellationToken cancellationToken = default)
        {
            if (BitConverter.IsLittleEndian == false)
            {
                Array.Reverse(value);
            }

            await WriteAsync(value, cancellationToken)
                .ConfigureAwait(false);
        }

        private async ValueTask WriteAsBigEndianAsync(
            byte[] value,
            CancellationToken cancellationToken = default)
        {
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(value);
            }

            await WriteAsync(value, cancellationToken)
                .ConfigureAwait(false);
        }

        private async ValueTask WriteAsync(
            byte[] value,
            CancellationToken cancellationToken = default)
        {
            if (value.Any() == false)
            {
                return;
            }
            
            _logger.Debug("Writing: {@value}", value);
            await _buffer.WriteAsync(value.AsMemory(), cancellationToken)
                .ConfigureAwait(false);
        }

        public async ValueTask DisposeAsync()
        {
            await _buffer.FlushAsync()
                .ConfigureAwait(false);
        }
    }
}