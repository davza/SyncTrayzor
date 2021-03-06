﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SyncTrayzor.SyncThing
{
    public interface ISyncThingPoller
    {
        bool Running { get; set; }
    }

    public abstract class SyncThingPoller : ISyncThingPoller
    {
        private readonly TimeSpan pollingInterval;
        private readonly TimeSpan erroredWaitInterval;

        private readonly object runningLock = new object();
        private bool _running;
        public bool Running
        {
            get { lock (this.runningLock) { return this._running; } }
            set
            {
                lock (this.runningLock)
                {
                    if (this._running == value)
                        return;

                    this._running = value;
                    if (value)
                    {
                        this.Start();
                    }
                }
            }
        }

        public SyncThingPoller(TimeSpan pollingInterval)
            : this(pollingInterval, TimeSpan.FromMilliseconds(1000))
        { }

        public SyncThingPoller(TimeSpan pollingInterval, TimeSpan erroredWaitInterval)
        {
            this.pollingInterval = pollingInterval;
            this.erroredWaitInterval = erroredWaitInterval;
        }

        protected virtual async void Start()
        {
            try
            {
                while (this.Running)
                {
                    bool errored = false;

                    try
                    {
                        await this.PollAsync();
                        if (this.pollingInterval.Ticks > 0)
                            await Task.Delay(this.pollingInterval);
                    }
                    catch (HttpRequestException)
                    {
                        errored = true;
                    }
                    catch (IOException)
                    {
                        // Socket forcibly closed. Could be a restart, could be a termination. We'll have to continue and quit if we're stopped
                        errored = true;
                    }
                    catch (OperationCanceledException)
                    {
                        // Not entirely sure why this can occur. Blame Refit
                        errored = true;
                    }

                    if (errored)
                        await Task.Delay(this.erroredWaitInterval);
                }
            }
            finally
            {
                this.Running = false;
            }
        }

        protected abstract Task PollAsync();
    }
}
