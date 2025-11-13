using System.Collections;
using UnityEngine;
using Unity.Cinemachine;

namespace MORTIS.Players
{
    public class CameraMotionController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] CinemachineCamera vcam;
        [SerializeField] CinemachineBasicMultiChannelPerlin noise;   // on CM_vcam

        [Header("Jump / Land Bob")]
        [SerializeField] float jumpKick = 0.08f;
        [SerializeField] float landKick = 0.12f;
        [SerializeField] float springStrength = 40f;
        [SerializeField] float springDamping = 9f;

        [Header("Walk Bob")]
        [SerializeField] float walkBobAmplitude = 0.03f;
        [SerializeField] float walkBobFrequency = 8f;
        [SerializeField] float sprintBobMultiplier = 1.3f;
        [SerializeField] float minBobSpeed = 0.1f;      // ignore tiny movement

        [Header("FOV Kick (Sprint)")]
        [SerializeField] float sprintFovMultiplier = 1.06f;
        [SerializeField] float fovLerpSpeed = 8f;

        [Header("Noise Kick")]
        [SerializeField] float jumpNoiseAmp = 0.25f;
        [SerializeField] float jumpNoiseFreq = 1.5f;
        [SerializeField] float jumpNoiseDuration = 0.15f;
        [SerializeField] float landNoiseAmp = 0.4f;
        [SerializeField] float landNoiseFreq = 2.0f;
        [SerializeField] float landNoiseDuration = 0.22f;

        // state fed from PlayerMover
        Vector2 _moveInput;
        bool _grounded;
        bool _sprinting;

        // internals
        Vector3 _baseLocalPos;

        float _springOffsetY;
        float _springVelocity;

        float _bobPhase;
        float _baseFov = 60f;

        float _baseNoiseAmp;
        float _baseNoiseFreq;
        Coroutine _noiseRoutine;

        void Awake()
        {
            _baseLocalPos = transform.localPosition;

            if (vcam != null)
                _baseFov = vcam.Lens.FieldOfView;

            if (!noise && vcam != null)
                noise = vcam.GetComponent<CinemachineBasicMultiChannelPerlin>();

            if (noise != null)
            {
                _baseNoiseAmp = noise.AmplitudeGain;
                _baseNoiseFreq = noise.FrequencyGain;
                noise.AmplitudeGain = _baseNoiseAmp;
                noise.FrequencyGain = _baseNoiseFreq;
            }
        }

        public void SetLocomotionState(Vector2 moveInput, bool grounded, bool sprinting)
        {
            _moveInput = moveInput;
            _grounded = grounded;
            _sprinting = sprinting;
        }

        public void OnJump()
        {
            _springOffsetY -= jumpKick;
            KickNoise(jumpNoiseAmp, jumpNoiseFreq, jumpNoiseDuration);
        }

        public void OnLand(float impactStrength)
        {
            float scale = Mathf.Clamp01(impactStrength / 12f); // tweak
            _springOffsetY += landKick * scale;
            KickNoise(landNoiseAmp * scale, landNoiseFreq, landNoiseDuration);
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;

            // -------- SPRING VERTICAL OFFSET (jump/land ease) --------
            {
                float displacement = _springOffsetY;
                float force = -springStrength * displacement - springDamping * _springVelocity;

                _springVelocity += force * dt;
                _springOffsetY += _springVelocity * dt;
            }

            // -------- WALK BOB (small up/down + side sway) --------
            float bobX = 0f;
            float bobY = 0f;
            {
                float speed = _moveInput.magnitude;

                if (_grounded && speed > minBobSpeed)
                {
                    float freq = walkBobFrequency * (_sprinting ? sprintBobMultiplier : 1f);
                    _bobPhase += freq * dt * speed;

                    bobY = Mathf.Sin(_bobPhase * 2f) * walkBobAmplitude;
                    bobX = Mathf.Cos(_bobPhase) * walkBobAmplitude * 0.5f;
                }
                else
                {
                    // reset phase slowly when not moving
                    _bobPhase = Mathf.Lerp(_bobPhase, 0f, 5f * dt);
                }
            }

            // -------- APPLY POSITION --------
            Vector3 offset = new Vector3(bobX, _springOffsetY + bobY, 0f);
            transform.localPosition = _baseLocalPos + offset;

            // -------- FOV KICK --------
            if (vcam != null)
            {
                float targetFov = _baseFov * (_sprinting ? sprintFovMultiplier : 1f);
                float currentFov = vcam.Lens.FieldOfView;
                currentFov = Mathf.Lerp(currentFov, targetFov, fovLerpSpeed * dt);
                vcam.Lens.FieldOfView = currentFov;
            }
        }

        void KickNoise(float extraAmp, float extraFreq, float duration)
        {
            if (noise == null || duration <= 0f || (extraAmp <= 0f && extraFreq <= 0f))
                return;

            if (_noiseRoutine != null)
                StopCoroutine(_noiseRoutine);

            _noiseRoutine = StartCoroutine(NoiseRoutine(extraAmp, extraFreq, duration));
        }

        IEnumerator NoiseRoutine(float extraAmp, float extraFreq, float duration)
        {
            float t = 0f;

            while (t < duration)
            {
                t += Time.deltaTime;
                float lerp = 1f - (t / duration);

                noise.AmplitudeGain = _baseNoiseAmp + extraAmp * lerp;
                noise.FrequencyGain = _baseNoiseFreq + extraFreq * lerp;

                yield return null;
            }

            noise.AmplitudeGain = _baseNoiseAmp;
            noise.FrequencyGain = _baseNoiseFreq;
            _noiseRoutine = null;
        }
    }
}
