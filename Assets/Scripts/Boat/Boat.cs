using System;
using UnityEngine;
using Water;

public class Boat : MonoBehaviour, IBuoyancy
{
    [SerializeField] private float mass = 8700f;
    [SerializeField] private float volume = 8.4f;


    private void Start()
    {
        GetComponent<Rigidbody>().mass = mass;
    }

    public float GetDensity()
    {
        return 0;
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
