namespace TecnicoBrasfort.Core
{
    public interface IExecutionObserver
    {
        void OnOutput(string line);
        void OnError(string line);
        void OnStepStarted(int stepIndex, int totalSteps, ActionDefinition action);
        void OnStepCompleted(int stepIndex, int totalSteps, ActionExecutionResult result);
    }

    public sealed class NullExecutionObserver : IExecutionObserver
    {
        public static readonly NullExecutionObserver Instance = new NullExecutionObserver();

        private NullExecutionObserver()
        {
        }

        public void OnOutput(string line)
        {
        }

        public void OnError(string line)
        {
        }

        public void OnStepStarted(int stepIndex, int totalSteps, ActionDefinition action)
        {
        }

        public void OnStepCompleted(int stepIndex, int totalSteps, ActionExecutionResult result)
        {
        }
    }
}
