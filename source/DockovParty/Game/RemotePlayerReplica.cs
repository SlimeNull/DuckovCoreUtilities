using SlimeNull.DockovParty.Networking.Protocol;
using UnityEngine;

namespace SlimeNull.DockovParty.Game
{
    internal sealed class RemotePlayerReplica : MonoBehaviour
    {
        private CharacterMainControl? _character;
        private Vector3 _targetPosition;
        private Quaternion _targetRotation = Quaternion.identity;
        private Vector3 _targetAimPoint;
        private float _interpolationDelay = 0.1f;
        private uint _lastSequence;
        private bool _hasState;

        public string PlayerId { get; private set; } = string.Empty;

        public void Initialize(string playerId, CharacterMainControl character, float interpolationDelay)
        {
            PlayerId = playerId;
            _character = character;
            _interpolationDelay = Mathf.Max(0.01f, interpolationDelay);
            _targetPosition = character.transform.position;
            _targetRotation = character.modelRoot != null ?
                character.modelRoot.rotation : character.transform.rotation;
            _targetAimPoint = character.GetCurrentAimPoint();
        }

        public void Apply(PlayerStateMessage state)
        {
            if (_hasState && state.Sequence <= _lastSequence)
            {
                return;
            }

            _hasState = true;
            _lastSequence = state.Sequence;
            _targetPosition = state.Transform.Position.ToUnity();
            _targetRotation = state.Transform.Rotation.ToUnity();
            _targetAimPoint = state.AimPoint.ToUnity();
        }

        private void LateUpdate()
        {
            if (!_hasState || _character == null)
            {
                return;
            }

            var factor = 1f - Mathf.Exp(-Time.unscaledDeltaTime / _interpolationDelay);
            _character.transform.position = Vector3.Lerp(
                _character.transform.position,
                _targetPosition,
                factor);

            if (_character.modelRoot != null)
            {
                _character.modelRoot.rotation = Quaternion.Slerp(
                    _character.modelRoot.rotation,
                    _targetRotation,
                    factor);
            }
            else
            {
                _character.transform.rotation = Quaternion.Slerp(
                    _character.transform.rotation,
                    _targetRotation,
                    factor);
            }

            _character.SetAimPoint(_targetAimPoint);
        }
    }
}
