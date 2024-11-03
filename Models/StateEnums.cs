using System;
using System.ComponentModel;

namespace WebAI.Models
{
    public static class StateEnums
    {
        public enum AgentState
        {
            [Description("Agent is available for new tasks")]
            Available,

            [Description("Agent is currently processing a task")]
            Processing,

            [Description("Agent is waiting for external input")]
            WaitingForInput,

            [Description("Agent has encountered an error")]
            Error,

            [Description("Agent is temporarily paused")]
            Paused,

            [Description("Agent is offline or unavailable")]
            Offline
        }

        public enum ProcessingState
        {
            [Description("Process not yet started")]
            NotStarted,

            [Description("Currently initializing")]
            Initializing,

            [Description("Active processing")]
            Processing,

            [Description("Waiting for external resource")]
            Waiting,

            [Description("Process completed successfully")]
            Completed,

            [Description("Process failed")]
            Failed,

            [Description("Process cancelled")]
            Cancelled
        }

        public enum Priority
        {
            [Description("Lowest priority")]
            Lowest = 0,

            [Description("Low priority")]
            Low = 1,

            [Description("Normal priority")]
            Normal = 2,

            [Description("High priority")]
            High = 3,

            [Description("Urgent priority")]
            Urgent = 4,

            [Description("Critical priority")]
            Critical = 5
        }

        public enum CompletionStatus
        {
            [Description("Not started")]
            NotStarted,

            [Description("In progress")]
            InProgress,

            [Description("Completed successfully")]
            Completed,

            [Description("Failed to complete")]
            Failed,

            [Description("Blocked by dependency")]
            Blocked,

            [Description("Cancelled")]
            Cancelled,

            [Description("On hold")]
            OnHold
        }

        public enum ValidationStatus
        {
            [Description("Not validated")]
            NotValidated,

            [Description("Validation in progress")]
            Validating,

            [Description("Validation passed")]
            Valid,

            [Description("Validation failed")]
            Invalid,

            [Description("Validation error")]
            Error
        }

        public enum FileProcessingState
        {
            [Description("File upload pending")]
            Pending,

            [Description("File currently uploading")]
            Uploading,

            [Description("File processing")]
            Processing,

            [Description("File processed successfully")]
            Processed,

            [Description("Error during processing")]
            Error,

            [Description("File deleted")]
            Deleted
        }

        public class StateTransition<T> where T : struct
        {
            public T CurrentState { get; private set; }
            public T NextState { get; private set; }
            public string Reason { get; private set; }
            public DateTime Timestamp { get; private set; }

            public StateTransition(T currentState, T nextState, string reason)
            {
                CurrentState = currentState;
                NextState = nextState;
                Reason = reason;
                Timestamp = DateTime.UtcNow;
            }

            public override string ToString()
            {
                if (string.IsNullOrEmpty(Reason))
                    return string.Format("{0:yyyy-MM-dd HH:mm:ss} - {1} → {2}",
                        Timestamp, CurrentState, NextState);

                return string.Format("{0:yyyy-MM-dd HH:mm:ss} - {1} → {2} ({3})",
                    Timestamp, CurrentState, NextState, Reason);
            }
        }

        public class StateValidation
        {
            public static bool IsValidTransition<T>(T currentState, T nextState) where T : struct
            {
                if (typeof(T) == typeof(ProcessingState))
                {
                    ProcessingState current = (ProcessingState)(object)currentState;
                    ProcessingState next = (ProcessingState)(object)nextState;

                    switch (current)
                    {
                        case ProcessingState.NotStarted:
                            return next == ProcessingState.Initializing;

                        case ProcessingState.Initializing:
                            return next == ProcessingState.Processing ||
                                   next == ProcessingState.Failed;

                        case ProcessingState.Processing:
                            return next == ProcessingState.Completed ||
                                   next == ProcessingState.Failed ||
                                   next == ProcessingState.Waiting;

                        case ProcessingState.Waiting:
                            return next == ProcessingState.Processing ||
                                   next == ProcessingState.Failed;

                        case ProcessingState.Completed:
                            return false; // Terminal state

                        case ProcessingState.Failed:
                            return next == ProcessingState.Initializing;

                        case ProcessingState.Cancelled:
                            return next == ProcessingState.Initializing;

                        default:
                            return false;
                    }
                }

                return true;
            }

            public static string GetTransitionError<T>(T currentState, T nextState) where T : struct
            {
                if (!IsValidTransition(currentState, nextState))
                {
                    return string.Format("Invalid state transition from {0} to {1}",
                        currentState, nextState);
                }
                return null;
            }
        }
    }
}