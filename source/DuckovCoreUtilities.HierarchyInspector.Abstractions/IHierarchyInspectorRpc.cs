namespace SlimeNull.DuckovCoreUtilities.HierarchyInspector
{
    public interface IHierarchyInspectorRpc
    {
        string Test(string input);

        string GetHierarchy();

        string FindByName(string name, bool includeInactive);

        string FindByType(string typeName, bool includeInactive);

        string GetValue(string objectId, string path, bool storeResult);

        string SetValue(string objectId, string path, string valueJson, bool storeResult);

        string CallMethod(string objectId, string path, string argumentsJson, bool storeResult);
    }
}
