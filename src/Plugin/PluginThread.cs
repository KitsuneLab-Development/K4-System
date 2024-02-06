using CounterStrikeSharp.API; // Feltételezve, hogy ez a Server.NextWorldUpdate helye

namespace K4System
{
	public static class ThreadHelper
	{
		public static void ExecuteAsync(Func<Task> asyncOperation, Action mainThreadContinuation)
		{
			SynchronizationContext? originalContext = SynchronizationContext.Current;

			Task.Run(async () =>
			{
				await asyncOperation();
				originalContext?.Post(_ => Server.NextWorldUpdate(mainThreadContinuation), null);
			});
		}

		public static void ExecuteAsync<TResult>(
			Func<Task<TResult>> asyncOperation,
			Action<TResult> mainThreadContinuation)
		{
			SynchronizationContext? originalContext = SynchronizationContext.Current;

			Task.Run(async () =>
			{
				TResult result = await asyncOperation();
				originalContext?.Post(_ => Server.NextWorldUpdate(() => mainThreadContinuation(result)), null);
			});
		}
	}
}
