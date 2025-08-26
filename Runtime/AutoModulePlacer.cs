using UnityEngine;
using System.Collections.Generic;
using VRC.SDKBase;

#if UNITY_EDITOR
using UnityEditor;
using nadena.dev.ndmf.util;
#endif

namespace BekoShop.VRCHeartRate
{
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

        public static readonly string[] OptionLabels = new[]
        {
            "FX","Additive","Action","Gesture","Base","Sitting","TPose","IKPose"
        };

        [Header("Prefabs")]
        [SerializeField, Tooltip("�A�o�^�[����1�����z�u����e�v���n�u�i�K�{�j")]
        private GameObject parentContainerPrefab;

        [SerializeField, Tooltip("�e�I�v�V�����ɑΉ�����q�v���n�u")]
        private GameObject[] optionPrefabs = new GameObject[8];

        [Header("Options")]
        [SerializeField, Tooltip("�e�I�v�V������z�u���邩�ǂ���")]
        private bool[] optionEnabled = new bool[8];

        // ���
        private bool _isValidPlacement = false;
        private int _lastValidationFrame = -1;

        // Undo���̎����Ĕz�u�}��
        private static bool suppressAutoPlacement = false;

        private bool _placementScheduled;
        // OnValidate ���Ă΂ꂽ���_�� Undo �O���[�v
        private int _validateUndoGroup = -1;

        private void OnEnable()
        {
            if (!gameObject.scene.IsValid()) return;

            Undo.undoRedoPerformed -= OnUndoRedo;
            Undo.undoRedoPerformed += OnUndoRedo;

            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            EditorApplication.hierarchyChanged += OnHierarchyChanged;

            ValidateAndProcess();
        }

        private void OnDisable()
        {
            if (!gameObject.scene.IsValid()) return;

            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
        }

        private void OnUndoRedo()
        {
            // �\�񂵂Ă����x���Ăяo�����L�����Z��
            if (_placementScheduled)
            {
                EditorApplication.delayCall -= DelayedValidateAndProcess;
                _placementScheduled = false;
            }

            suppressAutoPlacement = true;
            ValidateAndProcess();        // �����ł͍Ĕz�u���Ȃ�
            suppressAutoPlacement = false;
        }

        private void OnHierarchyChanged()
        {
            EnsureValidPlacement();
        }

        private void OnValidate()
        {
            if (EditorApplication.isPlaying) return;
            if (BuildPipeline.isBuildingPlayer) return;
            if (!gameObject.scene.IsValid()) return;

            // ���� Undo �O���[�v ID ���L�^���Ă���
            _validateUndoGroup = Undo.GetCurrentGroup();

            // ���ɗ\��ς݂Ȃ�d���\�񂵂Ȃ�
            if (!_placementScheduled)
            {
                _placementScheduled = true;
                EditorApplication.delayCall += DelayedValidateAndProcess;
            }
        }

        public void ValidateAndProcess()
        {
            if (!EnsureValidPlacement()) return;
            if (suppressAutoPlacement) return;
            if (EditorApplication.isPlaying) return;
            if (BuildPipeline.isBuildingPlayer) return; // �r���h���͎��s���Ȃ�

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
            if (BuildPipeline.isBuildingPlayer) return; // �r���h���͎��s���Ȃ�

            // �V�����O���[�v���J��
            Undo.IncrementCurrentGroup();
            int myGroup = Undo.GetCurrentGroup();

            // �����ŊK�w������������
            PlaceParentAndOptionsIfNeeded();

            /* �d�v�I
               OnValidate ���Ɏ擾�����O���[�v�ƌ�������
               �u���[�U����{�����z�u�v���P�̑���ɂ܂Ƃ߂� */
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
        public void SetOptionPrefab(OptionSlot slot, GameObject prefab) => optionPrefabs[(int)slot] = prefab;

        public bool[] GetOptionEnabled() => optionEnabled;
        public void SetOptionEnabled(OptionSlot slot, bool enabled) => optionEnabled[(int)slot] = enabled;

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
            for (int i = 0; i < 8; i++)
            {
                if (optionEnabled != null && i < optionEnabled.Length && optionEnabled[i]) yield return i;
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
                if (string.IsNullOrEmpty(avatarRootPath))
                {
                    _isValidPlacement = false;
                    return _isValidPlacement;
                }

                _isValidPlacement = true;
                return _isValidPlacement;
            }
            catch (System.Exception)
            {
                _isValidPlacement = false;
                return _isValidPlacement;
            }
        }

        private void PlaceParentAndOptionsIfNeeded()
        {
            if (parentContainerPrefab == null) return;

            // �A�o�^�[���[�g�̎擾
            Transform avatarRoot = GetAvatarRootTransform();
            if (avatarRoot == null) 
            {
                Debug.LogWarning("AutoAssetPlacer: Avatar root not found. Script must be placed inside an avatar hierarchy.", this);
                return; // �A�o�^�[���[�g��������Ȃ��ꍇ�͏����𒆎~
            }

            // �����̐e�v���n�u���A�o�^�[���[�g���� GUID �Ō����i���D��T���j
            string wantedGuid = GetPrefabAssetGUID(parentContainerPrefab);
            Transform parentNode = FindPrefabInstanceByGUID(avatarRoot, wantedGuid);

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();

            bool isNewParent = false;

            // �e�����݂��Ȃ��ꍇ�̂ݐV�K�����i�X�N���v�g�Ɠ��K�w�ɔz�u�j
            if (parentNode == null)
            {
                Transform siblingsParent = transform.parent;
                if (siblingsParent != null)
                {
                    GameObject parentGO = PrefabUtility.InstantiatePrefab(parentContainerPrefab) as GameObject;
                    if (parentGO != null)
                    {
                        parentGO.transform.SetParent(siblingsParent);
                        parentGO.transform.localPosition = Vector3.zero;
                        parentGO.transform.localRotation = Quaternion.identity;
                        parentGO.transform.localScale = Vector3.one;
                        Undo.RegisterCreatedObjectUndo(parentGO, "Auto Place Parent Container");
                        parentNode = parentGO.transform;
                        isNewParent = true;

                        // ���O��Context�𐶐����ꂽ�I�u�W�F�N�g���g�ɕύX
                        Debug.Log($"AutoAssetPlacer: Created new parent container prefab '{parentGO.name}'.", parentGO);
                    }
                }
            }
            else
            {
                // �����̐e�v���n�u���g�p����ꍇ�̃��O�iContext�������I�u�W�F�N�g�ɕύX�j
                Debug.Log($"AutoAssetPlacer: Using existing parent container prefab '{parentNode.name}'.", parentNode.gameObject);
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

                    // �e�̒����ɓ����I�u�W�F�N�g�����݂��邩�m�F
                    bool exists = false;
                    for (int i = 0; i < parentNode.childCount; i++)
                    {
                        var child = parentNode.GetChild(i);
                        if (child.name == childPrefab.name)
                        {
                            exists = true;
                            break;
                        }
                    }

                    if (!exists)
                    {
                        GameObject childGO = PrefabUtility.InstantiatePrefab(childPrefab) as GameObject;
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
                    string parentType = isNewParent ? "new" : "existing";
                    Debug.Log($"AutoAssetPlacer: Added {createdCount} option prefab(s) to {parentType} parent container.", parentNode.gameObject);

                    // �e�q�I�u�W�F�N�g�ɂ��ʃ��O�iContext���e�q�I�u�W�F�N�g�ɐݒ�j
                    foreach (var child in createdChildren)
                    {
                        Debug.Log($"AutoAssetPlacer: Added option prefab '{child.name}'.", child);
                    }
                }
            }

            Undo.CollapseUndoOperations(group);
        }

        /// <summary>
        /// AvatarRootPath() ���g���ăA�o�^�[���[�g��Transform���擾
        /// this.AvatarRootPath() �̓X�N���v�g�v���n�u�̃A�o�^�[���[�g����̑��΃p�X��Ԃ����߁A
        /// ���ۂ̃A�o�^�[���[�g�I�u�W�F�N�g�������ĕԂ�
        /// </summary>
        private Transform GetAvatarRootTransform()
        {
            try
            {
                string avatarRootPath = this.AvatarRootPath();
                if (string.IsNullOrEmpty(avatarRootPath))
                    return null;

                // �X�N���v�g�v���n�u����e��H���ăA�o�^�[���[�g��������
                Transform current = transform;

                // AvatarRootPath() �Ŏ擾�����p�X�̊K�w���������e��H��
                // �p�X�� "/" �Ŏn�܂�ꍇ�͏���
                if (avatarRootPath.StartsWith("/"))
                    avatarRootPath = avatarRootPath.Substring(1);

                string[] pathSegments = avatarRootPath.Split('/');

                // �p�X�̊K�w���������e��H��i�������g���܂ށj
                for (int i = 0; i < pathSegments.Length; i++)
                {
                    if (current.parent == null)
                        return null; // �e���Ȃ��ꍇ�͎��s
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

                // current���v���n�u�C���X�^���X�̃��[�g���`�F�b�N
                var prefabRoot = PrefabUtility.GetNearestPrefabInstanceRoot(current.gameObject);
                if (prefabRoot != null && prefabRoot == current.gameObject)
                {
                    // �v���n�u�A�Z�b�g��GUID���擾
                    string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(current.gameObject);
                    if (!string.IsNullOrEmpty(path))
                    {
                        string guid = AssetDatabase.AssetPathToGUID(path);
                        if (guid == wantedGuid)
                        {
                            return current;
                        }
                    }
                }

                // �q�v�f���L���[�ɒǉ�
                for (int i = 0; i < current.childCount; i++)
                {
                    queue.Enqueue(current.GetChild(i));
                }
            }

            return null;
        }
#endif
    }
}