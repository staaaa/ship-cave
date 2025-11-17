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
        
        [Header("Shader Properties - Colors")]
        [SerializeField] private Color waterColor = new Color(0, 0.4f, 0.7f, 0.5f);
        [SerializeField] private Color depthColor = new Color(0, 0.1f, 0.3f, 1f);
        [SerializeField, Range(0, 1)] private float smoothness = 0.9f;
        [SerializeField, Range(0, 10)] private float fresnelPower = 5f;
        
        [Header("Shader Properties - Foam")]
        [SerializeField] private Color foamColor = new Color(1, 1, 1, 0.9f);
        [SerializeField, Range(-2, 2)] private float foamThreshold = 0.5f;
        [SerializeField, Range(0.01f, 2)] private float foamSpread = 0.3f;
        [SerializeField] private float foamNoiseScale = 5f;
        [SerializeField] private float foamSpeed = 0.5f;
        
        [Header("Shader Properties - Caustics")]
        [SerializeField] private Texture2D causticsTexture;
        [SerializeField] private float causticsScale = 0.5f;
        [SerializeField] private float causticsSpeed = 0.2f;
        [SerializeField, Range(0, 2)] private float causticsStrength = 0.5f;
        
        [Header("Shader Properties - Reflections")]
        [SerializeField, Range(0, 1)] private float reflectionStrength = 0.8f;
        [SerializeField, Range(0, 0.5f)] private float reflectionDistortion = 0.05f;

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
            _waveSystem = WaveSystem.Instance;
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
            // Wave properties
            _waveDirectionsID = Shader.PropertyToID("_WaveDirections");
            _waveParamsID = Shader.PropertyToID("_WaveParams");
            _waveCountID = Shader.PropertyToID("_WaveCount");
            _waterLevelID = Shader.PropertyToID("_WaterLevel");
            
            // Color properties
            _colorID = Shader.PropertyToID("_Color");
            _depthColorID = Shader.PropertyToID("_DepthColor");
            _smoothnessID = Shader.PropertyToID("_Smoothness");
            _fresnelPowerID = Shader.PropertyToID("_FresnelPower");
            
            // Foam properties
            _foamColorID = Shader.PropertyToID("_FoamColor");
            _foamThresholdID = Shader.PropertyToID("_FoamThreshold");
            _foamSpreadID = Shader.PropertyToID("_FoamSpread");
            _foamNoiseScaleID = Shader.PropertyToID("_FoamNoiseScale");
            _foamSpeedID = Shader.PropertyToID("_FoamSpeed");
            
            // Caustics properties
            _causticsTexID = Shader.PropertyToID("_CausticsTex");
            _causticsScaleID = Shader.PropertyToID("_CausticsScale");
            _causticsSpeedID = Shader.PropertyToID("_CausticsSpeed");
            _causticsStrengthID = Shader.PropertyToID("_CausticsStrength");
            
            // Reflection properties
            _reflectionStrengthID = Shader.PropertyToID("_ReflectionStrength");
            _reflectionDistortionID = Shader.PropertyToID("_ReflectionDistortion");
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
            
            Vector3[] vertices = _mesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i].y = 0;
            }
            _mesh.vertices = vertices;
            _mesh.RecalculateBounds();
        }
        
        //Generates water surface plane
        public void GenerateWaterMesh(float meshSize)
        {
            int safeResolution = Mathf.Clamp(gridResolution, 10, 500);
            if (safeResolution != gridResolution)
            {
                Debug.LogWarning($"Grid resolution clamped from {gridResolution} to {safeResolution} for safety");
                gridResolution = safeResolution;
            }
            
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
            var allWaves = _waveSystem.GetWaves();
            
            // Sort waves by amplitude
            var sortedWaves = new System.Collections.Generic.List<WaveSystem.Wave>(allWaves);
            sortedWaves.Sort((a, b) => b.amplitude.CompareTo(a.amplitude));
            
            // Only send N biggest ones
            int waveCount = Mathf.Min(sortedWaves.Count, maxShaderWaves);
            waveCount = Mathf.Min(waveCount, 32);

            // Prepare shader data
            Vector4[] waveDirections = new Vector4[32];
            Vector4[] waveParams = new Vector4[32];

            for (int i = 0; i < waveCount; i++)
            {
                var w = sortedWaves[i];
                
                waveDirections[i] = new Vector4(
                    w.direction.x,
                    w.direction.y,
                    w.frequency,
                    w.amplitude
                );

                waveParams[i] = new Vector4(
                    w.phase,
                    w.steepness,
                    0, 0
                );
            }

            // Send to shader
            _waterMaterial.SetVectorArray(_waveDirectionsID, waveDirections);
            _waterMaterial.SetVectorArray(_waveParamsID, waveParams);
            _waterMaterial.SetInt(_waveCountID, waveCount);
            _waterMaterial.SetFloat(_waterLevelID, _waveSystem.GetWaterLevel());
        }
        
        private void UpdateStaticShaderProperties()
        {
            if (_waterMaterial == null) return;

            // Colors
            _waterMaterial.SetColor(_colorID, waterColor);
            _waterMaterial.SetColor(_depthColorID, depthColor);
            _waterMaterial.SetFloat(_smoothnessID, smoothness);
            _waterMaterial.SetFloat(_fresnelPowerID, fresnelPower);
            
            // Foam
            _waterMaterial.SetColor(_foamColorID, foamColor);
            _waterMaterial.SetFloat(_foamThresholdID, foamThreshold);
            _waterMaterial.SetFloat(_foamSpreadID, foamSpread);
            _waterMaterial.SetFloat(_foamNoiseScaleID, foamNoiseScale);
            _waterMaterial.SetFloat(_foamSpeedID, foamSpeed);
            
            // Caustics
            if (causticsTexture != null)
            {
                _waterMaterial.SetTexture(_causticsTexID, causticsTexture);
            }
            _waterMaterial.SetFloat(_causticsScaleID, causticsScale);
            _waterMaterial.SetFloat(_causticsSpeedID, causticsSpeed);
            _waterMaterial.SetFloat(_causticsStrengthID, causticsStrength);
            
            // Reflections
            _waterMaterial.SetFloat(_reflectionStrengthID, reflectionStrength);
            _waterMaterial.SetFloat(_reflectionDistortionID, reflectionDistortion);
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
        
        private void OnDrawGizmosSelected()
        {
            if (_waterVolume != null)
            {
                Collider volumeCollider = _waterVolume.GetComponent<Collider>();
                if (volumeCollider != null)
                {
                    Bounds bounds = volumeCollider.bounds;
                    float gridSize = Mathf.Max(bounds.size.x, bounds.size.z);
                    
                    Gizmos.color = new Color(0, 0.5f, 1f, 0.3f);
                    Gizmos.DrawWireCube(
                        new Vector3(bounds.center.x, bounds.max.y, bounds.center.z),
                        new Vector3(gridSize, 0.1f, gridSize)
                    );
                }
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