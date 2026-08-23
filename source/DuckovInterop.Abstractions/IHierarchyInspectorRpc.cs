namespace SlimeNull.DuckovInterop
{
    using System.Collections.Generic;

    public interface IHierarchyInspectorRpc
    {
        string Test(string input);

        ApiResult<HierarchyResponse> GetHierarchy();

        ApiResult<SceneSnapshot> GetSceneSnapshot();

        ApiResult<SceneSnapshot> GetSceneOverview();

        ApiResult<List<InspectorComponent>> GetInspectorComponents(string gameObjectId);

        ApiResult<bool> SetGameObjectActive(string gameObjectId, bool active);

        ApiResult<List<ComponentInfo>> GetComponents(string gameObjectId);

        ApiResult<List<ObjectSearchResult>> FindByName(string name, bool includeInactive);

        ApiResult<List<ObjectSearchResult>> FindByType(string typeName, bool includeInactive);

        ApiResult<ValueInfo> GetValue(string objectId, string path, bool storeResult);

        ApiResult<ValueInfo> SetValue(string objectId, string path, string valueJson, bool storeResult);

        ApiResult<ValueInfo> JintEvaluate(string script, bool storeResult);

        ApiResult<ValueInfo> CallMethod(string objectId, string path, string argumentsJson, bool storeResult);
    }
}
