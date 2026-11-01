using System.Collections;

namespace MelonLoader
{
    /// <summary>
    /// Starts and stops coroutines on the host's single main-thread scheduler.
    /// </summary>
    public class MelonCoroutines
    {
        public static object Start(IEnumerator routine)
        {
            if (routine == null)
            {
                MelonLogger.Publish(AdapterLogLevel.Error, "MelonCoroutines.Start was called with a null routine.", null, null);
                return null;
            }

            IAdapterCoroutineRunner runner = AdapterBridge.CoroutineRunner;
            if (runner == null)
            {
                MelonLogger.Publish(AdapterLogLevel.Error, AdapterBridge.DescribeMissingService("coroutine runner") + " The coroutine was not started.", null, null);
                return null;
            }

            return runner.Start(routine);
        }

        public static void Stop(object coroutineHandle)
        {
            if (coroutineHandle == null)
            {
                return;
            }

            IAdapterCoroutineRunner runner = AdapterBridge.CoroutineRunner;
            if (runner == null)
            {
                MelonLogger.Publish(AdapterLogLevel.Error, AdapterBridge.DescribeMissingService("coroutine runner") + " The coroutine cannot be stopped.", null, null);
                return;
            }

            runner.Stop(coroutineHandle);
        }
    }
}
