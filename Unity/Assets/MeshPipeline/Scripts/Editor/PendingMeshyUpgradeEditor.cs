using UnityEditor;

public static class PendingMeshyUpgradeEditor
{
    [MenuItem("CONTEXT/PendingMeshyUpgrade/Upgrade To Meshy Model", false, 2000)]
    /// <summary>
    /// Debug menu item to trigger the upgrade to the generated Meshy model when ready
    /// </summary>
    /// <param name="command">Menu command context</param>
    private static void UpgradeToMeshyModel(MenuCommand command)
    {
        if (command.context is PendingMeshyUpgrade pendingUpgrade)
        {
            pendingUpgrade.UpgradeToMeshyModel();
        }
    }

    [MenuItem("CONTEXT/PendingMeshyUpgrade/Upgrade To Meshy Model", true)]
    /// <summary>
    /// Enables the upgrade menu item only when the weapon is ready
    /// </summary>
    /// <param name="command">Menu command context</param>
    /// <returns>True if upgrade ready, false otherwise</returns>
    private static bool ValidateUpgradeToMeshyModel(MenuCommand command)
    {
        return command.context is PendingMeshyUpgrade pendingUpgrade && pendingUpgrade.IsUpgradeReady;
    }
}
