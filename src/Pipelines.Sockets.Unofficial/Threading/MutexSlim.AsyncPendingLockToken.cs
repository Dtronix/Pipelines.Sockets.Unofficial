﻿using System;
using System.IO.Pipelines;
using System.Threading;

namespace Pipelines.Sockets.Unofficial.Threading
{
    public partial class MutexSlim
    {
        internal sealed class AsyncPendingLockToken : PendingLockToken
        {
            private Action _continuation;
            private readonly MutexSlim _mutex;

            public AsyncPendingLockToken(MutexSlim mutex, uint start) : base(start)
                => _mutex = mutex;

            protected override void OnAssigned()
            {
                var callback = Interlocked.Exchange(ref _continuation, null);
                if (callback != null)
                {
                    var scheduler = _mutex?._scheduler ?? PipeScheduler.ThreadPool;
                    scheduler.Schedule(s => ((Action)s).Invoke(), callback);
                }
            }

            internal void OnCompleted(Action continuation)
            {
                if (IsCompleted)
                {
                    continuation.Invoke();
                    return;
                }
                if (Interlocked.CompareExchange(ref _continuation, continuation, null) != null)
                {
                    ThrowNotSupported();
                }

                void ThrowNotSupported() => throw new NotSupportedException($"Only one pending continuation is permitted");
            }

            internal LockToken GetResultAsToken() => new LockToken(_mutex, GetResult()).AssertNotCanceled();
        }
    }
}