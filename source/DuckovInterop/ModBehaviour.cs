using EleCho.JsonRpc;
using Jint;
using ModSetting.Api;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace SlimeNull.DuckovInterop
{
    internal sealed class ModBehaviour : Duckov.Modding.ModBehaviour, IHierarchyInspectorRpc
    {
        private readonly MainThreadDispatcher _dispatcher = new MainThreadDispatcher();
        private readonly ObjectRegistry _registry = new ObjectRegistry();
        private volatile bool _stopping;
        private readonly List<TcpClient> _clients = new List<TcpClient>();
        private readonly object _clientsLock = new object();
        private TcpListener? _listener;
        private Thread? _serverTask;
        private Engine? _jsEngine;
        private bool _serverEnabled = true;
        private volatile bool _diagnosticLogging;
        private string _listenHost = HierarchyInspectorRpcEndpoint.Host;
        private int _listenPort = HierarchyInspectorRpcEndpoint.Port;

        protected override void OnAfterSetup()
        {
            try
            {
                _jsEngine = new Engine((Options cfg) => cfg.AllowClr());
                ConfigureSettings();
                if (_serverEnabled)
                {
                    StartServer();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError(ex);
            }
        }

        protected override void OnBeforeDeactivate()
        {
            StopServer();

            _jsEngine?.Dispose();
            _jsEngine = null;
        }

        private void ConfigureSettings()
        {
            var builder = SettingsBuilder.Create(info) ?? throw new InvalidOperationException("ModSetting is not available.");
            _serverEnabled = Load(builder, "Server.Enabled", _serverEnabled);
            _diagnosticLogging = Load(builder, "Server.DiagnosticLogging", _diagnosticLogging);

            var savedHost = Load(builder, "Server.Host", _listenHost);
            if (IPAddress.TryParse(savedHost, out _))
            {
                _listenHost = savedHost;
            }
            else
            {
                Debug.LogError($"[DuckovInterop] Ignoring invalid saved listen address '{savedHost}'.");
            }

            _listenPort = Load(builder, "Server.Port", _listenPort);

            builder
                .AddToggle("Server.Enabled", "启用 JSON RPC 服务", _serverEnabled, SetServerEnabled)
                .AddInput("Server.Host", "监听地址（重新启用服务后生效）", _listenHost, 45, value =>
                {
                    if (IPAddress.TryParse(value, out _))
                    {
                        _listenHost = value;
                    }
                    else
                    {
                        Debug.LogError($"[DuckovInterop] Invalid listen address '{value}'.");
                    }
                })
                .AddSlider("Server.Port", "监听端口（重新启用服务后生效）", _listenPort, 1024, 65535, value => _listenPort = value, 5)
                .AddToggle("Server.DiagnosticLogging", "输出 RPC 诊断日志", _diagnosticLogging, value => _diagnosticLogging = value)
                .AddGroup("Server.Group", "JSON RPC 服务", new List<string>
                {
                    "Server.Enabled",
                    "Server.Host",
                    "Server.Port",
                    "Server.DiagnosticLogging"
                });
        }

        private static T Load<T>(SettingsBuilder builder, string key, T fallback)
        {
            return builder.GetSavedValue<T>(key, out var value) ? value : fallback;
        }

        private void SetServerEnabled(bool enabled)
        {
            _serverEnabled = enabled;
            if (enabled)
            {
                StartServer();
            }
            else
            {
                StopServer();
            }
        }

        private void StartServer()
        {
            if (_serverTask != null)
            {
                return;
            }

            _stopping = false;
            _serverTask = new Thread(RunServerLoop)
            {
                IsBackground = true
            };
            _serverTask.Start();
        }

        private void StopServer()
        {
            if (_serverTask == null && _listener == null)
            {
                return;
            }

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

        void Update()
        {
            _dispatcher.Drain();
        }

        private void RunServerLoop()
        {
            var host = _listenHost;
            var port = _listenPort;
            LogDiagnostic("Enter server loop");

            try
            {
                _listener = new TcpListener(IPAddress.Parse(host), port);
                _listener.Start();
                Debug.Log($"[DuckovInterop] RPC TCP listener started at {host}:{port}");

                while (!_stopping)
                {
                    var client = _listener.AcceptTcpClient();
                    TrackClient(client);
                    LogDiagnostic("RPC client connected");

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
                Debug.LogError($"[DuckovInterop] RPC listener error: {ex}");
            }
            finally
            {
                _listener = null;
                LogDiagnostic("RPC listener stopped");
            }
        }

        private void RunClient(TcpClient client)
        {
            try
            {
                using (client)
                using (var stream = client.GetStream())
                using (var server = new RpcServer<IHierarchyInspectorRpc>(stream, this)
                {
                    DiagnosticLogger = message =>
                    {
                        if (_diagnosticLogging)
                        {
                            Debug.Log($"[DuckovInterop/RPC] {message}");
                        }
                    }
                })
                {
                    server.Run();
                }
            }
            catch (Exception ex)
            {
                if (!_stopping)
                {
                    Debug.LogError($"[DuckovInterop] RPC client error: {ex}");
                }
            }
            finally
            {
                UntrackClient(client);
                LogDiagnostic("RPC client disconnected");
            }
        }

        private void LogDiagnostic(string message)
        {
            if (_diagnosticLogging)
            {
                Debug.Log($"[DuckovInterop] {message}");
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

        public ApiResult<SceneSnapshot> GetSceneSnapshot()
        {
            return BuildSceneSnapshot(includeFields: true);
        }

        public ApiResult<SceneSnapshot> GetSceneOverview()
        {
            return BuildSceneSnapshot(includeFields: false);
        }

        private ApiResult<SceneSnapshot> BuildSceneSnapshot(bool includeFields)
        {
            var requestStopwatch = Stopwatch.StartNew();
            var metrics = new SnapshotMetrics(requestStopwatch);
            LogDiagnostic(
                $"Snapshot request queued for the Unity main thread; " +
                $"fields={(includeFields ? "included" : "deferred")}.");

            using (var progressTimer = new Timer(
                _ => LogDiagnostic($"Snapshot still working: {metrics.Describe()}"),
                null,
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(5)))
            {
                return _dispatcher.Invoke(() =>
                {
                    metrics.Stage = "building snapshot";
                    LogDiagnostic(
                        $"Snapshot main-thread build started after " +
                        $"{requestStopwatch.ElapsedMilliseconds} ms in the queue.");

                    try
                    {
                        _registry.ResetUnityObjects();
                        var scenes = new List<InspectorScene>();
                        for (var i = 0; i < SceneManager.sceneCount; i++)
                        {
                            var scene = SceneManager.GetSceneAt(i);
                            if (!scene.IsValid() || !scene.isLoaded)
                                continue;

                            metrics.Scene = scene.name;
                            metrics.Stage = "reading scene";
                            LogDiagnostic($"Snapshot reading scene '{scene.name}'.");

                            var roots = scene.GetRootGameObjects();
                            scenes.Add(new InspectorScene
                            {
                                Name = scene.name,
                                BuildIndex = scene.buildIndex,
                                Roots = roots.Select(root => CreateInspectorGameObject(root, metrics, includeFields)).ToList()
                            });
                            metrics.Scenes++;
                            LogDiagnostic(
                                $"Snapshot scene '{scene.name}' completed: " +
                                $"roots={roots.Length}, {metrics.Describe()}.");
                        }

                        metrics.Stage = "snapshot built";
                        LogDiagnostic($"Snapshot build completed: {metrics.Describe()}.");
                        return ApiResult<SceneSnapshot>.Success(new SceneSnapshot
                        {
                            CapturedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                            Scenes = scenes
                        });
                    }
                    catch (Exception ex)
                    {
                        metrics.Stage = "build failed";
                        Debug.LogError($"[DuckovInterop/Snapshot] Build failed: {metrics.Describe()}. {ex}");
                        throw;
                    }
                });
            }
        }

        public ApiResult<List<InspectorComponent>> GetInspectorComponents(string gameObjectId)
        {
            var stopwatch = Stopwatch.StartNew();
            return _dispatcher.Invoke(() =>
            {
                if (!_registry.TryResolve(gameObjectId, out var root, out var error))
                {
                    return ApiResult<List<InspectorComponent>>.Failure(error);
                }

                if (root is not GameObject gameObject)
                {
                    return ApiResult<List<InspectorComponent>>.Failure($"Object '{gameObjectId}' is not a GameObject.");
                }

                var metrics = new SnapshotMetrics(stopwatch);
                metrics.VisitGameObject(gameObject.name);
                var unityComponents = gameObject.GetComponents<Component>();
                foreach (var component in unityComponents)
                {
                    if (component != null)
                    {
                        _registry.Remember(component);
                    }
                }

                var components = unityComponents
                    .Select(component => CreateInspectorComponent(component, metrics, includeFields: true))
                    .ToList();
                LogDiagnostic(
                    $"Snapshot loaded details for GameObject '{gameObject.name}' in " +
                    $"{stopwatch.ElapsedMilliseconds} ms: components={metrics.Components}, fields={metrics.Fields}.");
                return ApiResult<List<InspectorComponent>>.Success(components);
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

        public ApiResult<bool> SetGameObjectActive(string gameObjectId, bool active)
        {
            return _dispatcher.Invoke(() =>
            {
                if (!_registry.TryResolve(gameObjectId, out var root, out var error))
                {
                    return ApiResult<bool>.Failure(error);
                }

                if (root is not GameObject gameObject)
                {
                    return ApiResult<bool>.Failure($"Object '{gameObjectId}' is not a GameObject.");
                }

                gameObject.SetActive(active);
                return ApiResult<bool>.Success(gameObject.activeSelf);
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

        public ApiResult<ValueInfo> JintEvaluate(string script, bool storeResult)
        {
            return _dispatcher.Invoke(() =>
            {
                try
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var result = _jsEngine!.Evaluate(script);
                    LogDiagnostic($"Jint evaluation completed in {sw.ElapsedMilliseconds} ms");

                    if (result is Jint.Runtime.Interop.ObjectWrapper wrapper)
                    {
                        // returns CLR object
                        return ApiResult<ValueInfo>.Success(SerializeValue(wrapper.Target, storeResult));
                    }
                    else
                    {
                        // returns Jint value (primitive, array, object, etc.)
                        return ApiResult<ValueInfo>.Success(SerializeValue(result.ToObject(), storeResult));
                    }
                }
                catch (Exception ex)
                {
                    return ApiResult<ValueInfo>.Failure($"JavaScript execution error: {ex.Message}");
                }
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

                if (!ValueConverter.TryConvert(valueJson, access.MemberType, _registry, out var converted, out var convertError))
                {
                    return ApiResult<ValueInfo>.Failure(convertError);
                }

                try
                {
                    if (!access.SetValue(converted))
                    {
                        return ApiResult<ValueInfo>.Failure("The target path is not assignable.");
                    }
                }
                catch (Exception ex)
                {
                    return ApiResult<ValueInfo>.Failure(ex.GetBaseException().Message);
                }

                var updated = ReflectionPath.Resolve(root, path, requireAssignableTarget: false);
                return updated.Success
                    ? ApiResult<ValueInfo>.Success(SerializeValue(updated.Value, storeResult))
                    : ApiResult<ValueInfo>.Failure(updated.Error);
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
                HasRenderer = gameObject.GetComponent<Renderer>() != null,
                IsGUI = gameObject.transform is RectTransform,
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

        private InspectorGameObject CreateInspectorGameObject(GameObject gameObject, SnapshotMetrics metrics, bool includeFields)
        {
            metrics.VisitGameObject(gameObject.name);
            _registry.Remember(gameObject);
            var components = gameObject.GetComponents<Component>();
            List<InspectorComponent> inspectorComponents;
            if (includeFields)
            {
                inspectorComponents = components
                    .Select(component => CreateInspectorComponent(component, metrics, includeFields: true))
                    .ToList();
            }
            else
            {
                metrics.VisitComponents(components.Length);
                inspectorComponents = new List<InspectorComponent>();
            }

            string tag;
            try
            {
                tag = gameObject.tag;
            }
            catch
            {
                tag = "Untagged";
            }

            return new InspectorGameObject
            {
                Name = gameObject.name,
                InstanceID = gameObject.GetInstanceID(),
                ActiveSelf = gameObject.activeSelf,
                ActiveInHierarchy = gameObject.activeInHierarchy,
                HasRenderer = components.Any(component => component is Renderer),
                IsGUI = gameObject.transform is RectTransform,
                Tag = tag,
                Layer = gameObject.layer,
                ComponentCount = components.Length,
                Components = inspectorComponents,
                Children = Enumerable.Range(0, gameObject.transform.childCount)
                    .Select(i => CreateInspectorGameObject(gameObject.transform.GetChild(i).gameObject, metrics, includeFields))
                    .ToList()
            };
        }

        private static InspectorComponent CreateInspectorComponent(Component component, SnapshotMetrics metrics, bool includeFields)
        {
            metrics.VisitComponent(component == null ? "Missing Script" : component.GetType().FullName ?? component.GetType().Name);
            if (component == null)
            {
                return new InspectorComponent { Name = "Missing Script", Type = "Missing", Error = "The referenced script could not be loaded." };
            }

            var type = component.GetType();
            var result = new InspectorComponent
            {
                Name = NicifyName(type.Name),
                Type = type.FullName,
                InstanceID = component.GetInstanceID(),
                Enabled = TryGetEnabled(component)
            };

            if (includeFields)
            {
                try
                {
                    result.Fields = GetSerializedFields(component);
                    metrics.Fields += result.Fields.Count;
                }
                catch (Exception ex)
                {
                    result.Error = ex.GetBaseException().Message;
                }
            }

            return result;
        }

        private static bool? TryGetEnabled(Component component)
        {
            try
            {
                var property = component.GetType().GetProperty("enabled", BindingFlags.Instance | BindingFlags.Public);
                return property != null && property.CanRead && property.CanWrite && property.PropertyType == typeof(bool)
                    ? (bool?)property.GetValue(component)
                    : null;
            }
            catch
            {
                return null;
            }
        }

        private static List<SerializedFieldInfo> GetSerializedFields(Component component)
        {
            var result = new List<SerializedFieldInfo>();
            var visited = new HashSet<object>(ReferenceComparer.Instance);

            if (component is Transform transform)
            {
                result.Add(CreateValueField("localPosition", "Position", typeof(Vector3), transform.localPosition, "localPosition", 0, visited));
                result.Add(CreateValueField("localEulerAngles", "Rotation", typeof(Vector3), transform.localEulerAngles, "localEulerAngles", 0, visited));
                result.Add(CreateValueField("localScale", "Scale", typeof(Vector3), transform.localScale, "localScale", 0, visited));
            }

            foreach (var field in EnumerateUnitySerializedFields(component.GetType()))
            {
                try
                {
                    var info = CreateValueField(field.Name, NicifyName(field.Name), field.FieldType, field.GetValue(component), field.Name, 0, visited);
                    ApplyInspectorAttributes(info, field);
                    result.Add(info);
                }
                catch (Exception ex)
                {
                    result.Add(new SerializedFieldInfo
                    {
                        Name = field.Name,
                        DisplayName = NicifyName(field.Name),
                        Type = field.FieldType.FullName,
                        Kind = "Error",
                        Value = ex.GetBaseException().Message
                    });
                }
            }

            var memberNames = new HashSet<string>(result.Select(field => field.Name ?? string.Empty), StringComparer.Ordinal);
            foreach (var property in EnumerateUnityNativeProperties(component.GetType()))
            {
                if (!memberNames.Add(property.Name))
                {
                    continue;
                }

                try
                {
                    var info = CreateValueField(property.Name, NicifyName(property.Name), property.PropertyType,
                        property.GetValue(component, null), property.Name, 0, visited, property.GetSetMethod(nonPublic: false) != null);
                    result.Add(info);
                }
                catch (Exception ex)
                {
                    result.Add(new SerializedFieldInfo
                    {
                        Name = property.Name,
                        DisplayName = NicifyName(property.Name),
                        Type = property.PropertyType.FullName,
                        Path = property.Name,
                        Kind = "Error",
                        Value = ex.GetBaseException().Message
                    });
                }
            }

            return result;
        }

        private static IEnumerable<PropertyInfo> EnumerateUnityNativeProperties(Type componentType)
        {
            var hierarchy = new Stack<Type>();
            for (var type = componentType; type != null && type != typeof(object); type = type.BaseType)
            {
                hierarchy.Push(type);
            }

            while (hierarchy.Count > 0)
            {
                foreach (var property in hierarchy.Pop().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .OrderBy(property => property.MetadataToken))
                {
                    if (property.GetGetMethod(nonPublic: false) != null &&
                        property.GetIndexParameters().Length == 0 &&
                        property.Name != "enabled" &&
                        HasNativePropertyAttribute(property) &&
                        IsUnitySerializableType(property.PropertyType))
                    {
                        yield return property;
                    }
                }
            }
        }

        private static bool HasNativePropertyAttribute(PropertyInfo property)
        {
            const string attributeName = "UnityEngine.Bindings.NativePropertyAttribute";
            return property.GetCustomAttributes(true).Any(attribute => attribute.GetType().FullName == attributeName) ||
                property.GetAccessors(true).Any(accessor => accessor.GetCustomAttributes(true).Any(attribute => attribute.GetType().FullName == attributeName));
        }

        private static IEnumerable<FieldInfo> EnumerateUnitySerializedFields(Type componentType)
        {
            var hierarchy = new Stack<Type>();
            for (var type = componentType; type != null && type != typeof(object); type = type.BaseType)
            {
                hierarchy.Push(type);
            }

            while (hierarchy.Count > 0)
            {
                foreach (var field in hierarchy.Pop().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .OrderBy(field => field.MetadataToken))
                {
                    if (IsUnitySerializedField(field))
                    {
                        yield return field;
                    }
                }
            }
        }

        private static bool IsUnitySerializedField(FieldInfo field)
        {
            if (field.IsStatic || field.IsLiteral || field.IsInitOnly || field.IsNotSerialized || HasAttribute(field, "UnityEngine.HideInInspector"))
            {
                return false;
            }

            var serializeReference = HasAttribute(field, "UnityEngine.SerializeReference");
            if (!field.IsPublic && !HasAttribute(field, "UnityEngine.SerializeField") && !serializeReference)
            {
                return false;
            }

            return serializeReference || IsUnitySerializableType(field.FieldType);
        }

        private static bool IsUnitySerializableType(Type type)
        {
            if (type.IsPrimitive || type.IsEnum || type == typeof(string) || typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                return true;
            }

            if (type.IsArray)
            {
                return type.GetArrayRank() == 1 && IsUnitySerializableType(type.GetElementType()!);
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                return IsUnitySerializableType(type.GetGenericArguments()[0]);
            }

            return type.IsSerializable;
        }

        private static SerializedFieldInfo CreateValueField(string name, string displayName, Type declaredType, object? value, string path, int depth, HashSet<object> visited, bool canWrite = true)
        {
            var info = new SerializedFieldInfo
            {
                Name = name,
                DisplayName = displayName,
                Type = declaredType.FullName,
                Path = path
            };

            if (value == null)
            {
                info.Kind = typeof(UnityEngine.Object).IsAssignableFrom(declaredType) ? "ObjectReference" : "Null";
                info.Value = "None";
                info.CanWrite = canWrite && typeof(UnityEngine.Object).IsAssignableFrom(declaredType);
                return info;
            }

            if (value is UnityEngine.Object unityObject)
            {
                info.Kind = "ObjectReference";
                info.CanWrite = canWrite;
                if (unityObject == null)
                {
                    info.Value = "None";
                }
                else
                {
                    info.ObjectName = unityObject.name;
                    info.InstanceID = unityObject.GetInstanceID();
                    info.Value = unityObject.name + " (" + NicifyName(unityObject.GetType().Name) + ")";
                }
                return info;
            }

            var actualType = value.GetType();
            info.Type = actualType.FullName ?? declaredType.FullName;
            if (actualType == typeof(bool))
            {
                info.Kind = "Boolean";
                info.Value = ((bool)value).ToString(CultureInfo.InvariantCulture);
                info.CanWrite = canWrite;
            }
            else if (actualType.IsEnum)
            {
                info.Kind = "Enum";
                info.Value = value.ToString();
                info.EnumNames = Enum.GetNames(actualType).ToList();
                info.CanWrite = canWrite;
            }
            else if (actualType == typeof(string) || actualType == typeof(char))
            {
                info.Kind = "String";
                info.Value = value.ToString();
                info.CanWrite = canWrite;
            }
            else if (actualType == typeof(float) || actualType == typeof(double) || actualType == typeof(decimal))
            {
                info.Kind = "Float";
                info.Value = Convert.ToString(value, CultureInfo.InvariantCulture);
                info.CanWrite = canWrite;
            }
            else if (actualType.IsPrimitive)
            {
                info.Kind = "Integer";
                info.Value = Convert.ToString(value, CultureInfo.InvariantCulture);
                info.CanWrite = canWrite;
            }
            else if (value is Color color)
            {
                info.Kind = "Color";
                info.Value = string.Format(CultureInfo.InvariantCulture, "{0:R},{1:R},{2:R},{3:R}", color.r, color.g, color.b, color.a);
                AddVectorChildren(info, new[] { ("r", color.r), ("g", color.g), ("b", color.b), ("a", color.a) }, path, depth, visited, canWrite);
            }
            else if (value is Vector2 vector2)
            {
                info.Kind = "Vector2";
                AddVectorChildren(info, new[] { ("x", vector2.x), ("y", vector2.y) }, path, depth, visited, canWrite);
            }
            else if (value is Vector3 vector3)
            {
                info.Kind = "Vector3";
                AddVectorChildren(info, new[] { ("x", vector3.x), ("y", vector3.y), ("z", vector3.z) }, path, depth, visited, canWrite);
            }
            else if (value is Vector4 vector4)
            {
                info.Kind = "Vector4";
                AddVectorChildren(info, new[] { ("x", vector4.x), ("y", vector4.y), ("z", vector4.z), ("w", vector4.w) }, path, depth, visited, canWrite);
            }
            else if (value is Quaternion quaternion)
            {
                info.Kind = "Quaternion";
                AddVectorChildren(info, new[] { ("x", quaternion.x), ("y", quaternion.y), ("z", quaternion.z), ("w", quaternion.w) }, path, depth, visited, canWrite);
            }
            else if (value is IList list)
            {
                info.Kind = "Array";
                info.Value = list.Count.ToString(CultureInfo.InvariantCulture);
                var elementType = actualType.IsArray ? actualType.GetElementType()! : actualType.GetGenericArguments().FirstOrDefault() ?? typeof(object);
                var count = Math.Min(list.Count, 512);
                for (var i = 0; i < count; i++)
                {
                    info.Children.Add(CreateValueField("[" + i + "]", "Element " + i, elementType, list[i], path + "[" + i + "]", depth + 1, visited, canWrite));
                }
                if (list.Count > count)
                {
                    info.Children.Add(new SerializedFieldInfo { DisplayName = "...", Kind = "Truncated", Value = (list.Count - count) + " more elements" });
                }
            }
            else if (depth >= 8)
            {
                info.Kind = "Truncated";
                info.Value = "Max depth reached";
            }
            else if (!actualType.IsValueType && !visited.Add(value))
            {
                info.Kind = "Reference";
                info.Value = "Cyclic reference";
            }
            else
            {
                info.Kind = "Object";
                foreach (var childField in EnumerateUnitySerializedFields(actualType))
                {
                    try
                    {
                        var childPath = string.IsNullOrEmpty(path) ? childField.Name : path + "." + childField.Name;
                        var child = CreateValueField(childField.Name, NicifyName(childField.Name), childField.FieldType, childField.GetValue(value), childPath, depth + 1, visited, canWrite);
                        ApplyInspectorAttributes(child, childField);
                        info.Children.Add(child);
                    }
                    catch (Exception ex)
                    {
                        info.Children.Add(new SerializedFieldInfo { Name = childField.Name, DisplayName = NicifyName(childField.Name), Kind = "Error", Value = ex.GetBaseException().Message });
                    }
                }
                if (!actualType.IsValueType)
                {
                    visited.Remove(value);
                }
            }

            return info;
        }

        private static void AddVectorChildren(SerializedFieldInfo parent, IEnumerable<(string name, float value)> values, string path, int depth, HashSet<object> visited, bool canWrite)
        {
            foreach (var item in values)
            {
                parent.Children.Add(CreateValueField(item.name, item.name.ToUpperInvariant(), typeof(float), item.value, path + "." + item.name, depth + 1, visited, canWrite));
            }
        }

        private static void ApplyInspectorAttributes(SerializedFieldInfo info, FieldInfo field)
        {
            foreach (var attribute in field.GetCustomAttributes(true))
            {
                var name = attribute.GetType().FullName;
                if (name == "UnityEngine.HeaderAttribute")
                {
                    info.Header = ReadAttributeValue(attribute, "header")?.ToString();
                }
                else if (name == "UnityEngine.TooltipAttribute")
                {
                    info.Tooltip = ReadAttributeValue(attribute, "tooltip")?.ToString();
                }
                else if (name == "UnityEngine.RangeAttribute")
                {
                    info.RangeMin = Convert.ToSingle(ReadAttributeValue(attribute, "min"), CultureInfo.InvariantCulture);
                    info.RangeMax = Convert.ToSingle(ReadAttributeValue(attribute, "max"), CultureInfo.InvariantCulture);
                }
                else if (name == "UnityEngine.MultilineAttribute")
                {
                    info.Multiline = true;
                    info.TextAreaMinLines = Convert.ToInt32(ReadAttributeValue(attribute, "lines") ?? 3, CultureInfo.InvariantCulture);
                }
                else if (name == "UnityEngine.TextAreaAttribute")
                {
                    info.Multiline = true;
                    info.TextAreaMinLines = Convert.ToInt32(ReadAttributeValue(attribute, "minLines") ?? 3, CultureInfo.InvariantCulture);
                    info.TextAreaMaxLines = Convert.ToInt32(ReadAttributeValue(attribute, "maxLines") ?? 3, CultureInfo.InvariantCulture);
                }
            }
        }

        private static object? ReadAttributeValue(object attribute, string memberName)
        {
            var type = attribute.GetType();
            return type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(attribute)
                ?? type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(attribute);
        }

        private static bool HasAttribute(FieldInfo field, string fullName)
        {
            return field.GetCustomAttributes(true).Any(attribute => attribute.GetType().FullName == fullName);
        }

        private static string NicifyName(string value)
        {
            value = value.StartsWith("m_", StringComparison.Ordinal) ? value.Substring(2) : value.TrimStart('_');
            value = Regex.Replace(value, "(?<=[a-z0-9])(?=[A-Z])", " ");
            value = value.Replace('_', ' ');
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(value);
        }

        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();

            public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);

            public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
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

        private sealed class SnapshotMetrics
        {
            private readonly Stopwatch _stopwatch;

            public SnapshotMetrics(Stopwatch stopwatch)
            {
                _stopwatch = stopwatch;
            }

            public string Stage { get; set; } = "waiting for Unity main thread";
            public string Scene { get; set; } = "<none>";
            public string GameObject { get; private set; } = "<none>";
            public string Component { get; private set; } = "<none>";
            public int Scenes { get; set; }
            public int GameObjects { get; private set; }
            public int Components { get; private set; }
            public int Fields { get; set; }

            public void VisitGameObject(string name)
            {
                Stage = "reading GameObjects";
                GameObject = name;
                GameObjects++;
            }

            public void VisitComponent(string typeName)
            {
                Stage = "reading components";
                Component = typeName;
                Components++;
            }

            public void VisitComponents(int count)
            {
                Stage = "reading component summaries";
                Components += count;
            }

            public string Describe()
            {
                return $"stage={Stage}, elapsed={_stopwatch.ElapsedMilliseconds} ms, scenes={Scenes}, " +
                       $"gameObjects={GameObjects}, components={Components}, fields={Fields}, " +
                       $"scene='{Scene}', gameObject='{GameObject}', component='{Component}'";
            }
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
            private readonly Dictionary<int, UnityEngine.Object> _unityObjects = new Dictionary<int, UnityEngine.Object>();

            public void ResetUnityObjects()
            {
                _unityObjects.Clear();
            }

            public void Remember(UnityEngine.Object value)
            {
                if (value != null)
                {
                    _unityObjects[value.GetInstanceID()] = value;
                }
            }

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

                if (_unityObjects.TryGetValue(instanceId, out var unityObject) && unityObject != null)
                {
                    value = unityObject;
                    return true;
                }

                value = Resources.FindObjectsOfTypeAll<UnityEngine.Object>().FirstOrDefault(obj => obj.GetInstanceID() == instanceId);
                if (value == null)
                {
                    error = $"Unity object with instance ID {instanceId} was not found.";
                    return false;
                }

                _unityObjects[instanceId] = (UnityEngine.Object)value;

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
                    var owner = current;
                    var parentSetter = resolved?.SetValue;
                    var next = ResolveSegment(owner, currentType, segment);
                    if (!next.Success)
                    {
                        return next;
                    }

                    var directSetter = next.SetValue;
                    next.SetValue = newValue =>
                    {
                        if (!directSetter(newValue))
                        {
                            return false;
                        }

                        // Reflection mutates a boxed struct. Reassign that box through every
                        // parent accessor until the change reaches the reference-type root.
                        return owner == null || !owner.GetType().IsValueType || parentSetter == null || parentSetter(owner);
                    };
                    resolved = next;

                    current = resolved.Value;
                    currentType = current?.GetType() ?? resolved.MemberType ?? typeof(object);

                    if (!last && current == null)
                    {
                        return Fail($"Path segment '{segment}' evaluated to null.");
                    }
                }

                return resolved ?? new PathAccess { Success = true, Value = current, MemberType = currentType };
            }

            private static PathAccess ResolveSegment(object? current, Type currentType, string segment)
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

                        var indexedOwner = value;
                        var ownerSetter = setter;
                        if (!TryGetIndexed(indexedOwner, indexMatch.Groups[1].Value, out value, out valueType, out var indexedSetter, out var indexError))
                        {
                            return Fail(indexError);
                        }

                        setter = newValue =>
                        {
                            if (!indexedSetter(newValue))
                            {
                                return false;
                            }

                            return !indexedOwner!.GetType().IsValueType || ownerSetter(indexedOwner);
                        };
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
                    typeof(UnityEngine.Object).IsAssignableFrom(type) ||
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

            public static bool TryConvert(string valueJson, Type targetType, ObjectRegistry registry, out object? value, out string error)
            {
                value = null;
                error = string.Empty;

                try
                {
                    var token = JToken.Parse(valueJson);
                    return TryConvertJson(token, targetType, registry, out value, out error);
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
                    if (element.Type == JTokenType.Null)
                    {
                        if (!targetType.IsValueType || nullable != null)
                        {
                            value = null;
                        }
                        else
                        {
                            error = $"Type '{targetType.FullName}' cannot be null.";
                            return false;
                        }
                    }
                    else if (targetType == typeof(string))
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
                            error = string.IsNullOrEmpty(id)
                                ? "A Unity object instance ID is required. Use null to clear the reference."
                                : $"Unity object '{id}' was not found or is not assignable to '{targetType.FullName}'.";
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
