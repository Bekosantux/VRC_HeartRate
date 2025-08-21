using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.SceneManagement;
using nadena.dev.ndmf.util;
#endif

namespace BekoShop.VRCHeartRate
{
    // �A�o�^�[���ɐe�v���n�u��1�z�u���A���̎q�Ƃ��đI�����ꂽ�I�v�V�����v���n�u��s���������z�u����
    public class AutoAssetPlacer : MonoBehaviour, VRC.SDKBase.IEditorOnly
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

        // Editor ����Q�Ƃ��邽�� public
        public static readonly string[] OptionLabels = new[]
        {
            "FX","Additive","Action","Gesture","Base","Sitting","TPose","IKPose"
        };

        [Header("Prefabs")]
        [SerializeField, Tooltip("�A�o�^�[����1�����z�u����e�v���n�u�i�K�{�j")]
        private GameObject parentContainerPrefab;

        [SerializeField, Tooltip("�e�I�v�V�����ɑΉ�����q�v���n�u�i�v�f��8�E�C���f�b�N�X�� OptionSlot �ɑΉ��j")]
        private GameObject[] optionPrefabs = new GameObject[8];

        [Header("Options")]
        [SerializeField, Tooltip("�e�I�v�V������z�u���邩�ǂ����i�v�f��8�E�C���f�b�N�X�� OptionSlot �ɑΉ��j")]
        private bool[] optionEnabled = new bool[8];

        // ���
        private bool _isValidPlacement = false;
        private string _statusMessage = "";
        private int _lastValidationFrame = -1;

        // Undo���̎����Ĕz�u�}��
        private static bool suppressAutoPlacement = false;

        private void OnEnable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            Undo.undoRedoPerformed += OnUndoRedo;

            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            EditorApplication.hierarchyChanged += OnHierarchyChanged;

            ValidateAndProcess();
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
        }

        private void OnUndoRedo()
        {
            suppressAutoPlacement = true;
            ValidateAndProcess();
            suppressAutoPlacement = false;
        }

        private void OnHierarchyChanged()
        {
            EnsureValidPlacement();
        }

        private void Start()
        {
            if (!Application.isPlaying) ValidateAndProcess();
        }

        private void OnValidate()
        {
            if (Application.isPlaying) return;
            // �z�񒷂͌Œ�z��̂��ߕۏ؏����͍s��Ȃ�
            ValidateAndProcess();
        }

        public void ValidateAndProcess()
        {
            if (!EnsureValidPlacement()) return;
            if (suppressAutoPlacement) return;

            PlaceParentAndOptionsIfNeeded();
        }

        // �O���A�N�Z�X�iEditor�p�j
        public GameObject GetParentContainerPrefab() => parentContainerPrefab;
        public void SetParentContainerPrefab(GameObject prefab) => parentContainerPrefab = prefab;

        public GameObject[] GetOptionPrefabs() => optionPrefabs;
        public void SetOptionPrefab(OptionSlot slot, GameObject prefab) => optionPrefabs[(int)slot] = prefab;

        public bool[] GetOptionEnabled() => optionEnabled;
        public void SetOptionEnabled(OptionSlot slot, bool enabled) => optionEnabled[(int)slot] = enabled;

        public bool IsValidPlacement() => EnsureValidPlacement();
        public string GetStatusMessage()
        {
            EnsureValidPlacement();
            return _statusMessage;
        }

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
                    _statusMessage = "�A�o�^�[�̓����ɔz�u���Ă��������B\nPlease place this inside the avatar.";
                    return _isValidPlacement;
                }

                _isValidPlacement = true;
                _statusMessage = "���̃X�N���v�g�͍폜���Ȃ��ł��������B\nPlease don't delete this script.";
                return _isValidPlacement;
            }
            catch (System.Exception ex)
            {
                _isValidPlacement = false;
                _statusMessage = $"Error validating avatar root: {ex.Message}";
                return _isValidPlacement;
            }
        }

        private void PlaceParentAndOptionsIfNeeded()
        {
            // �e�v���n�u�K�{�i�t�H�[���o�b�N�����͂��Ȃ��j
            if (parentContainerPrefab == null) return;

            // �e�̓X�N���v�g�v���n�u�Ɠ��K�w�i�����e�̎q�j�ɔz�u����
            Transform siblingsParent = transform.parent;
            if (siblingsParent == null) return;

            // �����̐e�v���n�u�� GUID �Ō����i���K�w���̂݁j
            string wantedGuid = GetPrefabAssetGUID(parentContainerPrefab);
            Transform parentNode = FindSiblingPrefabInstanceByGUID(siblingsParent, wantedGuid);

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();

            // �e�����݂��Ȃ��ꍇ�͐���
            if (parentNode == null)
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
                }
            }

            // �e�����݂���Ȃ�q�̕s������z�u
            if (parentNode != null)
            {
                int createdCount = 0;

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
                        }
                    }
                }

                if (createdCount > 0)
                {
                    Debug.Log($"AutoAssetPlacer: {createdCount} �̃I�v�V�����v���n�u�������z�u���܂����B", this);
                }
            }

            Undo.CollapseUndoOperations(group);
        }

        private static string GetPrefabAssetGUID(GameObject prefabAsset)
        {
            if (prefabAsset == null) return null;
            var path = AssetDatabase.GetAssetPath(prefabAsset);
            if (string.IsNullOrEmpty(path)) return null;
            return AssetDatabase.AssetPathToGUID(path);
        }

        private static Transform FindSiblingPrefabInstanceByGUID(Transform siblingsParent, string wantedGuid)
        {
            if (siblingsParent == null || string.IsNullOrEmpty(wantedGuid)) return null;

            for (int i = 0; i < siblingsParent.childCount; i++)
            {
                var child = siblingsParent.GetChild(i);

                // ���� child ���v���n�u�C���X�^���X�̃��[�g���m�F
                var root = PrefabUtility.GetNearestPrefabInstanceRoot(child.gameObject);
                if (root == null || root != child.gameObject) continue;

                // ���̃v���n�u�C���X�^���X�̌��A�Z�b�gGUID���擾
                string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(child.gameObject);
                if (string.IsNullOrEmpty(path)) continue;

                string guid = AssetDatabase.AssetPathToGUID(path);
                if (guid == wantedGuid)
                {
                    return child;
                }
            }
            return null;
        }
#endif
    }
}