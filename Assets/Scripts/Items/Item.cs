using UnityEngine;
using Water;

public abstract class Item : MonoBehaviour, IBuoyancy
{
    [SerializeField] private string name;
    [SerializeField] private float mass;
    [SerializeField] private float density;
    [SerializeField] private float volume;
    
    private Rigidbody _rb;
    private Collider _coll;
    
    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _coll = GetComponent<Collider>();
    }

    public void OnPickup(Rigidbody rb)
    {
        rb.mass += this.GetMass();
        if (_rb != null)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.isKinematic = true;
        }
        if (_coll != null)
            _coll.enabled = false;
    }
    
    public void OnStore(Transform container)
    {
        transform.SetParent(container, worldPositionStays: false);
        gameObject.SetActive(false);
    }
    
    public void OnEquip(Transform holdPoint, Vector3 localPosition, Quaternion localRotation)
    {
        transform.SetParent(holdPoint, worldPositionStays: false);
        transform.localPosition = localPosition;
        transform.localRotation = localRotation;
        gameObject.SetActive(true);
    }
    
    public void OnDrop(Vector3 worldPosition, Vector3 throwForce, Rigidbody rb)
    {
        rb.mass -= this.GetMass();
        transform.SetParent(null, worldPositionStays: true);
        transform.position = worldPosition;
        gameObject.SetActive(true);

        if (_coll != null)
            _coll.enabled = true;

        if (_rb != null)
        {
            _rb.isKinematic = false;
            _rb.AddForce(throwForce, ForceMode.Impulse);
        }
    }

    public float GetDensity()
    {
        return density;
    }

    public float GetMass()
    {
        return mass;
    }

    public float GetVolume()
    {
        return volume;
    }
}
