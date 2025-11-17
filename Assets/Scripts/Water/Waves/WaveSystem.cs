using UnityEngine;

namespace Water.Waves
{
    [DefaultExecutionOrder(-100)]
    public class WaveSystem : MonoBehaviour
    {
        public static WaveSystem Instance
        {
            get;
            private set;
        }

        [System.Serializable]
        public struct Wave
        {
            public Vector2 direction;
            public float waveLength;
            public float amplitude;
            public float speed;
            public float steepness;

            [HideInInspector] public float frequency;
            [HideInInspector] public float phase;
        }

        [Header("Procedural wave configuration")] 
        [SerializeField] private int waveCount = 100;

        [Header("Ocean spectrum settings")]
        [Tooltip("Dominant direction of wind (0-360 degrees)")]
        [SerializeField, Range(0f, 360f)] private float windDirection = 0f;
        
        [Tooltip("Wave direction dispersion around the wind (degrees)")]
        [SerializeField, Range(0f, 180f)] private float directionalSpread = 45f;
        
        [Tooltip("Speed of wind (affects the size of waves)")]
        [SerializeField, Range(0f, 1000f)] private float windSpeed = 10f;
        
        [Tooltip("Wind fetch range")]
        [SerializeField, Range(1f, 10000f)] private float fetch = 100f;
        
        
        [Header("Wind size range")]
        [SerializeField] private float minWaveLength = 0.5f;
        [SerializeField] private float maxWaveLength = 50f;
        
        [Header("Wave amplitude boost")]
        [SerializeField] private float amplitudeMultiplier = 2.0f;
        
        [Header("Advanced")]
        [SerializeField] private int randomSeed = 12345;
        [SerializeField, Range(0f, 1f)] private float steepnessGlobal = 0.3f;

        private Wave[] waves;
        
        [Header("Water properties")]
        [SerializeField] private float waterLevel = 0f;
        [SerializeField] private float gravity = 9.81f;

        [Header("Gizmos")] 
        [SerializeField] private int gridSize = 50;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            GenerateOceanSpectrum();
            InitializeWaves();
        }

        //Describes wave's energy distribution in ocean
        private float JONSWAPSpectrum(float f, float fp, float U, float F, float g)
        {
            float alpha = 0.076f * Mathf.Pow(U * U / (F * g), 0.22f); // scaling parameter
            float gamma = 3.3f; // peak enhancement factor
        
            float sigma = (f <= fp) ? 0.07f : 0.09f;
            float r = Mathf.Exp(-Mathf.Pow(f - fp, 2f) / (2f * sigma * sigma * fp * fp));
        
            float S_pm = (alpha * g * g) / Mathf.Pow(2f * Mathf.PI * f, 5f) * 
                         Mathf.Exp(-1.25f * Mathf.Pow(fp / f, 4f));
        
            float S_jonswap = S_pm * Mathf.Pow(gamma, r);
        
            return S_jonswap;
        }
        
        //Generates spectrum of ocean waves using simplified JONSWAP model
        //Simulates natural wave's energy distribution in ocean 
        private void GenerateOceanSpectrum()
        {
            Random.InitState(randomSeed);
            waves = new Wave[waveCount];

            float windAngle = windDirection * Mathf.Deg2Rad;
            Vector2 windDir = new Vector2(Mathf.Cos(windAngle), Mathf.Sin(windAngle));
            
            //Params of JONSWAP spectrum
            float g = gravity;
            float U = windSpeed;
            float F = fetch * 1000f; //km -> m
            
            //Peak frequency of the dominant wave
            float fp = 3.5f * g / (2f * Mathf.PI * U);
            float peakWavelength = (2f * Mathf.PI * U * U) / (3.5f * g);
            
            
            //Logarithmic distribution of waves (more small, less big)
            for (int i = 0; i < waveCount; i++)
            {
                float t = (float)i / (waveCount - 1);
                float wavelength = Mathf.Lerp(
                    Mathf.Log(minWaveLength),
                    Mathf.Log(maxWaveLength),
                    t
                );

                wavelength = Mathf.Exp(wavelength);

                float frequency = 2f * Mathf.PI / wavelength;
                float f = Mathf.Sqrt(g * frequency / (2f * Mathf.PI));

                float energy = JONSWAPSpectrum(f, fp, U, F, g);
                
                float amplitude = Mathf.Sqrt(2f * energy * (maxWaveLength - minWaveLength) / waveCount) * amplitudeMultiplier;
                
                float angleOffset = Random.Range(-directionalSpread, directionalSpread) * Mathf.Deg2Rad;
                float cosSpread = Mathf.Cos(angleOffset);
                angleOffset *= Mathf.Abs(cosSpread);
                float waveAngle = windAngle + angleOffset;
                Vector2 direction = new Vector2(Mathf.Cos(waveAngle), Mathf.Sin(waveAngle));
                
                float speed = Mathf.Sqrt(g * wavelength / (2f * Mathf.PI));
                
                float steepness = steepnessGlobal * Mathf.Lerp(0.8f, 0.3f, t);
                
                waves[i] = new Wave
                {
                    direction = direction,
                    waveLength = wavelength,
                    amplitude = amplitude,
                    speed = speed,
                    steepness = steepness
                };
                
            }
            Debug.Log($"Generated {waveCount} procedural ocean waves (wind: {windSpeed}m/s @ {windDirection}°)");

        }

        // Calculates frequencies and phases of each wave
        private void InitializeWaves()
        {
            for (int i = 0; i <  waveCount; i++)
            {
                waves[i].direction.Normalize();
                waves[i].frequency = 2f * Mathf.PI / waves[i].waveLength;
                waves[i].phase = waves[i].speed * waves[i].frequency;
            }
        }

        [ContextMenu("Regenerate Waves")]
        public void RegenerateWaves()
        {
            GenerateOceanSpectrum();
            InitializeWaves();
            Debug.Log("Waves regenerated!");
        }
        
        //Calculates the position of the point on the surface of the water
        public Vector3 GetWavePosition(Vector3 worldPos, float time)
        {
            Vector3 result = worldPos;
            result.y = waterLevel;

            for (int i = 0; i < waves.Length; i++)
            {
                Wave w = waves[i];
            
                float projection = Vector2.Dot(w.direction, new Vector2(worldPos.x, worldPos.z));
                float theta = projection * w.frequency + time * w.phase;
                float sinTheta = Mathf.Sin(theta);
                float cosTheta = Mathf.Cos(theta);
            
                // Gerstner wave displacement
                float steepnessFactor = w.steepness / (w.frequency * waves.Length);
                result.x += w.direction.x * steepnessFactor * w.amplitude * cosTheta;
                result.z += w.direction.y * steepnessFactor * w.amplitude * cosTheta;
                result.y += w.amplitude * sinTheta;
            }

            return result;
        }
        
        //Calculates the normal vector of the point on the surface of the water
        public Vector3 GetWaveNormal(Vector3 worldPos, float time)
        {
            Vector3 normal = Vector3.up;

            for (int i = 0; i < waves.Length; i++)
            {
                Wave w = waves[i];
            
                float projection = Vector2.Dot(w.direction, new Vector2(worldPos.x, worldPos.z));
                float theta = projection * w.frequency + time * w.phase;
                float cosTheta = Mathf.Cos(theta);
            
                float wa = w.frequency * w.amplitude;
                float s = w.steepness * wa;
            
                normal.x -= w.direction.x * wa * cosTheta;
                normal.z -= w.direction.y * wa * cosTheta;
                normal.y -= s * Mathf.Sin(theta);
            }

            return normal.normalized;
        }
        
        //Calculates the velocity of water in given point on the surface of the water
        public Vector3 GetWaveVelocity(Vector3 worldPos, float time)
        {
            Vector3 velocity = Vector3.zero;

            for (int i = 0; i < waves.Length; i++)
            {
                Wave w = waves[i];
            
                float projection = Vector2.Dot(w.direction, new Vector2(worldPos.x, worldPos.z));
                float theta = projection * w.frequency + time * w.phase;
                float cosTheta = Mathf.Cos(theta);
            
                float speedFactor = w.phase * w.amplitude;
            
                velocity.x += w.direction.x * speedFactor * cosTheta;
                velocity.z += w.direction.y * speedFactor * cosTheta;
                velocity.y += w.phase * w.amplitude * Mathf.Sin(theta);
            }

            return velocity;
        }
        
        //Returns wave height
        public float GetWaveHeight(float x, float z, float time)
        {
            Vector3 pos = GetWavePosition(new Vector3(x, waterLevel, z), time);
            return pos.y;
        }
        
        //For shader
        public Wave[] GetWaves() => waves;
        public float GetWaterLevel() => waterLevel;
        public float GetGravity() => gravity;

        private void OnDrawGizmosSelected()
        {
            if (waves == null || waves.Length == 0) return;

            Gizmos.color = Color.cyan;
            float time = Application.isPlaying ? Time.time : 0f;

            WaterVolume[] waterVolumes = FindObjectsByType<WaterVolume>(FindObjectsSortMode.None);
            
            if (waterVolumes.Length > 0)
            {
                foreach (var waterVol in waterVolumes)
                {
                    DrawWavesOnVolume(waterVol, time);
                }
            }
            else
            {
                DrawWavesAtPosition(transform.position, 40f, time);
            }
        }
    
        private void DrawWavesOnVolume(WaterVolume waterVolume, float time)
        {
            Collider col = waterVolume.GetComponent<Collider>();
            if (col == null) return;
            
            Bounds bounds = col.bounds;
            float size = Mathf.Max(bounds.size.x, bounds.size.z);
            
            DrawWavesAtPosition(bounds.center, size, time);
        }
    
        private void DrawWavesAtPosition(Vector3 center, float size, float time)
        {
            float spacing = size / gridSize;
            float offset = size * 0.5f;
            
            Gizmos.color = new Color(0, 1f, 1f, 0.6f);
            
            for (int x = 0; x < gridSize; x++)
            {
                for (int z = 0; z < gridSize; z++)
                {
                    Vector3 worldPos = center + new Vector3(
                        x * spacing - offset, 
                        0, 
                        z * spacing - offset
                    );
                    
                    Vector3 wavePos = GetWavePosition(worldPos, time);
                    
                    // Rysuj punkt
                    Gizmos.DrawSphere(wavePos, spacing * 0.08f);
                    
                    // Linie siatki
                    if (x < gridSize - 1)
                    {
                        Vector3 nextPos = GetWavePosition(
                            worldPos + Vector3.right * spacing, 
                            time
                        );
                        Gizmos.DrawLine(wavePos, nextPos);
                    }
                    
                    if (z < gridSize - 1)
                    {
                        Vector3 nextPos = GetWavePosition(
                            worldPos + Vector3.forward * spacing, 
                            time
                        );
                        Gizmos.DrawLine(wavePos, nextPos);
                    }
                }
            }
            
            float windAngle = windDirection * Mathf.Deg2Rad;
            Vector3 windDir = new Vector3(Mathf.Cos(windAngle), 0, Mathf.Sin(windAngle));
            Vector3 arrowStart = center;
            Vector3 arrowEnd = center + windDir * size * 0.3f;
            
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(arrowStart, arrowEnd);
            
            Vector3 arrowHead1 = arrowEnd - windDir * size * 0.05f + Vector3.Cross(windDir, Vector3.up) * size * 0.03f;
            Vector3 arrowHead2 = arrowEnd - windDir * size * 0.05f - Vector3.Cross(windDir, Vector3.up) * size * 0.03f;
            Gizmos.DrawLine(arrowEnd, arrowHead1);
            Gizmos.DrawLine(arrowEnd, arrowHead2);
        }
    }
}