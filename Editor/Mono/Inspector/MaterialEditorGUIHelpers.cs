// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using UnityEngine;
using UnityEngine.Rendering;
using System;
using System.Collections.Generic;

namespace UnityEditor
{
    public partial class MaterialEditor
    {
        public const int kMiniTextureFieldLabelIndentLevel = 2;
        const float kSpaceBetweenFlexibleAreaAndField = 5f;
        const float kQueuePopupWidth = 100f;
        const float kCustomQueuePopupWidth = kQueuePopupWidth + 15f;

        private bool isPrefabAsset
        {
            get
            {
                if (m_SerializedObject == null || m_SerializedObject.targetObject == null)
                    return false;

                return PrefabUtility.IsPartOfPrefabAsset(m_SerializedObject.targetObject);
            }
        }

        // Field for editing render queue value, with an automatically calculated rect
        public void RenderQueueField()
        {
            Rect r = GetControlRectForSingleLine();
            RenderQueueField(r);
        }

        // Field for editing render queue value, with an explicit rect
        public void RenderQueueField(Rect r)
        {
            BeginProperty(r, MaterialSerializedProperty.CustomRenderQueue, targets);

            var mat = targets[0] as Material;
            int curRawQueue = mat.rawRenderQueue;
            int curDisplayQueue = mat.renderQueue; // this gets final queue value used for rendering, taking shader's queue into account

            // Figure out if we're using one of common queues, or a custom one
            GUIContent[] queueNames = null;
            int[] queueValues = null;
            float labelWidth;
            // If we use queue value that is not available, lets switch to the custom one
            bool useCustomQueue = Array.IndexOf(Styles.queueValues, curRawQueue) < 0;
            if (useCustomQueue)
            {
                // It is a big chance that we already have this custom queue value available
                bool updateNewCustomQueueValue = Array.IndexOf(Styles.customQueueNames, curRawQueue) < 0;
                if (updateNewCustomQueueValue)
                {
                    int targetQueueIndex = CalculateClosestQueueIndexToValue(curRawQueue);
                    string targetQueueName = Styles.queueNames[targetQueueIndex].text;
                    int targetQueueValueOverflow = curRawQueue - Styles.queueValues[targetQueueIndex];

                    string newQueueName = string.Format(
                        targetQueueValueOverflow > 0 ? "{0}+{1}" : "{0}{1}",
                        targetQueueName,
                        targetQueueValueOverflow);
                    Styles.customQueueNames[Styles.kCustomQueueIndex].text = newQueueName;
                    Styles.customQueueValues[Styles.kCustomQueueIndex] = curRawQueue;
                }

                queueNames = Styles.customQueueNames;
                queueValues = Styles.customQueueValues;
                labelWidth = kCustomQueuePopupWidth;
            }
            else
            {
                queueNames = Styles.queueNames;
                queueValues = Styles.queueValues;
                labelWidth = kQueuePopupWidth;
            }

            // We want the custom queue number field to line up with thumbnails & other value fields
            // (on the right side), and common queues popup to be on the left of that.
            float oldLabelWidth = EditorGUIUtility.labelWidth;
            float oldFieldWidth = EditorGUIUtility.fieldWidth;
            SetDefaultGUIWidths();
            EditorGUIUtility.labelWidth -= labelWidth;
            Rect popupRect = r;
            popupRect.width -= EditorGUIUtility.fieldWidth + 2;
            Rect numberRect = r;
            numberRect.xMin = numberRect.xMax - EditorGUIUtility.fieldWidth;
            numberRect.height = EditorGUI.kSingleLineHeight;

            // Queues popup
            int curPopupValue = curRawQueue;
            int newPopupValue = EditorGUI.IntPopup(popupRect, Styles.queueLabel, curRawQueue, queueNames, queueValues);

            // Custom queue field
            int newDisplayQueue = EditorGUI.DelayedIntField(numberRect, curDisplayQueue);

            // If popup or custom field changed, set the new queue
            if (curPopupValue != newPopupValue || curDisplayQueue != newDisplayQueue)
            {
                RegisterPropertyChangeUndo("Render Queue");
                // Take the value from the number field,
                int newQueue = newDisplayQueue;
                // But if it's the popup that was changed
                if (newPopupValue != curPopupValue)
                    newQueue = newPopupValue;
                newQueue = Mathf.Clamp(newQueue, -1, 5000); // clamp to valid queue ranges
                // Change the material queues
                foreach (var m in targets)
                {
                    ((Material)m).renderQueue = newQueue;
                }
            }

            EditorGUIUtility.labelWidth = oldLabelWidth;
            EditorGUIUtility.fieldWidth = oldFieldWidth;

            EndProperty();
        }

        public bool EnableInstancingField()
        {
            if (!ShaderUtil.HasInstancing(m_Shader))
                return false;
            Rect r = GetControlRectForSingleLine();
            EnableInstancingField(r);
            return true;
        }

        public void EnableInstancingField(Rect r)
        {
            BeginProperty(r, MaterialSerializedProperty.EnableInstancingVariants, targets);

            using (var scope = new EditorGUI.ChangeCheckScope())
            {
                bool enableInstancing = EditorGUI.Toggle(r, Styles.enableInstancingLabel, (targets[0] as Material).enableInstancing);
                if (scope.changed)
                {
                    foreach (Material material in targets)
                        material.enableInstancing = enableInstancing;
                }
            }

            EndProperty();
        }

        public bool IsInstancingEnabled()
        {
            return ShaderUtil.HasInstancing(m_Shader) && (targets[0] as Material).enableInstancing;
        }

        public bool DoubleSidedGIField()
        {
            Rect r = GetControlRectForSingleLine();

            BeginProperty(r, MaterialSerializedProperty.DoubleSidedGI, targets);

            EditorGUI.BeginChangeCheck();
            bool doubleSidedGI = EditorGUI.Toggle(r, Styles.doubleSidedGILabel, (targets[0] as Material).doubleSidedGI);
            if (EditorGUI.EndChangeCheck())
            {
                foreach (Material material in targets)
                    material.doubleSidedGI = doubleSidedGI;
            }

            EndProperty();

            return true;
        }

        private int CalculateClosestQueueIndexToValue(int requestedValue)
        {
            int bestCloseByDiff = int.MaxValue;
            int result = 1;
            for (int i = 1; i < Styles.queueValues.Length; i++)
            {
                int queueValue = Styles.queueValues[i];
                int closeByDiff = Mathf.Abs(queueValue - requestedValue);
                if (closeByDiff < bestCloseByDiff)
                {
                    result = i;
                    bestCloseByDiff = closeByDiff;
                }
            }
            return result;
        }

        public Rect TexturePropertySingleLine(GUIContent label, MaterialProperty textureProp)
        {
            return TexturePropertySingleLine(label, textureProp, null, null);
        }

        public Rect TexturePropertySingleLine(GUIContent label, MaterialProperty textureProp, MaterialProperty extraProperty1)
        {
            return TexturePropertySingleLine(label, textureProp, extraProperty1, null);
        }

        // Mini texture slot, with two extra controls on the same line (allocates rect in GUILayout)
        // Have up to 3 controls on one line
        public Rect TexturePropertySingleLine(GUIContent label, MaterialProperty textureProp, MaterialProperty extraProperty1, MaterialProperty extraProperty2)
        {
            Rect r = GetControlRectForSingleLine();

            bool hasExtraProp = !(extraProperty1 == null && extraProperty2 == null);
            if (hasExtraProp) BeginProperty(r, textureProp);
            if (extraProperty1 != null) BeginProperty(r, extraProperty1);
            if (extraProperty2 != null) BeginProperty(r, extraProperty2);

            TexturePropertyMiniThumbnail(r, textureProp, label.text, label.tooltip);

            // No extra properties: early out
            if (!hasExtraProp)
                return r;

            // Temporarily reset the indent level as it was already used earlier to compute the positions of the layout items. See issue 946082.
            int oldIndentLevel = EditorGUI.indentLevel;

            EditorGUI.indentLevel = 0;

            // One extra property
            if (extraProperty1 == null || extraProperty2 == null)
            {
                var prop = extraProperty1 ?? extraProperty2;
                ExtraPropertyAfterTexture(GetRectAfterLabelWidth(r), prop, false);
            }
            else // Two extra properties
            {
                if (extraProperty1.propertyType == ShaderPropertyType.Color)
                {
                    ExtraPropertyAfterTexture(GetFlexibleRectBetweenFieldAndRightEdge(r), extraProperty2);
                    ExtraPropertyAfterTexture(GetLeftAlignedFieldRect(r), extraProperty1);
                }
                else
                {
                    ExtraPropertyAfterTexture(GetRightAlignedFieldRect(r), extraProperty2);
                    ExtraPropertyAfterTexture(GetFlexibleRectBetweenLabelAndField(r), extraProperty1);
                }
            }
            // Restore the indent level
            EditorGUI.indentLevel = oldIndentLevel;

            if (extraProperty2 != null) EndProperty();
            if (extraProperty1 != null) EndProperty();
            if (hasExtraProp) EndProperty();
            return r;
        }

        [Obsolete("Use TexturePropertyWithHDRColor(GUIContent label, MaterialProperty textureProp, MaterialProperty colorProperty, bool showAlpha), true")]
        public Rect TexturePropertyWithHDRColor(
            GUIContent label, MaterialProperty textureProp, MaterialProperty colorProperty, ColorPickerHDRConfig hdrConfig, bool showAlpha
        )
        {
            return TexturePropertyWithHDRColor(label, textureProp, colorProperty, showAlpha);
        }

        public Rect TexturePropertyWithHDRColor(GUIContent label, MaterialProperty textureProp, MaterialProperty colorProperty, bool showAlpha)
        {
            Rect r = GetControlRectForSingleLine();

            bool isColorProperty = colorProperty.propertyType == ShaderPropertyType.Color;
            if (isColorProperty)
            {
                BeginProperty(r, textureProp);
                BeginProperty(r, colorProperty);
            }

            TexturePropertyMiniThumbnail(r, textureProp, label.text, label.tooltip);

            if (!isColorProperty)
            {
                Debug.LogError("Assuming ShaderPropertyType.Color (was " + colorProperty.propertyType + ")");
                return r;
            }

            // Temporarily reset the indent level. See issue 946082.
            int oldIndentLevel = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            BeginAnimatedCheck(r, colorProperty);

            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = colorProperty.hasMixedValue;
            Color newValue = EditorGUI.ColorField(GetRectAfterLabelWidth(r), GUIContent.none, colorProperty.colorValue, true, showAlpha, true);
            EditorGUI.showMixedValue = false;
            if (EditorGUI.EndChangeCheck())
                colorProperty.colorValue = newValue;

            EndAnimatedCheck();

            // Restore the indent level
            EditorGUI.indentLevel = oldIndentLevel;

            if (isColorProperty)
            {
                EndProperty();
                EndProperty();
            }

            return r;
        }

        public Rect TexturePropertyTwoLines(GUIContent label, MaterialProperty textureProp, MaterialProperty extraProperty1, GUIContent label2, MaterialProperty extraProperty2)
        {
            // If not using the second extra property then use the single line version as
            // the first extra property is always inlined with the the texture slot
            if (extraProperty2 == null)
            {
                return TexturePropertySingleLine(label, textureProp, extraProperty1);
            }

            Rect r = GetControlRectForSingleLine();

            BeginProperty(r, textureProp);
            BeginProperty(r, extraProperty1);

            TexturePropertyMiniThumbnail(r, textureProp, label.text, label.tooltip);

            // Temporarily reset the indent level. See issue 946082.
            int oldIndentLevel = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            // First extra control on the same line as the texture
            Rect r1 = GetRectAfterLabelWidth(r);
            if (extraProperty1.propertyType == ShaderPropertyType.Color)
                r1 = GetLeftAlignedFieldRect(r);
            ExtraPropertyAfterTexture(r1, extraProperty1);

            EndProperty();
            EndProperty();

            // New line for extraProperty2
            Rect r2 = GetControlRectForSingleLine();
            ShaderProperty(r2, extraProperty2, label2.text, MaterialEditor.kMiniTextureFieldLabelIndentLevel + 1);

            // Restore the indent level
            EditorGUI.indentLevel = oldIndentLevel;

            // Return total rect
            r.height += r2.height;
            return r;
        }

        Rect GetControlRectForSingleLine()
        {
            const float extraSpacing = 2f; // The shader properties needs a little more vertical spacing due to the mini texture field (looks cramped without)
            return EditorGUILayout.GetControlRect(true, EditorGUI.kSingleLineHeight + extraSpacing, EditorStyles.layerMaskField);
        }

        void ExtraPropertyAfterTexture(Rect r, MaterialProperty property, bool adjustLabelWidth = true)
        {
            if (adjustLabelWidth && (property.propertyType == ShaderPropertyType.Float || property.propertyType == ShaderPropertyType.Color) && r.width > EditorGUIUtility.fieldWidth)
            {
                // We want color fields and float fields to have same width as EditorGUIUtility.fieldWidth
                // so controls aligns vertically.
                // This also makes us able to have a draggable area in front of the float fields. We therefore ensures
                // the property has a label (here we use a whitespace) and adjust label width.
                float oldLabelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = r.width - EditorGUIUtility.fieldWidth;
                ShaderProperty(r, property, " ");
                EditorGUIUtility.labelWidth = oldLabelWidth;
                return;
            }

            ShaderProperty(r, property, string.Empty);
        }

        static public Rect GetRightAlignedFieldRect(Rect r)
        {
            return new Rect(r.xMax - EditorGUIUtility.fieldWidth, r.y, EditorGUIUtility.fieldWidth, EditorGUIUtility.singleLineHeight);
        }

        static public Rect GetLeftAlignedFieldRect(Rect r)
        {
            return new Rect(r.x + EditorGUIUtility.labelWidth, r.y, EditorGUIUtility.fieldWidth, EditorGUIUtility.singleLineHeight);
        }

        static public Rect GetFlexibleRectBetweenLabelAndField(Rect r)
        {
            return new Rect(r.x + EditorGUIUtility.labelWidth, r.y, r.width - EditorGUIUtility.labelWidth - EditorGUIUtility.fieldWidth - kSpaceBetweenFlexibleAreaAndField, EditorGUIUtility.singleLineHeight);
        }

        static public Rect GetFlexibleRectBetweenFieldAndRightEdge(Rect r)
        {
            Rect r2 = GetRectAfterLabelWidth(r);
            r2.xMin += EditorGUIUtility.fieldWidth + kSpaceBetweenFlexibleAreaAndField;
            return r2;
        }

        static public Rect GetRectAfterLabelWidth(Rect r)
        {
            return new Rect(r.x + EditorGUIUtility.labelWidth, r.y, r.width - EditorGUIUtility.labelWidth, EditorGUIUtility.singleLineHeight);
        }

        static internal System.Type GetTextureTypeFromDimension(TextureDimension dim)
        {
            switch (dim)
            {
                case TextureDimension.Tex2D: return typeof(Texture); // common use case is RenderTextures too, so return base class
                case TextureDimension.Cube: return typeof(Cubemap);
                case TextureDimension.Tex3D: return typeof(Texture3D);
                case TextureDimension.Tex2DArray: return typeof(Texture2DArray);
                case TextureDimension.CubeArray: return typeof(CubemapArray);
                case TextureDimension.Any: return typeof(Texture);
                default: return null; // Unknown, None etc.
            }
        }
    }
} // namespace UnityEditor
