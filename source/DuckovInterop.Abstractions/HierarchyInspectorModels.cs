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
}
