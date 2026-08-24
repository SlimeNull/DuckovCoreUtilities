using SlimeNull.DockovParty.Networking.Protocol;
using UnityEngine;

namespace SlimeNull.DockovParty.Game
{
    internal sealed class NetworkNpcReplica : MonoBehaviour
    {
        private CharacterMainControl? _character;
        private Vector3 _targetPosition;
        private Quaternion _targetRotation = Quaternion.identity;
        private Vector3 _targetAimPoint;
        private float _interpolationDelay;
        private bool _hasState;

        public int NpcId { get; private set; }

        public void Initialize(int npcId, CharacterMainControl character, float interpolationDelay)
        {
            NpcId = npcId;
            _character = character;
            _interpolationDelay = Mathf.Max(0.01f, interpolationDelay);
            _targetPosition = character.transform.position;
            _targetRotation = character.modelRoot != null ?
                character.modelRoot.rotation : character.transform.rotation;
            _targetAimPoint = character.GetCurrentAimPoint();
        }

        public void Apply(NpcState state)
        {
            _hasState = true;
            _targetPosition = state.Transform.Position.ToUnity();
            _targetRotation = state.Transform.Rotation.ToUnity();
            _targetAimPoint = state.AimPoint.ToUnity();

            if (_character == null)
            {
                return;
            }

            if (!state.Alive)
            {
                _character.gameObject.SetActive(false);
            }
            else
            {
                if (!_character.gameObject.activeSelf)
                {
                    _character.gameObject.SetActive(true);
                }

                if (!_character.Health.IsDead)
                {
                    _character.Health.SetHealth(state.Health);
                }
            }
        }

        private void LateUpdate()
        {
            if (!_hasState || _character == null || !_character.gameObject.activeInHierarchy)
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

            _character.SetAimPoint(_targetAimPoint);
        }
    }
}
