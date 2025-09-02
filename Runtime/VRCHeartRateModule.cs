using UnityEngine;
using VRC.SDKBase;

namespace BekoShop.VRCHeartRate
{
    /// <summary>
    /// OSC�S���v���W���[���̐ݒ���Ǘ�����R���|�[�l���g
    /// ���s���̓���ɂ͈�؊֗^���܂���iVRChat�r���h���ɂ͏�������܂��j
    /// </summary>
    [HelpURL("https://bekosantux.github.io/shop-document/category/vrc-heart-rate/")]
    public class VRCHeartRateModule : MonoBehaviour, IEditorOnly
    {
#if UNITY_EDITOR
        [Header("Heart Rate Control Settings")]
        [SerializeField, Tooltip("�S�����蓮����@�\���폜���ăp�����[�^�����팸���܂�")]
        private bool removeManualControl = false;

        [Header("GameObject References")]
        [SerializeField, Tooltip("�f�t�H���g�p�����[�^�p�Q�[���I�u�W�F�N�g")]
        private GameObject manualControlObject;

        [SerializeField, Tooltip("�팸�Ńp�����[�^�p�Q�[���I�u�W�F�N�g")]
        private GameObject autoControlObject;

        [SerializeField, Tooltip("���j���[�I�u�W�F�N�g")]
        private GameObject additionalSettingsObject;

        // �O���A�N�Z�X�p�v���p�e�B�iEditor��p�j
        public bool RemoveManualControl
        {
            get => removeManualControl;
            set
            {
                if (removeManualControl != value)
                {
                    removeManualControl = value;
                    // �`�F�b�N�{�b�N�X�̏�ԕύX���̂ݏ��������s
                    UpdateGameObjectStates();
                }
            }
        }

        public GameObject ManualControlObject
        {
            get => manualControlObject;
            set => manualControlObject = value;
        }

        public GameObject AutoControlObject
        {
            get => autoControlObject;
            set => autoControlObject = value;
        }

        public GameObject AdditionalSettingsObject
        {
            get => additionalSettingsObject;
            set => additionalSettingsObject = value;
        }

        /// <summary>
        /// �`�F�b�N�{�b�N�X�̏�Ԃɉ����ăQ�[���I�u�W�F�N�g�̗L����Ԃ��X�V
        /// </summary>
        public void UpdateGameObjectStates()
        {
            if (removeManualControl)
            {
                // �蓮������폜����ꍇ
                SetGameObjectState(manualControlObject, false, true);  // Disable + EditorOnly
                SetGameObjectState(autoControlObject, true, false);    // Enable + Default
                SetGameObjectState(additionalSettingsObject, false, true); // Disable + EditorOnly
            }
            else
            {
                // �蓮������ێ�����ꍇ
                SetGameObjectState(manualControlObject, true, false);  // Enable + Default
                SetGameObjectState(autoControlObject, false, true);   // Disable + EditorOnly
                SetGameObjectState(additionalSettingsObject, true, false); // Enable + Default
            }
        }

        /// <summary>
        /// �Q�[���I�u�W�F�N�g�̗L����Ԃƃ^�O��ݒ�
        /// </summary>
        private void SetGameObjectState(GameObject target, bool isActive, bool isEditorOnly)
        {
            if (target == null) return;

            target.SetActive(isActive);

            if (isEditorOnly)
            {
                target.tag = "EditorOnly";
            }
            else
            {
                // �f�t�H���g�^�O�ɖ߂�
                target.tag = "Untagged";
            }
        }
#endif
    }
}