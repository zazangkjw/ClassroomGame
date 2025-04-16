using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public static CameraFollow Singleton { get; private set; }

    //[SerializeField] private Highlighter highlighter;

    private Transform target;

    private void Awake()
    {
        if (Singleton != null)
        {
            Destroy(gameObject);
            return;
        }

        Singleton = this;
        DontDestroyOnLoad(gameObject);
    }

    private void LateUpdate()
    {
        if (target != null)
        {
            transform.SetPositionAndRotation(target.position, target.rotation);
            //highlighter.UpdateHighlightable(transform.position, transform.forward, player);
        }
    }

    public void SetTarget(Transform newTarget, Player player)
    {
        target = newTarget;
    }
}
