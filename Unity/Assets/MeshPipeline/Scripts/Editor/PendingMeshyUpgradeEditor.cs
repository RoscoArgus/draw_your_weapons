using UnityEditor;

public static class PendingMeshyUpgradeEditor
{
    [MenuItem("CONTEXT/PendingMeshyUpgrade/Upgrade To Meshy Model", false, 2000)]
    private static void UpgradeToMeshyModel(MenuCommand command)
    {
        if (command.context is PendingMeshyUpgrade pendingUpgrade)
            pendingUpgrade.UpgradeToMeshyModel();
    }

    [MenuItem("CONTEXT/PendingMeshyUpgrade/Upgrade To Meshy Model", true)]
    private static bool ValidateUpgradeToMeshyModel(MenuCommand command)
    {
        return command.context is PendingMeshyUpgrade pendingUpgrade && pendingUpgrade.IsUpgradeReady;
    }
}
