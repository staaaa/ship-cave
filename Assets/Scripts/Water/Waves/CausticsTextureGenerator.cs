using UnityEngine;

namespace Water.Waves
{
    public class CausticsTextureGenerator : MonoBehaviour
    {
        [Header("Texture Settings")]
        [SerializeField] private int textureSize = 512;
        [SerializeField] private float noiseScale = 3f;
        [SerializeField] private float contrast = 2f;
        [SerializeField, Range(0, 1)] private float brightness = 0.5f;
        
        [Header("Output")]
        [SerializeField] private string textureName = "CausticsTexture";
        
        [ContextMenu("Generate Caustics Texture")]
        public void GenerateCausticsTexture()
        {
            Texture2D causticsTexture = CreateCausticsTexture();
            
            #if UNITY_EDITOR
            string path = $"Assets/{textureName}.png";
            byte[] bytes = causticsTexture.EncodeToPNG();
            System.IO.File.WriteAllBytes(path, bytes);
            UnityEditor.AssetDatabase.Refresh();
            
            UnityEditor.TextureImporter importer = UnityEditor.AssetImporter.GetAtPath(path) as UnityEditor.TextureImporter;
            if (importer != null)
            {
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = UnityEditor.TextureImporterCompression.Compressed;
                UnityEditor.AssetDatabase.ImportAsset(path);
            }
            
            Debug.Log($"Caustics texture generated and saved to: {path}");
            #else
            Debug.LogWarning("Texture generation only works in Editor!");
            #endif
        }
        
        private Texture2D CreateCausticsTexture()
        {
            Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGB24, true);
            
            Color[] pixels = new Color[textureSize * textureSize];
            
            for (int y = 0; y < textureSize; y++)
            {
                for (int x = 0; x < textureSize; x++)
                {
                    float u = (float)x / textureSize;
                    float v = (float)y / textureSize;
                    
                    float n1 = Mathf.PerlinNoise(u * noiseScale, v * noiseScale);
                    float n2 = Mathf.PerlinNoise(u * noiseScale * 2.3f + 100f, v * noiseScale * 2.3f + 100f);
                    float n3 = Mathf.PerlinNoise(u * noiseScale * 5.7f + 200f, v * noiseScale * 5.7f + 200f);
                    float caustic = n1 * 0.6f + n2 * 0.3f + n3 * 0.1f;
                    caustic = Mathf.Pow(caustic, contrast);
                    caustic = Mathf.Lerp(0, 1, caustic) * brightness;
                    int index = y * textureSize + x;
                    pixels[index] = new Color(caustic, caustic, caustic, 1f);
                }
            }
            
            texture.SetPixels(pixels);
            texture.Apply();
            texture.wrapMode = TextureWrapMode.Repeat;
            
            return texture;
        }
        public Texture2D GenerateRuntimeTexture()
        {
            return CreateCausticsTexture();
        }
    }
    
    #if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(CausticsTextureGenerator))]
    public class CausticsTextureGeneratorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            
            CausticsTextureGenerator generator = (CausticsTextureGenerator)target;
            
            UnityEditor.EditorGUILayout.Space();
            
            if (GUILayout.Button("Generate and Save Caustics Texture", GUILayout.Height(40)))
            {
                generator.GenerateCausticsTexture();
            }
            
            UnityEditor.EditorGUILayout.HelpBox(
                "Click the button above to generate a procedural caustics texture. " +
                "The texture will be saved in the Assets folder.",
                UnityEditor.MessageType.Info
            );
        }
    }
    #endif
}