using SlimeNull.DuckovCoreUtilities.Infrastructure;
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.UI.ProceduralImage;

namespace SlimeNull.DuckovCoreUtilities.Features
{
    internal sealed class MinimapFeature : FeatureBase
    {
        public enum OrientationMode
        {
            FixedAngle,
            FollowPlayerHeading,
        }

        public const float MinimumZoom = 0.25f;
        public const float MaximumZoom = 4f;

        private const string HudCanvasName = "HUDCanvas";
        private const string TimeOfDayDisplayName = "TimeOfDayDisplay";
        private const string VerticalLayoutName = "Vertical";
        private const string MinimapName = "Minimap";
        private const string MinimapImageName = "MinimapImage";
        private const string MinimapCameraName = "MinimapCamera";
        private const string MaskSpriteName = "procedural_ui_image_default_sprite";
        private const int TextureResolution = 512;
        private const float CameraHeight = 100f;
        private const float BaseOrthographicSize = 30f;
        private const float ZoomStep = 1.2f;
        private const float AttachRetryInterval = 1f;

        private GameObject? _minimapObject;
        private LayoutElement? _layoutElement;
        private RawImage? _rawImage;
        private GameObject? _cameraObject;
        private Camera? _camera;
        private RenderTexture? _renderTexture;
        private float _nextAttachAttempt;
        private float _lastDisplaySize = -1f;
        private float _lastOpacity = -1f;

        public override string Name => "Minimap";

        public float DisplaySize { get; set; } = 260f;

        public float Zoom { get; set; } = 1f;

        public float Opacity { get; set; } = 0.7f;

        public OrientationMode Mode { get; set; } = OrientationMode.FixedAngle;

        public event Action<float>? ZoomChangedByInput;

        protected override void OnEnable()
        {
            LevelManager.OnAfterLevelInitialized += OnAfterLevelInitialized;
            TryCreateMinimap();
        }

        protected override void OnDisable()
        {
            LevelManager.OnAfterLevelInitialized -= OnAfterLevelInitialized;
            DestroyMinimap();
        }

        public override void Tick()
        {
            if (!IsInRaidLevel())
            {
                if (_minimapObject != null || _cameraObject != null || _renderTexture != null)
                {
                    DestroyMinimap();
                }
                return;
            }

            if (_minimapObject == null || _camera == null || _renderTexture == null || _rawImage == null || _layoutElement == null)
            {
                ClearDestroyedReferences();
                if (Time.unscaledTime >= _nextAttachAttempt)
                {
                    TryCreateMinimap();
                }
            }

            HandleZoomInput();
            ApplyUiSettings();
        }

        private void OnAfterLevelInitialized()
        {
            DestroyMinimap();
            TryCreateMinimap();
        }

        private void TryCreateMinimap()
        {
            if (_minimapObject != null)
            {
                return;
            }

            _nextAttachAttempt = Time.unscaledTime + AttachRetryInterval;
            if (!IsInRaidLevel())
            {
                return;
            }

            var hudCanvas = GameObject.Find(HudCanvasName);
            var timeOfDayDisplay = hudCanvas != null ? hudCanvas.transform.Find(TimeOfDayDisplayName) : null;
            var verticalLayout = timeOfDayDisplay != null ? timeOfDayDisplay.Find(VerticalLayoutName) : null;
            var player = GetFollowTarget();
            if (verticalLayout == null ||
                verticalLayout.GetComponent<VerticalLayoutGroup>() == null ||
                player == null)
            {
                return;
            }

            try
            {
                CreateRenderTarget();
                CreateCamera(player);
                CreateUi(verticalLayout);
                ApplyUiSettings(force: true);
                UpdateCamera();
            }
            catch
            {
                DestroyMinimap();
                throw;
            }
        }

        private void CreateRenderTarget()
        {
            _renderTexture = new RenderTexture(TextureResolution, TextureResolution, 24, RenderTextureFormat.ARGB32)
            {
                name = "DuckovCoreUtilities Minimap",
                antiAliasing = 2,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                autoGenerateMips = false,
            };
            _renderTexture.Create();
        }

        private void CreateCamera(Transform player)
        {
            _cameraObject = new GameObject(MinimapCameraName, typeof(Camera));
            _cameraObject.transform.SetParent(Context.HostObject.transform, false);

            _camera = _cameraObject.GetComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = GetOrthographicSize();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Color.black;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = CameraHeight * 2.5f;
            _camera.depth = -100f;
            _camera.allowHDR = false;
            _camera.allowMSAA = true;
            _camera.useOcclusionCulling = false;
            _camera.targetTexture = _renderTexture;
            _cameraObject.AddComponent<CameraFollower>().Initialize(this);

            var gameCamera = LevelManager.Instance?.GameCamera?.renderCamera;
            if (gameCamera != null)
            {
                _camera.cullingMask = gameCamera.cullingMask;
            }

            PositionCamera(player);
        }

        private void CreateUi(Transform verticalLayout)
        {
            _minimapObject = new GameObject(
                MinimapName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(LayoutElement));
            _minimapObject.transform.SetParent(verticalLayout, false);
            _minimapObject.transform.SetAsLastSibling();

            _layoutElement = _minimapObject.GetComponent<LayoutElement>();
            _layoutElement.flexibleWidth = 0f;
            _layoutElement.flexibleHeight = 0f;

            var modifier = _minimapObject.AddComponent<UniformModifier>();
            var maskImage = _minimapObject.AddComponent<ProceduralImage>();
            modifier.Radius = 14f;
            maskImage.color = Color.white;
            maskImage.raycastTarget = false;
            var maskSprite = FindMaskSprite();
            if (maskSprite != null)
            {
                maskImage.sprite = maskSprite;
            }

            var mask = _minimapObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            var imageObject = new GameObject(MinimapImageName, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            imageObject.transform.SetParent(_minimapObject.transform, false);

            var imageRect = imageObject.GetComponent<RectTransform>();
            imageRect.anchorMin = Vector2.zero;
            imageRect.anchorMax = Vector2.one;
            imageRect.offsetMin = Vector2.zero;
            imageRect.offsetMax = Vector2.zero;

            _rawImage = imageObject.GetComponent<RawImage>();
            _rawImage.texture = _renderTexture;
            _rawImage.raycastTarget = false;
        }

        private void HandleZoomInput()
        {
            if (!Application.isFocused || !InputManager.InputActived || Keyboard.current == null)
            {
                return;
            }

            if (Keyboard.current.leftBracketKey.wasPressedThisFrame)
            {
                SetZoomFromInput(Zoom / ZoomStep);
            }
            else if (Keyboard.current.rightBracketKey.wasPressedThisFrame)
            {
                SetZoomFromInput(Zoom * ZoomStep);
            }
        }

        private void SetZoomFromInput(float value)
        {
            var clamped = Mathf.Clamp(value, MinimumZoom, MaximumZoom);
            if (Mathf.Approximately(Zoom, clamped))
            {
                return;
            }

            Zoom = clamped;
            ZoomChangedByInput?.Invoke(Zoom);
        }

        private void ApplyUiSettings(bool force = false)
        {
            if (_layoutElement != null && (force || !Mathf.Approximately(_lastDisplaySize, DisplaySize)))
            {
                var size = Mathf.Max(1f, DisplaySize);
                _layoutElement.preferredWidth = size;
                _layoutElement.preferredHeight = size;
                _lastDisplaySize = DisplaySize;
            }

            if (_rawImage != null && (force || !Mathf.Approximately(_lastOpacity, Opacity)))
            {
                var color = _rawImage.color;
                color.a = Mathf.Clamp01(Opacity);
                _rawImage.color = color;
                _lastOpacity = Opacity;
            }
        }

        private void UpdateCamera()
        {
            if (_camera == null)
            {
                return;
            }

            var player = GetFollowTarget();
            if (player == null ||
                _minimapObject == null ||
                !_minimapObject.activeInHierarchy ||
                Opacity <= 0.001f)
            {
                _camera.enabled = false;
                return;
            }

            if (!_camera.enabled)
            {
                _camera.enabled = true;
            }

            _camera.orthographicSize = GetOrthographicSize();
            PositionCamera(player);
        }

        private void PositionCamera(Transform player)
        {
            if (_cameraObject == null)
            {
                return;
            }

            var mapUp = Vector3.forward;
            if (Mode == OrientationMode.FollowPlayerHeading)
            {
                mapUp = Vector3.ProjectOnPlane(player.forward, Vector3.up);
                if (mapUp.sqrMagnitude < 0.0001f)
                {
                    mapUp = Vector3.forward;
                }
                else
                {
                    mapUp.Normalize();
                }
            }

            _cameraObject.transform.SetPositionAndRotation(
                player.position + Vector3.up * CameraHeight,
                Quaternion.LookRotation(Vector3.down, mapUp));
        }

        private float GetOrthographicSize()
        {
            return BaseOrthographicSize / Mathf.Clamp(Zoom, MinimumZoom, MaximumZoom);
        }

        private static Transform? GetFollowTarget()
        {
            var levelManager = LevelManager.Instance;
            var character = levelManager?.ControllingCharacter ?? levelManager?.MainCharacter;
            return character != null && character.gameObject.activeInHierarchy ? character.transform : null;
        }

        private static bool IsInRaidLevel()
        {
            if (!LevelManager.LevelInited)
            {
                return false;
            }

            var levelManager = LevelManager.Instance;
            return levelManager != null && levelManager.IsRaidMap;
        }

        private static Sprite? FindMaskSprite()
        {
            var sprites = Resources.FindObjectsOfTypeAll<Sprite>();
            foreach (var sprite in sprites)
            {
                if (sprite != null && sprite.name == MaskSpriteName)
                {
                    return sprite;
                }
            }

            return null;
        }

        private void ClearDestroyedReferences()
        {
            if (_minimapObject != null && _camera != null && _renderTexture != null && _rawImage != null && _layoutElement != null)
            {
                return;
            }

            DestroyMinimap(resetAttachAttempt: false);
        }

        private void DestroyMinimap(bool resetAttachAttempt = true)
        {
            if (_camera != null)
            {
                _camera.targetTexture = null;
            }

            if (_cameraObject != null)
            {
                UnityEngine.Object.Destroy(_cameraObject);
            }

            if (_minimapObject != null)
            {
                UnityEngine.Object.Destroy(_minimapObject);
            }

            if (_renderTexture != null)
            {
                _renderTexture.Release();
                UnityEngine.Object.Destroy(_renderTexture);
            }

            _minimapObject = null;
            _layoutElement = null;
            _rawImage = null;
            _cameraObject = null;
            _camera = null;
            _renderTexture = null;
            if (resetAttachAttempt)
            {
                _nextAttachAttempt = 0f;
            }
            _lastDisplaySize = -1f;
            _lastOpacity = -1f;
        }

        private sealed class CameraFollower : MonoBehaviour
        {
            private MinimapFeature? _owner;

            public void Initialize(MinimapFeature owner)
            {
                _owner = owner;
            }

            private void LateUpdate()
            {
                _owner?.UpdateCamera();
            }
        }
    }
}
