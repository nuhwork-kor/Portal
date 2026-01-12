using System.Collections.Generic;
using UnityEngine;

public class ObjectPoolManager : MonoBehaviour
{
    //싱글톤
    public static ObjectPoolManager Instance { get; private set; }

    [System.Serializable]           //Inspector창에 보여줄 수 있게 해줌
    public class Pool
    {
        public string key;          //풀 이름(Fireball, Bullet, etc)
        public GameObject prefab;   //생성할 프리팹
        public int size;            //미리 생성할 갯수
    }

    [SerializeField] List<Pool> pools;  //인스펙터에서 설정할 풀 목록
    Dictionary<string, Queue<GameObject>> poolDict;     //풀의 이름을 Key값으로 찾기 위함

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        //딕셔너리 초기화
        poolDict = new Dictionary<string, Queue<GameObject>>();

        //각각의 풀 초기화
        foreach (Pool pool in pools)
        {
            //해당 풀의 큐 생성
            Queue<GameObject> objectQueue = new Queue<GameObject>();

            //Size 갯수만큼 오브젝트 미리 생성
            for (int i = 0; i < pool.size; i++)
            {
                //Object Pool Manager 자식으로 생성
                GameObject obj = Instantiate(pool.prefab, transform);
                obj.name = $"{pool.key}_{i}";
                obj.SetActive(false);           //비활성화
                objectQueue.Enqueue(obj);       //큐에 추가
            }

            //딕셔너리에 등록
            //poolDict[pool.key] = objectQueue;
            poolDict.Add(pool.key, objectQueue);
        }
    }

    /// <summary>
    /// 생성하기 -> 풀에서 오브젝트 가져오기
    /// </summary>
    /// <param name="key"></param>
    /// <param name="pos"></param>
    /// <param name="rot"></param>
    /// <returns></returns>
    public GameObject SpawnFromPool(string key, Vector3 pos, Quaternion rot)
    {
        //해당 이름의 풀이 없을 때
        if (!poolDict.ContainsKey(key))
        {
            Debug.LogWarning("Pool에서 Key값을 찾을 수 없음");
            return null;
        }

        Queue<GameObject> q = poolDict[key];

        //큐가 비어있을 때 새로 생성
        if (q.Count == 0)
        {
            //프리팹 찾기
            Pool originalPool = pools.Find(p => p.key == key);
            if (originalPool != null)
            {
                GameObject newObj = Instantiate(originalPool.prefab, transform);
                newObj.name = $"{key}";
                newObj.SetActive(false);
                q.Enqueue(newObj);
            }
        }

        //큐에서 오브젝트 가져오기
        GameObject obj = q.Dequeue();

        //위치 및 회전 설정
        obj.transform.position = pos;
        obj.transform.rotation = rot;

        //활성화
        obj.SetActive(true);

        return obj;
    }

    //삭제하기 -> 풀에 오브젝트 집어넣기
    public void ReturnToPool(string key, GameObject obj)
    {
        //해당 이름의 풀이 없을 때
        if (!poolDict.ContainsKey(key))
        {
            Debug.LogWarning("Pool에서 Key값을 찾을 수 없음");
            return;
        }

        //비활성화
        obj.SetActive(false);

        //큐에 다시 추가
        poolDict[key].Enqueue(obj);
    }

}
