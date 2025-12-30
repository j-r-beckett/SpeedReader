// Copyright (c) 2025 j-r-beckett
// Licensed under the Apache License, Version 2.0

namespace SpeedReader.Ocr;

public class TaskPool<T>
{
    private readonly Queue<(TaskCompletionSource<Task<T>>, Func<Task<T>>)> _pendingWorkQueue = new();

    private readonly Lock _lock = new();

    public TaskPool(int initialPoolSize = 1)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(initialPoolSize, 1, nameof(initialPoolSize));
        PoolSize = initialPoolSize;
    }

    public int PoolSize { get; private set; }  // Current max number of concurrently executing tasks

    public int PoolOccupancy { get; private set; }  // Current number of concurrently executing tasks

    // Outer task is for entering the pool, inner task is for executing the task created by userTaskCreator in the pool.
    // `userTaskCreator` should be a synchronous function that returns a Task
    public async Task<Task<T>> Execute(Func<Task<T>> userTaskCreator)
    {
        var tcs = new TaskCompletionSource<Task<T>>();

        lock (_lock)
        {
            _pendingWorkQueue.Enqueue((tcs, userTaskCreator));
            StartNewTasks();
        }

        var userTask = await tcs.Task;  // Wait for the user's task to be started

        // This runs after the user's task completes
#pragma warning disable CS4014  // Because this call is not awaited, execution of the current method continues before the call is completed
        userTask.ContinueWith(_ =>
        {
            lock (_lock)
            {
                PoolOccupancy--;
                StartNewTasks();
            }
        });
#pragma warning restore CS4014

        // Return the user's task so they can await it
        return userTask;
    }

    public void SetPoolSize(int newSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(newSize, 1, nameof(newSize));
        lock (_lock)
        {
            PoolSize = newSize;
            StartNewTasks();  // If pool size decreased this does nothing
        }
    }

    // Called after adding a task to the work queue, after increasing the pool size, and after completing a task.
    // Basically, we call it every time there's a possibility a new task could start executing.
    // Not threadsafe, must be called with lock(_lock).
    private void StartNewTasks()
    {
        while (PoolOccupancy < PoolSize && _pendingWorkQueue.TryDequeue(out var workItem))
        {
            var (tcs, taskCreator) = workItem;
            Task<T> userTask;
            try
            {
                userTask = taskCreator();
            }
            catch (Exception ex)
            {
                userTask = Task.FromException<T>(new UserTaskCreationException("Error creating user task", ex));
            }
            tcs.SetResult(userTask);
            PoolOccupancy++;
        }
    }
}

public class UserTaskCreationException : Exception
{
    public UserTaskCreationException(string message, Exception innerException) : base(message, innerException) { }
}
