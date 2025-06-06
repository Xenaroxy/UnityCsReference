// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System;
using UnityEngine;
using UnityEditor.VersionControl;
using UnityEditorInternal;
using UnityEditorInternal.VersionControl;
using System.Collections.Generic;
using Math = System.Math;
using AssetReference = UnityEditorInternal.InternalEditorUtility.AssetReference;
using RenameOverlay = UnityEditor.RenameOverlay<int>;

namespace UnityEditor
{
    internal partial class ObjectListArea
    {
        // Enable external source(s) to draw in the project browser.
        // 'iconRect' frame for the asset icon.
        // 'guid' asset being drawn.
        internal delegate void OnAssetIconDrawDelegate(Rect iconRect, string guid, bool isListMode);
        internal static event OnAssetIconDrawDelegate postAssetIconDrawCallback = null;

        // 'drawRect' prescribed draw area after the asset label.
        // 'guid' asset being drawn.
        // return whether drawing occured (space will be redistributed if false)
        internal delegate bool OnAssetLabelDrawDelegate(Rect drawRect, string guid, bool isListMode);
        internal static event OnAssetLabelDrawDelegate postAssetLabelDrawCallback = null;

        // Asset on local disk in project
        protected class LocalGroup : Group
        {
            public class ExtraItem : BuiltinResource
            {
                public Texture2D m_Icon = null;
            }
            ExtraItem[] m_NoneList;
            public ExtraItem[] NoneList => m_NoneList;

            GUIContent m_Content = new GUIContent();

            List<int> m_DragSelection = new List<int>();                // Temp instanceID state while dragging (not serialized)
            int m_DropTargetControlID = 0;

            private List<Type> m_AssetPreviewIgnoreList = new List<Type>();
            private List<string> m_AssetExtensionsPreviewIgnoreList = new List<string>();

            // Type name if resource is the key
            Dictionary<string, BuiltinResource[]> m_BuiltinResourceMap;
            BuiltinResource[] m_CurrentBuiltinResources;
            bool m_ShowNoneItem;
            public bool ShowNone { get { return m_ShowNoneItem; } }
            public override bool NeedsRepaint { get { return false; } protected set {} }
            List<int> m_LastRenderedAssetInstanceIDs = new List<int>();
            List<int> m_LastRenderedAssetDirtyCounts = new List<int>();

            public bool m_ListMode = false;
            FilteredHierarchy m_FilteredHierarchy;
            BuiltinResource[] m_ActiveBuiltinList;

            public const int k_ListModeLeftPadding = 13;
            public const int k_ListModeLeftPaddingForSubAssets = 28;
            public const int k_ListModeVersionControlOverlayPadding = 14;
            const int k_ListModeExternalIconPadding = 6;
            const float k_IconWidth = 16f;
            const float k_SpaceBetweenIconAndText = 2f;


            public SearchFilter searchFilter { get {return m_FilteredHierarchy.searchFilter; }}
            public override bool ListMode { get { return m_ListMode; } set { m_ListMode = value; } }
            public bool HasBuiltinResources { get {  return m_CurrentBuiltinResources.Length > 0; } }

            ItemFader m_ItemFader = new ItemFader();
            private readonly Action m_OwnerRepaintAction;

            public int projectItemCount { get { return m_FilteredHierarchy.results.Length; } }

            public override int ItemCount
            {
                get
                {
                    int totalItemCount = projectItemCount + m_ActiveBuiltinList.Length;
                    int noneItem = m_ShowNoneItem ? 1 : 0;
                    int newItem = m_Owner.m_State.m_NewAssetIndexInList != -1 ? 1 : 0;
                    return totalItemCount + noneItem + newItem;
                }
            }

            public LocalGroup(ObjectListArea owner, string groupTitle, bool showNone) : base(owner, groupTitle)
            {
                m_ShowNoneItem = showNone;
                m_ListMode = false;
                InitBuiltinResources();
                ItemsWantedShown = int.MaxValue;
                m_Collapsable = false;
                m_OwnerRepaintAction = () => m_Owner.Repaint();
                InitAssetPreviewIgnoreList();
            }

            //Initialize the list with known asset types that has no Preview Image and should use icons instead like Text, Folder, Scene etc.
            //This will prevent the AssetPreview generation for asset types that has no asset previews.
            //This has been added to handle projects with a large number of assets that has no preview,
            //as preview generation for all those assets is consuming significant amount of CPU,
            //it feels like Unity is frozen for repaint event in ProjectBrowse/ObjectSelector.
            private void InitAssetPreviewIgnoreList()
            {
                m_AssetPreviewIgnoreList.Add(typeof(DefaultAsset));                                                 //DLLs, corrupted files, pdf, Folder etc.
                m_AssetPreviewIgnoreList.Add(typeof(MonoScript));                                                   //Monobehaviour scripts
                m_AssetPreviewIgnoreList.Add(typeof(SceneAsset));
                m_AssetPreviewIgnoreList.Add(typeof(AnimationClip));
                m_AssetPreviewIgnoreList.Add(typeof(Animations.AnimatorController));
                m_AssetPreviewIgnoreList.Add(typeof(TextAsset));
                m_AssetPreviewIgnoreList.Add(typeof(Shader));
                m_AssetPreviewIgnoreList.Add(typeof(LightingSettings));
                m_AssetPreviewIgnoreList.Add(typeof(LightmapParameters));

                m_AssetExtensionsPreviewIgnoreList.Add(".index");
                m_AssetExtensionsPreviewIgnoreList.Add(".vfx");
            }

            //Use this to add the specific types that needs to ignored for AssetPreview image generation.
            //External packages can use these to add support for their specific asset types.
            public void AddTypetoAssetPreviewIgnoreList(Type assetType)
            {
                if (m_AssetPreviewIgnoreList.Contains(assetType))
                    return;

                m_AssetPreviewIgnoreList.Add(assetType);
            }

            public override void UpdateAssets()
            {
                // Set up our builtin list
                if (m_FilteredHierarchy?.hierarchyType == HierarchyType.Assets)
                    m_ActiveBuiltinList = m_CurrentBuiltinResources;
                else
                    m_ActiveBuiltinList = new BuiltinResource[0];   // The Scene tab does not display builtin resources

                ItemsAvailable = m_FilteredHierarchy.results.Length + m_ActiveBuiltinList.Length;
            }

            protected override float GetHeaderHeight()
            {
                return 0f;
            }

            override protected void DrawHeader(float yOffset, bool collapsable)
            {
                if (GetHeaderHeight() > 0f)
                {
                    Rect rect = new Rect(0, GetHeaderYPosInScrollArea(yOffset), m_Owner.GetVisibleWidth(), kGroupSeparatorHeight);

                    base.DrawHeaderBackground(rect, true, Visible);

                    // Draw the group toggle
                    if (collapsable)
                    {
                        rect.x += 7;
                        bool oldVisible = Visible;
                        Visible = GUI.Toggle(new Rect(rect.x, rect.y, 14, rect.height), Visible, GUIContent.none, Styles.groupFoldout);
                        if (oldVisible ^ Visible)
                            EditorPrefs.SetBool(m_GroupSeparatorTitle, Visible);

                        rect.x += 7;
                    }

                    float usedWidth = 0f;
                    if (m_Owner.drawLocalAssetHeader != null)
                        usedWidth = m_Owner.drawLocalAssetHeader(rect) + 10f; // add space between arrow and count

                    rect.x += usedWidth;
                    rect.width -= usedWidth;
                    if (rect.width > 0)
                        base.DrawItemCount(rect);
                }
            }

            public override void UpdateHeight()
            {
                // Ensure that m_Grid is setup before calling UpdateHeight
                m_Height = GetHeaderHeight();

                if (!Visible)
                    return;

                m_Height += m_Grid.height;
            }

            bool IsCreatingAtThisIndex(int itemIdx)
            {
                return m_Owner.m_State.m_NewAssetIndexInList == itemIdx;
            }

            protected override void DrawInternal(int beginIndex, int endIndex, float yOffset)
            {
                int itemIndex = beginIndex;
                int itemCount = 0;

                FilteredHierarchy.FilterResult[] results = m_FilteredHierarchy.results;

                bool isFolderBrowsing = m_FilteredHierarchy.searchFilter.GetState() == SearchFilter.State.FolderBrowsing;

                // The seperator bar is drawn before all items
                yOffset += GetHeaderHeight();

                // 1. None item
                Rect itemRect;
                if (m_NoneList.Length > 0)
                {
                    if (beginIndex < 1)
                    {
                        itemRect = m_Grid.CalcRect(itemIndex, yOffset);
                        DrawItem(itemRect, null, m_NoneList[0], isFolderBrowsing);
                        itemIndex++;
                    }
                    itemCount++;
                }

                // 2. Project Assets
                if (!ListMode && isFolderBrowsing)
                    DrawSubAssetBackground(beginIndex, endIndex, yOffset); // only show sub asset bg when not searching i.e. folder browsing

                if (Event.current.type == EventType.Repaint)
                    ClearDirtyStateTracking();
                int resultsIdx = itemIndex - itemCount;
                while (true)
                {
                    // Insert new asset item here
                    if (IsCreatingAtThisIndex(itemIndex))
                    {
                        BuiltinResource newAsset = new BuiltinResource();
                        newAsset.m_Name = m_Owner.GetCreateAssetUtility().originalName;
                        newAsset.m_InstanceID = m_Owner.GetCreateAssetUtility().instanceID;

                        DrawItem(m_Grid.CalcRect(itemIndex, yOffset), null, newAsset, isFolderBrowsing);
                        itemIndex++; // Push following items forward
                        itemCount++;
                    }

                    // Stop conditions
                    if (itemIndex > endIndex)
                        break;
                    if (resultsIdx >= results.Length)
                        break;

                    // Draw item
                    FilteredHierarchy.FilterResult result = results[resultsIdx];
                    itemRect = m_Grid.CalcRect(itemIndex, yOffset);
                    DrawItem(itemRect, result, null, isFolderBrowsing);
                    itemIndex++;
                    resultsIdx++;
                }
                itemCount += results.Length;

                // 3. Builtin
                if (m_ActiveBuiltinList.Length > 0)
                {
                    int builtinStartIdx = beginIndex - itemCount;
                    builtinStartIdx = Math.Max(builtinStartIdx, 0);
                    for (int builtinIdx = builtinStartIdx; builtinIdx < m_ActiveBuiltinList.Length && itemIndex <= endIndex; itemIndex++, builtinIdx++)
                    {
                        DrawItem(m_Grid.CalcRect(itemIndex, yOffset), null, m_ActiveBuiltinList[builtinIdx], isFolderBrowsing);
                    }
                }

                // Repaint again if we are in preview icon mode and previews are being loaded
                if (!ListMode && AssetPreview.IsLoadingAssetPreviews(m_Owner.GetAssetPreviewManagerID()))
                    m_Owner.Repaint();
            }

            void ClearDirtyStateTracking()
            {
                m_LastRenderedAssetInstanceIDs.Clear();
                m_LastRenderedAssetDirtyCounts.Clear();
            }

            void AddDirtyStateFor(int instanceID)
            {
                m_LastRenderedAssetInstanceIDs.Add(instanceID);
                m_LastRenderedAssetDirtyCounts.Add(EditorUtility.GetDirtyCount(instanceID));
            }

            public bool IsAnyLastRenderedAssetsDirty()
            {
                for (int i = 0; i < m_LastRenderedAssetInstanceIDs.Count; ++i)
                {
                    int dirtyCount = EditorUtility.GetDirtyCount(m_LastRenderedAssetInstanceIDs[i]);
                    if (dirtyCount != m_LastRenderedAssetDirtyCounts[i])
                    {
                        m_LastRenderedAssetDirtyCounts[i] = dirtyCount;
                        return true;
                    }
                }
                return false;
            }

            protected override void HandleUnusedDragEvents(float yOffset)
            {
                if (!m_Owner.allowDragging)
                    return;
                Event evt = Event.current;
                switch (evt.type)
                {
                    case EventType.DragUpdated:
                    case EventType.DragPerform:
                        Rect localGroupRect = new Rect(0, yOffset, m_Owner.m_TotalRect.width, m_Owner.m_TotalRect.height > Height ? m_Owner.m_TotalRect.height : Height);
                        if (localGroupRect.Contains(evt.mousePosition))
                        {
                            DragAndDropVisualMode mode;
                            bool isFolderBrowsing = (m_FilteredHierarchy.searchFilter.GetState() == SearchFilter.State.FolderBrowsing);
                            if (isFolderBrowsing && m_FilteredHierarchy.searchFilter.folders.Length == 1)
                            {
                                string folder = m_FilteredHierarchy.searchFilter.folders[0];
                                int instanceID = AssetDatabase.GetMainAssetInstanceID(folder);
                                bool perform = evt.type == EventType.DragPerform;
                                mode = DoDrag(instanceID, perform);
                                if (perform && mode != DragAndDropVisualMode.None)
                                    DragAndDrop.AcceptDrag();
                            }
                            else
                            {
                                // Disallow drop: more than one folder or search is active, since dropping would be ambiguous.
                                mode = DragAndDropVisualMode.None;
                            }
                            DragAndDrop.visualMode = mode;
                            evt.Use();
                        }
                        break;
                    case EventType.DragExited:
                        m_DragSelection.Clear();
                        break;
                }
            }

            void HandleMouseWithDragging(ref AssetReference assetReference, int controlID, Rect rect)
            {
                // Handle mouse down on entire line
                Event evt = Event.current;

                switch (evt.GetTypeForControl(controlID))
                {
                    case EventType.MouseDown:
                        if (Event.current.button == 0 && rect.Contains(Event.current.mousePosition))
                        {
                            if (evt.clickCount == 2)
                            {
                                // Double clicked
                                var newSelection = GetNewSelection(ref assetReference, false, false);
                                m_Owner.SetSelection(newSelection.ToArray(), true);
                                m_DragSelection.Clear();
                            }
                            else
                            {
                                // Begin drag
                                var newSelection = GetNewSelection(ref assetReference, false, false);
                                var oldItemControlID = controlID;
                                controlID = GetControlIDFromInstanceID(assetReference.instanceID);
                                if (controlID == oldItemControlID)
                                {
                                    newSelection = GetNewSelection(ref assetReference, true, false);
                                    m_DragSelection = newSelection;
                                    DragAndDropDelay delay = (DragAndDropDelay)GUIUtility.GetStateObject(typeof(DragAndDropDelay), controlID);
                                    delay.mouseDownPosition = Event.current.mousePosition;
                                    m_Owner.ScrollToPosition(ObjectListArea.AdjustRectForFraming(rect));
                                }
                                else
                                {
                                    m_Owner.SetSelection(newSelection.ToArray(), false);
                                    m_DragSelection.Clear();
                                }
                                GUIUtility.hotControl = controlID;
                            }

                            evt.Use();
                        }
                        else if (Event.current.button == 1 && rect.Contains(Event.current.mousePosition))
                        {
                            if (assetReference.instanceID == 0)
                            {
                                // For non selectable assets, don't show context menu. Selection is deselected
                                m_Owner.SetSelection(new int[0], false);
                                Event.current.Use();
                            }
                            else
                            {
                                // Right mouse down selection (do NOT use event since we need ContextClick event, which is not fired if right click is used)
                                m_Owner.SetSelection(GetNewSelection(ref assetReference, true, false).ToArray(), false);
                            }
                        }
                        break;
                    case EventType.MouseDrag:
                        if (GUIUtility.hotControl == controlID)
                        {
                            DragAndDropDelay delay = (DragAndDropDelay)GUIUtility.GetStateObject(typeof(DragAndDropDelay), controlID);
                            if (delay.CanStartDrag())
                            {
                                StartDrag(assetReference.instanceID, m_DragSelection);
                                GUIUtility.hotControl = 0;
                            }

                            evt.Use();
                        }
                        break;
                    case EventType.DragUpdated:
                    case EventType.DragPerform:
                    {
                        bool perform = evt.type == EventType.DragPerform;
                        if (rect.Contains(evt.mousePosition))
                        {
                            DragAndDropVisualMode mode = DoDrag(assetReference.instanceID, perform);

                            if (mode == DragAndDropVisualMode.Rejected && perform)
                                evt.Use();
                            else if (mode != DragAndDropVisualMode.None)
                            {
                                if (perform)
                                    DragAndDrop.AcceptDrag();

                                m_DropTargetControlID = controlID;
                                DragAndDrop.visualMode = mode;
                                evt.Use();
                            }

                            if (perform)
                                m_DropTargetControlID = 0;
                        }

                        if (perform)
                            m_DragSelection.Clear();
                    }
                    break;
                    case EventType.MouseUp:
                        if (GUIUtility.hotControl == controlID)
                        {
                            if (rect.Contains(evt.mousePosition))
                            {
                                bool clickedOnText;
                                if (ListMode)
                                {
                                    rect.x += 28;
                                    rect.width += 28;
                                    clickedOnText = rect.Contains(evt.mousePosition);
                                }
                                else
                                {
                                    rect.y = rect.y + rect.height - Styles.resultsGridLabel.fixedHeight;
                                    rect.height = Styles.resultsGridLabel.fixedHeight;
                                    clickedOnText = rect.Contains(evt.mousePosition);
                                }

                                List<int> selected = m_Owner.m_State.m_SelectedInstanceIDs;
                                if (clickedOnText && m_Owner.allowRenaming && m_Owner.m_AllowRenameOnMouseUp && selected.Count == 1 && selected[0] == assetReference.instanceID && !EditorGUIUtility.HasHolddownKeyModifiers(evt))
                                {
                                    m_Owner.BeginRename(0.5f);
                                }
                                else
                                {
                                    List<int> newSelection = GetNewSelection(ref assetReference, false, false);
                                    m_Owner.SetSelection(newSelection.ToArray(), false);
                                }

                                GUIUtility.hotControl = 0;
                                evt.Use();
                            }

                            m_DragSelection.Clear();
                        }
                        break;

                    case EventType.ContextClick:
                        HandleContextClick(evt, rect);
                        break;
                }
            }

            void HandleMouseWithoutDragging(ref AssetReference assetReference, int controlID, Rect position)
            {
                Event evt = Event.current;

                switch (evt.GetTypeForControl(controlID))
                {
                    case EventType.MouseDown:
                        if (evt.button == 0 && position.Contains(evt.mousePosition))
                        {
                            m_Owner.Repaint();

                            if (evt.clickCount == 1)
                            {
                                m_Owner.ScrollToPosition(ObjectListArea.AdjustRectForFraming(position));
                            }

                            evt.Use();
                            List<int> newSelection = GetNewSelection(ref assetReference, false, false);
                            m_Owner.SetSelection(newSelection.ToArray(), evt.clickCount == 2);
                        }
                        break;

                    case EventType.ContextClick:
                        if (position.Contains(evt.mousePosition))
                        {
                            // Select it
                            List<int> newSelection = GetNewSelection(ref assetReference, false, false);
                            m_Owner.SetSelection(newSelection.ToArray(), false);

                            HandleContextClick(evt, position);
                        }
                        break;
                }
            }

            static void HandleContextClick(Event evt, Rect rect)
            {
                var overlayRect = rect;
                overlayRect.x += 2;
                overlayRect = ProjectHooks.GetOverlayRect(overlayRect);

                var vco = VersionControlManager.activeVersionControlObject;
                if (vco != null && !vco.isConnected)
                    vco = null;
                var vcConnected = vco != null || Provider.isActive;
                if (vcConnected && overlayRect.width != rect.width && overlayRect.Contains(evt.mousePosition))
                {
                    if (vco != null)
                        vco.GetExtension<IPopupMenuExtension>()?.DisplayPopupMenu(new Rect(evt.mousePosition.x, evt.mousePosition.y, 0, 0));
                    else
                        EditorUtility.DisplayPopupMenu(new Rect(evt.mousePosition.x, evt.mousePosition.y, 0, 0), "Assets/Version Control", new MenuCommand(null, 0));
                    evt.Use();
                }
            }

            public void ChangeExpandedState(int instanceID, bool expanded)
            {
                m_Owner.m_State.m_ExpandedInstanceIDs.Remove(instanceID);
                if (expanded)
                    m_Owner.m_State.m_ExpandedInstanceIDs.Add(instanceID);
                m_FilteredHierarchy.RefreshVisibleItems(m_Owner.m_State.m_ExpandedInstanceIDs);
            }

            bool IsExpanded(int instanceID)
            {
                return (m_Owner.m_State.m_ExpandedInstanceIDs.IndexOf(instanceID) >= 0);
            }

            void SelectAndFrameParentOf(int instanceID)
            {
                int parentInstanceID = 0;
                FilteredHierarchy.FilterResult[] results = m_FilteredHierarchy.results;
                for (int i = 0; i < results.Length; ++i)
                {
                    if (results[i].instanceID == instanceID)
                    {
                        if (results[i].isMainRepresentation)
                            parentInstanceID = 0;
                        break;
                    }

                    if (results[i].isMainRepresentation)
                        parentInstanceID = results[i].instanceID;
                }

                if (parentInstanceID != 0)
                {
                    m_Owner.SetSelection(new int[] {parentInstanceID}, false);
                    m_Owner.Frame(parentInstanceID, true, false);
                }
            }

            bool IsRenaming(int instanceID)
            {
                RenameOverlay renameOverlay = m_Owner.GetRenameOverlay();
                return renameOverlay.IsRenaming() && renameOverlay.userData == instanceID && !renameOverlay.isWaitingForDelay;
            }

            protected void DrawSubAssetRowBg(int startSubAssetIndex, int endSubAssetIndex, bool continued, float yOffset)
            {
                Rect startRect = m_Grid.CalcRect(startSubAssetIndex, yOffset);
                Rect endRect = m_Grid.CalcRect(endSubAssetIndex, yOffset);

                float texWidth = 30f;
                float texHeight = 128f;
                float fraction = startRect.width / texHeight;
                float overflowHeight = 9f * fraction;
                float shrinkHeight = 4f;

                // Start
                bool startIsOnFirstColumn = (startSubAssetIndex % m_Grid.columns) == 0;
                float adjustStart = startIsOnFirstColumn ? 18f * fraction : m_Grid.horizontalSpacing + fraction * 10f;
                Rect rect = new Rect(startRect.x - adjustStart, startRect.y + shrinkHeight, texWidth * fraction, startRect.width - shrinkHeight * 2 + overflowHeight - 1);
                rect.y = Mathf.Round(rect.y);
                rect.height = Mathf.Ceil(rect.height);
                Styles.subAssetBg.Draw(rect, GUIContent.none, false, false, false, false);

                // End
                float scaledWidth = texWidth * fraction;
                bool endIsOnLastColumn = (endSubAssetIndex % m_Grid.columns) == (m_Grid.columns - 1);
                float extendEnd = (continued || endIsOnLastColumn) ? 16 * fraction : 8 * fraction;
                Rect rect2 = new Rect(endRect.xMax - scaledWidth + extendEnd, endRect.y + shrinkHeight, scaledWidth, rect.height);
                rect2.y = Mathf.Round(rect2.y);
                rect2.height = Mathf.Ceil(rect2.height);
                GUIStyle endStyle = continued ? Styles.subAssetBgOpenEnded : Styles.subAssetBgCloseEnded;
                endStyle.Draw(rect2, GUIContent.none, false, false, false, false);

                // Middle
                rect = new Rect(rect.xMax, rect.y, rect2.xMin - rect.xMax, rect.height);
                rect.y = Mathf.Round(rect.y);
                rect.height = Mathf.Ceil(rect.height);
                Styles.subAssetBgMiddle.Draw(rect, GUIContent.none, false, false, false, false);
            }

            void DrawSubAssetBackground(int beginIndex, int endIndex, float yOffset)
            {
                if (Event.current.type != EventType.Repaint)
                    return;

                FilteredHierarchy.FilterResult[] results = m_FilteredHierarchy.results;

                int columns = m_Grid.columns;
                int rows = (endIndex - beginIndex) / columns + 1;

                for (int y = 0; y < rows; ++y)
                {
                    int startSubAssetIndex = -1;
                    int endSubAssetIndex = -1;
                    for (int x = 0; x < columns; ++x)
                    {
                        int index = beginIndex + (x + y * columns);
                        if (index >= results.Length)
                            break;

                        FilteredHierarchy.FilterResult result = results[index];
                        if (!result.isMainRepresentation)
                        {
                            if (startSubAssetIndex == -1)
                                startSubAssetIndex = index;
                            endSubAssetIndex = index;
                        }
                        else
                        {
                            // Check if a section was ended
                            if (startSubAssetIndex != -1)
                            {
                                DrawSubAssetRowBg(startSubAssetIndex, endSubAssetIndex, false, yOffset);
                                startSubAssetIndex = -1;
                                endSubAssetIndex = -1;
                            }
                        }
                    }

                    if (startSubAssetIndex != -1)
                    {
                        bool continued = false;
                        if (y < rows - 1)
                        {
                            int indexFirstColumnNextRow = beginIndex + (y + 1) * columns;
                            if (indexFirstColumnNextRow < results.Length)
                                continued = !results[indexFirstColumnNextRow].isMainRepresentation;
                        }

                        DrawSubAssetRowBg(startSubAssetIndex, endSubAssetIndex, continued, yOffset);
                    }
                }
            }

            void DrawItem(Rect position, FilteredHierarchy.FilterResult filterItem, BuiltinResource builtinResource, bool isFolderBrowsing)
            {
                System.Diagnostics.Debug.Assert((filterItem != null && builtinResource == null) ||
                    (builtinResource != null && filterItem == null));          // only one should be valid

                Event evt = Event.current;
                Rect itemRect = position;
                Rect orgPosition = position;

                var assetReference = new AssetReference() { instanceID = 0 };
                bool showFoldout = false;
                if (filterItem != null)
                {
                    assetReference.instanceID = filterItem.instanceID;
                    assetReference.guid = filterItem.guid;
                    showFoldout = filterItem.hasChildren && !filterItem.isFolder && isFolderBrowsing; // we do not want to be able to expand folders
                }
                else if (builtinResource != null)
                {
                    assetReference.instanceID = builtinResource.m_InstanceID;
                }

                int controlID = GetControlIDFromInstanceID(assetReference.instanceID);

                bool selected;
                if (m_Owner.allowDragging)
                    selected = m_DragSelection.Count > 0 ? m_DragSelection.Contains(assetReference.instanceID) : m_Owner.IsSelected(assetReference.instanceID);
                else
                    selected = m_Owner.IsSelected(assetReference.instanceID);

                if (selected && assetReference.instanceID == m_Owner.m_State.m_LastClickedInstanceID)
                    m_LastClickedDrawTime = EditorApplication.timeSinceStartup;

                Rect foldoutRect = new Rect(position.x + Styles.groupFoldout.margin.left, position.y, Styles.groupFoldout.padding.left, position.height); // ListMode foldout
                if (showFoldout && !ListMode)
                {
                    float fraction = position.height / 128f;
                    float buttonWidth = 28f;
                    float buttonHeight = 32f;

                    if (fraction < 0.5f)
                    {
                        buttonWidth = 14f;
                        buttonHeight = 16;
                    }
                    else if (fraction < 0.75f)
                    {
                        buttonWidth = 21f;
                        buttonHeight = 24f;
                    }

                    foldoutRect = new Rect(position.xMax - buttonWidth * 0.5f, position.y + (position.height - Styles.resultsGridLabel.fixedHeight) * 0.5f - buttonWidth * 0.5f, buttonWidth, buttonHeight);
                }

                bool toggleState = false;
                if (selected && evt.type == EventType.KeyDown && m_Owner.HasFocus()) // We need to ensure we have keyboard focus because rename overlay might have it - and need the key events)
                {
                    switch (evt.keyCode)
                    {
                        // Fold in
                        case KeyCode.LeftArrow:
                            if (ListMode || m_Owner.IsPreviewIconExpansionModifierPressed())
                            {
                                if (IsExpanded(assetReference.instanceID))
                                    toggleState = true;
                                else
                                    SelectAndFrameParentOf(assetReference.instanceID);
                                evt.Use();
                            }
                            break;

                        // Fold out
                        case KeyCode.RightArrow:
                            if (ListMode || m_Owner.IsPreviewIconExpansionModifierPressed())
                            {
                                if (!IsExpanded(assetReference.instanceID))
                                    toggleState = true;
                                evt.Use();
                            }
                            break;
                    }
                }

                // Foldout mouse button logic (rendering the item itself can be found below)
                if (showFoldout && evt.type == EventType.MouseDown && evt.button == 0 && foldoutRect.Contains(evt.mousePosition))
                    toggleState = true;

                if (toggleState)
                {
                    bool expanded = !IsExpanded(assetReference.instanceID);
                    if (expanded)
                        m_ItemFader.Start(m_FilteredHierarchy.GetSubAssetInstanceIDs(assetReference.instanceID));
                    ChangeExpandedState(assetReference.instanceID, expanded);
                    evt.Use();
                    GUIUtility.ExitGUI();
                }

                bool isRenaming = IsRenaming(assetReference.instanceID);

                Rect labelRect = position;
                if (!ListMode)
                    labelRect = new Rect(position.x, position.yMax + 1 - Styles.resultsGridLabel.fixedHeight, position.width - 1, Styles.resultsGridLabel.fixedHeight);

                var vcPadding = VersionControlUtils.isVersionControlConnected && ListMode ? k_ListModeVersionControlOverlayPadding : 0;

                float contentStartX = foldoutRect.xMax;
                if (ListMode)
                {
                    itemRect.x = contentStartX;
                    if (filterItem != null && !filterItem.isMainRepresentation && isFolderBrowsing)
                    {
                        contentStartX = k_ListModeLeftPaddingForSubAssets;
                        itemRect.x = k_ListModeLeftPaddingForSubAssets + vcPadding * 0.5f;
                    }
                    itemRect.width -= itemRect.x;
                }

                // Draw section
                if (Event.current.type == EventType.Repaint)
                {
                    if (m_DropTargetControlID == controlID && !position.Contains(evt.mousePosition))
                        m_DropTargetControlID = 0;
                    bool isDropTarget = controlID == m_DropTargetControlID && m_DragSelection.IndexOf(m_DropTargetControlID) == -1;

                    string labeltext = filterItem != null ? filterItem.name : builtinResource.m_Name;
                    if (ListMode)
                    {
                        if (isRenaming)
                        {
                            selected = false;
                            labeltext = "";
                        }

                        m_Content.text = labeltext;
                        m_Content.image = null;
                        Texture2D icon;

                        if (string.IsNullOrEmpty(assetReference.guid) && m_Owner.GetCreateAssetUtility().instanceID == assetReference.instanceID && m_Owner.GetCreateAssetUtility().icon != null)
                        {
                            // If we are creating a new asset we might have an icon to use
                            icon = m_Owner.GetCreateAssetUtility().icon;
                        }
                        else if (builtinResource is ExtraItem extraItem)
                        {
                            icon = extraItem.m_Icon;
                        }
                        else
                        {
                            icon = filterItem != null ? filterItem.icon : null;
                            if (icon == null)
                            {
                                if (ShouldGetAssetPreview(assetReference))
                                {
                                    if (assetReference.instanceID != 0)
                                        icon = AssetPreview.GetAssetPreview(assetReference.instanceID, m_Owner.GetAssetPreviewManagerID());
                                    else if (!string.IsNullOrEmpty(assetReference.guid))
                                        icon = AssetPreview.GetAssetPreviewFromGUID(assetReference.guid, m_Owner.GetAssetPreviewManagerID());
                                }
                                else if (assetReference.instanceID != 0)
                                {
                                    icon = AssetPreview.GetMiniTypeThumbnail(EditorUtility.InstanceIDToObject(assetReference.instanceID));
                                }
                            }
                        }

                        if (selected)
                            Styles.resultsLabel.Draw(position, GUIContent.none, false, false, selected, m_Owner.HasFocus());

                        if (isDropTarget)
                            Styles.resultsLabel.Draw(position, GUIContent.none, true, true, false, false);

                        DrawIconAndLabel(new Rect(contentStartX, position.y, position.width - contentStartX, position.height),
                            filterItem, labeltext, icon, selected, m_Owner.HasFocus());

                        // Foldout!
                        if (showFoldout)
                            Styles.groupFoldout.Draw(foldoutRect, !ListMode, !ListMode, IsExpanded(assetReference.instanceID), false);
                    }
                    else // Icon grid
                    {
                        Texture previewImage = null;

                        // Get icon
                        bool drawDropShadow = false;
                        if (string.IsNullOrEmpty(assetReference.guid) && m_Owner.GetCreateAssetUtility().instanceID == assetReference.instanceID && m_Owner.GetCreateAssetUtility().icon != null)
                        {
                            // If we are creating a new asset we might have an icon to use
                            m_Content.image = m_Owner.GetCreateAssetUtility().icon;
                        }
                        else if (builtinResource is ExtraItem extraItem)
                        {
                            m_Content.image = extraItem.m_Icon;
                        }
                        else
                        {
                            // Check for asset preview
                            bool shouldGetAssetPreview = ShouldGetAssetPreview(assetReference);
                            if (shouldGetAssetPreview)
                            {
                                if (assetReference.instanceID != 0)
                                    previewImage = AssetPreview.GetAssetPreview(assetReference.instanceID, m_Owner.GetAssetPreviewManagerID());
                                else if (!string.IsNullOrEmpty(assetReference.guid))
                                    previewImage = AssetPreview.GetAssetPreviewFromGUID(assetReference.guid, m_Owner.GetAssetPreviewManagerID());
                            }

                            m_Content.image = previewImage;
                            if (m_Content.image != null)
                                drawDropShadow = true;

                            if (filterItem != null)
                            {
                                // Otherwise use cached icon
                                if (m_Content.image == null)
                                    m_Content.image = filterItem.icon;

                                // When folder browsing sub assets are shown on a background slate and do not need rounded corner overlay
                                if (isFolderBrowsing && !filterItem.isMainRepresentation)
                                    drawDropShadow = false;
                            }

                            // If the icon is still hasn't been found, fall back to the default one
                            if (m_Content.image == null && assetReference.instanceID != 0)
                            {
                                m_Content.image = AssetPreview.GetMiniTypeThumbnail(EditorUtility.InstanceIDToObject(assetReference.instanceID));
                            }
                        }

                        float padding = (drawDropShadow) ? 2.0f : 0.0f; // the padding compensates for the drop shadow (so it doesn't get too close to the label text)
                        position.height -= Styles.resultsGridLabel.fixedHeight + 2 * padding; // get icon rect (remove label height which is included in the position rect)
                        position.y += padding;

                        Rect actualImageDrawPosition = (m_Content.image == null) ? new Rect() : ActualImageDrawPosition(position, m_Content.image.width, m_Content.image.height);
                        m_Content.text = null;
                        float alpha = 1f;

                        if (filterItem != null)
                        {
                            AddDirtyStateFor(filterItem.instanceID);

                            if (!filterItem.isMainRepresentation && isFolderBrowsing)
                            {
                                position.x += 4f;
                                position.y += 4f;
                                position.width -= 8f;
                                position.height -= 8f;

                                actualImageDrawPosition = (m_Content.image == null) ? new Rect() : ActualImageDrawPosition(position, m_Content.image.width, m_Content.image.height);

                                alpha = m_ItemFader.GetAlpha(filterItem.instanceID);
                                if (alpha < 1f)
                                    m_Owner.Repaint();
                            }

                            // Draw static preview bg color as bg for small textures and non-square textures
                            if (drawDropShadow && filterItem.iconDrawStyle == IconDrawStyle.NonTexture)
                                Styles.previewBg.Draw(actualImageDrawPosition, GUIContent.none, false, false, false, false);
                        }

                        var color = ProjectBrowser.GetAssetItemColor(assetReference.instanceID);

                        using (new GUI.ColorScope(color))
                        {
                            Color orgColor = GUI.color;
                            if (selected)
                                GUI.color = GUI.color * new Color(0.85f, 0.9f, 1f);

                            if (m_Content.image != null)
                            {
                                Color orgColor2 = GUI.color;
                                if (alpha < 1f)
                                    GUI.color = new Color(GUI.color.r, GUI.color.g, GUI.color.b, alpha);

                                Styles.resultsGrid.Draw(actualImageDrawPosition, m_Content, false, false, selected, m_Owner.HasFocus());
                                DynamicHintUtility.DrawHint(position, evt.mousePosition, assetReference);

                                if (alpha < 1f)
                                    GUI.color = orgColor2;
                            }

                            if (selected)
                                GUI.color = orgColor;

                            if (drawDropShadow)
                            {
                                Rect borderPosition = new RectOffset(1, 1, 1, 1).Remove(Styles.textureIconDropShadow.border.Add(actualImageDrawPosition));
                                Styles.textureIconDropShadow.Draw(borderPosition, GUIContent.none, false, false, selected || isDropTarget, m_Owner.HasFocus() || isRenaming || isDropTarget);
                            }

                            // Draw label
                            if (!isRenaming)
                            {
                                if (isDropTarget)
                                    Styles.resultsLabel.Draw(new Rect(labelRect.x - 10, labelRect.y, labelRect.width + 20, labelRect.height), GUIContent.none, true, true, false, false);


                                Texture2D typeIcon = null;
                                if (filterItem != null && previewImage != null)
                                {
                                    Type type = InternalEditorUtility.GetTypeWithoutLoadingObject(filterItem.instanceID);
                                    
                                    if (type != typeof(Texture2D))
                                    {
                                        typeIcon = filterItem.icon;
                                    }
                                }

                                if (builtinResource != null)
                                {
                                    Type type = InternalEditorUtility.GetTypeWithoutLoadingObject(builtinResource.m_InstanceID);

                                    if (type != typeof(Texture2D))
                                    {
                                        typeIcon = AssetPreview.GetMiniTypeThumbnail(type);
                                    }
                                }

                                var orgClipping = Styles.resultsGridLabel.clipping;
                                var orgAlignment = Styles.resultsLabel.alignment;
                                var size  = Styles.resultsGridLabel.CalcSizeWithConstraints(GUIContent.Temp(labeltext, typeIcon), orgPosition.size);
                                size.x += Styles.resultsGridLabel.padding.horizontal;
                                labelRect.x = orgPosition.x + (orgPosition.width - size.x) / 2.0f;
                                labelRect.width = size.x;
                                labelRect.height = size.y;
                                m_Owner.sizeUsedForCroppingName = orgPosition.size;

                                Styles.resultsGridLabel.clipping = TextClipping.Ellipsis;
                                Styles.resultsGridLabel.alignment = TextAnchor.MiddleCenter;
                                Styles.resultsGridLabel.Draw(labelRect, GUIContent.Temp(labeltext, typeIcon), false, false, selected, m_Owner.HasFocus());
                                Styles.resultsGridLabel.clipping = orgClipping;
                                Styles.resultsLabel.alignment = orgAlignment;

                                // We only need to set the tooltip once, and not for every item.
                                if (labelRect.Contains(Event.current.mousePosition))
                                {
                                    string tooltip = null;

                                    if (filterItem != null)
                                    {
                                        //We use GetAssetPath to have the file extension as well
                                        string path = AssetDatabase.GetAssetPath(filterItem.instanceID);
                                        tooltip = path.Substring(path.LastIndexOf('/') + 1);
                                    }
                                    else if (builtinResource != null)
                                    {
                                        //We have a "None" item in the ObjectSelector that has a 0 instanceID
                                        if (builtinResource.m_InstanceID != 0)     
                                            tooltip = builtinResource.m_Name + "\n" + "(Built-in Resource)";
                                    }

                                    if (tooltip != null)
                                    {
                                        GUI.Label(labelRect, GUIContent.Temp("", tooltip));
                                    }
                                }
                            }
                        }

                        if (showFoldout)
                        {
                            var style = Styles.subAssetExpandButton;

                            if (foldoutRect.height <= 16)
                            {
                                style = Styles.subAssetExpandButtonSmall;
                            }
                            else if (foldoutRect.height <= 24)
                            {
                                style = Styles.subAssetExpandButtonMedium;
                            }
                            style.Draw(foldoutRect, !ListMode, !ListMode, IsExpanded(assetReference.instanceID), false);
                        }

                        if (filterItem != null && filterItem.isMainRepresentation)
                        {
                            if (null != postAssetIconDrawCallback)
                            {
                                postAssetIconDrawCallback(position, filterItem.guid, false);
                            }

                            ProjectHooks.OnProjectWindowItem(filterItem.guid, position, m_OwnerRepaintAction);
                        }
                    }
                }
                // Adjust edit field if needed
                if (isRenaming)
                {
                    if (ListMode)
                    {
                        float iconOffset = vcPadding + k_IconWidth + k_SpaceBetweenIconAndText + Styles.resultsLabel.margin.left;
                        labelRect.x = itemRect.x + iconOffset;
                        labelRect.width -= labelRect.x;
                    }
                    else
                    {
                        labelRect.x -= 4;
                        labelRect.width += 8f;
                    }
                    m_Owner.GetRenameOverlay().editFieldRect = labelRect;
                    m_Owner.HandleRenameOverlay();
                }

                // User hook for rendering stuff on top of items (notice it being called after rendering but before mouse handling to make user able to react on mouse events)
                if (filterItem != null && m_Owner.allowUserRenderingHook)
                {
                    if (EditorApplication.projectWindowItemOnGUI != null)
                        EditorApplication.projectWindowItemOnGUI(filterItem.guid, itemRect);

                    if (EditorApplication.projectWindowItemInstanceOnGUI != null)
                        EditorApplication.projectWindowItemInstanceOnGUI(filterItem.instanceID, itemRect);
                }

                // Mouse handling (must be after rename overlay to ensure overlay get mouseevents)
                if (m_Owner.allowDragging)
                    HandleMouseWithDragging(ref assetReference, controlID, position);
                else
                    HandleMouseWithoutDragging(ref assetReference, controlID, position);

                if (filterItem != null && filterItem.instanceID == 0)
                    filterItem.instanceID = assetReference.instanceID;
            }

            private static Rect ActualImageDrawPosition(Rect position, float imageWidth, float imageHeight)
            {
                if (imageWidth > position.width || imageHeight > position.height)
                {
                    Rect screenRect = new Rect();
                    Rect sourceRect = new Rect();
                    float imageAspect = imageWidth / imageHeight;
                    GUI.CalculateScaledTextureRects(position, ScaleMode.ScaleToFit, imageAspect, ref screenRect, ref sourceRect);
                    return screenRect;
                }
                else
                {
                    float x = position.x + Mathf.Round((position.width - imageWidth) / 2.0f);
                    float y = position.y + Mathf.Round((position.height - imageHeight) / 2.0f);
                    return new Rect(x, y, imageWidth, imageHeight);
                }
            }

            public List<KeyValuePair<string, int>> GetVisibleNameAndInstanceIDs()
            {
                List<KeyValuePair<string, int>> result = new List<KeyValuePair<string, int>>();

                // 1. None item
                if (m_NoneList.Length > 0)
                    result.Add(new KeyValuePair<string, int>(m_NoneList[0].m_Name, m_NoneList[0].m_InstanceID)); // 0

                // 2. Project Assets
                foreach (FilteredHierarchy.FilterResult r in m_FilteredHierarchy.results)
                    result.Add(new KeyValuePair<string, int>(r.name, r.instanceID));

                // 3. Builtin
                for (int i = 0; i < m_ActiveBuiltinList.Length; ++i)
                    result.Add(new KeyValuePair<string, int>(m_ActiveBuiltinList[i].m_Name, m_ActiveBuiltinList[i].m_InstanceID));

                return result;
            }

            private void BeginPing(int instanceID)
            {
            }

            public void GetAssetReferences(out List<int> instanceIDs, out List<string> guids)
            {
                instanceIDs = new List<int>();
                guids = new List<string>();

                // 1. None item
                if (m_NoneList.Length > 0)
                {
                    instanceIDs.Add(m_NoneList[0].m_InstanceID); // 0
                    guids.Add(null);
                }

                // 2. Project Assets
                foreach (FilteredHierarchy.FilterResult r in m_FilteredHierarchy.results)
                {
                    instanceIDs.Add(r.instanceID);
                    guids.Add(r.guid);
                }

                if (m_Owner.m_State.m_NewAssetIndexInList >= 0)
                {
                    instanceIDs.Add(m_Owner.GetCreateAssetUtility().instanceID);
                    guids.Add(null);
                }

                // 3. Builtin
                for (int i = 0; i < m_ActiveBuiltinList.Length; ++i)
                {
                    instanceIDs.Add(m_ActiveBuiltinList[i].m_InstanceID);
                    guids.Add(null);
                }
            }

            // Returns list of selected instanceIDs
            public List<int> GetNewSelection(ref AssetReference clickedAssetReference, bool beginOfDrag, bool useShiftAsActionKey)
            {
                // Flatten grid
                List<int> instanceIDs;
                List<string> guids;
                GetAssetReferences(out instanceIDs, out guids);
                List<int> selectedInstanceIDs = m_Owner.m_State.m_SelectedInstanceIDs;
                int lastClickedInstanceID = m_Owner.m_State.m_LastClickedInstanceID;
                bool allowMultiselection = m_Owner.allowMultiSelect;

                return InternalEditorUtility.GetNewSelection(ref clickedAssetReference, instanceIDs, guids, selectedInstanceIDs, lastClickedInstanceID, beginOfDrag, useShiftAsActionKey, allowMultiselection);
            }

            public override void UpdateFilter(HierarchyType hierarchyType, SearchFilter searchFilter, bool foldersFirst, SearchService.SearchSessionOptions searchSessionOptions)
            {
                // Filtered hierarchy list
                RefreshHierarchy(hierarchyType, searchFilter, foldersFirst, searchSessionOptions);

                // Filtered builtin list
                RefreshBuiltinResourceList(searchFilter);
            }

            private void RefreshHierarchy(HierarchyType hierarchyType, SearchFilter searchFilter, bool foldersFirst, SearchService.SearchSessionOptions searchSessionOptions)
            {
                m_FilteredHierarchy = new FilteredHierarchy(hierarchyType, searchSessionOptions);
                m_FilteredHierarchy.foldersFirst = foldersFirst;
                m_FilteredHierarchy.searchFilter = searchFilter;
                m_FilteredHierarchy.RefreshVisibleItems(m_Owner.m_State.m_ExpandedInstanceIDs);
            }

            void RefreshBuiltinResourceList(SearchFilter searchFilter)
            {
                // Early out if we do not want to show builtin resources
                if (!m_Owner.allowBuiltinResources || (searchFilter.GetState() == SearchFilter.State.FolderBrowsing) || (searchFilter.GetState() == SearchFilter.State.EmptySearchFilter))
                {
                    m_CurrentBuiltinResources = new BuiltinResource[0];
                    return;
                }

                List<BuiltinResource> currentBuiltinResources = new List<BuiltinResource>();

                // Filter by assets labels (Builtins have no asset labels currently)
                if (searchFilter.assetLabels != null && searchFilter.assetLabels.Length > 0)
                {
                    m_CurrentBuiltinResources = currentBuiltinResources.ToArray();
                    return;
                }

                // Filter by class/type
                List<int> requiredClassIDs = new List<int>();
                foreach (string className in searchFilter.classNames)
                {
                    var unityType = UnityType.FindTypeByNameCaseInsensitive(className);
                    if (unityType != null)
                        requiredClassIDs.Add(unityType.persistentTypeID);
                }
                if (requiredClassIDs.Count > 0)
                {
                    foreach (KeyValuePair<string, BuiltinResource[]> kvp in m_BuiltinResourceMap)
                    {
                        UnityType classType = UnityType.FindTypeByName(kvp.Key);
                        if (classType == null)
                            continue;

                        foreach (int requiredClassID in requiredClassIDs)
                        {
                            if (classType.IsDerivedFrom(UnityType.FindTypeByPersistentTypeID(requiredClassID)))
                                currentBuiltinResources.AddRange(kvp.Value);
                        }
                    }
                }

                // Filter by name
                BuiltinResource[] builtinList = currentBuiltinResources.ToArray();
                if (builtinList.Length > 0 && !string.IsNullOrEmpty(searchFilter.nameFilter))
                {
                    List<BuiltinResource> filtered = new List<BuiltinResource>(); // allocated here to prevent from allocating on every event.
                    string nameFilter = searchFilter.nameFilter.ToLower();
                    foreach (BuiltinResource br in builtinList)
                        if (br.m_Name.ToLower().Contains(nameFilter))
                            filtered.Add(br);

                    builtinList = filtered.ToArray();
                }

                m_CurrentBuiltinResources = builtinList;
            }

            public string GetNameOfLocalAsset(int instanceID)
            {
                foreach (var r in m_FilteredHierarchy.results)
                {
                    if (r.instanceID == instanceID)
                        return r.name;
                }
                return null;
            }

            public bool IsBuiltinAsset(int instanceID)
            {
                foreach (KeyValuePair<string, BuiltinResource[]> kvp in m_BuiltinResourceMap)
                {
                    BuiltinResource[] list = kvp.Value;
                    for (int i = 0; i < list.Length; ++i)
                        if (list[i].m_InstanceID == instanceID)
                            return true;
                }
                return false;
            }

            private void InitBuiltinAssetType(System.Type type)
            {
                if (type == null)
                {
                    Debug.LogWarning("ObjectSelector::InitBuiltinAssetType: type is null!");
                    return;
                }
                string typeName = type.ToString().Substring(type.Namespace.Length + 1);

                var unityType = UnityType.FindTypeByName(typeName);
                if (unityType == null)
                {
                    Debug.LogWarning("ObjectSelector::InitBuiltinAssetType: class '" + typeName + "' not found");
                    return;
                }

                BuiltinResource[] resourceList = EditorGUIUtility.GetBuiltinResourceList(unityType.persistentTypeID);
                if (resourceList != null)
                    m_BuiltinResourceMap.Add(typeName, resourceList);
            }

            private bool ShouldGetAssetPreview(AssetReference assetReference)
            {
                string path = AssetDatabase.GUIDToAssetPath(assetReference.guid);
                if (m_AssetExtensionsPreviewIgnoreList.Contains(System.IO.Path.GetExtension(path).ToLowerInvariant()))
                    return false;
                Type assetDataType = InternalEditorUtility.GetTypeWithoutLoadingObject(assetReference.instanceID);
                if (m_AssetPreviewIgnoreList.Contains(assetDataType))
                    return false;
                return true;
            }

            public void InitBuiltinResources()
            {
                if (m_BuiltinResourceMap != null)
                    return;

                m_BuiltinResourceMap = new Dictionary<string, BuiltinResource[]>();

                if (m_ShowNoneItem)
                {
                    m_NoneList = new ExtraItem[1];
                    m_NoneList[0] = new ExtraItem();
                    m_NoneList[0].m_InstanceID = 0;
                    m_NoneList[0].m_Name = "None";
                }
                else
                {
                    m_NoneList = new ExtraItem[0];
                }

                // We don't show all built-in resources; just the ones where their type
                // makes sense. The actual lists are in ResourceManager.cpp,
                // GetBuiltinResourcesOfClass
                InitBuiltinAssetType(typeof(Mesh));
                InitBuiltinAssetType(typeof(Material));
                InitBuiltinAssetType(typeof(Texture2D));
                InitBuiltinAssetType(typeof(Font));
                InitBuiltinAssetType(typeof(Shader));
                InitBuiltinAssetType(typeof(Sprite));
                InitBuiltinAssetType(typeof(LightmapParameters));

                // PrintBuiltinResourcesAvailable();
            }

            public void PrintBuiltinResourcesAvailable()
            {
                string text = "";
                text += "ObjectSelector -Builtin Assets Available:\n";
                foreach (KeyValuePair<string, BuiltinResource[]> kvp in m_BuiltinResourceMap)
                {
                    BuiltinResource[] list = kvp.Value;
                    text += "    " + kvp.Key + ": ";
                    for (int i = 0; i < list.Length; ++i)
                    {
                        if (i != 0)
                            text += ", ";
                        text += list[i].m_Name;
                    }
                    text += "\n";
                }
                Debug.Log(text);
            }

            // Can return an index 1 past end of existing items (if newText is last in sort)
            public int IndexOfNewText(string newText, bool isCreatingNewFolder, bool foldersFirst)
            {
                int idx = 0;
                if (m_ShowNoneItem)
                    idx++;

                for (; idx < m_FilteredHierarchy.results.Length; ++idx)
                {
                    FilteredHierarchy.FilterResult r = m_FilteredHierarchy.results[idx];

                    // Skip folders when inserting a normal asset if folders is sorted first
                    if (foldersFirst && r.isFolder && !isCreatingNewFolder)
                        continue;

                    // When inserting a folder in folders first list break when we reach normal assets
                    if (foldersFirst && !r.isFolder && isCreatingNewFolder)
                        break;

                    // Use same name compare as when we sort in the backend: See AssetDatabase.cpp: SortChildren
                    string propertyPath = AssetDatabase.GetAssetPath(r.instanceID);
                    if (EditorUtility.NaturalCompare(System.IO.Path.GetFileNameWithoutExtension(propertyPath), newText) > 0)
                    {
                        return idx;
                    }
                }
                return idx;
            }

            public int IndexOf(int instanceID)
            {
                int idx = 0;

                // 1. 'none' first (has instanceID 0)
                if (m_ShowNoneItem)
                {
                    if (instanceID == 0)
                        return 0;
                    else
                        idx++;
                }
                else if (instanceID == 0)
                    return -1;

                // 2. Project assets
                foreach (FilteredHierarchy.FilterResult r in m_FilteredHierarchy.results)
                {
                    // When creating new asset we jump over that item (assuming we do not search for that new asset)
                    if (m_Owner.m_State.m_NewAssetIndexInList == idx)
                        idx++;

                    if (r.instanceID == instanceID)
                        return idx;
                    idx++;
                }

                // 3. Builtin resources
                foreach (BuiltinResource b in m_ActiveBuiltinList)
                {
                    if (instanceID == b.m_InstanceID)
                        return idx;
                    idx++;
                }
                return -1;
            }

            public FilteredHierarchy.FilterResult LookupByInstanceID(int instanceID)
            {
                if (instanceID == 0)
                    return null;

                int idx = 0;
                foreach (FilteredHierarchy.FilterResult r in m_FilteredHierarchy.results)
                {
                    // When creating new asset we jump over that item (assuming we do not search for that new asset)
                    if (m_Owner.m_State.m_NewAssetIndexInList == idx)
                        idx++;

                    if (r.instanceID == instanceID)
                        return r;
                    idx++;
                }
                return null;
            }

            // Returns true if index was valid. Note that instance can be 0 if 'None' item was found at index
            public bool AssetReferenceAtIndex(int index, out AssetReference assetReference)
            {
                assetReference = new AssetReference() { instanceID = 0 };
                if (index >= m_Grid.rows * m_Grid.columns)
                    return false;

                int idx = 0;

                // 1. 'none' first (has instanceID 0)
                if (m_ShowNoneItem)
                {
                    if (index == 0)
                        return true;
                    else
                        idx++;
                }

                // 2. Project assets
                foreach (FilteredHierarchy.FilterResult r in m_FilteredHierarchy.results)
                {
                    assetReference.instanceID = r.instanceID;
                    assetReference.guid = r.guid;
                    if (idx == index)
                        return true;
                    idx++;
                }

                // 3. Builtin resources
                foreach (BuiltinResource b in m_ActiveBuiltinList)
                {
                    assetReference.instanceID = b.m_InstanceID;
                    if (idx == index)
                        return true;
                    idx++;
                }

                // If last row and the row is not entirely filled
                // we just use the last item on that row
                if (index < m_Grid.rows * m_Grid.columns)
                    return true;

                return false;
            }

            public virtual void StartDrag(int draggedInstanceID, List<int> selectedInstanceIDs)
            {
                ProjectWindowUtil.StartDrag(draggedInstanceID, selectedInstanceIDs);
            }

            public DragAndDropVisualMode DoDrag(int dragToInstanceID, bool perform)
            {
                return DragAndDrop.DropOnProjectBrowserWindow(dragToInstanceID, AssetDatabase.GetAssetPath(dragToInstanceID), perform);
            }

            static internal int GetControlIDFromInstanceID(int instanceID)
            {
                return instanceID + 100000000;
            }

            public bool DoCharacterOffsetSelection()
            {
                if (Event.current.type == EventType.KeyDown && Event.current.shift && Event.current.character != 0)
                {
                    System.StringComparison ignoreCase = System.StringComparison.CurrentCultureIgnoreCase;
                    string startName = "";
                    if (Selection.activeObject != null)
                        startName = Selection.activeObject.name;

                    string c = new string(new[] {Event.current.character});
                    List<KeyValuePair<string, int>> list = GetVisibleNameAndInstanceIDs();
                    if (list.Count == 0)
                        return false;


                    // If same c is same start char as current selected then find current selected index
                    int startIndex = 0;
                    if (startName.StartsWith(c, ignoreCase))
                    {
                        // Iterate from there until next char is found
                        for (int i = 0; i < list.Count; ++i)
                        {
                            if (list[i].Key == startName)
                            {
                                startIndex = i + 1;
                            }
                        }
                    }

                    // Check all items starting with startIndex
                    for (int i = 0; i < list.Count; i++)
                    {
                        int index = (i + startIndex) % list.Count;

                        if (list[index].Key.StartsWith(c, ignoreCase))
                        {
                            Selection.activeInstanceID = list[index].Value;
                            m_Owner.Repaint();
                            return true;
                        }
                    }
                }

                return false;
            }

            public void ShowObjectsInList(int[] instanceIDs)
            {
                m_FilteredHierarchy = new FilteredHierarchy(HierarchyType.Assets);
                m_FilteredHierarchy.SetResults(instanceIDs);
            }

            internal void ShowObjectsInList(int[] instanceIDs, string[] rootPaths)
            {
                m_FilteredHierarchy = new FilteredHierarchy(HierarchyType.Assets);
                m_FilteredHierarchy.SetResults(instanceIDs, rootPaths);
            }

            public void DrawIconAndLabel(Rect rect, FilteredHierarchy.FilterResult filterItem, string label, Texture2D icon, bool selected, bool focus)
            {
                var color = filterItem == null ? GUI.color : ProjectBrowser.GetAssetItemColor(filterItem.instanceID);

                float vcPadding = s_VCEnabled ? k_ListModeVersionControlOverlayPadding : 0f;
                using (new GUI.ColorScope(color))
                {
                    rect.xMin += Styles.resultsLabel.margin.left;

                    if (filterItem != null)
                    {
                        var assetReference = new AssetReference() { instanceID = filterItem.instanceID };
                        assetReference.guid = filterItem.guid;
                        DynamicHintUtility.DrawHint(rect, Event.current.mousePosition, assetReference);
                    }

                    // Reduce the label width to allow delegate drawing on the right.
                    float delegateDrawWidth = (k_ListModeExternalIconPadding * 2) + k_IconWidth;
                    Rect delegateDrawRect = new Rect(rect.xMax - delegateDrawWidth, rect.y, delegateDrawWidth, rect.height);
                    Rect labelRect = new Rect(rect);
                    if (DrawExternalPostLabelInList(delegateDrawRect, filterItem))
                    {
                        labelRect.width = (rect.width - delegateDrawWidth);
                    }

                    Styles.resultsLabel.padding.left = (int)(vcPadding + k_IconWidth + k_SpaceBetweenIconAndText);
                    Styles.resultsLabel.Draw(labelRect, label, false, false, selected, focus);

                    Rect iconRect = rect;
                    iconRect.width = k_IconWidth;
                    iconRect.x += vcPadding * 0.5f;

                    if (selected && focus)
                    {
                        var activeIcon = EditorUtility.GetIconInActiveState(icon) as Texture2D;

                        if (activeIcon)
                            icon = activeIcon;
                    }

                    if (icon != null)
                        GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);
                }

                if (filterItem != null && filterItem.guid != null && filterItem.isMainRepresentation)
                {
                    Rect overlayRect = rect;
                    overlayRect.width = vcPadding + k_IconWidth;

                    if (null != postAssetIconDrawCallback)
                    {
                        postAssetIconDrawCallback(overlayRect, filterItem.guid, true);
                    }
                    ProjectHooks.OnProjectWindowItem(filterItem.guid, overlayRect, m_OwnerRepaintAction);
                }
            }

            private static bool DrawExternalPostLabelInList(Rect drawRect, FilteredHierarchy.FilterResult filterItem)
            {
                bool didDraw = false;
                if (filterItem != null && filterItem.guid != null && filterItem.isMainRepresentation)
                {
                    if (null != postAssetLabelDrawCallback)
                    {
                        didDraw = postAssetLabelDrawCallback(drawRect, filterItem.guid, true);
                    }
                }
                return didDraw;
            }

            class ItemFader
            {
                double m_FadeDuration = 0.3;
                double m_FirstToLastDuration = 0.3;
                double m_FadeStartTime;
                double m_TimeBetweenEachItem;
                List<int> m_InstanceIDs;

                public void Start(List<int> instanceIDs)
                {
                    m_InstanceIDs = instanceIDs;
                    m_FadeStartTime = EditorApplication.timeSinceStartup;
                    m_FirstToLastDuration = Math.Min(0.5, instanceIDs.Count * 0.03);
                    m_TimeBetweenEachItem = 0;
                    if (m_InstanceIDs.Count > 1)
                        m_TimeBetweenEachItem = m_FirstToLastDuration / (m_InstanceIDs.Count - 1);
                }

                public float GetAlpha(int instanceID)
                {
                    if (m_InstanceIDs == null)
                        return 1f;

                    if (EditorApplication.timeSinceStartup > m_FadeStartTime + m_FadeDuration + m_FirstToLastDuration)
                    {
                        m_InstanceIDs = null; // reset
                        return 1f;
                    }

                    int index = m_InstanceIDs.IndexOf(instanceID);
                    if (index >= 0)
                    {
                        double elapsed = EditorApplication.timeSinceStartup - m_FadeStartTime;
                        double itemStartTime = m_TimeBetweenEachItem * index;

                        float alpha = 0f;
                        if (itemStartTime < elapsed)
                        {
                            alpha = Mathf.Clamp((float)((elapsed - itemStartTime) / m_FadeDuration), 0f, 1f);
                        }
                        return alpha;
                    }
                    return 1f;
                }
            }
        }
    }
}  // namespace UnityEditor
