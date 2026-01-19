using System.Collections.Generic;
using UnityEngine;

// 이 스크립트는 "오브젝트 풀링"을 담당한다.
// - Pool(key/prefab/size) 목록을 인스펙터로 받아, Awake에서 미리 size 만큼 생성해 비활성화 상태로 큐에 저장한다.
// - SpawnFromPool: 큐에서 하나 꺼내 활성화 후 반환(큐가 비면 prefab으로 추가 생성)
// - ReturnToPool: 비활성화 후 큐에 다시 넣는다.
// - DontDestroyOnLoad 싱글톤으로 동작한다(Instance).
public class ObjectPoolManager : MonoBehaviour
{
    // 싱글톤
    public static ObjectPoolManager Instance { get; private set; }                    // 전역 접근 인스턴스

    [System.Serializable]
    public class Pool
    {
        public string key;                                                             // 풀 이름(예: PortalBullet)
        public GameObject prefab;                                                      // 생성할 프리팹
        public int size;                                                               // 미리 생성할 개수
    }

    [SerializeField] List<Pool> pools;                                                 // 인스펙터에서 설정할 풀 목록
    Dictionary<string, Queue<GameObject>> poolDict;                                    // key -> 오브젝트 큐

    /// <summary>
    /// 유니티 생명주기: 싱글톤 초기화 + 풀 생성/프리워밍.
    /// </summary>
    private void Awake()
    {
        if (Instance == null)                                                          // 최초 인스턴스면
        {
            Instance = this;                                                           // Instance 설정
            DontDestroyOnLoad(gameObject);                                             // 씬 전환 유지
        }
        else                                                                           // 이미 있으면
        {
            Destroy(gameObject);                                                       // 중복 제거
            return;                                                                    // 종료
        }

        poolDict = new Dictionary<string, Queue<GameObject>>();                        // 딕셔너리 초기화

        foreach (Pool pool in pools)                                                   // 풀 목록 순회
        {
            Queue<GameObject> objectQueue = new Queue<GameObject>();                   // 해당 풀의 큐 생성

            for (int i = 0; i < pool.size; i++)                                        // size 만큼 미리 생성
            {
                GameObject obj = Instantiate(pool.prefab, transform);                  // 매니저 자식으로 생성
                obj.name = $"{pool.key}_{i}";                                          // 이름 지정(디버그 편의)
                obj.SetActive(false);                                                  // 비활성화
                objectQueue.Enqueue(obj);                                              // 큐에 추가
            }

            poolDict.Add(pool.key, objectQueue);                                       // key로 큐 등록
        }
    }

    /// <summary>
    /// 풀에서 오브젝트를 꺼내 활성화하여 반환한다.
    /// - 큐가 비면 해당 풀의 prefab으로 추가 생성한다.
    /// </summary>
    /// <param name="key">풀 키</param>
    /// <param name="pos">스폰 위치</param>
    /// <param name="rot">스폰 회전</param>
    /// <returns>스폰된 오브젝트(없으면 null)</returns>
    public GameObject SpawnFromPool(string key, Vector3 pos, Quaternion rot)
    {
        if (!poolDict.ContainsKey(key))                                                // 해당 키 풀이 없으면
        {
            Debug.LogWarning("Pool에서 Key값을 찾을 수 없음");                         // 경고 로그
            return null;                                                               // 실패
        }

        Queue<GameObject> q = poolDict[key];                                           // 해당 큐

        if (q.Count == 0)                                                              // 큐가 비어있으면
        {
            Pool originalPool = pools.Find(p => p.key == key);                         // 원본 풀 정보 찾기
            if (originalPool != null)                                                  // 찾았으면
            {
                GameObject newObj = Instantiate(originalPool.prefab, transform);       // 추가 생성
                newObj.name = $"{key}";                                                // 이름 지정
                newObj.SetActive(false);                                               // 비활성화
                q.Enqueue(newObj);                                                     // 큐에 넣기
            }
        }

        GameObject obj = q.Dequeue();                                                  // 큐에서 하나 꺼내기

        obj.transform.position = pos;                                                  // 위치 세팅
        obj.transform.rotation = rot;                                                  // 회전 세팅

        obj.SetActive(true);                                                           // 활성화

        return obj;                                                                    // 반환
    }

    /// <summary>
    /// 오브젝트를 풀에 반환한다(비활성화 후 큐에 다시 넣기).
    /// </summary>
    /// <param name="key">풀 키</param>
    /// <param name="obj">반환할 오브젝트</param>
    public void ReturnToPool(string key, GameObject obj)
    {
        if (!poolDict.ContainsKey(key))                                                // 해당 키 풀이 없으면
        {
            Debug.LogWarning("Pool에서 Key값을 찾을 수 없음");                         // 경고 로그
            return;                                                                    // 종료
        }

        obj.SetActive(false);                                                          // 비활성화
        poolDict[key].Enqueue(obj);                                                    // 큐에 다시 추가
    }
}
