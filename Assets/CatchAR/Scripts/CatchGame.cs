using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using WebXR;

namespace CatchAR
{
    public enum GamePhase { Ready, Placing, Countdown, Playing, Result }

    public sealed class CatchGame : MonoBehaviour
    {
        [Header("Scene references")]
        public WebXRCamera xrCamera;
        public WebXRController[] controllers;
        public CatchHud hud;
        public CatchTarget targetPrefab;
        public Transform arena;
        public GameObject placementMarker;
        public GameObject demoEnvironment;
        public AudioSource audioSource;
        public AudioClip catchSound;
        [Header("Round")]
        [Min(5)] public float roundDuration = 60;
        [Min(1)] public int maxTargets = 5;
        [Header("Falling objects")]
        [Min(1)] public float spawnDistance = 2.5f;
        [Range(.1f, 1.2f)] public float spawnHeightAboveCamera = .5f;
        [Min(.05f)] public float initialFallSpeed = .35f;
        [Min(0)] public float fallAcceleration = .22f;

        public GamePhase Phase { get; private set; }
        public int Score { get; private set; }
        public int Caught { get; private set; }
        public int Missed { get; private set; }
        public int Combo { get; private set; }
        public float Remaining { get; private set; }
        public int Best { get; private set; }
        public bool IsAR => WebXRManager.Instance != null && WebXRManager.Instance.XRState == WebXRState.AR;
        public int ActiveTargets => targets.Count;
        public IReadOnlyList<CatchTarget> Targets => targets;
        public float GroundHeight { get; private set; }
        public bool SimulationActive => visible && Phase == GamePhase.Playing;
        public Camera ViewCamera => xrCamera.GetCamera(IsAR ? WebXRCamera.CameraID.LeftAR : WebXRCamera.CameraID.Main);
        readonly List<CatchTarget> targets = new List<CatchTarget>();
        WebXRHitPoseData lastHit;
        float hitTime, phaseTime, nextSpawn, lastCatch = -100;
        int spawned, lastActionFrame = -1;
        bool visible = true;
        bool GroundHitAvailable => lastHit != null && lastHit.available && Time.unscaledTime - hitTime < .5f &&
            Vector3.Dot(lastHit.rotation * Vector3.up, Vector3.up) > .8f;

        void OnEnable()
        {
            WebXRManager.OnXRChange += XRChanged;
            WebXRManager.OnViewerHitTestUpdate += HitUpdated;
            WebXRManager.OnVisibilityChange += VisibilityChanged;
        }

        void OnDisable()
        {
            WebXRManager.OnXRChange -= XRChanged;
            WebXRManager.OnViewerHitTestUpdate -= HitUpdated;
            WebXRManager.OnVisibilityChange -= VisibilityChanged;
        }

        void Start()
        {
            Best = PlayerPrefs.GetInt("CatchAR.Best", 0);
            Phase = GamePhase.Ready;
            placementMarker.SetActive(false);
            hud.Bind(this);
        }

        void Update()
        {
            if (!visible) return;
            phaseTime += Time.deltaTime;
            if (Phase == GamePhase.Placing)
            {
                bool found = GroundHitAvailable;
                placementMarker.SetActive(found);
                if (found) placementMarker.transform.SetPositionAndRotation(lastHit.position + Vector3.up * .012f, lastHit.rotation * Quaternion.Euler(90, 0, 0));
                hud.SetPlacement(found, phaseTime > 5);
            }
            if (Phase == GamePhase.Countdown && phaseTime >= 3)
            {
                Phase = GamePhase.Playing;
                phaseTime = 0;
                nextSpawn = 0;
            }
            if (Phase == GamePhase.Playing)
            {
                Remaining = Mathf.Max(0, Remaining - Time.deltaTime);
                if (Remaining <= 0) EndRound();
                else if (Time.time >= nextSpawn && targets.Count < maxTargets)
                {
                    SpawnTarget();
                    nextSpawn = Time.time + Mathf.Lerp(1.05f, .60f, 1 - Remaining / roundDuration);
                }
            }
            HandleInput();
            hud.Refresh(phaseTime);
        }

        void HandleInput()
        {
            foreach (var controller in controllers)
            {
                if (!controller.isControllerActive || !controller.GetButtonDown(WebXRController.ButtonTypes.Trigger)) continue;
                Act(new Ray(controller.transform.position, controller.transform.forward));
            }
            // WebXR Export forwards screen-based AR selects as touch events. Handheld XR
            // controllers use the ray path above; lastActionFrame prevents duplicate actions.
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            {
                int id = Touchscreen.current.primaryTouch.touchId.ReadValue();
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(id)) return;
                Act(ViewCamera.ScreenPointToRay(Touchscreen.current.primaryTouch.position.ReadValue()));
            }
            else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
                Act(ViewCamera.ScreenPointToRay(Mouse.current.position.ReadValue()));
            }
        }

        public void Act(Ray ray)
        {
            if (lastActionFrame == Time.frameCount) return;
            lastActionFrame = Time.frameCount;
            if (Phase == GamePhase.Placing) { PlaceArena(); return; }
            // A select after the result starts another round even on headsets without mouse UI.
            if (Phase == GamePhase.Result && phaseTime > 1) { BeginCountdown(); return; }
            if (Phase != GamePhase.Playing) return;
            if (Physics.Raycast(ray, out RaycastHit hit, 12) && hit.collider.TryGetComponent(out CatchTarget target))
                target.Catch();
            else
            {
                Combo = 0;
                hud.Flash("МИМО · ПОПРОБУЙ ЕЩЁ", false);
            }
        }

        public void StartDemo()
        {
            if (IsAR) return;
            xrCamera.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            GroundHeight = .13f;
            arena.SetPositionAndRotation(new Vector3(0, GroundHeight, 3), Quaternion.identity);
            BeginCountdown();
        }

        public void PlaceArena()
        {
            if (Phase != GamePhase.Placing) return;
            bool found = GroundHitAvailable;
            if (!found && phaseTime < 5) return;
            var camera = ViewCamera.transform;
            Vector3 forward = Vector3.ProjectOnPlane(camera.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .01f) forward = Vector3.forward;
            // The reticle defines a fixed ground plane. Existing objects stay in
            // world space; new objects appear around the player's current position.
            GroundHeight = found ? lastHit.position.y : camera.position.y - 1.5f;
            Vector3 center = new Vector3(camera.position.x, GroundHeight, camera.position.z);
            arena.SetPositionAndRotation(center, Quaternion.LookRotation(forward, Vector3.up));
            placementMarker.SetActive(false);
            WebXRManager.Instance.StopViewerHitTest();
            BeginCountdown();
        }

        public void Restart()
        {
            if (IsAR) BeginCountdown();
            else StartDemo();
        }

        public void ReturnToMenu()
        {
            ClearTargets();
            Phase = GamePhase.Ready;
            if (IsAR) WebXRManager.Instance.ToggleAR();
        }

        void BeginCountdown()
        {
            ClearTargets();
            Score = Caught = Missed = Combo = spawned = 0;
            Remaining = roundDuration;
            lastCatch = -100;
            Phase = GamePhase.Countdown;
            phaseTime = 0;
        }

        public Vector3 GetSpawnPosition(Vector3 playerPosition, float yawDegrees)
        {
            // Constant 3D distance, independent of floor height, camera tilt and rig origin.
            float distance = Mathf.Max(1, spawnDistance);
            float height = Mathf.Clamp(spawnHeightAboveCamera, .1f, distance * .5f);
            float radius = Mathf.Sqrt(distance * distance - height * height);
            float angle = yawDegrees * Mathf.Deg2Rad;
            return playerPosition + new Vector3(Mathf.Sin(angle) * radius, height, Mathf.Cos(angle) * radius);
        }

        void SpawnTarget()
        {
            Camera camera = ViewCamera;
            Vector3 playerPosition = camera.transform.position;
            Vector3 world = Vector3.zero;
            // Only the azimuth changes. Each new object starts at exactly the same
            // distance and eye-relative height. Already falling objects stay in world space.
            float heading = Mathf.Atan2(camera.transform.forward.x, camera.transform.forward.z) * Mathf.Rad2Deg;
            float halfHorizontalFov = Mathf.Atan(Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * .5f) * camera.aspect) * Mathf.Rad2Deg;
            float demoSpread = Mathf.Clamp(halfHorizontalFov - 9, 0, 24);
            for (int attempt = 0; attempt < 20; attempt++)
            {
                // Give the player a visible reference at the start of an AR round.
                // Later targets still surround the player on the full ring.
                float yaw = IsAR && spawned > 0 ? Random.Range(-180f, 180f) : heading + Random.Range(-demoSpread, demoSpread);
                world = GetSpawnPosition(playerPosition, yaw);
                bool overlaps = false;
                foreach (var other in targets)
                    if ((world - other.transform.position).sqrMagnitude < .14f) overlaps = true;
                if (!overlaps) break;
                if (attempt == 19) return;
            }
            var target = Instantiate(targetPrefab, world, Quaternion.identity, arena);
            bool gold = ++spawned % 5 == 0;
            target.Initialize(this, gold, GroundHeight, initialFallSpeed, fallAcceleration);
            targets.Add(target);
        }

        public void RegisterCatch(CatchTarget target)
        {
            if (Phase != GamePhase.Playing || !targets.Remove(target)) return;
            Combo = Time.time - lastCatch < 1.8f ? Mathf.Min(Combo + 1, 5) : 1;
            lastCatch = Time.time;
            int points = (target.IsGold ? 25 : 10) * Combo;
            Score += points;
            Caught++;
            hud.Flash($"+{points}  " + (Combo > 1 ? $"СЕРИЯ ×{Combo}" : "ПОЙМАНО!"), true);
            if (audioSource != null && catchSound != null)
            {
                audioSource.pitch = 1 + Combo * .08f;
                audioSource.PlayOneShot(catchSound, .25f);
            }
        }

        public void RegisterEscape(CatchTarget target)
        {
            if (!targets.Remove(target) || Phase != GamePhase.Playing) return;
            Missed++;
            Combo = 0;
        }

        void EndRound()
        {
            Phase = GamePhase.Result;
            phaseTime = 0;
            ClearTargets();
            if (Score > Best)
            {
                Best = Score;
                PlayerPrefs.SetInt("CatchAR.Best", Best);
                PlayerPrefs.Save();
            }
        }

        void ClearTargets()
        {
            foreach (var target in targets) if (target != null) Destroy(target.gameObject);
            targets.Clear();
        }

        void XRChanged(WebXRState state, int count, Rect left, Rect right)
        {
            ClearTargets();
            phaseTime = 0;
            demoEnvironment.SetActive(state == WebXRState.NORMAL);
            placementMarker.SetActive(false);
            if (state == WebXRState.AR)
            {
                Phase = GamePhase.Placing;
                lastHit = null;
                WebXRManager.Instance.StartViewerHitTest();
            }
            else
            {
                xrCamera.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                Phase = GamePhase.Ready;
            }
        }

        void HitUpdated(WebXRHitPoseData data) { lastHit = data; hitTime = Time.unscaledTime; }
        void VisibilityChanged(WebXRVisibilityState state) { visible = state == WebXRVisibilityState.VISIBLE; }
    }
}
