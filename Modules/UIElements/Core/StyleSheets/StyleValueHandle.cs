// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System;
using UnityEngine.Bindings;

namespace UnityEngine.UIElements
{
    [Serializable]
    [VisibleToOtherModules("UnityEditor.UIBuilderModule")]
    internal struct StyleValueHandle : IEquatable<StyleValueHandle>
    {
        [SerializeField]
        StyleValueType m_ValueType;

        public StyleValueType valueType
        {
            get
            {
                return m_ValueType;
            }
            [VisibleToOtherModules("UnityEditor.UIBuilderModule")]
            internal set
            {
                m_ValueType = value;
            }
        }

        // Which index to read from in the value array for the corresponding type
        [VisibleToOtherModules("UnityEditor.UIBuilderModule")]
        [SerializeField]
        internal int valueIndex;

        [VisibleToOtherModules("UnityEditor.UIBuilderModule")]
        internal StyleValueHandle(int valueIndex, StyleValueType valueType)
        {
            this.valueIndex = valueIndex;
            m_ValueType = valueType;
        }

        public bool IsVarFunction()
        {
            return valueType == StyleValueType.Function && (StyleValueFunction)valueIndex == StyleValueFunction.Var;
        }

        public bool Equals(StyleValueHandle other)
        {
            return m_ValueType == other.m_ValueType && valueIndex == other.valueIndex;
        }

        public static bool operator ==(StyleValueHandle lhs, StyleValueHandle rhs)
        {
            return lhs.Equals(rhs);
        }

        public static bool operator !=(StyleValueHandle lhs, StyleValueHandle rhs)
        {
            return !(lhs == rhs);
        }

        public override bool Equals(object obj)
        {
            return obj is StyleValueHandle other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine((int) m_ValueType, valueIndex);
        }
    }
}
