// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System;
using UnityEditor.IMGUI.Controls;
using UnityEditor.ProjectWindowCallback;
using UnityEngine;
using UnityEditorInternal.VersionControl;
using UnityEditor.VersionControl;
using System.Collections.Generic;
using UnityEditor.Experimental;
using UnityEngine.Assertions;
using static UnityEditor.AssetsTreeViewDataSource;
using static UnityEditorInternal.InternalEditorUtility;

using TreeViewController = UnityEditor.IMGUI.Controls.TreeViewController<int>;
using TreeViewItem = UnityEditor.IMGUI.Controls.TreeViewItem<int>;
using TreeViewGUI = UnityEditor.IMGUI.Controls.TreeViewGUI<int>;
using TreeViewState = UnityEditor.IMGUI.Controls.TreeViewState<int>;


namespace UnityEditor
{
    internal class AssetsTreeViewGUI : TreeViewGUI
    {
        static bool s_VCEnabled;
        const float k_IconOverlayPadding = 7f;

        internal static ScalableGUIContent s_OpenFolderIcon = new ScalableGUIContent(null, null, EditorResources.openedFolderIconName);
        internal static ScalableGUIContent s_EmptyFolderIcon = new ScalableGUIContent(null, null, EditorResources.emptyFolderIconName);

        internal static Texture2D openFolderTexture
        {
            get
            {
                GUIContent folderContent = s_OpenFolderIcon;
                return folderContent.image as Texture2D;
            }
        }

        internal static Texture2D emptyFolderTexture
        {
            get
            {
                GUIContent folderContent = s_EmptyFolderIcon;
                return folderContent.image as Texture2D;
            }
        }

        internal delegate void OnAssetIconDrawDelegate(Rect iconRect, string guid);
        internal static event OnAssetIconDrawDelegate postAssetIconDrawCallback = null;

        internal delegate bool OnAssetLabelDrawDelegate(Rect drawRect, string guid);
        internal static event OnAssetLabelDrawDelegate postAssetLabelDrawCallback = null;

        private static IDictionary<int, string> s_GUIDCache = null;
        private readonly Action m_TreeViewRepaintAction;

        public AssetsTreeViewGUI(TreeViewController treeView)
            : base(treeView)
        {
            iconOverlayGUI += OnIconOverlayGUI;
            labelOverlayGUI += OnLabelOverlayGUI;
            k_TopRowMargin = 4f;
            m_TreeViewRepaintAction = () => m_TreeView.Repaint();
        }

        // ---------------------
        // OnGUI section

        override public void BeginRowGUI()
        {
            s_VCEnabled = VersionControlUtils.isVersionControlConnected;
            iconLeftPadding = iconRightPadding = s_VCEnabled ? k_IconOverlayPadding : 0f;
            base.BeginRowGUI();
        }

        //-------------------
        // Create asset and Rename asset section

        protected CreateAssetUtility GetCreateAssetUtility()
        {
            return ((TreeViewStateWithAssetUtility)m_TreeView.state).createAssetUtility;
        }

        virtual protected bool IsCreatingNewAsset(int instanceID)
        {
            return GetCreateAssetUtility().IsCreatingNewAsset() && IsRenaming(instanceID);
        }

        override protected void ClearRenameAndNewItemState()
        {
            GetCreateAssetUtility().Clear();
            base.ClearRenameAndNewItemState();
        }

        override protected void RenameEnded()
        {
            string name = string.IsNullOrEmpty(GetRenameOverlay().name) ? GetRenameOverlay().originalName : GetRenameOverlay().name;
            int instanceID = GetRenameOverlay().userData;
            bool isCreating = GetCreateAssetUtility().IsCreatingNewAsset();
            bool userAccepted = GetRenameOverlay().userAcceptedRename;

            if (userAccepted)
            {
                if (isCreating)
                {
                    // Create a new asset
                    GetCreateAssetUtility().EndNewAssetCreation(name);
                }
                else
                {
                    // Rename an existing asset
                    ObjectNames.SetNameSmartWithInstanceID(instanceID, name);
                }
            }
            else if (isCreating)
                GetCreateAssetUtility().EndNewAssetCreationCanceled(name);
        }

        override protected void SyncFakeItem()
        {
            if (!m_TreeView.data.HasFakeItem() && GetCreateAssetUtility().IsCreatingNewAsset())
            {
                int parentInstanceID = AssetDatabase.GetMainAssetInstanceID(GetCreateAssetUtility().folder);
                m_TreeView.data.InsertFakeItem(GetCreateAssetUtility().instanceID, parentInstanceID, GetCreateAssetUtility().originalName, GetCreateAssetUtility().icon);
            }

            if (m_TreeView.data.HasFakeItem() && !GetCreateAssetUtility().IsCreatingNewAsset())
            {
                m_TreeView.data.RemoveFakeItem();
            }
        }

        // Not part of interface because it is very specific to creating assets
        virtual public void BeginCreateNewAsset(int instanceID, EndNameEditAction endAction, string pathName, Texture2D icon, string resourceFile, bool selectAssetBeingCreated = true)
        {
            ClearRenameAndNewItemState();

            if (GetCreateAssetUtility().BeginNewAssetCreation(instanceID, endAction, pathName, icon, resourceFile, selectAssetBeingCreated))
            {
                SyncFakeItem();

                // Start nameing the asset
                bool renameStarted = GetRenameOverlay().BeginRename(GetCreateAssetUtility().originalName, instanceID, 0f);
                if (!renameStarted)
                    Debug.LogError("Rename not started (when creating new asset)");
            }
        }

        // Handles fetching rename icon or cached asset database icon
        protected override Texture GetIconForItem(TreeViewItem item)
        {
            if (item == null)
                return null;

            Texture icon = null;
            if (IsCreatingNewAsset(item.id))
                icon = GetCreateAssetUtility().icon;

            if (icon == null)
                icon = item.icon;

            if (icon == null && item.id != 0)
            {
                string path = AssetDatabase.GetAssetPath(item.id);
                icon = AssetDatabase.GetCachedIcon(path);
            }

            var folderItem = item as AssetsTreeViewDataSource.FolderTreeItemBase;
            if (folderItem != null)
            {
                if (folderItem.IsEmpty)
                    icon = emptyFolderTexture;
                else if (m_TreeView.data.IsExpanded(folderItem))
                    icon = openFolderTexture;
            }

            return icon;
        }

        protected override void DoItemGUI(Rect rect, int row, TreeViewItem item, bool selected, bool focused, bool useBoldFont)
        {
            if (item is AssetsTreeViewDataSource.RootTreeItem)
            {
                useBoldFont = true;
            }

            var color = ProjectBrowser.GetAssetItemColor(item.id);

            using (new GUI.ColorScope(color))
                base.DoItemGUI(rect, row, item, selected, focused, useBoldFont);
        }

        private void OnIconOverlayGUI(TreeViewItem item, Rect overlayRect)
        {
            if (!AssetReference.IsAssetImported(item.id))
            {
                var assetTreeItem = item as IAssetTreeViewItem;
                if (assetTreeItem == null)
                    return;

                OnIconOverlayGUI_ForNonImportAsset(assetTreeItem.Guid, overlayRect, false, m_TreeViewRepaintAction);
            }
            else
                OnIconOverlayGUI(item.id, overlayRect, false, m_TreeViewRepaintAction);
        }

        internal static void OnIconOverlayGUI_ForNonImportAsset(string guid, Rect overlayRect, bool addPadding, Action repaintAction = null)
        {
            if (addPadding)
            {
                overlayRect.x -= k_IconOverlayPadding;
                overlayRect.width += k_IconOverlayPadding * 2;
            }

            ProjectHooks.OnProjectWindowItem(guid, overlayRect, repaintAction);
        }

        internal static void OnIconOverlayGUI(int instanceID, Rect overlayRect, bool addPadding, Action repaintAction = null)
        {
            if (addPadding)
            {
                overlayRect.x -= k_IconOverlayPadding;
                overlayRect.width += k_IconOverlayPadding * 2;
            }

            if (postAssetIconDrawCallback != null && AssetDatabase.IsMainAsset(instanceID))
            {
                string guid = GetGUIDForInstanceID(instanceID);
                postAssetIconDrawCallback(overlayRect, guid);
            }

            // Draw vcs icons
            if (s_VCEnabled && AssetDatabase.IsMainAsset(instanceID))
            {
                string guid = GetGUIDForInstanceID(instanceID);
                ProjectHooks.OnProjectWindowItem(guid, overlayRect, repaintAction);
            }
        }

        private void OnLabelOverlayGUI(TreeViewItem item, Rect labelRect)
        {
            if (postAssetLabelDrawCallback != null && AssetDatabase.IsMainAsset(item.id))
            {
                string guid = GetGUIDForInstanceID(item.id);
                postAssetLabelDrawCallback(labelRect, guid);
            }
        }

        // Returns a previously stored GUID for the given ID,
        // else retrieves it from the asset database and stores it.
        private static string GetGUIDForInstanceID(int instanceID)
        {
            if (s_GUIDCache == null)
            {
                s_GUIDCache = new Dictionary<int, string>();
            }

            string GUID = null;
            if (!s_GUIDCache.TryGetValue(instanceID, out GUID))
            {
                string path = AssetDatabase.GetAssetPath(instanceID);
                GUID = AssetDatabase.AssetPathToGUID(path);
                Assert.IsTrue(!string.IsNullOrEmpty(GUID));
                s_GUIDCache.Add(instanceID, GUID);
            }

            return GUID;
        }
    }


    [System.Serializable]
    internal class TreeViewStateWithAssetUtility : TreeViewState
    {
        [SerializeField]
        CreateAssetUtility m_CreateAssetUtility = new CreateAssetUtility();

        internal CreateAssetUtility createAssetUtility { get { return m_CreateAssetUtility; } set { m_CreateAssetUtility = value; } }

        internal override void OnAwake()
        {
            base.OnAwake();

            // Clear state that should not survive closing/starting Unity (If TreeViewState is in EditorWindow that are serialized in a layout file)
            m_CreateAssetUtility.Clear();
        }
    }
}
