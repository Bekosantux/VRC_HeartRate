using UnityEngine;
using System.Collections.Generic;
using VRC.SDKBase;

#if UNITY_EDITOR
using UnityEditor;
using nadena.dev.ndmf.util;
#endif

namespace BekoShop.VRCHeartRate
{
    [HelpURL("https://bekosantux.github.io/shop-document/category/vrc-heart-rate/")]
    public class AutoModulePlacer : MonoBehaviour, IEditorOnly
    {
#if UNITY_EDITOR
        public enum OptionSlot
        {
            FX = 0,
            Additive = 1,
            Action = 2,
            Gesture = 3,
            Base = 4,
            Sitting = 5,
            TPose = 6,
            IKPose = 7
        }

        private const int OptionCount = 8;

        public static readonly string[] OptionLabels = new[]
        {
            "FX","Additive","Action","Gesture","Base","Sitting","TPose","IKPose"
        };

        [Header("Prefabs")]
        [SerializeField, Tooltip("�A�o�^�[����1�����z�u����e�v���n�u�i�K�{�j")]
        private GameObject parentContainerPrefab;

        [SerializeField, Tooltip("�e�I�v�V�����ɑΉ�����q�v���n�u")]
        private GameObject[] optionPrefabs = new GameObject[OptionCount];

        [Header("Options")]
        [SerializeField, Tooltip("�e�I�v�V������z�u���邩�ǂ���")]
        private bool[] optionEnabled = new bool[OptionCount];

        // ���
        private bool _isValidPlacement;
        private int _lastValidationFrame = -1;

        // Undo���̎����Ĕz�u�}���i�ÓI�F���C���X�^���X�Ƃ����L�j
        private static bool suppressAutoPlacement = false;

        // �x����������
        private bool _placementScheduled;
        // OnValidate ���_�� Undo �O���[�v
        private int _validateUndoGroup = -1;

        private void OnEnable()
        {
            if (!gameObject.scene.IsValid()) return;

            EnsureArrays();

            Undo.undoRedoPerformed -= OnUndoRedo;
            Undo.undoRedoPerformed += OnUndoRedo;

            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            EditorApplication.hierarchyChanged += OnHierarchyChanged;

            ValidateAndProcess();
        }

        private void OnDisable()
        {
            if (!gameObject.scene.IsValid()) return;

            CancelScheduledProcess();

            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
        }

        private void OnUndoRedo()
        {
            CancelScheduledProcess();

            suppressAutoPlacement = true;
            // �z�u�̑Ó��������͍X�V�i�Ĕz�u�͍s��Ȃ��j
            EnsureValidPlacement();
            suppressAutoPlacement = false;
        }

        private void OnHierarchyChanged()
        {
            EnsureValidPlacement();
        }

        private void OnValidate()
        {
            if (!CanRunEditorAutomation()) return;
            if (!gameObject.scene.IsValid()) return;

            EnsureArrays();

            // ���� Undo �O���[�v ID ���L�^
            _validateUndoGroup = Undo.GetCurrentGroup();

            // ���ɗ\��ς݂Ȃ�d���\�񂵂Ȃ�
            ScheduleDelayedValidateAndProcess();
        }

        public void ValidateAndProcess()
        {
            if (!EnsureValidPlacement()) return;
            if (suppressAutoPlacement) return;
            if (!CanRunEditorAutomation()) return;

            PlaceParentAndOptionsIfNeeded();
        }

        private void DelayedValidateAndProcess()
        {
            // �\�������
            EditorApplication.delayCall -= DelayedValidateAndProcess;
            _placementScheduled = false;

            // �I�u�W�F�N�g�������Ă����牽�����Ȃ�
            if (this == null) return;
            if (suppressAutoPlacement) return;
            if (BuildPipeline.isBuildingPlayer) return;

            // �V�����O���[�v���J��
            Undo.IncrementCurrentGroup();
            int myGroup = Undo.GetCurrentGroup();

            // �K�w������������
            PlaceParentAndOptionsIfNeeded();

            // OnValidate ���Ɏ擾�����O���[�v�ƌ�������1����ɂ܂Ƃ߂�
            if (_validateUndoGroup >= 0)
            {
                Undo.CollapseUndoOperations(_validateUndoGroup);
                _validateUndoGroup = -1;
            }
            else
            {
                Undo.CollapseUndoOperations(myGroup);
            }
        }

        // �O���A�N�Z�X�iEditor�p�j
        public GameObject GetParentContainerPrefab() => parentContainerPrefab;
        public void SetParentContainerPrefab(GameObject prefab) => parentContainerPrefab = prefab;

        public GameObject[] GetOptionPrefabs() => optionPrefabs;
        public void SetOptionPrefab(OptionSlot slot, GameObject prefab)
        {
            EnsureArrays();
            optionPrefabs[(int)slot] = prefab;
        }

        public bool[] GetOptionEnabled() => optionEnabled;
        public void SetOptionEnabled(OptionSlot slot, bool enabled)
        {
            EnsureArrays();
            optionEnabled[(int)slot] = enabled;
        }

        public bool IsValidPlacement() => EnsureValidPlacement();

        public bool HasAnyOptionEnabled()
        {
            if (optionEnabled == null) return false;
            for (int i = 0; i < optionEnabled.Length; i++)
            {
                if (optionEnabled[i]) return true;
            }
            return false;
        }

        public IEnumerable<int> EnabledSlots()
        {
            EnsureArrays();
            for (int i = 0; i < OptionCount; i++)
            {
                if (optionEnabled[i]) yield return i;
            }
        }

        // -------------------- �������� --------------------

        private bool EnsureValidPlacement()
        {
            if (_lastValidationFrame == Time.frameCount) return _isValidPlacement;
            _lastValidationFrame = Time.frameCount;

            try
            {
                string avatarRootPath = this.AvatarRootPath();
                _isValidPlacement = !string.IsNullOrEmpty(avatarRootPath);
            }
            catch
            {
                _isValidPlacement = false;
            }
            return _isValidPlacement;
        }

        private void PlaceParentAndOptionsIfNeeded()
        {
            if (parentContainerPrefab == null) return;

            // �A�o�^�[���[�g�̎擾
            Transform avatarRoot = GetAvatarRootTransform();
            if (avatarRoot == null)
            {
                Debug.LogWarning("AutoModulePlacer: Avatar root not found. Script must be placed inside an avatar hierarchy.", this);
                return;
            }

            // �����̐e�v���n�u���A�o�^�[���[�g���� GUID �Ō����i���D��T���j
            string wantedGuid = GetPrefabAssetGUID(parentContainerPrefab);
            Transform parentNode = FindPrefabInstanceByGUID(avatarRoot, wantedGuid);

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();

            bool createdParent = false;

            // �e�����݂��Ȃ��ꍇ�̂ݐV�K�����i�X�N���v�g�Ɠ��K�w�ɔz�u�j
            if (parentNode == null)
            {
                Transform siblingsParent = transform.parent;
                if (siblingsParent != null)
                {
                    var parentGO = PrefabUtility.InstantiatePrefab(parentContainerPrefab) as GameObject;
                    if (parentGO != null)
                    {
                        parentGO.transform.SetParent(siblingsParent);
                        parentGO.transform.localPosition = Vector3.zero;
                        parentGO.transform.localRotation = Quaternion.identity;
                        parentGO.transform.localScale = Vector3.one;
                        Undo.RegisterCreatedObjectUndo(parentGO, "Auto Place Parent Container");
                        parentNode = parentGO.transform;
                        createdParent = true;

                        Debug.Log($"AutoModulePlacer: Created new parent container prefab '{parentGO.name}'.", parentGO);
                    }
                }
            }

            // �e�����݂���Ȃ�q�̕s������z�u
            if (parentNode != null)
            {
                int createdCount = 0;
                List<GameObject> createdChildren = new List<GameObject>();

                foreach (int slot in EnabledSlots())
                {
                    var childPrefab = optionPrefabs[slot];
                    if (childPrefab == null) continue;

                    if (!ChildExistsByName(parentNode, childPrefab.name))
                    {
                        var childGO = PrefabUtility.InstantiatePrefab(childPrefab) as GameObject;
                        if (childGO != null)
                        {
                            childGO.transform.SetParent(parentNode);
                            childGO.transform.localPosition = Vector3.zero;
                            childGO.transform.localRotation = Quaternion.identity;
                            childGO.transform.localScale = Vector3.one;
                            Undo.RegisterCreatedObjectUndo(childGO, $"Auto Place Option {OptionLabels[slot]}");
                            createdCount++;
                            createdChildren.Add(childGO);
                        }
                    }
                }

                if (createdCount > 0)
                {
                    string parentType = createdParent ? "new" : "existing";
                    Debug.Log($"AutoModulePlacer: Added {createdCount} option prefab(s) to {parentType} parent container.", parentNode.gameObject);

                    foreach (var child in createdChildren)
                    {
                        Debug.Log($"AutoModulePlacer: Added option prefab '{child.name}'.", child);
                    }
                }
            }

            Undo.CollapseUndoOperations(group);
        }

        /// <summary>
        /// AvatarRootPath() ���g���ăA�o�^�[���[�g��Transform���擾
        /// </summary>
        private Transform GetAvatarRootTransform()
        {
            try
            {
                string avatarRootPath = this.AvatarRootPath();
                if (string.IsNullOrEmpty(avatarRootPath)) return null;

                Transform current = transform;

                if (avatarRootPath.StartsWith("/"))
                    avatarRootPath = avatarRootPath.Substring(1);

                string[] pathSegments = avatarRootPath.Split('/');
                // �p�X�̊K�w���������e��H��i�������g���܂ށj
                for (int i = 0; i < pathSegments.Length; i++)
                {
                    if (current == null || current.parent == null) return null;
                    current = current.parent;
                }

                return current;
            }
            catch
            {
                return null;
            }
        }

        private static string GetPrefabAssetGUID(GameObject prefabAsset)
        {
            if (prefabAsset == null) return null;
            var path = AssetDatabase.GetAssetPath(prefabAsset);
            if (string.IsNullOrEmpty(path)) return null;
            return AssetDatabase.AssetPathToGUID(path);
        }

        /// <summary>
        /// �w�肳�ꂽ���[�g���畝�D��Ńv���n�u�C���X�^���X��T�����AGUID����v������̂�Ԃ�
        /// </summary>
        private static Transform FindPrefabInstanceByGUID(Transform root, string wantedGuid)
        {
            if (root == null || string.IsNullOrEmpty(wantedGuid)) return null;

            var queue = new Queue<Transform>();
            queue.Enqueue(root);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                // current ���v���n�u�C���X�^���X�̃��[�g���`�F�b�N
                var instanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(current.gameObject);
                if (instanceRoot != null && instanceRoot == current.gameObject)
                {
                    string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(current.gameObject);
                    if (!string.IsNullOrEmpty(path))
                    {
                        string guid = AssetDatabase.AssetPathToGUID(path);
                        if (guid == wantedGuid) return current;
                    }
                }

                for (int i = 0; i < current.childCount; i++)
                {
                    queue.Enqueue(current.GetChild(i));
                }
            }

            return null;
        }

        private static bool ChildExistsByName(Transform parent, string childName)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                if (parent.GetChild(i).name == childName) return true;
            }
            return false;
        }

        private bool CanRunEditorAutomation()
        {
            if (EditorApplication.isPlaying) return false;
            if (BuildPipeline.isBuildingPlayer) return false;
            return true;
        }

        private void ScheduleDelayedValidateAndProcess()
        {
            if (_placementScheduled) return;
            _placementScheduled = true;
            EditorApplication.delayCall += DelayedValidateAndProcess;
        }

        private void CancelScheduledProcess()
        {
            if (!_placementScheduled) return;
            EditorApplication.delayCall -= DelayedValidateAndProcess;
            _placementScheduled = false;
        }

        private void EnsureArrays()
        {
            if (optionPrefabs == null || optionPrefabs.Length != OptionCount)
            {
                var old = optionPrefabs;
                optionPrefabs = new GameObject[OptionCount];
                if (old != null)
                {
                    int copy = Mathf.Min(OptionCount, old.Length);
                    for (int i = 0; i < copy; i++) optionPrefabs[i] = old[i];
                }
            }

            if (optionEnabled == null || optionEnabled.Length != OptionCount)
            {
                var old = optionEnabled;
                optionEnabled = new bool[OptionCount];
                if (old != null)
                {
                    int copy = Mathf.Min(OptionCount, old.Length);
                    for (int i = 0; i < copy; i++) optionEnabled[i] = old[i];
                }
            }
        }
#endif
    }
}
