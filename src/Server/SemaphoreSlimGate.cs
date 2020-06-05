﻿using System;
using System.Threading;
using System.Threading.Tasks;

namespace Kubernetes.PortForward.Manager.Server
{
    internal sealed class SemaphoreSlimGate : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;

        internal static SemaphoreSlimGate OneAtATime => 
            new SemaphoreSlimGate(new SemaphoreSlim(1, 1));

        internal SemaphoreSlimGate(
            SemaphoreSlim semaphore)
        {
            _semaphore = semaphore;
        }

        internal async Task<IDisposable> WaitAsync(
            CancellationToken cancellationToken)
        {
            await _semaphore.WaitAsync(cancellationToken)
                .ConfigureAwait(false);
            return new DisposableAction(
                () =>
                    _semaphore.Release());
        }

        public void Dispose()
        {
            _semaphore.Dispose();
        }
    }
}