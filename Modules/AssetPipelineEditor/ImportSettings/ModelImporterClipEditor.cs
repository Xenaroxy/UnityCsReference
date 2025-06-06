// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System;
using UnityEngine;
using UnityEditorInternal;
using System.Collections.Generic;
using UnityEditor.AssetImporters;
using UnityEngine.Profiling;
using Object = UnityEngine.Object;
using System.Globalization;
using System.Linq;
using UnityEditor.Presets;

namespace UnityEditor
{
    internal class ModelImporterClipEditor : BaseAssetImporterTabUI
    {
        AnimationClipEditor m_AnimationClipEditor;
        ModelImporter singleImporter { get { return targets[0] as ModelImporter; } }

        internal const string ActiveClipIndex = "ModelImporterClipEditor.ActiveClipIndex";

        public string selectedClipName
        {
            get
            {
                var clipInfo = GetSelectedClipInfo();
                return clipInfo != null ? clipInfo.name : "";
            }
        }

        private class ClipInformation
        {
            SerializedProperty prop;

            public ClipInformation(SerializedProperty clipProperty)
            {
                prop = clipProperty;
                animationClipProperty = clipProperty;
            }

            public SerializedProperty animationClipProperty { get; }
            AnimationClipInfoProperties m_Property;
            public AnimationClipInfoProperties property => m_Property ?? (m_Property = new AnimationClipInfoProperties(animationClipProperty));

            public string name
            {
                get { return prop.FindPropertyRelative("name").stringValue; }
            }

            public string firstFrame
            {
                get { return prop.FindPropertyRelative("firstFrame").floatValue.ToString("0.0", CultureInfo.InvariantCulture.NumberFormat); }
            }

            public string lastFrame
            {
                get { return prop.FindPropertyRelative("lastFrame").floatValue.ToString("0.0", CultureInfo.InvariantCulture.NumberFormat); }
            }
        }

        SerializedObject m_DefaultClipsSerializedObject = null;

#pragma warning disable 0649
        [CacheProperty]
        SerializedProperty m_AnimationType;

        [CacheProperty]
        SerializedProperty m_ImportAnimation;
        [CacheProperty]
        SerializedProperty m_ClipAnimations;
        [CacheProperty]
        SerializedProperty m_BakeSimulation;
        [CacheProperty]
        SerializedProperty m_ResampleCurves;
        [CacheProperty]
        SerializedProperty m_AnimationCompression;
        [CacheProperty]
        SerializedProperty m_AnimationRotationError;
        [CacheProperty]
        SerializedProperty m_AnimationPositionError;
        [CacheProperty]
        SerializedProperty m_AnimationScaleError;
        [CacheProperty]
        SerializedProperty m_AnimationWrapMode;
        [CacheProperty]
        SerializedProperty m_LegacyGenerateAnimations;
        [CacheProperty]
        SerializedProperty m_ImportAnimatedCustomProperties;
        [CacheProperty]
        SerializedProperty m_ImportConstraints;

        [CacheProperty]
        SerializedProperty m_MotionNodeName;
        [CacheProperty]
        SerializedProperty m_RemoveConstantScaleCurves;
        [CacheProperty]
        SerializedProperty m_ContainsAnimation;
#pragma warning restore 0649

        public int motionNodeIndex { get; set; }

        public int pivotNodeIndex { get; set; }

#pragma warning disable 0649
        [CacheProperty]
        private SerializedProperty m_AnimationImportErrors;
        [CacheProperty]
        private SerializedProperty m_AnimationImportWarnings;
        [CacheProperty]
        private SerializedProperty m_AnimationRetargetingWarnings;
        [CacheProperty]
        private SerializedProperty m_AnimationDoRetargetingWarnings;
#pragma warning restore 0649

        string m_Errors;
        string m_Warnings;
        string m_RetargetWarnings;

        GUIContent[] m_MotionNodeList;
        private static bool s_MotionNodeFoldout = false;
        private static bool s_ImportMessageFoldout = false;

        //Prefix used to pick up errors concerning the rig importation
        private const string k_RigErrorPrefix = "Rig Error: ";

        ReorderableList m_ClipList;

        bool isEditorPreset;

        private string[] referenceTransformPaths
        {
            get { return singleImporter.transformPaths; }
        }

        private ModelImporterAnimationType animationType
        {
            get { return (ModelImporterAnimationType)m_AnimationType.intValue; }
            set { m_AnimationType.intValue = (int)value; }
        }

        private ModelImporterGenerateAnimations legacyGenerateAnimations
        {
            get { return (ModelImporterGenerateAnimations)m_LegacyGenerateAnimations.intValue; }
            set { m_LegacyGenerateAnimations.intValue = (int)value; }
        }

        private class Styles
        {
            public GUIContent ErrorsFoundWhileImportingThisAnimation = EditorGUIUtility.TrTextContentWithIcon("Error(s) found while importing this animation file. Open \"Import Messages\" foldout below for more details.", MessageType.Error);
            public GUIContent WarningsFoundWhileImportingRig = EditorGUIUtility.TrTextContentWithIcon("Warning(s) found while importing rig in this animation file. Open \"Rig\" tab for more details.", MessageType.Warning);
            public GUIContent WarningsFoundWhileImportingThisAnimation = EditorGUIUtility.TrTextContentWithIcon("Warning(s) found while importing this animation file. Open \"Import Messages\" foldout below for more details.", MessageType.Warning);
            public GUIContent ImportAnimations = EditorGUIUtility.TrTextContent("Import Animation", "Controls if animations are imported.");

            public GUIStyle numberStyle = new GUIStyle(EditorStyles.label);

            public GUIContent AnimWrapModeLabel = EditorGUIUtility.TrTextContent("Wrap Mode", "The default Wrap Mode for the animation in the mesh being imported.");

            public GUIContent[] AnimWrapModeOpt =
            {
                EditorGUIUtility.TrTextContent("Default", "The animation plays as specified in the animation splitting options below."),
                EditorGUIUtility.TrTextContent("Once", "The animation plays through to the end once and then stops."),
                EditorGUIUtility.TrTextContent("Loop", "The animation plays through and then restarts when the end is reached."),
                EditorGUIUtility.TrTextContent("PingPong", "The animation plays through and then plays in reverse from the end to the start, and so on."),
                EditorGUIUtility.TrTextContent("ClampForever", "The animation plays through, but the last frame is repeated indefinitely. This is not the same as Once mode because playback does not technically stop at the last frame (which is useful when blending animations).")
            };

            public GUIContent BakeIK = EditorGUIUtility.TrTextContent("Bake Animations", "Enable this when using IK or simulation in your animation package. Unity will convert to forward kinematics on import. This option is available only for Maya, 3dsMax and Cinema4D files.");
            public GUIContent ResampleCurves = EditorGUIUtility.TrTextContent("Resample Curves ", " Curves will be resampled on every frame. Use this if you're having issues with the interpolation between keys in your original animation. Disable this to keep curves as close as possible to how they were originally authored.");
            public GUIContent AnimCompressionLabel = EditorGUIUtility.TrTextContent("Anim. Compression", "The type of compression that will be applied to this mesh's animation(s).");
            public GUIContent[] AnimCompressionOptLegacy =
            {
                EditorGUIUtility.TrTextContent("Off", "Disables animation compression. This means that Unity doesn't reduce keyframe count on import, which leads to the highest precision animations, but slower performance and bigger file and runtime memory size. It is generally not advisable to use this option - if you need higher precision animation, you should enable keyframe reduction and lower allowed Animation Compression Error values instead."),
                EditorGUIUtility.TrTextContent("Keyframe Reduction", "Reduces keyframes on import. If selected, the Animation Compression Errors options are displayed."),
                EditorGUIUtility.TrTextContent("Keyframe Reduction and Compression", "Reduces keyframes on import and compresses keyframes when storing animations in files. This affects only file size - the runtime memory size is the same as Keyframe Reduction. If selected, the Animation Compression Errors options are displayed.")
            };
            public GUIContent[] AnimCompressionOpt =
            {
                EditorGUIUtility.TrTextContent("Off", "Disables animation compression. This means that Unity doesn't reduce keyframe count on import, which leads to the highest precision animations, but slower performance and bigger file and runtime memory size. It is generally not advisable to use this option - if you need higher precision animation, you should enable keyframe reduction and lower allowed Animation Compression Error values instead."),
                EditorGUIUtility.TrTextContent("Keyframe Reduction", "Reduces keyframes on import. If selected, the Animation Compression Errors options are displayed."),
                EditorGUIUtility.TrTextContent("Optimal", "Reduces keyframes on import and choose between different curve representations to reduce memory usage at runtime. This affects the runtime memory size and how curves are evaluated.")
            };

            public GUIContent AnimRotationErrorLabel = EditorGUIUtility.TrTextContent("Rotation Error", "Defines how much rotation curves should be reduced. The smaller value you use - the higher precision you get.");
            public GUIContent AnimPositionErrorLabel = EditorGUIUtility.TrTextContent("Position Error", "Defines how much position curves should be reduced. The smaller value you use - the higher precision you get.");
            public GUIContent AnimScaleErrorLabel = EditorGUIUtility.TrTextContent("Scale Error", "Defines how much scale curves should be reduced. The smaller value you use - the higher precision you get.");
            public GUIContent AnimationCompressionHelp = EditorGUIUtility.TrTextContent("Rotation error is defined as maximum angle deviation allowed in degrees, for others it is defined as maximum distance/delta deviation allowed in percents");
            public GUIContent clipMultiEditInfo = EditorGUIUtility.TrTextContent("Multi-object editing of clips not supported.");

            public GUIContent updateMuscleDefinitionFromSource = EditorGUIUtility.TrTextContent("Update", "Update the copy of the muscle definition from the source.");

            public GUIContent MotionSetting = EditorGUIUtility.TrTextContent("Motion", "Advanced setting for root motion and blending pivot");
            public GUIContent MotionNode = EditorGUIUtility.TrTextContent("Root Motion Node", "Define a transform node that will be used to create root motion curves");
            public GUIContent ImportMessages = EditorGUIUtility.TrTextContent("Import Messages");

            public GUIContent GenerateRetargetingWarnings = EditorGUIUtility.TrTextContent("Generate Retargeting Quality Report");
            public GUIContent RetargetingQualityCompares = EditorGUIUtility.TrTextContentWithIcon("Retargeting Quality compares retargeted with original animation. It reports average and maximum position/orientation difference for body parts. It may slow down import time of this file.", MessageType.Info);
            public GUIContent AnimationDataWas = EditorGUIUtility.TrTextContentWithIcon("Animation data was imported using a deprecated Generation option in the Rig tab. Please switch to a non-deprecated import mode in the Rig tab to be able to edit the animation import settings.", MessageType.Info);
            public GUIContent TheAnimationsSettingsCanBe = EditorGUIUtility.TrTextContentWithIcon("The animations settings can be edited after clicking Apply.", MessageType.Info);
            public GUIContent ErrorsFoundWhileImporting = EditorGUIUtility.TrTextContentWithIcon("Error(s) found while importing rig in this animation file. Open \"Rig\" tab for more details.", MessageType.Error);
            public GUIContent NoAnimationDataAvailable = EditorGUIUtility.TrTextContentWithIcon("No animation data available in this model.", MessageType.Info);
            public GUIContent TheRigsOfTheSelectedModelsHave = EditorGUIUtility.TrTextContentWithIcon("The rigs of the selected models have different Animation Types.", MessageType.Info);
            public GUIContent TheRigsOfTheSelectedModelsAre = EditorGUIUtility.TrTextContentWithIcon("The rigs of the selected models are not setup to handle animation. Change the Animation Type in the Rig tab and click Apply.", MessageType.Info);
            public GUIContent Clips = EditorGUIUtility.TrTextContent("Clips");
            public GUIContent ClipName = EditorGUIUtility.TrTextContent("Clip Name");
            public GUIContent TakeName = EditorGUIUtility.TrTextContent("Take Reference Name", "Defines the name of the referenced clip that these values will be applied to. If referenced clip is not present, these clip values will be ignored.");
            public GUIContent Start = EditorGUIUtility.TrTextContent("Start");
            public GUIContent End = EditorGUIUtility.TrTextContent("End");
            public GUIContent MaskHasAPath = EditorGUIUtility.TrTextContent("Mask has a path that does not match the transform hierarchy. Animation may not import correctly.");
            public GUIContent UpdateMask = EditorGUIUtility.TrTextContent("Update Mask");
            public GUIContent SourceMaskHasChanged = EditorGUIUtility.TrTextContent("Source Mask has changed since last import and must be updated.");
            public GUIContent SourceMaskHasAPath = EditorGUIUtility.TrTextContent("Source Mask has a path that does not match the transform hierarchy. Animation may not import correctly.");

            public GUIContent Mask = EditorGUIUtility.TrTextContent("Mask", "Configure the mask for this clip to remove unnecessary curves.");

            public GUIContent ImportAnimatedCustomProperties = EditorGUIUtility.TrTextContent("Import Animated Custom Properties", "Controls if animated custom properties are imported.");
            public GUIContent ImportConstraints = EditorGUIUtility.TrTextContent("Import Constraints", "Controls if the constraints are imported.");
            public GUIContent RemoveConstantScaleCurves = EditorGUIUtility.TrTextContent("Remove Constant Scale Curves", "Removes constant animation curves with values identical to the object initial scale value.");
            public GUIContent ClipList = EditorGUIUtility.TrTextContent("Animation Clip List", "List of animation clips included in the model.");

            public Styles()
            {
                numberStyle.alignment = TextAnchor.UpperRight;
            }
        }
        static Styles styles;

        public ModelImporterClipEditor(AssetImporterEditor panelContainer)
            : base(panelContainer)
        {
            //Generate new Clip List
            m_ClipList = new ReorderableList(new List<ClipInformation>(), typeof(string), false, true, true, true);
            m_ClipList.onAddCallback = AddClipInList;
            m_ClipList.onSelectCallback = SelectClipInList;
            m_ClipList.onRemoveCallback = RemoveClipInList;
            m_ClipList.drawElementCallback = DrawClipElement;
            m_ClipList.drawHeaderCallback = DrawClipHeader;
            m_ClipList.drawFooterCallback = DrawClipFooter;
            m_ClipList.elementHeight = EditorGUI.kSingleLineHeight;
        }

        internal override void OnEnable()
        {
            Initialize();
        }

        internal override void PostSerializedObjectCreation()
        {
            Editor.AssignCachedProperties(this, serializedObject.GetIterator());
        }

        void Initialize()
        {
            Editor.AssignCachedProperties(this, serializedObject.GetIterator());

            // caching errors values now as they can't change until next re-import that will triggers a new OnEnable
            m_Errors = m_AnimationImportErrors.stringValue;
            m_Warnings = m_AnimationImportWarnings.stringValue;
            m_RetargetWarnings = m_AnimationRetargetingWarnings.stringValue;

            RegisterListeners();

            if (serializedObject.isEditingMultipleObjects)
                return;

            //Sometimes we dont want to start at the 0th index, this is where we're editing a clip - see
            m_ClipList.index = EditorPrefs.GetInt(ActiveClipIndex, 0);
            EditorPrefs.DeleteKey(ActiveClipIndex);
            //Reset the Model Importer to its serialized copy
            DeserializeClips();

            string[] transformPaths = singleImporter.transformPaths;
            m_MotionNodeList = new GUIContent[transformPaths.Length + 1];

            m_MotionNodeList[0] = EditorGUIUtility.TrTextContent("<None>");
            if (m_MotionNodeList.Length > 1)
                m_MotionNodeList[1] = EditorGUIUtility.TrTextContent("<Root Transform>");

            for (int i = 1; i < transformPaths.Length; i++)
                m_MotionNodeList[i + 1] = new GUIContent(transformPaths[i]);

            motionNodeIndex = ArrayUtility.FindIndex(m_MotionNodeList, delegate(GUIContent content) { return content.text == m_MotionNodeName.stringValue; });
            motionNodeIndex = motionNodeIndex < 1 ? 0 : motionNodeIndex;

            isEditorPreset = Preset.IsEditorTargetAPreset(target);
        }

        void SyncClipEditor(AnimationClipInfoProperties info)
        {
            if (m_AnimationClipEditor == null || m_MaskInspector == null)
                return;

            // It mandatory to set clip info into mask inspector first, this will update m_Mask.
            m_MaskInspector.clipInfo = info;

            m_AnimationClipEditor.ShowRange(info);
            m_AnimationClipEditor.mask = m_Mask;
            AnimationCurvePreviewCache.ClearCache();
        }

        private void SetupDefaultClips()
        {
            // Create dummy SerializedObject where we can add a clip for each
            // take without making any properties show up as changed.
            m_DefaultClipsSerializedObject = new SerializedObject(target);
            m_ClipAnimations = m_DefaultClipsSerializedObject.FindProperty("m_ClipAnimations");
            m_AnimationType = m_DefaultClipsSerializedObject.FindProperty("m_AnimationType");
            m_ClipAnimations.ClearArray();

            int clipListIndex = m_ClipList.index;
            var allClipNames = new HashSet<string>();
            m_ClipAnimations.arraySize = singleImporter.importedTakeInfos.Length;
            var arrayElemProp = m_ClipAnimations.FindPropertyRelative("Array.size");
            for (int i = 0; i < singleImporter.importedTakeInfos.Length; i++)
            {
                TakeInfo takeInfo = singleImporter.importedTakeInfos[i];

                string uniqueName = MakeUniqueClipName(takeInfo.defaultClipName, allClipNames);
                string uniqueIdentifier = MakeUniqueClipName(takeInfo.name, allClipNames);
                allClipNames.Add(uniqueName);

                arrayElemProp.Next(false);
                AnimationClipInfoProperties info = new AnimationClipInfoProperties(arrayElemProp);
                InitAnimationClipInfoProperties(info, takeInfo,uniqueName, uniqueIdentifier,0);
            }
            UpdateList();

            //Attempt to maintain the previous clip index, now we've reverted to default clips
            m_ClipList.index = Mathf.Min(clipListIndex, m_ClipList.list.Count);
        }

        // When switching to explicitly defined clips, we must fix up the internalID's to not lose AnimationClip references.
        // When m_ClipAnimations is defined, the clips are identified by the clipName
        // When m_ClipAnimations is not defined, the clips are identified by the takeName
        void PatchDefaultClipTakeNamesToSplitClipNames()
        {
            foreach (TakeInfo takeInfo in singleImporter.importedTakeInfos)
            {
                UnityType animationClipType = UnityType.FindTypeByName("AnimationClip");
                ImportSettingInternalID.Rename(serializedObject, animationClipType, takeInfo.name, takeInfo.defaultClipName);
            }
        }

        // A dummy SerializedObject is created when there are no explicitly defined clips.
        // When the user modifies any settings these clips must be transferred to the model importer.
        private void TransferDefaultClipsToCustomClips()
        {
            if (m_DefaultClipsSerializedObject == null)
                return;

            bool wasEmpty = serializedObject.FindProperty("m_ClipAnimations").arraySize == 0;
            if (!wasEmpty)
                Debug.LogError("Transferring default clips failed, target already has clips");

            // Transfer data to main SerializedObject
            serializedObject.CopyFromSerializedProperty(m_ClipAnimations);
            m_ClipAnimations = serializedObject.FindProperty("m_ClipAnimations");

            m_DefaultClipsSerializedObject = null;

            PatchDefaultClipTakeNamesToSplitClipNames();
            UpdateList();

            if (m_ClipList.index >= 0)
                SyncClipEditor(((ClipInformation)m_ClipList.list[m_ClipList.index]).property);
        }

        internal override void OnDestroy()
        {
            DestroyEditorsAndData();
        }

        internal override void OnDisable()
        {
            UnregisterListeners();
            DestroyEditorsAndData();

            base.OnDisable();
        }

        internal override void ResetValues()
        {
            base.ResetValues();
            DeserializeClips();
        }

        void AnimationClipGUI(ImportLog.ImportLogEntry[] importRigWarnings)
        {
            if (m_Errors.Length > 0)
            {
                EditorGUILayout.HelpBox(styles.ErrorsFoundWhileImportingThisAnimation);
            }
            else
            {
                if (importRigWarnings.Length > 0)
                {
                    EditorGUILayout.HelpBox(styles.WarningsFoundWhileImportingRig);
                }

                if (m_Warnings.Length > 0)
                {
                    EditorGUILayout.HelpBox(styles.WarningsFoundWhileImportingThisAnimation);
                }
            }

            // Show general animation import settings
            AnimationSettings();

            if (serializedObject.isEditingMultipleObjects)
                return;

            Profiler.BeginSample("Clip inspector");

            EditorGUILayout.Space();

            // Show list of animations and inspector for individual animation
            if (targets.Length == 1)
                AnimationSplitTable();
            else
                GUILayout.Label(styles.clipMultiEditInfo, EditorStyles.helpBox);

            Profiler.EndSample();

            RootMotionNodeSettings();

            s_ImportMessageFoldout = EditorGUILayout.Foldout(s_ImportMessageFoldout, styles.ImportMessages, true);

            if (s_ImportMessageFoldout)
            {
                if (m_Errors.Length > 0)
                    EditorGUILayout.HelpBox(L10n.Tr(m_Errors), MessageType.Error);
                if (m_Warnings.Length > 0)
                    EditorGUILayout.HelpBox(L10n.Tr(m_Warnings), MessageType.Warning);
                if (animationType == ModelImporterAnimationType.Human)
                {
                    EditorGUILayout.PropertyField(m_AnimationDoRetargetingWarnings, styles.GenerateRetargetingWarnings);

                    if (m_AnimationDoRetargetingWarnings.boolValue)
                    {
                        if (m_RetargetWarnings.Length > 0)
                        {
                            EditorGUILayout.HelpBox(L10n.Tr(m_RetargetWarnings), MessageType.Info);
                        }
                    }
                    else
                    {
                        EditorGUILayout.HelpBox(styles.RetargetingQualityCompares);
                    }
                }
            }
        }

        public override void OnInspectorGUI()
        {
            if (styles == null)
                styles = new Styles();

            EditorGUILayout.PropertyField(m_ImportConstraints, styles.ImportConstraints);

            using (var check = new EditorGUI.ChangeCheckScope())
            {
                EditorGUILayout.PropertyField(m_ImportAnimation, styles.ImportAnimations);
                if (check.changed)
                    DeserializeClips();
            }

            if (m_ImportAnimation.boolValue)
            {
                EditorGUILayout.PropertyField(m_ImportAnimatedCustomProperties, styles.ImportAnimatedCustomProperties);
            }

            if (m_ImportAnimation.boolValue && !m_ImportAnimation.hasMultipleDifferentValues)
            {
                ImportLog importLog = AssetImporter.GetImportLog(singleImporter.assetPath);
                ImportLog.ImportLogEntry[] importRigErrors = importLog != null ? importLog.logEntries.Where(x => x.flags == ImportLogFlags.Error && x.message.StartsWith(k_RigErrorPrefix)).ToArray() : new ImportLog.ImportLogEntry[0];
                ImportLog.ImportLogEntry[] importRigWarnings = importLog != null ? importLog.logEntries.Where(x => x.flags == ImportLogFlags.Warning && x.message.StartsWith(k_RigErrorPrefix)).ToArray() : new ImportLog.ImportLogEntry[0];

                bool hasNoValidAnimationData = targets.Length == 1 && !m_ContainsAnimation.boolValue && singleImporter.animationType != ModelImporterAnimationType.None && !isEditorPreset;
                if (IsDeprecatedMultiAnimationRootImport())
                    EditorGUILayout.HelpBox(styles.AnimationDataWas);
                else if (hasNoValidAnimationData)
                {
                    if (serializedObject.hasModifiedProperties)
                    {
                        EditorGUILayout.HelpBox(styles.TheAnimationsSettingsCanBe);
                    }
                    else
                    {
                        if (importRigErrors.Length > 0)
                        {
                            EditorGUILayout.HelpBox(styles.ErrorsFoundWhileImporting);
                        }
                        else
                        {
                            EditorGUILayout.HelpBox(styles.NoAnimationDataAvailable);
                        }
                    }
                }
                else if (m_AnimationType.hasMultipleDifferentValues)
                    EditorGUILayout.HelpBox(styles.TheRigsOfTheSelectedModelsHave);
                else if (animationType == ModelImporterAnimationType.None)
                    EditorGUILayout.HelpBox(styles.TheRigsOfTheSelectedModelsAre);
                else if (singleImporter.importedTakeInfos.Length != 0 || isEditorPreset)
                {
                    AnimationClipGUI(importRigWarnings);
                }
            }
        }

        void AnimationSettings()
        {
            EditorGUILayout.Space();

            // Bake IK
            bool isBakeIKSupported = true;
            foreach (ModelImporter importer in targets)
                if (!importer.isBakeIKSupported)
                    isBakeIKSupported = false;
            using (new EditorGUI.DisabledScope(!isBakeIKSupported))
            {
                EditorGUILayout.PropertyField(m_BakeSimulation, styles.BakeIK);
            }

            if (animationType == ModelImporterAnimationType.Generic)
            {
                EditorGUILayout.PropertyField(m_ResampleCurves, styles.ResampleCurves);
            }
            else
            {
                m_ResampleCurves.boolValue = true;
            }

            // Wrap mode
            if (animationType == ModelImporterAnimationType.Legacy)
            {
                EditorGUI.showMixedValue = m_AnimationWrapMode.hasMultipleDifferentValues;
                EditorGUILayout.Popup(m_AnimationWrapMode, styles.AnimWrapModeOpt, styles.AnimWrapModeLabel);
                EditorGUI.showMixedValue = false;

                // Compression
                int[] kCompressionValues = { (int)ModelImporterAnimationCompression.Off, (int)ModelImporterAnimationCompression.KeyframeReduction, (int)ModelImporterAnimationCompression.KeyframeReductionAndCompression };
                EditorGUILayout.IntPopup(m_AnimationCompression, styles.AnimCompressionOptLegacy, kCompressionValues, styles.AnimCompressionLabel);
            }
            else
            {
                // Compression
                int[] kCompressionValues = { (int)ModelImporterAnimationCompression.Off, (int)ModelImporterAnimationCompression.KeyframeReduction, (int)ModelImporterAnimationCompression.Optimal };
                EditorGUILayout.IntPopup(m_AnimationCompression, styles.AnimCompressionOpt, kCompressionValues, styles.AnimCompressionLabel);
            }

            if (m_AnimationCompression.intValue > (int)ModelImporterAnimationCompression.Off)
            {
                // keyframe reduction settings
                EditorGUILayout.PropertyField(m_AnimationRotationError, styles.AnimRotationErrorLabel);
                EditorGUILayout.PropertyField(m_AnimationPositionError, styles.AnimPositionErrorLabel);
                EditorGUILayout.PropertyField(m_AnimationScaleError, styles.AnimScaleErrorLabel);
                GUILayout.Label(styles.AnimationCompressionHelp, EditorStyles.helpBox);
            }

            EditorGUILayout.PropertyField(m_RemoveConstantScaleCurves, styles.RemoveConstantScaleCurves);
        }

        void RootMotionNodeSettings()
        {
            if (animationType == ModelImporterAnimationType.Human || animationType == ModelImporterAnimationType.Generic)
            {
                s_MotionNodeFoldout = EditorGUILayout.Foldout(s_MotionNodeFoldout, styles.MotionSetting, true);

                if (s_MotionNodeFoldout)
                {
                    EditorGUI.BeginChangeCheck();
                    motionNodeIndex = EditorGUILayout.Popup(styles.MotionNode, motionNodeIndex, m_MotionNodeList);

                    if (EditorGUI.EndChangeCheck())
                    {
                        if (motionNodeIndex > 0 && motionNodeIndex < m_MotionNodeList.Length)
                        {
                            m_MotionNodeName.stringValue = m_MotionNodeList[motionNodeIndex].text;
                        }
                        else
                        {
                            m_MotionNodeName.stringValue = "";
                        }
                    }
                }
            }
        }

        void DestroyEditorsAndData()
        {
            if (m_AnimationClipEditor != null)
            {
                Object.DestroyImmediate(m_AnimationClipEditor);
                m_AnimationClipEditor = null;
            }

            if (m_MaskInspector)
            {
                DestroyImmediate(m_MaskInspector);
                m_MaskInspector = null;
            }
            if (m_Mask)
            {
                DestroyImmediate(m_Mask);
                m_Mask = null;
            }
        }

        void SelectClip(int selected)
        {
            // If you were editing Clip Name (delayed text field had focus) and then selected a new clip from the clip list,
            // the active string in the delayed text field would get applied to the new selected clip instead of the old.
            // HACK: Calling EndGUI here on the recycled delayed text editor seems to fix this issue.
            // Sometime we should reimplement delayed text field code to not be super confusing and then fix the issue more properly.
            if (EditorGUI.s_DelayedTextEditor != null && Event.current != null)
                EditorGUI.s_DelayedTextEditor.EndGUI(Event.current.type);

            DestroyEditorsAndData();

            m_ClipList.index = selected;
            if (m_ClipList.index < 0)
                return;

            AnimationClipInfoProperties info = ((ClipInformation)m_ClipList.list[m_ClipList.index]).property;
            AnimationClip clip = singleImporter.GetPreviewAnimationClipForTake(info.takeName);
            if (clip != null)
            {
                m_AnimationClipEditor = (AnimationClipEditor)Editor.CreateEditor(clip, typeof(AnimationClipEditor));
                InitMask(info);
                SyncClipEditor(info);
                m_AnimationClipEditor.InitClipTime();
            }
        }

        void UpdateList()
        {
            List<ClipInformation> clipInfos = new List<ClipInformation>();
            var prop = m_ClipAnimations.FindPropertyRelative("Array.size");
            for (int i = 0; i < m_ClipAnimations.arraySize; i++)
            {
                prop.Next(false);
                clipInfos.Add(new ClipInformation(prop.Copy()));
            }
            m_ClipList.list = clipInfos;
            m_ClipList.index = Mathf.Clamp(m_ClipList.index, -1, m_ClipAnimations.arraySize - 1);
        }

        void AddClipInList(ReorderableList list)
        {
            if (m_DefaultClipsSerializedObject != null)
                TransferDefaultClipsToCustomClips();


            int takeIndex = 0;
            if (0 < m_ClipList.index && m_ClipList.index < m_ClipAnimations.arraySize)
            {
                AnimationClipInfoProperties info = ((ClipInformation)m_ClipList.list[m_ClipList.index]).property;
                for (int i = 0; i < singleImporter.importedTakeInfos.Length; i++)
                {
                    if (singleImporter.importedTakeInfos[i].name == info.takeName)
                    {
                        takeIndex = i;
                        break;
                    }
                }
            }
            if (singleImporter.importedTakeInfos.Length <= 0)
            {
                TakeInfo newTake = new TakeInfo();
                if(m_ClipList.index < 0)
                {
                    newTake.name = "New Take Name";
                    newTake.defaultClipName = "New Clip";
                    AddClip(newTake);
                }
                else
                {
                    var property = m_ClipAnimations.GetArrayElementAtIndex(m_ClipList.index);
                    AnimationClipInfoProperties info = new AnimationClipInfoProperties(property);

                    newTake.name = info.takeName;
                    newTake.defaultClipName = info.name;
                    newTake.startTime = info.firstFrame;
                    newTake.stopTime = info.lastFrame;
                    AddClip(newTake);
                }
            }
            else
                AddClip(singleImporter.importedTakeInfos[takeIndex]);
            SelectClip(list.list.Count - 1);
        }

        void RemoveClipInList(ReorderableList list)
        {
            TransferDefaultClipsToCustomClips();

            RemoveClip(list.index);
            SelectClip(Mathf.Min(list.index, list.count - 1));
        }

        void SelectClipInList(ReorderableList list)
        {
            SelectClip(list.index);
        }

        const int kFrameColumnWidth = 45;

        private void DrawClipElement(Rect rect, int index, bool selected, bool focused)
        {
            EditorGUI.BeginProperty(rect, styles.ClipList, m_ClipAnimations);
            ClipInformation info = (ClipInformation)m_ClipList.list[index];
            rect.xMax -= kFrameColumnWidth * 2;
            GUI.Label(rect, info.name, EditorStyles.label);
            rect.x = rect.xMax;
            rect.width = kFrameColumnWidth;
            GUI.Label(rect, info.firstFrame, styles.numberStyle);
            rect.x = rect.xMax;
            GUI.Label(rect, info.lastFrame, styles.numberStyle);
            EditorGUI.EndProperty();
        }

        private void DrawClipHeader(Rect rect)
        {
            EditorGUI.BeginProperty(rect, styles.ClipList, m_ClipAnimations);
            rect.xMax -= kFrameColumnWidth * 2;
            GUI.Label(rect, styles.Clips, EditorStyles.label);
            rect.x = rect.xMax;
            rect.width = kFrameColumnWidth;
            GUI.Label(rect, styles.Start, styles.numberStyle);
            rect.x = rect.xMax;
            GUI.Label(rect, styles.End, styles.numberStyle);
            EditorGUI.EndProperty();
        }
        private void DrawClipFooter(Rect rect)
        {
            EditorGUI.BeginProperty(rect, styles.ClipList, m_ClipAnimations);
            ReorderableList.defaultBehaviours.DrawFooter(rect, m_ClipList);
            EditorGUI.EndProperty();
        }

        void DrawPresetClipEditor()
        {
            AnimationClipInfoProperties clip = GetSelectedClipInfo();
            EditorGUI.BeginProperty(EditorGUILayout.GetControlRect(), styles.ClipList, m_ClipAnimations);

            clip.name = EditorGUILayout.TextField(styles.ClipName, clip.name);
            clip.takeName = EditorGUILayout.TextField(styles.TakeName, clip.takeName);
            clip.firstFrame = EditorGUILayout.FloatField(styles.Start, clip.firstFrame);
            clip.lastFrame = EditorGUILayout.FloatField(styles.End, clip.lastFrame);
            clip.loopTime = EditorGUILayout.Toggle(AnimationClipEditor.Styles.LoopTime, clip.loopTime);
            using (new EditorGUI.DisabledScope(!clip.loopTime))
            {
                EditorGUI.indentLevel++;
                clip.loopBlend = EditorGUI.Toggle(EditorGUILayout.GetControlRect(), AnimationClipEditor.Styles.LoopPose, clip.loopBlend);
                clip.cycleOffset = EditorGUILayout.FloatField(AnimationClipEditor.Styles.LoopCycleOffset, clip.cycleOffset);
                EditorGUI.indentLevel--;
            }
            clip.hasAdditiveReferencePose = EditorGUILayout.Toggle(AnimationClipEditor.Styles.HasAdditiveReferencePose, clip.hasAdditiveReferencePose);
            using (new EditorGUI.DisabledScope(!clip.hasAdditiveReferencePose))
            {
                EditorGUI.indentLevel++;
                clip.additiveReferencePoseFrame = EditorGUILayout.FloatField(AnimationClipEditor.Styles.AdditiveReferencePoseFrame, clip.additiveReferencePoseFrame);
                EditorGUI.indentLevel--;
            }
            EditorGUI.EndProperty();
        }

        void AnimationSplitTable()
        {
            if (m_ClipList.count != m_ClipAnimations.arraySize)
            {
                UpdateList();
                SelectClipInList(m_ClipList);
            }

            if (singleImporter.importedTakeInfos.Length > 0 || isEditorPreset)
            {
                m_ClipList.DoLayoutList();

                EditorGUI.BeginChangeCheck();
                // Show unique Preset editor
                if (isEditorPreset && m_ClipAnimations.arraySize > 0)
                {
                    DrawPresetClipEditor();
                }
                // Show selected clip info
                else
                {
                    AnimationClipInfoProperties clip = GetSelectedClipInfo();
                    if (clip == null)
                        return;

                    if (m_AnimationClipEditor != null)
                    {
                        GUILayout.Space(5);

                        AnimationClip actualClip = m_AnimationClipEditor.target as AnimationClip;

                        if (!actualClip.legacy)
                            clip.AssignToPreviewClip(actualClip);

                        TakeInfo[] importedTakeInfos = singleImporter.importedTakeInfos;
                        string[] takeNames = new string[importedTakeInfos.Length];
                        for (int i = 0; i < importedTakeInfos.Length; i++)
                            takeNames[i] = importedTakeInfos[i].name;

                        EditorGUI.BeginChangeCheck();
                        string currentName = clip.name;
                        int takeIndex = ArrayUtility.IndexOf(takeNames, clip.takeName);
                        m_AnimationClipEditor.takeNames = takeNames;
                        m_AnimationClipEditor.takeIndex = ArrayUtility.IndexOf(takeNames, clip.takeName);
                        m_AnimationClipEditor.DrawHeader();

                        if (EditorGUI.EndChangeCheck())
                        {
                            clip.name = clip.name.Trim();
                            if (clip.name == String.Empty)
                            {
                                clip.name = currentName;
                            }
                            // We renamed the clip name, try to maintain the localIdentifierInFile so we don't lose any data.
                            if (clip.name != currentName)
                            {
                                var newName = clip.name;
                                clip.name = currentName;
                                clip.name = MakeUniqueClipName(newName);

                                TransferDefaultClipsToCustomClips();
                                UnityType animationClipType = UnityType.FindTypeByName("AnimationClip");
                                ImportSettingInternalID.Rename(serializedObject, animationClipType, currentName, clip.name);
                            }

                            int newTakeIndex = m_AnimationClipEditor.takeIndex;
                            if (newTakeIndex != -1 && newTakeIndex != takeIndex)
                            {
                                clip.name = MakeUniqueClipName(takeNames[newTakeIndex]);
                                SetupTakeNameAndFrames(clip, importedTakeInfos[newTakeIndex]);
                                GUIUtility.keyboardControl = 0;
                                SelectClip(m_ClipList.index);

                                // actualClip has been changed by SelectClip
                                actualClip = m_AnimationClipEditor.target as AnimationClip;
                            }
                        }

                        m_AnimationClipEditor.OnInspectorGUI();

                        AvatarMaskSettings(clip);

                        if (!actualClip.legacy)
                            clip.ExtractFromPreviewClip(actualClip);

                        if (EditorGUI.EndChangeCheck() || m_AnimationClipEditor.needsToGenerateClipInfo)
                        {
                            TransferDefaultClipsToCustomClips();
                            m_AnimationClipEditor.needsToGenerateClipInfo = false;
                        }
                    }
                }
            }
        }

        public override bool HasPreviewGUI()
        {
            return m_ImportAnimation.boolValue && m_AnimationClipEditor != null && m_AnimationClipEditor.HasPreviewGUI();
        }

        public override void OnPreviewSettings()
        {
            if (m_AnimationClipEditor != null)
                m_AnimationClipEditor.OnPreviewSettings();
        }

        bool IsDeprecatedMultiAnimationRootImport()
        {
            if (animationType == ModelImporterAnimationType.Legacy)
                return legacyGenerateAnimations == ModelImporterGenerateAnimations.InOriginalRoots || legacyGenerateAnimations == ModelImporterGenerateAnimations.InNodes;
            else
                return false;
        }

        public override void OnInteractivePreviewGUI(Rect r, GUIStyle background)
        {
            m_AnimationClipEditor.OnInteractivePreviewGUI(r, background);
        }

        AnimationClipInfoProperties GetSelectedClipInfo()
        {
            //If it doesn't have a selected clip. return null - there is nothing to select!
            return m_ClipList.index >= 0 && m_ClipList.index < m_ClipList.count ? ((ClipInformation)m_ClipList.list[m_ClipList.index]).property : null;
        }

        /// <summary>
        /// Removes the duplicate brackets from a name.
        /// </summary>
        /// <returns>The name without the duplicate suffix.</returns>
        /// <param name="name">Name.</param>
        /// <param name="number">Number between the brackets (-1 if no brackets were found).</param>
        string RemoveDuplicateSuffix(string name, out int number)
        {
            number = -1;

            // The smallest format is " (1)".
            int length = name.Length;
            if (length < 4 || name[length - 1] != ')')
                return name;

            // Has an opening bracket.
            int openingBracket = name.LastIndexOf('(', length - 2);
            if (openingBracket == -1 || name[openingBracket - 1] != ' ')
                return name;

            // Brackets aren't empty.
            int numberLength = length - openingBracket - 2;
            if (numberLength == 0)
                return name;

            // Only has digits between the brackets.
            int i = 0;
            while (i < numberLength && char.IsDigit(name[openingBracket + 1 + i]))
                ++i;
            if (i != numberLength)
                return name;

            // Get number.
            string numberString = name.Substring(openingBracket + 1, numberLength);
            number = int.Parse(numberString);

            // Extract base name.
            return name.Substring(0, openingBracket - 1);
        }

        string FindNextAvailableName(string baseName, HashSet<string> allClipNames)
        {
            string resultName = baseName;
            int occurences = 0;
            while (allClipNames.Contains(resultName))
            {
                occurences++;
                resultName = baseName + " (" + occurences + ")";
            }

            return resultName;
        }

        string MakeUniqueClipName(string name, HashSet<string> allClipNames = null)
        {
            int dummy;
            string baseName = RemoveDuplicateSuffix(name, out dummy);

            if (allClipNames == null)
            {
                // If no collection was provided, the current list of clips is used.
                allClipNames = new HashSet<string>();
                for (int i = 0; i < m_ClipAnimations.arraySize; ++i)
                {
                    AnimationClipInfoProperties clip = ((ClipInformation)m_ClipList.list[i]).property;
                    allClipNames.Add(clip.name);
                }
            }

            return FindNextAvailableName(baseName, allClipNames);
        }

        void RemoveClip(int index)
        {
            m_ClipAnimations.DeleteArrayElementAtIndex(index);
            if (m_ClipAnimations.arraySize == 0)
            {
                SetupDefaultClips();
                if(!isEditorPreset)
                    m_ImportAnimation.boolValue = false;
            }
            UpdateList();
        }

        void SetupTakeNameAndFrames(AnimationClipInfoProperties info, TakeInfo takeInfo)
        {
            info.takeName = takeInfo.name;
            info.firstFrame = (int)Mathf.Round(takeInfo.bakeStartTime * takeInfo.sampleRate);
            info.lastFrame = (int)Mathf.Round(takeInfo.bakeStopTime * takeInfo.sampleRate);
        }

        void AddClip(TakeInfo takeInfo)
        {
            string uniqueName = MakeUniqueClipName(takeInfo.defaultClipName);

            m_ClipAnimations.InsertArrayElementAtIndex(m_ClipAnimations.arraySize);
            var property = m_ClipAnimations.GetArrayElementAtIndex(m_ClipAnimations.arraySize - 1);
            AnimationClipInfoProperties info = new AnimationClipInfoProperties(property);
            InitAnimationClipInfoProperties(info, takeInfo, uniqueName,uniqueName, m_ClipAnimations.arraySize - 1);
            UpdateList();
        }

        void InitAnimationClipInfoProperties(AnimationClipInfoProperties info, TakeInfo takeInfo,string uniqueName,string uniqueIdentifier,int clipOffset)
        {
            SetupTakeNameAndFrames(info, takeInfo);

            var animationClipType = UnityType.FindTypeByName("AnimationClip");

            long id = ImportSettingInternalID.FindInternalID(serializedObject, animationClipType, uniqueIdentifier);

            info.internalID = id == 0L
                ? AssetImporter.MakeLocalFileIDWithHash(animationClipType.persistentTypeID, uniqueIdentifier, clipOffset)
                : id;

            info.name = uniqueName;
            info.wrapMode = (int)WrapMode.Default;
            info.loop = false;
            info.orientationOffsetY = 0;
            info.level = 0;
            info.cycleOffset = 0;
            info.loopTime = false;
            info.loopBlend = false;
            info.loopBlendOrientation = false;
            info.loopBlendPositionY = false;
            info.loopBlendPositionXZ = false;
            info.keepOriginalOrientation = false;
            info.keepOriginalPositionY = true;
            info.keepOriginalPositionXZ = false;
            info.heightFromFeet = false;
            info.mirror = false;
            info.maskType = ClipAnimationMaskType.None;

            SetBodyMaskDefaultValues(info);

            info.ClearEvents();
            info.ClearCurves();
        }

        private AvatarMask m_Mask = null;
        private AvatarMaskInspector m_MaskInspector = null;
        static private bool m_MaskFoldout = false;

        ////////////////////////////////////////////////////////////////////////////////////////////////////////
        ///
        private void AvatarMaskSettings(AnimationClipInfoProperties clipInfo)
        {
            if (clipInfo != null && m_AnimationClipEditor != null)
            {
                InitMask(clipInfo);
                int prevIndent = EditorGUI.indentLevel;

                // Don't make toggling foldout cause GUI.changed to be true (shouldn't cause undoable action etc.)
                bool wasChanged = GUI.changed;
                m_MaskFoldout = EditorGUILayout.Foldout(m_MaskFoldout, styles.Mask, true);
                GUI.changed = wasChanged;

                var maskType = clipInfo.maskType;
                bool upToDate = true;
                if (maskType == ClipAnimationMaskType.CreateFromThisModel || maskType == ClipAnimationMaskType.CopyFromOther)
                {
                    upToDate = m_MaskInspector.IsMaskUpToDate();
                }

                if (maskType == ClipAnimationMaskType.CreateFromThisModel && !upToDate && !m_MaskInspector.IsMaskEmpty())
                {
                    GUILayout.BeginHorizontal(EditorStyles.helpBox);
                    GUILayout.Label(styles.MaskHasAPath,
                        EditorStyles.wordWrappedMiniLabel);
                    GUILayout.FlexibleSpace();
                    GUILayout.BeginVertical();
                    GUILayout.Space(5);
                    if (GUILayout.Button(styles.UpdateMask))
                    {
                        SetTransformMaskFromReference(clipInfo);
                        m_MaskInspector.UpdateTransformInfos();
                    }

                    GUILayout.EndVertical();
                    GUILayout.EndHorizontal();
                }
                else if (maskType == ClipAnimationMaskType.CopyFromOther && clipInfo.MaskNeedsUpdating())
                {
                    GUILayout.BeginHorizontal(EditorStyles.helpBox);
                    GUILayout.Label(styles.SourceMaskHasChanged,
                        EditorStyles.wordWrappedMiniLabel);
                    GUILayout.FlexibleSpace();
                    GUILayout.BeginVertical();
                    GUILayout.Space(5);
                    if (GUILayout.Button(styles.UpdateMask))
                    {
                        clipInfo.MaskToClip(clipInfo.maskSource);
                    }
                    GUILayout.EndVertical();
                    GUILayout.EndHorizontal();
                }
                else if (maskType == ClipAnimationMaskType.CopyFromOther && !upToDate)
                {
                    GUILayout.BeginHorizontal(EditorStyles.helpBox);
                    GUILayout.Label(styles.SourceMaskHasAPath,
                        EditorStyles.wordWrappedMiniLabel);
                    GUILayout.FlexibleSpace();
                    GUILayout.BeginVertical();
                    GUILayout.Space(5);
                    GUILayout.EndVertical();
                    GUILayout.EndHorizontal();
                }

                if (m_MaskFoldout)
                {
                    EditorGUI.indentLevel++;
                    m_MaskInspector.OnInspectorGUI();
                }

                EditorGUI.indentLevel = prevIndent;
            }
        }

        private void InitMask(AnimationClipInfoProperties clipInfo)
        {
            if (m_Mask == null)
            {
                AnimationClip clip = m_AnimationClipEditor.target as AnimationClip;

                m_Mask = new AvatarMask();
                m_MaskInspector = (AvatarMaskInspector)Editor.CreateEditor(m_Mask, typeof(AvatarMaskInspector));
                m_MaskInspector.canImport = false;
                m_MaskInspector.showBody = clip.isHumanMotion;
                m_MaskInspector.clipInfo = clipInfo;
                m_MaskInspector.UpdateMask(clipInfo.maskType);
            }
        }

        private void SetTransformMaskFromReference(AnimationClipInfoProperties clipInfo)
        {
            string[] transformPaths = referenceTransformPaths;
            string[] humanTransforms = animationType == ModelImporterAnimationType.Human ?
                AvatarMaskUtility.GetAvatarHumanAndActiveExtraTransforms(serializedObject, clipInfo.transformMaskProperty, transformPaths) :
                AvatarMaskUtility.GetAvatarInactiveTransformMaskPaths(clipInfo.transformMaskProperty);

            AvatarMaskUtility.UpdateTransformMask(clipInfo.transformMaskProperty, transformPaths, humanTransforms, animationType == ModelImporterAnimationType.Human);
        }

        private void SetBodyMaskDefaultValues(AnimationClipInfoProperties clipInfo)
        {
            SerializedProperty bodyMask = clipInfo.bodyMaskProperty;
            bodyMask.arraySize = (int)AvatarMaskBodyPart.LastBodyPart;
            var prop = bodyMask.FindPropertyRelative("Array.size");
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; ++i)
            {
                prop.Next(false);
                prop.intValue = 1;
            }
        }

        void RegisterListeners()
        {
            //Ensures that the ClipList and the Serialized copy of the clip remain in sync when an Undo/Redo is performed.
            if (!serializedObject.isEditingMultipleObjects)
                Undo.undoRedoEvent += HandleUndo;
        }

        void UnregisterListeners()
        {
            //Ensures that the ClipList and the Serialized copy of the clip remain in sync when an Undo/Redo is performed.
            if (!serializedObject.isEditingMultipleObjects)
                Undo.undoRedoEvent -= HandleUndo;
        }

        void HandleUndo(in UndoRedoInfo info)
        {
            //Update animations serialization in-case something has changed
            m_ClipAnimations.serializedObject.UpdateIfRequiredOrScript();

            //Reset the cache to the serialized values
            DeserializeClips();
        }

        void DeserializeClips()
        {
            //Clear the clip editors
            DestroyEditorsAndData();

            //Reload the clips
            m_ClipAnimations = serializedObject.FindProperty("m_ClipAnimations");
            m_AnimationType = serializedObject.FindProperty("m_AnimationType");
            m_DefaultClipsSerializedObject = null;
            if (m_ClipAnimations.arraySize == 0)
                SetupDefaultClips();
            UpdateList();

            //Set the active clip within a valid range, -1 ONLY if there are no possible clips to select.
            int selectedClip = m_ClipList.count > 0 ? Mathf.Clamp(m_ClipList.index, 0, m_ClipList.count) : -1;
            SelectClip(selectedClip);
        }
    }
}
