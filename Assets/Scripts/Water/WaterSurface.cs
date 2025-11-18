using UnityEngine;
using UnityEngine.Rendering;

namespace Water.Waves
{
    [RequireComponent(typeof(MeshRenderer))]
    public class WaterSurface : MonoBehaviour
    {
        [Header("Water volume")] 
        [SerializeField] private WaterVolume _waterVolume;
        
        [Header("Mesh Generation")]
        [Tooltip("More - smoother waves, but slower")]
        [SerializeField] private int gridResolution = 50;
        [SerializeField] private bool generateOnStart = true;
        
        [Header("Wave LOD (Level of Detail)")]
        [Tooltip("How many of the biggest waves to send to shader")]
        [SerializeField] private int maxShaderWaves = 16;
        
        [Header("Surface shader")]
        [SerializeField] private Material waterMaterial;

        private WaveSystem _waveSystem;
        private Material _waterMaterial;
        private Mesh _mesh;
        
        //shader cache
        private int _waveDirectionsID;
        private int _waveParamsID;
        private int _waveCountID;
        private int _waterLevelID;
        private int _colorID;
        private int _depthColorID;
        private int _smoothnessID;
        private int _fresnelPowerID;
        private int _foamColorID;
        private int _foamThresholdID;
        private int _foamSpreadID;
        private int _foamNoiseScaleID;
        private int _foamSpeedID;
        private int _causticsTexID;
        private int _causticsScaleID;
        private int _causticsSpeedID;
        private int _causticsStrengthID;
        private int _reflectionStrengthID;
        private int _reflectionDistortionID;
        
        private void Awake()
        {
            _waveSystem = FindFirstObjectByType<WaveSystem>();
            if (_waveSystem == null)
            {
                Debug.LogError("WaveSystem not found! Water surface won't animate.");
                enabled = false;
                return;
            }

            if (_waterVolume == null)
            {
                Debug.LogError("WaveVolume not found! Water surface won't animate.");
                enabled = false;
                return;
            }

            // Configure MeshRenderer
            var renderer = GetComponent<MeshRenderer>();
            if (waterMaterial != null)
            {
                _waterMaterial = new Material(waterMaterial);
                renderer.material = _waterMaterial;
            }
            else
            {
                _waterMaterial = renderer.material;
            }

            // Cache shader property IDs
            CacheShaderPropertyIDs();

            if (generateOnStart)
            {
                GenerateWaterMeshOnVolume();
            }
        }

        private void CacheShaderPropertyIDs()
        {
            _waveDirectionsID = Shader.PropertyToID("_WaveDirections");
            _waveParamsID = Shader.PropertyToID("_WaveParams");
            _waveCountID = Shader.PropertyToID("_WaveCount");
            _waterLevelID = Shader.PropertyToID("_WaterLevel");
        }

        private void Start()
        {
            UpdateAllShaderProperties();
        }

        private void Update()
        {
            if (_waterMaterial == null || _waveSystem == null) return;
            
            UpdateWaveShaderProperties();
        }

        private void OnValidate()
        {
            if (Application.isPlaying && _waterMaterial != null)
            {
                UpdateStaticShaderProperties();
            }
        }
        
        public void GenerateWaterMeshOnVolume()
        {
            Collider volumeCollider = _waterVolume.GetComponent<Collider>();
            if (volumeCollider == null)
            {
                Debug.LogError("Cannot generate water mesh: WaterVolume has no Collider!");
                return;
            }

            Bounds volumeBounds = volumeCollider.bounds;
            float gridSize = Mathf.Max(volumeBounds.size.x, volumeBounds.size.z);
            
            Vector3 surfacePosition = new Vector3(
                volumeBounds.center.x,
                volumeBounds.max.y,
                volumeBounds.center.z
            );
            
            transform.position = surfacePosition;
            transform.localScale = Vector3.one;
    
            GenerateWaterMesh(gridSize);
        }
        
        //Generates water surface plane
        public void GenerateWaterMesh(float meshSize)
        {
            _mesh = new Mesh();
            _mesh.indexFormat = IndexFormat.UInt32;
            _mesh.name = "Water Surface";

            //generate verticies
            int vertexCount = (gridResolution + 1) * (gridResolution + 1);
            Vector3[] vertices = new Vector3[vertexCount];
            Vector2[] uvs = new Vector2[vertexCount];
            int[] triangles = new int[gridResolution * gridResolution * 6];

            float cellSize = meshSize / gridResolution;
            float halfSize = meshSize * 0.5f;

            int index = 0;
            for (int z = 0; z <= gridResolution; z++)
            {
                for (int x = 0; x <= gridResolution; x++)
                {
                    float xPos = x * cellSize - halfSize;
                    float zPos = z * cellSize - halfSize;
                    
                    vertices[index] = new Vector3(xPos, 0, zPos);
                    uvs[index] = new Vector2((float)x / gridResolution, (float)z / gridResolution);
                    index++;
                }
            }
            
            //generate triangles
            int triIndex = 0;
            for (int z = 0; z < gridResolution; z++)
            {
                for (int x = 0; x < gridResolution; x++)
                {
                    int vertIndex = z * (gridResolution + 1) + x;

                    triangles[triIndex++] = vertIndex;
                    triangles[triIndex++] = vertIndex + gridResolution + 1;
                    triangles[triIndex++] = vertIndex + 1;

                    triangles[triIndex++] = vertIndex + 1;
                    triangles[triIndex++] = vertIndex + gridResolution + 1;
                    triangles[triIndex++] = vertIndex + gridResolution + 2;
                }
            }

            _mesh.vertices = vertices;
            _mesh.uv = uvs;
            _mesh.triangles = triangles;
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();

            var meshFilter = GetComponent<MeshFilter>();
            if (meshFilter == null) meshFilter = gameObject.AddComponent<MeshFilter>();
            meshFilter.mesh = _mesh;
            
            Debug.Log($"Generated water surface mesh: {gridResolution}x{gridResolution} = {vertexCount} vertices, size: {meshSize:F2}m");
            Debug.Log($"Mesh bounds: center={_mesh.bounds.center}, size={_mesh.bounds.size}");
            Debug.Log($"Mesh Y range: min={_mesh.bounds.min.y}, max={_mesh.bounds.max.y}");
        }

        private void UpdateAllShaderProperties()
        {
            UpdateWaveShaderProperties();
            UpdateStaticShaderProperties();
        }
        
        private void UpdateWaveShaderProperties()
        {
            if (_waveSystem == null) return;
    
            var waves = _waveSystem.GetWaves();
            if (waves == null || waves.Length == 0) return;
    
            // Ogranicz liczbę fal wysyłanych do shadera (performance)
            int waveCount = Mathf.Min(waves.Length, maxShaderWaves);
    
            // Przygotuj dane dla shadera
            Vector4[] directions = new Vector4[waveCount];
            Vector4[] parameters = new Vector4[waveCount];
    
            for (int i = 0; i < waveCount; i++)
            {
                var wave = waves[i];
        
                // _WaveDirections: (dirX, dirY, frequency, phase)
                directions[i] = new Vector4(
                    wave.direction.x,
                    wave.direction.y,
                    wave.frequency,
                    wave.phase
                );
        
                // _WaveParams: (amplitude, steepness, waveLength, speed)
                parameters[i] = new Vector4(
                    wave.amplitude,
                    wave.steepness,
                    wave.waveLength,
                    wave.speed
                );
            }
    
            // Wyślij do shadera
            _waterMaterial.SetVectorArray(_waveDirectionsID, directions);
            _waterMaterial.SetVectorArray(_waveParamsID, parameters);
            _waterMaterial.SetInt(_waveCountID, waveCount);
            _waterMaterial.SetFloat("_Time", Time.time);
        }
        
        private void UpdateStaticShaderProperties()
        {
            if (_waterMaterial == null) return;
            _waterMaterial.SetFloat(_waterLevelID, transform.position.y);
        }

        private void OnDestroy()
        {
            if (_mesh != null)
            {
                Destroy(_mesh);
            }
            
            if (_waterMaterial != null)
            {
                Destroy(_waterMaterial);
            }
        }
        
        [ContextMenu("Generate Water Surface From Volume")]
        private void GenerateFromVolumeInEditor()
        {
            _waterVolume = GetComponentInParent<WaterVolume>();
            if (_waterVolume == null) _waterVolume = FindFirstObjectByType<WaterVolume>();
            
            if (_waterVolume != null)
            {
                GenerateWaterMeshOnVolume();
                Debug.Log("Water surface generated from WaterVolume in editor");
            }
            else
            {
                Debug.LogError("No WaterVolume found to generate surface from!");
            }
        }
    }
}