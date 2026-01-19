using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PortalCloneVisual : MonoBehaviour
{
    [Header("Visual Root")]
    [Tooltip("클론으로 복제할 비주얼 루트(모델 루트). 비우면 자기 자신(transform).")]
    [SerializeField] private Transform visualRoot;                                // 클론의 원본이 되는 비주얼 루트 Transform

    [Header("Clone Layer (optional)")]
    [Tooltip("클론을 넣을 레이어 이름. 없으면 원본 레이어 그대로 둠.")]
    [SerializeField] private string cloneLayerName = "PortalClone";               // 클론 오브젝트에 적용할 레이어 이름

    private Portal inPortal;                                                      // 현재 들어간(입구) 포탈
    private Portal outPortal;                                                     // 현재 나오는(출구) 포탈

    private GameObject cloneGO;                                                   // 생성된 클론 GameObject
    private Transform cloneRoot;                                                  // 생성된 클론의 루트 Transform

    private bool active;                                                          // 현재 클론 표시 활성 상태
    private int insideCount;                                                      // 트리거 중첩 진입 카운트(겹침 안전장치)

    private readonly List<(Transform src, Transform dst)> pairs = new();          // 원본-클론 Transform 매핑 리스트(자식 포즈 동기화)

    private static int s_cloneBuildDepth = 0;                                     // Instantiate 과정에서 Awake 재귀 생성 방지용 깊이 카운터

    /// <summary>
    /// Unity Awake: 클론을 1회 생성해두고, 기본은 비활성 상태로 둔다.
    /// </summary>
    private void Awake()
    {
        if (s_cloneBuildDepth > 0)                                                // 지금 이 컴포넌트가 "클론 생성 과정에서" 복제된 쪽이면
        {
            enabled = false;                                                      // 스스로 비활성화해서 재귀 클론 생성을 차단
            return;                                                               // 추가 초기화 중단
        }

        if (!visualRoot) visualRoot = transform;                                  // 비주얼 루트가 비어있으면 자기 자신을 루트로 사용

        BuildCloneOnce();                                                         // 클론 오브젝트를 1회 생성/캐싱
        SetCloneActive(false);                                                    // 시작은 클론 비활성
    }

    /// <summary>
    /// Unity OnDestroy: 만들어둔 클론 GameObject를 정리한다.
    /// </summary>
    private void OnDestroy()
    {
        if (cloneGO) Destroy(cloneGO);                                            // 클론이 존재하면 파괴
    }

    /// <summary>
    /// Unity LateUpdate: 클론이 활성 상태일 때, 포탈 변환으로 위치/회전을 갱신하고 자식 트랜스폼 포즈를 동기화한다.
    /// </summary>
    private void LateUpdate()
    {
        if (!active) return;                                                      // 활성 상태가 아니면 갱신 불필요
        if (!inPortal || !outPortal) { SetCloneActive(false); return; }           // 포탈 참조가 끊기면 클론 off
        if (!inPortal.IsPlaced || !outPortal.IsPlaced) { SetCloneActive(false); return; } // 포탈이 미배치면 클론 off

        UpdateClonePose();                                                        // 클론 루트의 월드 포즈를 포탈 변환으로 갱신
        SyncChildTransforms();                                                    // 자식 로컬 포즈를 원본과 동일하게 복사
    }

    // ---------- Called from PortalTrigger ----------

    /// <summary>
    /// 포탈 트리거 진입 시 호출: 클론 표시를 시작하고 포탈 쌍을 설정한다.
    /// </summary>
    /// <param name="inP">입구 포탈</param>
    /// <param name="outP">출구 포탈</param>
    public void Begin(Portal inP, Portal outP)
    {
        if (!inP || !outP) return;                                                // 포탈 참조가 없으면 처리 불가

        if (active && (inPortal != inP || outPortal != outP))                     // 이미 활성인데 포탈 쌍이 달라지면
            ForceEnd();                                                           // 기존 상태를 강제로 종료(상태 꼬임 방지)

        if (insideCount == 0)                                                     // 최초 진입 프레임에서만 포탈 참조 세팅
        {
            inPortal = inP;                                                       // 입구 포탈 저장
            outPortal = outP;                                                     // 출구 포탈 저장
            SetCloneActive(true);                                                 // 클론 표시 활성화

            UpdateClonePose();                                                    // 즉시 포즈 갱신
            SyncChildTransforms();                                                // 즉시 자식 포즈 동기화
        }

        insideCount++;                                                            // 트리거 중첩 카운트 증가
        active = true;                                                            // 활성 상태 true
    }

    /// <summary>
    /// 포탈 트리거에서 나갈 때 호출: 중첩 카운트를 줄이고 0이면 클론 표시를 종료한다.
    /// </summary>
    /// <param name="exitedPortal">나간 포탈(입구 포탈과 일치할 때만 종료)</param>
    public void NotifyTriggerExit(Portal exitedPortal)
    {
        if (!active) return;                                                      // 활성 상태가 아니면 무시
        if (exitedPortal != null && inPortal != null && exitedPortal != inPortal) // 다른 포탈 트리거 exit이면 무시
            return;                                                               // 중첩된 다른 트리거 처리를 막음

        insideCount = Mathf.Max(insideCount - 1, 0);                               // 중첩 카운트 감소(0 아래로 내려가지 않게)
        if (insideCount > 0) return;                                              // 아직 남아있으면 종료하지 않음

        ForceEnd();                                                               // 완전 종료
    }

    /// <summary>
    /// 클론 표시 상태를 강제로 종료하고 모든 상태를 리셋한다.
    /// </summary>
    public void ForceEnd()
    {
        insideCount = 0;                                                          // 중첩 카운트 초기화
        active = false;                                                           // 활성 상태 false
        inPortal = null;                                                          // 입구 포탈 참조 해제
        outPortal = null;                                                         // 출구 포탈 참조 해제
        SetCloneActive(false);                                                    // 클론 표시 비활성
    }

    /// <summary>
    /// 워프가 발생했을 때 호출: 현재 포탈 쌍을 새 쌍으로 갱신한다.
    /// </summary>
    /// <param name="newIn">새 입구 포탈</param>
    /// <param name="newOut">새 출구 포탈</param>
    public void OnWarped(Portal newIn, Portal newOut)
    {
        if (!active) return;                                                      // 활성 상태가 아니면 갱신 불필요
        if (!newIn || !newOut) return;                                            // 포탈 참조가 없으면 갱신 불가

        inPortal = newIn;                                                         // 입구 포탈 갱신
        outPortal = newOut;                                                       // 출구 포탈 갱신
    }

    // ---------- Clone building ----------

    /// <summary>
    /// 클론 GameObject를 1회 생성하고, 물리/스크립트를 무력화한 뒤 Transform 매핑을 구축한다.
    /// </summary>
    private void BuildCloneOnce()
    {
        if (cloneGO != null) return;                                              // 이미 생성돼 있으면 재생성하지 않음

        s_cloneBuildDepth++;                                                      // 클론 생성 깊이 증가(재귀 방지)
        try
        {
            cloneGO = Instantiate(visualRoot.gameObject);                         // 비주얼 루트 GameObject를 그대로 복제
        }
        finally
        {
            s_cloneBuildDepth--;                                                  // 깊이 감소(try/finally로 안전하게 복구)
        }

        cloneGO.name = $"{visualRoot.name}_PortalClone";                          // 클론 오브젝트 이름 지정(디버깅 편의)
        cloneRoot = cloneGO.transform;                                            // 클론 루트 Transform 캐싱

        cloneGO.SetActive(false);                                                 // 생성 즉시 비활성화(Start/Update 실행 최소화)

        cloneRoot.SetParent(null, true);                                          // 씬에 독립 오브젝트로 두기(부모 영향 제거)

        int layer = LayerMask.NameToLayer(cloneLayerName);                        // 레이어 이름을 인덱스로 변환
        if (layer >= 0) SetLayerRecursively(cloneRoot, layer);                    // 레이어가 존재하면 재귀로 설정

        DisableClonePhysics(cloneGO);                                             // 콜라이더/리지드바디 무력화
        DisableCloneBehaviours(cloneGO);                                          // 모든 MonoBehaviour/Animator 비활성화

        pairs.Clear();                                                            // 기존 매핑 제거
        BuildPairsRecursive(visualRoot, cloneRoot);                               // 원본-클론 Transform 매핑 구축

        cloneGO.tag = "Untagged";                                                 // 클론 태그 제거(게임 로직 영향 방지)
    }

    /// <summary>
    /// 클론 GameObject의 활성/비활성을 설정한다.
    /// </summary>
    /// <param name="on">true면 활성, false면 비활성</param>
    private void SetCloneActive(bool on)
    {
        if (!cloneGO) return;                                                     // 클론이 없으면 처리 불가
        if (cloneGO.activeSelf == on) return;                                     // 이미 원하는 상태면 변경 불필요
        cloneGO.SetActive(on);                                                    // 활성 상태 적용
    }

    /// <summary>
    /// 포탈 변환을 이용해 클론 루트의 월드 위치/회전을 갱신한다.
    /// </summary>
    private void UpdateClonePose()
    {
        Transform inT = inPortal.Plane;                                           // 입구 포탈 평면 Transform
        Transform outT = outPortal.Plane;                                         // 출구 포탈 평면 Transform

        Vector3 newPos = PortalMath.TransformPoint(visualRoot.position, inT, outT);      // 원본 월드 위치를 포탈로 변환
        Quaternion newRot = PortalMath.TransformRotation(visualRoot.rotation, inT, outT); // 원본 월드 회전을 포탈로 변환

        cloneRoot.SetPositionAndRotation(newPos, newRot);                         // 클론 루트에 포즈 적용
        cloneRoot.localScale = visualRoot.lossyScale;                             // 스케일은 원본의 lossyScale을 그대로 복사
    }

    /// <summary>
    /// 원본과 클론의 자식 Transform 로컬 포즈를 동기화한다.
    /// </summary>
    private void SyncChildTransforms()
    {
        for (int i = 0; i < pairs.Count; i++)                                     // 매핑 리스트를 순회하면서
        {
            var (s, d) = pairs[i];                                                // src(원본), dst(클론) 가져오기
            if (!s || !d) continue;                                               // 둘 중 하나라도 null이면 스킵
            d.localPosition = s.localPosition;                                    // 로컬 위치 동기화
            d.localRotation = s.localRotation;                                    // 로컬 회전 동기화
            d.localScale = s.localScale;                                          // 로컬 스케일 동기화
        }
    }

    /// <summary>
    /// 원본-클론 Transform 트리를 동일 인덱스 기준으로 재귀 매핑한다.
    /// </summary>
    /// <param name="src">원본 Transform</param>
    /// <param name="dst">클론 Transform</param>
    private void BuildPairsRecursive(Transform src, Transform dst)
    {
        int n = Mathf.Min(src.childCount, dst.childCount);                        // 자식 수가 다를 수 있으니 min으로 제한
        for (int i = 0; i < n; i++)                                               // 자식들을 순회하면서
        {
            Transform s = src.GetChild(i);                                        // 원본 자식
            Transform d = dst.GetChild(i);                                        // 클론 자식
            pairs.Add((s, d));                                                    // 매핑 추가
            BuildPairsRecursive(s, d);                                            // 재귀로 하위 트리 매핑
        }
    }

    /// <summary>
    /// Transform 트리 전체에 레이어를 재귀적으로 적용한다.
    /// </summary>
    /// <param name="root">레이어를 적용할 루트 Transform</param>
    /// <param name="layer">적용할 레이어 인덱스</param>
    private static void SetLayerRecursively(Transform root, int layer)
    {
        root.gameObject.layer = layer;                                            // 현재 노드 레이어 설정
        for (int i = 0; i < root.childCount; i++)                                 // 자식 순회
            SetLayerRecursively(root.GetChild(i), layer);                         // 재귀 적용
    }

    // =========================
    // ✅ Clone 무력화 유틸
    // =========================

    /// <summary>
    /// 클론 오브젝트의 물리(콜라이더/리지드바디)를 모두 무력화한다.
    /// </summary>
    /// <param name="go">대상 GameObject</param>
    private static void DisableClonePhysics(GameObject go)
    {
        var cols = go.GetComponentsInChildren<Collider>(true);                    // 모든 콜라이더 가져오기(비활성 포함)
        for (int i = 0; i < cols.Length; i++)                                     // 콜라이더 순회
        {
            if (!cols[i]) continue;                                               // null이면 스킵
            cols[i].enabled = false;                                              // 콜라이더 비활성(충돌/레이캐스트 영향 제거)
        }

        var rbs = go.GetComponentsInChildren<Rigidbody>(true);                    // 모든 리지드바디 가져오기(비활성 포함)
        for (int i = 0; i < rbs.Length; i++)                                      // 리지드바디 순회
        {
            if (!rbs[i]) continue;                                                // null이면 스킵
            rbs[i].linearVelocity = Vector3.zero;                                 // 선속도 초기화
            rbs[i].angularVelocity = Vector3.zero;                                // 각속도 초기화
            rbs[i].useGravity = false;                                            // 중력 비활성
            rbs[i].isKinematic = true;                                            // 키네마틱으로 물리 시뮬레이션 제외
            rbs[i].detectCollisions = false;                                      // 충돌 감지 비활성
        }
    }

    /// <summary>
    /// 클론 오브젝트의 동작성 컴포넌트(MonoBehaviour/Animator)를 모두 비활성화한다.
    /// </summary>
    /// <param name="go">대상 GameObject</param>
    private static void DisableCloneBehaviours(GameObject go)
    {
        var mbs = go.GetComponentsInChildren<MonoBehaviour>(true);                // 모든 MonoBehaviour 가져오기(비활성 포함)
        for (int i = 0; i < mbs.Length; i++)                                      // 순회
        {
            if (!mbs[i]) continue;                                                // null이면 스킵
            mbs[i].enabled = false;                                               // 스크립트 비활성(로직 실행 차단)
        }

        var anims = go.GetComponentsInChildren<Animator>(true);                   // Animator 가져오기
        for (int i = 0; i < anims.Length; i++)                                    // 순회
        {
            if (!anims[i]) continue;                                              // null이면 스킵
            anims[i].enabled = false;                                             // Animator 비활성(애니메이션 갱신 차단)
        }
    }
}
