using UnityEngine;
using System.Collections.Generic;
using System.Linq;
#if UNITY_EDITOR
using nadena.dev.ndmf.util;
using UnityEditor;
#endif

namespace BekoShop.VRCHeartRate
{
    [ExecuteAlways]
    public class AutoAssetPlacer : MonoBehaviour, VRC.SDKBase.IEditorOnly
    {
#if UNITY_EDITOR
        // �萔��`
        private const string MESSAGE_DELETE_WARNING = "���̃X�N���v�g�͍폜���Ȃ��ł��������B\nDon't delete this script.";
        private const string MESSAGE_OUTSIDE_AVATAR = "�A�o�^�[�̓����ɔz�u���Ă��������B\nPlace this inside the avatar.";

        [SerializeField, HideInInspector]
        private List<GameObject> _targetPrefabs = new List<GameObject>();

        // public �v���p�e�B�͒��ڃt�B�[���h�ɂ���i�p�t�H�[�}���X����j
        public List<GameObject> targetPrefabs => _targetPrefabs;

        // �L���b�V�����邽��readonly
        private bool _isValidPlacement = false;
        private string _errorMessage = "";

        // ��ԃL���b�V���̗L������
        private int _lastValidationFrame = -1;

        // Undo/Redo�Ŏ����z�u���Ȃ��悤�ɏ�Ԃ�ێ�
        private static bool suppressAutoPlacement = false;

        private void OnEnable()
        {
            // �C�x���g����x�N���A���Ă���o�^�i�d���o�^�h�~�j
            UnityEditor.Undo.undoRedoPerformed -= OnUndoRedo;
            UnityEditor.Undo.undoRedoPerformed += OnUndoRedo;

            UnityEditor.EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            UnityEditor.EditorApplication.hierarchyChanged += OnHierarchyChanged;

            // ���񌟏�
            ValidateAndProcess();
        }

        private void OnDisable()
        {
            // �C�x���g������Y��Ȃ�
            UnityEditor.Undo.undoRedoPerformed -= OnUndoRedo;
            UnityEditor.EditorApplication.hierarchyChanged -= OnHierarchyChanged;
        }

        private void OnUndoRedo()
        {
            suppressAutoPlacement = true;
            ValidateAndProcess();
            suppressAutoPlacement = false;
        }

        private void OnHierarchyChanged()
        {
            // ���؂̂ݍs���A���ʂ��L���b�V���i_lastValidationFrame���X�V�j
            ValidateAvatarRootPlacement();

            // �K�v�ȃr���[�̂ݍĕ`��
            EditorUtility.SetDirty(this);
        }

        private void Start()
        {
            if (!Application.isPlaying) ValidateAndProcess();
        }

        private void OnValidate()
        {
            if (Application.isPlaying) return;

            // �x�����s�Ōx�����o���Ȃ��悤�ɂ���
            EditorApplication.delayCall += _OnValidate;
        }

        private void _OnValidate()
        {
            EditorApplication.delayCall -= _OnValidate;

            RemoveDuplicatesFromList();
            ValidateAndProcess();
        }

        public void ValidateAndProcess()
        {
            if (!EnsureValidPlacement()) return;

            if (!suppressAutoPlacement)
            {
                CheckAndPlaceObjects();
            }
        }

        /// <summary>
        /// �A�o�^�[Root���؂��s�����ʂ��L���b�V�����܂�
        /// </summary>
        private bool EnsureValidPlacement()
        {
            // ����t���[�����ł͍Č��؂��Ȃ��i�p�t�H�[�}���X����j
            if (_lastValidationFrame == Time.frameCount)
                return _isValidPlacement;

            return ValidateAvatarRootPlacement();
        }

        private bool ValidateAvatarRootPlacement()
        {
            _lastValidationFrame = Time.frameCount;

            try
            {
                string avatarRootPath = this.AvatarRootPath();

                if (string.IsNullOrEmpty(avatarRootPath))
                {
                    _isValidPlacement = false;
                    _errorMessage = MESSAGE_OUTSIDE_AVATAR;
                    return false;
                }

                _isValidPlacement = true;
                _errorMessage = MESSAGE_DELETE_WARNING;
                return true;
            }
            catch (System.Exception ex)
            {
                _isValidPlacement = false;
                _errorMessage = $"Error validating avatar root: {ex.Message}";
                return false;
            }
        }

        private void CheckAndPlaceObjects()
        {
            if (_targetPrefabs == null || _targetPrefabs.Count == 0) return;

            Transform parentTransform = transform.parent;
            if (parentTransform == null) return;

            // ���O�ɔz��T�C�Y���m�ۂ��� GC Alloc �����炷
            List<GameObject> prefabsToPlace = new List<GameObject>(_targetPrefabs.Count);

            foreach (GameObject prefab in _targetPrefabs)
            {
                if (prefab == null) continue;

                bool objectExists = false;
                int childCount = parentTransform.childCount;

                for (int i = 0; i < childCount; i++)
                {
                    Transform child = parentTransform.GetChild(i);
                    if (child == transform) continue;
                    if (child.name == prefab.name)
                    {
                        objectExists = true;
                        break;
                    }
                }

                if (!objectExists)
                {
                    prefabsToPlace.Add(prefab);
                }
            }

            if (prefabsToPlace.Count > 0)
            {
                PlaceNewObjects(prefabsToPlace);
            }
        }

        private void PlaceNewObjects(List<GameObject> prefabsToPlace)
        {
            if (prefabsToPlace.Count == 0) return;

            // �P��� Undo �O���[�v�ŏ���
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();

            int createdCount = 0;
            Transform parent = transform.parent;

            foreach (GameObject prefab in prefabsToPlace)
            {
                if (prefab == null) continue;

                GameObject newObject = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                if (newObject == null) continue;

                if (parent != null)
                {
                    newObject.transform.SetParent(parent);
                }

                newObject.transform.localPosition = Vector3.zero;
                newObject.transform.localRotation = Quaternion.identity;
                newObject.transform.localScale = Vector3.one;

                Undo.RegisterCreatedObjectUndo(newObject, "Auto Place Objects");
                createdCount++;
            }

            Undo.CollapseUndoOperations(group);

            if (createdCount > 0)
            {
                Debug.Log($"AutoAssetPlacer: {createdCount}�̃I�u�W�F�N�g�������z�u���܂����B");
            }
        }

        private void RemoveDuplicatesFromList()
        {
            if (_targetPrefabs == null) return;

            // �K�v�ȏꍇ�̂ݐV�������X�g���쐬
            bool hasNull = false;
            bool hasDuplicates = false;

            // null �`�F�b�N�ƃ��j�[�N���̊m�F
            var uniqueItems = new HashSet<GameObject>();
            foreach (var prefab in _targetPrefabs)
            {
                if (prefab == null)
                {
                    hasNull = true;
                }
                else if (!uniqueItems.Add(prefab))
                {
                    hasDuplicates = true;
                }

                // null�Əd���̗��������������ꍇ�����I��
                if (hasNull && hasDuplicates) break;
            }

            // �d���܂���null������ꍇ�̂݃��X�g���č\�z
            if (hasNull || hasDuplicates)
            {
                _targetPrefabs = _targetPrefabs
                    .Where(prefab => prefab != null)
                    .Distinct()
                    .ToList();
            }
        }

        public bool IsValidPlacement() => EnsureValidPlacement();

        public string GetErrorMessage()
        {
            EnsureValidPlacement();
            return _errorMessage;
        }
#endif
    }
}