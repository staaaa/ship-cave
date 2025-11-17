using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Water.Waves
{
    //Water volume integrated with WaveSystem 
    public class WaterVolume : MonoBehaviour
    {
        [Header("Buoyancy")] 
        [Tooltip("Water density")]
        public float density = 1000f;
        public float linearDamping = 2f;

        [Header("Surface")] 
        [Tooltip("Optional top collider offset")]
        public float surfaceOffset = 0f;
        
        [Header("Wave Forces")]
        [SerializeField] private bool applyWaveForces = true;
        [SerializeField, Range(0f, 10f)] private float waveForceMultiplier = 1f;
        [SerializeField, Range(0f, 5f)] private float waveTorqueMultiplier = 1f;
        [SerializeField] private float maxAngularVelocity = 4f;
        [SerializeField] private float angularDamping = 4f;
    
        [Header("Drag Forces")]
        [SerializeField] private bool applyWaterDrag = true;
        [SerializeField, Range(0f, 5f)] private float dragCoefficient = 1f;
    
        [Header("Centroid Buoyancy")]
        [SerializeField] private int samplesX = 3;    
        [SerializeField] private int samplesY = 2;
        [SerializeField] private int samplesZ = 5;
        [SerializeField] private float sampleSurfaceToleranceForDepth = 1.0f;

        private readonly Dictionary<Rigidbody, HashSet<Collider>> _rigidbodyColliders = new();
        private readonly Dictionary<Rigidbody, Water.IBuoyancy> _rigidbodyBuoyancy = new();
        
        private Collider _collider;
        private WaveSystem _waveSystem;

        private void Awake()
        {
            _collider = GetComponent<Collider>();
            if (!_collider.isTrigger) _collider.isTrigger = true;
            
            _waveSystem = WaveSystem.Instance;
            if (_waveSystem == null)
            {
                Debug.LogError("WaveSystem not found! Create a GameObject with WaveSystem component.");
            }
        }
        
        private void OnTriggerEnter(Collider other)
        {
            var rb = other.attachedRigidbody;
            if (other.gameObject.CompareTag("NO-VOLUME")) return;
            if (rb == null) return;
        
            if (!_rigidbodyColliders.ContainsKey(rb))
            {
                _rigidbodyColliders[rb] = new HashSet<Collider>();
                
                if (rb.name != "boat")
                {
                    rb.linearDamping = linearDamping;
                    rb.angularDamping = angularDamping;
                }
                
                var buoyant = rb.GetComponent<Water.IBuoyancy>();
                if (buoyant != null)
                {
                    _rigidbodyBuoyancy[rb] = buoyant;
                }
            }
            
            _rigidbodyColliders[rb].Add(other);
        }

        private void OnTriggerExit(Collider other)
        {
            var rb = other.attachedRigidbody;
            if (rb == null) return;
        
            if (!_rigidbodyColliders.ContainsKey(rb)) return;
            
            _rigidbodyColliders[rb].Remove(other);
            
            if (_rigidbodyColliders[rb].Count == 0)
            {
                _rigidbodyColliders.Remove(rb);
                _rigidbodyBuoyancy.Remove(rb);
                
                if (rb.name != "boat")
                {
                    rb.linearDamping = 0;
                    rb.angularDamping = 0.05f;
                }
            }
        }

        private void FixedUpdate()
        {
            if (_waveSystem == null) return;

            float time = Time.time;
            
            foreach (var kvp in _rigidbodyBuoyancy)
            {
                Rigidbody rb = kvp.Key;
                var buoyant = kvp.Value;
                
                if (rb == null) continue;
                
                float objVolume = buoyant.GetVolume();
                float objMass = buoyant.GetMass();
                float submerged = CalculateSubmersion(rb, time);
                if (submerged <= 0f) continue;
                
                ApplyBuoyancyForces(rb, objMass, objVolume, time);
                
                if (applyWaveForces)
                {
                    ApplyWaveForces(rb, submerged, time);
                }
                
                if (applyWaterDrag)
                {
                    ApplyWaterDrag(rb, submerged, time);
                }
            }
        }
        
        private float CalculateSubmersion(Rigidbody rb, float time)
        {
            // Użyj wszystkich colliderów do obliczenia zanurzenia
            if (!_rigidbodyColliders.ContainsKey(rb)) return 0f;
            
            HashSet<Collider> colliders = _rigidbodyColliders[rb];
            if (colliders.Count == 0) return 0f;

            // Oblicz combined bounds ze wszystkich colliderów
            Bounds combinedBounds = new Bounds();
            bool initialized = false;
            
            foreach (Collider col in colliders)
            {
                if (col == null) continue;
                
                if (!initialized)
                {
                    combinedBounds = col.bounds;
                    initialized = true;
                }
                else
                {
                    combinedBounds.Encapsulate(col.bounds);
                }
            }
            
            if (!initialized) return 0f;

            Vector3 center = combinedBounds.center;
            float surfaceY = _waveSystem.GetWaveHeight(center.x, center.z, time);
            float bottomY = combinedBounds.min.y;
            float topY = combinedBounds.max.y;

            return Mathf.Clamp01((surfaceY - bottomY) / (topY - bottomY));
        }
        
        private (float submergedVolume, Vector3 centroidWorld) CalculateSubmergedCentroidAndVolume(Rigidbody rb, float objectVolume, float time)
        {
            if (!_rigidbodyColliders.ContainsKey(rb)) return (0f, Vector3.zero);
            
            HashSet<Collider> colliders = _rigidbodyColliders[rb];
            if (colliders.Count == 0) return (0f, Vector3.zero);

            int cx = Mathf.Max(1, samplesX);
            int cy = Mathf.Max(1, samplesY);
            int cz = Mathf.Max(1, samplesZ);
            
            Vector3 accuPos = Vector3.zero;
            float accuVol = 0f;
            
            foreach (Collider col in colliders)
            {
                if (col == null) continue;
                
                // Bounds w przestrzeni świata
                Bounds bounds = col.bounds;
                
                int totalSamplesThisCollider = cx * cy * cz;
                float volumePerCollider = objectVolume / colliders.Count;
                float perSampleVolume = volumePerCollider / totalSamplesThisCollider;

                for (int ix = 0; ix < cx; ix++)
                {
                    float fx = (cx == 1) ? 0.5f : ((float)ix / (cx - 1));
                    for (int iy = 0; iy < cy; iy++)
                    {
                        float fy = (cy == 1) ? 0.5f : ((float)iy / (cy - 1));
                        for (int iz = 0; iz < cz; iz++)
                        {
                            float fz = (cz == 1) ? 0.5f : ((float)iz / (cz - 1));

                            // Próbkuj bezpośrednio w bounds w przestrzeni świata
                            Vector3 worldPoint = new Vector3(
                                Mathf.Lerp(bounds.min.x, bounds.max.x, fx),
                                Mathf.Lerp(bounds.min.y, bounds.max.y, fy),
                                Mathf.Lerp(bounds.min.z, bounds.max.z, fz)
                            );
                            
                            float surfaceY = _waveSystem.GetWaveHeight(worldPoint.x, worldPoint.z, time);
                            float depth = surfaceY - worldPoint.y;

                            if (depth <= 0f) continue;

                            float weight = Mathf.Clamp01(
                                depth / Mathf.Max(0.0001f, sampleSurfaceToleranceForDepth)
                            );

                            float contributedVol = perSampleVolume * weight;
                            accuVol += contributedVol;
                            accuPos += worldPoint * contributedVol;
                        }
                    }
                }
            }

            if (accuVol <= 0f) return (0f, Vector3.zero);

            Vector3 centroid = accuPos / accuVol;
            return (accuVol, centroid);
        }
        
        private void ApplyBuoyancyForces(Rigidbody rb, float objMass, float objVolume, float time)
        {
            var (submergedVolume, centroid) = CalculateSubmergedCentroidAndVolume(rb, objVolume, time);
            if (submergedVolume <= 0f) return;
        
            float gravity = _waveSystem.GetGravity();
            float Fw = density * gravity * submergedVolume;  
            float Fg = objMass * gravity;                    
            float finalForce = Fw - Fg;                      
        
            rb.AddForceAtPosition(Vector3.up * finalForce, centroid, ForceMode.Force);
        }

        private void ApplyWaveForces(Rigidbody rb, float submergedFraction, float time)
        {
            Vector3 rbPos = rb.worldCenterOfMass;

            Vector3 waveNormal = _waveSystem.GetWaveNormal(rbPos, time);

            Vector3 waveVelocity = _waveSystem.GetWaveVelocity(rbPos, time);

            Vector3 directionalForce = waveVelocity * waveForceMultiplier * submergedFraction * rb.mass;
            rb.AddForce(directionalForce, ForceMode.Force);

            Vector3 targetUp = waveNormal;
            Vector3 currentUp = rb.transform.up;
            Vector3 torqueAxis = Vector3.Cross(currentUp, targetUp);
            float torqueMag = torqueAxis.magnitude * waveTorqueMultiplier * submergedFraction;

            if (torqueMag > 0.01f)
            {
                rb.AddTorque(torqueAxis.normalized * torqueMag, ForceMode.Force);
            }

            if (rb.angularVelocity.magnitude > maxAngularVelocity)
            {
                rb.angularVelocity = rb.angularVelocity.normalized * maxAngularVelocity;
            }
        }
        
        private void ApplyWaterDrag(Rigidbody rb, float submergedFraction, float time)
        {
            Vector3 rbPos = rb.worldCenterOfMass;
            Vector3 waveVelocity = _waveSystem.GetWaveVelocity(rbPos, time);
            
            Vector3 relativeVelocity = rb.linearVelocity - waveVelocity;
        
            float dragMagnitude = 0.5f * dragCoefficient * relativeVelocity.sqrMagnitude * submergedFraction;
            Vector3 dragForce = -relativeVelocity.normalized * dragMagnitude;
        
            rb.AddForce(dragForce, ForceMode.Force);
        }
    }
}