namespace InstallerUI.Bootstrapper
{
    /// <summary>
    /// We define “InstallationStatus” as an enumeration, 
    /// to know in what phase of the bootstrapping process 
    /// we are in so we can change the UI elements
    /// </summary>
    public enum InstallationStatus
    {
        Initializing,
        DetectedAbsent,
        DetectedPresent,
        DetectedNewer,
        Applying,
        Applied,
        Failed,
    }
}
