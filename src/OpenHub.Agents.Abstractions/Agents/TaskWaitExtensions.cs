using System.Threading;
using System.Threading.Tasks;

namespace OpenHub.Agents;

internal static class TaskWaitExtensions
{
    public static Task WaitAsyncCompat(this Task task, CancellationToken cancellationToken)
    {
        if (!cancellationToken.CanBeCanceled || task.IsCompleted)
        {
            return task;
        }

#if NET8_0_OR_GREATER
        return task.WaitAsync(cancellationToken);
#else
        return WaitAsyncCore(task, cancellationToken);
#endif
    }

#if !NET8_0_OR_GREATER
    private static async Task WaitAsyncCore(Task task, CancellationToken cancellationToken)
    {
        TaskCompletionSource<bool> cancellationTaskSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

        using (cancellationToken.Register(
            static state => ((TaskCompletionSource<bool>)state!).TrySetCanceled(),
            cancellationTaskSource))
        {
            Task completedTask = await Task.WhenAny(task, cancellationTaskSource.Task).ConfigureAwait(false);
            if (completedTask == cancellationTaskSource.Task)
            {
                await completedTask.ConfigureAwait(false); // throws OperationCanceledException
                return;
            }
        }

        await task.ConfigureAwait(false);
    }
#endif
}
