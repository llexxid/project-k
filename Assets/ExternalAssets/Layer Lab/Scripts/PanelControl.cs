using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace LayerLab.GUIScripts
{
    public class PanelControl : MonoBehaviour
    {
        [SerializeField] private Transform panelTransformDefault;
        [SerializeField] private Transform panelTransformOther;
        [SerializeField] private Button buttonPrev;
        [SerializeField] private Button buttonNext;

        private readonly List<GameObject> _defaultPanels = new();
        private readonly List<GameObject> _otherPanels = new();
        private TextMeshProUGUI _textTitle;
        private int _page;
        private bool _isReady;
        private bool _isOtherMode;

        private bool HasOtherPanels => _otherPanels.Count > 0;
        private List<GameObject> ActivePanels => _isOtherMode && HasOtherPanels ? _otherPanels : _defaultPanels;

        private void OnValidate()
        {
            var panels = GameObject.Find("Panels");
            if (panels) panelTransformDefault = panels.transform;

            buttonPrev = transform.GetChild(0).GetComponent<Button>();
            buttonNext = transform.GetChild(2).GetComponent<Button>();
        }

        private void Reset()
        {
            OnValidate();
        }

        private void Start()
        {
            _textTitle = transform.GetComponentInChildren<TextMeshProUGUI>();
            buttonPrev.onClick.AddListener(() => Navigate(-1));
            buttonNext.onClick.AddListener(() => Navigate(1));

            CollectPanels(panelTransformDefault, _defaultPanels);

            if (panelTransformOther != null)
                CollectPanels(panelTransformOther, _otherPanels);

            _isReady = true;
            UpdateUI();
        }

        private void Update()
        {
            if (!_isReady || _defaultPanels.Count <= 0) return;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current.leftArrowKey.wasPressedThisFrame) Navigate(-1);
            else if (Keyboard.current.rightArrowKey.wasPressedThisFrame) Navigate(1);
#else
            if (Input.GetKeyDown(KeyCode.LeftArrow)) Navigate(-1);
            else if (Input.GetKeyDown(KeyCode.RightArrow)) Navigate(1);
#endif
        }

        private void Navigate(int direction)
        {
            int nextPage = _page + direction;
            if (nextPage < 0 || nextPage >= _defaultPanels.Count) return;

            SetPageActive(false);
            _page = nextPage;
            SetPageActive(true);
            UpdateUI();
        }

        private void SetPageActive(bool active)
        {
            _defaultPanels[_page].SetActive(active);
            if (HasOtherPanels) _otherPanels[_page].SetActive(active);
        }

        private void UpdateUI()
        {
            _textTitle.text = ActivePanels[_page].name.Replace("_", " ");
            buttonPrev.gameObject.SetActive(_page > 0);
            buttonNext.gameObject.SetActive(_page < _defaultPanels.Count - 1);
        }

        public void Click_Mode()
        {
            _isOtherMode = !_isOtherMode;
            panelTransformDefault.gameObject.SetActive(!_isOtherMode);
            if (HasOtherPanels) panelTransformOther.gameObject.SetActive(_isOtherMode);
            UpdateUI();
        }

        private static void CollectPanels(Transform parent, List<GameObject> list)
        {
            foreach (Transform t in parent)
            {
                list.Add(t.gameObject);
                t.gameObject.SetActive(false);
            }
            if (list.Count > 0) list[0].SetActive(true);
        }
    }
}
