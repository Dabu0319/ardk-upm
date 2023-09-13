using System;
using System.Collections.Generic;
using Niantic.Lightship.AR.Loader;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Niantic.Lightship.AR.Editor
{
    /// <summary>
    /// This Editor renders to the XR Plug-in Management category of the Project Settings window.
    /// </summary>
    [CustomEditor(typeof(LightshipSettings))]
    class LightshipSettingsEditor : UnityEditor.Editor
    {
        private string[] _platforms = {"Editor", "Device"};
        private int _platformSelected = 0;

        private SerializedObject _lightshipSettings;

        private SerializedProperty _apiKeyProperty;
        private SerializedProperty _useLightshipDepthProperty;
        private SerializedProperty _useLightshipMeshingProperty;
        private SerializedProperty _preferLidarIfAvailableProperty;
        private SerializedProperty _useLightshipPersistentAnchorProperty;
        private SerializedProperty _useLightshipSemanticSegmentationProperty;
        private SerializedProperty _useLightshipScanningProperty;

        private readonly PlaybackProperties[] _platformPlaybackProperties = new PlaybackProperties[2];
        private PlaybackProperties _editorPlaybackProperties;
        private PlaybackProperties _devicePlaybackProperties;

        [Serializable]
        private struct PlaybackProperties
        {
            public SerializedProperty UsePlayback;
            public SerializedProperty DatasetPath;
            public SerializedProperty RunManually;
            public SerializedProperty LoopInfinitely;
            public SerializedProperty NumberOfIterations;
        }

        void OnEnable()
        {
            _lightshipSettings = new SerializedObject(LightshipSettings.Instance);
            _apiKeyProperty = _lightshipSettings.FindProperty("_apiKey");
            _useLightshipDepthProperty = _lightshipSettings.FindProperty("_useLightshipDepth");
            _useLightshipMeshingProperty = _lightshipSettings.FindProperty("_useLightshipMeshing");
            _preferLidarIfAvailableProperty = _lightshipSettings.FindProperty("_preferLidarIfAvailable");
            _useLightshipPersistentAnchorProperty = _lightshipSettings.FindProperty("_useLightshipPersistentAnchor");
            _useLightshipSemanticSegmentationProperty = _lightshipSettings.FindProperty("_useLightshipSemanticSegmentation");
            _useLightshipScanningProperty = _lightshipSettings.FindProperty("_useLightshipScanning");

            string[] platformSettingsStrings = {"_editorPlaybackSettings", "_devicePlaybackSettings"};
            for (int i = 0; i < _platformPlaybackProperties.Length; i++)
            {
                _platformPlaybackProperties[i].UsePlayback =
                    _lightshipSettings.FindProperty($"{platformSettingsStrings[i]}._usePlayback");
                _platformPlaybackProperties[i].DatasetPath =
                    _lightshipSettings.FindProperty($"{platformSettingsStrings[i]}._playbackDatasetPath");
                _platformPlaybackProperties[i].RunManually =
                    _lightshipSettings.FindProperty($"{platformSettingsStrings[i]}._runPlaybackManually");
                _platformPlaybackProperties[i].LoopInfinitely =
                    _lightshipSettings.FindProperty($"{platformSettingsStrings[i]}._loopInfinitely");
                _platformPlaybackProperties[i].NumberOfIterations =
                    _lightshipSettings.FindProperty($"{platformSettingsStrings[i]}._numberOfIterations");
            }
        }

        public override void OnInspectorGUI()
        {
            _lightshipSettings.Update();

            using (var change = new EditorGUI.ChangeCheckScope())
            {
                // Disable changes during runtime
                EditorGUI.BeginDisabledGroup(Application.isPlaying);

                EditorGUIUtility.labelWidth = 175;

                EditorGUILayout.LabelField("Credentials", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_apiKeyProperty, new GUIContent("Api Key"));

                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Depth", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_useLightshipDepthProperty, new GUIContent("Enabled"));
                EditorGUI.indentLevel++;
                // Put Depth sub-settings here
                if (_useLightshipDepthProperty.boolValue)
                {
                    EditorGUILayout.PropertyField
                    (
                        _preferLidarIfAvailableProperty,
                        new GUIContent("Prefer LiDAR if Available")
                    );
                }

                EditorGUI.indentLevel--;

                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Semantic Segmentation", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_useLightshipSemanticSegmentationProperty, new GUIContent("Enabled"));

                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Meshing", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_useLightshipMeshingProperty, new GUIContent("Enabled"));
                EditorGUI.indentLevel++;
                // Put Meshing sub-settings here
                EditorGUI.indentLevel--;

                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Persistent Anchors", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_useLightshipPersistentAnchorProperty, new GUIContent("Enabled"));
                EditorGUI.indentLevel++;
                // Put Persistent Anchors sub-settings here
                EditorGUI.indentLevel--;

                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Scanning", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_useLightshipScanningProperty, new GUIContent("Enabled"));
                EditorGUI.indentLevel++;
                // Put Scanning sub-settings here
                EditorGUI.indentLevel--;

                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Playback", EditorStyles.boldLabel);

                GUILayout.BeginHorizontal();
                _platformSelected = GUILayout.Toolbar(_platformSelected, _platforms);
                GUILayout.EndHorizontal();

                DrawPlaybackGui();

                EditorGUI.EndDisabledGroup();

                if (change.changed)
                {
                    _lightshipSettings.ApplyModifiedProperties();
                }
            }
        }

        private void DrawPlaybackGui()
        {
            EditorGUILayout.PropertyField
            (
                _platformPlaybackProperties[_platformSelected].UsePlayback,
                new GUIContent("Enabled")
            );

            if (_platformPlaybackProperties[_platformSelected].UsePlayback.boolValue)
            {
                EditorGUI.indentLevel++;
                DrawDatasetPathGUI();
                EditorGUILayout.PropertyField
                (
                    _platformPlaybackProperties[_platformSelected].RunManually,
                    new GUIContent("Run Manually")
                );

                EditorGUILayout.PropertyField
                (
                    _platformPlaybackProperties[_platformSelected].LoopInfinitely,
                    new GUIContent("Loop Infinitely")
                );

                if (!_platformPlaybackProperties[_platformSelected].LoopInfinitely.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField
                    (
                        _platformPlaybackProperties[_platformSelected].NumberOfIterations,
                        new GUIContent("Number of iterations")
                    );

                    // prevent usage of 0 iterations
                    if (_platformPlaybackProperties[_platformSelected].NumberOfIterations.intValue == 0)
                        _platformPlaybackProperties[_platformSelected].NumberOfIterations.intValue = 1;
                    EditorGUI.indentLevel--;
                }

                EditorGUI.indentLevel--;
            }
        }

        private void DrawDatasetPathGUI()
        {
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.PropertyField
            (
                _platformPlaybackProperties[_platformSelected].DatasetPath,
                new GUIContent("Dataset Path")
            );

            if (GUILayout.Button("Browse", GUILayout.Width(125)))
            {
                var path = EditorUtility.OpenFolderPanel
                (
                    "Select Dataset Directory",
                    _platformPlaybackProperties[_platformSelected].DatasetPath.stringValue,
                    ""
                );

                if (path.Length > 0)
                {
                    _platformPlaybackProperties[_platformSelected].DatasetPath.stringValue = path;
                }
            }

            EditorGUILayout.EndHorizontal();
        }
    }
}
