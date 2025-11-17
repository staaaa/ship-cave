using UnityEngine;

public class WaterChecker : MonoBehaviour
{
    public bool IsInWater { get; private set; }

    [SerializeField] private LayerMask waterLayer;
    [SerializeField] private Stats playerStats;

    private void Awake()
    {
        if (playerStats.isLocalPlayer == false)
        {
            this.enabled = false;
        }
    }
    private void OnTriggerEnter(Collider other)
    {
        if (IsInLayerMask(other.gameObject, waterLayer))
        {
            IsInWater = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsInLayerMask(other.gameObject, waterLayer))
        {
            IsInWater = false;
        }
    }

    private bool IsInLayerMask(GameObject obj, LayerMask layerMask)
    {
        return (layerMask.value & (1 << obj.layer)) != 0;
    }
}