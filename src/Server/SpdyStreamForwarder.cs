﻿using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Log.It;
using Port.Server.Spdy;
using ReadResult = System.IO.Pipelines.ReadResult;

namespace Port.Server
{
    internal sealed class SpdyStreamForwarder : IAsyncDisposable
    {
        private readonly INetworkServer _networkServer;
        private readonly SpdySession _spdySession;
        private const int Stopped = 0;
        private const int Started = 1;
        private int _status = Stopped;

        private readonly CancellationTokenSource _cancellationTokenSource =
            new CancellationTokenSource();

        private CancellationToken CancellationToken
            => _cancellationTokenSource.Token;

        private readonly List<Task> _backgroundTasks = new List<Task>();

        private readonly ILogger _logger = LogFactory.Create<SpdyStreamForwarder>();

        private SpdyStreamForwarder(
            INetworkServer networkServer,
            SpdySession spdySession)
        {
            _networkServer = networkServer;
            _spdySession = spdySession;
        }

        internal static IAsyncDisposable Start(
            INetworkServer networkServer,
            SpdySession spdySession)
        {
            return new SpdyStreamForwarder(networkServer, spdySession)
                .Start();
        }

        private IAsyncDisposable Start()
        {
            var previousStatus = Interlocked.Exchange(ref _status, Started);
            if (previousStatus == Started)
            {
                return this;
            }

            _backgroundTasks.Add(StartReceivingLocalClientsAsync());
            return this;
        }

        private async Task StartReceivingLocalClientsAsync()
        {
            while (_cancellationTokenSource.IsCancellationRequested ==
                   false)
            {
                try
                {
                    var client = await _networkServer
                                       .WaitForConnectedClientAsync(CancellationToken)
                                       .ConfigureAwait(false);

                    _logger.Trace("Local socket connected");
                    _backgroundTasks.Add(StartPortForwardingAsync(client));
                }
                catch when (_cancellationTokenSource
                    .IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.Fatal(ex, "Unknown error while waiting for clients, closing down");
#pragma warning disable 4014
                    //Cancel and exit fast
                    //This will most likely change when we need to report
                    //back that the forwarding terminated or that we
                    //should retry
                    _cancellationTokenSource.Cancel(false);
#pragma warning restore 4014
                    return;
                }
            }
        }

        private async Task StartPortForwardingAsync(INetworkClient client)
        {
            await using (client.ConfigureAwait(false))
            {
                using var stream = _spdySession.Open();
                try
                {
                    using var _ = _logger.LogicalThread.With(
                        "local-socket-id", Guid.NewGuid());
                    _backgroundTasks.Add(StartSendingAsync(
                        client,
                        stream,
                        CancellationToken));
                    _backgroundTasks.Add(
                        StartReceivingAsync(
                            client,
                            stream,
                            CancellationToken));

                    await stream.Local.WaitForClosedAsync(CancellationToken)
                                .ConfigureAwait(false);
                    await stream.Remote.WaitForClosedAsync(CancellationToken)
                                .ConfigureAwait(false);
                }
                catch when (_cancellationTokenSource
                    .IsCancellationRequested)
                {
                }
                catch (Exception ex)
                {
                    _logger.Fatal(ex, "Unknown error while sending and receiving data, closing down");
#pragma warning disable 4014
                    //Cancel and exit fast
                    //This will most likely change when we need to report
                    //back that the forwarding terminated or that we
                    //should retry
                    _cancellationTokenSource.Cancel(false);
#pragma warning restore 4014
                }
            }
        }

        private async Task StartSendingAsync(
            INetworkClient localSocket,
            SpdyStream spdyStream,
            CancellationToken cancellationToken)
        {
            using var memoryOwner = MemoryPool<byte>.Shared.Rent(65536);
            var memory = memoryOwner.Memory;
            FlushResult sendResult;
            do
            {
                _logger.Trace("Receiving from local socket");
                var bytesReceived = await localSocket
                                          .ReceiveAsync(
                                              memory,
                                              cancellationToken)
                                          .ConfigureAwait(false);

                // End of the stream! 
                // https://docs.microsoft.com/en-us/dotnet/api/system.net.sockets.sockettaskextensions.receiveasync?view=netcore-3.1
                if (bytesReceived == 0)
                {
                    await spdyStream.SendLastAsync(
                                        new ReadOnlyMemory<byte>(),
                                        cancellationToken: cancellationToken)
                                    .ConfigureAwait(false);
                    return;
                }

                _logger.Trace(
                    "Sending {bytes} bytes to remote socket",
                    bytesReceived + 1);

                sendResult = await spdyStream
                                   .SendAsync(
                                       memory.Slice(0, bytesReceived),
                                       cancellationToken: cancellationToken)
                                   .ConfigureAwait(false);

            } while (sendResult.HasMore());
        }

        private static async Task StartReceivingAsync(
            INetworkClient localSocket,
            SpdyStream spdyStream,
            CancellationToken cancellationToken)
        {
            ReadResult content;
            do
            {
                content = await spdyStream
                                .ReceiveAsync(
                                    cancellationToken: cancellationToken)
                                .ConfigureAwait(false);

                foreach (var sequence in content.Buffer)
                {
                    await localSocket
                          .SendAsync(
                              sequence,
                              cancellationToken)
                          .ConfigureAwait(false);
                }
            } while (content.HasMoreData());
        }

        public async ValueTask DisposeAsync()
        {
            _cancellationTokenSource.Cancel(false);

            try
            {
                await _networkServer.DisposeAsync()
                                    .ConfigureAwait(false);
            }
            catch
            {
                // Ignore unhandled exceptions during shutdown 
            }

            try
            {
                await _spdySession.DisposeAsync()
                                  .ConfigureAwait(false);
            }
            catch
            {
                // Ignore unhandled exceptions during shutdown 
            }

            await Task.WhenAll(_backgroundTasks)
                      .ConfigureAwait(false);

            _cancellationTokenSource.Dispose();
        }
    }
}