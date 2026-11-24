namespace TecnicoBrasfort.Core
{
    public sealed class UnattendedSessionState
    {
        public bool IsRunning { get; private set; }
        public bool IsUnattendedMode { get; private set; }

        public void Start()
        {
            IsRunning = true;
        }

        public void Complete()
        {
            IsRunning = false;
        }

        public bool TryActivateUnattended()
        {
            if (!IsRunning || IsUnattendedMode)
            {
                return false;
            }

            IsUnattendedMode = true;
            return true;
        }

        public void ActivateUnattendedAfterConfirmation()
        {
            IsUnattendedMode = true;
        }

        public bool ShouldShowOwnerOnRestore()
        {
            return !IsUnattendedMode;
        }
    }
}
