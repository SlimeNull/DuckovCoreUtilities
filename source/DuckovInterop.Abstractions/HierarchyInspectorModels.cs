using System.Collections.Generic;

namespace SlimeNull.DuckovInterop
{
    public sealed class ApiResult<T>
    {
        public bool Ok { get; set; }

        public string? Error { get; set; }

        public T? Data { get; set; }

        public static ApiResult<T> Success(T? data)
        {
            return new ApiResult<T> { Ok = true, Data = data };
        }

        public static ApiResult<T> Failure(string error)
        {
            return new ApiResult<T> { Ok = false, Error = error };
        }
    }

    public sealed class HierarchyResponse
    {
        public List<SceneNode> Scenes { get; set; } = new List<SceneNode>();
    }

    public sealed class SceneNode
    {
        public string? Name { get; set; }

        public List<GameObjectNode> Roots { get; set; } = new List<GameObjectNode>();
    }

    public sealed class GameObjectNode
    {
        public string? Name { get; set; }

        public int InstanceID { get; set; }

        public bool HasRenderer { get; set; }

        public bool IsGUI { get; set; }

        public List<ComponentInfo> Components { get; set; } = new List<ComponentInfo>();

        public List<GameObjectNode> Children { get; set; } = new List<GameObjectNode>();
    }

    public sealed class ComponentInfo
    {
        public string? Type { get; set; }

        public int InstanceID { get; set; }
    }

    public sealed class ObjectSearchResult
    {
        public string? Kind { get; set; }

        public string? Name { get; set; }

        public string? Type { get; set; }

        public int InstanceID { get; set; }

        public GameObjectRef? GameObject { get; set; }
    }

    public sealed class GameObjectRef
    {
        public string? Name { get; set; }

        public int InstanceID { get; set; }
    }

    public sealed class ValueInfo
    {
        public string? Kind { get; set; }

        public object? Value { get; set; }

        public string? Type { get; set; }

        public int? InstanceID { get; set; }

        public string? Name { get; set; }

        public GameObjectRef? GameObject { get; set; }

        public string? Guid { get; set; }
    }

    public sealed class VectorInfo
    {
        public float X { get; set; }

        public float Y { get; set; }

        public float? Z { get; set; }

        public float? W { get; set; }
    }

    public sealed class ColorInfo
    {
        public float R { get; set; }

        public float G { get; set; }

        public float B { get; set; }

        public float A { get; set; }
    }

    public sealed class SceneSnapshot
    {
        public string? CapturedAtUtc { get; set; }

        public List<InspectorScene> Scenes { get; set; } = new List<InspectorScene>();
    }

    public sealed class InspectorScene
    {
        public string? Name { get; set; }

        public int BuildIndex { get; set; }

        public List<InspectorGameObject> Roots { get; set; } = new List<InspectorGameObject>();
    }

    public sealed class InspectorGameObject
    {
        public string? Name { get; set; }

        public int InstanceID { get; set; }

        public bool ActiveSelf { get; set; }

        public bool ActiveInHierarchy { get; set; }

        public bool HasRenderer { get; set; }

        public bool IsGUI { get; set; }

        public string? Tag { get; set; }

        public int Layer { get; set; }

        public int ComponentCount { get; set; }

        public List<InspectorComponent> Components { get; set; } = new List<InspectorComponent>();

        public List<InspectorGameObject> Children { get; set; } = new List<InspectorGameObject>();
    }

    public sealed class InspectorComponent
    {
        public string? Name { get; set; }

        public string? Type { get; set; }

        public int InstanceID { get; set; }

        public bool? Enabled { get; set; }

        public string? Error { get; set; }

        public List<SerializedFieldInfo> Fields { get; set; } = new List<SerializedFieldInfo>();
    }

    public sealed class SerializedFieldInfo
    {
        public string? Name { get; set; }

        public string? DisplayName { get; set; }

        public string? Type { get; set; }

        public string? Path { get; set; }

        public bool CanWrite { get; set; }

        public string? Kind { get; set; }

        public string? Value { get; set; }

        public int? InstanceID { get; set; }

        public string? ObjectName { get; set; }

        public string? Header { get; set; }

        public string? Tooltip { get; set; }

        public float? RangeMin { get; set; }

        public float? RangeMax { get; set; }

        public bool Multiline { get; set; }

        public int? TextAreaMinLines { get; set; }

        public int? TextAreaMaxLines { get; set; }

        public List<string> EnumNames { get; set; } = new List<string>();

        public List<SerializedFieldInfo> Children { get; set; } = new List<SerializedFieldInfo>();
    }
}
