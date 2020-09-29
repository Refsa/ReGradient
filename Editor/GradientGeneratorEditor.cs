using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Callbacks;

namespace Refsa.ReGradient
{
    public class ReGradientGeneratorEditor : EditorWindow
    {
        const int MaxGradientNodes = 256;
        const string FileExt = "regradient";

        static string lastPath = "";

        [MenuItem("Tools/GradientGenerator")]
        private static void ShowWindow()
        {
            var window = GetWindow<ReGradientGeneratorEditor>();
            window.titleContent = new GUIContent("GradientGenerator");

            window.minSize = new Vector2(520, 300);
            window.maxSize = new Vector2(520, 300);
            window.position = new Rect(window.position.position, window.minSize);

            window.SetupPreviewTexture();

            window.Show();
        }

        public static ReGradientGeneratorEditor Open(ReGradientData data)
        {
            var window = GetWindow<ReGradientGeneratorEditor>();
            window.titleContent = new GUIContent("GradientGenerator");

            window.minSize = new Vector2(520, 300);
            window.maxSize = new Vector2(520, 300);
            window.position = new Rect(window.position.position, window.minSize);

            window.SetupPreviewTexture();

            window.currentGradient = data;

            return window;
        }

        [OnOpenAsset(0)]
        public static bool OnOpen(int instanceID, int line)
        {
            var asset = EditorUtility.InstanceIDToObject(instanceID) as ReGradientDataProxy;

            if (asset != null)
            {
                var window = Open(asset.data);
                window.GeneratePreview();
                window.Show();
                lastPath = asset.path;

                return true;
            }

            return false;
        }

        ReGradientData currentGradient;
        int selectedNode = -1;
        int draggingNode = -1;

        [SerializeField] ComputeShader gradientCompute;
        ComputeBuffer gradientNodeBuffer;

        RenderTexture previewTexture;

        private void OnGUI()
        {
            if (gradientCompute == null) LoadGradientCompute();
            if (!currentGradient.HasValue) CreateNewGradient();
            if (previewTexture is null) SetupPreviewTexture();
            // if (selectedNode > currentGradient.Nodes.Count) selectedNode = -1;

            using (new GUILayout.HorizontalScope(EditorStyles.toolbar, GUILayout.Height(50f)))
            {
                if (GUILayout.Button("New", EditorStyles.toolbarButton))
                {
                    CreateNewGradient();
                    SetupPreviewTexture();
                    GeneratePreview();
                    selectedNode = -1;
                    lastPath = "";
                }
                if (GUILayout.Button("Load", EditorStyles.toolbarButton))
                {
                    Load();
                    lastPath = "";
                }
                if (GUILayout.Button("Save", EditorStyles.toolbarButton))
                {
                    Save();
                }
                if (GUILayout.Button("Export", EditorStyles.toolbarButton))
                {
                    Export();
                }
            }

            if (gradientCompute == null)
            {
                EditorGUILayout.LabelField("GradientCompute is not assigned");
                return;
            }

            using (new GUILayout.VerticalScope())
            {
                EditorGUI.BeginChangeCheck();
                currentGradient.Size = EditorGUILayout.Vector2IntField("Size", currentGradient.Size);
                if (EditorGUI.EndChangeCheck())
                {
                    SetupPreviewTexture();
                    GeneratePreview();
                }

                EditorGUILayout.Space();

                float height = position.size.y * 0.4f;
                float aspect = (float)currentGradient.Size.x / (float)currentGradient.Size.y;
                float width = height * aspect;
                Rect previewRect = GUILayoutUtility.GetRect(width - 32, height);
                EditorGUI.DrawTextureTransparent(previewRect, previewTexture, ScaleMode.StretchToFill);

                Rect sliderRect = GUILayoutUtility.GetRect(width, 32);
                Vector2 nodeSize = Vector2.one * 32;
                
                bool nodeActionUsed = false;
                for (int i = currentGradient.Nodes.Count - 1; i >= 0; i--)
                {
                    var node = currentGradient.Nodes[i];
                    float xpos = node.Percent * sliderRect.width;
                    Vector2 pos = new Vector2(xpos - 16, sliderRect.y);
                    Rect nodeRect = new Rect(pos, nodeSize);

                    if (selectedNode == i)
                    {
                        var selectedRect = new Rect(nodeRect);
                        selectedRect.width *= 0.5f;
                        selectedRect.x += 8;
                        selectedRect.height *= 1.5f;
                        EditorGUI.DrawRect(selectedRect, Color.yellow);
                    }
                    EditorGUI.DrawRect(nodeRect, node.Color);

                    if (nodeRect.Contains(Event.current.mousePosition) && !nodeActionUsed)
                    {
                        if (Event.current.type == EventType.MouseDown)
                        {
                            if (Event.current.button == 0)
                            {
                                selectedNode = i;
                                draggingNode = i;
                                Repaint();
                            }
                            else if (Event.current.button == 1)
                            {
                                selectedNode = -1;
                                currentGradient.Nodes.RemoveAt(i);
                                currentGradient.Nodes = currentGradient.Nodes.OrderBy(e => e.Percent).ToList();
                                GeneratePreview();
                                Repaint();
                            }
                            nodeActionUsed = true;
                        }
                    }

                    if (Event.current.type == EventType.MouseDrag && draggingNode == i)
                    {
                        nodeActionUsed = true;

                        float mouseX = Event.current.mousePosition.x;
                        mouseX -= sliderRect.position.x;
                        float percent = Mathf.Clamp01(mouseX / sliderRect.width);

                        node.Percent = percent;
                        currentGradient.Nodes[i] = node;

                        int nodeID = node.ID;
                        currentGradient.Nodes = currentGradient.Nodes.OrderBy(e => e.Percent).ToList();
                        selectedNode = currentGradient.Nodes.FindIndex(e => e.ID == nodeID);
                        draggingNode = currentGradient.Nodes.FindIndex(e => e.ID == nodeID);
                        GeneratePreview();
                        Repaint();
                    }
                }

                if (Event.current.type == EventType.MouseUp)
                {
                    draggingNode = -1;
                }

                if (!nodeActionUsed && sliderRect.Contains(Event.current.mousePosition) && Event.current.button == 0 && Event.current.type == EventType.MouseDown)
                {
                    float mouseX = Event.current.mousePosition.x;
                    mouseX -= sliderRect.position.x;
                    float percent = Mathf.Clamp01(mouseX / sliderRect.width);

                    currentGradient.AddNode(Color.white, percent);
                    currentGradient.Nodes = currentGradient.Nodes.OrderBy(e => e.Percent).ToList();
                    GeneratePreview();
                    Repaint();
                }

                EditorGUILayout.Space(25f);
                if (selectedNode != -1 && selectedNode < currentGradient.Nodes.Count && !nodeActionUsed)
                {
                    var currentNode = currentGradient.Nodes[selectedNode];
                    EditorGUI.BeginChangeCheck();
                    using (new GUILayout.HorizontalScope())
                    {
                        currentNode.Color = EditorGUILayout.ColorField(currentNode.Color);
                        currentNode.Percent = EditorGUILayout.Slider(currentNode.Percent, 0, 1);
                    }
                    if (EditorGUI.EndChangeCheck())
                    {
                        currentGradient.Nodes[selectedNode] = currentNode;
                        currentGradient.Nodes = currentGradient.Nodes.OrderBy(e => e.Percent).ToList();
                        GeneratePreview();
                    }
                }
            }
        }

        void OnDestroy()
        {
            gradientNodeBuffer?.Dispose();
            previewTexture?.Release();
        }

        void GeneratePreview()
        {
            if (gradientNodeBuffer is null) SetupCompute();

            if (currentGradient.Size.x <= 0 || currentGradient.Size.y <= 0) return;

            gradientNodeBuffer.SetData(currentGradient.Nodes);

            gradientCompute.SetVector("_Size", new Vector2(currentGradient.Size.x, currentGradient.Size.y));
            gradientCompute.SetInt("_NodeCount", currentGradient.Nodes.Count);
            gradientCompute.SetBuffer(0, "_GradientNodes", gradientNodeBuffer);

            gradientCompute.SetTexture(0, "_Result", previewTexture);

            gradientCompute.Dispatch(0, currentGradient.Size.x / 32, 1, 1);
        }

        void SetupCompute()
        {
            gradientNodeBuffer?.Dispose();

            gradientNodeBuffer = new ComputeBuffer(MaxGradientNodes, sizeof(float) * 6);
        }

        void SetupPreviewTexture()
        {
            previewTexture?.Release();

            previewTexture = new RenderTexture(currentGradient.Size.x, currentGradient.Size.y, 24, RenderTextureFormat.ARGB32, 0);
            previewTexture.enableRandomWrite = true;
            previewTexture.wrapMode = TextureWrapMode.Clamp;
            previewTexture.filterMode = FilterMode.Bilinear;
            previewTexture.Create();

            GeneratePreview();
        }

        void CreateNewGradient()
        {
            if (currentGradient.HasValue)
            {
                if (!PromptSave()) return;
            }

            currentGradient = new ReGradientData();
            currentGradient.Size = new Vector2Int(256, 64);
            currentGradient.Nodes = new List<ReGradientNode>();

            currentGradient.AddNode(Color.white, 0f);
            currentGradient.AddNode(Color.black, 1f);
        }

        bool PromptSave()
        {
            return true;
        }

        void Save()
        {
            if (!currentGradient.HasValue) return;

            string savePath = "";

            if (!string.IsNullOrEmpty(lastPath))
            {
                bool useLastPath = EditorUtility.DisplayDialog("Save with last path?", $"Save to {lastPath}?", "Yes", "No");

                if (useLastPath) savePath = lastPath;
            }
            
            if (string.IsNullOrEmpty(savePath))
                savePath = EditorUtility.SaveFilePanelInProject("Save Gradient", "regradient", FileExt, "Save gradient data");

            if (string.IsNullOrEmpty(savePath)) return;

            string asJson = JsonUtility.ToJson(currentGradient, true);

            if (string.IsNullOrEmpty(asJson)) return;

            System.IO.File.WriteAllText(savePath, asJson);

            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();

            lastPath = savePath;
        }

        void Load()
        {
            if (!PromptSave()) return;

            var gradientFiles = System.IO.Directory.GetFiles(Application.dataPath, "*." + FileExt, System.IO.SearchOption.AllDirectories);
            if (gradientFiles.Length == 0) return;

            var gm = new GenericMenu();

            foreach (var path in gradientFiles)
            {
                if (string.IsNullOrEmpty(path)) continue;

                gm.AddItem(new GUIContent(System.IO.Path.GetFileNameWithoutExtension(path)), false, () =>
                {
                    string content = System.IO.File.ReadAllText(path);
                    if (string.IsNullOrEmpty(content)) return;

                    currentGradient = JsonUtility.FromJson<ReGradientData>(content);
                    selectedNode = -1;
                    GeneratePreview();
                });
            }

            gm.ShowAsContext();
        }

        void Export()
        {
            if (!currentGradient.HasValue) return;

            var savePath = EditorUtility.SaveFilePanelInProject("Save image of gradient", "regradient_image", "png", "Save ReGradient as image");
            if (string.IsNullOrEmpty(savePath)) return;

            Texture2D tex = new Texture2D(currentGradient.Size.x, currentGradient.Size.y, TextureFormat.ARGB32, 1, true);
            RenderTexture.active = previewTexture;
            tex.ReadPixels(new Rect(0, 0, previewTexture.width, previewTexture.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            var bytes = tex.EncodeToPNG();

            string fullSavePath = Application.dataPath.Replace("Assets", savePath);

            System.IO.File.WriteAllBytes(fullSavePath, bytes);
            AssetDatabase.Refresh();
        }

        void LoadGradientCompute()
        {
            var guids = AssetDatabase.FindAssets("GradientCompute");
            if (guids.Length == 0) throw new System.Exception("ReGradient: Could not locate GradientCompute");

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            if (string.IsNullOrEmpty(path)) throw new System.Exception("ReGradient: Path could not be found from GUID");

            gradientCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>(path);
            if (gradientCompute is null) throw new System.Exception("ReGradient: Loaded gradient compute is null");

            SetupCompute();
        }
    }
}