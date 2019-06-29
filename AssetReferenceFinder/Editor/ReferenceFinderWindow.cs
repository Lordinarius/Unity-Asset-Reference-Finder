//MIT License

//Copyright(c) 2019 Ömer faruk sayılır

//Permission is hereby granted, free of charge, to any person obtaining a copy
//of this software and associated documentation files (the "Software"), to deal
//in the Software without restriction, including without limitation the rights
//to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//copies of the Software, and to permit persons to whom the Software is
//furnished to do so, subject to the following conditions:

//The above copyright notice and this permission notice shall be included in all
//copies or substantial portions of the Software.

//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//SOFTWARE.

using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class ReferenceCounterWindow : EditorWindow
{
    private static ReferenceCounterWindow win;
    private Object selection;
    private SelectionSearchResponse lastResponse;
    private Vector2 scrollPosition;

    [MenuItem("Window/Reference Counter")]
    public static void Init()
    {
        win = GetWindow<ReferenceCounterWindow>();
        win.titleContent = new GUIContent("Ref Counter");
        win.ShowUtility();
    }

    [MenuItem("GameObject/Look For References", false, -2)]
    [MenuItem("Assets/Look For References", false, -2)]
    public static void LookFromContext()
    {
        Init();
        win.UpdateSelection();
        win.DoSearch();
    }

    private void OnInspectorUpdate()
    {
        UpdateSelection();
        Repaint();
    }

    private void UpdateSelection()
    {
        var selection = Selection.activeObject;
        if (selection != null)
        {
            if (selection is GameObject || selection is ScriptableObject || selection is Object)
            {
                this.selection = selection;
            }
            else
            {
                this.selection = null;
            }
        }
        else
        {
            this.selection = null;
        }
    }

    private void OnGUI()
    {
        if (selection != null)
        {
            if (GUILayout.Button("Search References"))
            {
                DoSearch();
            }
        }

        if (lastResponse != null)
        {
            scrollPosition = GUILayout.BeginScrollView(scrollPosition);
            var selectedObj = lastResponse.request.selectedObject;
            EditorGUILayout.ObjectField("Searched object", selectedObj, selectedObj.GetType(), true);
            foreach (var item in lastResponse.referencePairs)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.BeginHorizontal();
                EditorGUILayout.ObjectField("Owner", item.referenceOwnerObject, item.referenceOwnerObject.GetType(), true);
                EditorGUILayout.ObjectField("Target Object", item.targetObject, item.targetObject.GetType(), true);
                GUILayout.EndHorizontal();
                var style = new GUIStyle(EditorStyles.toolbarButton);
                style.stretchWidth = true;
                style.richText = true;
                style.fixedHeight = 40;
                style.fontSize = 13;
                string buttonLabel = "<b>Variable name: </b><color=cyan>";
                buttonLabel += item.referenceFiledName;
                if (item.referenceArrayIndex.HasValue)
                {
                    buttonLabel += "(At array index: " + item.referenceArrayIndex.Value + ")";
                }
                buttonLabel += "\n</color> at Path: <color=olive>" + item.referenceOwnerScriptPath + " </color>" +
                item.referenceFiledLineNumber;
                GUILayout.Space(25);
                var lab = new GUIStyle(GUI.skin.label);
                lab.alignment = TextAnchor.MiddleCenter;
                lab.fontSize = 18;
                lab.fontStyle = FontStyle.Bold;
                if (string.IsNullOrEmpty(item.referenceOwnerScriptPath))
                {
                    GUILayout.Label("Internal type", lab);
                    GUI.enabled = false;
                }
                else
                {
                    GUILayout.Label("Click button to peek line", lab);
                }
                if (GUILayout.Button(buttonLabel, style))
                {
                    AssetDatabase.OpenAsset(item.scriptObject, item.referenceFiledLineNumber);
                }
                if (string.IsNullOrEmpty(item.referenceOwnerScriptPath))
                {
                    GUI.enabled = true;
                }
                GUILayout.EndVertical();

            }
            GUILayout.EndScrollView();
        }
    }

    private void DoSearch()
    {
        var allComps = Resources.FindObjectsOfTypeAll<Component>();
        if (selection is GameObject)
        {
            var selectionComponents = (selection as GameObject).GetComponents<Component>().Cast<Object>().ToList();
            selectionComponents.Add(selection);
            var req = new SelectionSearchRequest
            {
                selectedObject = selection,
                targetObjects = selectionComponents
            };

            lastResponse = RequestSearch(allComps, req);
        }
        else if (selection is Object)
        {
            var req = new SelectionSearchRequest
            {
                selectedObject = selection,
                targetObjects = new List<Object>() { selection }
            };

            lastResponse = RequestSearch(allComps, req);
        }
    }

    private static bool IsAssignableFrom(System.Type to, System.Type from)
    {
        if (to.IsAssignableFrom(from))
        {
            return true;
        }
        else if (to.IsArray && to.GetElementType().IsAssignableFrom(from))
        {
            return true;
        }
        else if (from == typeof(Texture2D) && to == typeof(Sprite) || to.HasElementType && to.GetElementType() == typeof(Sprite))
        {
            return true;
        }

        return false;
    }

    private static bool AreEqual(Object a, Object b)
    {
        if (a == null || b == null) return false;
        if (a == b)
        {
            return true;
        }
        else if (a.GetType() == typeof(Texture2D) && b.GetType() == typeof(Sprite))
        {
            var bTex = (b as Sprite).texture;
            return a == bTex;
        }
        else if (a.GetType() == typeof(Sprite) && b.GetType() == typeof(Texture2D))
        {
            var aTex = (a as Sprite).texture;
            return b == aTex;
        }

        return false;
    }

    private static SelectionSearchResponse RequestSearch(Component[] allComps, SelectionSearchRequest req)
    {
        var resp = new SelectionSearchResponse
        {
            request = req,
            referencePairs = new List<ReferencePair>()
        };

        var goType = typeof(GameObject);
        var spriteType = typeof(Sprite);
        var textureType = typeof(Texture2D);
        var targetObjects = req.targetObjects;
        var selectedObject = req.selectedObject;
        //All components in scene
        foreach (var sComp in allComps)
        {
            var type = sComp.GetType();
            var fields = type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            //All fields in that component
            foreach (var field in fields)
            {
                {
                    var fieldType = field.FieldType;
                    var isFieldArray = fieldType.IsArray;
                    

                    foreach (var targetObject in targetObjects)
                    {
                        System.Type targetType = targetObject.GetType();
                        if (isFieldArray && IsAssignableFrom(fieldType, targetType))
                        {
                            var array = field.GetValue(sComp) as IList;
                            if (array != null)
                            {
                                var index = 0;
                                foreach (Object fieldValue in array)
                                {
                                    if (AreEqual(fieldValue, targetObject))
                                    {
                                        var pair = new ReferencePair()
                                        {
                                            referenceOwnerObject = sComp,
                                            targetObject = fieldValue,
                                            referenceFiledName = field.Name,
                                            referenceArrayIndex = index
                                        };
                                        resp.referencePairs.Add(pair);
                                    }
                                    index++;
                                }
                            }
                        }

                        if (IsAssignableFrom(fieldType, targetType))
                        {
                            var fieldValue = field.GetValue(sComp) as Object;
                            if (AreEqual(fieldValue,targetObject))
                            {
                                var pair = new ReferencePair()
                                {
                                    referenceOwnerObject = sComp,
                                    targetObject = fieldValue,
                                    referenceFiledName = field.Name,
                                };
                                resp.referencePairs.Add(pair);
                            }
                        }
                    }
                }
            }
        }

        string[] searchInFolders = new string[] { "Assets" };
        string dataPath = Application.dataPath.Replace("Assets", "");

        foreach (var item in resp.referencePairs)
        {
            if (item.referenceOwnerObject is Component)
            {
                var guids = AssetDatabase.FindAssets("t:Script " + item.referenceOwnerObject.GetType().Name, searchInFolders);
                if (guids.Length > 0)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                    item.referenceOwnerScriptPath = assetPath;
                    string path = dataPath + assetPath;
                    item.scriptObject = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
                    if (item.scriptObject != null)
                    {
                        using (StreamReader sr = new StreamReader(path))
                        {
                            int linecount = 1;
                            while (!sr.EndOfStream)
                            {
                                var line = sr.ReadLine();
                                if (line.Contains(item.referenceFiledName))
                                {
                                    item.referenceFiledLineNumber = linecount;
                                    break;
                                }
                                linecount++;
                            }
                        }
                    }
                }
            }
        }

        return resp;
    }
    
    
}
