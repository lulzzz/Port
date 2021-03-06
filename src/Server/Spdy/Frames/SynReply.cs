﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Log.It;
using Port.Server.Spdy.Collections;
using Port.Server.Spdy.Extensions;
using Port.Server.Spdy.Primitives;
using Port.Server.Spdy.Zlib;

namespace Port.Server.Spdy.Frames
{
    /// <summary>
    /// SYN_REPLY indicates the acceptance of a stream creation by the recipient of a SYN_STREAM frame.
    /// 
    /// +------------------------------------+
    /// |1|    version    |         2        |
    /// +------------------------------------+
    /// |  Flags (8)  |  Length (24 bits)    |
    /// +------------------------------------+
    /// |X|           Stream-ID (31bits)     |
    /// +------------------------------------+
    /// | Number of Name/Value pairs (int32) |   &lt;+
    /// +------------------------------------+    |
    /// |     Length of name (int32)         |    | This section is the "Name/Value
    /// +------------------------------------+    | Header Block", and is compressed.
    /// |           Name (string)            |    |
    /// +------------------------------------+    |
    /// |     Length of value  (int32)       |    |
    /// +------------------------------------+    |
    /// |          Value   (string)          |    |
    /// +------------------------------------+    |
    /// |           (repeats)                |   &lt;+
    /// </summary>
    public sealed class SynReply : Control
    {
        private SynReply(
            Options flags,
            UInt31 streamId,
            NameValueHeaderBlock? headers = null) : base(Type)
        {
            Flags = flags;
            StreamId = streamId;
            Headers = headers ?? new NameValueHeaderBlock();
        }

        public static SynReply AcceptAndClose(
            UInt31 streamId,
            NameValueHeaderBlock? headers = null)
        {
            return new SynReply(Options.Fin, streamId, headers);
        }

        public static SynReply Accept(
            UInt31 streamId,
            NameValueHeaderBlock? headers = null)
        {
            return new SynReply(Options.None, streamId, headers);
        }

        public const ushort Type = 2;

        /// <summary>
        /// Flags related to this frame.
        /// </summary>
        private new Options Flags
        {
            get => (Options) base.Flags;
            set => base.Flags = (byte)value;
        }

        [Flags]
        public enum Options : byte
        {
            None = 0,
            /// <summary>
            /// 0x01 = FLAG_FIN - marks this frame as the last frame to be transmitted on this stream and puts the sender in the half-closed (Section 2.3.6) state.
            /// </summary>
            Fin = 1
        }

        public bool IsLastFrame => Flags.HasFlag(Options.Fin);

        /// <summary>
        /// The 31-bit identifier for this stream.
        /// </summary>
        public UInt31 StreamId { get; }

        /// <summary>
        /// Name/Value Header Block: A set of name/value pairs carried as part of the SYN_STREAM. see Name/Value Header Block (Section 2.6.10).
        /// </summary>
        public IReadOnlyDictionary<string, string[]> Headers { get; }

        internal static async ValueTask<ReadResult<SynReply>> TryReadAsync(
            byte flags,
            UInt24 length,
            IFrameReader frameReader,
            CancellationToken cancellation = default)
        {
            var streamId = 
                await frameReader.ReadUInt32Async(cancellation)
                    .AsUInt31Async()
                    .ConfigureAwait(false);
            // The length is the number of bytes which follow the length field in the frame. For SYN_REPLY frames, this is 4 bytes plus the length of the compressed Name/Value block.
            var headerLength = (int)length.Value - 4;
            var headers = new NameValueHeaderBlock();
            if (headerLength > 0)
            {
                try
                {
                    headers =
                        await
                            frameReader
                                .ReadNameValuePairsAsync(headerLength, cancellation)
                                .ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    return ReadResult<SynReply>.Error(
                        RstStream.ProtocolError(streamId, exception));
                }
            }

            return ReadResult.Ok(new SynReply(flags.ToEnum<Options>(), streamId, headers));
        }

        protected override async ValueTask WriteControlFrameAsync(
            IFrameWriter frameWriter,
            CancellationToken cancellationToken = default)
        {
            var headerStream = new MemoryStream(1024);
            await using (headerStream
                .ConfigureAwait(false))
            {
                var headerWriter = new ZlibWriter(
                    headerStream, SpdyConstants.HeadersDictionary);
                await using (headerWriter
                    .ConfigureAwait(false))
                {
                    await headerWriter.WriteNameValuePairs(
                                          Headers, cancellationToken)
                                      .ConfigureAwait(false);
                }
            }

            var compressedHeaders = headerStream.ToArray();
            var length = compressedHeaders.Length + 4;
            await frameWriter.WriteUInt24Async(
                    UInt24.From((uint)length), cancellationToken)
                .ConfigureAwait(false);
            await frameWriter.WriteUInt32Async(
                    StreamId, cancellationToken)
                .ConfigureAwait(false);
            await frameWriter.WriteBytesAsync(
                    compressedHeaders, cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
