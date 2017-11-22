﻿using Dynatrace.OpenKit.Protocol;

namespace Dynatrace.OpenKit.Core.Communication
{
    /// <summary>
    /// Initial state for beacon sending.
    /// 
    /// The initial state is used to retrieve the configuration from the server and update the configuration.
    /// 
    /// Transitions to:
    /// <ul>
    ///     <li><code>BeaconSendingTerminalState</code> if initial status request failed or on shutdown</li>
    ///     <li><code>BeaconSendingTimeSyncState</code> if initial status request succeeded</li>
    /// </ul>
    /// </summary>
    internal class BeaconSendingInitState : AbstractBeaconSendingState
    {
        public const int MAX_INITIAL_STATUS_REQUEST_RETRIES = 5;
        public const int INITIAL_RETRY_SLEEP_TIME_MILLISECONDS = 1000;

        internal BeaconSendingInitState() : base(false) { }

        internal override AbstractBeaconSendingState ShutdownState => new BeaconSendingTerminalState();

        internal override void OnInterrupted(IBeaconSendingContext context)
        {
            context.InitCompleted(false);
        }

        protected override void DoExecute(IBeaconSendingContext context)
        {
            var currentTimestamp = context.CurrentTimestamp;
            context.LastOpenSessionBeaconSendTime = currentTimestamp;
            context.LastStatusCheckTime = currentTimestamp;

            StatusResponse statusResponse = null;
            var retry = 0;
            var sleepTimeInMillis = INITIAL_RETRY_SLEEP_TIME_MILLISECONDS;

            while (true)
            {
                statusResponse = context.GetHTTPClient().SendStatusRequest();

                // if no (valid) status response was received -> sleep 1s [2s, 4s, 8s, 16s] and then retry (max 6 times altogether)
                if (!RetryStatusRequest(context, statusResponse, retry))
                {
                    break;
                }

                context.Sleep(sleepTimeInMillis);
                sleepTimeInMillis *= 2;
                retry++;
            }

            if (context.IsShutdownRequested || (statusResponse == null))
            {
                // initial configuration request was either terminated from outside or the config could not be retrieved
                context.InitCompleted(false);
                context.CurrentState = new BeaconSendingTerminalState();
            }
            else
            {
                // success -> continue with time sync
                context.HandleStatusResponse(statusResponse);
                context.CurrentState = new BeaconSendingTimeSyncState(true);
            }
        }

        private static bool RetryStatusRequest(IBeaconSendingContext context, StatusResponse statusResponse, int retry)
        {
            return statusResponse == null
                && (retry < MAX_INITIAL_STATUS_REQUEST_RETRIES)
                && !context.IsShutdownRequested;
                
        }
    }
}