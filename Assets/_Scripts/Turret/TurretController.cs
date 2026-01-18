// TurretController.cs
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class TurretController : MonoBehaviour
{
    private enum State
    {
        Idle,
        Opening,
        Firing,
        Searching,
        Panic,
        Shutdown
    }

    private enum Axis
    {
        Forward, Up, Right,
        NegForward, NegUp, NegRight
    }

    [Header("Refs")]
    [SerializeField] private Transform eye;              // 레이저 원점(위치)
    [SerializeField] private Transform laserPivot;       // 레이저/시야 회전 전용(몸통은 안 돈다)
    [SerializeField] private Axis laserForwardAxis = Axis.Forward;
    [SerializeField] private LineRenderer laser;
    [SerializeField] private Rigidbody rb;

    [Header("Laser Visual")]
    [SerializeField] private float laserLength = 12f;
    [SerializeField] private bool laserUseRaycastEnd = true;
    [Tooltip("레이저가 끊길 레이어(기본: occluderMask). 필요하면 Player도 포함 가능.")]
    [SerializeField] private LayerMask laserHitMask;

    [Header("Wings (Slide X)")]
    [SerializeField] private Transform leftWing;
    [SerializeField] private Transform rightWing;
    [SerializeField] private float leftWingClosedX = 0.304f;
    [SerializeField] private float leftWingOpenX = 0.566f;
    [SerializeField] private float rightWingClosedX = -0.304f;
    [SerializeField] private float rightWingOpenX = -0.566f;
    [SerializeField] private float wingSlideDuration = 0.7f;

    [Header("Muzzle Visuals (Move Z)")]
    [SerializeField] private Transform[] muzzleVisuals; // 비주얼 4개(앞/뒤)
    [SerializeField] private float muzzleExtendZ = 0.1f;
    [SerializeField] private float muzzleMoveDuration = 0.3f;

    [Header("Bullet Spawn")]
    [SerializeField] private Transform muzzleSpawnPoint; // 실제 총알 스폰 1개
    [SerializeField] private float bulletSpawnForwardOffset = 0.08f; // 겹침 방지(5~10cm)

    [Header("Vision")]
    [SerializeField] private float viewDistance = 18f;
    [Range(1f, 179f)][SerializeField] private float viewAngle = 80f;
    [SerializeField] private LayerMask playerMask;       // Player는 레이어로
    [SerializeField] private LayerMask occluderMask;     // 벽/바닥 등(큐브 레이어는 빼라)

    [Header("Fire")]
    [SerializeField] private string bulletPoolKey = "TurretBullet";
    [SerializeField] private float bulletSpeed = 22f;
    [SerializeField] private float fireInterval = 0.18f;

    [Tooltip("레이저Pivot이 플레이어를 따라 도는 속도(도/초)")]
    [SerializeField] private float aimYawSpeedDeg = 180f;

    [Header("Lose Sight -> Search")]
    [SerializeField] private float searchTime = 1.2f;
    [SerializeField] private float searchYaw = 25f;
    [SerializeField] private float searchYawSpeedDeg = 220f;

    [Header("Topple")]
    [Range(0f, 1f)][SerializeField] private float uprightDotThreshold = 0.78f;
    [SerializeField] private float panicDuration = 3f;
    [SerializeField] private float panicFireInterval = 0.10f;
    [SerializeField] private float panicAimJitter = 20f;

    [Header("Muzzle Flash FX")]
    [SerializeField] private ParticleSystem muzzleFlashL;
    [SerializeField] private ParticleSystem muzzleFlashR;

    private State _state = State.Idle;
    private Coroutine _co;
    private float _nextFireTime;

    private Vector3 _playerCenter;
    private bool _hasPlayer;
    private readonly Collider[] _playerHits = new Collider[12];

    private Vector3[] _muzzleBaseLocalPos;

    private Quaternion _laserBaseLocalRot;
    private float _laserYaw;
    private float _laserYawTarget;

    private bool _wingsOpen;
    private bool _muzzlesExtended;
    private bool _isHeld;

    private void Awake()
    {
        if (!rb) rb = GetComponent<Rigidbody>();
        if (!eye) eye = transform;

        if (!laserPivot) laserPivot = eye;
        _laserBaseLocalRot = laserPivot.localRotation;
        _laserYaw = 0f;
        ApplyLaserYawInstant(0f);

        if (!muzzleSpawnPoint) muzzleSpawnPoint = eye;

        if (playerMask.value == 0)
        {
            int layer = LayerMask.NameToLayer("Player");
            if (layer >= 0) playerMask = (1 << layer);
        }

        if (laserHitMask.value == 0)
            laserHitMask = occluderMask; // 기본은 벽/바닥에서 끊김

        CacheMuzzleVisualBase();
        SetLaserEnabled(laser != null);
    }

    private void CacheMuzzleVisualBase()
    {
        if (muzzleVisuals == null || muzzleVisuals.Length == 0)
        {
            _muzzleBaseLocalPos = null;
            return;
        }

        _muzzleBaseLocalPos = new Vector3[muzzleVisuals.Length];
        for (int i = 0; i < muzzleVisuals.Length; i++)
        {
            if (!muzzleVisuals[i]) continue;
            _muzzleBaseLocalPos[i] = muzzleVisuals[i].localPosition;
        }
    }

    private void OnEnable()
    {
        _state = State.Idle;
        _hasPlayer = false;
        _isHeld = false;

        _wingsOpen = false;
        _muzzlesExtended = false;

        SetWingsInstant(open: false);
        SetMuzzleVisualsInstant(extended: false);

        ApplyLaserYawInstant(0f);
        SetLaserEnabled(laser != null);
    }

    private void Update()
    {
        if (_state == State.Shutdown) return;

        if (_isHeld)
        {
            UpdateLaserVisual();
            return;
        }

        if (_state != State.Panic && !IsUpright())
        {
            EnterPanic();
            return;
        }

        UpdateLaserVisual();
    }

    private void FixedUpdate()
    {
        if (_state == State.Shutdown) return;
        if (_isHeld) return;

        if (_state == State.Idle)
        {
            if (CanSeePlayer(out _))
                EnterOpening();
        }
        else if (_state == State.Firing)
        {
            if (!CanSeePlayer(out var center))
                EnterSearching();
            else
                AimLaserTo(center);
        }
    }

    // =========================
    // 외부에서 호출(잡기/놓기)
    // =========================
    public void SetHeld(bool held)
    {
        _isHeld = held;

        if (held)
        {
            StopCo();
            _state = State.Idle;

            SetLaserEnabled(false);

            _co = StartCoroutine(CoOpenVisualsOnly());
        }
        else
        {
            StopCo();

            if (_state != State.Shutdown)
                SetLaserEnabled(laser != null);

            ApplyLaserYawInstant(0f);

            _wingsOpen = false;
            _muzzlesExtended = false;

            SetMuzzleVisualsInstant(false);
            SetWingsInstant(false);

            _state = State.Idle;
        }
    }

    private IEnumerator CoOpenVisualsOnly()
    {
        yield return CoMoveWings(open: true);
        _wingsOpen = true;

        yield return CoMoveMuzzleVisuals(extended: true);
        _muzzlesExtended = true;

        _co = null;
    }

    // =========================
    // Vision
    // =========================
    private bool CanSeePlayer(out Vector3 playerCenter)
    {
        playerCenter = Vector3.zero;

        if (!TryGetPlayerCandidate(out Vector3 center))
            return false;

        Vector3 origin = eye.position;
        Vector3 to = center - origin;

        float dist = to.magnitude;
        if (dist <= 0.001f || dist > viewDistance) return false;

        Vector3 dir = to / dist;

        float half = viewAngle * 0.5f;
        float ang = Vector3.Angle(GetLaserForward(), dir);
        if (ang > half) return false;

        if (occluderMask.value != 0)
        {
            if (Physics.Raycast(origin, dir, out _, dist, occluderMask, QueryTriggerInteraction.Ignore))
                return false;
        }

        _playerCenter = center;
        _hasPlayer = true;

        playerCenter = center;
        return true;
    }

    private bool TryGetPlayerCandidate(out Vector3 center)
    {
        center = Vector3.zero;
        if (playerMask.value == 0) return false;

        Vector3 origin = eye.position;
        int n = Physics.OverlapSphereNonAlloc(origin, viewDistance, _playerHits, playerMask, QueryTriggerInteraction.Ignore);
        if (n <= 0) return false;

        float best = float.PositiveInfinity;
        Collider bestCol = null;

        for (int i = 0; i < n; i++)
        {
            var c = _playerHits[i];
            if (!c) continue;

            float d2 = (c.bounds.center - origin).sqrMagnitude;
            if (d2 < best)
            {
                best = d2;
                bestCol = c;
            }
        }

        if (!bestCol) return false;

        center = bestCol.bounds.center;
        return true;
    }

    // =========================
    // State transitions
    // =========================
    private void EnterOpening()
    {
        if (_state == State.Opening || _state == State.Firing) return;

        StopCo();
        _state = State.Opening;

        SoundManager.PlaySFX(SfxId.Turret_LockOn, worldPos: eye.position);
        _co = StartCoroutine(CoOpenWingsThenExtendMuzzles());
    }

    private IEnumerator CoOpenWingsThenExtendMuzzles()
    {
        yield return CoMoveWings(open: true);
        _wingsOpen = true;

        yield return CoMoveMuzzleVisuals(extended: true);
        _muzzlesExtended = true;

        SoundManager.PlaySFX(SfxId.Turret_ReadyForShoot, worldPos: eye.position);

        if (CanSeePlayer(out _)) EnterFiring();
        else EnterSearching();
    }

    private void EnterFiring()
    {
        StopCo();
        _state = State.Firing;
        _nextFireTime = Time.time + 0.05f;
        _co = StartCoroutine(CoFiring());
    }

    private IEnumerator CoFiring()
    {
        while (_state == State.Firing)
        {
            if (CanSeePlayer(out var center))
            {
                AimLaserTo(center);

                if (Time.time >= _nextFireTime)
                {
                    Fire(center);
                    _nextFireTime = Time.time + Mathf.Max(0.05f, fireInterval);
                }
            }
            else
            {
                EnterSearching();
                yield break;
            }

            yield return null;
        }
    }

    private void EnterSearching()
    {
        if (_state == State.Searching) return;

        StopCo();
        _state = State.Searching;
        _co = StartCoroutine(CoSearchThenRetractAndClose());
    }

    private IEnumerator CoSearchThenRetractAndClose()
    {
        float end = Time.time + Mathf.Max(0.1f, searchTime);
        int step = 0;

        while (Time.time < end)
        {
            if (CanSeePlayer(out _))
            {
                EnterFiring();
                yield break;
            }

            float targetYaw = 0f;
            if (step == 1) targetYaw = -Mathf.Abs(searchYaw);
            else if (step == 2) targetYaw = +Mathf.Abs(searchYaw);

            yield return CoRotateLaserYawTo(targetYaw, searchYawSpeedDeg);

            if (CanSeePlayer(out _))
            {
                EnterFiring();
                yield break;
            }

            step = (step + 1) % 3;
            yield return null;
        }

        yield return CoRotateLaserYawTo(0f, searchYawSpeedDeg);

        yield return CoMoveMuzzleVisuals(extended: false);
        _muzzlesExtended = false;

        yield return CoMoveWings(open: false);
        _wingsOpen = false;

        _state = State.Idle;
        _co = null;
    }

    private void EnterPanic()
    {
        StopCo();
        _state = State.Panic;
        _co = StartCoroutine(CoPanicThenShutdown());
    }

    private IEnumerator CoPanicThenShutdown()
    {
        if (!_wingsOpen)
        {
            yield return CoMoveWings(open: true);
            _wingsOpen = true;
        }

        if (!_muzzlesExtended)
        {
            yield return CoMoveMuzzleVisuals(extended: true);
            _muzzlesExtended = true;
        }

        float end = Time.time + Mathf.Max(0.1f, panicDuration);
        float next = Time.time;

        while (Time.time < end)
        {
            float jitterYaw = Random.Range(-panicAimJitter, panicAimJitter);
            ApplyLaserYawInstant(jitterYaw);

            if (Time.time >= next)
            {
                Vector3 dir = RandomDirectionForwardJitter();
                FireInDirection(dir);
                next = Time.time + Mathf.Max(0.05f, panicFireInterval);
            }

            yield return null;
        }

        _state = State.Shutdown;
        SetLaserEnabled(false);
        _co = null;
    }

    // =========================
    // Wing / Muzzle Visual Animation
    // =========================
    private IEnumerator CoMoveWings(bool open)
    {
        if (!leftWing && !rightWing) yield break;

        float dur = Mathf.Max(0.01f, wingSlideDuration);
        float t = 0f;

        float l0 = leftWing ? leftWing.localPosition.x : 0f;
        float r0 = rightWing ? rightWing.localPosition.x : 0f;

        float l1 = open ? leftWingOpenX : leftWingClosedX;
        float r1 = open ? rightWingOpenX : rightWingClosedX;

        while (t < 1f)
        {
            t += Time.deltaTime / dur;
            float k = Mathf.Clamp01(t);

            if (leftWing)
            {
                Vector3 p = leftWing.localPosition;
                p.x = Mathf.Lerp(l0, l1, k);
                leftWing.localPosition = p;
            }

            if (rightWing)
            {
                Vector3 p = rightWing.localPosition;
                p.x = Mathf.Lerp(r0, r1, k);
                rightWing.localPosition = p;
            }

            yield return null;
        }

        if (leftWing) { var p = leftWing.localPosition; p.x = l1; leftWing.localPosition = p; }
        if (rightWing) { var p = rightWing.localPosition; p.x = r1; rightWing.localPosition = p; }
    }

    private IEnumerator CoMoveMuzzleVisuals(bool extended)
    {
        if (muzzleVisuals == null || muzzleVisuals.Length == 0) yield break;
        if (_muzzleBaseLocalPos == null || _muzzleBaseLocalPos.Length != muzzleVisuals.Length)
            CacheMuzzleVisualBase();

        float dur = Mathf.Max(0.01f, muzzleMoveDuration);
        float t = 0f;

        float[] z0 = new float[muzzleVisuals.Length];
        float[] z1 = new float[muzzleVisuals.Length];

        for (int i = 0; i < muzzleVisuals.Length; i++)
        {
            var m = muzzleVisuals[i];
            if (!m) continue;

            z0[i] = m.localPosition.z;
            float baseZ = _muzzleBaseLocalPos[i].z;
            z1[i] = extended ? (baseZ + muzzleExtendZ) : baseZ;
        }

        while (t < 1f)
        {
            t += Time.deltaTime / dur;
            float k = Mathf.Clamp01(t);

            for (int i = 0; i < muzzleVisuals.Length; i++)
            {
                var m = muzzleVisuals[i];
                if (!m) continue;

                Vector3 p = m.localPosition;
                p.z = Mathf.Lerp(z0[i], z1[i], k);
                m.localPosition = p;
            }

            yield return null;
        }

        for (int i = 0; i < muzzleVisuals.Length; i++)
        {
            var m = muzzleVisuals[i];
            if (!m) continue;

            Vector3 p = m.localPosition;
            p.z = z1[i];
            m.localPosition = p;
        }
    }

    private void SetWingsInstant(bool open)
    {
        if (leftWing)
        {
            Vector3 p = leftWing.localPosition;
            p.x = open ? leftWingOpenX : leftWingClosedX;
            leftWing.localPosition = p;
        }

        if (rightWing)
        {
            Vector3 p = rightWing.localPosition;
            p.x = open ? rightWingOpenX : rightWingClosedX;
            rightWing.localPosition = p;
        }
    }

    private void SetMuzzleVisualsInstant(bool extended)
    {
        if (muzzleVisuals == null || muzzleVisuals.Length == 0) return;
        if (_muzzleBaseLocalPos == null || _muzzleBaseLocalPos.Length != muzzleVisuals.Length)
            CacheMuzzleVisualBase();

        for (int i = 0; i < muzzleVisuals.Length; i++)
        {
            var m = muzzleVisuals[i];
            if (!m) continue;

            Vector3 p = m.localPosition;
            float baseZ = _muzzleBaseLocalPos[i].z;
            p.z = extended ? (baseZ + muzzleExtendZ) : baseZ;
            m.localPosition = p;
        }
    }

    // =========================
    // Laser Pivot Control (몸통 X)
    // =========================
    private void AimLaserTo(Vector3 worldPoint)
    {
        Vector3 origin = eye.position;
        Vector3 dir = worldPoint - origin;
        dir.y = 0f;
        if (dir.sqrMagnitude < 1e-6f) return;

        Vector3 baseForward = transform.forward;
        baseForward.y = 0f;
        if (baseForward.sqrMagnitude < 1e-6f) baseForward = Vector3.forward;
        baseForward.Normalize();

        dir.Normalize();

        float yaw = Vector3.SignedAngle(baseForward, dir, Vector3.up);

        float half = viewAngle * 0.5f;
        yaw = Mathf.Clamp(yaw, -half, half);

        _laserYawTarget = yaw;
        _laserYaw = Mathf.MoveTowardsAngle(_laserYaw, _laserYawTarget, aimYawSpeedDeg * Time.fixedDeltaTime);
        ApplyLaserYawInstant(_laserYaw);
    }

    private IEnumerator CoRotateLaserYawTo(float targetYaw, float speedDeg)
    {
        targetYaw = Mathf.Clamp(targetYaw, -89f, 89f);

        while (Mathf.Abs(Mathf.DeltaAngle(_laserYaw, targetYaw)) > 0.2f)
        {
            _laserYaw = Mathf.MoveTowardsAngle(_laserYaw, targetYaw, Mathf.Max(10f, speedDeg) * Time.deltaTime);
            ApplyLaserYawInstant(_laserYaw);
            yield return null;
        }

        _laserYaw = targetYaw;
        ApplyLaserYawInstant(_laserYaw);
    }

    private void ApplyLaserYawInstant(float yawDeg)
    {
        if (!laserPivot) return;
        laserPivot.localRotation = _laserBaseLocalRot * Quaternion.Euler(0f, yawDeg, 0f);
    }

    private Vector3 GetLaserForward()
    {
        if (!laserPivot) return transform.forward;

        Vector3 localAxis = laserForwardAxis switch
        {
            Axis.Forward => Vector3.forward,
            Axis.Up => Vector3.up,
            Axis.Right => Vector3.right,
            Axis.NegForward => Vector3.back,
            Axis.NegUp => Vector3.down,
            Axis.NegRight => Vector3.left,
            _ => Vector3.forward
        };

        Vector3 f = laserPivot.TransformDirection(localAxis);
        return (f.sqrMagnitude < 1e-6f) ? transform.forward : f.normalized;
    }

    // =========================
    // Fire
    // =========================
    private void Fire(Vector3 worldPoint)
    {
        Vector3 origin = muzzleSpawnPoint ? muzzleSpawnPoint.position : eye.position;
        Vector3 dir = (worldPoint - origin).normalized;

        // 겹침 방지: 총구 forward로 살짝 앞으로
        Vector3 forward = muzzleSpawnPoint ? muzzleSpawnPoint.forward : GetLaserForward();
        origin += forward.normalized * bulletSpawnForwardOffset;

        SpawnBullet(origin, dir);

        if (muzzleFlashL) muzzleFlashL.Play(true);
        if (muzzleFlashR) muzzleFlashR.Play(true);

        SoundManager.PlaySFX(SfxId.Turret_Shoot, worldPos: origin);
    }

    private void FireInDirection(Vector3 dir)
    {
        Vector3 origin = muzzleSpawnPoint ? muzzleSpawnPoint.position : eye.position;

        Vector3 forward = muzzleSpawnPoint ? muzzleSpawnPoint.forward : GetLaserForward();
        origin += forward.normalized * bulletSpawnForwardOffset;

        SpawnBullet(origin, dir.normalized);

        if (muzzleFlashL) muzzleFlashL.Play(true);
        if (muzzleFlashR) muzzleFlashR.Play(true);

        SoundManager.PlaySFX(SfxId.Turret_Shoot, worldPos: origin);
    }

    private void SpawnBullet(Vector3 origin, Vector3 dir)
    {
        if (ObjectPoolManager.Instance == null) return;

        GameObject obj = ObjectPoolManager.Instance.SpawnFromPool(
            bulletPoolKey,
            origin,
            Quaternion.LookRotation(dir, Vector3.up)
        );

        if (!obj) return;

        var b = obj.GetComponent<TurretBullet>();
        if (b)
        {
            b.Launch(dir, bulletSpeed, maxDist: viewDistance, owner: this, returnPoolKey: bulletPoolKey);
        }
        else
        {
            var brb = obj.GetComponent<Rigidbody>();
            if (brb) brb.linearVelocity = dir * bulletSpeed;
        }
    }

    private Vector3 RandomDirectionForwardJitter()
    {
        Vector3 f = GetLaserForward();
        float yaw = Random.Range(-panicAimJitter, panicAimJitter);
        float pitch = Random.Range(-panicAimJitter * 0.25f, panicAimJitter * 0.25f);
        return Quaternion.Euler(pitch, yaw, 0f) * f;
    }

    // =========================
    // Visual (Laser)
    // =========================
    private void UpdateLaserVisual()
    {
        if (!laser || _state == State.Shutdown) return;
        if (!laser.enabled) return;

        Vector3 o = eye.position;
        Vector3 d = GetLaserForward();

        Vector3 end = o + d * laserLength;

        if (laserUseRaycastEnd && laserHitMask.value != 0)
        {
            if (Physics.Raycast(o, d, out RaycastHit hit, laserLength, laserHitMask, QueryTriggerInteraction.Ignore))
                end = hit.point;
        }

        laser.positionCount = 2;
        laser.SetPosition(0, o);
        laser.SetPosition(1, end);
    }

    private void SetLaserEnabled(bool on)
    {
        if (laser) laser.enabled = on;
    }

    // =========================
    // Helpers
    // =========================
    private bool IsUpright()
    {
        return Vector3.Dot(transform.up, Vector3.up) >= uprightDotThreshold;
    }

    private void StopCo()
    {
        if (_co != null) StopCoroutine(_co);
        _co = null;
    }
}
