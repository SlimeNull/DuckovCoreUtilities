using EleCho.JsonRpc;
using SlimeNull.DuckovCoreUtilities.HierarchyInspector;
using SlimeNull.DuckovCoreUtilities.Infrastructure;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SlimeNull.DuckovCoreUtilities.Features
{
    internal sealed class HierarchyInspectorMcpFeature : FeatureBase, IHierarchyInspectorRpc
    {
        private readonly MainThreadDispatcher _dispatcher = new MainThreadDispatcher();
        private readonly ObjectRegistry _registry = new ObjectRegistry();
        private volatile bool _stopping;
        private readonly List<TcpClient> _clients = new List<TcpClient>();
        private readonly object _clientsLock = new object();
        private TcpListener? _listener;
        private Thread? _serverTask;

        public override string Name => "Hierarchy inspector MCP";

        async Task SomeAsyncMethod()
        {
            Debug.Log("Action in async method 1");
            await Task.Delay(10);
            Debug.Log("Action in async method 2");
        }

        protected override void OnEnable()
        {
            var t = SomeAsyncMethod();

            _stopping = false;
            Debug.Log($"[HierarchyInspectorMcpFeature] RPC server thread start");
            _serverTask = new Thread(RunServerLoop)
            {
                IsBackground = true
            };

            _serverTask.Start();
            Debug.Log($"[HierarchyInspectorMcpFeature] RPC server thread started");
        }

        protected override void OnDisable()
        {
            _stopping = true;
            try
            {
                _listener?.Stop();
            }
            catch
            {
            }

            lock (_clientsLock)
            {
                foreach (var client in _clients.ToArray())
                {
                    try
                    {
                        client.Close();
                    }
                    catch
                    {
                    }
                }

                _clients.Clear();
            }

            if (_serverTask != null && !_serverTask.Join(1000))
            {
                _serverTask.Abort();
            }

            _serverTask = null;
        }

        public override void Tick()
        {
            _dispatcher.Drain();
        }

        private void RunServerLoop()
        {
            Debug.Log($"[HierarchyInspectorMcpFeature] Enter Server Loop");

            try
            {
                _listener = new TcpListener(IPAddress.Parse(HierarchyInspectorRpcEndpoint.Host), HierarchyInspectorRpcEndpoint.Port);
                _listener.Start();
                Debug.Log($"[HierarchyInspectorMcpFeature] RPC TCP listener started at {HierarchyInspectorRpcEndpoint.Host}:{HierarchyInspectorRpcEndpoint.Port}");

                while (!_stopping)
                {
                    var client = _listener.AcceptTcpClient();
                    TrackClient(client);
                    Debug.Log($"[HierarchyInspectorMcpFeature] RPC client connected");

                    var thread = new Thread(() => RunClient(client))
                    {
                        IsBackground = true
                    };

                    thread.Start();
                }
            }
            catch (SocketException) when (_stopping)
            {
            }
            catch (ObjectDisposedException) when (_stopping)
            {
            }
            catch (Exception ex)
            {
                Debug.LogError($"[HierarchyInspectorMcpFeature] RPC listener error: {ex}");
            }
            finally
            {
                _listener = null;
                Debug.Log($"[HierarchyInspectorMcpFeature] RPC listener stopped");
            }
        }

        private void RunClient(TcpClient client)
        {
            try
            {
                using (client)
                using (var stream = client.GetStream())
                using (var server = new RpcServer<IHierarchyInspectorRpc>(stream, this))
                {
                    server.Run();
                }
            }
            catch (Exception ex)
            {
                if (!_stopping)
                {
                    Debug.LogError($"[HierarchyInspectorMcpFeature] RPC client error: {ex}");
                }
            }
            finally
            {
                UntrackClient(client);
                Debug.Log($"[HierarchyInspectorMcpFeature] RPC client disconnected");
            }
        }

        private void TrackClient(TcpClient client)
        {
            lock (_clientsLock)
            {
                _clients.Add(client);
            }
        }

        private void UntrackClient(TcpClient client)
        {
            lock (_clientsLock)
            {
                _clients.Remove(client);
            }
        }

        public ApiResult<HierarchyResponse> GetHierarchy()
        {
            return _dispatcher.Invoke(() =>
            {
                var scenes = Enumerable.Range(0, SceneManager.sceneCount)
                    .Select(SceneManager.GetSceneAt)
                    .Where(scene => scene.IsValid() && scene.isLoaded)
                    .Select(scene => new SceneNode
                    {
                        Name = scene.name,
                        Roots = scene.GetRootGameObjects().Select(CreateGameObjectNode).ToList()
                    })
                    .ToList();
                return ApiResult<HierarchyResponse>.Success(new HierarchyResponse { Scenes = scenes });
            });
        }

        public ApiResult<List<ComponentInfo>> GetComponents(string gameObjectId)
        {
            return _dispatcher.Invoke(() =>
            {
                if (!_registry.TryResolve(gameObjectId, out var root, out var error))
                {
                    return ApiResult<List<ComponentInfo>>.Failure(error);
                }

                if (root is not GameObject gameObject)
                {
                    return ApiResult<List<ComponentInfo>>.Failure($"Object '{gameObjectId}' is not a GameObject.");
                }

                return ApiResult<List<ComponentInfo>>.Success(GetComponentInfos(gameObject));
            });
        }

        public ApiResult<List<ObjectSearchResult>> FindByName(string name, bool includeInactive)
        {
            return _dispatcher.Invoke(() =>
            {
                var matches = Resources.FindObjectsOfTypeAll<GameObject>()
                    .Where(IsSceneObject)
                    .Where(go => includeInactive || go.activeInHierarchy)
                    .Where(go => string.Equals(go.name, name, StringComparison.OrdinalIgnoreCase) || go.name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
                    .Select(go => new ObjectSearchResult { Kind = "GameObject", Name = go.name, InstanceID = go.GetInstanceID() })
                    .ToList();
                return ApiResult<List<ObjectSearchResult>>.Success(matches);
            });
        }

        public ApiResult<List<ObjectSearchResult>> FindByType(string typeName, bool includeInactive)
        {
            return _dispatcher.Invoke(() =>
            {
                var type = TypeResolver.Resolve(typeName);
                if (type == null)
                {
                    return ApiResult<List<ObjectSearchResult>>.Failure($"Type '{typeName}' was not found.");
                }

                if (typeof(GameObject).IsAssignableFrom(type))
                {
                    var gameObjects = Resources.FindObjectsOfTypeAll<GameObject>()
                        .Where(IsSceneObject)
                        .Where(go => includeInactive || go.activeInHierarchy)
                        .Select(go => new ObjectSearchResult { Kind = "GameObject", Name = go.name, InstanceID = go.GetInstanceID() })
                        .ToList();
                    return ApiResult<List<ObjectSearchResult>>.Success(gameObjects);
                }

                if (!typeof(UnityEngine.Object).IsAssignableFrom(type))
                {
                    return ApiResult<List<ObjectSearchResult>>.Failure($"Type '{type.FullName}' is not a UnityEngine.Object type.");
                }

                var components = Resources.FindObjectsOfTypeAll(type)
                    .OfType<UnityEngine.Object>()
                    .Where(IsSceneObject)
                    .Where(obj => includeInactive || !IsInactive(obj))
                    .Select(obj => new ObjectSearchResult
                    {
                        Kind = obj is Component ? "Component" : "Object",
                        Type = obj.GetType().FullName,
                        InstanceID = obj.GetInstanceID(),
                        GameObject = obj is Component component ? new GameObjectRef { Name = component.gameObject.name, InstanceID = component.gameObject.GetInstanceID() } : null
                    })
                    .ToList();
                return ApiResult<List<ObjectSearchResult>>.Success(components);
            });
        }

        public ApiResult<ValueInfo> GetValue(string objectId, string path, bool storeResult)
        {
            return _dispatcher.Invoke(() =>
            {
                if (!_registry.TryResolve(objectId, out var root, out var error))
                {
                    return ApiResult<ValueInfo>.Failure(error);
                }

                var access = ReflectionPath.Resolve(root, path, requireAssignableTarget: false);
                if (!access.Success)
                {
                    return ApiResult<ValueInfo>.Failure(access.Error);
                }

                return ApiResult<ValueInfo>.Success(SerializeValue(access.Value, storeResult));
            });
        }

        public ApiResult<ValueInfo> SetValue(string objectId, string path, string valueJson, bool storeResult)
        {
            return _dispatcher.Invoke(() =>
            {
                if (!_registry.TryResolve(objectId, out var root, out var error))
                {
                    return ApiResult<ValueInfo>.Failure(error);
                }

                var access = ReflectionPath.Resolve(root, path, requireAssignableTarget: true);
                if (!access.Success)
                {
                    return ApiResult<ValueInfo>.Failure(access.Error);
                }

                if (access.MemberType == null || !ValueConverter.IsSettableSimple(access.MemberType))
                {
                    return ApiResult<ValueInfo>.Failure("Only primitive, string, enum, DateTime, decimal, Guid and Unity primitive structs can be set.");
                }

                if (!ValueConverter.TryConvert(valueJson, access.MemberType, out var converted, out var convertError))
                {
                    return ApiResult<ValueInfo>.Failure(convertError);
                }

                if (!access.SetValue(converted))
                {
                    return ApiResult<ValueInfo>.Failure("The target path is not assignable.");
                }

                return ApiResult<ValueInfo>.Success(SerializeValue(converted, storeResult));
            });
        }

        public ApiResult<ValueInfo> CallMethod(string objectId, string path, string argumentsJson, bool storeResult)
        {
            return _dispatcher.Invoke(() =>
            {
                object? target = null;
                Type? staticType = null;
                string methodPath = path;

                if (!string.IsNullOrWhiteSpace(objectId))
                {
                    if (!_registry.TryResolve(objectId, out target, out var error))
                    {
                        return ApiResult<ValueInfo>.Failure(error);
                    }

                    var instanceSplit = SplitInstanceMethodPath(path);
                    if (!string.IsNullOrEmpty(instanceSplit.targetPath))
                    {
                        var targetAccess = ReflectionPath.Resolve(target, instanceSplit.targetPath, requireAssignableTarget: false);
                        if (!targetAccess.Success)
                        {
                            return ApiResult<ValueInfo>.Failure(targetAccess.Error);
                        }

                        if (targetAccess.Value == null)
                        {
                            return ApiResult<ValueInfo>.Failure($"Method target path '{instanceSplit.targetPath}' evaluated to null.");
                        }

                        target = targetAccess.Value;
                        methodPath = instanceSplit.methodName;
                    }
                }
                else
                {
                    var split = SplitStaticPath(path);
                    if (split.type == null)
                    {
                        return ApiResult<ValueInfo>.Failure($"Static type '{split.typeName}' was not found.");
                    }
                    staticType = split.type;
                    methodPath = split.methodName;
                }

                if (!TryParseArguments(argumentsJson, out var arguments, out var argumentError))
                {
                    return ApiResult<ValueInfo>.Failure(argumentError);
                }

                var result = MethodInvoker.Invoke(target, staticType, methodPath, arguments, _registry);
                if (!result.Success)
                {
                    return ApiResult<ValueInfo>.Failure(result.Error);
                }

                return ApiResult<ValueInfo>.Success(SerializeValue(result.Value, storeResult));
            });
        }

        private static (string targetPath, string methodName) SplitInstanceMethodPath(string path)
        {
            var lastDot = LastDotOutsideGeneric(path);
            if (lastDot <= 0 || lastDot >= path.Length - 1)
            {
                return (string.Empty, path);
            }

            return (path.Substring(0, lastDot), path.Substring(lastDot + 1));
        }

        private static (Type? type, string typeName, string methodName) SplitStaticPath(string path)
        {
            for (var dot = LastDotOutsideGeneric(path); dot > 0; dot = LastDotOutsideGeneric(path, dot - 1))
            {
                var typeName = path.Substring(0, dot);
                var type = TypeResolver.Resolve(typeName);
                if (type != null)
                {
                    return (type, typeName, path.Substring(dot + 1));
                }
            }

            return (null, path, string.Empty);
        }

        private static int LastDotOutsideGeneric(string value, int startIndex = -1)
        {
            var depth = 0;
            var start = startIndex >= 0 ? Math.Min(startIndex, value.Length - 1) : value.Length - 1;
            for (var i = start; i >= 0; i--)
            {
                var ch = value[i];
                if (ch == '>')
                {
                    depth++;
                }
                else if (ch == '<')
                {
                    depth--;
                }
                else if (ch == '.' && depth == 0)
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool TryParseArguments(string argumentsJson, out JToken[] arguments, out string error)
        {
            arguments = Array.Empty<JToken>();
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(argumentsJson))
            {
                return true;
            }

            try
            {
                var token = JToken.Parse(argumentsJson);
                if (token is not JArray array)
                {
                    error = "argumentsJson must be a JSON array.";
                    return false;
                }

                arguments = array.ToArray();
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private ValueInfo SerializeValue(object? value, bool storeResult)
        {
            if (value == null)
            {
                return new ValueInfo { Kind = "Null", Value = null };
            }

            var valueType = value.GetType();
            if (ValueConverter.IsSimpleReadable(valueType))
            {
                return new ValueInfo { Kind = "Primitive", Type = valueType.FullName, Value = SerializeSimpleValue(value) };
            }

            if (value is UnityEngine.Object unityObject)
            {
                var summary = new ValueInfo
                {
                    Kind = unityObject is Component ? "Component" : unityObject is GameObject ? "GameObject" : "UnityObject",
                    Type = unityObject.GetType().FullName,
                    InstanceID = unityObject.GetInstanceID()
                };
                if (unityObject is GameObject go)
                {
                    summary.Name = go.name;
                }
                else if (unityObject is Component component)
                {
                    summary.GameObject = new GameObjectRef { Name = component.gameObject.name, InstanceID = component.gameObject.GetInstanceID() };
                }

                if (storeResult)
                {
                    summary.Guid = _registry.Store(value);
                }

                return summary;
            }

            var result = new ValueInfo { Kind = "Object", Type = valueType.FullName };
            if (storeResult)
            {
                result.Guid = _registry.Store(value);
            }

            return result;
        }

        private static object? SerializeSimpleValue(object value)
        {
            if (value is Vector2 vector2)
            {
                return new VectorInfo { X = vector2.x, Y = vector2.y };
            }

            if (value is Vector3 vector3)
            {
                return new VectorInfo { X = vector3.x, Y = vector3.y, Z = vector3.z };
            }

            if (value is Vector4 vector4)
            {
                return new VectorInfo { X = vector4.x, Y = vector4.y, Z = vector4.z, W = vector4.w };
            }

            if (value is Quaternion quaternion)
            {
                return new VectorInfo { X = quaternion.x, Y = quaternion.y, Z = quaternion.z, W = quaternion.w };
            }

            if (value is Color color)
            {
                return new ColorInfo { R = color.r, G = color.g, B = color.b, A = color.a };
            }

            return value;
        }

        private static GameObjectNode CreateGameObjectNode(GameObject gameObject)
        {
            return new GameObjectNode
            {
                Name = gameObject.name,
                InstanceID = gameObject.GetInstanceID(),
                Components = GetComponentInfos(gameObject),
                Children = Enumerable.Range(0, gameObject.transform.childCount)
                    .Select(i => CreateGameObjectNode(gameObject.transform.GetChild(i).gameObject))
                    .ToList()
            };
        }

        private static List<ComponentInfo> GetComponentInfos(GameObject gameObject)
        {
            return gameObject.GetComponents<Component>()
                .Where(component => component != null)
                .Select(component => new ComponentInfo { Type = component.GetType().FullName, InstanceID = component.GetInstanceID() })
                .ToList();
        }

        private static bool IsInactive(UnityEngine.Object obj)
        {
            if (obj is GameObject go)
            {
                return !go.activeInHierarchy;
            }

            if (obj is Component component)
            {
                return !component.gameObject.activeInHierarchy;
            }

            return false;
        }

        private static bool IsSceneObject(UnityEngine.Object obj)
        {
            if (obj is GameObject go)
            {
                return go.scene.IsValid();
            }

            if (obj is Component component)
            {
                return component.gameObject.scene.IsValid();
            }

            return true;
        }

        public string Test(string input)
        {
            return input;
        }

        private sealed class MainThreadDispatcher
        {
            private readonly ConcurrentQueue<Action> _actions = new ConcurrentQueue<Action>();

            public T Invoke<T>(Func<T> action)
            {
                var tcs = new TaskCompletionSource<T>();
                _actions.Enqueue(() =>
                {
                    try
                    {
                        tcs.SetResult(action());
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(ex);
                    }
                });
                return tcs.Task.GetAwaiter().GetResult();
            }

            public void Drain()
            {
                while (_actions.TryDequeue(out var action))
                {
                    action();
                }
            }
        }

        private sealed class ObjectRegistry
        {
            private readonly Dictionary<string, object> _storedObjects = new Dictionary<string, object>();

            public string Store(object value)
            {
                var guid = Guid.NewGuid().ToString("N");
                _storedObjects[guid] = value;
                return guid;
            }

            public bool TryResolve(string objectId, out object? value, out string error)
            {
                value = null;
                error = string.Empty;

                if (string.IsNullOrWhiteSpace(objectId))
                {
                    error = "objectId is required for instance access.";
                    return false;
                }

                if (_storedObjects.TryGetValue(objectId, out value))
                {
                    return true;
                }

                if (!int.TryParse(objectId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var instanceId))
                {
                    error = $"'{objectId}' is neither a stored GUID nor a Unity instance ID.";
                    return false;
                }

                value = Resources.FindObjectsOfTypeAll<UnityEngine.Object>().FirstOrDefault(obj => obj.GetInstanceID() == instanceId);
                if (value == null)
                {
                    error = $"Unity object with instance ID {instanceId} was not found.";
                    return false;
                }

                return true;
            }
        }

        private static class TypeResolver
        {
            public static Type? Resolve(string typeName)
            {
                if (string.IsNullOrWhiteSpace(typeName))
                {
                    return null;
                }

                return Type.GetType(typeName) ??
                    AppDomain.CurrentDomain.GetAssemblies()
                        .Select(assembly => assembly.GetType(typeName, throwOnError: false))
                        .FirstOrDefault(type => type != null) ??
                    AppDomain.CurrentDomain.GetAssemblies()
                        .SelectMany(SafeGetTypes)
                        .FirstOrDefault(type => type.FullName == typeName || type.Name == typeName);
            }

            private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
            {
                try
                {
                    return assembly.GetTypes();
                }
                catch
                {
                    return Array.Empty<Type>();
                }
            }
        }

        private sealed class PathAccess
        {
            public bool Success { get; set; }
            public string Error { get; set; } = string.Empty;
            public object? Value { get; set; }
            public Type? MemberType { get; set; }
            public Func<object?, bool> SetValue { get; set; } = _ => false;
        }

        private static class ReflectionPath
        {
            private static readonly Regex SegmentRegex = new Regex(@"^(?<name>[^\[\]]+)(?<indexes>(\[[^\]]+\])*)$", RegexOptions.Compiled);

            public static PathAccess Resolve(object? root, string path, bool requireAssignableTarget)
            {
                if (root == null)
                {
                    return Fail("Root object is null.");
                }

                if (string.IsNullOrWhiteSpace(path))
                {
                    return new PathAccess { Success = true, Value = root, MemberType = root.GetType() };
                }

                object? current = root;
                Type currentType = root is Type type ? type : root.GetType();
                var segments = path.Split('.');
                PathAccess? resolved = null;

                for (var i = 0; i < segments.Length; i++)
                {
                    var last = i == segments.Length - 1;
                    var segment = segments[i];
                    resolved = ResolveSegment(current, currentType, segment, last && requireAssignableTarget);
                    if (!resolved.Success)
                    {
                        return resolved;
                    }

                    current = resolved.Value;
                    currentType = current?.GetType() ?? resolved.MemberType ?? typeof(object);

                    if (!last && current == null)
                    {
                        return Fail($"Path segment '{segment}' evaluated to null.");
                    }
                }

                return resolved ?? new PathAccess { Success = true, Value = current, MemberType = currentType };
            }

            private static PathAccess ResolveSegment(object? current, Type currentType, string segment, bool assignable)
            {
                var match = SegmentRegex.Match(segment);
                if (!match.Success)
                {
                    return Fail($"Invalid path segment '{segment}'.");
                }

                var name = match.Groups["name"].Value;
                var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
                var property = currentType.GetProperty(name, flags);
                var field = currentType.GetField(name, flags);

                object? value;
                Type? valueType;
                Func<object?, bool> setter = _ => false;

                if (property != null)
                {
                    value = property.GetValue(property.GetGetMethod(true)?.IsStatic == true ? null : current, null);
                    valueType = property.PropertyType;
                    setter = newValue =>
                    {
                        if (!property.CanWrite)
                        {
                            return false;
                        }
                        property.SetValue(property.GetSetMethod(true)?.IsStatic == true ? null : current, newValue, null);
                        return true;
                    };
                }
                else if (field != null)
                {
                    value = field.GetValue(field.IsStatic ? null : current);
                    valueType = field.FieldType;
                    setter = newValue =>
                    {
                        field.SetValue(field.IsStatic ? null : current, newValue);
                        return true;
                    };
                }
                else
                {
                    return Fail($"Member '{name}' was not found on '{currentType.FullName}'.");
                }

                var indexText = match.Groups["indexes"].Value;
                if (!string.IsNullOrEmpty(indexText))
                {
                    foreach (Match indexMatch in Regex.Matches(indexText, @"\[([^\]]+)\]"))
                    {
                        if (value == null)
                        {
                            return Fail($"Indexed target '{name}' is null.");
                        }

                        if (!TryGetIndexed(value, indexMatch.Groups[1].Value, out value, out valueType, out var indexedSetter, out var indexError))
                        {
                            return Fail(indexError);
                        }

                        setter = indexedSetter;
                    }
                }

                return new PathAccess { Success = true, Value = value, MemberType = valueType, SetValue = setter };
            }

            private static bool TryGetIndexed(object target, string indexToken, out object? value, out Type? valueType, out Func<object?, bool> setter, out string error)
            {
                value = null;
                valueType = null;
                setter = _ => false;
                error = string.Empty;

                if (!int.TryParse(indexToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
                {
                    error = $"Only integer indexers are currently supported. Invalid index '{indexToken}'.";
                    return false;
                }

                if (target is Array array)
                {
                    value = array.GetValue(index);
                    valueType = target.GetType().GetElementType();
                    setter = newValue =>
                    {
                        array.SetValue(newValue, index);
                        return true;
                    };
                    return true;
                }

                if (target is IList list)
                {
                    value = list[index];
                    valueType = target.GetType().IsGenericType ? target.GetType().GetGenericArguments().FirstOrDefault() ?? typeof(object) : value?.GetType() ?? typeof(object);
                    setter = newValue =>
                    {
                        list[index] = newValue;
                        return true;
                    };
                    return true;
                }

                var indexer = target.GetType().GetDefaultMembers().OfType<PropertyInfo>().FirstOrDefault();
                if (indexer != null)
                {
                    value = indexer.GetValue(target, new object[] { index });
                    valueType = indexer.PropertyType;
                    setter = newValue =>
                    {
                        if (!indexer.CanWrite)
                        {
                            return false;
                        }

                        indexer.SetValue(target, newValue, new object[] { index });
                        return true;
                    };
                    return true;
                }

                error = $"Object of type '{target.GetType().FullName}' is not indexable.";
                return false;
            }

            private static PathAccess Fail(string error)
            {
                return new PathAccess { Success = false, Error = error };
            }
        }

        private sealed class InvokeResult
        {
            public bool Success { get; set; }
            public string Error { get; set; } = string.Empty;
            public object? Value { get; set; }
        }

        private static class MethodInvoker
        {
            private static readonly Regex GenericRegex = new Regex(@"^(?<name>[^<]+)<(?<types>.+)>$", RegexOptions.Compiled);

            public static InvokeResult Invoke(object? target, Type? staticType, string methodPath, JToken[] arguments, ObjectRegistry registry)
            {
                var targetType = staticType ?? target?.GetType();
                if (targetType == null)
                {
                    return Error("No target type could be resolved.");
                }

                var genericTypes = Array.Empty<Type>();
                var methodName = methodPath;
                var genericMatch = GenericRegex.Match(methodPath);
                if (genericMatch.Success)
                {
                    methodName = genericMatch.Groups["name"].Value;
                    genericTypes = SplitGenericTypeNames(genericMatch.Groups["types"].Value)
                        .Select(t => TypeResolver.Resolve(t.Trim()))
                        .ToArray()!;
                    if (genericTypes.Any(t => t == null))
                    {
                        return Error("One or more generic argument types could not be resolved.");
                    }
                }

                var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
                var candidates = targetType.GetMethods(flags)
                    .Where(method => method.Name == methodName)
                    .Where(method => method.IsStatic == (staticType != null))
                    .Where(method => method.GetParameters().Length == arguments.Length)
                    .ToList();

                foreach (var candidate in candidates)
                {
                    var method = candidate;
                    if (genericTypes.Length > 0)
                    {
                        if (!candidate.IsGenericMethodDefinition || candidate.GetGenericArguments().Length != genericTypes.Length)
                        {
                            continue;
                        }
                        method = candidate.MakeGenericMethod(genericTypes!);
                    }

                    if (TryBuildArguments(method.GetParameters(), arguments, registry, out var convertedArguments))
                    {
                        try
                        {
                            return new InvokeResult { Success = true, Value = method.Invoke(method.IsStatic ? null : target, convertedArguments) };
                        }
                        catch (Exception ex)
                        {
                            return Error(ex.InnerException?.Message ?? ex.Message);
                        }
                    }
                }

                return Error($"No compatible method '{methodPath}' was found on '{targetType.FullName}'.");
            }

            private static IEnumerable<string> SplitGenericTypeNames(string value)
            {
                var depth = 0;
                var start = 0;
                for (var i = 0; i < value.Length; i++)
                {
                    if (value[i] == '<')
                    {
                        depth++;
                    }
                    else if (value[i] == '>')
                    {
                        depth--;
                    }
                    else if (value[i] == ',' && depth == 0)
                    {
                        yield return value.Substring(start, i - start);
                        start = i + 1;
                    }
                }

                yield return value.Substring(start);
            }

            private static bool TryBuildArguments(ParameterInfo[] parameters, JToken[] arguments, ObjectRegistry registry, out object?[] converted)
            {
                converted = new object?[parameters.Length];
                for (var i = 0; i < parameters.Length; i++)
                {
                    if (!ValueConverter.TryConvertArgument(arguments[i], parameters[i].ParameterType, registry, out converted[i]))
                    {
                        return false;
                    }
                }

                return true;
            }

            private static InvokeResult Error(string error)
            {
                return new InvokeResult { Success = false, Error = error };
            }
        }

        private static class ValueConverter
        {
            public static bool IsSimpleReadable(Type type)
            {
                return IsSettableSimple(type);
            }

            public static bool IsSettableSimple(Type type)
            {
                return IsSupportedPrimitive(type) ||
                    type == typeof(Vector2) ||
                    type == typeof(Vector3) ||
                    type == typeof(Vector4) ||
                    type == typeof(Quaternion) ||
                    type == typeof(Color);
            }

            public static bool IsSupportedPrimitive(Type type)
            {
                type = Nullable.GetUnderlyingType(type) ?? type;
                return type.IsPrimitive ||
                    type.IsEnum ||
                    type == typeof(string) ||
                    type == typeof(decimal) ||
                    type == typeof(DateTime) ||
                    type == typeof(Guid);
            }

            public static bool TryConvert(string valueJson, Type targetType, out object? value, out string error)
            {
                value = null;
                error = string.Empty;

                try
                {
                    var token = JToken.Parse(valueJson);
                    return TryConvertJson(token, targetType, null, out value, out error);
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    return false;
                }
            }

            public static bool TryConvertArgument(JToken element, Type targetType, ObjectRegistry registry, out object? value)
            {
                return TryConvertJson(element, targetType, registry, out value, out _);
            }

            private static bool TryConvertJson(JToken element, Type targetType, ObjectRegistry? registry, out object? value, out string error)
            {
                value = null;
                error = string.Empty;

                var nullable = Nullable.GetUnderlyingType(targetType);
                if (nullable != null)
                {
                    if (element.Type == JTokenType.Null)
                    {
                        return true;
                    }
                    targetType = nullable;
                }

                try
                {
                    if (targetType == typeof(string))
                    {
                        value = element.Type == JTokenType.String ? element.Value<string>() : element.ToString(Formatting.None);
                    }
                    else if (targetType == typeof(int))
                    {
                        value = element.Value<int>();
                    }
                    else if (targetType == typeof(long))
                    {
                        value = element.Value<long>();
                    }
                    else if (targetType == typeof(float))
                    {
                        value = element.Value<float>();
                    }
                    else if (targetType == typeof(double))
                    {
                        value = element.Value<double>();
                    }
                    else if (targetType == typeof(bool))
                    {
                        value = element.Value<bool>();
                    }
                    else if (targetType.IsEnum)
                    {
                        value = element.Type == JTokenType.String
                            ? Enum.Parse(targetType, element.Value<string>(), ignoreCase: true)
                            : Enum.ToObject(targetType, element.Value<int>());
                    }
                    else if (targetType == typeof(DateTime))
                    {
                        value = element.Value<DateTime>();
                    }
                    else if (targetType == typeof(decimal))
                    {
                        value = element.Value<decimal>();
                    }
                    else if (targetType == typeof(Guid))
                    {
                        value = Guid.Parse(element.Value<string>() ?? element.ToString(Formatting.None));
                    }
                    else if (targetType == typeof(Type))
                    {
                        var typeName = element.Type == JTokenType.String ? element.Value<string>() : element.ToString(Formatting.None);
                        value = TypeResolver.Resolve(typeName ?? string.Empty);
                        if (value == null)
                        {
                            error = $"Type '{typeName}' was not found.";
                            return false;
                        }
                    }
                    else if (targetType == typeof(Vector2))
                    {
                        value = new Vector2(GetSingle(element, "x"), GetSingle(element, "y"));
                    }
                    else if (targetType == typeof(Vector3))
                    {
                        value = new Vector3(GetSingle(element, "x"), GetSingle(element, "y"), GetSingle(element, "z"));
                    }
                    else if (targetType == typeof(Vector4))
                    {
                        value = new Vector4(GetSingle(element, "x"), GetSingle(element, "y"), GetSingle(element, "z"), GetSingle(element, "w"));
                    }
                    else if (targetType == typeof(Quaternion))
                    {
                        value = new Quaternion(GetSingle(element, "x"), GetSingle(element, "y"), GetSingle(element, "z"), GetSingle(element, "w"));
                    }
                    else if (targetType == typeof(Color))
                    {
                        value = new Color(GetSingle(element, "r"), GetSingle(element, "g"), GetSingle(element, "b"), element["a"]?.Value<float>() ?? 1f);
                    }
                    else if (registry != null && typeof(UnityEngine.Object).IsAssignableFrom(targetType))
                    {
                        var id = element.Type == JTokenType.String ? element.Value<string>() : element.ToString(Formatting.None);
                        if (string.IsNullOrEmpty(id) || !registry.TryResolve(id, out value, out _) || value == null || !targetType.IsInstanceOfType(value))
                        {
                            return false;
                        }
                    }
                    else if (registry != null && element.Type == JTokenType.String && !string.IsNullOrEmpty(element.Value<string>()) && registry.TryResolve(element.Value<string>()!, out value, out _))
                    {
                        return value == null || targetType.IsInstanceOfType(value);
                    }
                    else
                    {
                        value = element.ToObject(targetType);
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    return false;
                }
            }

            private static float GetSingle(JToken element, string propertyName)
            {
                return element[propertyName]!.Value<float>();
            }
        }
    }
}
