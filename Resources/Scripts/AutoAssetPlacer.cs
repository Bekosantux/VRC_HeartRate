using UnityEngine;
using System.Collections.Generic;
using System.Linq;
#if UNITY_EDITOR
using nadena.dev.ndmf.util;
#endif

namespace BekoShop.VRCHeartRate
{
    public class AutoAssetPlacer : MonoBehaviour, VRC.SDKBase.IEditorOnly
    {
#if UNITY_EDITOR
        [SerializeField, HideInInspector]
        private List<GameObject> _targetPrefabs = new List<GameObject>();  // �V���A���C�Y�p�̃o�b�L���O�t�B�[���h

        // �O������A�N�Z�X�p�̃v���p�e�B
        public List<GameObject> targetPrefabs
        {
            get => _targetPrefabs;
            private set => _targetPrefabs = value;
        }

        private bool isValidPlacement = false;
        private string errorMessage = "";

        // Undo/Redo�Ŏ����z�u���Ȃ��悤�ɏ�Ԃ�ێ�
        private static bool suppressAutoPlacement = false;

        private void OnEnable()
        {
            // Undo/Redo�̃C�x���g�Ŏ����z�u��}��
            UnityEditor.Undo.undoRedoPerformed -= OnUndoRedo;
            UnityEditor.Undo.undoRedoPerformed += OnUndoRedo;
            // �q�G�����L�[�̈ړ��ł��Č���
            UnityEditor.EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            UnityEditor.EditorApplication.hierarchyChanged += OnHierarchyChanged;
            // ���������
            ValidateAndProcess();
        }

        private void OnDisable()
        {
            UnityEditor.Undo.undoRedoPerformed -= OnUndoRedo;
            UnityEditor.EditorApplication.hierarchyChanged -= OnHierarchyChanged;
        }

        private void OnUndoRedo()
        {
            // Undo/Redo���͎����z�u���Ȃ�
            suppressAutoPlacement = true;
            ValidateAndProcess();
            suppressAutoPlacement = false;
        }

        private void OnHierarchyChanged()
        {
            // �q�G�����L�[�ړ��ł��Č��؁E�x���X�V����
            ValidateAvatarRootPlacement();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        private void Start()
        {
            // �v���C���͉������Ȃ�
            if (Application.isPlaying)
                return;
            ValidateAndProcess();
        }

        private void OnValidate()
        {
            // �v���C���͉������Ȃ�
            if (Application.isPlaying)
                return;

            // �d��������
            RemoveDuplicatesFromList();
            ValidateAndProcess();
        }

        public void ValidateAndProcess()
        {
            if (!ValidateAvatarRootPlacement())
            {
                // �A�o�^�[�O�Ȃ牽�����Ȃ�
                return;
            }
            if (!suppressAutoPlacement)
            {
                CheckAndPlaceObjects();
            }
        }

        private bool ValidateAvatarRootPlacement()
        {
            try
            {
                // AvatarRootPath���擾
                string avatarRootPath = this.AvatarRootPath();

                if (string.IsNullOrEmpty(avatarRootPath))
                {
                    isValidPlacement = false;
                    errorMessage = "�A�o�^�[�̓����ɔz�u���Ă��������B\nPlease place this inside the avatar.";
                    return false;
                }

                isValidPlacement = true;
                errorMessage = "���̃X�N���v�g�͍폜���Ȃ��ł��������B";
                return true;
            }
            catch (System.Exception ex)
            {
                isValidPlacement = false;
                errorMessage = $"An error occurred while validating the avatar root: {ex.Message}";
                return false;
            }
        }

        private void CheckAndPlaceObjects()
        {
            if (targetPrefabs == null || targetPrefabs.Count == 0) return;

            Transform parentTransform = transform.parent;
            if (parentTransform == null) return; // �A�o�^�[�O�Ȃ炱���őł��؂�

            // �z�u����v���n�u�����W
            List<GameObject> prefabsToPlace = new List<GameObject>();

            foreach (GameObject prefab in targetPrefabs)
            {
                if (prefab == null) continue;

                bool objectExists = false;
                for (int i = 0; i < parentTransform.childCount; i++)
                {
                    Transform child = parentTransform.GetChild(i);
                    if (child == this.transform) continue;
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

            // �ꊇ�Ńv���n�u��z�u�i�P���Undo����Ƃ��āj
            if (prefabsToPlace.Count > 0)
            {
                PlaceNewObjects(prefabsToPlace);
            }
        }

        private void PlaceNewObjects(List<GameObject> prefabsToPlace)
        {
            if (prefabsToPlace == null || prefabsToPlace.Count == 0) return;
            if (transform.parent == null) return;

            // �P���Undo����Ƃ��đS�Ẵv���n�u��z�u
            UnityEditor.Undo.IncrementCurrentGroup();
            int group = UnityEditor.Undo.GetCurrentGroup();

            List<GameObject> createdObjects = new List<GameObject>();

            foreach (GameObject prefab in prefabsToPlace)
            {
                if (prefab == null) continue;

                GameObject newObject = UnityEditor.PrefabUtility.InstantiatePrefab(prefab, transform.parent) as GameObject;

                if (newObject != null)
                {
                    newObject.transform.localPosition = Vector3.zero;
                    newObject.transform.localRotation = Quaternion.identity;
                    newObject.transform.localScale = Vector3.one;

                    createdObjects.Add(newObject);
                    UnityEditor.Undo.RegisterCreatedObjectUndo(newObject, "Auto Place Objects");
                }
            }

            // �S�Ă̑����P���Undo����ɂ܂Ƃ߂�
            UnityEditor.Undo.CollapseUndoOperations(group);

            if (createdObjects.Count > 0)
            {
                Debug.Log($"AutoAssetPlacer: {createdObjects.Count}�̃I�u�W�F�N�g�������z�u���܂����B");
            }
        }

        // �d�����������郁�\�b�h
        private void RemoveDuplicatesFromList()
        {
            if (targetPrefabs == null) return;

            // null���������Ă���d��������
            var uniquePrefabs = targetPrefabs
                .Where(prefab => prefab != null)
                .Distinct()
                .ToList();

            if (uniquePrefabs.Count != targetPrefabs.Count)
            {
                targetPrefabs = uniquePrefabs;
            }
        }

        // �G�f�B�^�p�̃Z�b�g�A�b�v���\�b�h
        public void AddTargetPrefab(GameObject prefab)
        {
            if (prefab == null) return;
            if (targetPrefabs == null) targetPrefabs = new List<GameObject>();

            // �d���`�F�b�N
            if (!targetPrefabs.Contains(prefab))
            {
                targetPrefabs.Add(prefab);
            }
        }

        public void RemoveTargetPrefab(GameObject prefab)
        {
            if (targetPrefabs == null) return;
            targetPrefabs.Remove(prefab);
        }

        public void ClearTargetPrefabs()
        {
            if (targetPrefabs == null) targetPrefabs = new List<GameObject>();
            else targetPrefabs.Clear();
        }

        public bool IsValidPlacement()
        {
            ValidateAvatarRootPlacement();
            return isValidPlacement;
        }

        public string GetErrorMessage()
        {
            ValidateAvatarRootPlacement();
            return errorMessage;
        }
#endif
    }
}