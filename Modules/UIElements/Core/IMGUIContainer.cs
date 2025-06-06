// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System;
using System.Collections.Generic;
using Unity.Profiling;
using Unity.Properties;
using UnityEngine.UIElements.Experimental;

namespace UnityEngine.UIElements
{
    /// <summary>
    /// Element that draws IMGUI content. For more information, refer to [[wiki:UIE-uxml-element-IMGUIContainer|UXML element IMGUIContainer]].
    /// </summary>
    public class IMGUIContainer : VisualElement, IDisposable
    {
        internal static readonly BindingId cullingEnabledProperty = nameof(cullingEnabled);
        internal static readonly BindingId contextTypeProperty = nameof(contextType);

        [UnityEngine.Internal.ExcludeFromDocs, Serializable]
        public new class UxmlSerializedData : VisualElement.UxmlSerializedData
        {
            public override object CreateInstance() => new IMGUIContainer();
        }

        /// <summary>
        /// Instantiates an <see cref="IMGUIContainer"/> using the data read from a UXML file.
        /// </summary>
        [Obsolete("UxmlFactory is deprecated and will be removed. Use UxmlElementAttribute instead.", false)]
        public new class UxmlFactory : UxmlFactory<IMGUIContainer, UxmlTraits> {}

        /// <summary>
        /// Defines <see cref="UxmlTraits"/> for the <see cref="IMGUIContainer"/>.
        /// </summary>
        [Obsolete("UxmlTraits is deprecated and will be removed. Use UxmlElementAttribute instead.", false)]
        public new class UxmlTraits : VisualElement.UxmlTraits
        {
            /// <summary>
            /// Constructor.
            /// </summary>
            public UxmlTraits()
            {
                focusIndex.defaultValue = 0;
                focusable.defaultValue = true;
            }

            /// <summary>
            /// Returns an empty enumerable, as IMGUIContainer cannot have VisualElement children.
            /// </summary>
            public override IEnumerable<UxmlChildElementDescription> uxmlChildElementsDescription
            {
                get { yield break; }
            }
        }

        // Set this delegate to have your IMGUI code execute inside the container
        private Action m_OnGUIHandler;
        /// <summary>
        /// The function that's called to render and handle IMGUI events.
        /// </summary>
        /// <remarks>
        /// This is assigned to onGUIHandler and is similar to <see cref="MonoBehaviour.OnGUI"/>.
        /// </remarks>
        public Action onGUIHandler
        {
            get { return m_OnGUIHandler; }
            set
            {
                if (m_OnGUIHandler != value)
                {
                    m_OnGUIHandler = value;
                    IncrementVersion(VersionChangeType.Layout);
                    IncrementVersion(VersionChangeType.Repaint);
                }
            }
        }

        // If needed, an IMGUIContainer will allocate native state via this utility object to store control IDs
        ObjectGUIState m_ObjectGUIState;

        internal ObjectGUIState guiState
        {
            get
            {
                Debug.Assert(!useOwnerObjectGUIState, "!useOwnerObjectGUIState");
                if (m_ObjectGUIState == null)
                {
                    m_ObjectGUIState = new ObjectGUIState();
                }
                return m_ObjectGUIState;
            }
        }

        // This is not nice but needed until we properly remove the dependency on GUIView's own ObjectGUIState
        // At least this implementation is not needed for users, only for containers created to wrap each GUIView
        internal bool useOwnerObjectGUIState;
        internal Rect lastWorldClip { get; set; }

        // If true, skip OnGUI() calls when outside the viewport
        private bool m_CullingEnabled = false;
        // If true, the IMGUIContainer received Focus through delgation
        private bool m_IsFocusDelegated = false;
        /// <summary>
        /// When this property is set to true, <see cref="onGUIHandler"/> is not called when the Element is outside the viewport.
        /// </summary>
        [CreateProperty]
        public bool cullingEnabled
        {
            get { return m_CullingEnabled; }
            set
            {
                if (m_CullingEnabled == value)
                    return;
                m_CullingEnabled = value;
                IncrementVersion(VersionChangeType.Repaint);
                NotifyPropertyChanged(cullingEnabledProperty);
            }
        }

        private bool m_RefreshCachedLayout = true;
        private GUILayoutUtility.LayoutCache m_Cache = null;
        private GUILayoutUtility.LayoutCache cache
        {
            get
            {
                if (m_Cache == null)
                    m_Cache = new GUILayoutUtility.LayoutCache();
                return m_Cache;
            }
        }

        // We cache the clipping rect and transform during regular painting so that we can reuse them
        // during the DoMeasure call to DoOnGUI(). It's still important to not
        // pass Rect.zero for the clipping rect as this eventually sets the
        // global GUIClip.visibleRect which IMGUI code could be using to influence
        // size. See case 1111923 and 1158089.
        private Rect m_CachedClippingRect = Rect.zero;
        private Matrix4x4 m_CachedTransform = Matrix4x4.identity;

        private float layoutMeasuredWidth
        {
            get
            {
                return Mathf.Ceil(cache.topLevel.maxWidth);
            }
        }

        private float layoutMeasuredHeight
        {
            get
            {
                return Mathf.Ceil(cache.topLevel.maxHeight);
            }
        }

        private ContextType m_ContextType;

        /// <summary>
        /// ContextType of this IMGUIContainer. Currently only supports ContextType.Editor.
        /// </summary>
        [CreateProperty]
        public ContextType contextType
        {
            get => m_ContextType;
            set
            {
                if (m_ContextType == value)
                    return;
                m_ContextType = value;
                NotifyPropertyChanged(contextTypeProperty);
            }
        }

        // The following 2 flags indicate the following :
        // 1) lostFocus : a blur event occurred and we need to make sure the actual keyboard focus from IMGUI is really un-focused
        bool lostFocus = false;
        // 2) receivedFocus : a Focus event occurred and we need to focus the actual IMGUIContainer as being THE element focused.
        bool receivedFocus = false;
        FocusChangeDirection focusChangeDirection = FocusChangeDirection.unspecified;
        bool hasFocusableControls = false;

        int newKeyboardFocusControlID = 0;

        internal bool focusOnlyIfHasFocusableControls { get; set; } = true;

        public override bool canGrabFocus => focusOnlyIfHasFocusableControls ? hasFocusableControls && base.canGrabFocus : base.canGrabFocus;

        /// <summary>
        /// USS class name of elements of this type.
        /// </summary>
        public static readonly string ussClassName = "unity-imgui-container";

        internal static readonly string ussFoldoutChildDepthClassName = $"{Foldout.ussClassName}__{ussClassName}--depth-";
        internal static readonly List<string> ussFoldoutChildDepthClassNames;

        internal struct UITKScope : IDisposable { private bool wasUITK; public UITKScope() { wasUITK = GUIUtility.isUITK; GUIUtility.isUITK = true; } public void Dispose() { GUIUtility.isUITK = wasUITK; } }
        internal struct NotUITKScope : IDisposable { private bool wasUITK; public NotUITKScope() { wasUITK = GUIUtility.isUITK; GUIUtility.isUITK = false; } public void Dispose() { GUIUtility.isUITK = wasUITK; } }

        static IMGUIContainer()
        {
            ussFoldoutChildDepthClassNames = new List<string>(Foldout.ussFoldoutMaxDepth + 1);
            for (int i = 0; i <= Foldout.ussFoldoutMaxDepth; i++)
            {
                ussFoldoutChildDepthClassNames.Add(ussFoldoutChildDepthClassName + i);
            }
            ussFoldoutChildDepthClassNames.Add(ussFoldoutChildDepthClassName + "max");
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        public IMGUIContainer()
            : this(null)
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="onGUIHandler">The function assigned to <see cref="onGUIHandler"/>.</param>
        public IMGUIContainer(Action onGUIHandler)
        {
            isIMGUIContainer = true;

            AddToClassList(ussClassName);

            this.onGUIHandler = onGUIHandler;
            contextType = ContextType.Editor;
            focusable = true;

            requireMeasureFunction = true;
            generateVisualContent += OnGenerateVisualContent;
        }

        private void OnGenerateVisualContent(MeshGenerationContext mgc)
        {
            if (elementPanel is BaseRuntimePanel { drawsInCameras: true })
            {
                Debug.LogError($"{nameof(IMGUIContainer)} cannot be used in a panel drawn by cameras.");
                return;
            }

            lastWorldClip = elementPanel.repaintData.currentWorldClip;

            // Access to the painter is internal and is not exposed to public
            // The IStylePainter is kept as an interface rather than a concrete class for now to support tests
            mgc.entryRecorder.DrawImmediate(mgc.parentEntry, DoIMGUIRepaint, cullingEnabled);
        }

        // global GUI values.
        // container saves and restores them before doing his thing
        private struct GUIGlobals
        {
            public Matrix4x4 matrix;
            public Color color;
            public Color contentColor;
            public Color backgroundColor;
            public bool enabled;
            public bool changed;
            public int displayIndex;
            public float pixelsPerPoint;
        }

        private GUIGlobals m_GUIGlobals;

        private void SaveGlobals()
        {
            m_GUIGlobals.matrix = GUI.matrix;
            m_GUIGlobals.color = GUI.color;
            m_GUIGlobals.contentColor = GUI.contentColor;
            m_GUIGlobals.backgroundColor = GUI.backgroundColor;
            m_GUIGlobals.enabled = GUI.enabled;
            m_GUIGlobals.changed = GUI.changed;
            if (Event.current != null)
            {
                m_GUIGlobals.displayIndex = Event.current.displayIndex;
            }
            m_GUIGlobals.pixelsPerPoint = GUIUtility.pixelsPerPoint;
        }

        private void RestoreGlobals()
        {
            GUI.matrix = m_GUIGlobals.matrix;
            GUI.color = m_GUIGlobals.color;
            GUI.contentColor = m_GUIGlobals.contentColor;
            GUI.backgroundColor = m_GUIGlobals.backgroundColor;
            GUI.enabled = m_GUIGlobals.enabled;
            GUI.changed = m_GUIGlobals.changed;
            if (Event.current != null)
            {
                Event.current.displayIndex = m_GUIGlobals.displayIndex;
            }
            GUIUtility.pixelsPerPoint = m_GUIGlobals.pixelsPerPoint;
        }

        static readonly ProfilerMarker k_OnGUIMarker = new ProfilerMarker("OnGUI");

        private void DoOnGUI(Event evt, Matrix4x4 parentTransform, Rect clippingRect, bool isComputingLayout, Rect layoutSize, Action onGUIHandler, bool canAffectFocus = true)
        {
            // Extra checks are needed here because client code might have changed the IMGUIContainer
            // since we enter HandleIMGUIEvent()
            if (onGUIHandler == null
                || panel == null)
            {
                return;
            }

            // Save the GUIClip count to make sanity checks after calling the OnGUI handler
            int guiClipCount = GUIClip.Internal_GetCount();
            int guiDepthBeforeOnGUI = GUIUtility.guiDepth;

            SaveGlobals();

            // Save a copy of the container size.
            var previousMeasuredWidth = layoutMeasuredWidth;
            var previousMeasuredHeight = layoutMeasuredHeight;

            UIElementsUtility.BeginContainerGUI(cache, evt, this);

            // For the IMGUI, we need to update the GUI.color with the actual play mode tint ...
            // In fact, this is taken from EditorGUIUtility.ResetGUIState().
            // Here, the play mode tint is either white (no tint, or not in play mode) or the right color (if in play mode)
            GUI.color = playModeTintColor;
            // From now on, Event.current is either evt or a copy of evt.
            // Since Event.current may change while being processed, we do not rely on evt below but use Event.current instead.


            GUIUtility.pixelsPerPoint = scaledPixelsPerPoint;


            if (Event.current.type != EventType.Layout)
            {
                if (lostFocus)
                {
                    if (focusController != null)
                    {
                        // We dont want to clear the GUIUtility.keyboardControl if another IMGUIContainer
                        // just set it in the if (receivedFocus) block below. So we only clear it if own it.
                        if (GUIUtility.OwnsId(GUIUtility.keyboardControl))
                        {
                            GUIUtility.keyboardControl = 0;
                            focusController.imguiKeyboardControl = 0;
                        }
                    }
                    lostFocus = false;
                }

                if (receivedFocus)
                {
                    if (hasFocusableControls)
                    {
                        if (focusChangeDirection != FocusChangeDirection.unspecified && focusChangeDirection != FocusChangeDirection.none)
                        {
                            // Consume the Tab Event as UITK already used it.
                            if (Event.current.type == EventType.KeyDown && Event.current.character is '\t' or (char)25)
                                Event.current.Use();

                            // We assume we are using the VisualElementFocusRing.
                            if (focusChangeDirection == VisualElementFocusChangeDirection.left)
                            {
                                GUIUtility.SetKeyboardControlToLastControlId();
                            }
                            else if (focusChangeDirection == VisualElementFocusChangeDirection.right)
                            {
                                GUIUtility.SetKeyboardControlToFirstControlId();
                            }
                        }
                        else if (GUIUtility.keyboardControl == 0 && m_IsFocusDelegated)
                        {
                            // Since GUIUtility.keyboardControl == 0, we got focused in some other way than by clicking inside us
                            // (for example it could be by clicking in an element that delegates focus to us).
                            // Give GUIUtility.keyboardControl to our first control.
                            GUIUtility.SetKeyboardControlToFirstControlId();
                        }
                    }

                    if (focusController != null)
                    {
                        if (focusController.imguiKeyboardControl != GUIUtility.keyboardControl && focusChangeDirection != FocusChangeDirection.unspecified)
                        {
                            newKeyboardFocusControlID = GUIUtility.keyboardControl;
                        }

                        focusController.imguiKeyboardControl = GUIUtility.keyboardControl;
                    }

                    receivedFocus = false;
                    focusChangeDirection = FocusChangeDirection.unspecified;
                }
                // We intentionally don't send the NewKeyboardFocus command here since it creates an issue with the AutomatedWindow
                // newKeyboardFocusControlID = GUIUtility.keyboardControl;
            }

            EventType originalEventType = Event.current.type;

            bool isExitGUIException = false;
            bool restoreContainerGUIDepth = true;
            int guiClipFinalCount = 0;

            try
            {
                using (new GUIClip.ParentClipScope(parentTransform, clippingRect))
                {
                    using (k_OnGUIMarker.Auto())
                    {
                        onGUIHandler();
                    }
                }
            }
            catch (Exception exception)
            {
                // only for layout events: we always intercept any exceptions to not interrupt event processing
                if (originalEventType == EventType.Layout)
                {
                    isExitGUIException = GUIUtility.IsExitGUIException(exception);
                    if (!isExitGUIException)
                    {
                        Debug.LogException(exception);
                    }
                }
                else
                {
                    // UUM-47254: don't restore ContainerGUI depth if we are in an ExitGUI process that came from a
                    // lower layer of GUIView::OnInputEvent. Depth restoration is already handled from there.
                    if (guiDepthBeforeOnGUI > 0)
                        restoreContainerGUIDepth = false;

                    // rethrow event if not in layout
                    throw;
                }
            }
            finally
            {
                if (Event.current.type != EventType.Layout && canAffectFocus)
                {
                    bool alreadyUsed = Event.current.type == EventType.Used;
                    int currentKeyboardFocus = GUIUtility.keyboardControl;
                    int result = GUIUtility.CheckForTabEvent(Event.current);
                    if (focusController != null)
                    {
                        if (result < 0 && !alreadyUsed)
                        {
                            // If CheckForTabEvent returns -1 or -2, we have reach the end/beginning of its control list.
                            // We should switch the focus to the next VisualElement.
                            Focusable currentFocusedElement = focusController.GetLeafFocusedElement();
                            Focusable nextFocusedElement = focusController.FocusNextInDirection(this, result == -1
                                ? VisualElementFocusChangeDirection.right
                                : VisualElementFocusChangeDirection.left);

                            if (currentFocusedElement == this)
                            {
                                if (nextFocusedElement == this)
                                {
                                    // We will still have the focus. We should cycle around our controls.
                                    if (result == -2)
                                    {
                                        GUIUtility.SetKeyboardControlToLastControlId();
                                    }
                                    else if (result == -1)
                                    {
                                        GUIUtility.SetKeyboardControlToFirstControlId();
                                    }

                                    newKeyboardFocusControlID = GUIUtility.keyboardControl;
                                    focusController.imguiKeyboardControl = GUIUtility.keyboardControl;
                                }
                                else
                                {
                                    // We will lose the focus. Set the focused element ID to 0 until next
                                    // IMGUIContainer have a chance to set it to its own control.
                                    // Doing this will ensure we draw ourselves without any focused control.
                                    GUIUtility.keyboardControl = 0;
                                    focusController.imguiKeyboardControl = 0;
                                }
                            }
                        }
                        else if (result > 0 && !alreadyUsed)
                        {
                            // A positive result indicates that the focused control has changed to one of our elements; result holds the control id.
                            focusController.imguiKeyboardControl = GUIUtility.keyboardControl;
                            newKeyboardFocusControlID = GUIUtility.keyboardControl;
                        }
                        else if (result == 0)
                        {
                            // This means the event is not a tab. Synchronize our focus info with IMGUI.

                            if (originalEventType == EventType.MouseDown && !focusOnlyIfHasFocusableControls)
                            {
                                focusController.SyncIMGUIFocus(GUIUtility.keyboardControl, this, true);
                            }
                            else if ((currentKeyboardFocus != GUIUtility.keyboardControl) || (originalEventType == EventType.MouseDown))
                            {
                                focusController.SyncIMGUIFocus(GUIUtility.keyboardControl, this, false);
                            }
                            else if (GUIUtility.keyboardControl != focusController.imguiKeyboardControl)
                            {
                                // Here we want to resynchronize our internal state ...
                                newKeyboardFocusControlID = GUIUtility.keyboardControl;

                                if (focusController.GetLeafFocusedElement() == this)
                                {
                                    // In this case, the focused element is the right one in the Focus Controller... we are just updating the internal imguiKeyboardControl
                                    focusController.imguiKeyboardControl = GUIUtility.keyboardControl;
                                }
                                else
                                {
                                    // In this case, the focused element is NOT the right one in the Focus Controller... we also have to refocus...
                                    focusController.SyncIMGUIFocus(GUIUtility.keyboardControl, this, false);
                                }
                            }
                        }
                    }
                    // Cache the fact that we have focusable controls or not.
                    hasFocusableControls = GUIUtility.HasFocusableControls();
                }

                if (restoreContainerGUIDepth)
                {
                    // This will copy Event.current into evt. End the container by now since the container
                    // should end at this point no matter an exception occured or not.
                    // Not ending the container will make the GUIDepth off by 1.
                    UIElementsUtility.EndContainerGUI(evt, layoutSize);

                    RestoreGlobals();
                }

                guiClipFinalCount = GUIClip.Internal_GetCount();

                // Clear extraneous GUIClips
                while (GUIClip.Internal_GetCount() > guiClipCount)
                    GUIClip.Internal_Pop();
            }

            // See if the container size has changed. This is to make absolutely sure the VisualElement resizes
            // if the IMGUI content resizes.
            if (evt.type == EventType.Layout &&
                (!Mathf.Approximately(previousMeasuredWidth, layoutMeasuredWidth) || !Mathf.Approximately(previousMeasuredHeight, layoutMeasuredHeight)))
            {
                if (isComputingLayout && clippingRect == Rect.zero)
                    this.schedule.Execute(() => IncrementVersion(VersionChangeType.Layout));
                else
                    IncrementVersion(VersionChangeType.Layout);
            }

            if (!isExitGUIException)
            {
                // This is the same logic as GUIClipState::EndOnGUI
                if (evt.type != EventType.Ignore && evt.type != EventType.Used)
                {
                    if (guiClipFinalCount > guiClipCount)
                        Debug.LogError("GUI Error: You are pushing more GUIClips than you are popping. Make sure they are balanced.");
                    else if (guiClipFinalCount < guiClipCount)
                        Debug.LogError("GUI Error: You are popping more GUIClips than you are pushing. Make sure they are balanced.");
                }
            }

            if (evt.type == EventType.Used)
            {
                IncrementVersion(VersionChangeType.Repaint);
            }
        }

        /// <summary>
        /// Marks layout as dirty to trigger a redraw.
        /// </summary>
        public void MarkDirtyLayout()
        {
            m_RefreshCachedLayout = true;
            IncrementVersion(VersionChangeType.Layout);
        }

        static readonly ProfilerMarker k_ImmediateCallbackMarker = new ProfilerMarker(nameof(IMGUIContainer));

        // This is the IStylePainterInternal.DrawImmediate callback
        private void DoIMGUIRepaint()
        {
            using (k_ImmediateCallbackMarker.Auto())
            {
                var offset = elementPanel.repaintData.currentOffset;
                m_CachedClippingRect = ComputeAAAlignedBound(worldClip, offset);
                m_CachedTransform = offset * worldTransform;

                HandleIMGUIEvent(elementPanel.repaintData.repaintEvent, m_CachedTransform, m_CachedClippingRect, onGUIHandler, true);
            }
        }

        internal bool SendEventToIMGUI(EventBase evt, bool canAffectFocus = true, bool verifyBounds = true)
        {
            if (evt is IPointerEvent)
            {
                if (evt.imguiEvent != null && evt.imguiEvent.isDirectManipulationDevice)
                {
                    bool sendPointerEvent = false;
                    EventType originalEventType = evt.imguiEvent.rawType;
                    if (evt is PointerDownEvent)
                    {
                        sendPointerEvent = true;
                        evt.imguiEvent.type = EventType.TouchDown;
                    }
                    else if (evt is PointerUpEvent)
                    {
                        sendPointerEvent = true;
                        evt.imguiEvent.type = EventType.TouchUp;
                    }
                    else if (evt is PointerMoveEvent && evt.imguiEvent.rawType == EventType.MouseDrag)
                    {
                        sendPointerEvent = true;
                        evt.imguiEvent.type = EventType.TouchMove;
                    }
                    else if (evt is PointerLeaveEvent)
                    {
                        sendPointerEvent = true;
                        evt.imguiEvent.type = EventType.TouchLeave;
                    }
                    else if (evt is PointerEnterEvent)
                    {
                        sendPointerEvent = true;
                        evt.imguiEvent.type = EventType.TouchEnter;
                    }

                    if (sendPointerEvent)
                    {
                        bool result = SendEventToIMGUIRaw(evt, canAffectFocus, verifyBounds);
                        evt.imguiEvent.type = originalEventType;
                        return result;
                    }
                }
                // If not touch then we should not handle PointerEvents on IMGUI, we will handle the MouseEvent sent right after
                return false;
            }

            return SendEventToIMGUIRaw(evt, canAffectFocus, verifyBounds);
        }

        private bool SendEventToIMGUIRaw(EventBase evt, bool canAffectFocus, bool verifyBounds)
        {
            if (verifyBounds && !VerifyBounds(evt))
                return false;

            bool result;
            using (new EventDebuggerLogIMGUICall(evt))
            {
                result = HandleIMGUIEvent(evt.imguiEvent, canAffectFocus);
            }
            return result;
        }

        private bool VerifyBounds(EventBase evt)
        {
            return IsContainerCapturingTheMouse() || !IsLocalEvent(evt) || IsEventInsideLocalWindow(evt) || IsDockAreaMouseUp(evt);
        }

        private bool IsContainerCapturingTheMouse()
        {
            return this == panel?.dispatcher?.pointerState.GetCapturingElement(PointerId.mousePointerId);
        }

        private bool IsLocalEvent(EventBase evt)
        {
            long evtType = evt.eventTypeId;
            return evtType == MouseDownEvent.TypeId() || evtType == MouseUpEvent.TypeId() ||
                evtType == MouseMoveEvent.TypeId() ||
                evtType == PointerDownEvent.TypeId() || evtType == PointerUpEvent.TypeId() ||
                evtType == PointerMoveEvent.TypeId();
        }

        private bool IsEventInsideLocalWindow(EventBase evt)
        {
            Rect clippingRect = GetCurrentClipRect();
            string pointerType = (evt as IPointerEvent)?.pointerType;
            bool isDirectManipulationDevice = (pointerType == PointerType.touch || pointerType == PointerType.pen);
            return GUIUtility.HitTest(clippingRect, evt.originalMousePosition, isDirectManipulationDevice);
        }

        private static bool IsDockAreaMouseUp(EventBase evt)
        {
            return evt.eventTypeId == MouseUpEvent.TypeId() &&
                   evt.elementTarget == evt.elementTarget?.elementPanel.rootIMGUIContainer;
        }

        internal bool HandleIMGUIEvent(Event e, bool canAffectFocus)
        {
            return HandleIMGUIEvent(e, onGUIHandler, canAffectFocus);
        }

        internal bool HandleIMGUIEvent(Event e, Action onGUIHandler, bool canAffectFocus)
        {
            GetCurrentTransformAndClip(this, e, out m_CachedTransform, out m_CachedClippingRect);

            return HandleIMGUIEvent(e, m_CachedTransform, m_CachedClippingRect, onGUIHandler, canAffectFocus);
        }

        private bool HandleIMGUIEvent(Event e, Matrix4x4 worldTransform, Rect clippingRect, Action onGUIHandler, bool canAffectFocus)
        {
            if (e == null || onGUIHandler == null || elementPanel == null || elementPanel.IMGUIEventInterests.WantsEvent(e.rawType) == false)
            {
                return false;
            }

            using var scope = new NotUITKScope();

            EventType originalEventType = e.rawType;
            if (originalEventType != EventType.Layout)
            {
                if (m_RefreshCachedLayout || elementPanel.IMGUIEventInterests.WantsLayoutPass(e.rawType))
                {
                    // Only update the layout in-between repaint events.
                    e.type = EventType.Layout;
                    DoOnGUI(e, worldTransform, clippingRect, false, layout, onGUIHandler, canAffectFocus);
                    m_RefreshCachedLayout = false;
                    e.type = originalEventType;
                }
                else
                {
                    // Reuse layout cache for other events.
                    cache.ResetCursor();
                }
            }

            DoOnGUI(e, worldTransform, clippingRect, false, layout, onGUIHandler, canAffectFocus);

            if (newKeyboardFocusControlID > 0)
            {
                newKeyboardFocusControlID = 0;
                Event focusCommand = new Event
                {
                    type = EventType.ExecuteCommand,
                    commandName = EventCommandNames.NewKeyboardFocus
                };

                HandleIMGUIEvent(focusCommand, true);
            }

            if (e.rawType == EventType.Used)
            {
                return true;
            }
            else if (e.rawType == EventType.MouseUp && this.HasMouseCapture())
            {
                // This can happen if a MouseDown was caught by a different IM element but we ended up here on the
                // MouseUp event because no other element consumed it, including the one that had capture.
                // Example case: start text selection in a text field, but drag mouse all the way into another
                // part of the editor, release the mouse button.  Since the mouse up was sent to another container,
                // we end up here and that is perfectly legal (unfortunately unavoidable for now since no IMGUI control
                // used the event), but hot control might still belong to the IM text field at this point.
                // We can safely release the hot control which will release the capture as the same time.
                GUIUtility.hotControl = 0;
            }

            // If we detect that we were removed while processing this event, hi-jack the event loop to early exit
            // In IMGUI/Editor this is actually possible just by calling EditorWindow.Close() for example
            if (elementPanel == null)
            {
                GUIUtility.ExitGUI();
            }

            return false;
        }

        [EventInterest(EventInterestOptionsInternal.TriggeredByOS)]
        [EventInterest(typeof(NavigationMoveEvent), typeof(NavigationSubmitEvent), typeof(NavigationCancelEvent),
            typeof(BlurEvent), typeof(FocusEvent), typeof(DetachFromPanelEvent), typeof(AttachToPanelEvent))]
        internal override void HandleEventBubbleUpDisabled(EventBase evt)
        {
            HandleEventBubbleUp(evt);
        }

        [EventInterest(EventInterestOptionsInternal.TriggeredByOS)]
        [EventInterest(typeof(NavigationMoveEvent), typeof(NavigationSubmitEvent), typeof(NavigationCancelEvent),
            typeof(BlurEvent), typeof(FocusEvent), typeof(DetachFromPanelEvent), typeof(AttachToPanelEvent))]
        protected override void HandleEventBubbleUp(EventBase evt)
        {
            // No call to base.HandleEventBubbleUp(evt):
            // - we dont want mouse click to directly give focus to IMGUIContainer:
            //   they should be handled by IMGUI and if an IMGUI control grabs the
            //   keyboard, the IMGUIContainer will gain focus via FocusController.SyncIMGUIFocus.
            // - same thing for tabs: IMGUI should handle them.
            // - we dont want to set the PseudoState.Focus flag on IMGUIContainer.
            //   They are focusable, but only for the purpose of focusing their children.
            //
            // If IMGUIContainer is in the propagation path, it's necessarily the target because it can't be the parent
            // of any other element.

            if (evt.imguiEvent != null && SendEventToIMGUI(evt) ||
                // Prevent navigation events since IMGUI already uses KeyDown events
                evt.eventTypeId == NavigationMoveEvent.TypeId() ||
                evt.eventTypeId == NavigationSubmitEvent.TypeId() ||
                evt.eventTypeId == NavigationCancelEvent.TypeId())
            {
                evt.StopPropagation();
                focusController?.IgnoreEvent(evt);
            }

            // Here, we set flags that will be acted upon in DoOnGUI(), since we need to change IMGUI state.
            else if (evt.eventTypeId == BlurEvent.TypeId())
            {
                // A lost focus event is ... a lost focus event.
                // The specific handling of the IMGUI will be done in the DoOnGUI() above...
                lostFocus = true;

                // On lost focus, we need to repaint to remove any focused element blue borders.
                IncrementVersion(VersionChangeType.Repaint);
            }
            else if (evt.eventTypeId == FocusEvent.TypeId())
            {
                FocusEvent fe = evt as FocusEvent;
                receivedFocus = true;
                focusChangeDirection = fe.direction;
                m_IsFocusDelegated = fe.IsFocusDelegated;
            }
            else if (evt.eventTypeId == DetachFromPanelEvent.TypeId())
            {
                if (elementPanel != null)
                {
                    elementPanel.IMGUIContainersCount--;
                }
            }
            else if (evt.eventTypeId == AttachToPanelEvent.TypeId())
            {
                if (elementPanel != null)
                {
                    elementPanel.IMGUIContainersCount++;

                    // Set class names for foldout depth.
                    SetFoldoutDepthClass();
                }
            }
        }

        void SetFoldoutDepthClass()
        {
            // Remove from all the depth classes...
            for (var i = 0; i < ussFoldoutChildDepthClassNames.Count; i++)
            {
                RemoveFromClassList(ussFoldoutChildDepthClassNames[i]);
            }

            // Figure out the real depth of this actual Foldout...
            var depth = this.GetFoldoutDepth();

            if (depth == 0)
                return;

            // Add the class name corresponding to that depth
            depth = Mathf.Min(depth, ussFoldoutChildDepthClassNames.Count - 1);
            AddToClassList(ussFoldoutChildDepthClassNames[depth]);
        }

        static Event s_DefaultMeasureEvent = new Event() { type = EventType.Layout };
        static Event s_MeasureEvent = new Event() { type = EventType.Layout };
        static Event s_CurrentEvent = new Event() { type = EventType.Layout };
        protected internal override Vector2 DoMeasure(float desiredWidth, MeasureMode widthMode, float desiredHeight, MeasureMode heightMode)
        {

            float measuredWidth = float.NaN;
            float measuredHeight = float.NaN;
            using var scope = new NotUITKScope();

            bool restoreCurrentEvent = false;
            if (widthMode != MeasureMode.Exactly || heightMode != MeasureMode.Exactly)
            {
                if (Event.current != null)
                {
                    // The call to DoOnGUI below overwrite Event.current with the event pass in.
                    // If Even.current is not null we need to save it so we can restore it at the end of DoMeasure.
                    s_CurrentEvent.CopyFrom(Event.current);
                    restoreCurrentEvent = true;
                }

                s_MeasureEvent.CopyFrom(s_DefaultMeasureEvent);

                var layoutRect = layout;
                // Make sure the right width/height will be used at the final stage of the calculation
                switch (widthMode)
                {
                    case MeasureMode.Exactly:
                        layoutRect.width = desiredWidth;
                        break;
                }
                switch (heightMode)
                {
                    case MeasureMode.Exactly:
                        layoutRect.height = desiredHeight;
                        break;
                }
                // When computing layout it's important to not call GetCurrentTransformAndClip
                // because it will remove the dirty flag on the container transform which might
                // set the transform in an invalid state. That's why we have to pass
                // cached transform and clipping state here. It's still important to not
                // pass Rect.zero for the clipping rect as this eventually sets the
                // global GUIClip.visibleRect which IMGUI code could be using to influence
                // size. See case 1111923 and 1158089.
                DoOnGUI(s_MeasureEvent, m_CachedTransform, m_CachedClippingRect, true, layoutRect, onGUIHandler, true);
                measuredWidth = layoutMeasuredWidth;
                measuredHeight = layoutMeasuredHeight;

                if (restoreCurrentEvent)
                    Event.current.CopyFrom(s_CurrentEvent);
            }

            switch (widthMode)
            {
                case MeasureMode.Exactly:
                    measuredWidth = desiredWidth;
                    break;
                case MeasureMode.AtMost:
                    measuredWidth = Mathf.Min(measuredWidth, desiredWidth);
                    break;
            }

            switch (heightMode)
            {
                case MeasureMode.Exactly:
                    measuredHeight = desiredHeight;
                    break;
                case MeasureMode.AtMost:
                    measuredHeight = Mathf.Min(measuredHeight, desiredHeight);
                    break;
            }


            return new Vector2(measuredWidth, measuredHeight);
        }

        private Rect GetCurrentClipRect()
        {
            Rect clipRect = this.lastWorldClip;
            if (clipRect.width == 0.0f || clipRect.height == 0.0f)
            {
                // lastWorldClip will be empty until the first repaint occurred,
                // we fall back on the worldBound in this case.
                clipRect = this.worldBound;
            }
            return clipRect;
        }

        private static void GetCurrentTransformAndClip(IMGUIContainer container, Event evt, out Matrix4x4 transform, out Rect clipRect)
        {
            clipRect = container.GetCurrentClipRect();

            transform = container.worldTransform;
            if (evt?.rawType == EventType.Repaint
                && container.elementPanel != null)
            {
                // during repaint, we must use in case the current transform is not relative to Panel
                // this is to account for the pixel caching feature
                transform =  container.elementPanel.repaintData.currentOffset * container.worldTransform;
            }
        }

        /// <summary>
        /// Releases the native memory that this IMGUIContainer instance uses.
        /// </summary>
        public void Dispose()
        {
            // TODO there is no finalizer in this class, but it should probably be the case!
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposeManaged)
        {
            if (disposeManaged)
            {
                m_ObjectGUIState?.Dispose();
            }
        }
    }
}
