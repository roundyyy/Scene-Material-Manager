using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class SceneMaterialManager : EditorWindow
{
    private Vector2 scrollPosition;
    private bool includeInactive = false;
    private string searchFilter = "";
    private string gameObjectSearchFilter = "";
    private Dictionary<Shader, List<Material>> materialsByShader = new Dictionary<Shader, List<Material>>();
    private Dictionary<string, bool> shaderFoldouts = new Dictionary<string, bool>();
    private Dictionary<string, bool> textureFoldouts = new Dictionary<string, bool>();
    private Dictionary<string, bool> propertyFoldouts = new Dictionary<string, bool>();
    private Dictionary<Material, Editor> materialEditors = new Dictionary<Material, Editor>();
    private Dictionary<Material, List<GameObject>> materialUsage = new Dictionary<Material, List<GameObject>>();
    private GUIStyle shaderHeaderStyle;
    private GUIStyle materialCardStyle;
    private bool stylesInitialized;
    private bool showTextureWarnings = true;
    private SearchMode currentSearchMode = SearchMode.MaterialAndShader;

    private enum SearchMode
    {
        MaterialAndShader,
        GameObject
    }

    [MenuItem("Tools/Roundy/Scene Material Manager")]
    public static void ShowWindow()
    {
        GetWindow<SceneMaterialManager>("Scene Material Manager");
    }

    private void InitializeStyles()
    {
        if (!stylesInitialized)
        {
            shaderHeaderStyle = new GUIStyle(EditorStyles.foldout)
            {
                fontStyle = FontStyle.Bold
            };

            materialCardStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(10, 10, 10, 10),
                margin = new RectOffset(5, 5, 5, 5)
            };

            stylesInitialized = true;
        }
    }

    private bool GetFoldoutState(Dictionary<string, bool> dict, string key)
    {
        if (!dict.ContainsKey(key))
            dict[key] = false;
        return dict[key];
    }

    private void SetFoldoutState(Dictionary<string, bool> dict, string key, bool value)
    {
        dict[key] = value;
    }

    private void OnGUI()
    {
        InitializeStyles();

        EditorGUILayout.BeginVertical();

        DrawToolbar();
        DrawSearchBar();
        DrawStatistics();
        DrawMaterialList();

        EditorGUILayout.EndVertical();
    }

    private void DrawToolbar()
    {
        GUILayout.BeginHorizontal(EditorStyles.toolbar);
        {
            GUILayout.Space(5);
            if (GUILayout.Button("Refresh Materials", EditorStyles.toolbarButton, GUILayout.Width(120)))
            {
                FindMaterialsInScene();
            }

            GUILayout.Space(10);
            showTextureWarnings = GUILayout.Toggle(showTextureWarnings, "Show Texture Warnings", EditorStyles.toolbarButton);

            GUILayout.FlexibleSpace();

            includeInactive = GUILayout.Toggle(includeInactive, "Include Inactive", EditorStyles.toolbarButton, GUILayout.Width(100));
            GUILayout.Space(5);
        }
        GUILayout.EndHorizontal();
    }

    private void DrawSearchBar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        {
            GUILayout.Space(5);
            GUILayout.Label("Search Mode:", EditorStyles.miniLabel, GUILayout.Width(80));
            currentSearchMode = (SearchMode)EditorGUILayout.EnumPopup(currentSearchMode, GUILayout.Width(150));

            GUILayout.Label(currentSearchMode == SearchMode.MaterialAndShader ? "Search:" : "GameObject Name:",
                EditorStyles.miniLabel, GUILayout.Width(100));

            if (currentSearchMode == SearchMode.MaterialAndShader)
            {
                searchFilter = GUILayout.TextField(searchFilter, EditorStyles.toolbarSearchField, GUILayout.Width(200));
                if (GUILayout.Button("×", EditorStyles.toolbarButton, GUILayout.Width(24)) && !string.IsNullOrEmpty(searchFilter))
                {
                    searchFilter = "";
                    GUI.FocusControl(null);
                }
            }
            else
            {
                gameObjectSearchFilter = GUILayout.TextField(gameObjectSearchFilter, EditorStyles.toolbarSearchField, GUILayout.Width(200));
                if (GUILayout.Button("×", EditorStyles.toolbarButton, GUILayout.Width(24)) && !string.IsNullOrEmpty(gameObjectSearchFilter))
                {
                    gameObjectSearchFilter = "";
                    GUI.FocusControl(null);
                }
            }

            GUILayout.FlexibleSpace();
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawStatistics()
    {
        if (materialsByShader.Count > 0)
        {
            int totalMaterials = materialsByShader.Values.Sum(list => list.Count);
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"Found {totalMaterials} materials in {materialsByShader.Count} shader groups",
                EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(4);
        }
    }

    private void DrawMaterialList()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        {
            if (materialsByShader.Count == 0)
            {
                EditorGUILayout.HelpBox("Click 'Refresh Materials' to scan the scene.", MessageType.Info);
            }
            else
            {
                foreach (var shaderGroup in materialsByShader.OrderBy(x => x.Key.name))
                {
                    bool shouldDrawGroup = false;

                    if (currentSearchMode == SearchMode.MaterialAndShader)
                    {
                        shouldDrawGroup = string.IsNullOrEmpty(searchFilter) ||
                            shaderGroup.Key.name.ToLower().Contains(searchFilter.ToLower()) ||
                            shaderGroup.Value.Any(m => m.name.ToLower().Contains(searchFilter.ToLower()));
                    }
                    else // GameObject search mode
                    {
                        shouldDrawGroup = string.IsNullOrEmpty(gameObjectSearchFilter) ||
                            shaderGroup.Value.Any(m =>
                                materialUsage.ContainsKey(m) &&
                                materialUsage[m].Any(go =>
                                    go != null && go.name.ToLower().Contains(gameObjectSearchFilter.ToLower())
                                )
                            );
                    }

                    if (shouldDrawGroup)
                    {
                        DrawShaderGroup(shaderGroup.Key, shaderGroup.Value);
                    }
                }
            }
        }
        EditorGUILayout.EndScrollView();
    }

    private void DrawShaderGroup(Shader shader, List<Material> materials)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                string shaderKey = shader.name;
                bool foldoutState = GetFoldoutState(shaderFoldouts, shaderKey);
                bool newState = EditorGUILayout.Foldout(foldoutState, $"{shader.name} ({materials.Count})", true, shaderHeaderStyle);
                SetFoldoutState(shaderFoldouts, shaderKey, newState);

                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Select Shader", EditorStyles.miniButton, GUILayout.Width(100)))
                {
                    Selection.activeObject = shader;
                    EditorGUIUtility.PingObject(shader);
                }
                GUILayout.Space(5);
            }

            if (GetFoldoutState(shaderFoldouts, shader.name))
            {
                foreach (Material material in materials.Where(m => m != null))
                {
                    DrawMaterialEntry(material);
                }
            }
        }
        GUILayout.Space(2);
    }

    private void DrawMaterialEntry(Material material)
    {
        string materialKey = material.name + material.GetInstanceID();

        using (new EditorGUILayout.VerticalScope(materialCardStyle))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var previewTexture = AssetPreview.GetAssetPreview(material);
                if (previewTexture != null)
                {
                    GUILayout.Label(previewTexture, GUILayout.Width(50), GUILayout.Height(50));
                }

                using (new EditorGUILayout.VerticalScope())
                {
                    DrawMaterialHeader(material, materialKey);
                    DrawMaterialPath(material);
                    DrawMaterialUsageInfo(material);

                    if (GetFoldoutState(propertyFoldouts, materialKey))
                    {
                        DrawTextureSection(material);
                        DrawMaterialProperties(material);
                    }
                }
            }
        }
    }

    private void DrawMaterialHeader(Material material, string materialKey)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(material.name, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Duplicate", EditorStyles.miniButton, GUILayout.Width(70)))
            {
                DuplicateMaterial(material);
            }

            GUILayout.Space(5);
            bool propertyState = GetFoldoutState(propertyFoldouts, materialKey);
            var buttonContent = new GUIContent(
                propertyState ? "▼ Properties" : "► Properties",
                "Show/Hide material properties"
            );
            if (GUILayout.Button(buttonContent, EditorStyles.miniButton, GUILayout.Width(120)))
            {
                SetFoldoutState(propertyFoldouts, materialKey, !propertyState);
            }

            GUILayout.Space(5);
            if (GUILayout.Button("Select", EditorStyles.miniButton, GUILayout.Width(60)))
            {
                Selection.activeObject = material;
                EditorGUIUtility.PingObject(material);
            }
            GUILayout.Space(5);
        }
    }

    private void DrawMaterialPath(Material material)
    {
        EditorGUILayout.LabelField(AssetDatabase.GetAssetPath(material), EditorStyles.miniLabel);
    }

    private void DrawMaterialUsageInfo(Material material)
    {
        if (materialUsage.TryGetValue(material, out var users) && users.Count > 0)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Used by {users.Count} object(s)", EditorStyles.miniLabel);
            if (GUILayout.Button("Select Objects", EditorStyles.miniButton, GUILayout.Width(100)))
            {
                Selection.objects = users.ToArray();
            }
            EditorGUILayout.EndHorizontal();

            // Show GameObjects using this material if they match the search
            if (currentSearchMode == SearchMode.GameObject && !string.IsNullOrEmpty(gameObjectSearchFilter))
            {
                var matchingObjects = users
                    .Where(go => go != null && go.name.ToLower().Contains(gameObjectSearchFilter.ToLower()))
                    .ToList();

                if (matchingObjects.Any())
                {
                    EditorGUI.indentLevel++;
                    foreach (var go in matchingObjects)
                    {
                        EditorGUILayout.LabelField(go.name, EditorStyles.miniLabel);
                    }
                    EditorGUI.indentLevel--;
                }
            }
        }
    }

    private void DrawTextureSection(Material material)
    {
        var textureProps = GetMaterialTextures(material);
        var usedTextureCount = textureProps.Count;

        bool textureState = GetFoldoutState(textureFoldouts, material.name + material.GetInstanceID());
        bool newTextureState = EditorGUILayout.Foldout(textureState, $"Textures ({usedTextureCount})", true);
        SetFoldoutState(textureFoldouts, material.name + material.GetInstanceID(), newTextureState);

        if (newTextureState)
        {
            DrawTextureList(material, textureProps);
        }
    }

    private void DrawTextureList(Material material, List<string> textureProps)
    {
        EditorGUI.indentLevel++;
        foreach (var textureProp in textureProps)
        {
            var texture = material.GetTexture(textureProp) as Texture2D;
            if (texture != null)
            {
                EditorGUILayout.BeginHorizontal();
                {
                    GUILayout.Label(AssetPreview.GetMiniThumbnail(texture), GUILayout.Width(20), GUILayout.Height(20));

                    if (showTextureWarnings && (texture.width > 2048 || texture.height > 2048))
                    {
                        EditorGUILayout.LabelField($"{textureProp} ({texture.width}x{texture.height})", EditorStyles.boldLabel);
                        GUILayout.Label(EditorGUIUtility.IconContent("console.warnicon.sml"), GUILayout.Width(20));
                        EditorGUILayout.LabelField("Large texture", EditorStyles.miniLabel);
                    }
                    else
                    {
                        EditorGUILayout.LabelField($"{textureProp} ({texture.width}x{texture.height})");
                    }

                    if (GUILayout.Button("Select", EditorStyles.miniButton, GUILayout.Width(50)))
                    {
                        Selection.activeObject = texture;
                        EditorGUIUtility.PingObject(texture);
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
        }
        EditorGUI.indentLevel--;
    }

    private void DuplicateMaterial(Material sourceMaterial)
    {
        string sourcePath = AssetDatabase.GetAssetPath(sourceMaterial);
        string directory = System.IO.Path.GetDirectoryName(sourcePath);
        string sourceFileName = System.IO.Path.GetFileNameWithoutExtension(sourcePath);

        // Find a unique name for the new material
        int counter = 1;
        string newPath;
        do
        {
            string newFileName = $"{sourceFileName}_{counter}";
            newPath = System.IO.Path.Combine(directory, newFileName + ".mat");
            counter++;
        } while (System.IO.File.Exists(newPath));

        // Create the new material
        AssetDatabase.CopyAsset(sourcePath, newPath);
        AssetDatabase.Refresh();

        // Select and ping the new material
        Material newMaterial = AssetDatabase.LoadAssetAtPath<Material>(newPath);
        Selection.activeObject = newMaterial;
        EditorGUIUtility.PingObject(newMaterial);

        // Refresh the material list
        FindMaterialsInScene();
    }

    private void DrawMaterialProperties(Material material)
    {
        Shader shader = material.shader;
        int propertyCount = ShaderUtil.GetPropertyCount(shader);

        EditorGUI.indentLevel++;

        // Add shader selection field
        EditorGUI.BeginChangeCheck();
        Shader newShader = EditorGUILayout.ObjectField("Shader", shader, typeof(Shader), false) as Shader;
        if (EditorGUI.EndChangeCheck() && newShader != null)
        {
            Undo.RecordObject(material, "Change Material Shader");
            material.shader = newShader;
            FindMaterialsInScene(); // Refresh to update shader groups
            return;
        }

        // Get or create material editor
        if (!materialEditors.ContainsKey(material))
        {
            materialEditors[material] = Editor.CreateEditor(material);
        }

        EditorGUI.BeginChangeCheck();

        for (int i = 0; i < propertyCount; i++)
        {
            if (ShaderUtil.GetPropertyType(shader, i) != ShaderUtil.ShaderPropertyType.TexEnv)
            {
                string propertyName = ShaderUtil.GetPropertyName(shader, i);
                var propertyType = ShaderUtil.GetPropertyType(shader, i);

                EditorGUILayout.BeginHorizontal();
                {
                    EditorGUILayout.LabelField(propertyName);

                    switch (propertyType)
                    {
                        case ShaderUtil.ShaderPropertyType.Color:
                            Color newColor = EditorGUILayout.ColorField(material.GetColor(propertyName));
                            if (GUI.changed)
                            {
                                Undo.RecordObject(material, "Change Material Color");
                                material.SetColor(propertyName, newColor);
                            }
                            break;

                        case ShaderUtil.ShaderPropertyType.Vector:
                            Vector4 newVector = EditorGUILayout.Vector4Field("", material.GetVector(propertyName));
                            if (GUI.changed)
                            {
                                Undo.RecordObject(material, "Change Material Vector");
                                material.SetVector(propertyName, newVector);
                            }
                            break;

                        case ShaderUtil.ShaderPropertyType.Float:
                        case ShaderUtil.ShaderPropertyType.Range:
                            float rangeMin = 0, rangeMax = 1;
                            if (propertyType == ShaderUtil.ShaderPropertyType.Range)
                            {
                                rangeMin = ShaderUtil.GetRangeLimits(shader, i, 1);
                                rangeMax = ShaderUtil.GetRangeLimits(shader, i, 2);
                            }

                            float newValue = propertyType == ShaderUtil.ShaderPropertyType.Range
                                ? EditorGUILayout.Slider(material.GetFloat(propertyName), rangeMin, rangeMax)
                                : EditorGUILayout.FloatField(material.GetFloat(propertyName));

                            if (GUI.changed)
                            {
                                Undo.RecordObject(material, "Change Material Float");
                                material.SetFloat(propertyName, newValue);
                            }
                            break;
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        if (EditorGUI.EndChangeCheck())
        {
            EditorUtility.SetDirty(material);
        }

        EditorGUI.indentLevel--;
    }

    private List<string> GetMaterialTextures(Material material)
    {
        List<string> textureProperties = new List<string>();
        HashSet<Texture> uniqueTextures = new HashSet<Texture>();
        Shader shader = material.shader;
        int propertyCount = ShaderUtil.GetPropertyCount(shader);

        for (int i = 0; i < propertyCount; i++)
        {
            if (ShaderUtil.GetPropertyType(shader, i) == ShaderUtil.ShaderPropertyType.TexEnv)
            {
                string propertyName = ShaderUtil.GetPropertyName(shader, i);
                Texture texture = material.GetTexture(propertyName);

                if (texture != null && uniqueTextures.Add(texture))
                {
                    textureProperties.Add(propertyName);
                }
            }
        }

        return textureProperties;
    }

    private void FindMaterialsInScene()
    {
        materialsByShader.Clear();
        propertyFoldouts.Clear();
        textureFoldouts.Clear();
        shaderFoldouts.Clear();
        materialUsage.Clear();

        // Clean up existing material editors
        foreach (var editor in materialEditors.Values)
        {
            if (editor != null)
            {
                DestroyImmediate(editor);
            }
        }
        materialEditors.Clear();

        HashSet<Material> uniqueMaterials = new HashSet<Material>();

        Renderer[] renderers = includeInactive
            ? Resources.FindObjectsOfTypeAll<Renderer>()
            : FindObjectsOfType<Renderer>();

        foreach (Renderer renderer in renderers)
        {
            if (PrefabUtility.IsPartOfPrefabAsset(renderer.gameObject))
                continue;

            if (!includeInactive && !renderer.gameObject.activeInHierarchy)
                continue;

            foreach (Material material in renderer.sharedMaterials)
            {
                if (material != null)
                {
                    uniqueMaterials.Add(material);

                    // Update material usage tracking
                    if (!materialUsage.ContainsKey(material))
                    {
                        materialUsage[material] = new List<GameObject>();
                    }
                    materialUsage[material].Add(renderer.gameObject);
                }
            }
        }

        // Organize materials by shader
        foreach (var material in uniqueMaterials)
        {
            Shader shader = material.shader;
            if (!materialsByShader.ContainsKey(shader))
            {
                materialsByShader[shader] = new List<Material>();
            }
            materialsByShader[shader].Add(material);
        }

        // Sort materials within each shader group
        foreach (var shaderGroup in materialsByShader)
        {
            shaderGroup.Value.Sort((a, b) => string.Compare(a.name, b.name));
        }
    }

    private void OnDisable()
    {
        // Cleanup material editors
        foreach (var editor in materialEditors.Values)
        {
            if (editor != null)
            {
                DestroyImmediate(editor);
            }
        }
        materialEditors.Clear();
    }

    private void OnDestroy()
    {
        OnDisable();
    }
}