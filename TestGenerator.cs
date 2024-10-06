using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Com.Kooply.Unity.Config;
using Com.Kooply.Unity.ExtensionMethods;
using UnityEditor;
using UnityEngine;

namespace Editor.Private
{
    public class TestGenerator : EditorWindow
    {
        private enum DataType { Bool, String, Int, Float }
    
    
    
        [MenuItem("Window/AB Test Generator")]
        private static void OpenWindow()
        {
            GetWindow<TestGenerator>("AB Test Generator").Show();
        }
        
        
        
        private string _testName;
        private DataType _dataType;
        private string _activeTestsConfigOutput;
        private string _configConstsOutput;
        private int _existingUserValueIndex;
        private int _restartIndex;
        private bool _ignoreFirstTestNameWordInConfigClass = true;
        
        private List<string> _testValues = new ();
        private int _pendingFocusTestValue = -1;
        private string[] _restartEnumStrings;

        private void Awake()
        {
            _restartEnumStrings = Enum.GetNames(typeof(Tri));
        }

        protected void OnGUI()
        {
            EditorGUILayout.BeginVertical();
            
            _testName = EditorGUILayout.TextField("Test Name:", _testName);

            if (_dataType == DataType.String)
                _ignoreFirstTestNameWordInConfigClass = EditorGUILayout.ToggleLeft("Ignore first word in ConfigConsts", _ignoreFirstTestNameWordInConfigClass);
            
            var selectedDataType = (DataType)EditorGUILayout.EnumPopup("Data Type:", _dataType);

            EditorGUILayout.LabelField("Test Values:", EditorStyles.boldLabel);

            if (_testValues.Count > 0 && _testValues[0].Length > 0 && selectedDataType != _dataType)
            {
                var confirmClear = true;

                if (_dataType != DataType.Bool)
                {
                    confirmClear = EditorUtility.DisplayDialog(
                        "AB Test Generator Warning!",
                        "Warning! Changing data types will clear all test values. Are you sure you want to do this?",
                        "Yes - delete values",
                        "Cancel"
                    );
                }

                if (confirmClear)
                {
                    _testValues.Clear();
                    _dataType = selectedDataType;
                }
            }
            else
                _dataType = selectedDataType;
            
            switch (_dataType)
            {
                case DataType.Bool:
                {
                    if (_testValues.Count == 0)
                    {
                        _testValues.Add("false");
                        _testValues.Add("true");
                    }

                    break;
                }
                
                case DataType.String:
                case DataType.Int:
                case DataType.Float:
                {
                    if (_testValues.Count == 0)
                        _testValues.Add("");

                    break;
                }
            }

            for (var i = 0; i < _testValues.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();

                if (_dataType == DataType.Bool)
                    GUI.enabled = false;
                
                GUI.SetNextControlName("testValue" + i);
                
                if (_dataType is (DataType.Bool or DataType.String))
                    _testValues[i] = EditorGUILayout.TextField("", _testValues[i]);
                else
                {
                    if (_dataType == DataType.Int)
                    {
                        int.TryParse(_testValues[i], out var testValue);
                        var intValue = EditorGUILayout.IntField("", testValue);
                        _testValues[i] = intValue.ToString();
                    }
                    
                    if (_dataType == DataType.Float)
                    {
                        float.TryParse(_testValues[i], out var testValue);
                        var floatValue = EditorGUILayout.FloatField("", testValue);
                        _testValues[i] = floatValue.ToString();
                    }
                }

                if (_dataType == DataType.Bool)
                    GUI.enabled = true;
                else
                {
                    if (_testValues.Count > 1)
                    {
                        if (GUILayout.Button("-", new GUIStyle(GUI.skin.button) { fixedWidth = 30 }))
                            _testValues.RemoveAt(i);
                    }
                }

                EditorGUILayout.EndHorizontal();
            }
            
            if (_dataType != DataType.Bool)
            {
                if (GUILayout.Button("+", new GUIStyle(GUI.skin.button) {fixedWidth = 30}))
                    _testValues.Add("");
            }

            _existingUserValueIndex = EditorGUILayout.Popup("Existing User Value:", _existingUserValueIndex, _testValues.ToArray());
            _restartIndex = EditorGUILayout.Popup("Restart:", _restartIndex, _restartEnumStrings);
 
            EditorGUILayout.LabelField("ActiveTestsConfig.cs Output:", EditorStyles.boldLabel);
            
            var testOutput = GenerateTestOutput();
            EditorGUILayout.SelectableLabel(testOutput, EditorStyles.textArea, GUILayout.ExpandHeight(true));

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Copy"))
                GUIUtility.systemCopyBuffer = testOutput;
            
            if (GUILayout.Button("Open ActiveTestsConfig.cs"))
                OpenFile("Assets/KooplyRun/Scripts/Configuration/ActiveTestsConfig.cs");
            
            if (GUILayout.Button("Open Config.Active.cs"))
                OpenFile("Assets/Scripts/_INFRA_SPECIFICS/Config.Active.cs");
            
            EditorGUILayout.EndHorizontal();
         
            if (_dataType == DataType.String)
            {
                EditorGUILayout.LabelField("ConfigConsts.cs Output:", EditorStyles.boldLabel);
                
                var configConstsOutput = GenerateConfigConstsOutput();
                EditorGUILayout.SelectableLabel(configConstsOutput, EditorStyles.textArea, GUILayout.ExpandHeight(true));
                
                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("Copy"))
                    GUIUtility.systemCopyBuffer = configConstsOutput;

                if (GUILayout.Button("Open ConfigConsts.cs"))
                    OpenFile("Assets/KooplyRun/Scripts/Configuration/ConfigConsts.cs");
                
                if (GUILayout.Button("Open Config.Consts.cs"))
                    OpenFile("Assets/Scripts/_INFRA_SPECIFICS/Config.Consts.cs");
                
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space();
            EditorGUILayout.EndVertical();
        }

        private string GenerateTestOutput()
        {
            var result = new StringBuilder();
            var testValues = new List<string>(_testValues);
            var stringTestClassName = GenerateStringsTestClassName();
            
            for (var i = 0; i < _testValues.Count; i++)
            {
                if (_dataType == DataType.String)
                    testValues[i] = $"{stringTestClassName}.{SnakeCaseToCamelCase(_testValues[i])}";
            }

            var configTestValues = testValues.JoinStrings();
            if (_existingUserValueIndex >= 0 && _existingUserValueIndex < _testValues.Count)
                configTestValues += ", ExistingUserValue = " + testValues[_existingUserValueIndex];
            
            if (_restartIndex > 0 && _restartIndex < _restartEnumStrings.Length)
                configTestValues += ", Restart = Tri." + _restartEnumStrings[_restartIndex];
            
            result.Append($"[ConfigTest({configTestValues})]\npublic ");

            switch (_dataType)
            {
                case DataType.Bool:
                    result.Append("bool");
                    break;
                
                case DataType.String: 
                    result.Append("string");
                    break;
                
                case DataType.Int: 
                    result.Append("int");
                    break;
                
                case DataType.Float: 
                    result.Append("float");
                    break;
            }

            result.Append($" {_testName}");

            if (!_testValues[0].IsNullOrEmpty())
                result.Append($" = {testValues[0]}");

            result.Append(";");

            return result.ToString();
        }

        private string GenerateConfigConstsOutput()
        {
            var result = new StringBuilder();
            var values = new StringBuilder();

            result.Append($"// TODO: Delete when {_testName} is closed\n");
            
            for (var i = 0; i < _testValues.Count; i++)
            {
                var stringName = SnakeCaseToCamelCase(_testValues[i]);
                values.Append($"\tpublic const string {stringName} = \"{_testValues[i].ToSnakeCase()}\";");

                if (i < _testValues.Count - 1)
                    values.Append("\n");
            }
            
            result.Append($"public static class {GenerateStringsTestClassName()}\n{{\n{values}\n}}");
            return result.ToString();
        }

        private string GenerateStringsTestClassName()
        {
            return $"{SnakeCaseToCamelCase(_testName, _ignoreFirstTestNameWordInConfigClass)}Consts";
        }
        
        private string SnakeCaseToCamelCase(string input, bool ignoreFirstWord = false)
        {
            if (input.IsNullOrEmpty())
                return "";
            
            var words = input.Split('_');
            for (var i = 0; i < words.Length; i++)
            {
                if (words[i].Length > 0)
                    words[i] = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(words[i].ToLower());
            }

            if (ignoreFirstWord && words.Length > 1 && words[0].Length > 0)
                return string.Join("", words.Skip(1));

            return string.Join("", words);
        }

        private void OpenFile(string path)
        {
            AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<MonoScript>(path));
        }
    }
}